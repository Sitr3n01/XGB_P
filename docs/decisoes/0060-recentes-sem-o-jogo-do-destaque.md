# 0060 — Três outros recentes, sem o jogo de Voltar ao jogo

- **Status:** Aceita; implementada e publicada, bateria aprovada; teste físico no FSE pendente
- **Data:** 2026-10-27
- **Substitui:** ADR 0025 quanto à repetição do destaque; retoma a exclusão definida pela ADR 0020.

**Revisão posterior de apresentação:** a [ADR 0064](0064-jogos-abertos-no-grupo-voltar-ao-jogo.md) agrupa todos os abertos em Voltar ao jogo e reserva os recentes da Home aos fechados. A descrição de prioridade dos outros abertos abaixo é histórica; a consulta legada `Recent` permanece para compatibilidade.

## Contexto

O usuário esclareceu o comportamento antes de autorizar a implementação: Voltar ao jogo deve mostrar o jogo atual ou, sem jogo aberto, o último executado; Jogos recentes deve reservar suas três vagas a outros jogos. O código ainda seguia a ADR 0025, que incluía o destaque para completar a lista. Além disso, a preservação de posições durante atualizações da Home podia recolocar entradas antigas da lista, inclusive o novo destaque.

## Decisão

- Preservar a seleção existente de Voltar ao jogo: jogo atual identificado; sem ele, último uso conhecido. Não mudar histórico, detecção de execução ou disponibilidade.
- Em `GamesView.Recent()`, excluir por **GameKey** o mesmo jogo de `Featured`, tanto dos abertos quanto dos fechados, **antes** do limite de três. O jogo excluído não ocupa uma vaga.
- Preservar a prioridade existente dos demais jogos abertos e a ordenação por último acesso dos fechados, com desempates determinísticos. Outros jogos abertos continuam elegíveis; somente o jogo representado em Voltar ao jogo é excluído por esta regra.
- Renderizar o snapshot canônico atual, sem recuperar cartões antigos para preservar suas posições. Preservar o foco por identidade; se o cartão focado passar a ser o destaque, voltar o foco a Voltar ao jogo.
- Se houver menos de três outros jogos elegíveis, mostrar somente os existentes, sem repetir, inventar ou incluir indisponíveis para preencher. O catálogo real desta passagem tem quatro jogos disponíveis com histórico, permitindo três outros no cenário sem jogo aberto.

## Consequências

Sem alteração de dados ou formato de arquivo. Imagens, retomada, pausa, fechamento, confirmações, política FSE e transporte da Steam permanecem. O desenho comporta três recentes independentes do cartão superior.

## Evidências

11 verificações isoladas novas: excluir antes de limitar, estado aberto/fechado, identidade versus nome/referência, outro jogo aberto, disponibilidade e histórico vazio/curto. Regressões WPF cobrem substituição do destaque durante foco na lista, preenchimento das três vagas, ordem canônica e navegação com apps/player. Renderizações são de fixtures, sem iniciar jogos reais. Bateria, publicação e limites na continuidade.
