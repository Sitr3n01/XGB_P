# Painel de Som

Implementado na prévia em 2026-10-23 ([ADR 0012](../decisoes/0012-painel-de-som-funcional.md)), com layout compacto e mixer integrado pela [ADR 0013](../decisoes/0013-som-compacto-e-mixer-integrado.md). Player no topo e listas flutuantes pela [ADR 0016](../decisoes/0016-player-no-som-e-listas-flutuantes.md). Usa a paleta Xbox e controles reais do Windows.

## Acessos

- Home → **Áudio**.
- Ícone de alto-falante no trilho.
- L1/R1 a partir da Home (o painel Jogo atual saiu do trilho pela ADR 0016).

Em todos os acessos, o foco entra em **Saída de som**, e não no player. Assim, ✕ logo ao entrar não pausa a música.

## Conteúdo

| Controle | Comportamento |
|---|---|
| Player de mídia | o mesmo cartão da [Home](home.md#player-de-mídia), no topo do painel: capa, faixa, transporte, posição, aleatório, repetir, troca de fonte e volume/mute do app pelo mixer |
| Nome da saída | abre/fecha a lista flutuante de saídas; selecionar aplica o dispositivo padrão |
| Volume do PC | slider 0–100% do dispositivo de saída padrão de multimídia |
| Quadrado com alto-falante ao lado do slider | alterna mute sem zerar o nível; ícone riscado indica som mutado |
| Nome do microfone | abre/fecha a lista flutuante de entradas; selecionar altera o dispositivo padrão |
| Volume do microfone | slider 0–100% da entrada padrão de multimídia |
| Quadrado com microfone ao lado do slider | alterna mute da entrada, com ícone riscado quando mutado; não inicia gravação |
| Mixer de aplicativos | fica na tela principal, abaixo do microfone, com ícone, nome, nível e mute por aplicativo |
| Abrir Som do Windows | abre `ms-settings:sound` e oculta a prévia |

**A escolha de saída/entrada aplica o padrão para jogos, mídia e chamadas.** Apps configurados para um dispositivo específico podem continuar usando sua própria seleção.

## Interação

- Setas ou direcional navegam; esquerda/direita no slider ajustam em passos de 5 pontos percentuais. Mouse permite ajuste em passos de 1.
- Enter/✕ seleciona um botão; quando o slider está em foco, alterna o mute daquele controle. Direita no quadrado de mute leva ao slider; cima/baixo navegam por linhas.
- **Player:** as três linhas entram na navegação por linhas. Esquerda/direita andam dentro da linha e, na ponta esquerda, voltam ao trilho; nos sliders, ajustam posição (10 s) e volume (5 pontos). Cima/baixo lembram a coluna e pulam os controles que o app não oferece.
- **Player — ADR 0047:** ✕/Enter nos sliders não muta nem reproduz/pausa. ○/Esc no volume volta ao alto-falante; na posição, a reproduzir/pausar, dentro do player. Os botões próprios disparam as ações. Ajustes de volume/mute são independentes e preservados durante aplicação; troca de fonte não herda pendências da anterior. A regra de mute pelo slider dos demais controles de Som permanece.
- **Listas de dispositivos:** abrem como um menu flutuante sob o seletor, por cima dos controles seguintes, sem mover o layout. A lista tem superfície opaca, borda, cantos arredondados, sombra e no máximo 220 px de altura com rolagem fina. Enquanto ela está aberta, o foco circula só entre o seletor e as opções.
- ○/Esc ou um clique fora fecham a lista aberta; o clique fora não aciona o controle por baixo. Se o painel veio do atalho da Home, voltar novamente restaura o foco nesse atalho.
- O mixer tem rolagem própria, mantendo saída, microfone e acesso ao Windows visíveis. Com o player no topo, ele mostra menos linhas e rola antes.
- A barra de rolagem é a fina do Windows 11, comum a todo o app: uma linha sobreposta de 3 px, que engrossa para 6 px sob o mouse.
- Mudanças externas de volume/mute e dispositivos são consultadas aproximadamente a cada 2 segundos enquanto o painel está visível. Eventos rápidos de volume são agrupados por até 150 ms.
- Na ausência de dispositivo, o controle correspondente fica indisponível; o acesso ao Som do Windows permanece.
- Abrir ou atualizar o painel preserva nível, mute e dispositivos existentes.

## Mixer

Usa as sessões Core Audio que alimentam os controles de volume do Windows. Não enumera indiscriminadamente todos os processos do PC. Sessões expiradas e processos encerrados são descartados; sessões inativas ainda existentes permanecem, para permitir ajustar um aplicativo antes de ele voltar a tocar.

A apresentação tem uma linha por aplicativo: une sessões pela identidade do executável ou por um GUID de agrupamento não vazio informado pelo app. O mesmo programa em duas saídas não aparece repetido. Nomes iguais, sozinhos, não agrupam programas diferentes; `GUID_NULL` não une apps distintos. Sons do sistema têm uma linha própria.

Nome: preferir o nome da sessão informado pelo Windows, resolvendo recursos indiretos; fallback para descrição/produto do executável e nome do processo. Ícones são extraídos de arquivos locais, com símbolo genérico quando indisponíveis. Não há catálogo de nomes fixos.

Um ajuste de aplicativo controla suas sessões nas saídas em que elas existem, sem mudar seu roteamento. O nível exibido é a média das sessões; o mute fica riscado quando todas estão mutadas. Os comandos são separados por dispositivo e identificador exato da sessão, evitando atingir outro processo que reutilize um PID. Essa unificação entre saídas é uma escolha da prévia: o Sndvol clássico documentado pela Microsoft trabalha com uma saída por vez.

O tracking consulta o Windows a cada aproximadamente 2 segundos. Alterações de topologia preservam o foco e a posição da lista; a reconstrução é adiada durante arraste de slider ou mudanças pendentes.

## Efeitos e reversão

Os ajustes são reais e não são revertidos automaticamente ao fechar a barra. Use o painel ou Som do Windows para restaurar volume, mute e dispositivos. O estado lido no início de cada execução fica em `bin/design-preview/audio-inicial-AAAAMMDD-HHMMSS.txt`; ações e erros ficam em `preview.log`.

Trocar dispositivo usa uma interface privada do Windows. Caso não funcione nesta versão do sistema, use **Abrir Som do Windows**. Nenhum componente da Microsoft é modificado ou recebe injeção.

## Validação feita

- Testes de UI com dados controlados: volume master, mute pelo quadrado e pelo slider, navegação horizontal/vertical, seletores inline, agrupamento entre saídas e por GUID, nomes iguais de programas diferentes, alvos de sessões, scroll/foco durante tracking e estado sem dispositivos.
- Leitura real e renders de Som, mixer, saídas e microfones, incluindo nomes reais e rolagem.
- ADR 0016, com dados controlados:
  - player no topo do Som: navegação, comandos e volume;
  - entrada do painel pela saída de som;
  - lista do microfone aberta sem mover o slider do microfone nem o mixer;
  - foco preso na lista aberta.

  Renders `som-midia-exemplo.png`, `som-lista-flutuante.png` e `som-mixer-rolagem.png`. No diagnóstico real, `som-real.png` mostra a mídia real no topo, lida sem comandos.
- Setters nativos de master/microfone e 12 sessões testados reenviando os valores existentes e conferindo a leitura, sem mudar o nível ou mute.
- Interface de seleção aceitou reaplicar os seis padrões de saída/entrada já existentes. **Não foi feita troca para um dispositivo diferente**, nem validação auditiva ou em jogos.
- Revisão do layout: leitura de 12 sessões reais, reunidas em 6 linhas (sistema, Firefox, Radeon, Spotify, Steam e Wallpaper Engine). Renders do painel principal, fim do scroll e listas expandidas inspecionados. A revisão usou apenas leitura real e testes de comandos substituídos; não alterou volumes ou dispositivos. Não foi comparada automaticamente a árvore de UI do mixer nativo.

Para renderizar com dados reais, sem setters:

```powershell
Start-Process .\bin\XgbDesignPreview.exe -ArgumentList '--verify-audio-live' -Wait
```

Renders: `som-real.png`, `mixer-real.png`, `mixer-real-final.png`, `microfones-reais.png` e `saidas-reais.png`, em `bin/design-preview/`.

Há cópias preservadas, resultados e procedência. Na coleta final de leitura às 19:22 foram 13 sessões reunidas em 6 linhas; os testes nativos anteriores tinham 12. Nessa coleta a saída padrão de multimídia era Razer em 86%, e o microfone Razer estava sem mute; não restaurar o estado inicial antigo automaticamente. Ver [referência atualizada](../referencia/audio-windows.md).

Teste opcional dos setters, reaplicando os valores atuais (não troca dispositivos):

```powershell
powershell -NoProfile -Mta -Command "[void][Reflection.Assembly]::LoadFrom((Resolve-Path '.\bin\XgbDesignPreview.exe').Path); [XGBP.Audio.CoreAudio]::CheckControls()"
```
