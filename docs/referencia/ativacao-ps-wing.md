# Referência — ativação PS/Win+G por estado FSE

**Revisão de produto às 17:34 (ADR 0048):** opção persistida permite Win+G também com desktop confirmado; inicia desligada. PS HID continua restrito ao FSE. Reutiliza a ponte da sonda, sem hook/novo hotkey; eventos Win+G gerados por remapeadores são indistinguíveis do atalho. As observações abaixo são históricas. Implementação, testes e publicação; teste físico da nova opção pendente.

## Observado nesta máquina (2026-10-25)

- `ModoXboxMonitor.exe 0`, **10:48:18**: API suportada, FSE inativo, permissão de alternância disponível. Consulta apenas; não houve alternância.
- `RegisterHotKey(NULL, 22600, MOD_WIN | MOD_NOREPEAT, G)` falhou com erro Win32 **1409**. O programa de sonda liberaria o registro imediatamente em caso de sucesso; nenhuma tecla foi enviada e nada persistiu.
- `Microsoft.XboxGamingOverlay` local versão **99.0.0.0** continua instalado. A sonda recebe protocolos do shell e grava `GameBarProbe/logs/probe.log`; não encaminha comandos à ferramenta WPF atual.
- O marcador de silêncio da sonda estava ausente. Foi criado vazio na publicação, **11:03:11**, pelo mecanismo `File.Exists(QuietFlag)` já presente em `probe/src/Program.cs`. Isso suprime o aviso visual, preservando o log, sem tocar no pacote instalado.

## Base documental

- [API oficial FSE](https://learn.microsoft.com/en-us/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers?view=gdk-2604): consultar `IsGamingFullScreenExperienceActive`, registrar notificações e consultar novamente quando notificado.
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey): atalhos Win são reservados ao sistema; combinação já registrada pode impedir novo registro.
- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc): callback no contexto do processo que instala, não injetado no processo alvo; encaminhar a cadeia e não usar o estado assíncrono da tecla modificada dentro do callback.

Consultados em 2026-10-25. Sem REA nem modificação/análise de novos binários nesta revisão. Decisão: [ADR 0037](../decisoes/0037-ps-e-wing-somente-no-modo-xbox.md).

## Limites

Na primeira implementação, Win+G foi observado na interface às 11:07:13, mas vários acionamentos chegaram à sonda sem chegar à interface. A causa exata no hook não foi comprovada. O hook foi retirado; agora o evento já registrado pela sonda é lido incrementalmente, com parser restrito e descarte de histórico/duplicatas. A ponte não substitui IPC do ativador final.

O shell continua acionando a sonda; o marcador evita um segundo aviso visual. A política não bloqueia PS na Steam nem modifica DSX/HidHide. Testes com modo/log substituídos não provam foreground, ausência de resposta simultânea da Steam ou funcionamento com todos os privilégios/jogos.
