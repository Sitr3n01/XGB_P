# 0003 — Instalação da ponte local (registro público resumido)

- **Status:** Aceita; detalhes mantidos fora do repositório público
- **Data:** 2026-10-23

## Contexto e decisão

Esta decisão trata de como a ponte local da [ADR 0002](0002-identidade-do-pacote-da-game-bar.md) é instalada no PC do desenvolvedor. Como a ponte não é distribuída, o procedimento, os scripts e os certificados ficam no arquivo privado.

## Consequências

- Nenhum pacote, certificado ou script de instalação da ponte é versionado aqui. A verificação estática do repositório recusa binários, certificados e manifestos com identidade de publicador da Microsoft.
- O aplicativo publicado (`XGB_P.exe`) não depende desta decisão para funcionar: ver as consequências da ADR 0002.
