# 0111 — TV integrada: comandos, automação e retomada

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

KEY_POWER é um toggle; conectividade de rede não confirma a tela ligada.

## Decisão

Manter confirmação explícita de tela, fila serializada e nunca repetir um toggle entregue ou possivelmente entregue. Expor comandos em Energia e opções em Configurações. Automação de suspensão, retomada, topologia, HDR e PS é optativa. Um único leitor HID registra o PS; não há tarefa nem segunda bandeja. Ver guia da TV para limites e preparação local.

Ver [guia da TV](../guias/tv-samsung-integrada.md).

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).
