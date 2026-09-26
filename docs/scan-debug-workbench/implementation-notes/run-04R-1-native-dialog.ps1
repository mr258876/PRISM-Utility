# 04R-1 native dialog evidence entry. This script does not start the app or drive its UI.
# From the repository root, after building the chosen Release configuration:
#   powershell.exe -NoProfile -File 'docs/scan-debug-workbench/implementation-notes/run-04R-1-native-dialog.ps1' -AppPath 'PRISM Utility/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/PrismUtility.exe' -EvidenceRoot 'evidence/04R-1-release' -PrismVisualQa false
# Use a different EvidenceRoot for PrismVisualQa true; an existing manifest is never overwritten.
# A native test host/operator must first trigger the dialog, then navigate the Frame on the UI
# dispatcher while it is open. Shell clicks blocked by ContentDialog do NOT count as unload.
# Save a timestamped host/navigation/dialog trace inside EvidenceRoot (no real hardware required).
# Allow at most 120 seconds per scenario; record a timeout as FAIL, never as PASS.
# Example AFTER observing the host run (not before):
#   powershell.exe -NoProfile -File 'docs/scan-debug-workbench/implementation-notes/run-04R-1-native-dialog.ps1' -AppPath 'PRISM Utility/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/PrismUtility.exe' -EvidenceRoot 'evidence/04R-1-release' -PrismVisualQa false -ScenarioId R1-T05 -Status PASS -Observed 'A unloaded; old dialog closed; B remained open after A completion' -LogPath 'evidence/04R-1-release/T05-host.log' -NavigationLogPath 'evidence/04R-1-release/T05-navigation.log' -ProcessExitCode 0
# If no host exists, leave scenarios NOT_RUN and hand off the manifest to the native host owner.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AppPath,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [Parameter(Mandatory = $true)][ValidateSet('true', 'false')][string]$PrismVisualQa,
    [ValidateSet('Release')][string]$Configuration = 'Release',
    [ValidateSet('x64')][string]$Platform = 'x64',
    [ValidateSet('R1-T05', 'R1-T06', 'R1-T07', 'R1-T10')][string]$ScenarioId,
    [ValidateSet('PASS', 'FAIL', 'NOT_RUN', 'ENVIRONMENT_BLOCKED')][string]$Status,
    [string]$Observed,
    [string]$LogPath,
    [string]$NavigationLogPath,
    [int]$ProcessExitCode
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
function Resolve-InputPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$root = Resolve-InputPath $EvidenceRoot
$app = Resolve-InputPath $AppPath
$manifest = Join-Path $root '04R-1-native-dialog.json'

