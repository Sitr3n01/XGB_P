# 0052 — Teclado virtual controlado pelo DualSense

- **Status:** Aceita; aguarda teste físico no modo Xbox
- **Data:** 2026-10-26
- **Complementa:** ADRs 0023 (camada), 0036 (controle), 0037 (ativação), 0040–0044 (foco e Steam Input) e 0046 (entrega de foco).

## Contexto

O usuário pediu um teclado virtual parecido com o teclado de toque do Windows 11, completo e com atalhos do Windows, porque o do Windows não funciona com o DualSense. Os requisitos:

- abrir sozinho quando o Windows identificar uma caixa de texto, **só com o modo Xbox ativo**;
- ser controlado pelo DualSense;
- ter um atalho no Acesso rápido da Home;
- respeitar as camadas existentes: aberto pelo atalho, a barra se oculta sozinha.

**Por que o teclado do Windows não serve** (**inferido**): ele usa o controle pelo Windows.Gaming.Input, que reconhece controles Xbox. O DSX emula um DualSense, sem XInput ([arquitetura](../arquitetura.md), item 3).

Fatos que definiram o desenho (**observado**):

- O layout de desktop da Steam nesta máquina está vazio (`empty.vdf`, ADR 0041). Com um app comum na frente, o controle não chega ao app pela Steam.
- Sobre o Big Picture ou um jogo da Steam, o layout do app navega junto.
- A Steam registra o layout escolhido em `controller_ui.txt`, como `OnFocusWindowChanged to window type: …, AppID N` (769 é o Big Picture). Isso foi lido sem alterar nada, nesta sessão.
- `launch.json` está com `AlwaysAdmin: false`. Steam, DSX e o `steamwebhelper` rodam elevados (ADR 0040).
- O layout de teclado do Windows é pt-BR ABNT (0416). O Windows enxerga sete teclados conectados, então o teclado de toque dele não abre sozinho por falta de teclado físico.

## Decisão

**Revisão visual posterior:** a [ADR 0053](0053-design-do-teclado-baseado-no-windows.md) substitui o estilo das teclas e o foco verde do item 8 por um desenho baseado na referência do Windows, aprovado antes da implementação. Também move fechar ao cabeçalho e acrescenta a engrenagem para a seção existente de Configurações. As políticas de envio, abertura, suspensão, integridade e Steam Input abaixo permanecem.

Escolha do usuário: implementar o plano proposto, com as recomendações padrão para as decisões em aberto.

1. **Janela sem foco.**
   - O teclado é uma janela própria, sempre por cima, com `WS_EX_NOACTIVATE` e `WS_EX_TOOLWINDOW`, e responde `MA_NOACTIVATE` ao clique.
   - O campo de texto continua com o foco, e o shell do modo Xbox não tem motivo para esconder o app.
   - As teclas vão ao app da frente pelo `SendInput` (`src/lib/input/TextInput.cs`). Texto sai como Unicode, sem tecla morta e independente do layout. Atalhos, setas e teclas de função saem como tecla virtual, com o prefixo estendido onde é preciso.
   - Cada toque é um único lote, com modificadores, tecla e soltura em ordem inversa. Nenhum modificador fica apertado entre chamadas.
2. **Controle.** Com o teclado visível e a barra fechada, todo botão menos o PS é do teclado, sem precisar de foco. O PS segue para a barra. PS segurado com outro botão é atalho do DSX e não vira tecla. O mapeamento segue o teclado do Windows:

   | DualSense | Ação |
   |---|---|
   | Direcional / analógico esquerdo | Andar entre as teclas, com repetição; a coluna é mantida na vertical |
   | ✕ | Teclar; **segurar** abre os acentos (e o número nas teclas q–p) |
   | ○ | Fechar a lista de acentos ou o teclado |
   | □ / △ / Options | Backspace (repete) / Espaço / Enter |
   | L1 / R1 | ← / → (repetem) |
   | L2 | Letras ↔ símbolos (duas páginas) |
   | L3 | Shift de um uso; dois toques: Caps |
   | R2 segurado | Ctrl |
   | R3 | Teclado em cima ↔ embaixo |
   | Analógico direito | Setas do cursor; com Shift, seleção |
   | Microfone | Ditado do Windows (Win+H) |
   | Create | Painel de emoji (Win+.) |
   | Touchpad | Letras ↔ Completo (F1–F12, Ins/Del/Home/End/PgUp/PgDn, PrtSc, Menu, setas) |

   - A faixa de cima tem desfazer, recortar, copiar, colar, selecionar tudo, histórico da área de transferência (Win+V), emoji, Completo e fechar.
   - Ctrl, Win e Alt na tela são de um uso; Win duas vezes manda a tecla Windows sozinha.
   - O Caps vale só para letras e não mexe no Caps Lock do sistema.
