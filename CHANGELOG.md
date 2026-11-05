# Changelog

All notable changes to XGB_P are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/). Each entry links the decision record (ADR, in Portuguese) that explains it.

## [Unreleased]

### Added

- **Hide the taskbar in the Xbox mode.** Taskbars that stay registered as an app bar while the Xbox mode hides them (for example Start11's) left desktop apps short of the full screen. On a confirmed Xbox mode the bar turns on the Windows taskbar auto-hide through the public app bar API and turns it off again on the desktop. It only undoes what it did: an auto-hide you already had stays, and turning it off yourself during the Xbox mode wins. A local marker undoes the change after a crash. Settings → MODO XBOX → *Ocultar a barra de tarefas*, on by default ([ADR 0091](docs/decisoes/0091-barra-de-tarefas-oculta-no-modo-xbox.md)).
- **Lower the focused app while the bar is open.** Opening the bar halves the mixer volume of the game or app in front (all processes of a game, plus sessions of the same executable) with a short ramp, and closing it restores the level. A change made in the mixer while the bar is open is kept. Windows remembers per-app volume, so a journal restores an app that closed, or a bar that crashed, while the level was lowered. Settings → *Abaixar o som do app ao abrir*, on by default ([ADR 0092](docs/decisoes/0092-volume-do-app-em-foco-ao-abrir.md)).
- **Bluetooth headset battery.** Headsets that report their level to Windows over Hands-Free show a headset icon and battery in the status strip and the level in Connections → Bluetooth devices. The value is read from the device property Windows keeps; nothing is sent to the headset. Only connected headsets are shown, because Windows keeps the last level after a disconnect ([ADR 0094](docs/decisoes/0094-bateria-de-fones-bluetooth.md)).
- **Status strip over the Big Picture.** The Big Picture already shows its own clock in the same corner as the bar's status strip. When the bar opens with any Big Picture screen in front, it hides its own clock and moves network and battery next to the panel title. Over a game or a desktop app, the strip stays in the corner with the clock ([ADR 0093](docs/decisoes/0093-status-ao-lado-do-painel-nas-telas-steam.md)).

### Fixed

- **Back to Home after a slow exit.** A game that took a couple of seconds to quit stayed listed without a path or creation time while it was exiting. The return to the Big Picture Home took that copy for another open game and cancelled. The exiting copy of the same game no longer counts as another game, for the return, the exit confirmation, an exit from the game's own menu and *Fechar jogo e iniciar* ([ADR 0090](docs/decisoes/0090-jogo-em-encerramento-nao-e-outro-jogo.md)).
- **Games started by a launcher.** An external Steam shortcut whose target starts the game as a child process (such as a frame rate unlocker) adopts the unclaimed children of the target, so the game's window, focus, pause and close work without a manual mapping ([ADR 0089](docs/decisoes/0089-jogo-filho-do-lancador-de-atalho.md)).

## [2.1.0] — 2026-11-04

First public release. The application behaves exactly like 2.0.2; this version changes how it is built, verified and shipped.

### Changed

- **The shipped executable carries no test code.** `XGB_P.exe` is compiled from `src/` only and rejects every `--verify*` and `--inspect-motion` argument with exit code 2. The verification suites build into a separate `XGB_P.Tests.exe` from the same sources plus `tests/` and the `XGB_VERIFY` symbol. The executable went from 1,288 KB to 718 KB.
- **Repository layout.** The application moved from `tools/design-preview` to `src/app`, the libraries from `lib` to `src/lib`, and the verification suites to `tests/`.
- **Quality policy v2.** Line width is capped at 160 characters for new code, with the existing long lines recorded per file as a ratchet that can only go down. File length caps were tightened to the current sizes. The static checks are shared by the local gate and CI and also reject tracked binaries, certificates, sound files and Microsoft package identities.
- **Release package.** The ZIP now holds only `XGB_P.exe`, `LEIA-ME.md`, `LICENSE.txt` and `NOTICE.txt`, with a `SHA256SUMS.txt` beside it. Verification reports stay with the local candidate.

### Added

- GitHub Actions: CI on Windows (static checks, both builds, fixture suites, product refusal of test modes), Gitleaks over the full history, CodeQL for C# and workflows, and a tag-driven release with build provenance attestation. All actions are pinned by commit SHA and kept current by Dependabot.
- Apache-2.0 license, NOTICE with third-party products and trademarks, security policy, contributing guide, code of conduct, issue and pull request templates.
- English and Portuguese READMEs, installation and development guides, and a consolidated architecture, requirements and roadmap.

### Removed

- From the public tree: the local activation bridge and its install scripts, reverse-engineering notes and tools, the script that downloaded interface sounds, raw evidence and session logs. They remain in the author's private archive ([ADR 0002](docs/decisoes/0002-identidade-do-pacote-da-game-bar.md)).
- `scripts/validar-design.ps1` and the `XgbDesignPreview.exe` build name. Use `scripts/compilar-xgb.ps1`.

## [2.0.2] — 2026-11-01

### Fixed

- The virtual keyboard opened by itself on the desktop. Automatic opening now reads the Xbox mode at that moment and fails closed, waits 3 s after entering the Xbox mode, and ignores a field that already had focus on the desktop ([ADR 0087](docs/decisoes/0087-teclado-automatico-so-com-modo-xbox-estavel.md)).

## [2.0.0] — 2026-10-29

### Added

- **Recording panel:** Radeon ReLive instant replay, screenshot, recording, GIF and in-game replay through AMD's command tool, the Windows microphone, and the latest captures with thumbnails ([ADR 0081](docs/decisoes/0081-aba-amd-captura-pelo-relive.md)).
- **AMD panel:** global graphics settings through ADLX, re-read after every change ([ADR 0082](docs/decisoes/0082-painel-amd-ajustes-pelo-adlx.md)).
- **Activity history** in Settings, seven days or 300 entries; Home shows only errors, warnings and long waits ([ADR 0078](docs/decisoes/0078-historico-de-acoes-e-inicio-so-critico.md)).
- **Xbox emulation in DSX:** the virtual Xbox 360 Guide counts as PS, and a button icon option (Automatic, PlayStation, Xbox) ([ADR 0080](docs/decisoes/0080-emulacao-xbox-guia-xinput-e-icones.md)).

### Changed

- DSX is restored from the tray instead of starting a second instance, which could close the running one ([ADR 0079](docs/decisoes/0079-dsx-pela-bandeja-sem-segunda-instancia.md)).
- Recording icons, rail order Sound → Recording → Performance → AMD, and Windows-style toggles with confirmed state ([ADR 0083](docs/decisoes/0083-icones-gravacao-ordem-do-trilho-e-interruptores.md)).
- Photos opened from the Recording panel in the Xbox mode are maximized ([ADR 0084](docs/decisoes/0084-miniaturas-e-fotos-no-modo-xbox.md)).

### Fixed

- Capture thumbnails were upside down ([ADR 0084](docs/decisoes/0084-miniaturas-e-fotos-no-modo-xbox.md)).
- Confirming on a mixer slider muted the app; mute is now only on its own button ([ADR 0085](docs/decisoes/0085-slider-do-mixer-sem-mute.md)).

## [1.0.8] — 2026-10-29

- Steam focus confirmation separated from profile loading; the PS return waits for the last button release; animated exit restored ([ADR 0076](docs/decisoes/0076-versao-1.0.8-confirmacao-steam-e-abertura.md), [ADR 0077](docs/decisoes/0077-fechamento-ps-e-final-da-sprint-1.0.8.md)).

## [1.0.7] — 2026-10-29

- Faster Steam Input hand-off after the PS release, without lowering the confirmation bar ([ADR 0074](docs/decisoes/0074-transferencia-rapida-do-steam-input-apos-soltar-ps.md)).

## [1.0.6] — 2026-10-28

- A new game waits for Steam to end the previous game's session, not just for its process to exit ([ADR 0072](docs/decisoes/0072-aguardar-encerramento-da-sessao-steam.md)).

## [1.0.5] — 2026-10-28

- Starting a game with another one open asks *Close game and start* or *Cancel*; closing the last game returns to the Big Picture Home ([ADR 0070](docs/decisoes/0070-fechar-antes-de-iniciar-e-retorno-ao-inicio.md)).

## [1.0.4] — 2026-10-28

- The second game opens after Big Picture is brought forward, and focus goes to a game only after the button is released ([ADR 0068](docs/decisoes/0068-abertura-pelo-big-picture-e-foco-apos-soltar.md)).

## [1.0.3] — 2026-10-28

- PS is protected over every Steam-managed game, including non-Steam shortcuts ([ADR 0066](docs/decisoes/0066-ps-protegido-em-jogos-geridos-pela-steam.md)).

## [1.0.2] — 2026-10-27

- Every open game in *Back to game*, current first; recent games exclude open ones; transitions between games wait for Steam and a stable window ([ADR 0063](docs/decisoes/0063-transicao-e-retorno-entre-jogos.md), [ADR 0064](docs/decisoes/0064-jogos-abertos-no-grupo-voltar-ao-jogo.md)).

## [1.0.1] — 2026-10-27

- First versioned build: placeholder UI removed from the normal flow, real power actions behind confirmation, persisted reduced motion ([ADR 0062](docs/decisoes/0062-preparacao-da-versao-1.0.1.md)).

## Before 1.0.1

Design preview and feature development, 2026-10-23 to 2026-10-27: ADRs [0001](docs/decisoes/0001-nao-modificar-binarios-microsoft.md) to [0061](docs/decisoes/0061-som-continuo-na-navegacao-do-trilho.md).

[Unreleased]: https://github.com/Sitr3n01/XGB_P/compare/v2.1.0...HEAD
[2.1.0]: https://github.com/Sitr3n01/XGB_P/releases/tag/v2.1.0
