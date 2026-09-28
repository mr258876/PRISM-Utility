[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$EvidenceDirectory,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Executable,
    [switch]$TwoColumnOnlyAt192
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
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr handle);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
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

function Get-FiniteCoordinate([double]$value) {
    if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) { return $null }
    return $value
}

function Get-PhysicalBounds($element) {
    $box = $element.Current.BoundingRectangle
    return @{ x = Get-FiniteCoordinate $box.X; y = Get-FiniteCoordinate $box.Y;
        width = Get-FiniteCoordinate $box.Width; height = Get-FiniteCoordinate $box.Height;
        offscreen = $element.Current.IsOffscreen }
}

function Get-Bounds($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { return $null }
    return Get-PhysicalBounds $element
}

function Test-FiniteBounds($bounds) {
    return $null -ne $bounds -and -not $bounds.offscreen -and
        $null -ne $bounds.x -and $null -ne $bounds.y -and
        $null -ne $bounds.width -and $null -ne $bounds.height -and
        -not [double]::IsNaN($bounds.x) -and -not [double]::IsInfinity($bounds.x) -and
        -not [double]::IsNaN($bounds.y) -and -not [double]::IsInfinity($bounds.y) -and
        -not [double]::IsNaN($bounds.width) -and -not [double]::IsInfinity($bounds.width) -and
        -not [double]::IsNaN($bounds.height) -and -not [double]::IsInfinity($bounds.height) -and
        $bounds.width -gt 0 -and $bounds.height -gt 0
}

function Test-Inside($bounds, $viewport) {
    if (-not (Test-FiniteBounds $bounds) -or -not (Test-FiniteBounds $viewport)) { return $false }
    return $bounds.x -ge ($viewport.x - 0.5) -and $bounds.y -ge ($viewport.y - 0.5) -and
        ($bounds.x + $bounds.width) -le ($viewport.x + $viewport.width + 0.5) -and
        ($bounds.y + $bounds.height) -le ($viewport.y + $viewport.height + 0.5)
}

function Get-Intersection($first, $second) {
    if (-not (Test-FiniteBounds $first) -or -not (Test-FiniteBounds $second)) { return $null }
    $left = [math]::Max($first.x, $second.x); $top = [math]::Max($first.y, $second.y)
    $right = [math]::Min(($first.x + $first.width), ($second.x + $second.width))
    $bottom = [math]::Min(($first.y + $first.height), ($second.y + $second.height))
    if ($right -le $left -or $bottom -le $top) { return $null }
    return @{ x = $left; y = $top; width = $right - $left; height = $bottom - $top; offscreen = $false }
}

function Get-VisibleRailBounds($window) {
    return Get-Intersection (Get-Bounds $window 'ChannelCalibrationSection') $script:rootBoundsPhysical
}

function Measure-RootBounds($window, $geometry, $screen, [string]$name) {
    $root = $geometry.scanDebugRootGrid; $preview = $geometry.previewScrollViewer
    $scale = [double]$geometry.xamlRootRasterizationScale
    if ([double]::IsNaN($scale) -or [double]::IsInfinity($scale) -or $scale -le 0 -or
        $scale -ne $screen.rasterizationScale -or -not $geometry.emptyStateCapture -or
        $geometry.previewFramePresent -or $geometry.pendingCalibrationPresent) {
        throw "Unexpected XAML scale/offline source for $name`: expected scale $($screen.rasterizationScale); actual $($geometry | ConvertTo-Json -Depth 8 -Compress)"
    }
    $rootSize = @{ x = 0; y = 0; width = $root.actualWidth; height = $root.actualHeight; offscreen = $false }
    $previewSize = @{ x = 0; y = 0; width = $preview.actualWidth; height = $preview.actualHeight; offscreen = $false }
    if (-not (Test-FiniteBounds $rootSize) -or
        [math]::Abs($root.actualWidth - $screen.rootWidth) -gt 0.5 -or
        [math]::Abs($root.actualHeight - $screen.rootHeight) -gt 0.5) {
        throw "Root DIP mismatch for $name`: expected $($screen.rootWidth) x $($screen.rootHeight); actual $($root.actualWidth) x $($root.actualHeight)"
    }
    if (-not (Test-FiniteBounds $previewSize) -or
        $preview.visibleViewportWidth -lt ($screen.previewWidth - 0.01) -or
        $preview.visibleViewportHeight -lt ($screen.previewHeight - 0.01)) {
        throw "Preview DIP below floor for $name`: minimum $($screen.previewWidth) x $($screen.previewHeight); actual $($preview.visibleViewportWidth) x $($preview.visibleViewportHeight)"
    }
    $rootRect = $root.rectInPage; $previewRect = $preview.rectInPage
    if ($null -eq $rootRect -or $null -eq $previewRect) { throw "Root/preview page rectangles missing: $name" }
    $rootPage = @{ x = $rootRect.x; y = $rootRect.y; width = $rootRect.width; height = $rootRect.height; offscreen = $false }
    $previewPage = @{ x = $previewRect.x; y = $previewRect.y; width = $previewRect.width; height = $previewRect.height; offscreen = $false }
    if (-not (Test-FiniteBounds $rootPage) -or -not (Test-FiniteBounds $previewPage) -or
        [math]::Abs(($rootPage.width - $root.actualWidth) * $scale) -gt 1 -or
        [math]::Abs(($rootPage.height - $root.actualHeight) * $scale) -gt 1 -or
        [math]::Abs(($previewPage.width - $preview.actualWidth) * $scale) -gt 1 -or
        [math]::Abs(($previewPage.height - $preview.actualHeight) * $scale) -gt 1) {
        throw "Invalid Root/preview page rectangles for $name"
    }
    $previewBounds = Get-Bounds $window 'PreviewScrollViewer'
    if (-not (Test-FiniteBounds $previewBounds) -or
        [math]::Abs($previewBounds.width - $previewPage.width * $scale) -gt 1 -or
        [math]::Abs($previewBounds.height - $previewPage.height * $scale) -gt 1 -or
        [math]::Abs($previewBounds.width - $preview.actualWidth * $scale) -gt 1 -or
        [math]::Abs($previewBounds.height - $preview.actualHeight * $scale) -gt 1) {
        throw "Preview UIA physical bounds disagree with QA geometry at $name"
    }
    $pageX = $previewBounds.x - $previewPage.x * $scale
    $pageY = $previewBounds.y - $previewPage.y * $scale
    $rootBounds = @{ x = $pageX + $rootPage.x * $scale; y = $pageY + $rootPage.y * $scale;
        width = $rootPage.width * $scale; height = $rootPage.height * $scale; offscreen = $false }
    if (-not (Test-FiniteBounds $rootBounds)) { throw "Root physical bounds invalid: $name" }
    if ($rootBounds.x -lt 0 -or $rootBounds.y -lt 0 -or
        ($rootBounds.x + $rootBounds.width) -gt [RootQaNative]::GetSystemMetrics(0) -or
        ($rootBounds.y + $rootBounds.height) -gt [RootQaNative]::GetSystemMetrics(1) -or
        ([math]::Round($rootBounds.x) + [math]::Round($rootBounds.width)) -gt [RootQaNative]::GetSystemMetrics(0) -or
        ([math]::Round($rootBounds.y) + [math]::Round($rootBounds.height)) -gt [RootQaNative]::GetSystemMetrics(1)) {
        throw "Root extends beyond the primary display; refusing partial native capture: $name"
    }
    return @{ rootBoundsPhysical = $rootBounds; previewBoundsPhysical = $previewBounds }
}

