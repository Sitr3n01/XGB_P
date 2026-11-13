# 0101 — Teclado sobre jogos: compor e enviar

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

Um teclado sem foco deixa o jogo receber os mesmos comandos do controle.

## Decisão

Ao abrir manualmente sobre um jogo catalogado, tomar o foco e compor o texto localmente. Menu envia depois da soltura, devolve o foco, espera 150 ms e envia texto e Enter; Círculo cancela. Apps e abertura automática preservam o comportamento sem foco. Steam Input reconhece o jogo pela identidade.

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).
