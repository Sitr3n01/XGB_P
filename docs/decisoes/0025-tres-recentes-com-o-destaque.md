# 0025 — Recentes com os três jogos mais recentes, incluindo o destaque

- **Status:** Substituída pela [ADR 0060](0060-recentes-sem-o-jogo-do-destaque.md) em 27/10/2026: o jogo de Voltar ao jogo não ocupa vaga nos três recentes. O conteúdo abaixo é histórico.
- **Data:** 2026-10-24
- **Revisa:** a regra da ADR 0020 de não repetir o destaque nos recentes, e o "até três, excluindo o destaque" da ADR 0019

## Contexto

O usuário pediu que "Jogos recentes" tenha sempre 3 jogos exibidos.

**Observado** na leitura real de 2026-10-24, depois de recriar o mapeamento do Genshin:

- só três jogos instaláveis: Wuthering Waves, Genshin Impact e CONTROL Resonant;
- Honkai: Star Rail, Black Myth Wukong e Lies of P apontam para executáveis que não existem mais;
- com um jogo sempre em "Voltar ao jogo" e sem repetição, sobravam dois na lista.

Havia três opções, e o usuário escolheu **repetir o jogo do destaque**:

- completar com jogos do histórico não instalados;
- repetir o jogo do destaque;
- usar só jogos instalados.

## Decisão

- A lista mostra os **três jogos mais recentes**, incluindo o de "Voltar ao jogo".
- Ordem:
  - o jogo do destaque, se instalado ou aberto;
  - os outros jogos abertos, do foco mais recente ao mais antigo, que nunca saem da lista;
  - os fechados disponíveis, por último acesso.
- O desempate é determinístico pela `GameKey`.
- Com menos de três jogos elegíveis no PC, aparecem menos: não há exemplos nem vagas vazias. Jogos não instalados continuam fora.
- Um jogo aberto pode ter pausar/fechar nos dois lugares, no destaque e na sua linha da lista. As etiquetas de foco da lista têm prefixo próprio (`recent-pause-`, `recent-close-`), e cancelar o fechamento volta ao botão de origem.
- Navegação: ao descer de outra seção, o foco entra pelo cartão do jogo. Entre linhas de jogo, a coluna se mantém (cartão, pausar ou fechar). O foco nunca cai direto em fechar.

## Consequências

- O jogo de "Voltar ao jogo" aparece duas vezes na Home. A linha da lista mostra o último acesso, ou "Em execução"/"Pausado".
- Selecionar o jogo do destaque na lista faz o mesmo que "Voltar ao jogo": retoma se estiver aberto e inicia se estiver fechado.

## Evidências

- A leitura real `--verify-games-live` mostrou na lista Wuthering Waves, Genshin Impact e CONTROL Resonant, com Wuthering em "Voltar ao jogo".
- A suíte confere:
  - três recentes com o destaque primeiro;
  - o destaque fechado liderando a lista;
  - a troca do destaque quando muda o último uso;
  - os abertos com botões nas duas posições e as etiquetas da lista;
  - a navegação entre apps, jogos, player e acessos.
- Renders.
