# 0106 — Bateria de fones pelo G HUB

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

Alguns fones LIGHTSPEED informam a bateria ao agente local do G HUB.

## Decisão

Consultar somente GET de /devices/list e /battery/<id>/state em ws://127.0.0.1:9010. Sem SET, SUBSCRIBE ou comando ao fone. Priorizar G HUB sobre Bluetooth; exibir nível, carga e autonomia quando fornecidos. Falha de uma fonte não apaga a outra.

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).
