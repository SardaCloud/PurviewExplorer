# Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
# Licensed under the MIT License. See LICENSE in the repository root.

<#
.SYNOPSIS
    Builds Purview Explorer, runs the tests and optionally packages a zip and a setup.

.EXAMPLE
    .\build.ps1                 build Release and run all tests
.EXAMPLE
    .\build.ps1 -SkipTests      build only
.EXAMPLE
    .\build.ps1 -Package        also create artifacts\PurviewExplorer-<version>.zip
.EXAMPLE
    .\build.ps1 -Setup          also create artifacts\PurviewExplorer-<version>-setup.exe (needs Inno Setup 6)
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Package,
    [switch]$Setup
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
Write-Host "Purview Explorer $version ($Configuration)"

dotnet build PurviewExplorer.sln -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    dotnet test PurviewExplorer.sln -c $Configuration --no-build -nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

$bin = Join-Path $PSScriptRoot "src\PurviewExplorer\bin\$Configuration\net48"
$exe = Join-Path $bin 'PurviewExplorer.exe'
$artifacts = Join-Path $PSScriptRoot 'artifacts'

if ($Package -or $Setup) {
    $fileVersion = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
    if ($fileVersion -ne $version) { throw "PurviewExplorer.exe reports $fileVersion, but VERSION says $version." }
    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
}

if ($Package) {
    $staging = Join-Path $artifacts "PurviewExplorer-$version"
    $zip = "$staging.zip"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    Copy-Item $exe, (Join-Path $bin 'PurviewExplorer.exe.config'), README.md, LICENSE, THIRD-PARTY-NOTICES.md -Destination $staging
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
    Write-Host "Packaged $zip"
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

    & $iscc /Qp "/DAppVersion=$version" "/DBinDir=$bin" "/O$artifacts" (Join-Path $PSScriptRoot 'installer\PurviewExplorer.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Building the setup failed.' }
    Write-Host "Built $(Join-Path $artifacts "PurviewExplorer-$version-setup.exe")"
}
