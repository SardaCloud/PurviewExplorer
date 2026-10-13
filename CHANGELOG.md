# Changelog

Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community. Licensed under the MIT License.

All notable changes to Purview Explorer (called Purview Configuration Viewer in 0.1.0).

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2026-10-13

### Added

- **Other organizations.** An account with access to several organizations can connect to any of them: a partner
  managing customer organizations (CSP or GDAP) or a guest with a Purview role in another organization. In
  *File > Connect to Tenant*, *Interactive sign-in* has a new optional **Other organization** box for the other
  organization's `*.onmicrosoft.com` domain. Purview Explorer then signs in at that organization and maps the
  session to it (`Connect-IPPSSession -DelegatedOrganization` with the organization's own sign-in address, in every
  supported cloud), as Microsoft documents for customer organizations.
- **Recent organizations.** The last ten other organizations and certificate organizations connected to are
  offered in both organization boxes (interactive and certificate). Settings from earlier versions start the list
  with the certificate organization.
- The prerequisite report lists whether the installed module supports `DelegatedOrganization`.

### Changed

- When connected to another organization, the title bar, status bar, Summary tab, snapshot file name and
  snapshot `source.organization` name that organization instead of the domain of the account that signed in.
- The certificate *Organization* box checks for a domain name such as `contoso.onmicrosoft.com`.

## [0.3.0] - 2026-10-12

### Added

- **x64 and Arm64 builds.** Each release has a setup and a zip per architecture
  (`PurviewExplorer-0.3.0-win-x64-setup.exe`, `-win-arm64-setup.exe`, `-win-x64.zip`, `-win-arm64.zip`). The x64
  build targets .NET Framework 4.8; the Arm64 build targets .NET Framework 4.8.1 and runs natively on Windows 11
  on Arm. Each setup checks the requirements of its architecture and refuses to run on the other one.
- Help > About and the prerequisite report show the architecture.

### Changed

- **Privacy.** The prerequisite report (Help > Check Prerequisites, `/CheckPrerequisites`, and the setup's last
  page) replaces the user's own folders with placeholders such as `<Documents>`, and the setup's requirements
  page says whether the module is installed for the current user or for all users instead of showing its folder.
  Reports can be shared without revealing the user name. The documentation screenshots were replaced: earlier ones
  showed an account and folder path.

## [0.2.0] - 2026-10-12

### Added

- **Setup** (`PurviewExplorer-0.2.0-setup.exe`), a wizard that:
  - installs for the current user without administrator rights, or for all users;
  - **checks the requirements** and shows what it found: Windows 10 version 1607 / Windows Server 2016 or later,
    .NET Framework 4.8, Windows PowerShell 5.1 and the ExchangeOnlineManagement module 3.2.0 or later, with a
    *Check again* button, and doesn't continue while one of the first three is missing;
  - **installs the ExchangeOnlineManagement module** from the PowerShell Gallery when it's missing or too old
    (bootstrapping the NuGet provider if needed);
  - **checks the installation** by running `PurviewExplorer.exe /CheckPrerequisites` and shows the report;
  - adds a Start menu shortcut, an optional desktop shortcut and an uninstall entry, asks to close a running copy
    before upgrading or uninstalling, and supports unattended installs (`/VERYSILENT`, `/LOG`, `/MERGETASKS`).
- `/CheckPrerequisites[:report.txt]` command-line switch: checks that the module loads without signing in, shows
  or writes the report, and exits with 0 when everything is ready.
- The prerequisite report now includes the Windows, .NET Framework and PowerShell engine versions.

### Changed

- **Renamed to Purview Explorer.** The executable is `PurviewExplorer.exe`, the repository is
  [SardaCloud/PurviewExplorer](https://github.com/SardaCloud/PurviewExplorer) (the old address redirects), and
  settings are in `%APPDATA%\SardaCloud\PurviewExplorer`; settings from 0.1.0 are moved there automatically.
  Snapshots are now saved as `PurviewExplorer.Snapshot`; snapshots saved by 0.1.0 still open.

## [0.1.0] - 2026-10-12

First public preview, as Purview Configuration Viewer: Microsoft Purview configuration in a classic Windows
application, read-only.

### Added

- **A tab per solution**: Information Protection, Data Classification, Data Loss Prevention, Data Lifecycle
  Management, Records Management, eDiscovery, Audit, Insider Risk Management, Communication Compliance,
  Information Barriers, Alert Policies and Permissions, plus a Summary of the source and every collection.
- **43 collections** read with Security & Compliance PowerShell `Get-*` cmdlets, through Windows PowerShell 5.1
  hosted in the application and the ExchangeOnlineManagement module (3.2.0 or later).
- **Sign-in** with Web Account Manager (falling back to a browser window when WAM isn't usable), a browser window,
  or certificate-based app-only authentication; Commercial/GCC, GCC High, DoD and 21Vianet environments.
- **Lazy loading** per tab, *Load All*, refresh and stop; collections the account can't read are marked
  *not available*.
- **Properties dialog** with all properties, related items (a policy's rules, a label's sublabels, a case's
  holds) and the raw JSON; a value viewer that pretty-prints embedded JSON and XML.
- **Sensitivity label hierarchy**, a filter that searches every property, column sorting, copy and **CSV export**.
- **Snapshots**: save and open the whole configuration as indented, diff-friendly JSON.
- **Demo data**: a fictional Contoso tenant, embedded in the executable.
- **Help > Check Prerequisites**: verifies that the module loads in the hosted runspace, without signing in.
- `/SelfTest`, which drives the whole UI with the demo data, and xUnit tests including hosted-runspace
  integration tests.

[0.4.0]: https://github.com/SardaCloud/PurviewExplorer/releases/tag/v0.4.0
[0.3.0]: https://github.com/SardaCloud/PurviewExplorer/releases/tag/v0.3.0
[0.2.0]: https://github.com/SardaCloud/PurviewExplorer/releases/tag/v0.2.0
[0.1.0]: https://github.com/SardaCloud/PurviewExplorer/releases/tag/v0.1.0
