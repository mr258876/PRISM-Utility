[CmdletBinding()]
param(
    [ValidateSet('Partial', 'Full')]
    [string]$Mode = 'Partial',

    [string[]]$Files = @(),

    [ValidateSet(
        'MissingSection',
        'MissingPageMapping',
        'MissingServiceCoverage',
        'EndpointMismatch',
        'MissingProtocolStep',
        'PixelTermMismatch',
        'MissingDngDependency',
        'DuplicateIssueId',
        'BrokenLink',
        'BadSourcePath',
        'BadMermaid')]
    [string]$SelfTest
)

$ArchitectureRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$HostSoftwareRoot = Split-Path -Parent (Split-Path -Parent $ArchitectureRoot)
$RepositoryRoot = Split-Path -Parent $HostSoftwareRoot

$Deliverables = @(
    'README.md',
    'system-overview.md',
    'app-lifecycle-and-di.md',
    'navigation-and-shell.md',
    'ui-mvvm.md',
    'viewmodel-lifecycle.md',
    'settings-persistence.md',
    'logging-localization-theme.md',
    'usb-transport.md',
    'scanner-session-and-access.md',
    'scan-protocol-and-execution.md',
    'scan-workflow-and-device-control.md',
    'image-decoding-preview.md',
    'alignment-color-processing.md',
    'calibration-autofocus-film-profiles.md',
    'dng-export-native-bridge.md',
    'testing-and-build.md',
    'issues-and-remediation.md'
)

$TopicDeliverables = @(
    'app-lifecycle-and-di.md',
    'navigation-and-shell.md',
    'ui-mvvm.md',
    'viewmodel-lifecycle.md',
    'settings-persistence.md',
    'logging-localization-theme.md',
    'usb-transport.md',
    'scanner-session-and-access.md',
    'scan-protocol-and-execution.md',
    'scan-workflow-and-device-control.md',
    'image-decoding-preview.md',
    'alignment-color-processing.md',
    'calibration-autofocus-film-profiles.md',
    'dng-export-native-bridge.md',
    'testing-and-build.md',
    'issues-and-remediation.md'
)

$SectionPatterns = [ordered]@{
    'Scope' = '(?im)^#{1,6}\s+.*(scope|non-responsibilit)'
    'Functionality' = '(?im)^#{1,6}\s+.*function'
    'Key entry points' = '(?im)^#{1,6}\s+.*(key\s+entry|entry\s+point)'
    'Implementation' = '(?im)^#{1,6}\s+.*(implementation|mechanism)'
    'Control and data flow' = '(?im)^#{1,6}\s+.*(control|data\s+flow)'
    'Dependencies' = '(?im)^#{1,6}\s+.*dependenc'
    'State and concurrency' = '(?im)^#{1,6}\s+.*(state|concurr)'
    'Error handling' = '(?im)^#{1,6}\s+.*error'
    'Test coverage' = '(?im)^#{1,6}\s+.*(test|coverage)'
    'Known issues and solutions' = '(?im)^#{1,6}\s+.*(known\s+issues|issues|remediation)'
    'Related source' = '(?im)^#{1,6}\s+.*(related\s+source|source)'
}

function New-ValidationResult {
    param(
        [string]$Path,
        [string[]]$Errors
    )

    return [pscustomobject]@{
        Path = $Path
        Valid = ($Errors.Count -eq 0)
        Errors = @($Errors)
    }
}

function Resolve-DocumentPath {
    param([string]$Document)

    if ([IO.Path]::IsPathRooted($Document)) {
        return [IO.Path]::GetFullPath($Document)
    }

    return [IO.Path]::GetFullPath((Join-Path -Path $ArchitectureRoot -ChildPath $Document))
}

function Test-LocalLinks {
    param(
        [string]$Path,
        [string]$Content
    )

    $errors = @()
    $linkMatches = [regex]::Matches($Content, '\[[^\]]+\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)')
    foreach ($linkMatch in $linkMatches) {
        $target = $linkMatch.Groups['target'].Value
        if ($target -match '^(?i)(https?://|mailto:|#)') {
            continue
        }

        $targetPath = $target.Split('#')[0].Split('?')[0]
        if ([string]::IsNullOrWhiteSpace($targetPath)) {
            continue
        }

        try {
            $resolvedTarget = [IO.Path]::GetFullPath((Join-Path -Path (Split-Path -Parent $Path) -ChildPath ([Uri]::UnescapeDataString($targetPath))))
        }
        catch {
            $errors += "Broken local link '$target' in '$Path'."
            continue
        }

        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            $errors += "Broken local link '$target' in '$Path'."
        }
    }

    return @($errors)
}

