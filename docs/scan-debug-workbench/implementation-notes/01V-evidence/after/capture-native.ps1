[CmdletBinding()]
param(
    [string]$Executable = 'PRISM Utility/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/PrismUtility.exe',
    [ValidateSet('Empty', 'Synthetic')][string]$Source = 'Empty',
    [switch]$CompactAndWide,
    [int]$TwoColumnPhysicalWidth = 2350,
    [int]$TwoColumnPhysicalHeight = 1440,
    [int]$WidePhysicalWidth = 3550,
    [int]$WidePhysicalHeight = 1850,
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PostQaNative {
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

function Find-Control($root, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Use-Selection($element) {
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        throw "SelectionItemPattern unavailable: $($element.Current.AutomationId)"
    }
    ([System.Windows.Automation.SelectionItemPattern]$pattern).Select()
}
function Get-SelectedTask($window) {
    $combo = Find-Control $window 'WorkbenchTaskComboBox'
    if ($null -ne $combo -and -not $combo.Current.IsOffscreen) {
        $pattern = $null
        if (-not $combo.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern, [ref]$pattern)) { throw 'Compact task selection is not readable' }
        $selected = ([System.Windows.Automation.SelectionPattern]$pattern).Current.GetSelection()
        if ($selected.Count -ne 1) { throw 'Compact task selection is not singular' }
        return $selected[0].Current.Name
    }
    foreach ($task in @('Sampling', 'BlackWhite', 'Focus', 'Motion')) {
        $item = Find-Control $window "Workbench${task}TaskButton"
        $pattern = $null
        if ($null -ne $item -and $item.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern) -and
            ([System.Windows.Automation.SelectionItemPattern]$pattern).Current.IsSelected) { return $task }
    }
    throw 'Selected task not exposed by UI Automation'
}
function Invoke-SafeButton($window, [string]$id) {
    $button = Find-Control $window $id
    if ($null -eq $button -or $button.Current.IsOffscreen -or -not $button.Current.IsEnabled -or
        $button.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { throw "Safe visible button unavailable: $id" }
    $pattern = $null
    if (-not $button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { throw "Button cannot invoke: $id" }
    ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
    Start-Sleep -Milliseconds 500
}
function Test-InspectionVisible($window) {
    return $null -ne (Find-Control $window 'InspectionEvidenceExpander')
}
function Select-Task($window, [string]$task, [int]$index) {
    $taskIds = @('WorkbenchSamplingTaskButton', 'WorkbenchBlackWhiteTaskButton', 'WorkbenchFocusTaskButton', 'WorkbenchMotionTaskButton')
    $selector = Find-Control $window 'WorkbenchTaskSelectorBar'
    if ($null -eq $selector) { $selector = Find-Control $window 'WorkbenchTaskSelector' }
    if ($null -ne $selector -and -not $selector.Current.IsOffscreen -and $selector.Current.IsEnabled) {
        $item = $selector.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "Workbench${task}TaskButton")))
        if ($null -eq $item -or $item.Current.IsOffscreen -or -not $item.Current.IsEnabled) { throw "Task SelectorBar item unavailable: $task" }
        Use-Selection $item
        return "SelectorBar SelectionItem: $($item.Current.Name)"
    }
    $button = Find-Control $window ("Workbench${task}TaskButton")
    if ($null -ne $button -and -not $button.Current.IsOffscreen -and $button.Current.IsEnabled) {
        if ($button.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            foreach ($taskId in $taskIds) {
                $peer = Find-Control $window $taskId
                if ($null -eq $peer -or $peer.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) { throw "Task SelectorBar peer unavailable: $taskId" }
            }
            Use-Selection $button
            return "SelectorBarItem SelectionItem: $($button.Current.Name)"
        }
        if ($button.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { throw "Unexpected task button type for $task" }
        $pattern = $null
        if (-not $button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { throw "No InvokePattern on $task task button" }
        ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
        return "Task button Invoke: $($button.Current.Name)"
    }
    $combo = Find-Control $window 'WorkbenchTaskComboBox'
    if ($null -eq $combo -or $combo.Current.IsOffscreen -or -not $combo.Current.IsEnabled -or $combo.Current.ControlType -ne [System.Windows.Automation.ControlType]::ComboBox) {
        throw "No recognized visible safe task control for $task (expected WorkbenchTaskSelectorBar, Workbench${task}TaskButton or WorkbenchTaskComboBox)"
    }
    $expand = $null
    if (-not $combo.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand)) { throw 'Compact task combo cannot expand' }
    ([System.Windows.Automation.ExpandCollapsePattern]$expand).Expand()
    try {
        $items = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
        if ($items.Count -ne 4) { throw "Compact task combo must expose exactly four options, got $($items.Count)" }
        Use-Selection $items[$index]
        return "Compact ComboBox SelectionItem: $($items[$index].Current.Name)"
    }
    finally { ([System.Windows.Automation.ExpandCollapsePattern]$expand).Collapse() }
}
function Request-Geometry([string]$requestDir, $process) {
    $ready = Join-Path $requestDir 'ready.json'
    for ($attempt = 0; $attempt -lt 75; $attempt++) {
        if ((Test-Path -LiteralPath $ready) -and (Get-Item -LiteralPath $ready).LastWriteTimeUtc -ge $process.StartTime.ToUniversalTime()) { break }
        Start-Sleep -Milliseconds 200
    }
    if (-not (Test-Path -LiteralPath $ready) -or (Get-Item -LiteralPath $ready).LastWriteTimeUtc -lt $process.StartTime.ToUniversalTime()) { throw 'QA service not ready for this process' }
    $readyState = Get-Content -LiteralPath $ready -Raw | ConvertFrom-Json
    if (-not $readyState.ready -or $readyState.marker -ne 'PRISM_VISUAL_QA_PENDING_CALIBRATION_HOOK') { throw 'Unrecognized QA service ready marker' }
    $id = '01v-after-' + [guid]::NewGuid().ToString('N')
    $requestPath = Join-Path $requestDir ($id + '.request.json')
    $resultPath = Join-Path $requestDir ($id + '.request.result.json')
    [IO.File]::WriteAllText($requestPath, (@{ id = $id; kind = 'geometry'; targetAutomationId = 'ScanDebugRootGrid'; outputPath = '' } | ConvertTo-Json -Compress))
    for ($attempt = 0; $attempt -lt 75 -and -not (Test-Path -LiteralPath $resultPath); $attempt++) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Geometry timeout: $id" }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($result.id -ne $id -or -not $result.success) { throw "Geometry failed: $($result.error)" }
    return $result
}
function Capture-Shot($window, [IntPtr]$handle, $process, [string]$requestDir, [string]$name, [string]$source, [string]$entry, [bool]$allowHiddenPreview = $false) {
    [PostQaNative]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 650
    if ([PostQaNative]::GetForegroundWindow() -ne $handle) { throw "Not foreground: $name" }
    $rect = New-Object PostQaNative+Rect
    if (-not [PostQaNative]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($name.StartsWith('two-column-') -and ($width -ne $TwoColumnPhysicalWidth -or $height -ne $TwoColumnPhysicalHeight)) { throw "Two-column shot size drifted: $name ($width x $height)" }
    $expectedWideWidth = if ($CompactAndWide) { 3550 } else { $WidePhysicalWidth }
    $expectedWideHeight = if ($CompactAndWide) { 1850 } else { $WidePhysicalHeight }
    if ($name.StartsWith('wide-') -and ($width -ne $expectedWideWidth -or $height -ne $expectedWideHeight)) { throw "Wide shot size drifted: $name ($width x $height)" }
    if ($name.StartsWith('compact-') -and ($width -ne 1840 -or $height -ne 1440)) { throw "Compact shot size drifted: $name ($width x $height)" }
    $dpi = [PostQaNative]::GetDpiForWindow($handle)
    if ($dpi -ne 192) { throw "Expected current 192 DPI, got $dpi" }
    $geometryResult = Request-Geometry $requestDir $process
    $geometry = $geometryResult.state
    if ($null -eq $geometry -or $geometry.xamlRootRasterizationScale -le 0 -or $geometry.scanDebugRootGrid.actualWidth -le 0 -or
        (-not $allowHiddenPreview -and $geometry.previewScrollViewer.viewportWidth -le 0)) {
        throw "Incomplete in-process geometry: $name"
    }
    if ($source -eq 'Empty' -and (-not $geometry.emptyStateCapture -or $geometry.previewFramePresent -or $geometry.pendingCalibrationPresent)) {
        throw "Empty source was not empty: $name"
    }
    if ($source -eq 'Synthetic' -and ($geometry.emptyStateCapture -or -not $geometry.previewFramePresent)) { throw "Synthetic preview not present: $name" }
    if ([PostQaNative]::GetForegroundWindow() -ne $handle) { throw "Foreground changed during geometry request: $name" }
    $path = Join-Path $script:outputDir ($name + '.png')
    if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite shot: $path" }
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size) }
        finally { $graphics.Dispose() }
        if ([PostQaNative]::GetForegroundWindow() -ne $handle) { throw "Foreground changed during screenshot: $name" }
        $samples = @{}
        for ($row = 1; $row -le 7; $row++) {
            for ($column = 1; $column -le 7; $column++) {
                $samples[$bitmap.GetPixel([int]($width * $column / 8), [int]($height * $row / 8)).ToArgb()] = $true
            }
        }
        if ($samples.Count -lt 2) { throw "Capture appears uncomposited or uniform: $name" }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }
    $bytes = [IO.File]::ReadAllBytes($path)
    $signature = [byte[]](137,80,78,71,13,10,26,10)
    if ($bytes.Length -lt 33) { throw "Truncated PNG: $path" }
    for ($i = 0; $i -lt 8; $i++) { if ($bytes[$i] -ne $signature[$i]) { throw "Invalid PNG signature: $path" } }
    $image = [System.Drawing.Image]::FromFile($path)
    try { if ($image.Width -ne $width -or $image.Height -ne $height) { throw "PNG dimensions differ from window: $path" } }
    finally { $image.Dispose() }
    $bounds = @{}
    foreach ($id in @('ScanDebugRootGrid', 'PreviewScrollViewer', 'WorkbenchInspectionRail', 'ManualBlackLevelTextBox', 'ManualWhiteLevelTextBox')) {
        $element = Find-Control $window $id
        if ($null -ne $element) {
            $box = $element.Current.BoundingRectangle
            $bounds[$id] = @{ x = $box.X; y = $box.Y; width = $box.Width; height = $box.Height; offscreen = $element.Current.IsOffscreen }
        }
    }
    return [ordered]@{
        name = $name; file = $path; capturedUtc = (Get-Date).ToUniversalTime().ToString('o'); geometryCompletedUtc = $geometryResult.completedUtc
        processId = $process.Id; entry = $entry; dataSource = $(if ($source -eq 'Empty') { 'conditional QA no-seed; no preview; offline device state must be confirmed visually' } else { 'SYNTHETIC QA FIXTURE; not a device measurement' })
        windowTitle = $window.Current.Name; windowRectPhysical = @{ x = $rect.Left; y = $rect.Top; width = $width; height = $height }
        dpiForWindow = $dpi; xamlRootRasterizationScale = $geometry.xamlRootRasterizationScale
        scanDebugRootGrid = $geometry.scanDebugRootGrid; previewViewport = $geometry.previewScrollViewer
        qaGeometry = $geometry; uiaBoundsPhysical = $bounds; pngSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        compositing = 'PNG signature/dimensions and foreground verified; visually inspect entire screenshot for black/missing compositor regions and occlusion; NOT AUTOMATICALLY PASSED'
    }
}
function Record-Supplemental($window, [IntPtr]$handle, $process, [string]$requestDir, [string]$name, [string]$entry, [bool]$hiddenPreview) {
    $shot = Capture-Shot $window $handle $process $requestDir $name 'Empty' $entry $hiddenPreview
    $shot['selectedTask'] = Get-SelectedTask $window
    $shot['inspectionVisible'] = Test-InspectionVisible $window
    $shot['previewVisible'] = $shot.previewViewport.visibility -eq 0 -and $shot.previewViewport.visibleViewportHeight -gt 0
    $script:supplemental.shots += $shot
    [IO.File]::WriteAllText((Join-Path $script:outputDir 'native-inprocess-measurements.json'), ($script:supplemental | ConvertTo-Json -Depth 15), (New-Object Text.UTF8Encoding($false)))
    return $shot
}

