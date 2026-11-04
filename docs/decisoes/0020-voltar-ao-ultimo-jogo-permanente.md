# 0020 — Voltar ao jogo permanece com o último jogo executado

- **Status:** Aceita; exclusão do destaque retomada pela [ADR 0060](0060-recentes-sem-o-jogo-do-destaque.md), após a revisão histórica da ADR 0025.
- **Data:** 2026-10-23

## Contexto

Na primeira implementação da ADR 0019, o cartão “Voltar ao jogo” desaparecia quando não havia janela de jogo aberta. O usuário pediu que o cartão permanecesse com o último jogo executado, excluindo esse jogo da lista de recentes.

## Decisão

- Manter o cartão **Voltar ao jogo** acima dos aplicativos abertos, com o mesmo título e ícone.
- Havendo jogo atual identificado, apresentar esse jogo; sem ele, apresentar o último uso conhecido (histórico próprio ou informado pela Steam). O destaque é calculado por identidade e data, sem depender da existência de janela aberta.
- Ao selecionar: retomar a janela se estiver aberta; caso contrário, solicitar novo lançamento pelo mesmo fluxo dos recentes, incluindo validação de disponibilidade e confirmação quando houver outro jogo aberto.
- Excluir o jogo do cartão da lista de recentes em ambos os estados, inclusive durante atualizações com foco na lista.
- Se o último jogo estiver indisponível, conservar nome/cartão desabilitado. Sem histórico algum, manter o cartão desabilitado com “Nenhum jogo executado ainda”; não inventar um título.

## Consequências

Revisa a regra de ocultar a retomada sem janela nas ADRs 0010/0019 e no design da Home. Reutiliza catálogo/histórico existentes, sem novo arquivo ou mudança de formato. “Último jogo” importado mantém a mesma limitação de procedência da ADR 0019: não é uma execução observada pelo XGB_P.

## Evidências

- `GamesView.Featured` centraliza a seleção; `Recent()` exclui a mesma identidade. `RecentGames.cs` mantém o cartão e resolve retomada/lançamento no clique.
- `-Verificar` passou: destaque com jogo fechado, ausência de duplicação, novo lançamento com comando substituído, jogo indisponível, atualização do último uso, estado sem histórico e regressões dos demais painéis.
- `--verify-games-live` foi executado sem iniciar jogos. No retrato desta máquina, Wuthering Waves fica no cartão e Genshin Impact/CONTROL Resonant nos recentes.
- Renders em `bin/design-preview/home-ultimo-jogo.png`, `home-jogos-reais.png` e `home-jogos-vazio.png`. Lançamento real continua sujeito à validação do usuário; o agente não abriu jogos.
