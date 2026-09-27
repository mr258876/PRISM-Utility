param(
    [string]$Executable = 'PRISM Utility/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/PrismUtility.exe',
    [switch]$QaEmptyState
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NativeQaWindow {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr handle);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[NativeQaWindow]::SetProcessDPIAware() | Out-Null
$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $outDir '../../../../..')).Path
$exe = (Resolve-Path (Join-Path $repo $Executable)).Path
$previousEmptyState = $env:PRISM_VISUAL_QA_EMPTY_STATE
if ($QaEmptyState) { $env:PRISM_VISUAL_QA_EMPTY_STATE = '1' }
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
$env:PRISM_VISUAL_QA_EMPTY_STATE = $previousEmptyState
try {
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        Start-Sleep -Milliseconds 400
        $process.Refresh()
        if ($process.HasExited) { throw "App exited before window appeared: $($process.ExitCode)" }
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero) { break }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'No app main window after 40 seconds' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $nav = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'Shell_ScanDebug')))
    if ($null -eq $nav) {
        $nav = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '扫描调试')))
    }
    if ($null -eq $nav) {
        $menu = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'MenuItemsHost')))
        if ($null -eq $menu) { throw 'Shell MenuItemsHost was not exposed by UI Automation' }
        $items = $menu.FindAll([System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
        if ($items.Count -ne 4) { throw "Expected 4 Shell navigation entries; got $($items.Count)" }
        $nav = $items[3]
    }
    $selection = $null
    if ($nav.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
        ([System.Windows.Automation.SelectionItemPattern]$selection).Select()
    } else {
        $invocation = $null
        if (-not $nav.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invocation)) {
            throw "Navigation item '$($nav.Current.Name)' lacks selection and invocation patterns"
        }
        ([System.Windows.Automation.InvokePattern]$invocation).Invoke()
    }
    Start-Sleep -Seconds 3

    $condition = { param($id) New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id) }
    $screens = @(
        @{ Name = 'zh-CN-two-column-empty'; Width = 2350; Height = 1440 },
        @{ Name = 'zh-CN-wide-empty'; Width = 3550; Height = 1850 }
    )
    $result = @()
    $requestDirectory = Join-Path $env:TEMP 'PRISM_Utility_VisualQaCaptureRequests'
    foreach ($screen in $screens) {
        $screenWidth = [NativeQaWindow]::GetSystemMetrics(0)
        $screenHeight = [NativeQaWindow]::GetSystemMetrics(1)
        if ($screen.Width -gt $screenWidth -or $screen.Height -gt $screenHeight) { continue }
        if (-not [NativeQaWindow]::MoveWindow($handle, 100, 100, $screen.Width, $screen.Height, $true)) {
            throw "MoveWindow failed for $($screen.Name)"
        }
        Start-Sleep -Seconds 2
        for ($foregroundAttempt = 0; $foregroundAttempt -lt 12; $foregroundAttempt++) {
            [NativeQaWindow]::SetForegroundWindow($handle) | Out-Null
            Start-Sleep -Milliseconds 250
            if ([NativeQaWindow]::GetForegroundWindow() -eq $handle) { break }
        }
        if ([NativeQaWindow]::GetForegroundWindow() -ne $handle) { throw "App not foreground for $($screen.Name); refusing occluded screenshot" }
        $rect = New-Object NativeQaWindow+Rect
        if (-not [NativeQaWindow]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed' }
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        $png = Join-Path $outDir ($screen.Name + $(if ($QaEmptyState) { '-qa' } else { '' }) + '.png')
        $elements = @{}
        foreach ($id in @('ScanDebugRootGrid', 'WorkbenchPreviewColumnContent', 'PreviewScrollViewer', 'PreviewEmptyStateGrid', 'WorkbenchInspectionRail', 'WorkbenchSamplingTaskButton')) {
            $element = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (& $condition $id))
            if ($null -ne $element) {
                $bounds = $element.Current.BoundingRectangle
                $elements[$id] = @{ x = $bounds.X; y = $bounds.Y; widthPhysical = $bounds.Width; heightPhysical = $bounds.Height; offscreen = $element.Current.IsOffscreen; name = $element.Current.Name }
            }
        }
        $geometry = $null
        if ($QaEmptyState) {
            $id = '01v-' + [guid]::NewGuid().ToString('N')
            $requestPath = Join-Path $requestDirectory ($id + '.request.json')
            $responsePath = Join-Path $requestDirectory ($id + '.request.result.json')
            $readyPath = Join-Path $requestDirectory 'ready.json'
            for ($attempt = 0; $attempt -lt 75; $attempt++) {
                if ((Test-Path -LiteralPath $readyPath) -and (Get-Item -LiteralPath $readyPath).LastWriteTimeUtc -ge $process.StartTime.ToUniversalTime()) { break }
                Start-Sleep -Milliseconds 200
            }
            if (-not (Test-Path -LiteralPath $readyPath) -or (Get-Item -LiteralPath $readyPath).LastWriteTimeUtc -lt $process.StartTime.ToUniversalTime()) { throw 'QA capture service is not ready for this process' }
            $request = @{ id = $id; kind = 'geometry'; targetAutomationId = 'ScanDebugRootGrid'; outputPath = '' }
            [System.IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json -Compress))
            for ($attempt = 0; $attempt -lt 75 -and -not (Test-Path -LiteralPath $responsePath); $attempt++) {
                Start-Sleep -Milliseconds 200
            }
            if (-not (Test-Path -LiteralPath $responsePath)) { throw "Geometry request timed out: $id" }
            $response = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
            if (-not $response.success) { throw "Geometry request failed: $($response.error)" }
            $geometry = $response.state
            if (-not $geometry.emptyStateCapture -or $geometry.previewFramePresent -or $geometry.pendingCalibrationPresent) {
                throw "Not an honest empty state: $id"
            }
        }
        if ([NativeQaWindow]::GetForegroundWindow() -ne $handle) { throw "App lost foreground for $($screen.Name); refusing screenshot" }
        $bitmap = New-Object System.Drawing.Bitmap($width, $height)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size) }
            finally { $graphics.Dispose() }
            $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $bitmap.Dispose() }
        $result += @{
            name = $screen.Name; file = $png; utc = (Get-Date).ToUniversalTime().ToString('o');
            processId = $process.Id; windowTitle = $window.Current.Name;
            requestedWindowSizePhysical = @($screen.Width, $screen.Height);
            windowRectPhysical = @{ x = $rect.Left; y = $rect.Top; width = $width; height = $height };
            monitorPhysical = @($screenWidth, $screenHeight);
            dpiForWindow = [NativeQaWindow]::GetDpiForWindow($handle);
            dpiDerivedScale = ([NativeQaWindow]::GetDpiForWindow($handle) / 96.0);
            xamlRootRasterizationScale = $(if ($geometry) { $geometry.xamlRootRasterizationScale } else { $null });
            scanDebugRootGridActualWidth = $(if ($geometry) { $geometry.scanDebugRootGrid.actualWidth } else { $null });
            scanDebugRootGridActualHeight = $(if ($geometry) { $geometry.scanDebugRootGrid.actualHeight } else { $null });
            previewViewportActualWidth = $(if ($geometry) { $geometry.previewScrollViewer.viewportWidth } else { $null });
            previewViewportActualHeight = $(if ($geometry) { $geometry.previewScrollViewer.viewportHeight } else { $null });
            qaGeometry = $geometry;
            uiaBoundsPhysical = $elements
        }
    }
    $outputName = if ($QaEmptyState) { 'native-inprocess-measurements.json' } else { 'uia-window-measurements.json' }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outDir $outputName) -Encoding UTF8
    $result | ConvertTo-Json -Depth 8
}
finally {
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null }
}