function Test-SourcePaths {
    param(
        [string]$Path,
        [string]$Content
    )

    $errors = @()
    $sourceExtensions = '(cs|xaml|cpp|c|h|vcxproj|sln|md|json|resw)$'
    $codeMatches = [regex]::Matches($Content, '`(?<value>[^`\r\n]+)`')
    foreach ($codeMatch in $codeMatches) {
        $value = $codeMatch.Groups['value'].Value.Trim()
        if ($value -notmatch '[\\/]' -or $value -notmatch "\.$sourceExtensions") {
            continue
        }

        if ($value -match '^(?i)Host Software[\\/]') {
            $candidate = Join-Path -Path $RepositoryRoot -ChildPath $value
        }
        else {
            $candidate = Join-Path -Path $HostSoftwareRoot -ChildPath $value
        }

        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            $errors += "Missing source path '$value' referenced by '$Path'."
        }
    }

    return @($errors)
}

function Test-MermaidBlocks {
    param(
        [string]$Path,
        [string]$Content,
        [bool]$Required = $true
    )

    $errors = @()
    $openingCount = [regex]::Matches($Content, '(?im)^```mermaid\s*$').Count
    $blockMatches = [regex]::Matches($Content, '(?ms)^```mermaid\s*\r?\n(?<body>.*?)^```\s*$')

    if ($Required -and $openingCount -eq 0) {
        $errors += "No Mermaid fenced block found in '$Path'."
    }
    if ($openingCount -ne $blockMatches.Count) {
        $errors += "Unclosed Mermaid fenced block in '$Path'."
    }

    $diagramStart = '^(flowchart|graph|sequenceDiagram|stateDiagram(?:-v2)?|classDiagram|erDiagram|journey|gantt|mindmap|timeline|xychart-beta|block-beta|architecture-beta|sankey-beta|quadrantChart|gitGraph|pie|C4Context|packet-beta)\b'
    foreach ($blockMatch in $blockMatches) {
        $body = $blockMatch.Groups['body'].Value.Trim()
        $firstLine = ($body -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($firstLine) -or $firstLine -notmatch $diagramStart) {
            $errors += "Invalid Mermaid diagram start in '$Path'."
        }
    }

    return @($errors)
}

function Get-MermaidBlocks {
    param([string]$Content)

    return @(
        [regex]::Matches($Content, '(?ms)^```mermaid\s*\r?\n(?<body>.*?)^```\s*$') |
            ForEach-Object { $_.Groups['body'].Value.Trim() }
    )
}

