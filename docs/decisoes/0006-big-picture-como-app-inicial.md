# 0006 — Big Picture como app inicial (AnyFSE); Início/Biblioteca vão para `steam://`

- **Status:** Aceita
- **Data:** 2026-10-23

> **Revisão parcial pela ADR 0010:** Início passa a abrir a Home própria. A ação explícita Big Picture dentro dela continua usando `steam://open/bigpicture`. O restante desta decisão permanece válido.

> **Rota da Biblioteca revisada pela ADR 0054 (2026-10-26):** no FSE, usar `steam://open/library/collection/all-games`; a rota anterior `open/bigpicture/games` volta à Home no cliente atual.

## Contexto

Na Game Bar oficial, Início e Biblioteca abrem `windows.gaming:///home|library`, e o shell entrega isso ao app inicial. Hoje o app inicial é o app Xbox (`GamingHomeApp` não está definido).
- Na edição PC limitada do modo Xbox não existe o seletor de app inicial.
- O AnyFSE se registra como app inicial (`windows.gamingApp` + `Microsoft.appCategory.gamingHome_8wekyb3d8bbwe`) e abre o launcher escolhido, mas trata toda URI como "abrir o launcher".

## Decisão

- O app inicial é o **AnyFSE configurado para o Steam Big Picture** (externo ao projeto).
- Na UI do XGB_P, Início chama `steam://open/bigpicture` e Biblioteca chama `steam://open/bigpicture/games` **direto**, sem passar pelo `windows.gaming:`.

## Consequências

- **Pré-requisito:** reinstalar o AnyFSE e, se o seletor não aparecer, o XboxFullScreenExperienceTool.
- **Futuro opcional:** um app inicial próprio, para tratar `/library` e evitar que o app Xbox roube o foco (relatado no AnyFSE #104).

## Evidências

uris.md (rotas `steam://` confirmadas no `steamui/sp.js`); AnyFSE: `AppxManifest.xml`, `CmdLine.cpp`, `Constants.hpp`.
