# 0041 — Layout de desktop da Steam ao abrir a barra

- **Status:** Aceita; testada às 13:35–13:37 (o Big Picture reage por cerca de 1,1 s após abrir). Desde a ADR 0042, o ciclo só roda quando o PS também chega à Steam pelo virtual do DSX
- **Data:** 2026-10-25
- **Revisão posterior:** [ADR 0074](0074-transferencia-rapida-do-steam-input-apos-soltar-ps.md), candidata separada em 29/10: despacho após soltar o PS, confirmação também do contexto, transporte local restrito e bloqueio em falha. Evidências abaixo são históricas.
- **Complementa:** ADRs 0039 e 0040. Trata a hipótese H2 de [arquitetura.md](../arquitetura.md).

## Contexto

Com a barra elevada (ADR 0040), o primeiro plano foi concedido em todas as aberturas do teste das 12:58–12:59. O usuário relatou que "inicialmente ficou perfeito, mas após um período a Steam voltou a ter o conflito". Os logs mostram por quê (**observado**):

- **A Steam escolhe o layout do controle pelo foco, com atraso.** Ela só passou para a configuração de desktop (`OnFocusWindowChanged ... Desktop, AppID 413080`) 2,6 s, 0,7 s e 4 s depois de a barra ganhar o foco nas aberturas das 12:59:03, 12:59:08 e 12:59:16. Nas duas primeiras aberturas (12:58:44 e 12:58:46) foi em menos de 1 s: depois do primeiro fechamento o foco caiu numa janela do shell (explorer, HWND 65950), e a Steam já estava em desktop.
- **No intervalo, o Big Picture continua navegando.** O primeiro comando depois de cada abertura gera em `webhelper_js.txt` o aviso `SteamUI: WARNING: No active context; finding one` (12:59:04, 12:59:08, 12:59:18). A interface da Steam procura um contexto e processa o controle mesmo sem foco, enquanto o layout ativo for o do Big Picture (AppID 769).
- **O PS também é da Steam.** O menu lateral do Big Picture abriu às 12:59:06 por causa do PS que abriu a barra, e fechou com o PS seguinte (12:59:09). A Steam carrega o layout de atalhos do Guia (AppID 443510) a cada aperto.
- **O retorno do foco ao Big Picture foi recusado** com a barra elevada (`aceito=False`). O foco caiu na janela do shell e só voltou à Steam meio segundo depois, ou nem voltou.

Não há como acelerar de fora a detecção de foco da Steam. A Steam aceita o protocolo `steam://forceinputappid/<id>`, que fixa o layout de um app em qualquer janela, inclusive no Big Picture, e `steam://forceinputappid/0`, que libera. **O protocolo não é documentado pela Valve**; aparece em guias da comunidade.

Teste no desktop, autorizado pelo usuário, em 2026-10-25 às 13:04–13:05 (**observado**):

- `.../413080` foi executado em 0,5 a 1,2 s (`ExecuteSteamURL` no `console_log.txt`, `OnFocusWindowChanged URL Forcing ... 413080` no `controller_ui.txt`). O foco não mudou.
- `.../0` foi executado, mas a Steam **não reavaliou o layout** até a troca de foco seguinte. Um layout forçado (769) continuou ativo até uma janela de teste receber o foco às 13:05:24, quando a Steam voltou ao desktop.

## Decisão

Escolha do usuário: fixar o layout de desktop da Steam enquanto a barra abre, depois de testar no desktop.

1. **Ciclo curto, só com a barra na frente.** Logo depois de a barra ganhar o primeiro plano (na abertura, num aperto ou num clique), uma thread própria despacha `steam://forceinputappid/413080`. Ela espera a linha `ExecuteSteamURL` no `console_log.txt` da Steam, com limite de 2,5 s, e só então despacha `steam://forceinputappid/0`. A liberação acontece com a barra ainda na frente. Assim, o layout que sobra é justamente o de desktop, o certo para a barra, e a próxima troca de foco volta a escolher pelo app. Um ciclo por abertura.
2. **Nunca com outra janela na frente, nem sem a Steam.** Sem primeiro plano, a barra não força nada: liberar com o Big Picture na frente o deixaria preso no layout de desktop. Com `steam.exe` fechado, nada é despachado, porque o protocolo iniciaria a Steam.
3. **Ordem garantida.** A liberação só sai depois da confirmação ou do limite. Se a forçada aparecer no log depois da liberação, a liberação é repetida. Com o log ilegível, os limites fazem o papel de espera.
4. **Fechar no meio do ciclo.** Se o usuário fechar antes da liberação, a barra fica invisível (opacidade 0, sem receber cliques) e mantém o primeiro plano. O foco só volta ao app de trás depois da liberação, e reabrir durante a espera mostra a barra de novo. Sair do modo Xbox ou despachar outro destino oculta sem esperar.
5. **Recuperação.** `%LOCALAPPDATA%\XGB_P\settings\steam-input-forcado.txt` existe enquanto o ciclo não liberou. Ao encerrar, a barra cancela o ciclo e libera. Ao iniciar depois de um encerramento no meio, ela também libera.
6. **Retorno do foco.** Quando o pedido direto de foco for recusado, `DesktopWindows.FocusOnly` repete com a fila de entrada do destino ligada por um instante. Continua sem restaurar janela minimizada.
7. **Sem arquivos da Steam.** Nada é escrito em `config.vdf`, `localconfig.vdf` ou nos layouts; só os dois protocolos e a leitura do `console_log.txt`. O diagnóstico `--verify-steam-input-live` despacha o ciclo real e fica fora da bateria. Ele recusa quando o modo Xbox está ativo ou a Steam está na frente.

## Teste das 13:18: o PS devolve o controle à Steam