[PostQaNative]::SetProcessDPIAware() | Out-Null
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
$exe = [IO.Path]::GetFullPath((Join-Path $repo $Executable))
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Prebuilt QA executable missing: $exe" }
$script:outputDir = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $outputDir) { throw "Use a new evidence directory, never overwrite: $outputDir" }
    $oldTemp = $env:TEMP; $oldTmp = $env:TMP; $oldLocal = $env:LOCALAPPDATA
    $oldSettingsFolder = $env:LocalSettingsOptions__ApplicationDataFolder
    $oldEmpty = $env:PRISM_VISUAL_QA_EMPTY_STATE; $oldSection = $env:PRISM_VISUAL_QA_SECTION_INDEX
    $oldMotion = $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED; $oldBuffer = $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER
$process = $null
try {
    [IO.Directory]::CreateDirectory($outputDir) | Out-Null
    $env:TEMP = Join-Path $outputDir 'isolated-temp'
    $env:TMP = $env:TEMP
    $env:LOCALAPPDATA = Join-Path $outputDir 'isolated-settings'
    $env:LocalSettingsOptions__ApplicationDataFolder = Join-Path $env:LOCALAPPDATA 'PRISM_Utility\ApplicationData'
    [IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    [IO.Directory]::CreateDirectory($env:LOCALAPPDATA) | Out-Null
    if (-not [IO.Path]::IsPathRooted($env:LocalSettingsOptions__ApplicationDataFolder) -or
        -not [IO.Path]::GetFullPath($env:LocalSettingsOptions__ApplicationDataFolder).StartsWith([IO.Path]::GetFullPath($outputDir) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Configured app settings folder is not within the isolated evidence directory; refusing launch'
    }
    $env:PRISM_VISUAL_QA_EMPTY_STATE = $(if ($Source -eq 'Empty') { '1' } else { '0' })
    $env:PRISM_VISUAL_QA_SECTION_INDEX = '1'
    $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED = '0'
    $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER = '0'
    $requestDir = Join-Path $env:TEMP 'PRISM_Utility_VisualQaCaptureRequests'
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        Start-Sleep -Milliseconds 400
        $process.Refresh()
        if ($process.HasExited) { throw "QA app exited early: $($process.ExitCode)" }
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero) { break }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'No native main window after 40 seconds' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $ready = Join-Path $requestDir 'ready.json'
    for ($attempt = 0; $attempt -lt 75; $attempt++) {
        if ((Test-Path -LiteralPath $ready) -and (Get-Item -LiteralPath $ready).LastWriteTimeUtc -ge $process.StartTime.ToUniversalTime()) { break }
        Start-Sleep -Milliseconds 200
    }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'QA startup did not activate ScanDebug' }
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 500
        $window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
        if (($null -ne (Find-Control $window 'WorkbenchSamplingTaskButton') -or $null -ne (Find-Control $window 'WorkbenchTaskComboBox')) -and
            $null -ne (Find-Control $window 'PreviewScrollViewer')) { break }
    }
    if (($null -eq (Find-Control $window 'WorkbenchSamplingTaskButton') -and $null -eq (Find-Control $window 'WorkbenchTaskComboBox')) -or
        $null -eq (Find-Control $window 'PreviewScrollViewer')) {
        [PostQaNative]::SetForegroundWindow($handle) | Out-Null
        $failureRect = New-Object PostQaNative+Rect
        [PostQaNative]::GetWindowRect($handle, [ref]$failureRect) | Out-Null
        $failureImage = New-Object System.Drawing.Bitmap(($failureRect.Right - $failureRect.Left), ($failureRect.Bottom - $failureRect.Top))
        try {
            $failureGraphics = [System.Drawing.Graphics]::FromImage($failureImage)
            try { $failureGraphics.CopyFromScreen($failureRect.Left, $failureRect.Top, 0, 0, $failureImage.Size) }
            finally { $failureGraphics.Dispose() }
            $failureImage.Save((Join-Path $outputDir 'navigation-failure.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $failureImage.Dispose() }
        $visible = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $identifiers = @($visible | ForEach-Object { $_.Current.AutomationId } | Where-Object { $_ -match 'Scan|Navigation|Content|Error' } | Sort-Object -Unique)
        $names = @($visible | ForEach-Object { "$($_.Current.ControlType.ProgrammaticName):$($_.Current.Name):$($_.Current.AutomationId)" })
        [IO.File]::WriteAllLines((Join-Path $outputDir 'navigation-uia-tree.txt'), $names)
        throw "ScanDebug page did not open; $($visible.Count) descendants, visible IDs: $($identifiers -join ', ')"
    }
    if ($CompactAndWide) {
        if ($Source -ne 'Empty') { throw 'Supplemental scenario requires no-seed Empty source' }
        $script:supplemental = [ordered]@{ status = 'IN_PROGRESS'; source = 'conditional QA no-seed; not production device data'; executable = $exe; scenario = 'wide inspection before compact override; compact BlackWhite draft and view transitions'; shots = @(); checks = [ordered]@{} }
        if ([PostQaNative]::GetSystemMetrics(0) -lt 3550 -or [PostQaNative]::GetSystemMetrics(1) -lt 1850) { throw 'Primary display cannot fit wide screenshot' }
        if (-not [PostQaNative]::MoveWindow($handle, 100, 100, 3550, 1850, $true)) { throw 'Wide MoveWindow failed' }
        Start-Sleep -Seconds 2
        Select-Task $window 'Sampling' 0 | Out-Null
        Start-Sleep -Milliseconds 400
        if (-not (Test-InspectionVisible $window)) { Invoke-SafeButton $window 'WorkbenchInspectionButton' }
        if (-not (Test-InspectionVisible $window)) { throw 'Wide inspection controls not exposed after safe toggle' }
        $wideShot = Record-Supplemental $window $handle $process $requestDir 'wide-inspection-open-empty' 'Fresh wide state, before any narrow inspection override' $false
        if (-not $wideShot.inspectionVisible -or -not $wideShot.previewVisible -or $wideShot.previewViewport.visibleViewportWidth -ge 800) { throw 'Wide screenshot lacks three-column inspector or preview' }
        $script:supplemental.checks.wideInspector = 'PASS'

        if (-not [PostQaNative]::MoveWindow($handle, 100, 100, 1840, 1440, $true)) { throw 'Compact MoveWindow failed' }
        Start-Sleep -Seconds 2
        Select-Task $window 'BlackWhite' 1 | Out-Null
        Start-Sleep -Milliseconds 400
        $selected = Get-SelectedTask $window
        $initial = Record-Supplemental $window $handle $process $requestDir 'compact-editor-default-empty' 'BlackWhite editor first screen before draft or view toggle' $true
        if ($initial.previewVisible -or $initial.inspectionVisible) { throw 'Initial compact editor unexpectedly displays preview or inspector' }
        $editor = Find-Control $window 'ManualBlackLevelTextBox'
        $value = $null
        if ($null -eq $editor -or $editor.Current.IsOffscreen -or -not $editor.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value) -or
            ([System.Windows.Automation.ValuePattern]$value).Current.IsReadOnly) { throw 'Safe manual black editor unavailable' }
        ([System.Windows.Automation.ValuePattern]$value).SetValue('513')
        Start-Sleep -Milliseconds 400
        if (([System.Windows.Automation.ValuePattern]$value).Current.Value -ne '513') { throw 'Unsaved black draft not entered' }
        $draft = Record-Supplemental $window $handle $process $requestDir 'compact-editor-draft-empty' 'Unsaved black draft 513, no apply/save' $true
        Invoke-SafeButton $window 'OpenPreviewButton'
        $preview = Record-Supplemental $window $handle $process $requestDir 'compact-preview-open-empty' 'Explicit OpenPreview only' $false
        if (-not $preview.previewVisible -or $preview.inspectionVisible) { throw 'Compact preview not visible after OpenPreview' }
        Invoke-SafeButton $window 'BackToEditorButton'
        $returned = Record-Supplemental $window $handle $process $requestDir 'compact-editor-return-empty' 'BackToEditor only; verify unsaved draft' $true
        $editor = Find-Control $window 'ManualBlackLevelTextBox'
        $value = $null
        if ($null -eq $editor -or -not $editor.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value) -or
            ([System.Windows.Automation.ValuePattern]$value).Current.Value -ne '513') { throw 'Manual black draft lost on return' }
        Invoke-SafeButton $window 'WorkbenchInspectionButton'
        $open = Record-Supplemental $window $handle $process $requestDir 'compact-inspection-open-empty' 'Inspection toggle open only' $true
        if (-not $open.inspectionVisible) { throw 'Compact inspector not visible after toggle' }
        Invoke-SafeButton $window 'WorkbenchInspectionButton'
        $closed = Record-Supplemental $window $handle $process $requestDir 'compact-inspection-closed-empty' 'Inspection toggle close only' $true
        $editor = Find-Control $window 'ManualBlackLevelTextBox'
        $value = $null
        if ($closed.inspectionVisible -or $null -eq $editor -or -not $editor.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value) -or
            ([System.Windows.Automation.ValuePattern]$value).Current.Value -ne '513') { throw 'Compact editor/draft not restored after inspection' }
        foreach ($shot in $script:supplemental.shots) { if ($shot.selectedTask -ne $(if ($shot.name.StartsWith('wide-')) { 'Sampling' } else { $selected })) { throw "Task selection changed: $($shot.name)" } }
        $script:supplemental.checks.compactTaskBeforeAfter = "PASS: $selected"
        $script:supplemental.checks.unsavedBlackDraftBeforeAfter = 'PASS: 513 before preview and after return/inspection close; no apply/save'
        $script:supplemental.checks.compactPreviewAndInspection = 'PASS: explicit open/back/inspection open/close'
        $script:supplemental.status = 'CAPTURED_NOT_VISUALLY_REVIEWED'
        [IO.File]::WriteAllText((Join-Path $outputDir 'native-inprocess-measurements.json'), ($script:supplemental | ConvertTo-Json -Depth 15), (New-Object Text.UTF8Encoding($false)))
        $script:supplemental | ConvertTo-Json -Depth 3
        return
    }
    $screens = @(@{ name = 'two-column'; width = $TwoColumnPhysicalWidth; height = $TwoColumnPhysicalHeight }, @{ name = 'wide'; width = $WidePhysicalWidth; height = $WidePhysicalHeight })
    $shots = @()
    foreach ($screen in $screens) {
        if ($screen.width -gt [PostQaNative]::GetSystemMetrics(0) -or $screen.height -gt [PostQaNative]::GetSystemMetrics(1)) { throw "Monitor cannot fit $($screen.name) physical window" }
        if (-not [PostQaNative]::MoveWindow($handle, 100, 100, $screen.width, $screen.height, $true)) { throw "MoveWindow failed: $($screen.name)" }
        Start-Sleep -Seconds 2
        $windowRect = New-Object PostQaNative+Rect
        if (-not [PostQaNative]::GetWindowRect($handle, [ref]$windowRect) -or ($windowRect.Right - $windowRect.Left) -ne $screen.width -or ($windowRect.Bottom - $windowRect.Top) -ne $screen.height) { throw "Window size not matched for $($screen.name)" }
        foreach ($task in @(@{ key = 'Sampling'; index = 0 }, @{ key = 'BlackWhite'; index = 1 }, @{ key = 'Focus'; index = 2 }, @{ key = 'Motion'; index = 3 })) {
            $entry = Select-Task $window $task.key $task.index
            Start-Sleep -Milliseconds 500
            $shots += Capture-Shot $window $handle $process $requestDir "$($screen.name)-$($task.key)-default-$($Source.ToLowerInvariant())" $Source "$entry; default first screen, no scroll"
        }
    }
    $manifestPath = Join-Path $outputDir 'native-inprocess-measurements.json'
    $manifest = [ordered]@{ status = 'DEFAULTS_CAPTURED_NOT_VISUALLY_REVIEWED'; source = $Source; executable = $exe; sourceBuild = 'operator-provided prebuilt PrismVisualQa=true; runtime marker checked, build provenance not certified'; optionalInteractions = 'NOT_RUN'; shellNavigation = 'NOT_RUN'; draftAfterTaskSwitch = 'NOT_RUN'; shots = $shots }
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 15), (New-Object Text.UTF8Encoding($false)))
    try {
    $menu = Find-Control $window 'MenuItemsHost'
    if ($null -eq $menu) { throw 'Shell navigation menu unavailable' }
    $navItems = $menu.FindAll([System.Windows.Automation.TreeScope]::Children,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
    if ($navItems.Count -ne 4) { throw "Expected four Shell navigation entries, got $($navItems.Count)" }
    Use-Selection $navItems[1]
    Start-Sleep -Seconds 2
    if ($null -ne (Find-Control $window 'ScanDebugRootGrid')) { throw 'ScanDebug remained active after navigating to Log' }
    Use-Selection $navItems[3]
    Start-Sleep -Seconds 2
    if ((Get-SelectedTask $window) -ne 'Sampling') { throw 'QA no-seed hook did not restore its configured initial task' }
    $restored = (Request-Geometry $requestDir $process).state
    if ($restored.scanDebugRootGrid.actualWidth -ne $shots[7].scanDebugRootGrid.actualWidth -or
        $restored.scanDebugRootGrid.actualHeight -ne $shots[7].scanDebugRootGrid.actualHeight) { throw 'ScanDebug Root geometry changed after Shell navigation round-trip' }
    $manifest.shellNavigation = 'PASS: Log then ScanDebug restores Root geometry; QA no-seed hook forces Sampling on every Page Loaded, so task-memory is not verified'
    foreach ($screen in $screens) {
        if (-not [PostQaNative]::MoveWindow($handle, 100, 100, $screen.width, $screen.height, $true)) { throw "MoveWindow failed: $($screen.name) interaction" }
        Start-Sleep -Seconds 2
        $entry = Select-Task $window 'BlackWhite' 1
        Start-Sleep -Milliseconds 400
        foreach ($id in @('ManualBlackLevelTextBox', 'ManualWhiteLevelTextBox')) {
            $field = Find-Control $window $id
            if ($null -eq $field -or $field.Current.ControlType -ne [System.Windows.Automation.ControlType]::Edit) { throw "Expected safe editor: $id" }
            $field.SetFocus()
            Start-Sleep -Milliseconds 350
            $shots += Capture-Shot $window $handle $process $requestDir "$($screen.name)-$id-focus-$($Source.ToLowerInvariant())" $Source "$entry; keyboard focus only; field may scroll into view"
            $value = $null
            if (-not $field.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value) -or ([System.Windows.Automation.ValuePattern]$value).Current.IsReadOnly) { throw "Non-editable field: $id" }
            ([System.Windows.Automation.ValuePattern]$value).SetValue($(if ($id -eq 'ManualBlackLevelTextBox') { '513' } else { '60000' }))
            Start-Sleep -Milliseconds 350
            $shots += Capture-Shot $window $handle $process $requestDir "$($screen.name)-$id-draft-$($Source.ToLowerInvariant())" $Source "$entry; unsaved non-dispatch editor draft; field may scroll into view"
        }
        Select-Task $window 'Sampling' 0 | Out-Null
        Select-Task $window 'BlackWhite' 1 | Out-Null
        foreach ($draft in @(@{ id = 'ManualBlackLevelTextBox'; expected = '513' }, @{ id = 'ManualWhiteLevelTextBox'; expected = '60000' })) {
            $field = Find-Control $window $draft.id
            $value = $null
            if ($null -eq $field -or -not $field.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value) -or
                ([System.Windows.Automation.ValuePattern]$value).Current.Value -ne $draft.expected) { throw "Unapplied draft lost after task switch: $($draft.id)" }
        }
        $manifest.draftAfterTaskSwitch = 'PASS: 513/60000 editor values survived Sampling/BlackWhite view switch; Apply and Save not invoked'
        $entry = Select-Task $window 'Sampling' 0
        Start-Sleep -Milliseconds 450
        if ($screen.name -eq 'two-column') {
            $inspectionButton = Find-Control $window 'WorkbenchInspectionButton'
            if ($null -eq $inspectionButton -or $inspectionButton.Current.IsOffscreen -or $inspectionButton.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { throw 'Two-column inspection toggle unavailable' }
            $toggle = $null
            if (-not $inspectionButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$toggle)) { throw 'Inspection toggle cannot be invoked' }
            ([System.Windows.Automation.InvokePattern]$toggle).Invoke()
            Start-Sleep -Milliseconds 450
            $shots += Capture-Shot $window $handle $process $requestDir "$($screen.name)-inspection-$($Source.ToLowerInvariant())" $Source 'Inspection toggle only; no device action; explicitly not a default screen'
        }
        else {
            if (-not (Test-InspectionVisible $window)) { Invoke-SafeButton $window 'WorkbenchInspectionButton' }
            if (-not (Test-InspectionVisible $window)) { throw 'Wide inspection controls not exposed after safe toggle' }
            $shots += Capture-Shot $window $handle $process $requestDir "$($screen.name)-inspection-$($Source.ToLowerInvariant())" $Source 'Wide inspection opened explicitly; no device action'
        }
        $flyout = Find-Control $window 'PreviewDisplayToolsButton'
        if ($null -eq $flyout -or $flyout.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button -or $flyout.Current.IsOffscreen) { throw 'Safe direct preview display button unavailable' }
        $invoke = $null
        if (-not $flyout.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { throw 'Preview display button cannot open flyout' }
        ([System.Windows.Automation.InvokePattern]$invoke).Invoke()
        Start-Sleep -Milliseconds 400
        $shots += Capture-Shot $window $handle $process $requestDir "$($screen.name)-preview-flyout-$($Source.ToLowerInvariant())" $Source "$entry; preview display flyout only; no menu command invoked"
    }
    $manifest.optionalInteractions = 'CAPTURED_NOT_VISUALLY_REVIEWED'
    }
    catch {
        $manifest.optionalInteractions = "FAIL: $($_.Exception.Message); prior default screenshots retained"
    }
    $manifest.shots = $shots
    $manifest.status = 'CAPTURED_NOT_VISUALLY_REVIEWED'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 15), (New-Object Text.UTF8Encoding($false)))
    $manifest | ConvertTo-Json -Depth 5
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        if ($process.MainWindowHandle -eq [IntPtr]::Zero) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        else {
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        }
    }
    $env:TEMP = $oldTemp; $env:TMP = $oldTmp; $env:LOCALAPPDATA = $oldLocal
    $env:LocalSettingsOptions__ApplicationDataFolder = $oldSettingsFolder
    $env:PRISM_VISUAL_QA_EMPTY_STATE = $oldEmpty; $env:PRISM_VISUAL_QA_SECTION_INDEX = $oldSection
    $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED = $oldMotion; $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER = $oldBuffer
}
