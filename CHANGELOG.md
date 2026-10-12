# Changelog

Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community. Licensed under the MIT License.

All notable changes to Purview Configuration Viewer.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-10-12

First public preview: Microsoft Purview configuration in a classic Windows application, read-only.

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

[0.1.0]: https://github.com/SardaCloud/PurviewViewer/releases/tag/v0.1.0
