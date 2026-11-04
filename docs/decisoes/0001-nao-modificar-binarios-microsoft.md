# 0001 — Não modificar binários da Microsoft

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

Os problemas da Game Bar no modo Xbox poderiam ser atacados mexendo direto no `GameBar.exe`, com patch nas URIs, ou injetando código nele em tempo de execução (Frida/DLL).

## Decisão

O XGB_P **não altera nem injeta código** em binários da Microsoft (`GameBar.exe`, `twinui.pcshell.dll`, app Xbox). A integração usa só mecanismos do próprio Windows: identidade de pacote, protocolos, APIs públicas e leitura de HID.

## Consequências

- O pacote oficial é assinado pela Store. Um patch quebraria a assinatura e seria desfeito na próxima atualização.
- Injeção num processo AppContainer é frágil, quebra a cada atualização e pode chamar a atenção de anti-cheats.
- Em troca, abrimos mão de usar recursos internos da Game Bar oficial (captura, widgets, z-band privilegiada).

## Evidências

relatorio-completo.md, seção "Caminhos de correção" (rotas C e D descartadas).
