[CmdletBinding()]
param(
    [string]$OutputPath = '..\.omo\evidence\task-24-design-correction-repairs.json',
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 600
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    param([string]$StartPath)
    $current = [IO.Path]::GetFullPath($StartPath)
    while ($true) {
        if ((Test-Path -LiteralPath (Join-Path $current '.git')) -or (Test-Path -LiteralPath (Join-Path $current 'PRISM Utility.sln'))) {
            return $current
        }
        $parent = [IO.Directory]::GetParent($current)
        if ($null -eq $parent -or $parent.FullName -eq $current) { break }
        $current = $parent.FullName
    }
    throw "Could not locate the Host Software repository root from '$StartPath'."
}
function Resolve-OutputFilePath {
    param([string]$Path, [string]$RepositoryRoot)
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}
function Get-ExecutablePath {
    param([string]$Name)

    $command = Get-Command -Name $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command) { return $null }
    $path = if ([string]::IsNullOrEmpty($command.Source)) { $command.Definition } else { $command.Source }
    if ([string]::IsNullOrEmpty($path)) { return $null }
    return [IO.Path]::GetFullPath($path)
}
function Quote-ProcessArgument {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + $Value.Replace('"', '\"') + '"'
}
function Format-ProcessCommand {
    param([string]$FilePath, [string[]]$ArgumentList)

    return (@((Quote-ProcessArgument $FilePath)) + @($ArgumentList | ForEach-Object { Quote-ProcessArgument $_ }) -join ' ')
}
function Limit-OutputTail {
    param([AllowNull()][string]$Text, [int]$MaximumCharacters = 12000)

    if ($null -eq $Text) { return '' }
    if ($Text.Length -le $MaximumCharacters) { return $Text }
    return '[output truncated; showing tail]' + [Environment]::NewLine + $Text.Substring($Text.Length - $MaximumCharacters)
}
function Read-AsyncTaskTail {
    param([AllowNull()]$Task)

    if ($null -eq $Task) { return '' }
    try {
        if (-not $Task.Wait(10000)) { return '[output capture did not finish within 10 seconds]' }
        return Limit-OutputTail ([string]$Task.Result)
    }
    catch { return '[output capture failed: ' + $_.Exception.Message + ']' }
}
function New-LayerRecord {
    param([string]$Name, [string]$Command)

    return [ordered]@{
        name = $Name; status = 'SKIP'; command = $Command; exitCode = $null; timedOut = $false
        durationSeconds = 0.0; stdout = ''; stderr = ''; reason = 'Layer was not run.'
    }
}
function Test-ProcessIdPresent {
    param([AllowNull()][int]$ProcessId)

    if ($null -eq $ProcessId -or $ProcessId -le 0) { return $false }
    return $null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
}
function Find-TaskKillPath {
    $systemRoot = [Environment]::GetEnvironmentVariable('SystemRoot')
    $candidate = if ([string]::IsNullOrEmpty($systemRoot)) { $null } else { Join-Path $systemRoot 'System32\taskkill.exe' }
    if ($null -ne $candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    return Get-ExecutablePath 'taskkill.exe'
}
function Invoke-CapturedProcess {
    param(
        [string]$Name,
        [string]$FilePath,
        [string[]]$ArgumentList,
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds,
        [bool]$CleanupOnTimeout = $true
    )

    $startedAt = Get-Date
    $displayFilePath = if ([string]::IsNullOrEmpty($FilePath)) { $Name + '.exe' } else { $FilePath }
    $record = New-LayerRecord -Name $Name -Command (Format-ProcessCommand $displayFilePath $ArgumentList)
    $process = $null
    $stdoutTask = $null
    $stderrTask = $null
    try {
        if ([string]::IsNullOrEmpty($FilePath) -or -not (Test-Path -LiteralPath $FilePath)) {
            $record.status = 'ENVIRONMENT_BLOCKED'
            $record.reason = "Required executable was not available: '$displayFilePath'."
            return $record
        }

        $startInfo = New-Object Diagnostics.ProcessStartInfo
        $startInfo.FileName = $FilePath
        $startInfo.Arguments = (@($ArgumentList | ForEach-Object { Quote-ProcessArgument $_ }) -join ' ')
        $startInfo.WorkingDirectory = $WorkingDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $process = New-Object Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) { throw "Could not start '$FilePath'." }

        $record.processId = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $milliseconds = [Math]::Max(1, [Math]::Min([Int32]::MaxValue, $TimeoutSeconds * 1000))
        if (-not $process.WaitForExit($milliseconds)) {
            $record.timedOut = $true
            if ($CleanupOnTimeout) {
                $record.cleanup = Stop-StartedProcessTree -ProcessId $process.Id -WorkingDirectory $WorkingDirectory
            }
            else {
                $process.Kill()
            }
            [void]$process.WaitForExit(10000)
        }
        $record.stdout = Read-AsyncTaskTail $stdoutTask
        $record.stderr = Read-AsyncTaskTail $stderrTask
        if ($process.HasExited) { $record.exitCode = $process.ExitCode }
        if ($record.timedOut) {
            $record.status = 'FAIL'; $record.reason = 'The process exceeded its bounded timeout.'
        }
        elseif ($record.exitCode -eq 0) {
            $record.status = 'PASS'; $record.reason = 'Process exited successfully.'
        }
        else {
            $record.status = 'FAIL'; $record.reason = 'Process exited with a nonzero exit code.'
        }
    }
    catch {
        $record.status = 'FAIL'
        $record.reason = 'Process execution threw: ' + $_.Exception.Message
    }
    finally {
        if ($null -ne $process) { $process.Dispose() }
        $record.durationSeconds = [Math]::Round(((Get-Date) - $startedAt).TotalSeconds, 3)
    }
    return $record
}
function Stop-StartedProcessTree {
    param([int]$ProcessId, [string]$WorkingDirectory)

    $taskKillPath = Find-TaskKillPath
    if ($null -eq $taskKillPath) {
        return [ordered]@{
            status = 'ENVIRONMENT_BLOCKED'; command = 'taskkill.exe /PID ' + $ProcessId + ' /T /F'; targetProcessId = $ProcessId
            exitCode = $null; timedOut = $false; durationSeconds = 0.0; stdout = ''; stderr = ''; leftoverProcess = $true
            reason = 'taskkill.exe was not available to clean the process tree.'
        }
    }

    $run = Invoke-CapturedProcess -Name 'taskkill' -FilePath $taskKillPath -ArgumentList @('/PID', [string]$ProcessId, '/T', '/F') -WorkingDirectory $WorkingDirectory -TimeoutSeconds 10 -CleanupOnTimeout $false
    $leftover = $true
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (-not (Test-ProcessIdPresent $ProcessId)) { $leftover = $false; break }
        Start-Sleep -Milliseconds 100
    }
    return [ordered]@{
        status = if (-not $run.timedOut -and $run.exitCode -eq 0 -and -not $leftover) { 'PASS' } else { 'FAIL' }
        command = 'taskkill.exe /PID ' + $ProcessId + ' /T /F'; targetProcessId = $ProcessId; exitCode = $run.exitCode
        timedOut = $run.timedOut; durationSeconds = $run.durationSeconds; stdout = $run.stdout; stderr = $run.stderr
        leftoverProcess = $leftover
        reason = if (-not $run.timedOut -and $run.exitCode -eq 0 -and -not $leftover) { 'taskkill.exe reported successful recursive cleanup and the target PID is gone.' } else { 'Process-tree cleanup did not provide a clean result.' }
    }
}
function Get-AppEnvironmentBlockReason {
    param($Record)

    $text = [string]::Concat([string]$Record.stdout, [Environment]::NewLine, [string]$Record.stderr)
    foreach ($pattern in @(
        'MSB4019.*(not found|could not be resolved)', 'MSB4236.*(not found|could not be found)', 'MSB8036',
        'NETSDK1100', 'NETSDK1147', 'Windows SDK.*(not found|could not be found|missing)',
        'Windows App SDK.*(not found|could not be found|missing)', 'Microsoft\.WindowsAppSDK.*(not found|could not be found|missing)',
        'makepri(\.exe)?.*(not recognized|not found)', 'makeappx(\.exe)?.*(not recognized|not found)',
        'Native DNG export requires a prebuilt', 'Expected native DNG bridge output was not produced', 'NU1101', 'NU1301'
    )) {
        $match = [regex]::Match($text, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) { return 'Explicit environment prerequisite evidence matched: ' + $match.Value }
    }
    return $null
}
function Set-BuildLayerStatus {
    param($Record, [bool]$IsAppBuild)

    if ($Record.status -in @('PASS', 'ENVIRONMENT_BLOCKED')) { return $Record }
    if ($Record.timedOut) {
        $Record.reason = 'Build exceeded its bounded timeout; this is an actual build-layer failure.'
    }
    elseif ($IsAppBuild) {
        $environmentReason = Get-AppEnvironmentBlockReason $Record
        if ($null -ne $environmentReason) { $Record.status = 'ENVIRONMENT_BLOCKED'; $Record.reason = $environmentReason; return $Record }
    }
    $Record.status = 'FAIL'
    if ($Record.reason -eq 'Process exited with a nonzero exit code.') {
        $Record.reason = 'Build/test command returned a nonzero exit code without an explicit environment-block evidence match.'
    }
    return $Record
}
function Find-VsWherePath {
    $path = Get-ExecutablePath 'vswhere.exe'
    if ($null -ne $path) { return $path }
    foreach ($root in @([Environment]::GetEnvironmentVariable('ProgramFiles(x86)'), [Environment]::GetEnvironmentVariable('ProgramFiles'))) {
        if (-not [string]::IsNullOrEmpty($root)) {
            $candidate = Join-Path $root 'Microsoft Visual Studio\Installer\vswhere.exe'
            if (Test-Path -LiteralPath $candidate) { return [IO.Path]::GetFullPath($candidate) }
        }
    }
    return $null
}
function Get-VisualStudioV143Discovery {
    $component = 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64'
    $discovery = [ordered]@{
        status = 'ENVIRONMENT_BLOCKED'; method = $null; vswherePath = $null
        vswhereQuery = "-version '[17.0,18.0)' -products '*' -requires $component -format json"
        vswhereExitCode = $null; vswhereMatchedComponent = $false; instanceId = $null; installationPath = $null
        installationVersion = $null; msbuildPath = $null; vctoolsVersionFile = $null; vctoolsVersion = $null
        compilerPath = $null; v143ToolsetProps = $null; v143ToolsetTargets = $null; missing = @()
        reason = 'Visual Studio v143 discovery was not attempted.'
    }
    $missing = New-Object 'Collections.Generic.List[string]'
    $installationPath = $null
    $vswherePath = Find-VsWherePath
    $discovery.vswherePath = $vswherePath
    if ($null -ne $vswherePath) {
        $run = Invoke-CapturedProcess -Name 'vswhere' -FilePath $vswherePath -ArgumentList @('-version', '[17.0,18.0)', '-products', '*', '-requires', $component, '-format', 'json') -WorkingDirectory ([Environment]::CurrentDirectory) -TimeoutSeconds 30 -CleanupOnTimeout $false
        $discovery.vswhereExitCode = $run.exitCode
        try {
            if ($run.exitCode -ne 0) { $discovery.reason = 'vswhere.exe returned exit code ' + $run.exitCode + '.' }
            elseif ([string]::IsNullOrWhiteSpace($run.stdout)) { $discovery.reason = 'vswhere.exe returned no VS2022 instances.' }
            else {
                $instances = @($run.stdout | ConvertFrom-Json)
                $instance = @($instances | Where-Object { $_.isComplete -eq $true -and -not [string]::IsNullOrEmpty($_.installationPath) } | Sort-Object installationVersion -Descending | Select-Object -First 1)[0]
                if ($null -eq $instance) { $discovery.reason = 'vswhere.exe found no complete VS2022 instance with the required Visual C++ component.' }
                else {
                    $installationPath = [IO.Path]::GetFullPath($instance.installationPath)
                    $discovery.method = 'vswhere'; $discovery.vswhereMatchedComponent = $true; $discovery.instanceId = $instance.instanceId
                    $discovery.installationPath = $installationPath; $discovery.installationVersion = $instance.installationVersion
                }
            }
        }
        catch { $discovery.reason = 'vswhere.exe discovery threw: ' + $_.Exception.Message }
    }
    if ($null -eq $installationPath) {
        $fallbacks = New-Object 'Collections.Generic.List[string]'
        foreach ($root in @([Environment]::GetEnvironmentVariable('ProgramFiles'), [Environment]::GetEnvironmentVariable('ProgramFiles(x86)'))) {
            if (-not [string]::IsNullOrEmpty($root)) {
                foreach ($directory in @(Get-ChildItem -Path (Join-Path $root 'Microsoft Visual Studio\2022\*') -Directory -ErrorAction SilentlyContinue)) {
                    if (-not ($fallbacks -contains $directory.FullName)) { [void]$fallbacks.Add($directory.FullName) }
                }
            }
        }
        $msbuildCommand = Get-ExecutablePath 'msbuild.exe'
        if ($null -ne $msbuildCommand) {
            $match = [regex]::Match($msbuildCommand, '^(.*)\\MSBuild\\Current\\Bin\\MSBuild\.exe$', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($match.Success -and -not ($fallbacks -contains $match.Groups[1].Value)) { [void]$fallbacks.Add($match.Groups[1].Value) }
        }
        if ($fallbacks.Count -gt 0) {
            $installationPath = $fallbacks[0]; $discovery.method = 'fallback-path-inspection'; $discovery.installationPath = $installationPath
            $discovery.reason = 'vswhere did not yield a complete instance; using a VS2022 path fallback with explicit v143 target-file checks.'
        }
    }
    if ($null -eq $installationPath) {
        [void]$missing.Add('Complete Visual Studio 2022 instance with ' + $component + ' (vswhere or fallback path)')
    }
    else {
        $msbuild = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
        if (Test-Path -LiteralPath $msbuild) { $discovery.msbuildPath = [IO.Path]::GetFullPath($msbuild) } else { [void]$missing.Add('MSBuild.exe: ' + $msbuild) }
        $versionFile = Join-Path $installationPath 'VC\Auxiliary\Build\Microsoft.VCToolsVersion.default.txt'
        $discovery.vctoolsVersionFile = [IO.Path]::GetFullPath($versionFile)
        if (Test-Path -LiteralPath $versionFile) {
            $discovery.vctoolsVersion = ([IO.File]::ReadAllText($versionFile)).Trim()
            if ([string]::IsNullOrEmpty($discovery.vctoolsVersion)) { [void]$missing.Add('Non-empty Visual C++ toolset version in: ' + $versionFile) }
            else {
                $compiler = Join-Path $installationPath ('VC\Tools\MSVC\' + $discovery.vctoolsVersion + '\bin\Hostx64\x64\cl.exe')
                if (Test-Path -LiteralPath $compiler) { $discovery.compilerPath = [IO.Path]::GetFullPath($compiler) } else { [void]$missing.Add('Visual C++ compiler for toolset ' + $discovery.vctoolsVersion + ': ' + $compiler) }
            }
        }
        else { [void]$missing.Add('Visual C++ toolset version file: ' + $versionFile) }
        foreach ($toolset in @(@{ Name = 'v143ToolsetProps'; Path = 'MSBuild\Microsoft\VC\v170\Platforms\x64\PlatformToolsets\v143\Toolset.props' }, @{ Name = 'v143ToolsetTargets'; Path = 'MSBuild\Microsoft\VC\v170\Platforms\x64\PlatformToolsets\v143\Toolset.targets' })) {
            $path = Join-Path $installationPath $toolset.Path
            $discovery[$toolset.Name] = [IO.Path]::GetFullPath($path)
            if (-not (Test-Path -LiteralPath $path)) { [void]$missing.Add(($toolset.Name -replace 'v143Toolset', 'v143 x64 ') + ': ' + $path) }
        }
    }
    $discovery.missing = @($missing.ToArray())
    if ($missing.Count -eq 0) {
        $discovery.status = 'PASS'
        $discovery.reason = 'vswhere/fallback discovery proved a complete VS2022 Visual C++ component, MSBuild, the project v143 target files, and the compiler selected by Microsoft.VCToolsVersion.default.txt.'
    }
    elseif ($null -ne $installationPath) { $discovery.reason = 'Visual Studio v143 prerequisites are unavailable: ' + ($missing -join '; ') }
    return $discovery
}
function Get-NativePathCheck {
    param([string]$NativeRoot, [string]$RelativePath, [string]$Kind)

    $requested = $RelativePath.Replace('/', '\')
    $resolved = Join-Path $NativeRoot $requested
    $wildcard = $resolved -match '[*?]'
    $matches = @(
        if ($wildcard) { Get-ChildItem -Path $resolved -File -ErrorAction SilentlyContinue }
        elseif (Test-Path -LiteralPath $resolved) { Get-Item -LiteralPath $resolved }
    )
    return [ordered]@{
        kind = $Kind; requestedPath = $RelativePath; resolvedPath = if ($wildcard) { $resolved } else { [IO.Path]::GetFullPath($resolved) }
        exists = $matches.Count -gt 0; matchCount = $matches.Count
        samplePaths = @($matches | Select-Object -First 5 | ForEach-Object { [IO.Path]::GetFullPath($_.FullName) })
    }
}
function Invoke-NativeDependencyProbe {
    param([string]$RepositoryRoot)

    $startedAt = Get-Date
    $nativeRoot = Join-Path $RepositoryRoot 'DngSdkWarpper'
    $projectPath = Join-Path $nativeRoot 'DngSdkWarpper.vcxproj'
    $record = New-LayerRecord 'nativeDependencyProbe' 'probe: DngSdkWarpper.vcxproj, Visual C++ v143/MSBuild, and Adobe_DNG_SDK paths/files'
    $record.evidence = [ordered]@{
        projectPath = [IO.Path]::GetFullPath($projectPath); platformToolset = $null; msbuildPath = $null
        visualCppV143CompilerPath = $null; adobeDngSdkChecks = @(); nativeBuildRun = $false; nativeBuildStatus = 'SKIP'
    }
    try {
        if (-not (Test-Path -LiteralPath $projectPath)) { $record.status = 'ENVIRONMENT_BLOCKED'; $record.reason = 'Missing native project prerequisite: ' + [IO.Path]::GetFullPath($projectPath); return $record }
        $project = [IO.File]::ReadAllText($projectPath)
        $toolsets = @([regex]::Matches($project, '<PlatformToolset>\s*([^<]+?)\s*</PlatformToolset>', [Text.RegularExpressions.RegexOptions]::IgnoreCase) | ForEach-Object { $_.Groups[1].Value.Trim() } | Select-Object -Unique)
        $record.evidence.platformToolset = $toolsets -join ', '
        if ($toolsets.Count -eq 0 -or $toolsets -notcontains 'v143') { $record.status = 'FAIL'; $record.reason = 'The native vcxproj does not explicitly declare the required v143 platform toolset.'; return $record }
        $visualStudio = Get-VisualStudioV143Discovery
        $record.evidence.visualStudioDiscovery = $visualStudio
        $record.evidence.msbuildPath = $visualStudio.msbuildPath
        $record.evidence.visualCppV143CompilerPath = $visualStudio.compilerPath
        $record.evidence.v143ToolsetProps = $visualStudio.v143ToolsetProps
        $record.evidence.v143ToolsetTargets = $visualStudio.v143ToolsetTargets
        $missing = New-Object 'Collections.Generic.List[string]'
        foreach ($item in $visualStudio.missing) { [void]$missing.Add($item) }
        $checks = New-Object 'Collections.Generic.List[object]'
        $includePaths = New-Object 'Collections.Generic.List[string]'
        foreach ($match in [regex]::Matches($project, '<AdditionalIncludeDirectories>(.*?)</AdditionalIncludeDirectories>', [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [Text.RegularExpressions.RegexOptions]::Singleline)) {
            foreach ($path in ($match.Groups[1].Value -split ';')) {
                $path = $path.Trim()
                if ($path.Length -gt 0 -and $path -notmatch '^%\(' -and -not ($includePaths -contains $path)) { [void]$includePaths.Add($path) }
            }
        }
        $sourcePaths = @([regex]::Matches($project, '<ClCompile\s+Include="([^"]+)"', [Text.RegularExpressions.RegexOptions]::IgnoreCase) | ForEach-Object { $_.Groups[1].Value.Trim() } | Where-Object { $_ -like 'Adobe_DNG_SDK\*' } | Select-Object -Unique)
        foreach ($spec in @($includePaths | ForEach-Object { @{ Path = $_; Kind = 'include-directory'; Missing = 'Adobe_DNG_SDK include path/file: ' } }) + @($sourcePaths | ForEach-Object { @{ Path = $_; Kind = 'source-glob'; Missing = 'Adobe_DNG_SDK source path/file: ' } }) + @('PrismDngApi.h', 'PrismDngWriter.cpp', 'PrismDngBridge.cpp' | ForEach-Object { @{ Path = $_; Kind = 'native-wrapper-file'; Missing = 'Native wrapper file: ' } })) {
            $check = Get-NativePathCheck $nativeRoot $spec.Path $spec.Kind
            [void]$checks.Add($check)
            if (-not $check.exists) { [void]$missing.Add($spec.Missing + $check.resolvedPath) }
        }
        $record.evidence.adobeDngSdkChecks = @($checks.ToArray())
        if ($missing.Count -gt 0) { $record.status = 'ENVIRONMENT_BLOCKED'; $record.reason = 'Native dependency prerequisites are unavailable: ' + ($missing -join '; ') }
        else { $record.status = 'PASS'; $record.reason = 'Dependency probe PASS: MSBuild, Visual C++ v143, and all inspected Adobe_DNG_SDK/native wrapper paths are present. Native build status is SKIP because no native build was run.' }
    }
    catch { $record.status = 'FAIL'; $record.reason = 'Native dependency probe threw: ' + $_.Exception.GetType().FullName + ': ' + $_.Exception.Message + ' at ' + $_.InvocationInfo.PositionMessage }
    finally { $record.durationSeconds = [Math]::Round(((Get-Date) - $startedAt).TotalSeconds, 3) }
    return $record
}
function Invoke-TimeoutSelfTest {
    param([string]$PowerShellPath, [string]$WorkingDirectory)

    if ([string]::IsNullOrEmpty($PowerShellPath) -or -not (Test-Path -LiteralPath $PowerShellPath)) {
        $blocked = New-LayerRecord 'timeoutSelfTest' 'current PowerShell executable with a sleep child; expected bounded timeout'
        $blocked.status = 'ENVIRONMENT_BLOCKED'; $blocked.reason = 'Current Windows PowerShell executable was not available for timeout self-test.'
        return $blocked
    }
    $child = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes('Start-Sleep -Seconds 30'))
    $parent = '$child = Start-Process -FilePath ([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName) -ArgumentList @(''-NoProfile'', ''-NonInteractive'', ''-EncodedCommand'', ''' + $child + ''') -PassThru; Write-Output (''childPid='' + $child.Id); Wait-Process -Id $child.Id'
    $encodedParent = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($parent))
    $record = Invoke-CapturedProcess -Name 'timeoutSelfTest' -FilePath $PowerShellPath -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encodedParent) -WorkingDirectory $WorkingDirectory -TimeoutSeconds 3
    $record.expectedTimeout = $true
    $match = [regex]::Match([string]$record.stdout, 'childPid=(\d+)')
    $record.childProcessId = if ($match.Success) { [int]$match.Groups[1].Value } else { $null }
    $record.childProcessStarted = $match.Success
    $record.childProcessLeftover = if ($match.Success) { Test-ProcessIdPresent $record.childProcessId } else { $null }
    $cleanupPassed = $record.Contains('cleanup') -and $record.cleanup.status -eq 'PASS' -and -not $record.cleanup.leftoverProcess
    if ($record.timedOut -and $cleanupPassed -and $record.childProcessStarted -and -not $record.childProcessLeftover) {
        $record.status = 'PASS'; $record.reason = 'The self-test timed out as intended, taskkill recursively cleaned the parent tree, and the sleep child PID is gone.'
    }
    elseif (-not $record.timedOut) { $record.status = 'FAIL'; $record.reason = 'The intentional sleep self-test completed before the timeout deadline.' }
    elseif (-not $cleanupPassed -or $record.childProcessLeftover) { $record.status = 'FAIL'; $record.reason = 'The intentional timeout occurred, but leftover-process cleanup was not proven successful.' }
    else { $record.status = 'FAIL'; $record.reason = 'The timeout self-test did not provide complete parent/child process evidence.' }
    return $record
}
function New-CleanupLayer {
    param($TimeoutLayer)

    if (-not $TimeoutLayer.Contains('cleanup')) {
        $record = New-LayerRecord 'leftoverProcessCleanup' 'taskkill.exe /PID <timeout-self-test-parent> /T /F'
        $record.status = 'ENVIRONMENT_BLOCKED'; $record.reason = 'Timeout self-test did not produce a cleanup record.'
        return $record
    }
    $cleanup = $TimeoutLayer.cleanup
    $record = [ordered]@{
        name = 'leftoverProcessCleanup'; status = $cleanup.status; command = $cleanup.command; exitCode = $cleanup.exitCode
        timedOut = $cleanup.timedOut; durationSeconds = $cleanup.durationSeconds; stdout = $cleanup.stdout; stderr = $cleanup.stderr
        targetProcessId = $cleanup.targetProcessId; leftoverProcess = $cleanup.leftoverProcess; childProcessId = $TimeoutLayer.childProcessId
        childProcessLeftover = $TimeoutLayer.childProcessLeftover; reason = $cleanup.reason
    }
    if ($record.status -eq 'PASS' -and $record.childProcessLeftover -eq $false) { $record.reason = 'Parent process tree and recorded sleep child were both absent after targeted cleanup.' }
    elseif ($record.status -eq 'PASS') { $record.status = 'FAIL'; $record.reason = 'taskkill.exe reported success for the parent, but the recorded child PID remained.' }
    return $record
}
function Set-OverallResult {
    param($Result, [bool]$RunnerFailure)

    if ($RunnerFailure) { $Result.status = 'FAIL'; $Result.exitCode = 1; return 1 }
    $layers = @($Result.layers.GetEnumerator() | ForEach-Object { $_.Value })
    $failed = @($layers | Where-Object { $_.status -eq 'FAIL' })
    $blocked = @($layers | Where-Object { $_.status -eq 'ENVIRONMENT_BLOCKED' })
    if ($failed.Count -gt 0) {
        $Result.status = 'FAIL'; $Result.reason = 'One or more layers reported an actual FAIL or cleanup failure: ' + (($failed | ForEach-Object { $_.name }) -join ', '); $Result.exitCode = 1
    }
    elseif ($blocked.Count -gt 0) {
        $Result.status = 'ENVIRONMENT_BLOCKED'; $Result.reason = 'Available checks completed; one or more prerequisites were environment-blocked. Environment blocks do not fail the runner.'; $Result.exitCode = 0
    }
    else { $Result.status = 'PASS'; $Result.reason = 'All requested layers passed.'; $Result.exitCode = 0 }
    return $Result.exitCode
}
function Write-ResultJson {
    param($Result, [string]$Path)

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { [void](New-Item -ItemType Directory -Path $parent -Force) }
    [IO.File]::WriteAllText($Path, ($Result | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
}
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$startedAt = Get-Date
$repositoryRoot = $null
$resolvedOutputPath = $null
$runnerFailure = $false
$writeFailure = $false
$result = [ordered]@{
    schemaVersion = 1; task = 'task24-design-correction-repairs'; startUtc = $startedAt.ToUniversalTime().ToString('o'); endUtc = $null
    durationSeconds = 0.0; shell = $null; repositoryRoot = $null; requestedOutputPath = $OutputPath; outputPath = $null
    requestedTimeoutSeconds = $TimeoutSeconds; status = 'SKIP'; reason = 'Runner has not completed.'; layers = [ordered]@{}
}
try {
    $repositoryRoot = Get-RepositoryRoot $scriptRoot
    $resolvedOutputPath = Resolve-OutputFilePath $OutputPath $repositoryRoot
    $currentPowerShell = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $currentPowerShell)) { $currentPowerShell = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName }
    $result.repositoryRoot = $repositoryRoot
    $result.outputPath = $resolvedOutputPath
    $result.shell = [ordered]@{ name = 'Windows PowerShell'; version = $PSVersionTable.PSVersion.ToString(); edition = [string]$PSVersionTable.PSEdition; executable = $currentPowerShell }

    $dotnetPath = Get-ExecutablePath 'dotnet.exe'
    foreach ($spec in @(
        @{ Name = 'managedTests'; Arguments = @('test', 'PrismUtility.Core.Tests\PrismUtility.Core.Tests.csproj'); IsAppBuild = $false },
        @{ Name = 'coreBuild'; Arguments = @('build', 'PRISM Utility.Core\PrismUtility.Core.csproj'); IsAppBuild = $false },
        @{ Name = 'appBuildX64'; Arguments = @('build', 'PRISM Utility\PrismUtility.csproj', '-p:Platform=x64'); IsAppBuild = $true }
    )) {
        $layer = Invoke-CapturedProcess -Name $spec.Name -FilePath $dotnetPath -ArgumentList $spec.Arguments -WorkingDirectory $repositoryRoot -TimeoutSeconds $TimeoutSeconds
        $result.layers[$spec.Name] = Set-BuildLayerStatus $layer $spec.IsAppBuild
    }
    $result.layers.nativeDependencyProbe = Invoke-NativeDependencyProbe $repositoryRoot
    $result.layers.timeoutSelfTest = Invoke-TimeoutSelfTest $currentPowerShell $repositoryRoot
    $result.layers.leftoverProcessCleanup = New-CleanupLayer $result.layers.timeoutSelfTest
}
catch {
    $runnerFailure = $true
    $result.status = 'FAIL'; $result.reason = 'Runner threw before all layers completed: ' + $_.Exception.Message; $result.runnerError = $_.Exception.ToString()
    $result.layers.runner = New-LayerRecord 'runner' 'run-layered-qa.ps1'
    $result.layers.runner.status = 'FAIL'; $result.layers.runner.reason = $result.reason
}
finally {
    $endedAt = Get-Date
    $result.endUtc = $endedAt.ToUniversalTime().ToString('o')
    $result.durationSeconds = [Math]::Round(($endedAt - $startedAt).TotalSeconds, 3)
    $exitCode = Set-OverallResult $result $runnerFailure
    try { Write-ResultJson $result $resolvedOutputPath }
    catch {
        $writeFailure = $true; $exitCode = 1; $result.exitCode = 1; $result.status = 'FAIL'; $result.reason = 'Evidence JSON could not be written: ' + $_.Exception.Message; $result.writeError = $_.Exception.ToString()
        try { if (-not [string]::IsNullOrEmpty($resolvedOutputPath)) { Write-ResultJson $result $resolvedOutputPath } } catch { Write-Error ('Fallback evidence JSON write also failed: ' + $_.Exception.Message) }
    }
    Write-Output ('Layered QA evidence: ' + [string]$resolvedOutputPath)
    foreach ($entry in $result.layers.GetEnumerator()) { Write-Output ($entry.Key + ': ' + $entry.Value.status) }
    Write-Output ('Overall: ' + $result.status)
    Write-Output ('Exit code: ' + $exitCode)
}

if ($writeFailure) { exit 1 }
exit $exitCode