function Invoke-MermaidRenderValidation {
    param([string[]]$Paths)

    $errors = @()
    $renderRoot = Join-Path -Path ([IO.Path]::GetTempPath()) -ChildPath ("prism-doc-mermaid-" + [guid]::NewGuid().ToString('N'))
    $previousNpmCache = $env:npm_config_cache
    $rendererCommand = $null
    $rendererPrefix = @()
    $rendererLabel = $null
    $puppeteerArguments = @()

    try {
        New-Item -ItemType Directory -Path $renderRoot -Force | Out-Null
        $env:npm_config_cache = Join-Path -Path $renderRoot -ChildPath 'npm-cache'

        $mmdc = Get-Command mmdc -ErrorAction SilentlyContinue
        if ($mmdc) {
            $rendererCommand = $mmdc.Source
            $rendererLabel = 'mmdc'
        }
        else {
            $npx = Get-Command npx -ErrorAction SilentlyContinue
            if ($npx) {
                $rendererCommand = $npx.Source
                $rendererPrefix = @('--yes', '@mermaid-js/mermaid-cli')
                $rendererLabel = 'npx --yes @mermaid-js/mermaid-cli'
            }
        }

        if (-not $rendererCommand) {
            $errors += 'Mermaid renderer unavailable: neither mmdc nor npx was found.'
            return @($errors)
        }

        $browserCandidates = @(
            'C:\Program Files\Google\Chrome\Application\chrome.exe',
            'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
            'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
            'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
        )
        $browserPath = $browserCandidates |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
        if ($browserPath) {
            $puppeteerConfigPath = Join-Path -Path $renderRoot -ChildPath 'puppeteer.json'
            $puppeteerConfig = @{
                executablePath = $browserPath
                args = @('--no-sandbox', '--disable-setuid-sandbox')
            } | ConvertTo-Json
            [IO.File]::WriteAllText($puppeteerConfigPath, $puppeteerConfig)
            $puppeteerArguments = @('-p', $puppeteerConfigPath)
            Write-Host ("MERMAID_BROWSER={0}" -f $browserPath)
        }
        else {
            Write-Host 'MERMAID_BROWSER=renderer-default'
        }

        $versionOutput = @(& $rendererCommand @($rendererPrefix + @('--version')) 2>&1)
        $versionExit = $LASTEXITCODE
        Write-Host ("MERMAID_RENDERER={0}" -f $rendererLabel)
        Write-Host ("MERMAID_RENDERER_VERSION_EXIT={0}" -f $versionExit)
        Write-Host ("MERMAID_RENDERER_VERSION={0}" -f (($versionOutput | ForEach-Object { $_.ToString().Trim() }) -join ' '))
        if ($versionExit -ne 0) {
            $errors += "Mermaid renderer version probe failed with exit code $versionExit."
            return @($errors)
        }

        $diagramIndex = 0
        foreach ($path in $Paths) {
            $documentName = [IO.Path]::GetFileNameWithoutExtension($path)
            $blocks = Get-MermaidBlocks -Content ([IO.File]::ReadAllText($path))
            foreach ($block in $blocks) {
                $diagramIndex++
                $inputPath = Join-Path -Path $renderRoot -ChildPath ("{0}-{1}.mmd" -f $diagramIndex, $documentName)
                $outputPath = Join-Path -Path $renderRoot -ChildPath ("{0}-{1}.svg" -f $diagramIndex, $documentName)
                [IO.File]::WriteAllText($inputPath, $block)
                $renderArguments = @($rendererPrefix + $puppeteerArguments + @('-i', $inputPath, '-o', $outputPath))
                Write-Host ("MERMAID_RENDER_COMMAND={0} {1}" -f $rendererCommand, (($renderArguments | ForEach-Object { '"' + $_ + '"' }) -join ' '))
                $renderOutput = @(& $rendererCommand @renderArguments 2>&1)
                $renderExit = $LASTEXITCODE
                if ($renderExit -ne 0 -or -not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
                    $errors += "Mermaid render failed for '$path' block $diagramIndex with exit code $renderExit. Output: $($renderOutput -join ' ')"
                    continue
                }

                $outputInfo = Get-Item -LiteralPath $outputPath
                if ($outputInfo.Length -le 0) {
                    $errors += "Mermaid render produced an empty artifact for '$path' block $diagramIndex."
                    continue
                }

                $hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
                Write-Host ("MERMAID_RENDER_PASS document={0} block={1} bytes={2} sha256={3}" -f $path, $diagramIndex, $outputInfo.Length, $hash)
            }
        }

        Write-Host ("MERMAID_RENDER_TOTAL={0}" -f $diagramIndex)
        return @($errors)
    }
    catch {
        $errors += "Mermaid rendering failed: $($_.Exception.Message)"
        return @($errors)
    }
    finally {
        if ($null -eq $previousNpmCache) {
            Remove-Item Env:npm_config_cache -ErrorAction SilentlyContinue
        }
        else {
            $env:npm_config_cache = $previousNpmCache
        }

        if (Test-Path -LiteralPath $renderRoot) {
            Remove-Item -LiteralPath $renderRoot -Recurse -Force
        }
        Write-Host ("MERMAID_RENDER_TEMP_CLEANED={0}" -f (-not (Test-Path -LiteralPath $renderRoot)))
    }
}

function Test-IssueIds {
    param(
        [string]$Path,
        [string]$Content
    )

    $errors = @()
    $ids = @(
        [regex]::Matches($Content, '(?im)^\s*(?:Issue-ID|ID)\s*:\s*(?<id>[A-Z][A-Z0-9_-]+)\s*$') |
            ForEach-Object { $_.Groups['id'].Value }
    )
    $duplicates = @($ids | Group-Object | Where-Object { $_.Count -gt 1 })
    foreach ($duplicate in $duplicates) {
        $errors += "Duplicate issue ID '$($duplicate.Name)' in '$Path'."
    }

    return @($errors)
}