function Assert-VisibleInRail($window, [string]$id) {
    $bounds = Get-Bounds $window $id
    if (-not (Test-Inside $bounds (Get-VisibleRailBounds $window))) {
        throw "$id is not fully visible in the BlackWhite rail and Root"
    }
    return $bounds
}

function Get-BwSummaryBounds($window) {
    return [ordered]@{ activeRoi = Assert-VisibleInRail $window 'BwActiveRoiRangeText';
        shieldRoi = Assert-VisibleInRail $window 'BwShieldRoiRangeText';
        validation = Assert-VisibleInRail $window 'BwValidationSummaryText';
        roiEdit = Assert-VisibleInRail $window 'BwRoiEditButton';
        viewIssues = Assert-VisibleInRail $window 'BwViewIssuesButton' }
}

function Get-TextState($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { throw "Required text not exposed by UIA: $id" }
    return [ordered]@{ text = $element.Current.Name; visible = -not $element.Current.IsOffscreen;
        boundsPhysical = (Get-Bounds $window $id) }
}

function Get-OptionalTextState($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { return $null }
    return [ordered]@{ text = $element.Current.Name; visible = -not $element.Current.IsOffscreen;
        boundsPhysical = Get-PhysicalBounds $element }
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
            boundsPhysical = Get-PhysicalBounds $element }
    }
    return $entries
}

function Find-DetailViewport($window, $rail, $issueEntry) {
    $items = Find-Control $window 'ChannelCalibrationIssueItemsControl'
    $origin = if ($null -ne $items) { $items } else { $issueEntry }
    if ($null -eq $origin) { return $null }
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    $railId = $rail.GetRuntimeId() -join '.'
    for ($node = $origin; $null -ne $node; $node = $walker.GetParent($node)) {
        if (($node.GetRuntimeId() -join '.') -eq $railId) { break }
        $pattern = $null
        if (-not $node.Current.IsOffscreen -and
            $node.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$pattern)) {
            $bounds = $node.Current.BoundingRectangle
            $viewport = @{ x = $bounds.X; y = $bounds.Y; width = $bounds.Width; height = $bounds.Height; offscreen = $false }
            $visibleViewport = Get-Intersection $viewport (Get-VisibleRailBounds $window)
            if ($null -ne $visibleViewport) {
                return [ordered]@{ automationId = $node.Current.AutomationId; boundsPhysical = $visibleViewport }
            }
        }
    }
    return $null
}

function Find-ReturnButton($rail) {
    $button = Find-Control $rail 'BwReturnToParametersButton'
    if ($null -eq $button -or $button.Current.IsOffscreen -or -not $button.Current.IsEnabled) { return $null }
    return $button
}

