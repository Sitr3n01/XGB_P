# 0108 — SVG no indicador de bateria do DualSense

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

Usar o vetor aprovado no indicador do controle.

## Decisão

Incorporar o SVG Controllercons 2.1 como recurso, com geometria nonzero e leitura XML sem DTD nem resolução externa. Área de 30 × 20 DIP, proporção preservada e 5 DIP até a bateria. A licença OFL-1.1 e o crédito acompanham fontes e pacote; a ADR 0113 acrescenta o traço aprovado.

Origem: [Controllercons](https://controllercons.github.io/); [licença](../../licenses/Controllercons-OFL-1.1.txt).

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).
