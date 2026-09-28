[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-Control($root, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-TextState($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { return [ordered]@{ present = $false; visible = $false; text = $null } }
    return [ordered]@{ present = $true; visible = -not $element.Current.IsOffscreen; text = $element.Current.Name }
}

function Get-ButtonState($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { throw "Required button not exposed: $id" }
    return [ordered]@{ enabled = $element.Current.IsEnabled; visible = -not $element.Current.IsOffscreen }
}

function Get-EditorValue($window, [string]$id) {
    $element = Find-Control $window $id
    if ($null -eq $element) { throw "Required editor not exposed: $id" }
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

function Use-Selection($element) {
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        throw "SelectionItemPattern unavailable: $($element.Current.Name)"
    }
    ([System.Windows.Automation.SelectionItemPattern]$pattern).Select()
}

function Get-SelectedTask($window) {
    $combo = Find-Control $window 'WorkbenchTaskComboBox'
    if ($null -ne $combo -and -not $combo.Current.IsOffscreen) {
        $pattern = $null
        if (-not $combo.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern, [ref]$pattern)) { throw 'Task combo selection unreadable' }
        $selected = ([System.Windows.Automation.SelectionPattern]$pattern).Current.GetSelection()
        if ($selected.Count -ne 1) { throw 'Task combo has no singular selection' }
        return $selected[0].Current.Name
    }
    foreach ($task in @('Sampling', 'BlackWhite', 'Focus', 'Motion')) {
        $item = Find-Control $window "Workbench${task}TaskButton"
        $pattern = $null
        if ($null -ne $item -and $item.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern) -and
            ([System.Windows.Automation.SelectionItemPattern]$pattern).Current.IsSelected) { return $task }
    }
    throw 'Selected task not exposed by UIA'
}

function Select-BlackWhiteTask($window) {
    $item = Find-Control $window 'WorkbenchBlackWhiteTaskButton'
    if ($null -ne $item -and -not $item.Current.IsOffscreen -and $item.Current.IsEnabled) {
        Use-Selection $item
        return 'BlackWhite'
    }
    $combo = Find-Control $window 'WorkbenchTaskComboBox'
    if ($null -eq $combo -or $combo.Current.IsOffscreen -or -not $combo.Current.IsEnabled) { throw 'No visible task selector' }
    $pattern = $null
    if (-not $combo.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { throw 'Task combo cannot expand' }
    $expand = [System.Windows.Automation.ExpandCollapsePattern]$pattern
    $expand.Expand()
    try {
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
        $items = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($items.Count -ne 4) { throw "Expected four task choices, got $($items.Count)" }
        $name = $items[1].Current.Name
        Use-Selection $items[1]
        return $name
    }
    finally { $expand.Collapse() }
}

function Get-TaskState($window) {
    return [ordered]@{
        selectedTask = Get-SelectedTask $window
        black = Get-EditorValue $window 'ManualBlackLevelTextBox'
        white = Get-EditorValue $window 'ManualWhiteLevelTextBox'
        status = Get-TextState $window 'ManualReferenceStatusText'
        disabledReason = Get-TextState $window 'ManualReferenceDisabledReasonText'
        validationNotice = Get-TextState $window 'ChannelCalibrationValidationNotice'
        apply = Get-ButtonState $window 'ApplyManualReferenceLevelsButton'
        revert = Get-ButtonState $window 'RevertManualReferenceLevelsButton'
    }
}

function Wait-ForPage($process, [IntPtr]$handle, [bool]$visible) {
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        $process.Refresh()
        if ($process.HasExited) { throw "QA process exited early: $($process.ExitCode)" }
        $window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
        $taskPresent = $null -ne (Find-Control $window 'WorkbenchSamplingTaskButton') -or
            $null -ne (Find-Control $window 'WorkbenchTaskComboBox')
        if ($taskPresent -eq $visible) { return $window }
        Start-Sleep -Milliseconds 400
    }
    $elements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $tree = @($elements | ForEach-Object { "$($_.Current.ControlType.ProgrammaticName):$($_.Current.Name):$($_.Current.AutomationId)" })
    [IO.File]::WriteAllLines((Join-Path $script:outputDir 'uia-tree-on-failure.txt'), $tree, (New-Object Text.UTF8Encoding($false)))
    throw "ScanDebug task UIA visibility did not become $visible; $($elements.Count) descendants logged"
}

