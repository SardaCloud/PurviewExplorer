# Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
# Licensed under the MIT License. See LICENSE in the repository root.

<#
.SYNOPSIS
    Builds Purview Configuration Viewer, runs the tests and optionally packages a zip.

.EXAMPLE
    .\build.ps1                 build Release and run all tests
.EXAMPLE
    .\build.ps1 -SkipTests      build only
.EXAMPLE
    .\build.ps1 -Package        also create artifacts\PurviewViewer-<version>.zip
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Package
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
Write-Host "Purview Configuration Viewer $version ($Configuration)"

dotnet build PurviewViewer.sln -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    dotnet test PurviewViewer.sln -c $Configuration --no-build -nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

if ($Package) {
    $bin = Join-Path $PSScriptRoot "src\PurviewViewer\bin\$Configuration\net48"
    $exe = Join-Path $bin 'PurviewViewer.exe'
    $fileVersion = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
    if ($fileVersion -ne $version) { throw "PurviewViewer.exe reports $fileVersion, but VERSION says $version." }

    $staging = Join-Path $PSScriptRoot "artifacts\PurviewViewer-$version"
    $zip = "$staging.zip"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    Copy-Item $exe, (Join-Path $bin 'PurviewViewer.exe.config'), README.md, LICENSE, THIRD-PARTY-NOTICES.md -Destination $staging
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
    Write-Host "Packaged $zip"
}
