# Third-party notices

Purview Configuration Viewer is Copyright (C) 2026 Francesco Valerio Buccoli, a Sarda Cloud Community project,
and is licensed under the MIT License; see `LICENSE`.

## What the release contains

The released `PurviewViewer.exe` contains only this project's code and resources. It redistributes no
third-party code.

At run time it uses components that are part of Windows or that you install yourself, under their own terms:

- **.NET Framework 4.8** and **Windows PowerShell 5.1**, part of Windows.
- The **ExchangeOnlineManagement** PowerShell module, which you install from the PowerShell Gallery and which is
  licensed by Microsoft under its own terms. It isn't redistributed with this application.
- Windows **stock shell icons**, loaded from Windows at run time with `SHGetStockIconInfo`.

## Build-time only

These packages are used to build or test the application and are not part of the release:

| Package | License |
| --- | --- |
| Microsoft.NETFramework.ReferenceAssemblies | MIT |
| Microsoft.PowerShell.5.1.ReferenceAssemblies | MIT |
| Microsoft.NET.Test.Sdk | MIT |
| xunit, xunit.runner.visualstudio | Apache-2.0 |

## Trademarks

The application icon and the README logos are drawn from the Sarda Cloud logo. The Sarda Cloud name and logo
are trademarks of Sarda Cloud Srl and are not licensed under the MIT License.
