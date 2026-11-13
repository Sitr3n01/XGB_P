# Home: jogos recentes e acesso rápido

**Revisão atual — 3.0.0:** Início → Voltar ao jogo → Acesso rápido (quatro retângulos sem texto e seis quadrados) → Aplicativos abertos → Jogos recentes → player. Apps ficam centralizados enquanto couberem. O mesmo conteúdo atende trilho e título com seta; a preferência fica nas Configurações. Ver [ADRs 0099, 0105 e 0112](../decisoes/README.md) e a [atualização pública](../atualizacao-publica-2026-11-08.md). As seções abaixo preservam o histórico de design.

**Publicada na 1.0.6 (ADR 0072):** depois da saída nativa, a Home pode mostrar “Aguardando a Steam concluir a sessão anterior…”. A abertura aguarda confirmação da sessão Steam por até 45 segundos, inclusive ao escolher um recente após fechamento manual. Sem confirmação, cancela. Entrega e teste físico pendente.

**Revisão da ADR 0070 — publicada na 1.0.5, 28/10:** ao escolher outro jogo, a confirmação usa somente texto: **Fechar jogo e iniciar** e **Cancelar**, inicialmente focado. O novo jogo só abre após saída confirmada dos jogos identificados na pergunta. Ao fechar o último jogo ou a barra sem jogo ativo, no modo Xbox, o destino é o Início do Big Picture. Abrir pelo PS continua mostrando a barra. Prévia, testes e limites.

Design iniciado em 2026-10-23 por solicitação do usuário. Decisões nas [ADRs 0010](../decisoes/0010-home-com-recentes-e-utilidades.md), [0011](../decisoes/0011-navegacao-steam-ativa-na-previa.md), [0014](../decisoes/0014-alternador-de-janelas-na-home.md), [0015](../decisoes/0015-player-de-midia-na-home.md), [0016](../decisoes/0016-player-no-som-e-listas-flutuantes.md) e [0029](../decisoes/0029-lojas-pelo-acesso-rapido.md) (Lojas). Continuidade completa e evidências preservadas.

**Estado atual — ADRs 0019–0021/0025/0049:** jogo atual/recentes vêm de fontes reais. O cartão do atual/último jogo é permanente e **lidera a lista de até três recentes**, seguido dos demais abertos e dos fechados elegíveis. Duas leituras consecutivas da runtime exata com PID + criação podem salvar o uso sem foco; fechar/reiniciar preserva o histórico. Nome/pasta apenas conservam confirmação por foco. Imagens são extraídas localmente, com símbolo U+E7FC de reserva. Ausência de histórico não gera exemplos nem datas retroativas. Catálogo Steam + atalhos externos, cadastro de outras origens e observador independente de loja: [guia atual](../guias/jogos-recentes.md). Revisão de persistência e teste real do Genshin pendente; consolidação da revisão de UX.

## Hierarquia recomendada

**Feedback operacional — ADR 0035:** o rodapé informa pedido, confirmação, aviso ou erro, com ícone e texto completo em múltiplas linhas. Abrir/fechar/pausar/retomar jogos aguarda a observação correspondente; sucesso expira em 5 s, informação em 6 s e aviso/erro em 12 s, inclusive com a barra oculta. O timeout de abertura não reabre a barra. As caixas de descrição de controles continuam ocultas na Home; o rodapé de operações permanece ativo. Fluxos, testes e limites.

**ADR 0021:** cartões de jogos agora usam imagens locais extraídas automaticamente, mantendo proporções e ícone de controle como reserva. A direção anterior de símbolo uniforme foi revisada por solicitação do usuário.

**ADR 0020:** o cartão “Voltar ao jogo” é permanente. Sem jogo aberto, mostra o último jogo executado conhecido e permite iniciá-lo novamente. O jogo destacado nunca se repete nos recentes. Sem histórico/indisponível, o cartão permanece desabilitado.

| Ordem | Conteúdo | Objetivo |
|---|---|---|
| 1 | **Início do Big Picture**, com rótulo visível só "Início" (ADR 0017) | voltar à Home da Steam, conforme pedido do usuário (ADR 0011) |
| 2 | **Voltar ao jogo**, com o nome da sessão atual | retomar sem procurar menus |
| 3 | **Aplicativos abertos**, em faixa de ícones de janelas reais | alternar de janela sem procurar no desktop |
| 4 | **Três jogos recentes**, incluindo o do destaque (ADR 0025), com ícone local, nome e último acesso | trocar de jogo com poucos passos; sem preencher vagas fictícias |
| 5 | **Player de mídia** da música ou vídeo tocando no PC | controlar a música sem sair da barra (ADRs 0015 e 0016) |
| 6 | **Biblioteca**, **Lojas**, **Downloads**, **Áudio**, **Desempenho** e **Energia**, apenas ícones | acessar utilidades em uma única linha |

