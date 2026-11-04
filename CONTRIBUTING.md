# Contributing

XGB_P is a personal project built for one specific setup (Windows 11 Xbox full screen experience, Steam Big Picture, a DualSense through DSX). Issues and pull requests are welcome, but changes are judged against that setup first.

The codebase, comments and documentation are in Brazilian Portuguese; identifiers are in English. Issues and pull requests can be written in English or Portuguese.

## Before you start

1. Read [AGENTS.md](AGENTS.md): it holds the non-negotiable rules (read-only DualSense, no injection, no Microsoft binaries touched, system changes made by the user).
2. For a behavior change, check the [decision records](docs/decisoes/README.md). A change that contradicts an accepted ADR needs a new ADR that supersedes it.

## Toolchain

No SDK or NuGet: the build uses the C# 5 compiler that ships with the .NET Framework 4.x (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) and Windows PowerShell 5.1. Details in the [development guide](docs/guias/desenvolvimento.md).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\compilar-xgb.ps1 -Verificar
```

## Pull requests

- One logical change per pull request, with the reason in the description.
- New behavior comes with a verification in `tests/` (or a method under `#if XGB_VERIFY` when it must live next to private state). Verifications use fixtures and substitutes; they never touch real games, power, radios or user settings.
- `scripts/verificar-ci.ps1` must pass. If you have the hardware, also run `scripts/verificar-qualidade.ps1` and say so.
- The quality policy is a ratchet: `legacyLineCaps` and `legacyLongLines` in `quality/policy.json` may only go down.
- PowerShell scripts are ASCII-only (Windows PowerShell 5.1 reads BOM-less files as ANSI).
- Update `CHANGELOG.md` under `Unreleased`.

## Never commit

- binaries, archives, certificates or signing keys;
- sound files or any other Microsoft, Valve, Sony or AMD asset;
- logs, screenshots of a real desktop, personal paths, account names or window titles;
- anything that impersonates a Microsoft package identity.

CI enforces most of this, and Gitleaks scans the full history.
