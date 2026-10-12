# Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
# Licensed under the MIT License. See LICENSE in the repository root.

<#
.SYNOPSIS
    Builds Purview Explorer, runs the tests and optionally packages zips and setups for x64 and ARM64.

.EXAMPLE
    .\build.ps1                       build Release and run all tests
.EXAMPLE
    .\build.ps1 -SkipTests            build only
.EXAMPLE
    .\build.ps1 -Package              also artifacts\PurviewExplorer-<version>-win-<arch>.zip for x64 and ARM64
.EXAMPLE
    .\build.ps1 -Setup -Arch x64      also artifacts\PurviewExplorer-<version>-win-x64-setup.exe (needs Inno Setup 6)
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'arm64')]
    [string[]]$Arch = @('x64', 'arm64'),
    [switch]$SkipTests,
    [switch]$Package,
    [switch]$Setup
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
Write-Host "Purview Explorer $version ($Configuration)"

# The tests use the AnyCPU build.
dotnet build PurviewExplorer.sln -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if (-not $SkipTests) {
    dotnet test PurviewExplorer.sln -c $Configuration --no-build -nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
if (-not ($Package -or $Setup)) { return }

# The released builds, one per architecture.
$targets = @{
    x64   = @{ Platform = 'x64';   Framework = 'net48';  Machine = 0x8664 }
    arm64 = @{ Platform = 'ARM64'; Framework = 'net481'; Machine = 0xAA64 }
}
$artifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

function Get-PeMachine([string] $Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    [BitConverter]::ToUInt16($bytes, $pe + 4)
}

if ($Setup) {
    $iscc = @(
        $env:ISCC
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 is needed for -Setup: winget install JRSoftware.InnoSetup' }
}

foreach ($a in $Arch) {
    $t = $targets[$a]
    dotnet build (Join-Path $PSScriptRoot 'src\PurviewExplorer\PurviewExplorer.csproj') -c $Configuration "-p:Platform=$($t.Platform)" -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $a." }
    $bin = Join-Path $PSScriptRoot "src\PurviewExplorer\bin\$($t.Platform)\$Configuration\$($t.Framework)"
    $exe = Join-Path $bin 'PurviewExplorer.exe'

    $fileVersion = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
    if ($fileVersion -ne $version) { throw "PurviewExplorer.exe ($a) reports $fileVersion, but VERSION says $version." }
    $machine = Get-PeMachine $exe
    if ($machine -ne $t.Machine) { throw ('PurviewExplorer.exe ({0}) is built for machine 0x{1:X4}, not 0x{2:X4}.' -f $a, $machine, $t.Machine) }

    if ($Package) {
        $staging = Join-Path $artifacts "PurviewExplorer-$version-win-$a"
        $zip = "$staging.zip"
        if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $staging | Out-Null
        Copy-Item $exe, (Join-Path $bin 'PurviewExplorer.exe.config'), README.md, LICENSE, THIRD-PARTY-NOTICES.md -Destination $staging
        if (Test-Path $zip) { Remove-Item $zip -Force }
        Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
        Remove-Item $staging -Recurse -Force
        Write-Host "Packaged $zip"
    }

    if ($Setup) {
        & $iscc /Qp "/DAppVersion=$version" "/DArch=$a" "/DBinDir=$bin" "/O$artifacts" (Join-Path $PSScriptRoot 'installer\PurviewExplorer.iss')
        if ($LASTEXITCODE -ne 0) { throw "Building the $a setup failed." }
        Write-Host "Built $(Join-Path $artifacts "PurviewExplorer-$version-win-$a-setup.exe")"
    }
}
