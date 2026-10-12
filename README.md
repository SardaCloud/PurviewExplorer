<p align="center">
  <img src="assets/PurviewViewer-256.png" width="104" alt="Purview Configuration Viewer icon: the Sarda Cloud sheep with a magnifying glass">
</p>

<h1 align="center">Purview Configuration Viewer</h1>

<p align="center">
  <b>Microsoft Purview configuration in a classic Windows application, one tab per solution.</b><br>
  Sensitivity labels, DLP, retention, records, eDiscovery, audit, insider risk, communication compliance and information barriers, read-only.
</p>

<p align="center">
  <a href="https://github.com/SardaCloud/PurviewViewer/actions/workflows/build.yml"><img src="https://github.com/SardaCloud/PurviewViewer/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://github.com/SardaCloud/PurviewViewer/releases/latest"><img src="https://img.shields.io/github/v/release/SardaCloud/PurviewViewer" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT License"></a>
</p>

<p align="center">
  <a href="https://www.sardacloud.com">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="assets/sardacloud-logo-white.png">
      <img src="assets/sardacloud-logo-blue.png" width="200" alt="Sarda Cloud">
    </picture>
  </a>
</p>

<p align="center">A <b>Sarda Cloud Community</b> project by Francesco Valerio Buccoli. Licensed under the <a href="LICENSE">MIT License</a>.</p>

---

![Information Protection tab](docs/images/information-protection.png)

## Why

The Microsoft Purview portal shows each policy on its own page, a few clicks deep, and some settings (DLP
advanced rules, label actions, endpoint DLP settings, file plan metadata) only through PowerShell. Reviewing a
tenant's configuration, or comparing it with last month's, means a lot of clicking or a lot of scripting.

Purview Configuration Viewer reads the configuration with the **read-only `Get-*` cmdlets** of Security &
Compliance PowerShell and shows it in a familiar Windows application: menu bar, toolbar, a tab per solution,
list views, property sheets and a status bar. It **never changes anything** in the tenant; the session layer
refuses to run any cmdlet that doesn't start with `Get-`.

- **Everything in one place.** 12 solutions, 43 kinds of configuration object, with the relations between them.
- **Offline snapshots.** Save the whole configuration to a diff-friendly JSON file and open it later, anywhere.
- **One small file.** A 400 KB `PurviewViewer.exe` for .NET Framework 4.8, which is part of Windows.

## Features

- **A tab per solution**: Information Protection, Data Classification, Data Loss Prevention, Data Lifecycle
  Management, Records Management, eDiscovery, Audit, Insider Risk Management, Communication Compliance,
  Information Barriers, Alert Policies and Permissions, plus a **Summary** tab.
- Each tab lists its **collections** (for example *DLP policies*, *DLP rules*, *Global and endpoint settings*),
  the **items** of the selected collection and the **properties** of the selected item.
- **Properties dialog** (double-click or Alt+Enter) with all properties, **related items** (a policy's rules, a
  label's sublabels, a case's holds) and the raw **JSON**.
- **Value viewer** (double-click a property): embedded JSON (label actions, advanced DLP rules) and XML (rule
  packages, EDM schemas) are pretty-printed.
- Sensitivity labels are shown as a **hierarchy** (sublabels indented under their parent).
- **Filter** box that searches every property of every item, not just the visible columns.
- Collections load **on demand** when you open a tab; *Load All* (Ctrl+L) retrieves everything. Collections your
  account can't read are marked *not available* instead of failing the whole view.
- **Snapshots**: save everything to an indented JSON file and open it later, offline, on any machine, without
  signing in. Snapshots are stable and diff-friendly, so you can compare configurations over time.
- **Export to CSV** (Excel-friendly; formula injection is neutralized).
- **Demo data** (File > Open Demo Data): a fictional *Contoso* tenant for trying the application.
- Sign-in with **Web Account Manager**, a **browser window**, or **certificate-based app-only** authentication;
  Commercial/GCC, GCC High, DoD and 21Vianet environments.

| Summary | Properties dialog | Connect |
|---|---|---|
| ![Summary](docs/images/summary.png) | ![Properties](docs/images/properties-dialog.png) | ![Connect](docs/images/connect-dialog.png) |

## Download

