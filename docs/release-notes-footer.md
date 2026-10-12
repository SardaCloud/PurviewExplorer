## Download

| File | What it is |
| --- | --- |
| `PurviewExplorer-VERSION-setup.exe` | **Recommended.** Setup checks the requirements, can install the ExchangeOnlineManagement module, installs Purview Explorer and checks that it can load the module |
| `PurviewExplorer-VERSION.zip` | `PurviewExplorer.exe` and its `.config`, the README, LICENSE and third-party notices: unzip and run |

Purview Explorer needs Windows 10 version 1607 or later, Windows 11, or Windows Server 2016 or later, and the
ExchangeOnlineManagement PowerShell module 3.2.0 or later. The setup installs the module for you; with the zip,
install it yourself:

```powershell
Install-Module ExchangeOnlineManagement -Scope CurrentUser
```

Try it without signing in with **File > Open Demo Data**.

Neither file is code-signed, so Windows SmartScreen may warn the first time you run it.

### Verifying a download

```powershell
Get-FileHash .\PurviewExplorer-VERSION-setup.exe, .\PurviewExplorer-VERSION.zip -Algorithm SHA256
```

Compare the hashes with `SHA256SUMS.txt`.

---

A Sarda Cloud Community project. Copyright (C) 2026 Francesco Valerio Buccoli.
Licensed under the [MIT License](https://github.com/SardaCloud/PurviewExplorer/blob/main/LICENSE).
