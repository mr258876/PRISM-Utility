[CmdletBinding()]
param(
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot ('01V-3-inprocess-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$Executable = (Join-Path $PSScriptRoot '..\..\..\..\PRISM Utility\bin\x64\Release-01V3Baseline\PrismUtility.exe'),
    [switch]$CloseoutChecks
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RootQaNative {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr handle);
}
'@

function Find-Control($window, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Select-SafeTask($window, [string]$task, [int]$index) {
    $item = Find-Control $window "Workbench${task}TaskButton"
    if ($null -ne $item -and -not $item.Current.IsOffscreen -and $item.Current.IsEnabled) {
        $pattern = $null
        if (-not $item.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { throw "No task SelectionItemPattern: $task" }
        ([System.Windows.Automation.SelectionItemPattern]$pattern).Select()
        if (-not ([System.Windows.Automation.SelectionItemPattern]$pattern).Current.IsSelected) { throw "Task did not select: $task" }
        return "SelectorBar SelectionItem: $task"
    }
    $combo = Find-Control $window 'WorkbenchTaskComboBox'
    if ($null -eq $combo -or $combo.Current.IsOffscreen -or -not $combo.Current.IsEnabled) { throw "No visible task selector: $task" }
    $expand = $null
    if (-not $combo.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand)) { throw 'Task combo cannot expand' }
    ([System.Windows.Automation.ExpandCollapsePattern]$expand).Expand()
    try {
        $items = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
        if ($items.Count -ne 4) { throw "Expected four task options, got $($items.Count)" }
        $selection = $null
        if (-not $items[$index].TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) { throw "No task SelectionItemPattern: $task" }
        ([System.Windows.Automation.SelectionItemPattern]$selection).Select()
        if (-not ([System.Windows.Automation.SelectionItemPattern]$selection).Current.IsSelected) { throw "Task did not select: $task" }
        return "ComboBox SelectionItem: $task"
    }
    finally { ([System.Windows.Automation.ExpandCollapsePattern]$expand).Collapse() }
}

function Request-Qa($requestDirectory, [string]$kind, [string]$outputPath) {
    $id = 'root-baseline-' + [guid]::NewGuid().ToString('N')
    $path = Join-Path $requestDirectory ($id + '.request.json')
    $resultPath = Join-Path $requestDirectory ($id + '.request.result.json')
    $payload = @{ id = $id; kind = $kind; targetAutomationId = 'ScanDebugRootGrid'; outputPath = $outputPath } | ConvertTo-Json -Compress
    [IO.File]::WriteAllText(($path + '.pending'), $payload)
    [IO.File]::Move(($path + '.pending'), $path)
    for ($attempt = 0; $attempt -lt 75 -and -not (Test-Path -LiteralPath $resultPath); $attempt++) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "QA $kind timeout: $id" }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($result.id -ne $id -or -not $result.success) { throw "QA $kind failed: $($result.error)" }
    return $result
}

function Get-Bounds($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { return $null }
    $box = $element.Current.BoundingRectangle
    return @{ x = $box.X; y = $box.Y; width = $box.Width; height = $box.Height; offscreen = $element.Current.IsOffscreen }
}

function Get-TextState($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { throw "Required text not exposed by UIA: $id" }
    return [ordered]@{ text = $element.Current.Name; visible = -not $element.Current.IsOffscreen;
        boundsPhysical = (Get-Bounds $window $id) }
}

function Get-EditorValue($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { throw "Required editor not exposed by UIA: $id" }
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { throw "No ValuePattern on $id" }
    return ([System.Windows.Automation.ValuePattern]$pattern).Current.Value
}

function Set-EditorValue($window, [string]$id, [string]$text) {
    $element = Find-Control $window $id
    if ($null -eq $element -or $element.Current.IsOffscreen -or -not $element.Current.IsEnabled) { throw "Editable field unavailable: $id" }
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern) -or
        ([System.Windows.Automation.ValuePattern]$pattern).Current.IsReadOnly) { throw "Editable ValuePattern unavailable: $id" }
    ([System.Windows.Automation.ValuePattern]$pattern).SetValue($text)
}