Get `PurviewViewer-<version>.zip` from the [latest release](https://github.com/SardaCloud/PurviewViewer/releases/latest),
unzip it anywhere and run `PurviewViewer.exe`. Nothing needs installing apart from the PowerShell module below.

The executable isn't code-signed, so Windows SmartScreen may warn the first time you run it; choose **More
info > Run anyway**, or build it yourself. Each release lists the SHA-256 hash of the zip in `SHA256SUMS.txt`.

## Requirements

- Windows 10 or 11, or Windows Server 2016 or later (.NET Framework 4.8 and Windows PowerShell 5.1 are built in).
- The **ExchangeOnlineManagement** PowerShell module, version 3.2.0 or later:

  ```powershell
  Install-Module ExchangeOnlineManagement -Scope CurrentUser
  ```

  The module is found whether it was installed for Windows PowerShell or PowerShell 7. *Help > Check
  Prerequisites* verifies that it loads, without signing in.
- An account with permission to read the Purview configuration, for example a member of the *Compliance
  Administrator* or *Organization Management* role group. Some collections need specific roles (eDiscovery,
  Insider Risk Management, Communication Compliance); without them those collections show as *not available*.

## Usage

1. **File > Connect to Tenant**, choose the environment and the sign-in method, and sign in.
2. Pick a tab. Its collections load in the background; select a collection to see its items.
3. Select an item to see its properties, or double-click it for the full property sheet.
4. **File > Save Snapshot** to keep a copy you can open later with **File > Open Snapshot**.

| Key | Action |
|---|---|
| F5 | Refresh the selected collection |
| Shift+F5 | Refresh every collection of the tab |
| Ctrl+L | Load all collections |
| Ctrl+F5 | Refresh all collections |
| Ctrl+F | Filter the list |
| Alt+Enter, Enter | Properties of the selected item |
| Ctrl+C | Copy the selected rows (tab-separated) |
| Ctrl+E | Export the list to CSV |
| Ctrl+O / Ctrl+S | Open / save a snapshot |

Command line:

```text
PurviewViewer.exe [snapshot.json]     open a snapshot
PurviewViewer.exe /Demo               open the demo data
PurviewViewer.exe /SelfTest:log.txt   drive the UI with the demo data and write PASS/FAIL lines (exit code 0 = all passed)
```

### Sign-in options

- **Interactive** (default): the module signs in with Web Account Manager (WAM). If WAM isn't usable from the
  application, it automatically falls back to a browser window; you can also choose the browser directly.
- **Certificate (app-only)**: application ID, certificate thumbprint (CurrentUser\My or LocalMachine\My) and the
  organization's `*.onmicrosoft.com` domain. See
  [App-only authentication for unattended scripts](https://aka.ms/exo-cba) for setting up the app registration
  and assigning it a Purview role.

Settings (last environment, account, app ID, thumbprint, window position) are saved in
`%APPDATA%\SardaCloud\PurviewViewer\settings.json`. No passwords, tokens or keys are stored.

## What is retrieved

| Solution | Collection | Command |
|---|---|---|
| Information Protection | Sensitivity labels | `Get-Label -IncludeDetailedLabelActions` |
| Information Protection | Label policies | `Get-LabelPolicy` |
| Information Protection | Auto-labeling policies | `Get-AutoSensitivityLabelPolicy` |
| Information Protection | Auto-labeling rules | `Get-AutoSensitivityLabelRule` |
| Data Classification | Sensitive information types | `Get-DlpSensitiveInformationType` |
| Data Classification | SIT rule packages | `Get-DlpSensitiveInformationTypeRulePackage` |
| Data Classification | Keyword dictionaries | `Get-DlpKeywordDictionary` |
| Data Classification | Exact data match schemas | `Get-DlpEdmSchema` |
| Data Loss Prevention | DLP policies | `Get-DlpCompliancePolicy` |
| Data Loss Prevention | DLP rules | `Get-DlpComplianceRule` |
| Data Loss Prevention | Custom policy tips | `Get-PolicyTipConfig` |
| Data Loss Prevention | Global and endpoint settings | `Get-PolicyConfig` |
| Data Lifecycle Management | Retention policies | `Get-RetentionCompliancePolicy -RetentionRuleTypes` |
| Data Lifecycle Management | Retention rules | `Get-RetentionComplianceRule` |
| Data Lifecycle Management | App retention policies | `Get-AppRetentionCompliancePolicy` |
| Data Lifecycle Management | App retention rules | `Get-AppRetentionComplianceRule` |
| Data Lifecycle Management | Adaptive scopes | `Get-AdaptiveScope` |
| Records Management | Retention labels | `Get-ComplianceTag` |
| Records Management | Event types | `Get-ComplianceRetentionEventType` |
| Records Management | Events | `Get-ComplianceRetentionEvent` |
| Records Management | File plan: departments | `Get-FilePlanPropertyDepartment` |
| Records Management | File plan: categories | `Get-FilePlanPropertyCategory` |
| Records Management | File plan: subcategories | `Get-FilePlanPropertySubCategory` |
| Records Management | File plan: authorities | `Get-FilePlanPropertyAuthority` |
| Records Management | File plan: citations | `Get-FilePlanPropertyCitation` |
| Records Management | File plan: reference IDs | `Get-FilePlanPropertyReferenceId` |
| eDiscovery | Cases (Standard) | `Get-ComplianceCase` |
| eDiscovery | Cases (Premium) | `Get-ComplianceCase -CaseType AdvancedEdiscovery` |
| eDiscovery | Case holds | `Get-CaseHoldPolicy -Case <each case>` |
| eDiscovery | Content searches | `Get-ComplianceSearch` |
| eDiscovery | Compliance security filters | `Get-ComplianceSecurityFilter` |
| eDiscovery | eDiscovery administrators | `Get-eDiscoveryCaseAdmin` |
| Audit | Audit retention policies | `Get-UnifiedAuditLogRetentionPolicy` |
| Audit | Audit configuration | `Get-AdminAuditLogConfig` |
| Insider Risk Management | Insider risk policies | `Get-InsiderRiskPolicy` (not publicly documented) |
| Insider Risk Management | Insider risk cases | `Get-ComplianceCase -CaseType InsiderRisk` |
| Communication Compliance | Policies | `Get-SupervisoryReviewPolicyV2` |
| Communication Compliance | Rules | `Get-SupervisoryReviewRule` |
| Information Barriers | Policies | `Get-InformationBarrierPolicy` |
| Information Barriers | Segments | `Get-OrganizationSegment` |
| Information Barriers | Application status | `Get-InformationBarrierPoliciesApplicationStatus` |
| Alert Policies | Alert policies | `Get-ProtectionAlert` |
| Permissions | Role groups | `Get-RoleGroup` |

The list is defined in [`Catalog/PurviewCatalog.cs`](src/PurviewViewer/Catalog/PurviewCatalog.cs); adding a
collection is one entry (cmdlet, parameters, preferred columns and optional relations).

## How it works

The application hosts a Windows PowerShell 5.1 runspace in-process (on its own STA thread so the module's
sign-in UI works), imports ExchangeOnlineManagement and runs `Connect-IPPSSession` limited to the cmdlets above.
Results are converted to plain JSON-like values, which the UI, snapshots and CSV export all share. When the
application is started from PowerShell 7, PowerShell 7 module folders are removed from the process's
`PSModulePath`, so Windows PowerShell doesn't try to load PowerShell 7's copy of PackageManagement.

### Snapshot format

```json
{
  "format": "PurviewViewer.Snapshot",
  "formatVersion": 1,
  "createdUtc": "2026-10-12T18:00:00Z",
  "source": { "kind": "Live", "organization": "contoso.onmicrosoft.com", "account": "...", "tenantId": "..." },
  "collections": {
    "DataLossPrevention.Policies": {
      "command": "Get-DlpCompliancePolicy",
      "state": "Loaded",
      "retrievedUtc": "2026-10-12T17:58:41Z",
      "items": [ { "Name": "U.S. Financial Data", "Mode": "Enable", "...": "..." } ]
    }
  }
}
```

A snapshot contains your tenant's configuration (names, addresses, conditions). Store and share it accordingly.

## Building

You need Windows and the .NET SDK (8 or later; it builds the `net48` project without Visual Studio).

```powershell
.\build.ps1             # Release build + all tests
.\build.ps1 -Package    # also artifacts\PurviewViewer-<version>.zip
```

The version comes from the `VERSION` file. The tests include an integration test that loads
ExchangeOnlineManagement in the hosted runspace (when the module is installed), one that exercises the
Connect-IPPSSession error path without network access, and one that runs `/SelfTest`, which drives every tab,
collection, properties dialog, filter and snapshot round trip. CI installs the module so all of them run.

| Script | Purpose |
|---|---|
| `assets\make-assets.ps1` | Draws the icon and README logos from the Sarda Cloud logo |
| `tools\make-demo.ps1` | Regenerates the fictional demo snapshot (`pwsh`) |

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to add a collection and the rules of the house.

## Security

Please report vulnerabilities privately; see [SECURITY.md](SECURITY.md).

## License and trademarks

Purview Configuration Viewer is released under the [MIT License](LICENSE).

The Sarda Cloud name and logo are trademarks of Sarda Cloud Srl and are not licensed under the MIT License.
Microsoft, Microsoft Purview, Windows and PowerShell are trademarks of the Microsoft group of companies.
Purview Configuration Viewer is an independent community tool and is not affiliated with or endorsed by
Microsoft. The demo data describes a fictional company; any resemblance to a real organization is coincidental.

---

A Sarda Cloud Community project. Copyright (C) 2026 Francesco Valerio Buccoli.