function Get-FocusState {
    $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
    if ($null -eq $focused) { return $null }
    return [ordered]@{ automationId = $focused.Current.AutomationId; name = $focused.Current.Name;
        boundsPhysical = Get-PhysicalBounds $focused }
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

function Capture-Root($window, [IntPtr]$handle, $requestDirectory, [string]$output, $screen, [string]$name) {
    $rect = New-Object RootQaNative+Rect
    if (-not [RootQaNative]::GetWindowRect($handle, [ref]$rect)) { throw "GetWindowRect failed: $name" }
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    $dpi = [RootQaNative]::GetDpiForWindow($handle)
    if ($width -ne $screen.width -or $height -ne $screen.height -or $dpi -ne $screen.dpi) {
        throw "Window/DPI mismatch for $name`: expected $($screen.width) x $($screen.height) at $($screen.dpi) DPI; actual $width x $height at $dpi DPI"
    }
    $geometry = (Request-Qa $requestDirectory 'geometry' '').state
    $measurement = Measure-RootBounds $window $geometry $screen $name
    $script:rootBoundsPhysical = $measurement.rootBoundsPhysical
    $root = $geometry.scanDebugRootGrid; $preview = $geometry.previewScrollViewer
    $rootBounds = $measurement.rootBoundsPhysical
    if ([RootQaNative]::GetForegroundWindow() -ne $handle) { throw "QA window is not foreground; refusing occluded native capture: $name" }
    $pixelWidth = [int][math]::Round($rootBounds.width)
    $pixelHeight = [int][math]::Round($rootBounds.height)
    $path = Join-Path $output "$name-root.png"
    $bitmap = [System.Drawing.Bitmap]::new($pixelWidth, $pixelHeight)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen([int][math]::Round($rootBounds.x), [int][math]::Round($rootBounds.y),
                0, 0, $bitmap.Size, [System.Drawing.CopyPixelOperation]::SourceCopy)
        }
        finally { $graphics.Dispose() }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }
    $png = Inspect-Png $path $pixelWidth $pixelHeight
    $after = (Request-Qa $requestDirectory 'geometry' '').state
    if ([math]::Abs($after.scanDebugRootGrid.actualWidth - $root.actualWidth) -gt 0.5 -or
        [math]::Abs($after.scanDebugRootGrid.actualHeight - $root.actualHeight) -gt 0.5 -or
        [math]::Abs($after.previewScrollViewer.visibleViewportWidth - $preview.visibleViewportWidth) -gt 0.5 -or
        [math]::Abs($after.previewScrollViewer.visibleViewportHeight - $preview.visibleViewportHeight) -gt 0.5 -or
        $after.previewScrollViewer.visibleViewportWidth -lt ($screen.previewWidth - 0.01) -or
        $after.previewScrollViewer.visibleViewportHeight -lt ($screen.previewHeight - 0.01)) {
        throw "Root or preview changed or fell below its DIP floor during closeout render: $path; before Root $($root.actualWidth) x $($root.actualHeight), preview $($preview.visibleViewportWidth) x $($preview.visibleViewportHeight); after Root $($after.scanDebugRootGrid.actualWidth) x $($after.scanDebugRootGrid.actualHeight), preview $($after.previewScrollViewer.visibleViewportWidth) x $($after.previewScrollViewer.visibleViewportHeight)"
    }
    $pixelWidthDelta = $png.width - ($root.actualWidth * $geometry.xamlRootRasterizationScale)
    $pixelHeightDelta = $png.height - ($root.actualHeight * $geometry.xamlRootRasterizationScale)
    if ([math]::Abs($pixelWidthDelta) -gt 1 -or [math]::Abs($pixelHeightDelta) -gt 1) {
        throw "Unexpected native capture dimensions for measured Root: $path ($pixelWidthDelta, $pixelHeightDelta)"
    }
    if ($png.distinctSampleColors -lt 7 -or $png.nonBlackSamples -lt 40) { throw "Native Root bitmap nearly black: $path" }
    return [ordered]@{ name = $name; file = $path; completedUtc = [DateTime]::UtcNow.ToString('o');
        captureMethod = 'Screen pixels cropped to QA-measured Root anchored to physical UIA PreviewScrollViewer; no QA capture hook, scrolling or focus request';
        captureScope = 'ScanDebugRootGrid only; not Shell or full window';
        windowRectPhysical = @{ x = $rect.Left; y = $rect.Top; width = $width; height = $height };
        dpiForWindow = $dpi; geometry = $geometry; geometryAfterCapture = $after;
        bitmapPixelDeltaFromRoot = @{ width = $pixelWidthDelta; height = $pixelHeightDelta };
        rootBoundsPhysical = $rootBounds;
        previewBoundsPhysical = $measurement.previewBoundsPhysical; png = $png }
}

