# 0112 — Trilho lateral como opção e ordem das Configurações

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

Escolher o layout não deve exigir trocar de executável.

## Decisão

Salvar RailLayout em appearance.json e aplicar na hora. Sem escolha gravada, o marcador define a migração; sem ambos, usar trilho. Ordem: Conexões, Tela, Controle, Teclado virtual, Interface, Abrir a barra, Modo Xbox, Inicialização, Sistema, Sobre. Sobre lê a versão do assembly.

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).