function Get-VerticalScrollState($window) {
    $scroller = Find-Control $window 'ChannelCalibrationScrollViewer'
    if ($null -eq $scroller -or $scroller.Current.IsOffscreen) { throw 'BlackWhite parameter scroller unavailable' }
    $pattern = $null
    if (-not $scroller.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$pattern)) {
        throw 'BlackWhite parameter scroll offset cannot be verified by UIA'
    }
    $scroll = ([System.Windows.Automation.ScrollPattern]$pattern).Current
    return [ordered]@{ verticallyScrollable = $scroll.VerticallyScrollable; verticalPercent = $scroll.VerticalScrollPercent;
        boundsPhysical = (Get-Bounds $window 'ChannelCalibrationScrollViewer') }
}

function Assert-AtTop($scroll) {
    if (($scroll.verticallyScrollable -and $scroll.verticalPercent -gt 0.01) -or
        (-not $scroll.verticallyScrollable -and $scroll.verticalPercent -ne [System.Windows.Automation.ScrollPattern]::NoScroll)) {
        throw "BlackWhite parameter area is not at its default top position: $($scroll.verticalPercent)%"
    }
}

function Test-ViewIssuesButtonInViewport($window, $scroller, $button) {
    if ($button.Current.IsOffscreen -or -not $button.Current.IsEnabled) { return $false }
    $buttonBounds = $button.Current.BoundingRectangle
    $scrollBounds = $scroller.Current.BoundingRectangle
    $preview = Find-Control $window 'PreviewScrollViewer'
    if ($null -eq $preview) { throw 'Preview viewport unavailable for physical button visibility check' }
    $previewBounds = $preview.Current.BoundingRectangle
    return $buttonBounds.Width -gt 0 -and $buttonBounds.Height -gt 0 -and
        $buttonBounds.Left -ge $scrollBounds.Left -and $buttonBounds.Right -le $scrollBounds.Right -and
        $buttonBounds.Top -ge $scrollBounds.Top -and $buttonBounds.Bottom -le $previewBounds.Bottom
}

function Get-ExpandableElements($root) {
    $elements = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $expanders = @()
    foreach ($element in $elements) {
        $pattern = $null
        if (-not $element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { continue }
        $expanders += [pscustomobject]@{ element = $element; runtimeId = ($element.GetRuntimeId() -join '.');
            state = ([System.Windows.Automation.ExpandCollapsePattern]$pattern).Current.ExpandCollapseState.ToString() }
    }
    return $expanders
}

function Get-IssueEntries($root) {
    $elements = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $entries = @()
    foreach ($element in $elements) {
        $id = $element.Current.AutomationId
        if ($id -notlike 'FilmProfileIssue_CurrentDraft_*') { continue }
        $invoke = $null
        $entries += [ordered]@{ automationId = $id; name = $element.Current.Name;
            controlType = $element.Current.ControlType.ProgrammaticName; visible = -not $element.Current.IsOffscreen;
            enabled = $element.Current.IsEnabled; keyboardFocusable = $element.Current.IsKeyboardFocusable;
            hasKeyboardFocus = $element.Current.HasKeyboardFocus;
            navigable = $element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke);
            boundsPhysical = @{ x = $element.Current.BoundingRectangle.X; y = $element.Current.BoundingRectangle.Y;
                width = $element.Current.BoundingRectangle.Width; height = $element.Current.BoundingRectangle.Height } }
    }
    return $entries
}

