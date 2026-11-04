# 0015 — Player de mídia na Home

- **Status:** Aceita. A posição foi revisada pela [0016](0016-player-no-som-e-listas-flutuantes.md): o player fica acima do acesso rápido e também no topo do Som.
- **Data:** 2026-10-23

**Revisão de interação e sincronização:** a [ADR 0047](0047-midia-intencoes-fontes-e-retorno-dos-sliders.md) substitui a ação implícita de mute/play nos sliders: confirmar edita, voltar retorna ao player e os botões próprios disparam as ações. Também revisa identidade, filas, snapshots, seek, edição e capa. Os trechos abaixo registram a decisão original.

## Contexto

Depois da limpeza de textos, sobrou espaço livre embaixo da Home. O usuário pediu, nesse espaço, um player para controlar a música que está tocando no PC, "com todas as opções completas de um player de música".

## Decisão

- **Fonte dos dados:** a API pública de mídia do Windows (SMTC, `GlobalSystemMediaTransportControlsSessionManager`). É a mesma que alimenta o controle de mídia do sistema. Não usa teclas de mídia simuladas, injeção nem integração específica de um app.
- **Build:** referencia os metadados WinRT do próprio Windows (`%WINDIR%\System32\WinMetadata`) e a fachada `System.Runtime.dll` do .NET Framework, sem SDK nem NuGet. As operações assíncronas são esperadas por um auxiliar próprio, porque `System.Runtime.WindowsRuntime.dll` exige o `Windows.winmd` unificado do SDK.
- **Cartão no pé da Home, abaixo do acesso rápido, sem título de seção.** Três linhas:
  - capa, título e "artista · app", com anterior, reproduzir/pausar (botão branco em destaque) e próxima;
  - barra de posição com tempo decorrido e duração;
  - aleatório, repetir (desligado → lista → faixa), trocar fonte (só com mais de uma mídia), mute e volume do app.
- **Medidas:** compactas para caber no espaço livre sem rolagem. A verificação falha se a Home passar a rolar.
- **Volume:** é o mesmo do mixer do painel Som, isto é, as sessões de áudio do app de origem. O AppUserModelID do SMTC é casado com o executável da sessão de áudio de três formas:
  - pelo nome do `.exe`;
  - pelo nome e editor do pacote, no caminho `WindowsApps`;
  - pela propriedade `PKEY_AppUserModel_ID` das janelas (Firefox, Chrome).

  Sem correspondência, o volume fica inativo.
- **Fonte exibida:** a sessão atual do Windows. Trocar fonte fixa a escolha do usuário enquanto ela existir.
- **Controles inativos:** o que o app não oferece fica apagado e é pulado pela navegação. Esquerda/direita na barra de posição avança/volta 10 s; no volume, muda 5 pontos. Enter/✕ na barra reproduz/pausa; no volume, alterna o mute.
- **Leitura:** em worker, a cada ~1 s com a Home visível, e logo após cada comando. A posição avança pelo relógio entre as leituras. A capa só é lida quando a faixa muda.
- **Comandos:** saem somente por escolha do usuário, com atualização otimista que a leitura seguinte confirma. Se o app recusar, a prévia mostra uma mensagem.
- **Nome do item em foco:** na Home, ele passa ao lado direito do título da seção (apps abertos, acesso rápido) em vez do rodapé. Assim, focar um ícone não muda o layout nem empurra o player. Revisa a identificação "no rodapé ao focar" da ADR 0014. O rodapé fica só para o retorno de ações.

## Consequências

O que o player consegue fazer depende de cada app:

- **Spotify da Store:** ofereceu todos os controles.
- **Firefox (vídeo):** só reproduzir/pausar e posição.

Apps que não publicam mídia no SMTC não aparecem. O volume depende da correspondência entre a mídia e o áudio do app. Um app sem janela, com ID explícito, não tem volume no player, mas continua no mixer.

Não há gravação, captura de áudio, download de capa da internet nem preferência persistente. Ajustes de volume e comandos têm o mesmo efeito de usar o próprio app.

O diagnóstico `--verify-media-live` lê e renderiza sem enviar comandos.

## Evidências

- Pedido do usuário nesta conversa.
- Sonda de leitura em 2026-10-23, Windows 10.0.26300:
  - **Spotify** (`SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify`) com controles anterior/reproduzir/próxima/posição/aleatório/repetir, capa PNG e linha do tempo;
  - **Firefox** (`308046B0AF4A39CB`) só com reproduzir/pausar e posição.
- Verificação da prévia com backend substituído: comandos, ciclo de repetir, posição, volume/mute, troca de fonte, controles inativos, erro, fonte encerrada e ausência de rolagem.
- Render real só leitura. Fatos técnicos em [midia-windows.md](../referencia/midia-windows.md).
