# 0079 — DSX pela bandeja, sem segunda instância

- **Status:** Aceita; implementada e publicada em `.exe` para teste (build 15554DEF). Teste físico pendente.
- **Data:** 2026-10-29

## Contexto

Em Configurações → CONTROLE, "DSX · ajustes do controle" procurava o DSX só entre as janelas visíveis (ADR 0028). Sem achar, abria `steam://rungameid/1812620`. O usuário relatou que o DSX está sempre rodando na bandeja, porque opera em tempo real, e que a barra tentava abri-lo pela Steam.

**Observado em 29/10, somente leitura:**
- O DSX 24864 (WPF, .NET 9) tem a janela principal `HwndWrapper[DSX;;…]`, título "DSX", **minimizada e oculta**. Os hosts de bandeja `wpfui_th_*` são do Wpf.Ui.
- O log do DSX registra "Setting Window State based on user settings. MINIMIZED".
- O lançador da Steam (`DSX\DSX.exe`, `DSX_Launcher.dll`) abre `Main_v3_Beta\DSX.exe` por uma tarefa agendada e depois sai.

**Comportamento do DSX 3.2 ao abrir uma segunda instância:**
- Sem argumentos, ela avisa que o DSX já está rodando e, conforme a resposta, pode encerrar a instância residente. Isso derruba o controle virtual em uso.
- Com argumentos, os comandos vão para a instância residente pelo canal de comandos do DSX, que só aceita comandos de perfil e de dispositivo. Não existe um comando "mostrar janela".

A análise detalhada que sustenta essas observações fica no arquivo privado do projeto.

## Decisão

1. **Visível:** a janela do DSX vem para a frente pelo fluxo normal de alternância de apps, como antes.
2. **Na bandeja:** encontrar a janela de topo sem dono, com barra de título e título, do processo `Main_v3_Beta\DSX.exe`. Pedir **restaurar** (`ShowWindowAsync`), o mesmo efeito do ícone da bandeja. Esperar até 1,5 s ela aparecer e alternar para ela.
   - Sem mensagens privadas do Wpf.Ui, cliques simulados ou injeção.
   - Se a janela não aparecer, o aviso `Attention` depende da elevação da barra:
     - sem elevação (o DSX roda como administrador): ligar "Sempre executar como administrador";
     - com elevação: abrir pelo ícone da bandeja.
3. **Steam só com o DSX fechado:**
   - processo DSX sem janela (lançador ou início): "O DSX está abrindo";
   - processo sem caminho legível ou leitura impossível: erro, e a Steam **nunca** é chamada;
   - só sem nenhum processo DSX da Steam é que a barra abre `steam://rungameid/1812620`.

## Consequências e limites

- A restauração usa o mesmo pedido do Windows que o ícone da bandeja. O DSX é WPF e não foi testado restaurado por outro processo: o estado interno de visibilidade dele pode divergir. O teste físico vai dizer se a janela responde normalmente e se volta à bandeja ao minimizar.
- Entre processos de níveis diferentes, o Windows recusa o pedido. Por isso a barra precisa da opção de administrador (ADR 0040), e o aviso diz isso.
- O DSX, a Steam, os perfis e o HidHide não foram alterados.

## Evidências

Evidências, `configuracoes-extras-verificacao.txt`. As 11 verificações novas do DSX cobrem:
- janela visível;
- janela na bandeja restaurada;
- elevação ausente e restauração recusada;
- processo sem janela e sem caminho;
- DSX fechado, falhas de leitura e erro da Steam;
- lançador separado do programa.
