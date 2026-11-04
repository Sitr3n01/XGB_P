# Security policy

## Supported code

Security fixes target the default branch and the latest release of `XGB_P.exe`, the libraries under `src/lib`, and the build and verification scripts. The private activation bridge described in [ADR 0002](docs/decisoes/0002-identidade-do-pacote-da-game-bar.md) is not part of this repository and is not supported.

## Reporting a vulnerability

Report it privately through GitHub's private vulnerability reporting: open the repository's **Security** tab and choose **Report a vulnerability**, or go straight to <https://github.com/Sitr3n01/XGB_P/security/advisories/new>. Only you and the maintainer see the report until an advisory is published.

Do not open a public issue with exploit details. Include:

- the affected version or commit;
- reproduction steps, using test data;
- the impact and the privileges required (standard user, administrator);
- sanitized log lines from `logs\xgb.log`, without personal paths, window titles or account names.

## What the application may and may not do

These rules are design invariants, enforced by code review and, where possible, by the quality gate:

| Invariant | How it is enforced |
|---|---|
| The DualSense is read-only | No output report (LED, rumble, triggers) is ever sent. The gate rejects `HidD_SetOutputReport` and `HidD_SetFeature` in any source file |
| No code injection into other processes | The gate rejects `WriteProcessMemory`, `CreateRemoteThread` and `VirtualAllocEx` |
| No Microsoft binary is modified | Only public, documented APIs are called ([ADR 0001](docs/decisoes/0001-nao-modificar-binarios-microsoft.md)) |
| System-wide changes are made by the user | The app never installs packages or certificates, never configures HidHide and never creates services or scheduled tasks. Its only autostart mechanism is the optional per-user `Run` entry the user turns on in Settings. Windows settings shown in the panels (radios, Night light, audio, power mode) change only when the user acts on them |
| Elevation only through UAC | "Always run as administrator" relaunches through the standard UAC prompt, confirmed by the user ([ADR 0033](docs/decisoes/0033-inicializacao-e-execucao-como-administrador.md)) |
| Power actions need two confirmations | Suspend, restart and shut down require a second, explicit confirmation whose initial focus is **Cancel**. The automated tests replace these effects and never run them |
| The shipped executable has no test surface | `XGB_P.exe` is compiled without the verification code and rejects every `--verify*` argument. Verification runs in the separate `XGB_P.Tests.exe` |
| Nothing private goes into Git | CI rejects tracked binaries, certificates, sound files and Microsoft package identities, and Gitleaks scans the full history |

## Release integrity

Releases are built by `.github/workflows/release.yml` on a GitHub-hosted runner. Each release carries `SHA256SUMS.txt` and a build provenance attestation, verifiable with:

```powershell
gh attestation verify .\XGB_P-<version>-win-x64.zip --repo Sitr3n01/XGB_P
```

The executable is not Authenticode-signed, so Windows SmartScreen may warn on first launch. Verify the hash before running it.
