# Contributing

Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community. Licensed under the MIT License.

Thank you for helping. Issues and pull requests are welcome.

## Reporting a problem

Open an issue with the application version (Help > About), the ExchangeOnlineManagement version, the Windows
version, and what looks wrong. If you attach a snapshot, a CSV or a screenshot, **remove tenant names, people,
addresses and policy details first**; the demo data (File > Open Demo Data) is often enough to show a UI problem.
For a security problem, follow [SECURITY.md](SECURITY.md) instead.

## Building and testing

You need Windows and the .NET SDK 8 or later; for the setup, also Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

```powershell
.\build.ps1             # build Release and run the tests
.\build.ps1 -Package    # also artifacts\PurviewExplorer-<version>.zip
.\build.ps1 -Setup      # also artifacts\PurviewExplorer-<version>-setup.exe
.\installer\test-setup.ps1 -Setup .\artifacts\PurviewExplorer-<version>-setup.exe
```

The integration tests load ExchangeOnlineManagement in the hosted runspace when it's installed
(`Install-Module ExchangeOnlineManagement -Scope CurrentUser`); without it they only check that its absence is
reported. The UI self-test (`PurviewExplorer.exe /SelfTest:log.txt`) drives every tab and collection with the demo
data. `installer\test-setup.ps1` installs the setup silently for the current user into a temporary folder, checks
the result and uninstalls it; it refuses to run if Purview Explorer is already installed for you.

When you change the setup's pages, go through every page yourself at your display scaling; Inno Setup's Pascal
Script reports some mistakes only at run time.

## Adding a collection

1. Add an entry to the solution in `src/PurviewExplorer/Catalog/PurviewCatalog.cs`: cmdlet, parameters, preferred
   columns, and optionally a sort property, a parent property (for hierarchies) or relations to other collections.
2. Add representative, fictional items to `tools/make-demo.ps1`, run `pwsh -File tools\make-demo.ps1`, and commit
   the regenerated `src/PurviewExplorer/Resources/demo-snapshot.json`. A test checks that the demo data covers every
   collection.
3. Add the row to the table in the README and an entry to the CHANGELOG.

## Rules of the house

- **Read-only.** Only `Get-*` cmdlets, never anything that changes the tenant, and never `Invoke-Expression` or
  scripts built from data.
- **Documented cmdlets.** Prefer cmdlets documented on Microsoft Learn. An undocumented one is marked with
  `Undocumented = true` in the catalog and noted in the README.
- **No secrets, no tenant data in the repository.** Demo data is fictional (Contoso and friends); never commit a
  real snapshot.
- **Classic and dependency-free.** The application is a single .NET Framework 4.8 executable with no NuGet
  runtime dependencies; native controls, system fonts and Windows stock icons.
- Each source file starts with the copyright and licence lines used throughout the repository. The build treats
  warnings as errors.

By contributing you agree that your contribution is licensed under the MIT License.
