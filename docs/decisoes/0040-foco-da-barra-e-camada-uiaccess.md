# 0040 — Foco da barra no modo Xbox e teste da camada uiAccess

- **Status:** Aceita; revisada no mesmo dia pelo teste das 12:48 (barra como administrador; uiAccess fica como reserva, não instalada)
- **Data:** 2026-10-25
- **Complementa:** ADRs 0023, 0037 e 0039. Trata as hipóteses H1 e H2 de [arquitetura.md](../arquitetura.md).

## Contexto

O usuário pediu que, ao abrir o painel no modo Xbox, a barra sempre receba o foco, com o jogo ou a Steam visível ao fundo e sem que o controle continue indo para eles. Isso vale para tela cheia, janela sem borda ou qualquer outro modo. No teste de 2026-10-25, entre 12:16 e 12:17, com o Big Picture aberto pelo AnyFSE, foram observados três sintomas:

1. **A barra não recebia o controle.** Nas cinco aberturas por PS (12:16:54 a 12:17:14), a janela em primeiro plano continuou sendo o Big Picture (`foreground=133986`, PID 24212), e não houve nenhuma linha `entrada:` no log (**observado**). O `Show()` chamava `Activate()` e `SetForegroundWindow`, mas o Windows só concede o primeiro plano ao processo que recebeu a última entrada de teclado ou mouse. A leitura HID do controle não conta como entrada (**documentado**, ver SetForegroundWindow). A navegação exige o primeiro plano (`ControllerFocused`), por isso os botões eram ignorados pela barra e ficavam só com a Steam.
2. **O fundo sumia ao clicar.** O clique deu o primeiro plano à barra (`foreground=461404`, às 12:17:16.97). No mesmo instante, `FindSteamBigPicture` passou a responder `ausente`, embora a observação direta do mesmo HWND mostrasse a janela visível, sem minimizar e ocupando o monitor (**observado**). Esse filtro só descarta janelas camufladas pelo DWM, então o Big Picture foi camuflado (**inferido**). A Microsoft documenta que no modo Xbox "só uma janela é exibida por vez". Quando a barra vira a janela da frente, o shell esconde a de trás, e pela transparência aparece o preto.
3. **O controle agia nos dois.** A Steam só registrou a troca de foco às 12:17:20 (`OnFocusWindowChanged ... k_nGameIDControllerConfigs_Desktop`), cerca de 3 s depois, e então carregou a configuração de desktop, que está vazia (`empty.vdf`). Nesse intervalo a barra já navegava (`LS_BAIXO`/`LS_CIMA` às 12:17:17 e 12:17:18), e o Big Picture também atualizava a interface (**observado** nos logs da Steam). Cada aperto do PS também carrega na Steam a configuração do Guia (AppID 443510): a Steam lê o PS por conta própria.

A Game Bar oficial evita os dois primeiros sintomas com uma camada privilegiada do shell (`CreateWindowInBand`/`OverlayApp`), que exige um token da Microsoft (ver componentes-windows.md). Uma janela comum que toma o primeiro plano no modo Xbox faz o shell esconder a anterior; uma janela que não toma deixa o controle com a Steam ou o jogo.

## Decisão

Escolhas do usuário nesta sessão: primeiro plano por `AttachThreadInput`, fundo ao vivo tentado pela variante uiAccess, e nenhuma ação sobre o jogo por enquanto (só o foco, medindo depois quanto da entrada ainda vaza).