if ($PSBoundParameters.ContainsKey('ScenarioId')) {
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Initialize this EvidenceRoot first: $manifest" }
    if (-not $PSBoundParameters.ContainsKey('Status')) { throw 'Recording requires -Status.' }
    $record = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if ($record.appPath -ne $app -or $record.build.prismVisualQa -ne $PrismVisualQa -or $record.build.configuration -ne $Configuration -or $record.build.platform -ne $Platform) {
        throw 'AppPath or build switches differ from this run; choose a new EvidenceRoot.'
    }
    $scenario = @($record.scenarios | Where-Object { $_.id -eq $ScenarioId })[0]
    if ($scenario.status -ne 'NOT_RUN') { throw 'Scenario already recorded; use a new EvidenceRoot for another run.' }
    if ([string]::IsNullOrWhiteSpace($Observed)) { throw 'Record the observed result or reason, even for NOT_RUN.' }
    if ($Status -in @('PASS', 'FAIL')) {
        if ([string]::IsNullOrWhiteSpace($LogPath) -or -not (Test-Path -LiteralPath $LogPath -PathType Leaf)) { throw 'An observed run needs an existing host log.' }
        if (-not $PSBoundParameters.ContainsKey('ProcessExitCode')) { throw 'An observed run needs the actual host process exit code.' }
    }
    if ($Status -eq 'PASS' -and $ScenarioId -eq 'R1-T10' -and $ProcessExitCode -ne 0) { throw 'Window-close PASS requires exit code 0.' }
    if ($Status -eq 'PASS' -and $ScenarioId -in @('R1-T05', 'R1-T06', 'R1-T07')) {
        if ([string]::IsNullOrWhiteSpace($NavigationLogPath) -or -not (Test-Path -LiteralPath $NavigationLogPath -PathType Leaf)) {
            throw 'PASS for an unload/fault scenario needs an existing programmatic host navigation/fault trace.'
        }
    }
    foreach ($path in @($LogPath, $NavigationLogPath)) {
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            $full = Resolve-InputPath $path
            if (-not (Test-Path -LiteralPath $full -PathType Leaf) -or -not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Logs must exist inside EvidenceRoot: $path"
            }
        }
    }
    $scenario.status = $Status
    $scenario.observed = $Observed
    $scenario.logPath = if ($LogPath) { Resolve-InputPath $LogPath } else { $null }
    $scenario.navigationLogPath = if ($NavigationLogPath) { Resolve-InputPath $NavigationLogPath } else { $null }
    $scenario.processExitCode = if ($PSBoundParameters.ContainsKey('ProcessExitCode')) { $ProcessExitCode } else { $null }
    $scenario.recordedUtc = (Get-Date).ToUniversalTime().ToString('o')
}
else {
    if ($PSBoundParameters.ContainsKey('Status') -or $PSBoundParameters.ContainsKey('Observed') -or $PSBoundParameters.ContainsKey('LogPath') -or $PSBoundParameters.ContainsKey('NavigationLogPath') -or $PSBoundParameters.ContainsKey('ProcessExitCode')) {
        throw 'Observation fields require -ScenarioId.'
    }
    if (Test-Path -LiteralPath $manifest) { throw "Existing run is immutable at initialization: $manifest" }
    $env:GIT_MASTER = '1'
    $head = & git -C $repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not record git HEAD.' }
    $record = [ordered]@{
        schemaVersion = 1
        initializedUtc = (Get-Date).ToUniversalTime().ToString('o')
        head = ($head | Out-String).Trim()
        appPath = $app
        appPresentAtInitialization = (Test-Path -LiteralPath $app -PathType Leaf)
        build = [ordered]@{ configuration = $Configuration; platform = $Platform; prismVisualQa = $PrismVisualQa; provenance = 'operator-declared switches; not a build verification' }
        execution = 'NOT_RUN; this entry never launches the app or navigates the native host'
        timeLimitSecondsPerScenario = 120
        scenarios = @(
            [ordered]@{ id = 'R1-T05'; setup = 'Page A: dirty film profile -> New -> discard confirmation; host navigates Frame to Page B while dialog is open; trigger a new B dialog and allow A late completion.'; expected = 'A request terminates and old dialog closes; B dialog remains independently pending and usable.'; status = 'NOT_RUN'; observed = $null; processExitCode = $null; logPath = $null; navigationLogPath = $null; recordedUtc = $null },
            [ordered]@{ id = 'R1-T06'; setup = 'With fake/offline calibration prompt or notice visible, host navigates Frame away or initiates terminal cleanup.'; expected = 'Cancellation path finishes, dialog exits, no calibration candidate is published.'; status = 'NOT_RUN'; observed = $null; processExitCode = $null; logPath = $null; navigationLogPath = $null; recordedUtc = $null },
            [ordered]@{ id = 'R1-T07'; setup = 'Host injects ShowAsync failure or lost dialog host, then repeats unload and activates a fresh Page; record which fault was actually injected.'; expected = 'No permanent wait or unhandled exception; subsequent valid activation can show a new dialog.'; status = 'NOT_RUN'; observed = $null; processExitCode = $null; logPath = $null; navigationLogPath = $null; recordedUtc = $null },
            [ordered]@{ id = 'R1-T10'; setup = 'With a dialog visible, close the native window; request close again only if the host can safely inject a second close.'; expected = 'Close completes, no dialog wait remains, each request terminates at most once.'; status = 'NOT_RUN'; observed = $null; processExitCode = $null; logPath = $null; navigationLogPath = $null; recordedUtc = $null }
        )
    }
    [void][IO.Directory]::CreateDirectory($root)
}

[IO.File]::WriteAllText($manifest, ($record | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Output "Evidence: $manifest"
Write-Output 'Script exit 0 means the manifest was written, NOT that any native scenario passed.'
