# 0118 — Versão 3.1.0

- **Status:** Aceita; candidata pública 3.1.0, sem release publicada.
- **Data:** 2026-11-25

## Contexto

Depois da 3.0.0, três mudanças entraram: jogos do Xbox e do Game Pass no catálogo ([ADR 0117](0117-jogos-do-xbox-no-catalogo.md)), entrega do foco de entrada ao jogo ([ADR 0115](0115-entrega-do-foco-de-entrada-ao-jogo.md)) e mouse nos sliders ([ADR 0116](0116-mouse-nos-sliders.md)), além do registro da ausência do DualSense uma vez só no log.

## Decisão

- **Versão 3.1.0:** produto `3.1.0` e arquivo `3.1.0.0` em `AssemblyInfo.cs`, no manifesto, na política de qualidade e nos contratos de `ProductReleaseVerification`. Recursos novos e compatíveis pedem a versão menor.
- O código foi adaptado à estrutura pública: verificações em `tests/`, linhas novas de até 160 colunas e os tetos por arquivo sem ampliação.

## Consequências

- `verificar-ci.ps1` aprovado antes do envio, com as suítes novas (24 do Xbox, 33 dos sliders e 9 do foco) e o produto recusando os modos de teste.
- Nenhuma release ou tag criada. A matriz física continua pendente.

## Evidências

Execução local do `verificar-ci.ps1` e a CI do GitHub no pull request.