1. **Primeiro plano na abertura.** `DesktopWindows.TakeForeground` faz o pedido direto e, se o Windows negar, liga por um instante a fila de entrada da thread da barra à do app da frente (`AttachThreadInput`), repete o pedido com `BringWindowToTop`/`SetForegroundWindow` e desliga a ligação em seguida. Não simula teclas nem mouse e não muda configurações do sistema. Cada tentativa vai para o log: `foco: barra | <motivo> | <resultado>; antes=<hwnd>; foreground=<hwnd>`.
2. **Nova tentativa pelo controle.** Se a barra estiver aberta sem o foco e ainda não o tiver recebido nesta abertura, um aperto pede o foco de novo, no máximo a cada 500 ms. Se o pedido for concedido, o mesmo aperto já navega. Depois que a barra tiver o foco uma vez, uma troca posterior para outra janela é respeitada, como na ADR 0039.
3. **Janela de ferramenta.** A camada recebe `WS_EX_TOOLWINDOW` logo após criar o handle. Ela já ficava fora da barra de tarefas; agora sai também do Alt+Tab e da Visão de tarefas. Não há garantia de que isso evite a camuflagem do shell: é um dado a mais para o mesmo teste, sem instalação.
4. **Diagnóstico de camuflagem.** `WindowObservation` passa a ler `DWMWA_CLOAKED` e as linhas `janela:` mostram `camuflada=nao|app|shell|herdada`. Assim o próximo teste confirma ou descarta a inferência do sintoma 2 e mostra quem escondeu a janela.
5. **Variante uiAccess (experimento).** O mesmo código-fonte é compilado com `uiAccess="true"` por `validar-design.ps1 -UiAccess`, em `bin\uiaccess`. O Windows só abre esse executável quando ele está assinado, com o certificado numa raiz confiável e numa pasta protegida. `scripts\instalar-previa-uiaccess.ps1`, **executado pelo usuário como administrador**, faz isto:
   - cria um certificado autoassinado de assinatura de código (30 dias), assina uma cópia e apaga a chave privada;
   - põe só o certificado público em `LocalMachine\Root`;
   - copia o executável para `C:\Program Files\XGB_P`.
   `scripts\remover-previa-uiaccess.ps1` desfaz tudo isso. A variante instalada grava os logs em `%LOCALAPPDATA%\XGB_P\design-preview`, porque não tem permissão de escrita em Arquivos de Programas. O log de início mostra `uiAccess=True|False`.
6. **Sem bloqueio de entrada.** Continua sem saída HID, sem HidHide/DSX/Steam reconfigurados e sem pausa automática de jogo.

## Revisão pelo teste das 12:48–12:49

O usuário testou o build publicado às 12:46:36 no modo Xbox, sobre o Big Picture, e relatou que a barra abriu com a entrada ainda na Steam e que o conflito continuou.

- **O foco continuou negado porque a Steam roda elevada.** Todas as tentativas, na abertura e nos apertos, retornaram `negado; fila nao anexada (erro 5)` (**observado**). Às 12:50, a leitura do token mostrou integridade alta (`0x3000`) para `steam.exe` (PID 24092) e para o `DSX.exe`. O `steamwebhelper` dono da janela do Big Picture (PID 24212) recusou a leitura do token a um processo comum, compatível com o mesmo nível. A barra rodava sem elevação (`administrador=False`, integridade média) (**observado**). O Windows não deixa um processo de integridade média ligar a fila de entrada a um processo de integridade alta (UIPI) (**inferido** do erro 5 e dos níveis lidos).
- **O fundo foi preservado.** Com a barra em primeiro plano depois de um clique (`foreground=1706834`, às 12:48:49.6 e 12:49:09.5), o Big Picture ficou `camuflada=nao` e continuou visível (**observado** no log e confirmado pelo usuário). No teste anterior, sem `WS_EX_TOOLWINDOW`, ele era camuflado. A janela de ferramenta resolve o fundo neste cenário, e a variante uiAccess deixa de ser necessária para isso.
- **Atraso da Steam medido:** a troca para a configuração de desktop veio às 12:48:53, 3,4 s depois da barra ganhar o foco. Na segunda abertura, veio em menos de 1 s, no mesmo segundo do log (**observado**). Nesse intervalo o Big Picture também recebe o controle.

**Decisão revisada, escolhida pelo usuário:** a barra roda como administrador, pela preferência existente “Sempre executar como administrador” (ADR 0033), com o UAC confirmado pelo usuário a cada abertura. Com a mesma integridade da Steam e do DSX, a ligação da fila é permitida. Quando o pedido falhar com erro 5 e a barra não estiver elevada, o log explica o motivo (`app da frente com integridade maior`). A barra também mostra, uma vez por abertura, o aviso “A Steam ou o jogo roda como administrador…”, que indica a opção. A variante uiAccess continua compilável e com scripts, mas não é o caminho atual.

## Consequências e limites