function Inspect-Png([string]$path, [int]$expectedWidth, [int]$expectedHeight) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $signature = [byte[]](137,80,78,71,13,10,26,10)
    if ($bytes.Length -lt 33) { throw "Truncated PNG: $path" }
    for ($offset = 0; $offset -lt 8; $offset++) { if ($bytes[$offset] -ne $signature[$offset]) { throw "Bad PNG signature: $path" } }
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    try {
        if ($bitmap.Width -ne $expectedWidth -or $bitmap.Height -ne $expectedHeight) { throw "Root PNG dimensions drifted: $path" }
        $colors = @{}
        $nonBlack = 0
        for ($row = 1; $row -le 13; $row++) {
            for ($column = 1; $column -le 17; $column++) {
                $color = $bitmap.GetPixel([int]($bitmap.Width * $column / 18), [int]($bitmap.Height * $row / 14))
                $colors[$color.ToArgb()] = $true
                if ($color.R -gt 10 -or $color.G -gt 10 -or $color.B -gt 10) { $nonBlack++ }
            }
        }
        return @{ signature = '89504E470D0A1A0A'; width = $bitmap.Width; height = $bitmap.Height; bytes = $bytes.Length;
            sampleCount = 221; distinctSampleColors = $colors.Count; nonBlackSamples = $nonBlack;
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    finally { $bitmap.Dispose() }
}

function Capture-CloseoutRoot($window, [IntPtr]$handle, $requestDirectory, [string]$output, [string]$name) {
    $rect = New-Object RootQaNative+Rect
    if (-not [RootQaNative]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed for closeout' }
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    $dpi = [RootQaNative]::GetDpiForWindow($handle)
    if ($width -ne 2174 -or $height -ne 1440 -or $dpi -ne 192) { throw "Closeout window/DPI mismatch: $width x $height at $dpi DPI" }
    $geometry = (Request-Qa $requestDirectory 'geometry' '').state
    $root = $geometry.scanDebugRootGrid; $preview = $geometry.previewScrollViewer
    if ($geometry.xamlRootRasterizationScale -ne 2 -or $root.actualWidth -le 0 -or $root.actualHeight -le 0 -or
        $preview.visibleViewportWidth -le 0 -or $preview.visibleViewportHeight -le 0 -or
        -not $geometry.emptyStateCapture -or $geometry.previewFramePresent -or $geometry.pendingCalibrationPresent) {
        throw "Unexpected closeout Root/preview/source: $($geometry | ConvertTo-Json -Depth 8 -Compress)"
    }
    $path = Join-Path $output "$name-root.png"
    $capture = Request-Qa $requestDirectory 'capture' $path
    if ($capture.targetAutomationId -ne 'ScanDebugRootGrid') { throw "Wrong closeout capture target: $($capture.targetAutomationId)" }
    $png = Inspect-Png $path $capture.pixelWidth $capture.pixelHeight
    $after = (Request-Qa $requestDirectory 'geometry' '').state
    if ($after.scanDebugRootGrid.actualWidth -ne $root.actualWidth -or
        $after.scanDebugRootGrid.actualHeight -ne $root.actualHeight -or
        $after.previewScrollViewer.visibleViewportWidth -ne $preview.visibleViewportWidth -or
        $after.previewScrollViewer.visibleViewportHeight -ne $preview.visibleViewportHeight) {
        throw "Root or preview changed during closeout render: $path"
    }
    $pixelWidthDelta = $png.width - ($root.actualWidth * $geometry.xamlRootRasterizationScale)
    $pixelHeightDelta = $png.height - ($root.actualHeight * $geometry.xamlRootRasterizationScale)
    if ($pixelWidthDelta -lt 0 -or $pixelWidthDelta -gt 8 -or $pixelHeightDelta -ne 0) {
        throw "Unexpected RenderTargetBitmap dimensions for measured closeout Root: $path ($pixelWidthDelta, $pixelHeightDelta)"
    }
    if ($png.distinctSampleColors -lt 7 -or $png.nonBlackSamples -lt 40) { throw "Closeout Root bitmap nearly black: $path" }
    return [ordered]@{ name = $name; file = $path; completedUtc = $capture.completedUtc;
        noScrollOrFocusTargetRequested = $true; captureScope = 'ScanDebugRootGrid only; not Shell or full window';
        windowRectPhysical = @{ x = $rect.Left; y = $rect.Top; width = $width; height = $height };
        dpiForWindow = $dpi; geometry = $geometry; geometryAfterCapture = $after;
        bitmapPixelDeltaFromRoot = @{ width = $pixelWidthDelta; height = $pixelHeightDelta };
        rootBoundsPhysical = (Get-Bounds $window 'ScanDebugRootGrid');
        previewBoundsPhysical = (Get-Bounds $window 'PreviewScrollViewer'); png = $png }
}

[RootQaNative]::SetProcessDPIAware() | Out-Null
$exe = [IO.Path]::GetFullPath($Executable)
$dll = Join-Path (Split-Path -Parent $exe) 'PrismUtility.dll'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or -not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "QA executable/DLL missing: $exe / $dll" }
if ([RootQaNative]::GetSystemMetrics(0) -lt 3374 -or [RootQaNative]::GetSystemMetrics(1) -lt 1850) { throw 'Physical primary display cannot fit the specified wide window' }
$output = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $output) { throw "Refusing to touch existing evidence: $output" }
if (-not (Test-Path -LiteralPath (Split-Path -Parent $output) -PathType Container)) { throw "Missing evidence parent: $output" }
$oldTemp = $env:TEMP; $oldTmp = $env:TMP; $oldLocal = $env:LOCALAPPDATA
$oldSettings = $env:LocalSettingsOptions__ApplicationDataFolder
$oldEmpty = $env:PRISM_VISUAL_QA_EMPTY_STATE; $oldSection = $env:PRISM_VISUAL_QA_SECTION_INDEX
$oldMotion = $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED; $oldBuffer = $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER
$process = $null
$record = [ordered]@{ status = 'IN_PROGRESS'; captureScope = 'ScanDebugRootGrid only; NOT a full-window screenshot';
    sourceBuild = 'Selected executable supplied by operator; source/commit linkage not independently certified';
    executable = $exe; exeSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;
    assembly = $dll; dllSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash;
    dataSource = 'Conditional QA no-seed, offline; not device data or a preview frame'; shots = @() }