function Open-Issues($window, [string]$screenName, [switch]$AllowEditorScroll) {
    $rail = Find-Control $window 'ChannelCalibrationSection'
    $button = Find-Control $window 'BwViewIssuesButton'
    if ($null -eq $rail -or $null -eq $button -or -not $button.Current.IsEnabled) { throw 'BlackWhite rail or View Issues action unavailable' }
    $buttonBounds = Assert-VisibleInRail $window 'BwViewIssuesButton'
    $scrollBefore = Get-VerticalScrollState $window
    if (-not $AllowEditorScroll) { Assert-AtTop $scrollBefore }
    $invoke = $null
    if (-not $button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { throw 'BwViewIssuesButton has no InvokePattern' }
    ([System.Windows.Automation.InvokePattern]$invoke).Invoke()

    $returnButton = $null
    $entries = @()
    $viewport = $null
    for ($attempt = 0; $attempt -lt 25; $attempt++) {
        Start-Sleep -Milliseconds 200
        $returnButton = Find-ReturnButton $rail
        $items = Find-Control $rail 'ChannelCalibrationIssueItemsControl'
        $entries = @(Get-IssueEntries $(if ($null -ne $items) { $items } else { $rail }))
        $firstElement = if ($entries.Count -gt 0) { Find-Control $rail $entries[0].automationId } else { $null }
        $viewport = Find-DetailViewport $window $rail $firstElement
        if ($null -ne $returnButton -and ($entries.Count -eq 0 -or
            ($null -ne $viewport -and (Test-Inside $entries[0].boundsPhysical $viewport.boundsPhysical)))) { break }
    }
    if ($null -eq $returnButton) { throw "View Issues did not reveal the visible BwReturnToParametersButton at $screenName" }
    $returnBounds = $returnButton.Current.BoundingRectangle
    $returnPhysical = @{ x = $returnBounds.X; y = $returnBounds.Y; width = $returnBounds.Width;
        height = $returnBounds.Height; offscreen = $returnButton.Current.IsOffscreen }
    if (-not (Test-Inside $returnPhysical (Get-VisibleRailBounds $window))) { throw 'Return action is clipped in the BlackWhite rail and Root' }
    $result = [ordered]@{ action = 'One BwViewIssuesButton UIA Invoke; no script scroll, focus or task selection after invocation';
        buttonBeforePhysical = $buttonBounds; editorScrollBefore = $scrollBefore;
        returnAutomationId = $returnButton.Current.AutomationId; returnName = $returnButton.Current.Name;
        returnBoundsPhysical = $returnPhysical; detailViewport = $viewport;
        issueEntries = $entries; issueEntryCount = $entries.Count; firstIssueMessageUiA = $null;
        firstIssuePhysicallyVisible = $false; focusBeforeCapture = Get-FocusState;
        navigation = 'NOT_RUN: issue action inspected, not invoked' }
    if ($entries.Count -gt 0) {
        $first = $entries[0]
        $result.firstIssuePhysicallyVisible = $null -ne $viewport -and
            (Test-Inside $first.boundsPhysical (Get-Bounds $window 'ChannelCalibrationSection')) -and
            (Test-Inside $first.boundsPhysical $viewport.boundsPhysical)
        if (-not $result.firstIssuePhysicallyVisible) { throw "First actual issue is not fully visible in the rail/detail viewport on $screenName" }
        if ([string]::IsNullOrWhiteSpace($first.name) -or $first.name -eq $first.automationId) {
            throw "First actual issue lacks an accessible message on $screenName"
        }
        $result.firstIssueMessageUiA = $first.name
        $result.issueResult = 'Actual first issue has accessible text and finite positive bounds inside the detail viewport; navigation not invoked'
    }
    else {
        $result.issueResult = 'ZERO_CHANNEL_ISSUES: detail screenshot is an empty state, NOT evidence that issue text or navigation works'
    }
    $focus = $result.focusBeforeCapture
    if ($null -ne $focus -and -not ((Test-Inside $focus.boundsPhysical $returnPhysical) -or
        ($null -ne $viewport -and (Test-Inside $focus.boundsPhysical $viewport.boundsPhysical)))) {
        throw "Product focus did not land on visible detail content on $screenName`: $($focus.automationId)"
    }
    return $result
}

function Return-ToParameters($window, $opened, [string]$expectedBlack, [string]$expectedWhite) {
    $rail = Find-Control $window 'ChannelCalibrationSection'
    $button = Find-ReturnButton $rail
    if ($null -eq $button) { throw 'Return to parameters action disappeared before invocation' }
    $invoke = $null
    if (-not $button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { throw 'Return to parameters has no InvokePattern' }
    ([System.Windows.Automation.InvokePattern]$invoke).Invoke()
    $restored = $false
    for ($attempt = 0; $attempt -lt 25; $attempt++) {
        Start-Sleep -Milliseconds 200
        $editor = Find-Control $window 'ManualBlackLevelTextBox'
        if ($null -ne $editor -and -not $editor.Current.IsOffscreen -and
            (Test-Inside (Get-Bounds $window 'ManualBlackLevelTextBox') (Get-VisibleRailBounds $window)) -and
            (Test-Inside (Get-Bounds $window 'BwViewIssuesButton') (Get-VisibleRailBounds $window))) {
            $restored = $true
            break
        }
    }
    if (-not $restored) { throw 'Return did not restore the visible BlackWhite editor' }
    $black = Get-EditorValue $window 'ManualBlackLevelTextBox'
    $white = Get-EditorValue $window 'ManualWhiteLevelTextBox'
    $scroll = Get-VerticalScrollState $window
    if ($black -ne $expectedBlack -or $white -ne $expectedWhite -or
        [math]::Abs($scroll.verticalPercent - $opened.editorScrollBefore.verticalPercent) -gt 0.01) {
        throw 'Return lost the local inputs or the prior editor scroll position'
    }
    $entryBounds = Assert-VisibleInRail $window 'BwViewIssuesButton'
    $focus = Get-FocusState
    if ($null -ne $focus -and -not (Test-Inside $focus.boundsPhysical $entryBounds)) {
        throw "Return did not focus the visible View Issues entry: $($focus.automationId)"
    }
    return [ordered]@{ action = 'Return to parameters UIA Invoke'; blackAfter = $black; whiteAfter = $white;
        editorScrollAfter = $scroll; viewIssuesBoundsPhysical = $entryBounds; focusAfterReturn = $focus;
        inputAndScrollRestored = $true }
}

function Assert-DetailAfterCapture($window, $opened) {
    $rail = Find-Control $window 'ChannelCalibrationSection'
    $button = Find-ReturnButton $rail
    if ($null -eq $button) { throw 'Return action disappeared during detail capture' }
    $returnBounds = $button.Current.BoundingRectangle
    $returnPhysical = @{ x = $returnBounds.X; y = $returnBounds.Y; width = $returnBounds.Width;
        height = $returnBounds.Height; offscreen = $button.Current.IsOffscreen }
    if (-not (Test-Inside $returnPhysical (Get-VisibleRailBounds $window))) { throw 'Return action was clipped during detail capture' }
    $items = Find-Control $rail 'ChannelCalibrationIssueItemsControl'
    $entries = @(Get-IssueEntries $(if ($null -ne $items) { $items } else { $rail }))
    if ($entries.Count -ne $opened.issueEntryCount) { throw 'Issue list changed during detail capture' }
    $viewport = $opened.detailViewport
    if ($entries.Count -gt 0) {
        $first = $entries[0]
        $element = Find-Control $rail $first.automationId
        $viewport = Find-DetailViewport $window $rail $element
        if ($first.automationId -ne $opened.issueEntries[0].automationId -or
            $first.name -ne $opened.firstIssueMessageUiA -or $null -eq $viewport -or
            -not (Test-Inside $first.boundsPhysical $viewport.boundsPhysical)) {
            throw 'First actual issue was obscured or changed during detail capture'
        }
    }
    $focus = Get-FocusState
    if ($null -ne $focus -and -not ((Test-Inside $focus.boundsPhysical $returnPhysical) -or
        ($null -ne $viewport -and (Test-Inside $focus.boundsPhysical $viewport.boundsPhysical)))) {
        throw "Focus was outside visible detail after native capture: $($focus.automationId)"
    }
    $focusBeforeId = if ($null -ne $opened.focusBeforeCapture) { $opened.focusBeforeCapture.automationId } else { $null }
    $focusAfterId = if ($null -ne $focus) { $focus.automationId } else { $null }
    return [ordered]@{ entries = $entries; focusAfterCapture = $focus;
        focusChangedDuringCapture = $focusAfterId -ne $focusBeforeId;
        result = if ($entries.Count -gt 0) { 'First issue still fully visible; screen capture made no focus request' }
                 else { 'No issue entry present; detail text acceptance NOT verified' } }
}

function Assert-PreviewUnchanged($defaultShot, $otherShot) {
    $baseline = $defaultShot.geometry.previewScrollViewer
    $current = $otherShot.geometry.previewScrollViewer
    if ([math]::Abs($current.visibleViewportWidth - $baseline.visibleViewportWidth) -gt 0.5 -or
        [math]::Abs($current.visibleViewportHeight - $baseline.visibleViewportHeight) -gt 0.5) {
        throw "Preview changed between default and $($otherShot.name)"
    }
}

function Add-Shot($record, $shot, [string]$output) {
    $record.shots += $shot
    $record.status = 'CAPTURED_NOT_VISUALLY_REVIEWED'
    [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16),
        (New-Object Text.UTF8Encoding($false)))
}

[RootQaNative]::SetProcessDPIAware() | Out-Null
$exe = [IO.Path]::GetFullPath($Executable)
$dll = Join-Path (Split-Path -Parent $exe) 'PrismUtility.dll'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or -not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "QA executable/DLL missing: $exe / $dll" }
$primaryWidth = [RootQaNative]::GetSystemMetrics(0); $primaryHeight = [RootQaNative]::GetSystemMetrics(1)
$systemDpi = [RootQaNative]::GetDpiForSystem()
if ($TwoColumnOnlyAt192) {
    if ($systemDpi -ne 192) { throw "Two-column-only mode requires 192 system DPI; measured $systemDpi DPI" }
    if ($primaryWidth -lt 2174 -or $primaryHeight -lt 1440) { throw 'Physical primary display cannot fit the 2174 x 1440 two-column window' }
}
elseif ($primaryWidth -lt 3374 -or $primaryHeight -lt 1850) { throw 'Physical primary display cannot fit the specified wide window' }
$output = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $output) { throw "Refusing to touch existing evidence: $output" }
if (-not (Test-Path -LiteralPath (Split-Path -Parent $output) -PathType Container)) { throw "Missing evidence parent: $output" }
$oldTemp = $env:TEMP; $oldTmp = $env:TMP; $oldLocal = $env:LOCALAPPDATA
$oldSettings = $env:LocalSettingsOptions__ApplicationDataFolder
$oldEmpty = $env:PRISM_VISUAL_QA_EMPTY_STATE; $oldSection = $env:PRISM_VISUAL_QA_SECTION_INDEX
$oldMotion = $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED; $oldBuffer = $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER
$process = $null
$window = $null
$script:rootBoundsPhysical = $null
$record = [ordered]@{ status = 'IN_PROGRESS'; captureScope = 'ScanDebugRootGrid only; NOT a full-window screenshot';
    sourceBuild = 'Operator-supplied executable and DLL hashes identify one build for all six shots; current HEAD is metadata, not a certified source-to-binary link';
    executable = $exe; exeSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;
    assembly = $dll; dllSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash;
    dataSource = 'Isolated conditional QA empty-state, offline; profile issues (if present) are existing configuration facts, not caused by the local 65536 input or device measurements';
    sourceHead = 'unknown (git unavailable)'; sourceTreeStatus = 'unknown (git unavailable)';
    systemDpi = $systemDpi; primaryDisplayPhysical = [ordered]@{ width = $primaryWidth; height = $primaryHeight };
    language = 'unknown (app resource language not independently measured)';
    theme = 'unknown (inspect screenshots)'; textScale = [ordered]@{ percent = $null;
        source = 'HKCU TextScaleFactor if present; effective XAML text scale not independently measured' };
    shots = @(); interactions = [ordered]@{} }