- **Validado no modo Xbox só em parte:** o fundo preservado sobre o Big Picture e o motivo da negação do foco. O foco com a barra elevada e o comportamento sobre jogos ainda não foram testados. As verificações automáticas substituem o foco. O teste nativo não pede foco com a fila anexada, para não roubar o foco do usuário durante a bateria.
- Se, sobre algum jogo, o shell voltar a esconder a janela de trás, a barra terá o controle, mas o fundo será preto. Nesse caso o fundo ao vivo depende da variante uiAccess, e não há garantia de que o modo Xbox respeite a camada uiAccess. A Microsoft orienta não usar uiAccess só para aparecer acima de outros apps: aqui é um teste local, que não deve ser distribuído. Se o experimento falhar, a alternativa já levantada é o quadro congelado (capturar a tela antes de abrir).
- `AttachThreadInput` falha contra uma janela elevada quando a barra não está elevada (observado às 12:48), e pode falhar contra uma janela travada; o resultado fica no log. Barra elevada e jogo comum é o caso permitido (de integridade maior para menor). A variante uiAccess talvez passasse pelo isolamento de integridade (UIPI) sem UAC (**não comprovado**).
- A Steam leva alguns segundos para notar a troca de foco e lê o PS sozinha. Os primeiros comandos depois de abrir ainda podem chegar aos dois. Jogos que leem o controle direto (XInput do Xbox 360 virtual do ViGEm, ou HID) podem ignorar o foco. Isso fica para medir no teste.
- Com a barra em primeiro plano, o Big Picture camuflado não passa no filtro de janelas disponíveis. Assim, Início volta a usar `steam://open/bigpicture` em vez de reutilizar a janela (ADR 0039). O protocolo é seguro, mas não preserva a página atual.
- Cada novo build da variante uiAccess precisa ser instalado de novo pelo usuário, porque a chave privada é apagada depois de assinar. A reinstalação tira de `Root` os certificados das instalações anteriores.

## Teste físico pedido ao usuário

O primeiro roteiro (build normal e, se o fundo sumisse, a variante uiAccess) foi feito às 12:48: o fundo ficou e o foco falhou pela elevação. Roteiro atual:

1. Abrir a barra como administrador e confirmar o UAC. O log deve mostrar `administrador=True`.
2. No modo Xbox, abrir por PS sobre o Big Picture e, depois, sobre um jogo. Conferir se o controle vai para a barra sem clicar e se o fundo continua visível. No log, ver `foco: barra | abertura | concedido…`.
3. Para não depender do UAC manual, ligar “Sempre executar como administrador” em Configurações.
4. Registrar se a Steam ou o jogo ainda reagem ao controle com a barra aberta, e por quanto tempo.

## Evidências

- `bin/design-preview/preview.log`, das 12:16:24 às 12:17:27: aberturas sem foco, primeiro plano após o clique, `Big Picture | ausente` e saída do modo Xbox pelo usuário às 12:17:26.
- `C:\Program Files (x86)\Steam\logs\controller_ui.txt` e `controller.txt`: configuração 443510 a cada PS, troca para desktop/413080 às 12:17:20. `webhelper.txt`: atividade da interface às 12:17:17.
- `%USERPROFILE%\GameBarProbe\logs\probe.log`: o PS do DualSense não gera ativação `ms-gamebar://` do shell. Logo, o caminho de ativação do pacote não dá o foco para o PS.
- Suíte funcional: 46 verificações de foco/Steam (antes 31) no build das 12:46 e 50 no das 12:56, com o aviso de elevação. Bateria completa e hashes em estado da máquina.
- Teste das 12:48–12:49 em `bin/design-preview/preview.log`: `erro 5` em todas as tentativas, `camuflada=nao` com a barra em primeiro plano. `controller_ui.txt`: troca para desktop às 12:48:53 e às 12:49:09. Integridade dos processos lida às 12:50 (`steam.exe`/`DSX.exe` em `0x3000`).

Fontes oficiais consultadas em 2026-10-25:

- [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow): condições para tomar o primeiro plano.
- [Windows Gaming: Full screen experience](https://support.microsoft.com/en-US/servicing/os/windows/docs/2025/10/windows-gaming-full-screen-experience): uma janela exibida por vez.
- [Security Considerations for Assistive Technologies](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview): requisitos e uso esperado do uiAccess.
- [The dangers of attaching input queues](https://devblogs.microsoft.com/oldnewthing/20080801-00/?p=21393): riscos de `AttachThreadInput`, mitigados com a ligação curta.
