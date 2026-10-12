## Download

Pick the build for the PC you'll run it on: **x64** for Intel and AMD processors, **arm64** for Windows 11 on Arm.

| File | What it is |
| --- | --- |
| `PurviewExplorer-VERSION-win-x64-setup.exe` | **Recommended for x64.** Setup checks the requirements, can install the ExchangeOnlineManagement module, installs Purview Explorer and checks that it can load the module |
| `PurviewExplorer-VERSION-win-arm64-setup.exe` | **Recommended for Arm64.** The same setup, for Windows 11 on Arm |
| `PurviewExplorer-VERSION-win-x64.zip` | The x64 `PurviewExplorer.exe` with its `.config`, the README, LICENSE and third-party notices: unzip and run |
| `PurviewExplorer-VERSION-win-arm64.zip` | The same for Arm64 |

The x64 build needs Windows 10 version 1607 or later, Windows 11, or Windows Server 2016 or later, with .NET
Framework 4.8. The Arm64 build needs Windows 11 on Arm with .NET Framework 4.8.1. Both need the
ExchangeOnlineManagement PowerShell module 3.2.0 or later: the setups install it for you; with a zip, install it
yourself:

```powershell
Install-Module ExchangeOnlineManagement -Scope CurrentUser
```

Try it without signing in with **File > Open Demo Data**.

The files aren't code-signed, so Windows SmartScreen may warn the first time you run them.

### Verifying a download

```powershell
Get-FileHash .\PurviewExplorer-VERSION-* -Algorithm SHA256
```

Compare the hashes with `SHA256SUMS.txt`.

---

A Sarda Cloud Community project. Copyright (C) 2026 Francesco Valerio Buccoli.
Licensed under the [MIT License](https://github.com/SardaCloud/PurviewExplorer/blob/main/LICENSE).