Pela ADR 0025, o jogo do destaque lidera a lista de recentes, seguido dos outros abertos e dos fechados. Com menos de três jogos instalados no PC, aparecem menos. A Home é compacta e mantém o jogo/desktop visível ao fundo; a biblioteca completa continua no Big Picture. O conjunto de utilidades é uma proposta inicial implementada para avaliação do usuário.

**ADR 0024:** "Voltar ao jogo" mostra o jogo aberto em foco. Os outros jogos abertos lideram os recentes. Os jogos abertos têm ⏸/▶ (pausar/retomar) e ✕ (fechar) à direita do cartão, no padrão dos atalhos: 52 px, altura do cartão e só ícone. Jogos fechados não têm esses botões. Fechar pede confirmação e só depois envia o fechamento normal; forçar aparece se o jogo não sair em 10 s. Evidências.

**ADR 0026:** segurar 2 s o ícone de um app em "Aplicativos abertos" mostra um X logo ao lado, com o foco nele. Selecionar o X fecha a janela pelo fechamento normal; Esc/○ ou focar outro controle recolhe o X. Um toque curto continua abrindo o app. A barra verde no pé do ícone aguarda 250 ms antes de aparecer, para não piscar num toque curto; preenche nos 1750 ms restantes, sem mudar os 2 s do gesto. Soltar/cancelar ou iniciar transferência limpa imediatamente a animação, inclusive durante o atraso (refino de 2026-10-25).

## Navegação implementada na prévia

- A prévia inicia em **Início**, com foco em **Início do Big Picture**, acima de **Voltar ao jogo**. O botão mostra só "Início"; a dica e o nome acessível dizem "Início do Big Picture".
- Abaixo de Voltar ao jogo, **Aplicativos abertos** mostra uma faixa horizontal de ícones de janelas reais, incluindo minimizadas. Selecionar solicita restaurar/ativar a janela e oculta a barra. Uma janela por ícone permite escolher entre várias janelas do mesmo programa.
- Recentes formam uma lista vertical. **Acesso rápido** tem uma linha de seis ícones, sem rótulos abaixo: Biblioteca, Lojas, Downloads, Áudio, Desempenho e Energia (Lojas desde a ADR 0029). Tooltips e nomes acessíveis preservam a identificação. Ao focar com teclado ou controle, o nome aparece ao lado direito do título da seção, sem rodapé que mude o layout (ADR 0015).
- Esquerda/direita navegam entre utilidades; cima/baixo preservam a coluna quando possível.
- Esquerda na primeira coluna retorna ao trilho. Direita no trilho entra na primeira ação.
- Selecionar Lojas, Áudio, Desempenho ou Energia abre o painel correspondente. ○/Esc retorna à Home e restaura o foco no atalho que abriu o painel.
- **Lojas (ADR 0029):** painel com as lojas e launchers instalados (Xbox, Steam, Epic, EA, Ubisoft, Riot, Microsoft Store e outros conhecidos), com o ícone real, o nome e o que acontece ao abrir. Selecionar abre pelo shell e oculta a barra; a Steam e a Epic abrem direto na loja. O painel fica sob o Início: no trilho, o Início fica destacado, ir ao trilho não sai de Lojas, e L1/R1 sai como da Home.
- L1/R1 ou Q/E percorrem Início → Som → Desempenho → Configurações → Controle e energia, na ordem do trilho. Biblioteca continua sendo uma ação direta. O painel Jogo atual saiu do trilho (ADR 0016): o jogo fica aqui, em Voltar ao jogo.

## Escopo desta entrega

**Aplicativos abertos são reais** desde a [ADR 0014](../decisoes/0014-alternador-de-janelas-na-home.md). A faixa consulta janelas de topo do desktop a cada aproximadamente 2 segundos enquanto a Home está visível e atualiza ao reabrir a barra. Não é uma lista de todos os processos: exclui janelas sem título, ocultas/cloaked, ferramentas, janelas auxiliares com owner e a própria prévia. Apps apenas na bandeja não aparecem. A lista pode diferir do Alt+Tab nativo para alguns modelos de janela e áreas de trabalho virtuais.

