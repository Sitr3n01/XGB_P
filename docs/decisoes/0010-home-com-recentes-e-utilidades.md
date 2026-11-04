# 0010 — Home com recentes e utilidades

- **Status:** Aceita
- **Data:** 2026-10-23

> **Evolução pela ADR 0011:** o acesso ao Big Picture foi movido para acima do jogo ativo. Início do Big Picture e Biblioteca passam a navegar de verdade na Steam na prévia; recentes e demais ações externas continuam simulados.

> **Evoluções posteriores:** Downloads/Configurações também foram ativados na ampliação da 0011; Som real pelas 0012/0013; apps abertos, utilidades apenas por ícone e faixa refinada pela 0014. A estrutura de seis utilidades abaixo descreve a proposta inicial; atualmente há Big Picture no topo e cinco ícones no acesso rápido. Ver continuidade para o estado final da sessão.

## Contexto

O usuário pediu que a tela Home exibisse jogos recentes e botões de utilidade para jogar na TV, solicitando recomendações e a criação imediata da tela.

Anteriormente, as ADRs 0006/0007 definiam Início como uma ação direta para o Big Picture. Esse comportamento precisa evoluir para uma Home própria, preservando o acesso à Steam.

## Decisão

Início passa a ser um **painel próprio** com ação de retomar o jogo, três recentes e seis utilidades propostas: Big Picture, Biblioteca, Downloads, Áudio, Desempenho e Energia. Big Picture permanece como ação explícita na Home. Biblioteca mantém também seu atalho direto no trilho.

Na prévia de design, iniciar na Home e dar foco à ação principal. Representar sessões e recentes com exemplos identificados; não ler histórico nem iniciar jogos nesta etapa. Os atalhos para Áudio, Desempenho e Energia abrem os painéis já existentes, com retorno à Home e foco restaurado.

## Consequências

- Revisa apenas o papel de **Início** nas ADRs 0006/0007 e o ponto de entrada visual anterior. Demais decisões de modos, gesto, escopo local e leitura HID continuam válidas.
- O conjunto de utilidades é uma primeira proposta concreta para revisão do usuário.
- A implementação real dos recentes requer validar uma fonte local, ordenação e estados vazios/indisponíveis.
- Sem alteração no pacote instalado, no sistema ou no lançamento de jogos.

## Evidências

- Pedido do usuário nesta conversa em 2026-10-23.
- [Design da Home](../design/home.md).
- `src/app/HomePreview.cs` e render `bin/design-preview/painel-0.png`.