O usuário reabriu às 13:17:28 o build publicado às 12:56, **sem este ciclo** (o build novo não tinha sido publicado), e relatou que, "após ir testando, o foco volta novamente para a Steam". O que os logs mostram (**observado**):

- **O Windows nunca tirou o foco da barra.** Nas dez aberturas, entre 13:18:06 e 13:18:49, a barra ficou em primeiro plano do início ao fim (`foreground=655934` em todas as observações com ela aberta). Todos os retornos ao Big Picture foram aceitos (`aceito=True`).
- **A Steam passou para desktop no mesmo segundo de cada abertura** (`OnFocusWindowChanged ... Desktop`). A detecção própria dela foi rápida neste teste.
- **Em 7 das 10 aberturas, a Steam voltou ao Big Picture no PS que abriu a barra.** A sequência registrada é desktop → configuração do Guia (443510, enquanto o PS fica apertado) → Big Picture (769) ao soltar, com `OnFocusWindowChanged to ... ClientUI` no mesmo segundo. Em seguida vem `No active context; finding one` no primeiro comando. A partir daí, o Big Picture recebeu o controle **até a barra fechar**. Nas outras três aberturas (13:18:06, 13:18:13 e 13:18:25), a Steam voltou ao desktop ao soltar o PS e não houve conflito.
- **Não há configuração da Steam que desligue isso.** No `localconfig.vdf` (só leitura), "Guide button focuses Steam" (`Controller_CheckGuideButton`) e os atalhos do Guia (`SteamController_Enable_Chord`) já estão em `0`. A Steam lê o PS do controle virtual do DSX como botão Guia e, no Big Picture, o usa para retomar o controle (**inferido** da sequência acima).

Isso também explica o "atraso" do teste das 12:59. Na abertura das 12:59:03, a sequência foi 769 → 443510 → 769: o PS solto devolveu o Big Picture antes de a Steam registrar a troca de foco. O desktop só veio às 12:59:06.

Consequências para a solução:

- O ciclo desta ADR contorna o efeito: a forçada chega 0,5 a 1,2 s depois de abrir, depois do PS solto, e mantém o desktop até a barra fechar. **Sobra uma janela curta logo após a abertura.**
- **Sem brecha, só se a Steam não receber o PS.** O perfil do DSX (`Default Profile.dsx`, Mapping Lab, lido sem alterar) já usa o PS como modificador de atalhos de teclado com `muteInput`. Os códigos de saída são `0x4000` + código de tecla (**inferido**: dá atalhos coerentes, como Alt+Z, Win+Tab e Ctrl+Shift+S). Um dos atalhos existentes, PS com o botão de código 2 do DSX, gera **Win+G** (`0x5B`, `0x47`). Win+G chega à barra pela sonda e pela ponte da ADR 0037, sem passar pela Steam. Remapear o PS sozinho para Win+G, com `muteInput`, tiraria o PS do controle virtual. Seria uma mudança no DSX feita pelo usuário. A Steam perderia o botão Guia, a menos que outro atalho do DSX o gere.
- O log passa a registrar `entrada: PS solto | <ms>`, para relacionar a duração do toque com a volta da Steam.

## Consequências e limites

- **O conflito diminui, mas não some.** A Steam leva de 0,5 a 1,2 s para executar o protocolo, porque abre um processo `steam.exe` e passa o endereço ao cliente. A detecção própria dela levava até 5 s. Os primeiros comandos depois de abrir ainda podem chegar ao Big Picture.
- O PS que abre a barra continua sendo lido pela Steam. O menu lateral pode abrir por trás quando a liberação não chega antes de o PS ser solto. Com o layout de desktop ativo, o PS que fecha a barra não abriu o menu nos testes.
- Fechar nos primeiros ~2,5 s depois de abrir deixa a barra invisível e o controle sem resposta por até ~5 s, enquanto a Steam executa os dois protocolos.
- O layout de desktop desta máquina está vazio (`empty.vdf`, substituição local da Steam). Se o usuário configurar um layout de desktop com teclado ou mouse, a Steam passa a mandar essas teclas para a barra enquanto ela estiver aberta.
- Despachar um app ou um jogo pela barra nos primeiros segundos pode cair no meio do ciclo: o destino ganha o foco antes de a Steam liberar e fica no layout de desktop até a troca de foco seguinte. O log registra `liberado sem a barra na frente`.
- Às 13:14:19 a Steam desligou o controle por inatividade (`Turn off idle controller 0`), no mesmo segundo do teste real, 15 minutos depois do último uso. É o desligamento automático da Steam e não muda o ciclo.

## Evidências

- `bin/design-preview/preview.log`, 12:58:14–12:59:24: foco concedido, `aceito=False` no retorno e trocas de foco.
- `C:\Program Files (x86)\Steam\logs`: `controller_ui.txt` (trocas de layout, `URL Forcing`), `console_log.txt` (`ExecuteSteamURL`), `webhelper_js.txt` (`No active context`), `webhelper.txt` (menu lateral às 12:59:06/09), `controller.txt` (`Turn off idle controller` às 13:14:19).
- `bin/steam-input/design-preview/steam-input-real.txt`, às 13:14: desktop confirmado em 1209 ms e liberação em 1184 ms, com outro app na frente.
- Suíte: 23 verificações do ciclo (`steam-input-verificacao.txt`) e 50 de foco/Steam.

Fontes consultadas em 2026-10-25, não oficiais:

- [Steam Community: forceinputappid para usar o layout de um app em jogos, desktop e Big Picture](https://steamcommunity.com/discussions/forum/1/4849905062597228496)
- [Perfil com os comandos forceinputappid e o reset /0](https://steamcommunity.com/id/tintingaroo?l=romanian)
