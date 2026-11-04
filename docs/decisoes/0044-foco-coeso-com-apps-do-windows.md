# 0044 — Foco coeso com apps do Windows no modo Xbox

- **Status:** Aceita; aguarda teste físico no modo Xbox
- **Data:** 2026-10-25
- **Complementa:** ADRs 0039, 0040, 0042 e 0043. Revisa a regra "sem simular teclas" da ADR 0040 num caso único.

## Contexto

Com a Steam resolvida ("na Steam está perfeito"), o usuário testou a barra sobre programas da área de trabalho no modo Xbox, como Configurações, e relatou problemas com o foco. Os logs de 14:33–14:38 mostram (**observado**):

1. **Apps da Store recusam a ligação da fila.** Sobre o Configurações (janela `ApplicationFrameHost`, PID 23412), todas as tentativas retornaram `fila nao anexada (erro 5)`, mesmo com a barra elevada (14:34:52, 14:35:15, 14:35:19, 14:35:33). O controle seguiu no Configurações até um clique. A mensagem do log atribuía o erro à integridade, o que não vale aqui. **Inferido:** o Windows não deixa ligar a fila de entrada à janela de apps da Store.
2. **Espera da Steam sem necessidade.** Com o Configurações na frente, a Steam carregou 443510 → 413080 no PS, sem voltar ao Big Picture (14:34:52, 14:35:04, 14:35:15–14:35:28). Mesmo assim, a barra esperou cerca de 1,1 s pela Steam.
3. **Atalhos PS + botão do DSX abriam a barra.**
   - Às 14:35:23, o Quadrado veio 7 ms antes do PS (o Win+Tab do DSX): a barra não viu o atalho e abriu sobre a Visão de Tarefas.
   - Às 14:35:29, 14:35:34 e 14:35:35, com o PS também no virtual, a barra abriu no aperto, e o Quadrado chegou 5 a 12 ms depois.
   - O DSX solta o PS virtual em 5 a 28 ms quando reconhece um atalho, e manda toques de 1 a 28 ms no fim.
4. **Retorno para janelas que não são apps.**
   - A Visão de Tarefas (`explorer`) foi guardada como app de trás. Ela fecha ao perder o foco, e o retorno falhou. O foco caiu numa janela vazia do shell (HWND 65952), guardada depois como app de trás, com retorno recusado (14:35:27, 14:35:32).
   - Às 14:35:04, a barra oculta ainda era a janela da frente, e nenhum app de trás foi guardado.
5. **"Essa janela já foi fechada" ao alternar para o Configurações** (14:35:10); a segunda tentativa funcionou. O log não dizia qual janela nem o motivo.
6. **No modo Xbox, o shell esconde os apps de trás.** Às 14:34:50, ao abrir o Configurações, o Big Picture ficou `camuflada=shell` e deixou de ser encontrado. Esses apps somem de Apps abertos.

Fatores previstos sem teste: jogos em tela cheia costumam minimizar ao perder o foco, e o retorno não restaurava janela minimizada (ADR 0039).

## Decisão

Escolhas do usuário: tecla neutra só quando o Windows recusar o foco, e investigar antes de listar as janelas escondidas pelo shell.

1. **Foco com Alt neutro, só como último recurso.** Primeiro, o pedido direto; depois, a fila ligada. Se a fila for recusada, a barra envia Alt apertado, a tecla `0xE8` (sem uso) apertada e solta, e Alt solto. A tecla do meio impede que soltar o Alt ative a barra de menus. A documentação de `LockSetForegroundWindow` diz que o sistema volta a permitir `SetForegroundWindow` quando o usuário aperta Alt. Que um Alt simulado tenha o mesmo efeito é **inferido** e precisa ser confirmado no teste. O app da frente recebe esse Alt e pode mostrar as dicas de atalho por um instante. O log passa a registrar `fila nao anexada (erro N)` e, conforme o resultado, `concedido com Alt neutro` ou `negado mesmo com Alt neutro (N de 4 teclas)`.
2. **Espera da Steam só com app da Steam atrás.** O ciclo e a entrada lenta das ADRs 0041/0043 só valem se o app de trás for da Steam: um executável na pasta da Steam ou em `\steamapps\common\`. Se o app de trás for desconhecido, valem por precaução.
3. **Atalhos do DSX.**
   - Qualquer botão (exceto PS e analógicos) já apertado quando o PS desce torna o toque um atalho.
   - Com a Steam vendo o PS, a barra decide 40 ms depois do aperto, no tick do controle, em vez de no aperto. Um botão nesse intervalo, ou o DSX soltar o PS virtual em menos de 30 ms com o físico apertado, vira atalho.
   - Toques de menos de 30 ms não abrem nem fecham, nas duas vias.
4. **Destino do retorno.** Quando a janela da frente na abertura não é um app (Visão de Tarefas, janela sem título do shell, a própria barra oculta), o destino é o primeiro app de cima para baixo, pela mesma regra de Apps abertos. O log registra `janela: frente nao e app | ...; retorno para ...`.
5. **Restaurar só o que minimizou com a barra na frente.** Se o app de trás não estava minimizado na abertura e está no fechamento (mesmo processo e mesma criação), ele é restaurado e recebe o foco. Uma janela já minimizada antes da barra continua sem restauração.
6. **Diagnóstico.**
   - A cada abertura no modo Xbox, quando a lista muda, `janela: escondidas pelo shell | ...` registra os apps com `DWM_CLOAKED_SHELL`, com HWND, executável e título.
   - Em Apps abertos, `janelas: indisponivel | <chave> <app> | <motivo>` registra por que a janela foi recusada.

## Consequências e limites

- O Alt neutro é entrada simulada, enviada ao app da frente. Só ocorre depois de a fila ser recusada, ou seja, nos apps da Store e em apps elevados acima da barra. Com a barra sem elevação diante de um app elevado, o isolamento de integridade (UIPI) pode descartar as teclas, e o aviso de elevação da ADR 0040 continua.
- A decisão de 40 ms atrasa um pouco a abertura quando a Steam vê o PS. A espera da ADR 0043 cobre esse tempo. Silenciar o PS sozinho no DSX elimina os dois atrasos.
- Toques reais de menos de 30 ms são descartados; nos testes, o menor toque humano foi de 68 ms.
- Listar no modo Xbox os apps escondidos pelo shell continua pendente, à espera dos dados do novo log.
- Restaurar ao fechar usa `SW_RESTORE`, que respeita o estado maximizado anterior. Não força geometria.

## Evidências

- `bin/design-preview/preview.log`, 14:32:50–14:38:32; `%USERPROFILE%\GameBarProbe\logs\dualsense.log` (eventos dos dois dispositivos, 14:35:09–14:35:37); `controller_ui.txt` da Steam (443510 → 413080 com o Configurações na frente).
- Processo do Configurações: `C:\WINDOWS\system32\ApplicationFrameHost.exe -Embedding` (PID 23412), lido depois do teste.
- Suíte: 57 verificações de foco/Steam, 72 de ativação e 34 do ciclo da Steam. O caminho do Alt neutro não roda nos testes, que substituem o pedido de foco.
- [LockSetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-locksetforegroundwindow): o sistema libera `SetForegroundWindow` quando o usuário aperta Alt.