3. **Abrir pelo Acesso rápido** (7º atalho, "Teclado", depois de Downloads).
   - A barra se oculta pelo caminho existente: `Dismiss` → `Hide(true)`, com retorno de foco por identidade e espera da liberação da Steam.
   - O teclado aparece quando o app de trás volta à frente, com limite de 6 s. Se outro app estiver na frente no limite, ele abre sobre esse app.
   - Sem app de trás, a barra fica aberta e avisa.
   - A bandeja ganhou o item "Teclado virtual".
4. **Abrir sozinho, só no modo Xbox.** Revisado pela [ADR 0087](0087-teclado-automatico-so-com-modo-xbox-estavel.md): o modo é lido na hora e falha fechado; nos 3 s depois de entrar no modo Xbox o automático não abre, e o campo que já tinha foco no desktop conta como fechado.
   - `src/lib/windows/TextFocus.cs` usa ganchos WinEvent de primeiro plano e de foco, fora do processo e sem injeção. Eles disparam uma leitura UI Automation numa thread MTA, só do elemento em foco.
   - Abre quando todas estas condições valem:
     - a opção está ligada;
     - o FSE está confirmado pela API;
     - a barra está fechada;
     - o elemento é editável (Edit; Document com valor ou cursor editável; ComboBox editável);
     - a janela está na frente;
     - o app não está excluído;
     - o campo não foi fechado pelo usuário.
   - Ficam de fora a Steam (inclusive jogos em `steamapps`), o DSX, a própria barra, TextInputHost, LockApp, LogonUI, consent e caminhos ilegíveis. Esses processos nem recebem consulta UIA, porque o Chromium/CEF liga a acessibilidade quando é consultado.
   - O teclado automático segue o foco: fecha 400 ms depois de o foco sair do campo e troca de janela com o campo.
   - O teclado manual fica até o ○ e acompanha o app da frente, inclusive depois de um Alt+Tab mandado por ele.
   - Fechar num campo impede a reabertura ali até o foco ir para outro elemento.
   - O detector só roda quando é necessário: no modo Xbox com a opção ligada, ou com o teclado aberto ou pendente.
5. **Camadas.**
   - A ordem é barra (com foco) > teclado (por cima, sem foco) > app.
   - Abrir a barra suspende o teclado sem animação. Quando ela devolve o foco ao mesmo app, com limite de 1,8 s, o teclado volta no mesmo estado. Com outro app na frente, a suspensão expira.
   - Mudar o modo Xbox fecha o teclado.
6. **Steam Input sobre apps da Steam** (manual, porque o automático exclui a Steam).
   - Antes de fixar, o teclado lê o último layout no `controller_ui.txt`, ignorando o 443510 do Guia. Depois fixa o desktop (`forceinputappid/413080`) e fica apagado, aceitando só o ○, até a Steam confirmar ou até 3 s, como na ADR 0043.
   - Ao fechar com o app na frente, ele **força de volta o layout lembrado** e depois libera com `/0`, porque a liberação sozinha só vale na próxima troca de foco (ADR 0041). Se a linha do Big Picture ainda não estiver no log, usa o 769.
   - Com a barra abrindo por cima ou com o foco já em outro app, ele só libera.
   - O marcador `steam-input-teclado.txt` serve de recuperação após encerramento.
7. **Integridade.**
   - Antes de enviar, o teclado confere se o app da frente é o destino e se a integridade dele não está acima da barra. Se o token for ilegível, presume administrador só para apps da Steam.
   - Fora disso, nada é enviado e o status mostra o aviso para ligar "Sempre executar como administrador".
   - Com a Steam acima da barra, o layout dela não é preso.
