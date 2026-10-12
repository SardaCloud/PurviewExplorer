# Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
# Licensed under the MIT License. See LICENSE in the repository root.

<#
.SYNOPSIS
    Installs Purview Explorer silently for the current user, checks the result, and uninstalls it.

.DESCRIPTION
    Runs the setup with /VERYSILENT /CURRENTUSER into a temporary folder and checks:
    the files, the Start menu shortcut, the uninstall entry, Setup's requirement checks and
    verification (in Setup's log), and the installed application's /SelfTest. Then it uninstalls
    and checks that everything is gone.

    With -ExpectModuleInstall the ExchangeOnlineManagement module must be missing before Setup
    runs, and Setup must install it from the PowerShell Gallery (used in CI on a clean runner).

.EXAMPLE
    .\installer\test-setup.ps1 -Setup .\artifacts\PurviewExplorer-0.2.0-setup.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Setup,
    [switch] $ExpectModuleInstall,
    [switch] $KeepInstalled
)
$ErrorActionPreference = 'Stop'
$Setup = (Resolve-Path $Setup).Path
$appId = '{C8A6D82E-0CDD-424C-8ADC-1A8BD8EC3918}_is1'
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId"
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Purview Explorer.lnk'
$work = Join-Path ([IO.Path]::GetTempPath()) ('pe-setup-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$dir = Join-Path $work 'Purview Explorer'
$log = Join-Path $work 'setup.log'
New-Item -ItemType Directory -Force -Path $work | Out-Null
$failures = 0

function Check([bool] $Condition, [string] $What) {
    if ($Condition) { Write-Host "PASS $What" } else { Write-Host "FAIL $What" -ForegroundColor Red; $script:failures++ }
}

function Find-Module {
    $roots = @(
        (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WindowsPowerShell\Modules'),
        (Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules')
    )
    foreach ($r in $roots) { Get-ChildItem (Join-Path $r 'ExchangeOnlineManagement') -Directory -ErrorAction SilentlyContinue }
}

if (Test-Path $uninstallKey) { throw 'Purview Explorer is already installed for this user; uninstall it before running this test.' }
if ($ExpectModuleInstall) { Check (-not (Find-Module)) 'ExchangeOnlineManagement is missing before Setup runs' }

Write-Host "Installing into $dir"
$p = Start-Process -FilePath $Setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$dir`"", "/LOG=`"$log`"" -PassThru -Wait
Check ($p.ExitCode -eq 0) "Setup exit code ($($p.ExitCode))"
$setupLog = if (Test-Path $log) { Get-Content $log -Raw } else { '' }

Check (Test-Path (Join-Path $dir 'PurviewExplorer.exe')) 'PurviewExplorer.exe installed'
Check (Test-Path (Join-Path $dir 'PurviewExplorer.exe.config')) 'PurviewExplorer.exe.config installed'
Check (Test-Path (Join-Path $dir 'LICENSE.txt')) 'LICENSE.txt installed'
Check (Test-Path $shortcut) 'Start menu shortcut created'
$entry = Get-ItemProperty $uninstallKey -ErrorAction SilentlyContinue
Check ($entry -and $entry.DisplayName -eq 'Purview Explorer') 'Uninstall entry: DisplayName'
Check ($entry -and $entry.Publisher -eq 'Sarda Cloud Community') 'Uninstall entry: Publisher'
$exeVersion = (Get-Item (Join-Path $dir 'PurviewExplorer.exe') -ErrorAction SilentlyContinue).VersionInfo.ProductVersion
Check ($entry -and $exeVersion -and $entry.DisplayVersion -eq $exeVersion) "Uninstall entry: DisplayVersion ($($entry.DisplayVersion))"

Check ($setupLog -match '\.NET Framework: Version 4\.8') 'Setup detected .NET Framework 4.8'
Check ($setupLog -match 'Windows PowerShell: Version 5\.1') 'Setup detected Windows PowerShell 5.1'
if ($ExpectModuleInstall) {
    Check ($setupLog -match 'ExchangeOnlineManagement: not found') 'Setup found the module missing'
    Check ($setupLog -match 'Installed ExchangeOnlineManagement \d') 'Setup installed the module from the PowerShell Gallery'
    Check ([bool](Find-Module)) 'ExchangeOnlineManagement is present after Setup'
}
Check ($setupLog -match 'Verification passed\.') 'Setup verification passed (module loads in Purview Explorer)'

$selfTest = Join-Path $work 'selftest.log'
$st = Start-Process -FilePath (Join-Path $dir 'PurviewExplorer.exe') -ArgumentList "`"/SelfTest:$selfTest`"" -PassThru -Wait
Check ($st.ExitCode -eq 0 -and (Get-Content $selfTest -ErrorAction SilentlyContinue | Select-Object -Last 1) -eq 'RESULT: PASS') 'Installed application passes /SelfTest'

if (-not $KeepInstalled) {
    Write-Host 'Uninstalling'
    $uninstaller = Join-Path $dir 'unins000.exe'
    Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait
    # The uninstaller relaunches itself from %TEMP%, so wait for its effects rather than the process.
    $deadline = (Get-Date).AddSeconds(90)
    while ((Test-Path $uninstallKey) -or (Test-Path (Join-Path $dir 'PurviewExplorer.exe'))) {
        if ((Get-Date) -gt $deadline) { break }
        Start-Sleep -Milliseconds 500
    }
    Check (-not (Test-Path (Join-Path $dir 'PurviewExplorer.exe'))) 'Uninstall removed the files'
    Check (-not (Test-Path $shortcut)) 'Uninstall removed the Start menu shortcut'
    Check (-not (Test-Path $uninstallKey)) 'Uninstall removed the uninstall entry'
}

if ($failures -gt 0 -and (Test-Path $log)) {
    Write-Host "`n----- Setup log -----"
    Get-Content $log | Select-Object -Last 80
}
if (-not $KeepInstalled) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
if ($failures -gt 0) { Write-Host "$failures check(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All setup checks passed.'
exit 0
