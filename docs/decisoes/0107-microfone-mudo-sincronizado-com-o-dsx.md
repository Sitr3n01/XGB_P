# 0107 — Microfone mudo sincronizado com o DSX

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

A barra deve acompanhar o microfone padrão que o DSX já silencia.

## Decisão

Observar avisos do Core Audio sem SetMute no observador. Mostrar o microfone riscado em vermelho só no mudo, atualizar Som e Gravação, e avisar gravação sem voz. O botão MUTE não abre o ditado no teclado; a tecla de ditado continua disponível na tela.

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).