if ($TwoColumnOnlyAt192) {
    $record.sourceBuild = 'Operator-supplied executable and DLL hashes identify one build for all four two-column shots; current HEAD is metadata, not a certified source-to-binary link'
    $record['captureMode'] = 'TWO_COLUMN_ONLY_AT_192'
}
$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -ne $git) {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
    $oldGitMaster = $env:GIT_MASTER
    try {
        $env:GIT_MASTER = '1'
        $record.sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim()
        $record.sourceTreeStatus = @(& git -C $repoRoot status --porcelain)
    }
    finally { $env:GIT_MASTER = $oldGitMaster }
}
if (Test-Path -LiteralPath 'HKCU:\Software\Microsoft\Accessibility') {
    $setting = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Accessibility' -Name TextScaleFactor -ErrorAction SilentlyContinue
    if ($null -ne $setting) { $record.textScale.percent = $setting.TextScaleFactor }
}
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
    $wide = @{ name = 'wide'; width = 3374; height = 1850; dpi = 192; rasterizationScale = 2; rootWidth = 1305; rootHeight = 809; previewWidth = 950; previewHeight = 601 }
    $twoColumn = @{ name = 'two-column'; width = 2174; height = 1440; dpi = 192; rasterizationScale = 2; rootWidth = 977; rootHeight = 604; previewWidth = 645; previewHeight = 396 }

    if ($TwoColumnOnlyAt192) {
        $wideReason = if ($primaryWidth -lt $wide.width -or $primaryHeight -lt $wide.height) {
            "Primary display $primaryWidth x $primaryHeight cannot fit the $($wide.width) x $($wide.height) physical wide window"
        } else { 'Wide capture skipped by opt-in two-column-only mode' }
        $record.interactions['wide'] = [ordered]@{ status = 'NOT_RUN'; reason = $wideReason;
            requiredWindowPhysical = [ordered]@{ width = $wide.width; height = $wide.height } }
    }
    else {
        $script:rootBoundsPhysical = $null
        if (-not [RootQaNative]::MoveWindow($handle, 0, 0, $wide.width, $wide.height, $true)) { throw 'MoveWindow failed: wide' }
        Start-Sleep -Seconds 2
        $record.interactions['wideTaskEntry'] = Select-SafeTask $window 'BlackWhite' 1
        Start-Sleep -Milliseconds 500
        $script:rootBoundsPhysical = (Measure-RootBounds $window (Request-Qa $requestDirectory 'geometry' '').state $wide 'wide').rootBoundsPhysical
        Assert-AtTop (Get-VerticalScrollState $window)
        $initialBlack = Get-EditorValue $window 'ManualBlackLevelTextBox'
        $initialWhite = Get-EditorValue $window 'ManualWhiteLevelTextBox'
        $record.interactions['wideBaseline'] = [ordered]@{ black = $initialBlack; white = $initialWhite;
            summaryUiA = Get-TextState $window 'BwValidationSummaryText';
            summaryBoundsPhysical = Get-BwSummaryBounds $window;
            editorScroll = Get-VerticalScrollState $window }
        $wideDefault = Capture-Root $window $handle $requestDirectory $output $wide 'wide-BlackWhite-default-empty'
        Add-Shot $record $wideDefault $output
        Assert-AtTop (Get-VerticalScrollState $window)
        $record.interactions['existingConfigurationBeforeWideView'] = (Request-Qa $requestDirectory 'state' '').state.currentFilmProfileValidationIssues
        $wideOpened = Open-Issues $window 'wide'
        $record.interactions['wideIssues'] = $wideOpened
        $wideIssue = Capture-Root $window $handle $requestDirectory $output $wide 'wide-BlackWhite-view-issues-detail'
        Add-Shot $record $wideIssue $output
        Assert-PreviewUnchanged $wideDefault $wideIssue
        $record.interactions['wideIssueAfterCapture'] = Assert-DetailAfterCapture $window $wideOpened
        if ($wideIssue.png.sha256 -eq $wideDefault.png.sha256) { throw 'Wide detail screenshot is identical to wide default' }
        $record.interactions['wideReturn'] = Return-ToParameters $window $wideOpened $initialBlack $initialWhite
    }

    $script:rootBoundsPhysical = $null
    if (-not [RootQaNative]::MoveWindow($handle, 0, 0, $twoColumn.width, $twoColumn.height, $true)) { throw 'MoveWindow failed: two-column' }
    Start-Sleep -Seconds 2
    $record.interactions['twoColumnTaskEntry'] = Select-SafeTask $window 'BlackWhite' 1
    Start-Sleep -Milliseconds 500
    $script:rootBoundsPhysical = (Measure-RootBounds $window (Request-Qa $requestDirectory 'geometry' '').state $twoColumn 'two-column').rootBoundsPhysical
    Assert-AtTop (Get-VerticalScrollState $window)
    if ($TwoColumnOnlyAt192) {
        $initialBlack = Get-EditorValue $window 'ManualBlackLevelTextBox'
        $initialWhite = Get-EditorValue $window 'ManualWhiteLevelTextBox'
    }
    elseif ((Get-EditorValue $window 'ManualBlackLevelTextBox') -ne $initialBlack -or
        (Get-EditorValue $window 'ManualWhiteLevelTextBox') -ne $initialWhite) { throw 'Wide View/Return changed the default BlackWhite inputs' }
    $record.interactions['twoColumnDefault'] = [ordered]@{ black = $initialBlack; white = $initialWhite;
        feedbackUiA = Get-OptionalTextState $window 'ManualReferenceStatusText';
        validationSummaryUiA = Get-TextState $window 'BwValidationSummaryText';
        editorScroll = Get-VerticalScrollState $window;
        blackBoundsPhysical = Assert-VisibleInRail $window 'ManualBlackLevelTextBox';
        whiteBoundsPhysical = Assert-VisibleInRail $window 'ManualWhiteLevelTextBox';
        applyBoundsPhysical = Assert-VisibleInRail $window 'ApplyManualReferenceLevelsButton';
        revertBoundsPhysical = Assert-VisibleInRail $window 'RevertManualReferenceLevelsButton';
        summaryBoundsPhysical = Get-BwSummaryBounds $window }
    $default = Capture-Root $window $handle $requestDirectory $output $twoColumn 'two-column-BlackWhite-default-empty'
    Add-Shot $record $default $output
    Assert-AtTop (Get-VerticalScrollState $window)
    $focusEntry = Find-Control $window 'BwViewIssuesButton'
    if ($null -eq $focusEntry) { throw 'View Issues entry is unavailable for focus-state capture' }
    $focusEntry.SetFocus()
    $entryFocus = Get-FocusState
    $entryBounds = Assert-VisibleInRail $window 'BwViewIssuesButton'
    if ($null -eq $entryFocus -or $entryFocus.automationId -ne 'BwViewIssuesButton') {
        throw 'View Issues did not receive keyboard focus before the separate focus-state capture'
    }
    $record.interactions['twoColumnViewIssuesFocus'] = [ordered]@{
        purpose = 'B1 focus-only evidence before draft/detail; not a post-click View Issues repair';
        entryBoundsPhysical = $entryBounds; focus = $entryFocus;
        shot = Capture-Root $window $handle $requestDirectory $output $twoColumn 'two-column-BlackWhite-view-issues-focus' }
    Assert-AtTop (Get-VerticalScrollState $window)
    $baselineState = (Request-Qa $requestDirectory 'state' '').state
    $record.interactions['existingConfigurationBeforeDraft'] = [ordered]@{
        issueCount = @($baselineState.currentFilmProfileValidationIssues).Count;
        issues = $baselineState.currentFilmProfileValidationIssues;
        draftSha256 = $baselineState.profileDraftSha256 }
    Set-EditorValue $window 'ManualBlackLevelTextBox' '65536'
    Start-Sleep -Milliseconds 500
    $draftState = (Request-Qa $requestDirectory 'state' '').state
    if ($draftState.profileDraftSha256 -ne $baselineState.profileDraftSha256 -or
        (Get-EditorValue $window 'ManualBlackLevelTextBox') -ne '65536') { throw 'Local draft was not retained as unapplied UI-only input' }
    $sameConfigurationIssues = (ConvertTo-Json -InputObject $baselineState.currentFilmProfileValidationIssues -Depth 8 -Compress) -eq
        (ConvertTo-Json -InputObject $draftState.currentFilmProfileValidationIssues -Depth 8 -Compress)
    if (-not $sameConfigurationIssues) { throw 'Existing configuration issue projection changed when only local BW input was edited' }
    $draftStatus = Get-TextState $window 'ManualReferenceLocalInputStatus'
    if (-not $draftStatus.visible -or
        $draftStatus.text -cnotmatch '^(?:Local input not applied|\u672C\u5730\u8F93\u5165\u672A\u5E94\u7528)$') {
        throw "Short unapplied local-input status is not visible or accurate: $($draftStatus.text)"
    }
    $null = Assert-VisibleInRail $window 'ManualReferenceLocalInputStatus'
    $record.interactions['twoColumnDraft'] = [ordered]@{
        kind = '65536 is only local unapplied text; existing configuration issues are not an input-validation result';
        black = Get-EditorValue $window 'ManualBlackLevelTextBox';
        white = Get-EditorValue $window 'ManualWhiteLevelTextBox';
        localInputStatusUiA = $draftStatus; summaryUiA = Get-TextState $window 'BwValidationSummaryText';
        disabledReasonUiA = Get-OptionalTextState $window 'ManualReferenceDisabledReasonText';
        profileDraftSha256Unchanged = $true; existingConfigurationIssuesUnchanged = $sameConfigurationIssues;
        editorScroll = Get-VerticalScrollState $window;
        summaryBoundsPhysical = Get-BwSummaryBounds $window }
    Assert-AtTop $record.interactions.twoColumnDraft.editorScroll
    $draft = Capture-Root $window $handle $requestDirectory $output $twoColumn 'two-column-BlackWhite-unapplied-draft'
    Add-Shot $record $draft $output
    Assert-PreviewUnchanged $default $draft
    if ($draft.png.sha256 -eq $default.png.sha256) { throw 'Draft screenshot is identical to default' }
    Assert-AtTop (Get-VerticalScrollState $window)
    $twoOpened = Open-Issues $window 'two-column'
    $record.interactions['twoColumnIssues'] = $twoOpened
    $twoIssue = Capture-Root $window $handle $requestDirectory $output $twoColumn 'two-column-BlackWhite-view-issues-detail'
    Add-Shot $record $twoIssue $output
    Assert-PreviewUnchanged $default $twoIssue
    $record.interactions['twoColumnIssueAfterCapture'] = Assert-DetailAfterCapture $window $twoOpened
    if ($twoIssue.png.sha256 -eq $draft.png.sha256) { throw 'Two-column detail screenshot is identical to draft' }
    $record.interactions['twoColumnReturn'] = Return-ToParameters $window $twoOpened '65536' $initialWhite
    $configure = Find-Control $window 'BwConfigureChannelButton'
    if ($null -eq $configure -or -not $configure.Current.IsEnabled) { throw 'Configure current channel entry is unavailable for the missing profile' }
    $configureBounds = Assert-VisibleInRail $window 'BwConfigureChannelButton'
    $invoke = $null
    if (-not $configure.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { throw 'Configure current channel has no InvokePattern' }
    ([System.Windows.Automation.InvokePattern]$invoke).Invoke()
    $configuredTarget = $null
    for ($attempt = 0; $attempt -lt 25; $attempt++) {
        Start-Sleep -Milliseconds 200
        $configuredTarget = Find-Control $window 'ExposureMicrosecondsTextBox'
        if ($null -ne $configuredTarget -and $configuredTarget.Current.HasKeyboardFocus -and
            (Test-Inside (Get-PhysicalBounds $configuredTarget) (Get-VisibleRailBounds $window))) { break }
    }
    if ($null -eq $configuredTarget -or -not $configuredTarget.Current.HasKeyboardFocus -or
        -not (Test-Inside (Get-PhysicalBounds $configuredTarget) (Get-VisibleRailBounds $window))) {
        throw 'Configure current channel did not focus the visible selected-channel parameter editor'
    }
    if ((Get-EditorValue $window 'ManualBlackLevelTextBox') -ne '65536') { throw 'Configure current channel discarded the unapplied input' }
    $record.interactions['twoColumnConfigure'] = [ordered]@{ action = 'One Configure current channel UIA Invoke; no script scroll or focus';
        entryBoundsPhysical = $configureBounds; targetAutomationId = $configuredTarget.Current.AutomationId;
        targetBoundsPhysical = Get-PhysicalBounds $configuredTarget; focusAfter = Get-FocusState;
        unappliedBlackAfter = Get-EditorValue $window 'ManualBlackLevelTextBox' }
    $afterConfigureState = (Request-Qa $requestDirectory 'state' '').state
    if ($afterConfigureState.profileDraftSha256 -ne $baselineState.profileDraftSha256 -or
        $afterConfigureState.selectedCalibrationChannel -ne $baselineState.selectedCalibrationChannel) {
        throw 'Configure current channel changed the draft or selected role'
    }
    $record.interactions.twoColumnConfigure['draftSha256Unchanged'] = $true
    $locateOpened = Open-Issues $window 'two-column-locate' -AllowEditorScroll
    if ($locateOpened.issueEntryCount -eq 0 -or -not $locateOpened.issueEntries[0].navigable) { throw 'No navigable channel issue for Locate test' }
    $locateButton = Find-Control $window $locateOpened.issueEntries[0].automationId
    if ($null -eq $locateButton) { throw 'Navigable issue disappeared before Locate invocation' }
    $invoke = $null
    if (-not $locateButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { throw 'Issue has no InvokePattern for Locate' }
    ([System.Windows.Automation.InvokePattern]$invoke).Invoke()
    $locatedTarget = $null
    for ($attempt = 0; $attempt -lt 25; $attempt++) {
        Start-Sleep -Milliseconds 200
        $locatedTarget = Find-Control $window 'ExposureMicrosecondsTextBox'
        if ($null -ne $locatedTarget -and $locatedTarget.Current.HasKeyboardFocus -and
            (Test-Inside (Get-PhysicalBounds $locatedTarget) (Get-VisibleRailBounds $window))) { break }
    }
    if ($null -eq $locatedTarget -or -not $locatedTarget.Current.HasKeyboardFocus -or
        -not (Test-Inside (Get-PhysicalBounds $locatedTarget) (Get-VisibleRailBounds $window))) {
        throw 'Locate did not focus the visible selected-channel parameter editor'
    }
    if ((Get-EditorValue $window 'ManualBlackLevelTextBox') -ne '65536') { throw 'Locate discarded the unapplied input' }
    $record.interactions['twoColumnLocate'] = [ordered]@{ action = 'One navigable issue UIA Invoke; no script scroll or focus';
        issueAutomationId = $locateOpened.issueEntries[0].automationId;
        targetAutomationId = $locatedTarget.Current.AutomationId; targetBoundsPhysical = Get-PhysicalBounds $locatedTarget;
        focusAfter = Get-FocusState; unappliedBlackAfter = Get-EditorValue $window 'ManualBlackLevelTextBox' }
    $record.interactions['motionTaskEntry'] = Select-SafeTask $window 'Motion' 3
    Start-Sleep -Milliseconds 500
    $record.interactions['motionParameterRailBoundsPhysical'] = Get-Bounds $window 'EngineeringToolsSection'
    $motion = Capture-Root $window $handle $requestDirectory $output $twoColumn 'two-column-Motion-default-empty'
    Add-Shot $record $motion $output
    Assert-PreviewUnchanged $default $motion
    if ($TwoColumnOnlyAt192) {
        if ($record.shots.Count -ne 4) { throw "Expected exactly four two-column native root captures, got $($record.shots.Count)" }
        $record.status = if ($twoOpened.issueEntryCount -gt 0) {
            'FOUR_TWO_COLUMN_NATIVE_ROOT_PNGS_GEOMETRY_AND_UIA_VALIDATED_NOT_VISUALLY_REVIEWED'
        } else { 'FOUR_TWO_COLUMN_NATIVE_ROOT_PNGS_CAPTURED_ZERO_ISSUE_DETAIL_NOT_VERIFIED' }
    }
    else {
        if ($record.shots.Count -ne 6) { throw "Expected exactly six native root captures, got $($record.shots.Count)" }
        $record.status = if ($wideOpened.issueEntryCount -gt 0 -and $twoOpened.issueEntryCount -gt 0) {
            'SIX_NATIVE_ROOT_PNGS_GEOMETRY_AND_UIA_VALIDATED_NOT_VISUALLY_REVIEWED'
        } else { 'SIX_NATIVE_ROOT_PNGS_CAPTURED_ZERO_ISSUE_DETAIL_NOT_VERIFIED' }
    }
}
catch {
    $record.status = 'FAIL_OR_ENVIRONMENT_BLOCKED'
    $record['error'] = $_.Exception.Message
    if ($null -ne $window -and $null -ne $process -and -not $process.HasExited) {
        $diagnosticPath = Join-Path $output 'diagnostic-root.png'
        $diagnostic = [ordered]@{
            status = 'FAILURE_ONLY_NOT_ACCEPTANCE';
            captureHookEffect = 'In-process QA capture may bring Root into view and focus page; geometry and UIA bounds below precede capture';
            geometry = $null; geometryError = $null; windowRectPhysical = $null; dpiForWindow = $null;
            boundsPhysical = $null; boundsError = $null; containmentBeforeGeometry = $null;
            focusBeforeCapture = $null;
            capture = [ordered]@{ status = 'NOT_RUN'; file = $diagnosticPath; png = $null; error = $null } }
        $record['diagnostic'] = $diagnostic
        try {
            $rect = New-Object RootQaNative+Rect
            if ([RootQaNative]::GetWindowRect($handle, [ref]$rect)) {
                $diagnostic.windowRectPhysical = [ordered]@{ x = $rect.Left; y = $rect.Top;
                    width = $rect.Right - $rect.Left; height = $rect.Bottom - $rect.Top }
            }
            $diagnostic.dpiForWindow = [RootQaNative]::GetDpiForWindow($handle)
            $diagnostic.boundsPhysical = [ordered]@{
                root = $script:rootBoundsPhysical;
                rail = Get-Bounds $window 'ChannelCalibrationSection';
                parameterScroll = Get-Bounds $window 'ChannelCalibrationScrollViewer';
                blackEditor = Get-Bounds $window 'ManualBlackLevelTextBox';
                whiteEditor = Get-Bounds $window 'ManualWhiteLevelTextBox';
                footer = Get-Bounds $window 'BwRoiSummary';
                footerRoi = Get-Bounds $window 'BwActiveRoiRangeText';
                footerValidation = Get-Bounds $window 'BwValidationSummaryText';
                footerAction = Get-Bounds $window 'BwViewIssuesButton';
                preview = Get-Bounds $window 'PreviewScrollViewer' }
            $diagnostic.containmentBeforeGeometry = [ordered]@{
                railInsideRoot = Test-Inside $diagnostic.boundsPhysical.rail $diagnostic.boundsPhysical.root;
                blackInsideRail = Test-Inside $diagnostic.boundsPhysical.blackEditor $diagnostic.boundsPhysical.rail;
                blackInsideRoot = Test-Inside $diagnostic.boundsPhysical.blackEditor $diagnostic.boundsPhysical.root;
                blackInsideVisibleRail = Test-Inside $diagnostic.boundsPhysical.blackEditor (Get-Intersection $diagnostic.boundsPhysical.rail $diagnostic.boundsPhysical.root) }
            $diagnostic.focusBeforeCapture = Get-FocusState
        }
        catch { $diagnostic.boundsError = $_.Exception.Message }
        try { $diagnostic.geometry = (Request-Qa $requestDirectory 'geometry' '').state }
        catch { $diagnostic.geometryError = $_.Exception.Message }
        try {
            if (Test-Path -LiteralPath $diagnosticPath) { throw "Refusing to overwrite diagnostic capture: $diagnosticPath" }
            $capture = Request-Qa $requestDirectory 'capture' $diagnosticPath
            $diagnostic.capture.png = Inspect-Png $diagnosticPath ([int]$capture.pixelWidth) ([int]$capture.pixelHeight)
            $diagnostic.capture.status = 'CAPTURED_DIAGNOSTIC_NOT_ACCEPTANCE'
        }
        catch {
            $diagnostic.capture.status = 'FAILED_DIAGNOSTIC_NOT_ACCEPTANCE'
            $diagnostic.capture.error = $_.Exception.Message
        }
    }
    throw
}
finally {
    $cleanupFailure = $null
    try {
        if ($null -ne $process -and -not $process.HasExited) {
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                $process.CloseMainWindow() | Out-Null
                if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
            }
            else { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
            if (-not $process.HasExited) { throw 'Isolated QA app did not exit after cleanup' }
        }
    }
    catch {
        $cleanupFailure = $_.Exception.Message
        $record.status = 'CLEANUP_FAILED'
        $record['cleanupError'] = $cleanupFailure
    }
    finally {
        $env:TEMP = $oldTemp; $env:TMP = $oldTmp; $env:LOCALAPPDATA = $oldLocal
        $env:LocalSettingsOptions__ApplicationDataFolder = $oldSettings
        $env:PRISM_VISUAL_QA_EMPTY_STATE = $oldEmpty; $env:PRISM_VISUAL_QA_SECTION_INDEX = $oldSection
        $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED = $oldMotion; $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER = $oldBuffer
        if (Test-Path -LiteralPath $output -PathType Container) {
            [IO.File]::WriteAllText((Join-Path $output 'root-baseline.json'), ($record | ConvertTo-Json -Depth 16),
                (New-Object Text.UTF8Encoding($false)))
        }
    }
    if ($null -ne $cleanupFailure) { throw "QA process cleanup failed: $cleanupFailure" }
}
$record | ConvertTo-Json -Depth 5