$exe = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "QA executable missing: $exe" }
$outputDir = [IO.Path]::GetFullPath($EvidenceDirectory)
$script:outputDir = $outputDir
if (Test-Path -LiteralPath $outputDir) { throw "Evidence directory already exists: $outputDir" }
$oldEnvironment = @{}
foreach ($name in @('TEMP', 'TMP', 'LOCALAPPDATA', 'LocalSettingsOptions__ApplicationDataFolder',
        'PRISM_VISUAL_QA_EMPTY_STATE', 'PRISM_VISUAL_QA_SECTION_INDEX', 'PRISM_VISUAL_QA_PRESERVE_TASK_SELECTION',
        'PRISM_VISUAL_QA_MOTION_READ_REQUIRED', 'PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER')) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$receipt = [ordered]@{
    status = 'NOT_RUN'
    executable = $exe
    executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    assemblySha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $exe) 'PrismUtility.dll') -Algorithm SHA256).Hash
    processId = $null
    qaSwitches = @{ emptyState = '1'; sectionIndex = '1'; preserveTaskSelection = '1'; motionReadRequired = '0'; populateScanBuffer = '0' }
    interaction = 'UIA task selection, two ValuePattern edits and Shell Log/ScanDebug navigation only; no Apply, Save, scan or device command'
    scope = 'Exploratory cross-Shell observation; DESIGN.md explicitly guarantees configuration Return within the same Page, not cross-Shell draft retention'
    captureReadyInitially = $false
    captureReadyAfterReturn = $false
    scannerStatus = $null
    shellNavigation = $null
    initialTask = $null
    initial = $null
    draft = $null
    returned = $null
    checks = [ordered]@{}
    failure = $null
}
$process = $null
try {
    [IO.Directory]::CreateDirectory($outputDir) | Out-Null
    $env:TEMP = Join-Path $outputDir 'isolated-temp'
    $env:TMP = $env:TEMP
    $env:LOCALAPPDATA = Join-Path $outputDir 'isolated-settings'
    $env:LocalSettingsOptions__ApplicationDataFolder = Join-Path $env:LOCALAPPDATA 'PRISM_Utility\ApplicationData'
    [IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    [IO.Directory]::CreateDirectory($env:LOCALAPPDATA) | Out-Null
    $env:PRISM_VISUAL_QA_EMPTY_STATE = '1'
    $env:PRISM_VISUAL_QA_SECTION_INDEX = '1'
    $env:PRISM_VISUAL_QA_PRESERVE_TASK_SELECTION = '1'
    $env:PRISM_VISUAL_QA_MOTION_READ_REQUIRED = '0'
    $env:PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER = '0'
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
    $receipt.processId = $process.Id
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $process.Refresh()
        if ($process.HasExited) { throw "QA process exited before window: $($process.ExitCode)" }
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 400
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'No native main window after 40 seconds' }
    $window = Wait-ForPage $process $handle $true
    $readyPath = Join-Path $env:TEMP 'PRISM_Utility_VisualQaCaptureRequests\ready.json'
    for ($attempt = 0; $attempt -lt 40 -and -not (Test-Path -LiteralPath $readyPath); $attempt++) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path -LiteralPath $readyPath)) { throw 'Capture service did not write ready.json' }
    $ready = [IO.File]::ReadAllText($readyPath) | ConvertFrom-Json
    if (-not $ready.ready -or $ready.marker -ne 'PRISM_VISUAL_QA_PENDING_CALIBRATION_HOOK') { throw 'Unrecognized QA capture service ready marker' }
    $receipt.captureReadyInitially = $true
    $initialReadyTime = (Get-Item -LiteralPath $readyPath).LastWriteTimeUtc

    $receipt.scannerStatus = Get-TextState $window 'ScannerConnectionItem'
    $receipt.initialTask = Get-SelectedTask $window
    $expectedTask = Select-BlackWhiteTask $window
    Start-Sleep -Milliseconds 500
    $receipt.initial = Get-TaskState $window
    if ($receipt.initialTask -eq $expectedTask) { throw 'Initial task was already BlackWhite; cannot verify initial UIA task selection' }
    if ($receipt.initial.selectedTask -ne $expectedTask) { throw "BlackWhite selection failed: $($receipt.initial.selectedTask)" }
    Set-EditorValue $window 'ManualBlackLevelTextBox' '513'
    Set-EditorValue $window 'ManualWhiteLevelTextBox' '60000'
    Start-Sleep -Milliseconds 500
    $receipt.draft = Get-TaskState $window
    if ($receipt.draft.black -ne '513' -or $receipt.draft.white -ne '60000') { throw 'Draft values not readable immediately after entry' }

    $menu = Find-Control $window 'MenuItemsHost'
    if ($null -eq $menu) { throw 'Shell navigation menu unavailable' }
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $menu.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    if ($items.Count -ne 4) { throw "Expected four Shell entries, got $($items.Count)" }
    $receipt.shellNavigation = [ordered]@{ logName = $items[1].Current.Name; scanDebugName = $items[3].Current.Name }
    Use-Selection $items[1]
    $window = Wait-ForPage $process $handle $false
    $menu = Find-Control $window 'MenuItemsHost'
    $items = $menu.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    if ($items.Count -ne 4) { throw 'Shell entries changed while on Log' }
    Use-Selection $items[3]
    $window = Wait-ForPage $process $handle $true
    for ($attempt = 0; $attempt -lt 40 -and (Get-Item -LiteralPath $readyPath).LastWriteTimeUtc -le $initialReadyTime; $attempt++) { Start-Sleep -Milliseconds 250 }
    if ((Get-Item -LiteralPath $readyPath).LastWriteTimeUtc -le $initialReadyTime) { throw 'Capture service did not restart after Shell return' }
    $ready = [IO.File]::ReadAllText($readyPath) | ConvertFrom-Json
    if (-not $ready.ready -or $ready.marker -ne 'PRISM_VISUAL_QA_PENDING_CALIBRATION_HOOK') { throw 'Unrecognized QA marker on return' }
    $receipt.captureReadyAfterReturn = $true
    $receipt.returned = Get-TaskState $window
    $receipt.checks['selectedTask'] = $receipt.returned.selectedTask -eq $expectedTask
    $receipt.checks['blackDraft'] = $receipt.returned.black -eq '513'
    $receipt.checks['whiteDraft'] = $receipt.returned.white -eq '60000'
    foreach ($key in @('status', 'disabledReason', 'validationNotice', 'apply', 'revert')) {
        $before = $receipt.draft[$key] | ConvertTo-Json -Compress
        $after = $receipt.returned[$key] | ConvertTo-Json -Compress
        $receipt.checks[$key] = $before -eq $after
    }
    $mismatches = @($receipt.checks.Keys | Where-Object { -not $receipt.checks[$_] })
    if ($mismatches.Count -gt 0) {
        $receipt.status = 'OBSERVED_CROSS_SHELL_MISMATCH'
        $receipt.failure = "Changed on return: $($mismatches -join ', ')"
    }
    else { $receipt.status = 'PASS_UIA_OFFLINE_NO_DEVICE_COMMANDS' }
}
catch {
    $receipt.status = 'ENVIRONMENT_OR_UIA_BLOCKED'
    $receipt.failure = $_.Exception.Message
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    }
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name]) }
    $receipt['completedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    $initialSelected = if ($null -ne $receipt.initial) { $receipt.initial.selectedTask } else { 'NOT_OBSERVED' }
    $returnedSelected = if ($null -ne $receipt.returned) { $receipt.returned.selectedTask } else { 'NOT_OBSERVED' }
    $beforeDraft = if ($null -ne $receipt.draft) { "$($receipt.draft.black) / $($receipt.draft.white)" } else { 'NOT_OBSERVED' }
    $afterDraft = if ($null -ne $receipt.returned) { "$($receipt.returned.black) / $($receipt.returned.white)" } else { 'NOT_OBSERVED' }
    [IO.File]::WriteAllText((Join-Path $outputDir 'receipt.json'), ($receipt | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllLines((Join-Path $outputDir 'receipt.txt'), @(
        "QA1 UIA result: $($receipt.status)",
        "Initial task: $($receipt.initialTask)",
        "Selected BlackWhite before navigation: $initialSelected",
        "Returned selected task: $returnedSelected",
        "Draft black/white before return: $beforeDraft",
        "Draft black/white after return: $afterDraft",
        "Capture ready before/after: $($receipt.captureReadyInitially) / $($receipt.captureReadyAfterReturn)",
        "Failure: $($receipt.failure)",
        "No screenshots or device/Apply/Save commands attempted. Detailed visible status/disabled/error states: receipt.json"
    ), (New-Object Text.UTF8Encoding($false)))
    $receipt | ConvertTo-Json -Depth 10
}
if ($receipt.status -ne 'PASS_UIA_OFFLINE_NO_DEVICE_COMMANDS') { exit 1 }
