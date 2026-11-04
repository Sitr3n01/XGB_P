# Referência — fechar, pausar e interpretar acesso negado

Análise somente leitura em 26/10/2026, solicitada pelo usuário antes das correções de UX. Fontes do próprio projeto e documentação pública; REA não foi utilizado.

## Observado no projeto

- `src/lib/games/GameControl.cs`, `Denied`: código Win32 5 ou NTSTATUS `0xC0000022` recebe a mensagem genérica sobre administrador/anti-cheat. Não há detecção da causa ou consulta ao anti-cheat.
- `Close` envia `WM_CLOSE` com `PostMessage`; `Kill` abre `PROCESS_TERMINATE` e chama `TerminateProcess`; `Suspend`/`Resume` usam `PROCESS_SUSPEND_RESUME` e `NtSuspendProcess`/`NtResumeProcess`.
- A UI pede fechamento normal primeiro; jogo pausado é retomado antes. Sem saída após 10 s, oferece forçar, com confirmação separada. A [ADR 0050](../decisoes/0050-ocultar-barra-ao-fechar-jogo.md) só acrescenta ocultação após pedido aceito.
- Log `bin/design-preview/preview.log`: em 24/10, às 13:33:47, 13:46:08 e 13:46:30, Wuthering Waves recusou fechar/pausar. A execução da prévia começou às 13:31:29 com `administrador=False`. O log não capturou o nível de integridade do jogo naquele instante nem uma resposta identificada do anti-cheat.
- O usuário posteriormente confirmou que o fechamento funciona. Em 26/10, pedido de fechamento às 13:13:47 e confirmação às 13:13:52 constam no log, antes da publicação da ocultação.

## O que as fontes técnicas sustentam

- [`PostMessage`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew) está sujeito a UIPI; mensagem bloqueada por essa política retorna acesso negado, código 5. Portanto, esse erro sozinho não identifica interferência de anti-cheat.
- [`WM_CLOSE`](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-close) é pedido à janela; o app pode apresentar confirmação ou tratar a saída. Não garante salvamento nem término do processo.
- [`TerminateProcess`](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess) causa encerramento incondicional, requer permissão específica e é assíncrono quando chamado de outro processo. Seu retorno não substitui observar a saída.
- [Direitos de processo](https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights) distinguem consulta, encerramento e suspensão/retomada. Suspender externamente não equivale ao menu de pausa do jogo.
- O [aviso do Nyrna](https://github.com/Merrit/nyrna#disclaimer) reconhece possibilidade de crash e recomenda salvar antes. A técnica não fornece garantia universal de compatibilidade com anti-cheats.

## Inferências e limites

A diferença de privilégio é hipótese compatível com as recusas antigas, mas não foi comprovada para aquele processo de jogo. Não foi demonstrado banimento, violação detectada ou bloqueio causado por anti-cheat. Não há garantia de segurança da suspensão externa em jogos online/protegidos; a recomendação da análise foi usar a pausa nativa nesses casos e restringir conscientemente suspensão externa a jogos offline sem anti-cheat confirmado.

Não foi implementada uma nova política de bloqueio de pausa, remoção de forçar ou alteração dessa mensagem genérica. Nenhum jogo real foi operado pelo agente para investigar a hipótese. Consolidação da sessão.