try {
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $env:TEMP = Join-Path $output 'isolated-temp'; $env:TMP = $env:TEMP
    $env:LOCALAPPDATA = Join-Path $output 'isolated-settings'
    $env:LocalSettingsOptions__ApplicationDataFolder = Join-Path $env:LOCALAPPDATA 'PRISM_Utility\ApplicationData'
    [IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    [IO.Directory]::CreateDirectory($env:LOCALAPPDATA) | Out-Null
    $env:PRISM_VISUAL_QA_EMPTY_STATE = '1'; $env:PRISM_VISUAL_QA_SECTION_INDEX = '1'
    $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED = '0'; $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER = '0'
    $requestDirectory = Join-Path $env:TEMP 'PRISM_Utility_VisualQaCaptureRequests'
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
    $record['processId'] = $process.Id
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        Start-Sleep -Milliseconds 400
        $process.Refresh()
        if ($process.HasExited) { throw "QA app exited before capture: $($process.ExitCode)" }
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero) { break }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'No native QA main window after 40 seconds' }
    $ready = Join-Path $requestDirectory 'ready.json'
    for ($attempt = 0; $attempt -lt 75; $attempt++) {
        if ((Test-Path -LiteralPath $ready) -and (Get-Item -LiteralPath $ready).LastWriteTimeUtc -ge $process.StartTime.ToUniversalTime()) { break }
        Start-Sleep -Milliseconds 200
    }
    if (-not (Test-Path -LiteralPath $ready) -or (Get-Item -LiteralPath $ready).LastWriteTimeUtc -lt $process.StartTime.ToUniversalTime()) { throw 'Fresh QA service ready marker missing' }
    $readyState = Get-Content -LiteralPath $ready -Raw | ConvertFrom-Json
    if (-not $readyState.ready -or $readyState.marker -ne 'PRISM_VISUAL_QA_PENDING_CALIBRATION_HOOK') { throw 'Wrong QA service marker' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $screens = @(@{ name = 'two-column'; width = 2174; height = 1440; rootWidth = 977; rootHeight = 604; previewWidth = 645; previewHeight = 396 },
                 @{ name = 'wide'; width = 3374; height = 1850; rootWidth = 1305; rootHeight = 809; previewWidth = 950; previewHeight = 601 })
    foreach ($screen in $screens) {
        if (-not [RootQaNative]::MoveWindow($handle, 100, 100, $screen.width, $screen.height, $true)) { throw "MoveWindow failed: $($screen.name)" }
        Start-Sleep -Seconds 2
        foreach ($task in @(@{ name = 'BlackWhite'; index = 1; scrollId = 'ChannelCalibrationScrollViewer' },
                             @{ name = 'Motion'; index = 3; scrollId = '' })) {
            $entry = Select-SafeTask $window $task.name $task.index
            Start-Sleep -Milliseconds 500
            $rect = New-Object RootQaNative+Rect
            if (-not [RootQaNative]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed' }
            $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
            $dpi = [RootQaNative]::GetDpiForWindow($handle)
            if ($width -ne $screen.width -or $height -ne $screen.height -or $dpi -ne 192) { throw "Window/DPI mismatch: $($screen.name) $width x $height at $dpi DPI" }
            $geometryResult = Request-Qa $requestDirectory 'geometry' ''
            $geometry = $geometryResult.state
            $root = $geometry.scanDebugRootGrid; $preview = $geometry.previewScrollViewer
            if ($geometry.xamlRootRasterizationScale -ne 2 -or $root.actualWidth -ne $screen.rootWidth -or $root.actualHeight -ne $screen.rootHeight -or
                $preview.visibleViewportWidth -ne $screen.previewWidth -or $preview.visibleViewportHeight -ne $screen.previewHeight -or
                -not $geometry.emptyStateCapture -or $geometry.previewFramePresent -or $geometry.pendingCalibrationPresent) {
                throw "Unexpected root/preview/source geometry for $($screen.name)-$($task.name): $($geometry | ConvertTo-Json -Depth 8 -Compress)"
            }
            $path = Join-Path $output "$($screen.name)-$($task.name)-root-default-empty.png"
            $capture = Request-Qa $requestDirectory 'capture' $path
            if ($capture.targetAutomationId -ne 'ScanDebugRootGrid') { throw "Wrong capture target: $($capture.targetAutomationId)" }
            $png = Inspect-Png $path $capture.pixelWidth $capture.pixelHeight
            $geometryAfter = (Request-Qa $requestDirectory 'geometry' '').state
            if ($geometryAfter.scanDebugRootGrid.actualWidth -ne $root.actualWidth -or
                $geometryAfter.scanDebugRootGrid.actualHeight -ne $root.actualHeight -or
                $geometryAfter.previewScrollViewer.visibleViewportWidth -ne $preview.visibleViewportWidth -or
                $geometryAfter.previewScrollViewer.visibleViewportHeight -ne $preview.visibleViewportHeight) {
                throw "Root or preview expanded during render: $path"
            }
            $pixelWidthDelta = $png.width - ($root.actualWidth * 2)
            $pixelHeightDelta = $png.height - ($root.actualHeight * 2)
            if ($pixelWidthDelta -lt 0 -or $pixelWidthDelta -gt 8 -or $pixelHeightDelta -ne 0) {
                throw "Unexpected RenderTargetBitmap dimensions for measured Root: $path ($pixelWidthDelta, $pixelHeightDelta)"
            }
            if ($png.distinctSampleColors -lt 7 -or $png.nonBlackSamples -lt 40) { throw "Root bitmap uncomposited or nearly black: $path; samples $($png.distinctSampleColors)/$($png.nonBlackSamples)" }
            $record.shots += [ordered]@{ name = "$($screen.name)-$($task.name)"; file = $path;
                completedUtc = $capture.completedUtc; entry = $entry; noScrollOrFocusTargetRequested = $true;
                windowRectPhysical = @{ x = $rect.Left; y = $rect.Top; width = $width; height = $height };
                dpiForWindow = $dpi; geometry = $geometry; geometryAfterCapture = $geometryAfter;
                bitmapPixelDeltaFromRoot = @{ width = $pixelWidthDelta; height = $pixelHeightDelta };
                rootBoundsPhysical = (Get-Bounds $window 'ScanDebugRootGrid');
                previewBoundsPhysical = (Get-Bounds $window 'PreviewScrollViewer'); png = $png }
            $record.status = 'CAPTURED_NOT_VISUALLY_REVIEWED'
            [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16), (New-Object Text.UTF8Encoding($false)))
        }
    }
    $record.status = 'FOUR_ROOT_PNGS_VALIDATED_NOT_FULL_WINDOW'
    if ($CloseoutChecks) {
        $record['closeoutChecks'] = [ordered]@{ status = 'IN_PROGRESS';
            scope = 'Two-column offline BlackWhite Root-only draft and View Issues, no Apply, Save or device command';
            shots = @() }
        $textScaleSetting = [ordered]@{ percent = $null;
            source = 'HKCU per-user TextScaleFactor if present; effective XAML text scale not independently measured' }
        if (Test-Path -LiteralPath 'HKCU:\Software\Microsoft\Accessibility') {
            $setting = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Accessibility' -Name TextScaleFactor -ErrorAction SilentlyContinue
            if ($null -ne $setting) { $textScaleSetting.percent = $setting.TextScaleFactor }
        }
        $record.closeoutChecks['textScaleSetting'] = $textScaleSetting
        if (-not [RootQaNative]::MoveWindow($handle, 100, 100, 2174, 1440, $true)) { throw 'MoveWindow failed: two-column closeout' }
        Start-Sleep -Seconds 2
        $record.closeoutChecks['entry'] = Select-SafeTask $window 'BlackWhite' 1
        Start-Sleep -Milliseconds 500
        Assert-AtTop (Get-VerticalScrollState $window)
        $baselineState = (Request-Qa $requestDirectory 'state' '').state
        $record.closeoutChecks['existingProfileValidationIssueCount'] = @($baselineState.currentFilmProfileValidationIssues).Count
        $record.closeoutChecks['existingProfileValidationIssues'] = $baselineState.currentFilmProfileValidationIssues
        $record.closeoutChecks['existingValidationSummaryUiA'] = Get-TextState $window 'BwValidationSummaryText'
        $record.closeoutChecks['existingDisabledReasonUiA'] = Get-TextState $window 'ManualReferenceDisabledReasonText'
        $record.closeoutChecks['blackBeforeDraft'] = Get-EditorValue $window 'ManualBlackLevelTextBox'
        $record.closeoutChecks['whiteBeforeDraft'] = Get-EditorValue $window 'ManualWhiteLevelTextBox'
        Set-EditorValue $window 'ManualBlackLevelTextBox' '65536'
        Start-Sleep -Milliseconds 500
        if ((Get-EditorValue $window 'ManualBlackLevelTextBox') -ne '65536') { throw 'BlackWhite draft not reflected by UIA' }
        $record.closeoutChecks['problemState'] = [ordered]@{
            kind = 'Unapplied out-of-range black draft; NOT a validated input error, profile issue, or device result';
            blackDraft = Get-EditorValue $window 'ManualBlackLevelTextBox';
            whiteDraft = Get-EditorValue $window 'ManualWhiteLevelTextBox';
            manualStatusUiA = Get-TextState $window 'ManualReferenceStatusText';
            validationSummaryUiA = Get-TextState $window 'BwValidationSummaryText';
            disabledReasonUiA = Get-TextState $window 'ManualReferenceDisabledReasonText';
            parameterScrollBefore = Get-VerticalScrollState $window }
        Assert-AtTop $record.closeoutChecks.problemState.parameterScrollBefore
        if (-not $record.closeoutChecks.problemState.manualStatusUiA.visible -or
            -not $record.closeoutChecks.problemState.disabledReasonUiA.visible) { throw 'Draft status or disabled reason is not visible before scrolling' }
        $record.closeoutChecks.problemState['validationSummaryBelowFirstScreen'] = -not $record.closeoutChecks.problemState.validationSummaryUiA.visible
        $problemShot = Capture-CloseoutRoot $window $handle $requestDirectory $output 'two-column-BlackWhite-unapplied-draft'
        $record.closeoutChecks.shots += $problemShot
        $record.closeoutChecks.problemState['parameterScrollAfterCapture'] = Get-VerticalScrollState $window
        Assert-AtTop $record.closeoutChecks.problemState.parameterScrollAfterCapture
        $defaultShot = @($record.shots | Where-Object { $_.name -eq 'two-column-BlackWhite' })[0]
        if ($problemShot.png.sha256 -eq $defaultShot.png.sha256) { throw 'Problem-state Root PNG is identical to the default BlackWhite PNG' }
        [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16), (New-Object Text.UTF8Encoding($false)))

        $parameterScroller = Find-Control $window 'ChannelCalibrationScrollViewer'
        $viewButton = Find-Control $window 'BwViewIssuesButton'
        if ($null -eq $parameterScroller -or $null -eq $viewButton) { throw 'BlackWhite parameter scroller or View Issues button unavailable' }
        $record.closeoutChecks['viewIssuesButtonBefore'] = @{ name = $viewButton.Current.Name;
            boundsPhysical = (Get-Bounds $window 'BwViewIssuesButton');
            physicallyVisible = (Test-ViewIssuesButtonInViewport $window $parameterScroller $viewButton) }
        $record.closeoutChecks['parameterScrollBeforeView'] = Get-VerticalScrollState $window
        $scrollPattern = $null
        if (-not $parameterScroller.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$scrollPattern)) {
            throw 'BlackWhite parameter scroller has no ScrollPattern'
        }
        $scrollSteps = 0
        while (-not (Test-ViewIssuesButtonInViewport $window $parameterScroller $viewButton) -and $scrollSteps -lt 20) {
            $priorPercent = (Get-VerticalScrollState $window).verticalPercent
            ([System.Windows.Automation.ScrollPattern]$scrollPattern).Scroll(
                [System.Windows.Automation.ScrollAmount]::NoAmount, [System.Windows.Automation.ScrollAmount]::SmallIncrement)
            $scrollSteps++
            Start-Sleep -Milliseconds 200
            if ((Get-VerticalScrollState $window).verticalPercent -le $priorPercent) { break }
        }
        $record.closeoutChecks['viewIssuesScroll'] = [ordered]@{ smallIncrements = $scrollSteps;
            before = $record.closeoutChecks.parameterScrollBeforeView; after = Get-VerticalScrollState $window;
            buttonPhysicallyVisible = (Test-ViewIssuesButtonInViewport $window $parameterScroller $viewButton);
            buttonBoundsPhysical = Get-Bounds $window 'BwViewIssuesButton' }
        if (-not $record.closeoutChecks.viewIssuesScroll.buttonPhysicallyVisible) { throw 'BwViewIssuesButton not fully visible after bounded parameter scrolling' }
        $beforeExpanders = @(Get-ExpandableElements $parameterScroller)
        $previousStates = @{}
        foreach ($expander in $beforeExpanders) { $previousStates[$expander.runtimeId] = $expander.state }
        $invoke = $null
        if (-not $viewButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { throw 'BwViewIssuesButton has no InvokePattern' }
        ([System.Windows.Automation.InvokePattern]$invoke).Invoke()
        $openedDetails = @()
        for ($attempt = 0; $attempt -lt 25; $attempt++) {
            Start-Sleep -Milliseconds 200
            $openedDetails = @(Get-ExpandableElements $parameterScroller | Where-Object {
                $_.state -eq 'Expanded' -and $previousStates[$_.runtimeId] -eq 'Collapsed'
            })
            if ($openedDetails.Count -gt 0) { break }
        }
        if ($openedDetails.Count -ne 1 -or $openedDetails[0].element.Current.IsOffscreen) {
            throw "View Issues did not expose one reachable BlackWhite details expander; transitions: $($openedDetails.Count)"
        }
        $details = $openedDetails[0]
        $detailsBounds = $details.element.Current.BoundingRectangle
        Start-Sleep -Milliseconds 800
        $entries = @(Get-IssueEntries $details.element)
        $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
        $focusedId = if ($null -ne $focused) { $focused.Current.AutomationId } else { $null }
        $focusedIssueEntry = if ($entries.Count -gt 0) {
            @($entries | Where-Object { $_.automationId -eq $focusedId -and $_.visible -and $_.keyboardFocusable }).Count -gt 0
        } else { $null }
        $record.closeoutChecks['openedIssueUiA'] = [ordered]@{
            settleMilliseconds = 800;
            detailsExpanderReachable = $true; detailsExpanderState = $details.state;
            detailsExpanderRuntimeId = $details.runtimeId;
            detailsExpanderAutomationId = $details.element.Current.AutomationId;
            detailsExpanderName = $details.element.Current.Name;
            detailsExpanderBoundsPhysical = @{ x = $detailsBounds.X; y = $detailsBounds.Y;
                width = $detailsBounds.Width; height = $detailsBounds.Height };
            detailsCardBoundsPhysicalIfExposed = Get-Bounds $window 'ChannelCalibrationCurrentFilmProfileValidationCard';
            issueEntries = $entries; issueEntryCount = $entries.Count;
            issueEntryResult = if (@($entries | Where-Object { $_.visible }).Count -gt 0) { 'At least one actual profile issue entry UIA-visible; no issue action invoked' } elseif ($entries.Count -gt 0) { 'Profile issue entries exposed but all UIA-offscreen; no issue action invoked' } else { 'Details reachable; no BlackWhite profile issue entry exposed; local draft remains unvalidated' };
            focusedIssueEntry = $focusedIssueEntry;
            focusedAutomationId = if ($null -ne $focused) { $focused.Current.AutomationId } else { $null };
            focusedName = if ($null -ne $focused) { $focused.Current.Name } else { $null };
            parameterScrollAfterView = Get-VerticalScrollState $window }
        [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16), (New-Object Text.UTF8Encoding($false)))
        $expandedShot = Capture-CloseoutRoot $window $handle $requestDirectory $output 'two-column-BlackWhite-view-issues-expanded'
        $record.closeoutChecks.shots += $expandedShot
        if ($expandedShot.png.sha256 -eq $problemShot.png.sha256) { throw 'Expanded Root PNG is identical to the pre-view PNG' }
        $afterExpanders = @(Get-ExpandableElements $parameterScroller | Where-Object { $_.runtimeId -eq $details.runtimeId -and $_.state -eq 'Expanded' })
        if ($afterExpanders.Count -ne 1 -or $afterExpanders[0].element.Current.IsOffscreen) { throw 'BlackWhite validation details became unreachable during capture' }
        $entriesAfter = @(Get-IssueEntries $afterExpanders[0].element)
        $record.closeoutChecks['issueEntriesAfterCaptureUiA'] = $entriesAfter
        $record.closeoutChecks['detailsCardAfterCaptureBoundsPhysicalIfExposed'] = Get-Bounds $window 'ChannelCalibrationCurrentFilmProfileValidationCard'
        $record.closeoutChecks['parameterScrollAfterExpandedCapture'] = Get-VerticalScrollState $window
        $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
        $focusedId = if ($null -ne $focused) { $focused.Current.AutomationId } else { $null }
        $focusedIssueEntry = @($entriesAfter | Where-Object { $_.automationId -eq $focusedId -and $_.visible -and $_.keyboardFocusable }).Count -gt 0
        $record.closeoutChecks['focusAfterExpandedCapture'] = [ordered]@{
            focusedIssueEntry = $focusedIssueEntry; focusedAutomationId = $focusedId;
            focusedName = if ($null -ne $focused) { $focused.Current.Name } else { $null } }
        [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16), (New-Object Text.UTF8Encoding($false)))
        if ($entriesAfter.Count -gt 0 -and @($entriesAfter | Where-Object { $_.visible }).Count -eq 0) {
            $record.closeoutChecks.status = 'FAIL'
            $record.status = 'FAIL'
            throw 'View Issues opened but no reported issue entry is visible after bounded settle and Root capture'
        }
        if ($entriesAfter.Count -gt 0 -and -not $focusedIssueEntry) { throw 'View Issues exposed entries but did not focus a visible keyboard-focusable issue entry' }
        $record.closeoutChecks.status = 'TWO_ADDITIONAL_ROOT_PNGS_VALIDATED_NOT_FULL_WINDOW'
        $record.status = 'FOUR_BASELINE_PLUS_TWO_BW_CLOSEOUT_ROOT_PNGS_VALIDATED_NOT_FULL_WINDOW'
    }
}
catch {
    if ($record.status -ne 'FAIL') { $record.status = 'BLOCKED' }
    $record['error'] = $_.Exception.Message
    if ($CloseoutChecks -and $record.Contains('closeoutChecks') -and $record.closeoutChecks.status -ne 'FAIL') { $record.closeoutChecks.status = 'BLOCKED' }
    throw
}
finally {
    if (Test-Path -LiteralPath $output -PathType Container) {
        [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16), (New-Object Text.UTF8Encoding($false)))
    }
    if ($null -ne $process -and -not $process.HasExited) {
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        }
        else { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    }
    $env:TEMP = $oldTemp; $env:TMP = $oldTmp; $env:LOCALAPPDATA = $oldLocal
    $env:LocalSettingsOptions__ApplicationDataFolder = $oldSettings
    $env:PRISM_VISUAL_QA_EMPTY_STATE = $oldEmpty; $env:PRISM_VISUAL_QA_SECTION_INDEX = $oldSection
    $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED = $oldMotion; $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER = $oldBuffer
}
$record | ConvertTo-Json -Depth 5
