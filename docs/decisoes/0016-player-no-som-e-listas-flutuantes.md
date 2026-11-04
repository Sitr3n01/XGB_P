# 0016 — Player também no Som, sem Jogo atual, rolagem fina e listas flutuantes

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

Ao avaliar a prévia com o player da ADR 0015, o usuário pediu cinco ajustes:

- o player ficaria melhor **acima do acesso rápido** na Home;
- ele também deveria aparecer no **painel Som, acima da saída de som**;
- o item **Jogo atual** do trilho ficou redundante, porque a Home já mostra o jogo e "Voltar ao jogo";
- a barra de rolagem estava **grossa**, e deveria ser **bem fina, como no Windows 11**, em todo o app;
- no Som, as listas de **saída e microfone** não deveriam **empurrar os controles para baixo**, e sim abrir flutuando.

## Decisão

- **Home:** o cartão do player passa a ficar entre os jogos recentes e o **Acesso rápido**. A ordem das linhas é: Big Picture, jogo atual, apps abertos, três recentes, três linhas do player e acesso rápido. A navegação vertical pula os controles inativos e lembra a coluna. Revisa a posição "no pé da Home, abaixo do acesso rápido" da ADR 0015.
- **Som:** o mesmo cartão aparece no topo do painel, acima de "Saída de som". Ele usa a mesma fonte, os mesmos comandos e o mesmo volume pelo mixer. As três linhas do player entram na navegação por linhas do Som:
  - esquerda/direita andam dentro da linha;
  - na ponta esquerda, o foco volta ao trilho;
  - nos sliders, esquerda/direita ajustam o valor.

  Mudanças na estrutura do player, como a fonte encerrar ou surgir uma segunda fonte, refazem o painel preservando o foco e a rolagem do mixer.
- **Entrada no Som:** pelo trilho, pelo atalho da Home ou por L1/R1 o foco vai para **Saída de som**, e não para o player. Assim, ✕ logo ao entrar não pausa nem pula a música.
- **Jogo atual sai do trilho, da navegação e da prévia.** A ordem do trilho fica Início, Biblioteca, divisor, Som e Desempenho no topo, mais Configurações e Energia no pé. L1/R1 percorrem Início → Som → Desempenho → Configurações → Controle e energia. Na Home, o jogo atual continua como "Voltar ao jogo". Revisa a lista de painéis da ADR 0007. O ID interno 2 fica reservado, sem botão visível, para não renumerar os demais painéis.
- **Rolagem do Windows 11 em todo o app:** um estilo implícito vale para todas as barras e áreas roláveis.
  - **Barra:** linha de 3 px alinhada à borda, cinza `#8C8C8C`. Sob o mouse ou no arraste, ela engrossa para 6 px e clareia para `#C8C8C8`. Sem setas, sem trilho visível e sem foco.
  - **Áreas roláveis:** a barra fica **sobreposta** ao conteúdo, em vez de ocupar uma coluna. O `Padding` de cada área reserva só a faixa da linha.
- **Listas de dispositivos flutuantes:** o seletor abre a lista como um menu, ancorada logo abaixo dele e por cima dos controles seguintes. O layout não se move. Visual da lista:
  - superfície opaca `SurfaceFlyout` (`#2C2C2C`), borda `Divider`, cantos de 10 px e sombra;
  - mesma largura do seletor, altura máxima de 220 px com rolagem fina.

  Enquanto a lista está aberta:
  - o foco fica preso entre o seletor e as opções;
  - escolher uma opção aplica o padrão e fecha a lista;
  - ○/Esc ou um clique fora fecham a lista sem acionar o que estava por baixo.

  Revisa os "seletores inline" da ADR 0013.

## Consequências

- No Som, o mixer perde altura para o player. Com muitos apps, ele mostra menos linhas e rola antes. A saída, o microfone e "Abrir Som do Windows" continuam fixos.
- Sem mídia, o cartão mostra "Nada tocando agora" no Som e na Home, como já acontecia na Home.
- Aberta, a lista cobre o volume e o mixer. A lista do microfone pode cobrir o início do mixer.
- Sem o painel Jogo atual, "Fechar o jogo" e o tempo de sessão de exemplo deixam de existir na prévia. Se voltarem, vão para a Home ou para um menu do jogo, a decidir.
- O diagnóstico `--verify-audio-live` passa a ler também a mídia real, só para o render, sem enviar comandos.

## Evidências

- Pedido do usuário nesta conversa, em 2026-10-23.
- `--verify` da prévia com dados controlados:
  - 5 painéis;
  - trilho e ciclo sem Jogo atual;
  - entrada do Som pela saída de som;
  - player no topo do Som, com navegação, comandos e volume;
  - Home com o player acima do acesso rápido;
  - lista do microfone aberta sem mover o slider do microfone nem o mixer;
  - foco preso na lista aberta;
  - mixer rolando com a barra fina.

  Renders: `som-midia-exemplo.png`, `som-lista-flutuante.png`, `som-mixer-rolagem.png` e `home-midia-exemplo.png`.
- Diagnósticos só leitura com os dispositivos reais: `som-real.png` mostra o Spotify no topo do Som; `saidas-reais.png` e `microfones-reais.png` mostram as listas flutuantes.
