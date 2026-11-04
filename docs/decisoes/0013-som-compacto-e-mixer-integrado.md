# 0013 — Som compacto e mixer integrado

- **Status:** Aceita. As listas de dispositivos inline foram revisadas pela [0016](0016-player-no-som-e-listas-flutuantes.md): abrem flutuando, sem empurrar os controles.
- **Data:** 2026-10-23

## Contexto

O usuário pediu mutes quadrados junto aos sliders, seleção de saída expansível no painel principal e volumes dos aplicativos nesse mesmo painel com scroll. A referência é o mixer rápido do Windows, com programas reconhecíveis e sem linhas repetidas por saída.

## Decisão

- Substituir os botões largos de mute por quadrados de 34 × 34 com ícones vetoriais de alto-falante/microfone e risco quando mutados. Tooltip e nome acessível descrevem a ação.
- Expandir as escolhas de saída e entrada na própria tela, uma lista por vez, sem navegar para outro painel. Preservar o botão que abre Som do Windows.
- Fixar controles de PC/microfone e colocar os aplicativos em região com rolagem própria. Enter/✕ no slider alterna mute; cima/baixo navegam por linhas.
- Continuar lendo sessões Core Audio em todas as saídas, mas exibir uma linha por identidade do programa, também respeitando grupos não vazios informados pelas sessões. Não agrupar por nome visível nem tratar `GUID_NULL` como grupo comum. Unificar sons do sistema.
- Aplicar um ajuste às sessões daquele grupo, separando os comandos por dispositivo e IDs exatos. Mostrar a média dos níveis e mute quando todas as sessões estiverem mutadas.
- Atualizar a cada aproximadamente 2 segundos, preservando foco/scroll e adiando a reconstrução durante arraste. Não usar a lista de processos como lista de apps com áudio.

## Consequências

O painel permanece compacto e o mesmo app não se repete para cada saída. Apps sem sessão de áudio não aparecem; sessões inativas ainda existentes podem aparecer, como ocorre nos controles baseados em sessões. Um ajuste de aplicativo afeta suas sessões nas várias saídas, mas não altera seu roteamento.

Essa unificação entre saídas é uma decisão de apresentação do XGB_P, não uma afirmação de que o Sndvol clássico tenha esse comportamento: a API documenta o agrupamento por GUID dentro de cada endpoint. A equivalência exata com todas as versões do mixer rápido do Windows 11 não foi provada.

Atualiza a apresentação e o agrupamento da ADR 0012; mantém backend, efeitos persistentes dos ajustes e limitações da escolha de dispositivo.

## Evidências

- Pedido e imagem de referência do usuário nesta conversa.
- [Design e validação](../design/som.md), [contratos de áudio](../referencia/audio-windows.md).
- `--verify`: testes de alvos, agrupamento, navegação, listas inline e tracking com scroll/foco.
- `--verify-audio-live`: 12 sessões reais em 6 linhas e renders inspecionados, sem setters.
