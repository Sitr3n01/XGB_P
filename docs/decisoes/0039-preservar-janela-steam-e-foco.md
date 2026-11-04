# 0039 — Preservar a janela da Steam e o foco

- **Status:** Aceita; navegação de Início revisada pela ADR 0058 (27/10): reutiliza a janela e pede explicitamente a Home. As demais políticas e o histórico abaixo permanecem.
- **Data:** 2026-10-25
- **Complementa:** ADRs 0011, 0014 e 0037.

## Contexto

O usuário relatou Big Picture menor e sem bordas no FSE. Os logs da Steam registram interface desktop às 11:07:05, recriação do Big Picture às 11:07:59 e escala interna de 1,42 para 1,03. A criação em 1280×800 aparece também antes da falha e, sozinha, não prova o tamanho final. A investigação posterior leu uma janela minimizada cuja posição normal de restauração mede 1296×839 no monitor 1920×1080. Não houve reprodução controlada da causa.

O botão Início sempre enviava `steam://open/bigpicture`; o fechamento não devolvia explicitamente o foco. O estado do PS compartilhava o dicionário da navegação, apagado ao abrir/ocultar/perder foco. O usuário autorizou corrigir esses caminhos e instrumentar a janela, sem forçar escala ou resolução.

## Decisão

- Início procura uma janela visível/minimizada com título Steam/Big Picture e executável pertencente à instalação Steam registrada. Se disponível, reutiliza a janela através do alternador existente, inclusive sua confirmação de foreground e timeout de 900 ms. Mantém a página atual do Big Picture. Só despacha o protocolo se não encontrar janela disponível; erro de identificação ou ambiguidade não equivale a ausência.
- Biblioteca, Downloads e Configurações continuam enviando suas rotas explícitas. Reutilizar uma janela já pequena não corrige sua posição salva pela Steam.
- Capturar a janela anterior somente ao passar de oculta para visível, com HWND, PID, criação do processo e classe. Fechar por PS/Win+G, Voltar ou clique externo devolve apenas o foco se a barra ainda estiver em primeiro plano e a identidade continuar válida. Não restaurar uma janela que o usuário minimizou, nem tomar foco de outra aplicação escolhida durante a abertura. Despachar outro destino ou sair do FSE oculta sem devolver foco ao jogo anterior.
- Ler geometria, posição normal, comando de exibição, monitor, DPI e foreground antes de abrir/ocultar e a cada 500 ms enquanto visível; continuar por dois segundos após ocultar. Gravar apenas mudanças, além dos marcos. `ocupaMonitor` é aproximação geométrica; não representa confirmação de fullscreen exclusivo. Coordenadas `GetWindowRect` dependem do contexto DPI do observador; não confundir o retângulo de uma janela minimizada com sua posição normal.
- Separar o PS pressionado por dispositivo do estado transitório da navegação. Só um novo aperto após as solturas gera outra ação; limpar na mudança de modo/desconexão. Soltura anterior ao corte temporal da navegação ainda libera o PS. Gestos de segurar não são novos apertos.
- Retirar o caminho legado `WM_HOTKEY`, sem registro ativo. Win+G vem somente da ponte de novos eventos da sonda. Preservar a deduplicação por identidade do evento e a rejeição de `gamepadCommand`, que evita duplicar PS do shell. Não usar janela temporal arbitrária para fundir dois acionamentos distintos.
- Não escrever tamanho, posição, DPI, resolução, configuração Steam/DSX/HidHide ou relatórios de saída HID. O retorno de foco não usa `ShowWindow`; a escolha explícita de um aplicativo minimizado preserva o mecanismo existente de restauração do Windows.

## Consequências e limites

`DesktopWindowState.cs` observa janelas e valida a origem do Big Picture sem extrair ícones na UI. `WindowFocus.cs` integra captura/retorno e diagnóstico; `WindowFocusVerification.cs` testa com comandos/foco substituídos e uma janela própria. A barra continua estritamente condicionada ao FSE.

Não há bloqueio dos inputs que a Steam também lê. A coexistência física do PS e o gatilho original da redução ainda precisam de validação pelo usuário. Não entrar no FSE nem reconfigurar a Steam automaticamente. Uma janela reduzida já persistida precisa voltar à tela cheia pelo próprio cliente Steam para validar a prevenção de recorrência.

## Evidências

Passagem consolidada · [referência factual da janela/escala](../referencia/janela-steam-e-escala.md).

Relatórios, build e reversão. Suíte funcional, temporal, testes em processos/janelas próprios e leituras reais seguras aprovados. Sem REA: alteração no código-fonte do projeto e consulta a APIs públicas.

Fontes oficiais consultadas em 2026-10-25:

- [GetWindowPlacement](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement): estado e posição normal da janela.
- [GetWindowRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect): coordenadas virtualizadas por DPI e limites de interpretação.
- [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow): pedido de foco sujeito à autorização do Windows.