function Test-Document {
    param(
        [string]$Path,
        [bool]$RequireTopicSections,
        [bool]$RequireMermaid,
        [string]$FixtureCase
    )

    $errors = @()
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return New-ValidationResult -Path $Path -Errors @("Document does not exist: '$Path'.")
    }

    $content = [IO.File]::ReadAllText($Path)
    if ($RequireTopicSections) {
        foreach ($section in $SectionPatterns.GetEnumerator()) {
            if ($content -notmatch $section.Value) {
                $errors += "Missing required section '$($section.Key)' in '$Path'."
            }
        }
    }

    $errors += Test-LocalLinks -Path $Path -Content $content
    $errors += Test-SourcePaths -Path $Path -Content $content
    $errors += Test-MermaidBlocks -Path $Path -Content $content -Required:$RequireMermaid
    $errors += Test-IssueIds -Path $Path -Content $content

    if ($FixtureCase) {
        $fixtureRules = [ordered]@{
            'MissingPageMapping' = @{ Pattern = '(?im)^PageMapping\s*:'; Message = 'Missing page mapping marker.' }
            'MissingServiceCoverage' = @{ Pattern = '(?im)^ServiceCoverage\s*:\s*ScanFilmProfileWorkspace\s*$'; Message = 'Missing service coverage marker.' }
            'EndpointMismatch' = @{ Pattern = '(?im)619C\s+IN\s+0x82.*619D\s+OUT\s+0x01.*619D\s+ACK\s+IN\s+0x81'; Message = 'USB endpoint mapping is incomplete or mismatched.' }
            'MissingProtocolStep' = @{ Pattern = '(?im)SET_SCAN_LINES.*START_SCAN.*done\s+ACK'; Message = 'Scan protocol sequence is incomplete.' }
            'PixelTermMismatch' = @{ Pattern = '(?im)BGRA'; Message = 'Expected BGRA pixel term is missing.' }
            'MissingDngDependency' = @{ Pattern = '(?im)Adobe\s+DNG\s+SDK'; Message = 'Adobe DNG SDK dependency marker is missing.' }
        }

        if ($fixtureRules.Contains($FixtureCase) -and $content -notmatch $fixtureRules[$FixtureCase].Pattern) {
            $errors += $fixtureRules[$FixtureCase].Message
        }
    }

    return New-ValidationResult -Path $Path -Errors $errors
}

function Get-FixtureContent {
    return @'
# Validator fixture

## Scope and non-responsibilities
This fixture defines the validation boundary.

## Functionality
This fixture records the covered behavior.

## Key entry points
The fixture entry point is `Host Software/README.md`.

## Implementation mechanism
The implementation is represented by the validator skeleton.

## Control and data flow
PageMapping: MainPage -> MainViewModel
ServiceCoverage: ScanFilmProfileWorkspace
Endpoint mapping: 619C IN 0x82; 619D OUT 0x01; 619D ACK IN 0x81
Protocol: SET_SCAN_LINES -> START_SCAN -> done ACK
Pixel layout: BGRA
DNG dependency: Adobe DNG SDK

## Dependencies
The fixture has one local link and one source path.

## State and concurrency
The fixture is immutable during validation.

## Error handling
Validation errors are returned to the caller.

## Test coverage
The fixture is exercised by the self-test harness.

## Known issues and solutions
Issue-ID: ISSUE-001

## Related source
`Host Software/README.md`

[Fixture target](target.md)

```mermaid
flowchart LR
    A[Fixture] --> B[Validator]
```
'@
}

