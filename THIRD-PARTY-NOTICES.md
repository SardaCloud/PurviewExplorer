# Third-party notices

Purview Explorer is Copyright (C) 2026 Francesco Valerio Buccoli, a Sarda Cloud Community project, and is
licensed under the MIT License; see `LICENSE`.

## What the release contains

The released `PurviewExplorer.exe` contains only this project's code and resources. It redistributes no
third-party code.

The setup, `PurviewExplorer-<version>-setup.exe`, is built with **Inno Setup** and contains its setup and
uninstall program:

    Inno Setup
    Copyright (C) 1997-2026 Jordan Russell. All rights reserved.
    Portions Copyright (C) 2000-2026 Martijn Laan. All rights reserved.
    https://jrsoftware.org/

    This software is provided "as-is," without any express or implied warranty. In no event shall
    the author be held liable for any damages arising from the use of this software.

    Permission is granted to anyone to use this software for any purpose, including commercial
    applications, and to alter and redistribute it, provided that the following conditions are met:

    1. All redistributions of source code files must retain all copyright notices that are currently
       in place, and this list of conditions without modification.

    2. All redistributions in binary form must retain all occurrences of the above copyright notice
       and web site addresses that are currently in place (for example, in the About boxes).

    3. The origin of this software must not be misrepresented; you must not claim that you wrote
       the original software. If you use this software to distribute a product, an acknowledgment
       in the product documentation would be appreciated but is not required.

    4. Modified versions in source or binary form must be plainly marked as such, and must not
       be misrepresented as being the original software.

At run time Purview Explorer uses components that are part of Windows or that you install, under their own terms:

- **.NET Framework 4.8** and **Windows PowerShell 5.1**, part of Windows.
- The **ExchangeOnlineManagement** PowerShell module, licensed by Microsoft under its own terms, which the setup
  can download from the PowerShell Gallery. It isn't redistributed with Purview Explorer.
- Windows **stock shell icons**, loaded from Windows at run time with `SHGetStockIconInfo`.

## Build-time only

These packages and tools are used to build or test Purview Explorer and are not part of the release:

| Package or tool | License |
| --- | --- |
| Microsoft.NETFramework.ReferenceAssemblies | MIT |
| Microsoft.PowerShell.5.1.ReferenceAssemblies | MIT |
| Microsoft.NET.Test.Sdk | MIT |
| xunit, xunit.runner.visualstudio | Apache-2.0 |
| Inno Setup compiler | Inno Setup License (above) |

## Trademarks

The application icon, the setup images and the README logos are drawn from the Sarda Cloud logo. The Sarda Cloud
name and logo are trademarks of Sarda Cloud Srl and are not licensed under the MIT License.
