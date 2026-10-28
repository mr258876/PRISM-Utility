[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

if (-not (Test-Path -LiteralPath $vswherePath)) {
    throw 'Install Visual Studio with WinUI and C++ desktop development tools first.'
}

$installationPaths = & $vswherePath -all -products '*' -property installationPath
$msbuildPath = $null
foreach ($installationPath in $installationPaths) {
    $candidate = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
    $toolsetPath = Join-Path $installationPath 'MSBuild\Microsoft\VC\v170\Platforms\x64\PlatformToolsets\v143\Toolset.props'
    if ((Test-Path -LiteralPath $candidate) -and (Test-Path -LiteralPath $toolsetPath)) {
        $msbuildPath = $candidate
        break
    }
}

if ($null -eq $msbuildPath) {
    throw 'Add the MSVC v143 x64/x86 build tools to Visual Studio, then run this script again.'
}

$sdkRoot = Join-Path $repositoryRoot 'DngSdkWarpper\Adobe_DNG_SDK'
foreach ($relativePath in @('dng_sdk\source\dng_host.h', 'libjxl\libjxl\lib\include', 'libjxl\client_projects\include', 'xmp\toolkit\third-party\zlib')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sdkRoot $relativePath))) {
        throw "Extract the Adobe DNG SDK into '$sdkRoot' before building. Missing: $relativePath"
    }
}

Push-Location $repositoryRoot
try {
    # The app requires the native bridge to exist before its build starts.
    & $msbuildPath 'DngSdkWarpper\DngSdkWarpper.vcxproj' /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /m:1 /nr:false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Native DNG build failed with exit code $LASTEXITCODE." }

    & $msbuildPath 'PRISM Utility.sln' /restore /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /p:PlatformTarget=x64 /m:1 /nr:false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Solution build failed with exit code $LASTEXITCODE." }

    if ($Test) {
        & dotnet test 'PrismUtility.Core.Tests\PrismUtility.Core.Tests.csproj' --no-build --no-restore -c $Configuration -p:PlatformTarget=x64 --blame-hang-timeout 5min --logger 'console;verbosity=minimal'
        if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
    }
}
finally {
    Pop-Location
}
