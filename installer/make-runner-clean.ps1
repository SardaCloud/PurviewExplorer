# Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
# Licensed under the MIT License. See LICENSE in the repository root.

# Removes what a GitHub Windows runner has but a freshly installed Windows doesn't, so Setup's module
# installation is tested from scratch: the ExchangeOnlineManagement module, PowerShellGet 2.x /
# PackageManagement 1.4.x and the NuGet provider. The PowerShellGet and PackageManagement 1.0.0.1 that
# ship with Windows live next to the newer versions and are kept.
$ErrorActionPreference = 'Stop'

$docs = [Environment]::GetFolderPath('MyDocuments')
$windowsPowerShellModules = Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules'

# The module itself, wherever it is.
foreach ($root in (Join-Path $docs 'WindowsPowerShell\Modules'), (Join-Path $docs 'PowerShell\Modules'),
                  $windowsPowerShellModules, (Join-Path $env:ProgramFiles 'PowerShell\Modules')) {
    $dir = Join-Path $root 'ExchangeOnlineManagement'
    if (Test-Path $dir) { Write-Host "Removing $dir"; Remove-Item $dir -Recurse -Force }
}

foreach ($name in 'PowerShellGet', 'PackageManagement') {
    # Per-user copies for Windows PowerShell: none on a fresh Windows.
    $dir = Join-Path $docs "WindowsPowerShell\Modules\$name"
    if (Test-Path $dir) { Write-Host "Removing $dir"; Remove-Item $dir -Recurse -Force }
    # All-users copies: keep only the in-box 1.0.0.1.
    Get-ChildItem (Join-Path $windowsPowerShellModules $name) -Directory -ErrorAction SilentlyContinue |
        Where-Object Name -ne '1.0.0.1' |
        ForEach-Object { Write-Host "Removing $($_.FullName)"; Remove-Item $_.FullName -Recurse -Force }
}

foreach ($dir in (Join-Path $env:ProgramFiles 'PackageManagement\ProviderAssemblies\nuget'),
                 (Join-Path $env:LOCALAPPDATA 'PackageManagement\ProviderAssemblies\nuget')) {
    if (Test-Path $dir) { Write-Host "Removing $dir"; Remove-Item $dir -Recurse -Force }
}

# Check the result in Windows PowerShell, with its own module path (this script may run in PowerShell 7).
$env:PSModulePath = [Environment]::GetEnvironmentVariable('PSModulePath', 'Machine')
$report = powershell.exe -NoProfile -Command @'
$v = Get-Module -ListAvailable PowerShellGet, PackageManagement | ForEach-Object { "$($_.Name) $($_.Version)" }
"Left: $($v -join ', ')"
"ExchangeOnlineManagement: $(@(Get-Module -ListAvailable ExchangeOnlineManagement).Count) found"
"NuGet provider: $(@(Get-PackageProvider -ListAvailable -Name NuGet -ErrorAction SilentlyContinue).Count) found"
'@
$report | ForEach-Object { Write-Host $_ }
$text = $report -join "`n"
if ($text -notmatch 'PowerShellGet 1\.0\.0\.1' -or $text -notmatch 'PackageManagement 1\.0\.0\.1') { throw 'The in-box PowerShellGet / PackageManagement 1.0.0.1 is missing.' }
if ($text -match 'PowerShellGet 2\.' -or $text -match 'PackageManagement 1\.4') { throw 'Newer PowerShellGet / PackageManagement versions are still present.' }
if ($text -notmatch 'ExchangeOnlineManagement: 0 found') { throw 'ExchangeOnlineManagement is still present.' }
if ($text -notmatch 'NuGet provider: 0 found') { throw 'The NuGet provider is still present.' }
Write-Host 'The runner now looks like a freshly installed Windows.'
