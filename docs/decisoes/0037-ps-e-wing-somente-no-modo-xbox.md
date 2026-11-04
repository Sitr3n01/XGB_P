# 0037 — PS e Win+G somente no modo Xbox/FSE

- **Status:** Aceita; o momento de abrir pelo PS foi revisado pela ADR 0042 (ao soltar, quando só o controle físico recebe o PS). A [ADR 0048](0048-wing-opcional-no-desktop.md) permite Win+G no desktop por opção desligada por padrão; PS permanece restrito ao FSE.
- **Data:** 2026-10-25
- **Revisa:** gesto das ADRs 0005/0007 e abertura manual das ADRs 0008/0036.

## Contexto

O usuário pediu PS do DualSense e Win+G como abertura/fechamento, exclusivamente no modo Xbox/Full Screen Experience do Windows. No desktop, preservar Steam/Windows e não abrir a interface do projeto. O estado do Big Picture, um jogo em tela cheia ou a presença do AnyFSE não substituem o estado FSE.

`RegisterHotKey(MOD_WIN | MOD_NOREPEAT, G)` foi sondado por uma chamada temporária, sem enviar teclas: falhou com **1409**, pois o Windows já possui o atalho. Trocar apenas as flags de Win+Shift+G não implementaria Win+G nesta máquina.

## Decisão

- Autorizar por `GamingExperience.Active()` **igual a `true`**. `false`, `null` ou exceção mantêm o projeto inativo. Notificação oficial e consulta de conferência detectam entrada/saída; cada ativação consulta novamente, sem confiar só no cache.
- Inicializar na bandeja, mesmo ao abrir manualmente. Entrar no FSE não abre automaticamente a barra: aguarda PS/Win+G. Sem processo em execução, essa implementação não cria um residente nem um mecanismo novo de inicialização.
- PS abre/fecha no **aperto**, uma vez por aperto. Segurar e soltar não repetem. **Se a barra está visível, o próximo acionamento fecha mesmo que ela tenha perdido foco.** Microfone curto/longo e Create não têm ação no XGB_P. Manter navegação/analógicos e parada de foco na Biblioteca.
- Usar no FSE uma **ponte temporária de leitura do log da sonda já instalada**: apenas novos eventos `args(1)=ms-gamebar://hotkey/?ihkid=IHKID_GAME_GUIDE_OVERLAY...` acionam Win+G. Sem captura de teclado, hooks, bloqueio, reenvio ou síntese de teclas. Não reinstalar o pacote só para essa integração da ferramenta independente.
- A ponte só existe enquanto o FSE estiver confirmado. Remover ao sair ou perder a API; ocultar a barra e cancelar repetição/segurar. A decisão não altera a pausa que o usuário escolheu para jogos. Não alterar Steam/DSX/HidHide nem filtrar seus relatórios.
- Ignorar histórico ao iniciar, eventos antigos/duplicados, dicas/capturas e comandos de controle do shell (evita duplicar o PS lido pelo HID). Validar formato/URI e limitar leitura/linha; tratar linha parcial e truncamento. Consulta a cada 100 ms, com janela de frescor de cinco segundos e geração da sessão FSE para descartar callbacks antigos.
- Remover o registro Win+Shift+G. Bandeja, teclado, mouse e controle não podem abrir/operar a barra normal no desktop. Modos explícitos de verificação/inspeção usam fixtures, sem teclado/HID reais.
- A sonda instalada continua recebendo protocolos e gravando diagnóstico, sem IPC com esta UI. Como o observador não consome Win+G, silenciar o aviso visual da sonda pelo mecanismo existente `%USERPROFILE%\GameBarProbe\silencioso.txt`; preservar arquivo existente ou criar apenas se ausente. Isso não modifica/reinstala o pacote. Logs continuam e a remoção desse marcador reativa os avisos.

## Consequências e limites

`Activation.cs` centraliza política e ciclo de vida. `src/lib/windows/ProbeActivations.cs` implementa parser, acompanhamento incremental e ponte; é integração temporária da ferramenta independente, a ser substituída por IPC no ativador final. `ModeWatcher.StateChanged` inclui perda da API, preservando o evento booleano antigo. `ActivationVerification.cs` verifica matriz, callbacks e arquivos isolados; `--verify-activation-live` somente lê o estado real.

**Correção após teste físico:** a primeira implementação por `WH_KEYBOARD_LL` recebeu só parte dos Win+G que a sonda confirmou, sem causa exata comprovada, e foi retirada. O filtro que trazia a barra visível sem foco para frente impedia a alternância esperada pelo usuário; foi substituído por alternância baseada na visibilidade. Os testes agora cobrem explicitamente perda de foco e o caminho real de eventos Windows → log da sonda → interface.

O PS continua sendo lido em compartilhamento. Esta entrega não bloqueia o comportamento que Steam/DSX possam aplicar ao mesmo botão no FSE; coexistência e foco precisam de teste real. Não instala AnyFSE, não entra/sai do modo Xbox por conta própria e não integra o ativador empacotado ao núcleo futuro. O pacote com identidade da Microsoft continua estritamente local.

## Fontes

- [Microsoft: API de estado e notificação FSE](https://learn.microsoft.com/en-us/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers?view=gdk-2604).
- [Microsoft: RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey): atalhos com Win reservados ao sistema e conflito de registro.
- [Microsoft: LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc): callback no processo instalador, encaminhamento e estado assíncrono ainda não atualizado no callback.

Consultados em 2026-10-25. Sem uso de REA nesta revisão. Continuidade e evidências.