Ícones vêm das próprias janelas/classes ou dos executáveis locais, com símbolo genérico quando necessário. O título completo aparece no tooltip e, ao focar, no lugar da contagem ao lado de "Aplicativos abertos", distinguindo várias janelas do mesmo programa. A faixa tem setas, roda do mouse e rolagem automática com o foco. A ordem é preservada durante a navegação; uma nova abertura pode usar a ordem Z atual. Não fecha programas, não inicia apps nem captura imagens de suas janelas.

Cada seta aparece somente quando há conteúdo fora da área visível no respectivo lado. No início, a esquerda fica oculta; no fim, a direita fica oculta; se todos os apps couberem, ambas somem. **Refinamento visual:** a faixa ocupa toda a largura da Home, sem reservar colunas para setas. Chevrons vetoriais com área de clique de 44 × 48 ficam sobre os ícones nas bordas, com degradê da superfície e máscara de opacidade para esfumar a continuação da lista. O hover dá contraste à seta sem criar um bloco permanente separado.

Ícones têm área de 52 × 52, espaçamento de 8 e alinhamento com os outros cartões. O cabeçalho informa a quantidade de janelas. As setas avançam por grupos adequados à largura da faixa; a roda avança um ícone. Ao focar pelo teclado/controle, o app é trazido para fora do degradê, mantendo a borda de foco inteira visível. Paginar preserva um foco visível, sem ativar aplicativos. Ao atualizar a lista, a posição é ancorada ao app que estava no início da área visível, quando ele ainda existe.

A ativação usa `ShowWindowAsync(SW_RESTORE)` apenas para minimizadas e `SetForegroundWindow`, validando HWND/PID/classe antes e conferindo foreground depois. O pedido acontece enquanto a prévia ainda tem foco; em seguida ela se oculta. Se a janela fechar ou o Windows bloquear a mudança de foco, a prévia continua/volta aberta com mensagem. Retornos dos atalhos da Home usam identidade estável, para que uma mudança na lista de apps não devolva o foco ao botão errado.

Na primeira entrega, recentes e Forza eram exemplos fixos (ADR 0010). Desde as ADRs 0019–0021, a execução normal usa catálogo/histórico/sessões reais e imagens extraídas localmente. O símbolo genérico só aparece quando não há arte válida. Não são baixadas capas.

**Início do Big Picture e Biblioteca são funcionais** desde a ADR 0011. O primeiro reutiliza a janela ou solicita `steam://open/bigpicture` (ADR 0039). Biblioteca, tanto no trilho quanto na Home, usa a mesma ação: modo Xbox ativo solicita `steam://open/library/collection/all-games`; desktop/API indisponível solicita `steam://open/games`. A prévia se oculta quando o shell aceita o pedido; em erro de despacho permanece aberta. O destino da nova rota foi conferido no Big Picture real: Coleções → Todos, com a grade completa (ADR 0054). O retorno do shell, isoladamente, não confirma a tela final.

**Downloads também é funcional:** solicita `steam://open/downloads` pelo mesmo fluxo de abertura/ocultação e erro. A tela final na Steam deve ser validada no cliente real.

Recentes e Voltar ao jogo têm ações reais por escolha: retomar a janela ou iniciar novamente, com validação de disponibilidade, confirmação de outro jogo aberto e timeout. A suíte substitui comandos externos; não comprova lançamento/retomada reais em jogos. Os atalhos para painéis e Som continuam funcionais; comandos de energia permanecem simulados.

## Player de mídia

O player é um cartão real entre os recentes e o **Acesso rápido** (ADRs 0015 e 0016). Ele lê a mídia que o Windows conhece, a mesma do controle de mídia do sistema: Spotify, navegadores e outros apps que publicam no SMTC. O mesmo cartão aparece no topo do painel [Som](som.md).

| Linha | Conteúdo |
|---|---|
| 1 | capa, título, "artista · app" e anterior / **reproduzir-pausar** (botão branco) / próxima |
| 2 | tempo decorrido, barra de posição e duração |
| 3 | aleatório, repetir (desligado → lista → faixa), trocar fonte (só com 2+ mídias), mute e volume do app |

