# 0045 — Apps abertos camuflados no modo Xbox

- **Status:** Aceita; implementada e verificada automaticamente, teste real no FSE pendente
- **Data:** 2026-10-25
- **Revisa:** o descarte de toda janela camuflada da ADR 0014; complementa as ADRs 0039 e 0044.

## Contexto

O usuário relatou que a faixa funciona no desktop, mas no modo Xbox mostra apenas o app em foco. A investigação confirmou que `EligibleReason` descartava qualquer `DWMWA_CLOAKED != 0`. O shell mantém os apps de trás com `DWM_CLOAKED_SHELL` (2), embora suas janelas continuem existindo e sendo enumeradas.

O log de 14:57 registra Explorer, Steam, Spotify, Firefox, Codex/ChatGPT, Claude e Configurações nessa condição. A lista diagnóstica antiga também trazia TextInputHost e duas representações de Configurações; ela interrompia os filtros ao encontrar camuflagem. Uma liberação indiscriminada incluiria componentes internos e duplicatas.

## Decisão

1. Cada consulta cria `AppWindowQuery`, com estado oficial do FSE e recursos COM próprios da thread. **Somente camuflagem exatamente 2** pode ser aceita, com `FSE == true` e confirmação de `IVirtualDesktopManager.IsWindowOnCurrentVirtualDesktop`. API/modo/desktop incertos não autorizam a exceção. Camuflagem pelo app, herdada ou combinada continua recusada. Janelas comuns e minimizadas mantêm a política existente.
2. A mesma regra serve à lista, disponibilidade, ativação, busca do Big Picture e escolha/retorno do foco. O backend consulta o modo em cada operação; não usa uma autorização persistente no snapshot. Uma leitura da Home de outro ciclo FSE é descartada.
3. Preservar filtros de visibilidade, ferramenta, owner, título, shell e processo próprio. Excluir CoreWindows internas de TextInputHost, ShellExperienceHost, StartMenuExperienceHost e SearchHost apenas nos diretórios de sistema do Windows. Não substituir janelas por processos em execução.
4. Preferir `ApplicationFrameWindow` a `Windows.UI.Core.CoreWindow` quando ambas têm o mesmo AppUserModelID não vazio. Ler `System.AppUserModel.ID` da janela e, no conteúdo CoreWindow, usar a identidade do processo como fallback. Não agrupar janelas comuns por título/PID ou AppUserModelID: múltiplas janelas legítimas continuam distintas. Identidade desconhecida não é prova de duplicação.
5. Não escrever `DWMWA_CLOAK`, estilos, tamanho, posição ou desktop de outros apps. Seleção explícita continua solicitando restauração, se minimizada, e foreground. Confirmar somente foreground da janela/root owner com janela da frente visível e sem camuflagem; listar um app não prova que o Windows aceitou a troca.
6. `Exists` distingue identidade viva, destruída/substituída e incerta, independentemente da elegibilidade visual. Após pedido de fechar, sumir da faixa não confirma saída. Falha de leitura/identidade termina sem sucesso presumido; manter `WM_CLOSE`, nunca matar o processo.

## Consequências

Sem pacote, certificado, serviço, tarefa, configuração de HidHide/DSX/Steam, saída HID ou ativação automática do FSE. O usuário continua confirmando UAC e entrando no modo Xbox. A ferramenta segue como prévia WPF independente.

A inclusão depende das APIs públicas identificarem a janela e o desktop atual. Isso não promete equivalência absoluta à barra de tarefas ou a todos os apps empacotados. A ativação real de janelas camufladas no FSE ainda exige teste do usuário; as restrições de foco do Windows continuam válidas.

## Evidências

- Resultados, build e limites e continuidade.
- [DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute), [desktop virtual atual](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ivirtualdesktopmanager-iswindowoncurrentvirtualdesktop), [AppUserModelID](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-appusermodel-id).
