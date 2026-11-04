# 0005 — Ativo só no modo Xbox; no desktop quem responde é a Steam

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

> **Revisão pela ADR 0037 (2026-10-25):** PS e Win+G passam a abrir/fechar o XGB_P apenas com FSE confirmado; desktop/API incerta ficam inativos. O gesto de microfone foi substituído. O texto original abaixo permanece histórico.

O usuário quer a Game Bar própria só no modo Xbox. No desktop, mesmo com o Big Picture aberto, a Steam continua com o overlay dela.

## Decisão

- O XGB_P consulta `IsGamingFullScreenExperienceActive()` e assina `RegisterGamingFullScreenExperienceChangeNotification()`.
- **Modo Xbox:** gestos e comandos abrem o XGB_P.
- **Modo desktop:** o XGB_P fica inativo para gestos. A política para os `ms-gamebar://` que ainda chegarem (Win+G, capturas) está em aberto.

## Consequências

- O Big Picture aberto **não** é critério; só o modo do Windows conta.
- No modo Xbox, a Steam e o XGB_P convivem: o PS abre a Steam, e o gesto abre o XGB_P.

## Evidências

API documentada em <https://devdocs.xbox.com/build/gdk-and-engines/handheld/handheld-launchers>; uso equivalente no AnyFSE (`GamingExperience.cpp`).
