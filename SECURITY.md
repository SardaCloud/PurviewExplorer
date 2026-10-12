# Security

Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community. Licensed under the MIT License.

## Reporting a vulnerability

Report privately, not in a public issue. Use GitHub's **Report a vulnerability** button under the
[Security tab](https://github.com/SardaCloud/PurviewViewer/security).

Please say what an attacker gains, how to reproduce it, and which version or commit you used. You will get an
acknowledgement within five working days.

## What Purview Configuration Viewer promises

It is a read-only viewer. It promises the following, and a way to break any of them is in scope:

1. **It changes nothing in the tenant.** Only `Get-*` cmdlets are run; the session layer refuses anything else.
2. **It stores no secret.** No password, token, certificate or private key is written to the settings file,
   a snapshot, a CSV export or a log. Sign-in and tokens are handled by the ExchangeOnlineManagement module, in
   memory, and the session is disconnected when the application closes.
3. **Opening a file is safe.** A snapshot is data: opening a crafted snapshot must not run code, start a
   process, write files or contact a server.

Also in scope: a crafted snapshot, or cmdlet output, that makes the application crash, hang or misreport in a
way that could mislead an administrator reviewing a configuration.

## Known and accepted

**Snapshots and exports describe the tenant.** Policy names, conditions, people and group addresses are what
they are for. Review a snapshot before sharing it.

**The module runs with your permissions.** The viewer can read whatever your account, or the app registration
used for certificate sign-in, can read through Security & Compliance PowerShell.

**The executable isn't code-signed.** Verify a download against `SHA256SUMS.txt` in the release, or build it
from source.

## Supported versions

| Version | Supported |
| --- | --- |
| 0.1.x | yes |
