## Download

| Package | Contents |
| --- | --- |
| `PurviewViewer-VERSION.zip` | `PurviewViewer.exe` and its `.config`, the README, LICENSE and third-party notices |

Unzip it anywhere and run `PurviewViewer.exe`. It needs Windows 10 or 11 (or Windows Server 2016 or later) and
the ExchangeOnlineManagement PowerShell module, 3.2.0 or later:

```powershell
Install-Module ExchangeOnlineManagement -Scope CurrentUser
```

Try it without signing in with **File > Open Demo Data**.

The executable isn't code-signed, so Windows SmartScreen may warn the first time you run it.

### Verifying a download

```powershell
(Get-FileHash .\PurviewViewer-VERSION.zip -Algorithm SHA256).Hash.ToLower()
```

Compare it with `SHA256SUMS.txt`.

---

A Sarda Cloud Community project. Copyright (C) 2026 Francesco Valerio Buccoli.
Licensed under the [MIT License](https://github.com/SardaCloud/PurviewViewer/blob/main/LICENSE).