- **Controle (ADR 0047):** cima/baixo entram e saem de cada linha, preservando a coluna. Esquerda/direita na posição avançam/voltam 10 s; no volume, mudam 5 pontos. ✕/Enter nos sliders apenas confirma a edição, sem mute/play/pause. ○/Esc retorna ao alto-falante no volume ou a reproduzir/pausar na posição, sem acioná-los; outro voltar usa a navegação normal. Mute e reprodução ficam nos botões próprios.
- **Controles que o app não oferece:** ficam apagados e são pulados. Exemplo: o Firefox tocando vídeo só oferece reproduzir/pausar e posição.
- **Volume:** é o do app no mixer do painel Som. Sem correspondência, fica inativo.
- **Trocar fonte:** fixa a mídia escolhida. Sem escolha, o player segue a sessão atual do Windows.
- **Sem mídia:** o cartão mostra "Nada tocando agora". Se o foco estava no player, ele volta ao acesso rápido.
- **Medidas:** compactas para não haver rolagem na Home.

## Estados implementados e limites de validação

- **Sem jogo rodando:** manter Voltar ao jogo com o último uso conhecido, sem repeti-lo nos recentes (ADR 0020). Início do Big Picture permanece acima dele.
- **Sem recentes:** mensagem de estado vazio e acesso à biblioteca, sem inventar histórico.
- **Jogo indisponível:** excluir dos recentes; se for o último jogo destacado, conservar nome/cartão desabilitado. Mudança entre listagem e clique informa indisponibilidade; Biblioteca continua acessível.
- **Outro jogo aberto:** confirmar a intenção de trocar e preservar a sessão; nunca encerrar automaticamente o jogo atual.
- **Erro de despacho ou lançamento não confirmado:** manter/reabrir a prévia com retorno de falha; permitir nova tentativa após timeout de 180 s.

Esses estados têm implementação e testes com fontes/comandos substituídos; a leitura de catálogo/imagens reais também foi conferida. Testes de lançamento/retomada com jogos reais e integração ao modo Xbox permanecem pendentes. A proporção do destaque usa padding `12,11,12,11`, preservado ao aplicar imagens; recentes usam `10,6,10,6`.

## Validação

Render da Home em `bin/design-preview/painel-0.png`. A verificação existente inclui foco na grade, preservação de coluna, abertura/retorno do painel Desempenho, ação de um recente e ciclo de painéis. A aparência na TV e o uso do controle real continuam sujeitos à avaliação do usuário.

Revisão dos apps abertos: testes de comandos substituídos para alternância, janela fechada, erro, foreground não confirmado, faixa longa, estado vazio e foco durante atualização. Leitura real encontrou 8 janelas, todas com ícones locais; render `bin/design-preview/home-janelas-reais.png` e inventário `janelas-reais.txt`.

Teste nativo com janelas temporárias confirmou filtros, exclusão do próprio PID, rejeição de PID incorreto/janela fechada e restauração com foreground de uma janela minimizada em outro processo. Evidência em `bin/window-switch-check.txt`; fonte original em `bin/WindowSwitchCheck.cs`, agora com cópia preservada e receita. Não comprova alternância em todos os apps elevados, UWP, jogos exclusivos ou modo Xbox.

Refinamento da faixa: verificação cobre lista curta sem setas, início/meio/fim de uma lista longa, seleção fora da região esfumada e paginação sem ativar app. Renders com dados de exemplo: `faixa-exemplo-inicio.png`, `faixa-exemplo-meio.png`, `faixa-exemplo-fim.png` e `faixa-exemplo-foco.png`. A leitura real também gera `home-janelas-meio.png` e `home-janelas-fim.png`; se todos os apps couberem, esses renders naturalmente mostram a mesma faixa sem setas.

Para atualizar o render com janelas reais, sem alternar:

```powershell
Start-Process .\bin\XgbDesignPreview.exe -ArgumentList '--verify-windows-live' -Wait
```

Player: a verificação cobre reproduzir/pausar, anterior/próxima, aleatório, ciclo de repetir, posição, volume/mute pelo mixer, troca de fonte, controles inativos pulados, erro do app, fonte encerrada com foco no player e ausência de rolagem. Também confere a posição acima do acesso rápido: subir das utilidades passa pelas três linhas do player e chega aos recentes. Render de exemplo: `home-midia-exemplo.png`. Para o render com a mídia real, só leitura e sem comandos (gera `home-midia-real.png` e `midia-real.txt`):

```powershell
Start-Process .\bin\XgbDesignPreview.exe -ArgumentList '--verify-media-live' -Wait
```