function Invoke-SelfTest {
    param([string]$Case)

    $fixtureRoot = Join-Path -Path ([IO.Path]::GetTempPath()) -ChildPath ("prism-doc-validator-" + [guid]::NewGuid().ToString('N'))
    $fixturePath = Join-Path -Path $fixtureRoot -ChildPath 'fixture.md'
    $targetPath = Join-Path -Path $fixtureRoot -ChildPath 'target.md'
    $exitCode = 0
    $cleaned = $false

    try {
        New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
        [IO.File]::WriteAllText($fixturePath, (Get-FixtureContent))
        [IO.File]::WriteAllText($targetPath, '# Target')

        $content = [IO.File]::ReadAllText($fixturePath)
        switch ($Case) {
            'MissingSection' { $content = $content -replace '(?m)^## Functionality\r?\n', '' }
            'MissingPageMapping' { $content = $content -replace '(?m)^PageMapping:.*\r?\n', '' }
            'MissingServiceCoverage' { $content = $content -replace '(?m)^ServiceCoverage:.*\r?\n', '' }
            'EndpointMismatch' { $content = $content.Replace('619C IN 0x82', '619C IN 0x81') }
            'MissingProtocolStep' { $content = $content -replace ' -> done ACK', '' }
            'PixelTermMismatch' { $content = $content.Replace('BGRA', 'RGBA') }
            'MissingDngDependency' { $content = $content.Replace('Adobe DNG SDK', 'native SDK') }
            'DuplicateIssueId' { $content = $content.Replace('Issue-ID: ISSUE-001', "Issue-ID: ISSUE-001`r`nIssue-ID: ISSUE-001") }
            'BrokenLink' { $content = $content.Replace('(target.md)', '(missing-target.md)') }
            'BadSourcePath' { $content = $content.Replace('Host Software/README.md', 'Host Software/does-not-exist.cs') }
            'BadMermaid' { $content = $content.Replace('flowchart LR', 'not-a-mermaid-diagram') }
        }
        [IO.File]::WriteAllText($fixturePath, $content)

        $result = Test-Document -Path $fixturePath -RequireTopicSections:$true -RequireMermaid:$true -FixtureCase $Case
        if ($result.Valid) {
            Write-Error "Self-test '$Case' did not reject its damaged fixture."
            $exitCode = 1
        }
        else {
            Write-Host "Self-test '$Case': damaged fixture rejected."
            foreach ($errorMessage in $result.Errors) {
                Write-Host "  $errorMessage"
            }
        }
    }
    catch {
        Write-Error "Self-test '$Case' failed: $($_.Exception.Message)"
        $exitCode = 1
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
        $cleaned = -not (Test-Path -LiteralPath $fixtureRoot)
        Write-Host "Self-test temp fixture cleaned: $cleaned"
        if (-not $cleaned) {
            $exitCode = 1
        }
    }

    return $exitCode
}

if ($SelfTest) {
    $selfTestExitCode = Invoke-SelfTest -Case $SelfTest
    exit $selfTestExitCode
}

if ($Mode -eq 'Partial' -and $Files.Count -eq 0) {
    Write-Output 'Partial mode: no files supplied; validation skipped.'
    exit 0
}

$pathsToValidate = @()
if ($Mode -eq 'Full') {
    $pathsToValidate = @($Deliverables | ForEach-Object { Resolve-DocumentPath -Document $_ })
}
else {
    foreach ($file in $Files) {
        $candidate = Resolve-DocumentPath -Document $file
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $pathsToValidate += $candidate
        }
        else {
            Write-Output "Partial mode: skipped missing file '$file'."
        }
    }
}

$validationFailures = @()
foreach ($path in $pathsToValidate) {
    $name = Split-Path -Leaf $path
    $requireTopicSections = $TopicDeliverables -contains $name
    $requireMermaid = $name -ne 'README.md'
    $result = Test-Document -Path $path -RequireTopicSections:$requireTopicSections -RequireMermaid:$requireMermaid
    if ($result.Valid) {
        Write-Output "PASS $path"
    }
    else {
        $validationFailures += $result
        foreach ($errorMessage in $result.Errors) {
            Write-Error $errorMessage
        }
    }
}

if ($Mode -eq 'Full') {
    $missing = @($pathsToValidate | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($missing.Count -gt 0) {
        Write-Error ("Full mode missing {0} deliverable(s): {1}" -f $missing.Count, (($missing | ForEach-Object { Split-Path -Leaf $_ }) -join ', '))
        exit 1
    }
}

if ($validationFailures.Count -gt 0) {
    exit 1
}

if ($Mode -eq 'Full') {
    $renderFailures = @(Invoke-MermaidRenderValidation -Paths $pathsToValidate)
    if ($renderFailures.Count -gt 0) {
        foreach ($renderFailure in $renderFailures) {
            Write-Error $renderFailure
        }
        exit 1
    }
}

Write-Output ("{0} mode validation passed for {1} existing file(s)." -f $Mode, $pathsToValidate.Count)
exit 0