8. **Posição e visual.**
   - O teclado fica centralizado no monitor do app, embaixo. Fica em cima quando, embaixo, cobriria o campo (retângulo do UIA).
   - A escala é reduzida só quando o teclado não cabe na largura.
   - Paleta, foco verde e cantos são os da barra, com o botão do DualSense em cada tecla. Entrada e saída animadas respeitam "Reduzir movimento".
   - Abrir toca `lista` e fechar pelo usuário toca `voltar`. As teclas tocam `selecionar`/`mover` só com "Sons das teclas" ligado.
9. **Configurações → TECLADO VIRTUAL:** "Abrir sozinho no modo Xbox" (ligado por padrão), "Posição do teclado" (automática/embaixo/em cima) e "Sons das teclas". Ficam salvas em `%LOCALAPPDATA%\XGB_P\settings\keyboard.json`, com gravação atômica e backup.
10. **Privacidade.** O log registra tipo de controle, editável/senha, executável e HWND. Nunca registra conteúdo, nome ou valor do campo, nem o que foi digitado; só a contagem de toques ao fechar. Campo de senha aparece no status.

## Consequências e limites

- **Não comprovado no hardware:**
  - leitura UIA em apps reais no modo Xbox;
  - `SendInput` real;
  - janela sem foco com o shell do modo Xbox;
  - restauração do layout da Steam forçando o ID lembrado, que é **inferida** do teste das 13:04 da ADR 0041.
- A verificação substitui tudo isso. O diagnóstico real `--verify-keyboard-live` leu o UIA do app da frente, sem ganchos e sem envio.
- **Com a barra sem elevação, não há como digitar** em apps elevados (Steam/Big Picture, DSX, terminais de administrador): o UIPI descarta o `SendInput` em silêncio. O teclado avisa em vez de digitar no vazio.
- Campos desenhados pelo jogo (DirectX) não aparecem no UIA. Nesses casos, o caminho é o atalho da Home. Jogos que leem o HID do DualSense direto, fora da Steam, recebem os botões também, como na H2.
- Jogos em tela cheia exclusiva podem não mostrar uma janela por cima (H1).
- Elementos que mudam de foco sozinhos podem abrir o teclado automático; a opção desliga isso.
- O Windows não aceita Ctrl+Alt+Del nem Win+L por `SendInput`.
- Win+G mandado pelo teclado abre a própria barra (pela sonda), e o teclado fica suspenso.
- A primeira consulta UIA a um app Chromium/Electron liga a acessibilidade dele, o que tem um custo pequeno. A Steam fica de fora.
- Se o teclado do Windows também abrir (por toque), quem desliga é o usuário, em Configurações → Hora e idioma → Digitação → Teclado virtual.

## Evidências

- Bateria completa (`scripts/verificar-previa.ps1`) aprovada em 2026-10-26, das 16:25:52 (build) às 16:26:52 (último diagnóstico), com SHA-256 `B2C7F2518C2C6188BC87A45E80C46D71629844181CB59892738A5EE8F5C7E89B` em `bin/verificacao`:
  - **89 verificações novas** do teclado;
  - a suíte funcional e a temporal;
  - os diagnósticos reais, incluindo `--verify-keyboard-live`, com saída 0.
- Publicado fechado em `bin/XgbDesignPreview.exe` às 17:20:44, com backup do build anterior e smoke somente leitura com saída 0 (continuidade).
- Renders e relatórios.
- `C:\Program Files (x86)\Steam\logs\controller_ui.txt`, lido sem alterar: formato das linhas de layout (769, 413080, 443510).
- Registro `HKCU\Software\Microsoft\TabletTip\1.7` e `Win32_Keyboard`, lidos sem alterar: teclado de toque do Windows configurado no padrão e sete teclados conectados.

Fontes oficiais consultadas:

- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput): UIPI descarta em silêncio; `KEYEVENTF_UNICODE`.
- [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook): `WINEVENT_OUTOFCONTEXT` sem DLL no processo alvo.
- [UI Automation e acessibilidade](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview): limites de integridade de clientes UIA.
- [Estilos estendidos de janela](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles): `WS_EX_NOACTIVATE`.
