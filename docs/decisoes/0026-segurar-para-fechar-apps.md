# 0026 — Segurar o ícone de um app aberto para mostrar o X de fechar

- **Status:** Aceita
- **Data:** 2026-10-24
- **Complementa:** ADR 0014 (alternador de janelas)

**Refino visual, 2026-10-25:** a barrinha espera 250 ms e preenche nos 1750 ms restantes. O X continua sendo acionado em 2 s. Toque curto e transferência limpam indicador/clock, sem lampejo inicial. Observação temporal e publicação.

## Contexto

O usuário pediu que, em "Aplicativos abertos", segurar o ícone de um app por 2 segundos mostre ao lado dele um botão X para fechar o programa.

## Decisão

- **Segurar 2 s** num ícone da faixa mostra um X logo depois dele.
  - Vale para ✕ no DualSense, Enter/espaço no teclado e o botão esquerdo do mouse.
  - Soltar antes de 2 s é o toque normal: abre o app, como antes.
  - Depois que o X aparece, soltar não faz nada.
  - A repetição automática do teclado não reabre o app durante o segurar.
- **O X:**
  - O foco vai para o X, que tem o padrão da faixa: 52 px, só ícone, dica e nome acessível "Fechar <app>". O título da seção mostra o nome em foco.
  - Ele ocupa o mesmo passo de 60 da faixa, então a rolagem por índice e as setas continuam exatas.
  - Esquerda volta ao ícone. Esc/○ ou focar outro controle recolhe o X.
  - Sobrevive às leituras da faixa a cada 2 s.
- **Progresso:** durante o segurar, uma barra verde de 3 px enche o pé do ícone em 2 s, de forma linear. Com movimento reduzido ou animações desativadas no Windows, não há barra, mas o X aparece igual.
- **Fechar:**
  - Selecionar o X envia o fechamento normal da janela (`WM_CLOSE`), como o botão fechar dela. O programa pode salvar ou pedir confirmação.
  - Antes, a identidade da janela é conferida (mesmo processo, criação e classe).
  - A barra continua aberta com "Fechando <app>…". A leitura seguinte confirma "<app> fechado".
  - Se a janela continuar aberta após 6 s, aparece "ainda está aberto; pode estar pedindo confirmação".
  - Janela de programa elevado: o Windows nega (UIPI), e a mensagem explica.
- **Alcance:** fecha a janela representada pelo ícone. Programas com várias janelas têm um ícone por janela, como na ADR 0014. Para jogos, a Home continua com o fechar com confirmação da ADR 0024.

## Correção relacionada (ADR 0024)

O teste real de pausa falhou de forma intermitente durante a publicação. **Observado:** depois de `NtSuspendProcess`, o `conhost` do teste tinha 2 de 3 threads em espera por suspensão. O terceiro ainda estava numa espera do kernel (`UserRequest`), porque a suspensão só é entregue quando o thread volta ao modo usuário.

O critério "todos os threads" podia mostrar um jogo pausado como em execução e permitir uma segunda suspensão. A correção:

- pausado = **maioria** dos threads suspensos (`GameControl.SuspendedShare`);
- a pausa feita pela própria prévia vale até a leitura mostrar zero threads suspensos (retomado por fora) ou o processo sair;
- o teste real espera até 1 s pelo estado e, mesmo se falhar, sempre retoma e encerra a árvore de teste.

O teste passou 7 vezes seguidas depois da correção. Os dois processos de teste que a falha deixou pausados (`ping` e `conhost`, filhos do `cmd` de teste já encerrado) foram encerrados depois de conferir a origem.

## Consequências

- Fechar um app pela faixa não pede confirmação adicional: o segurar de 2 s mais a seleção do X já são duas ações explícitas.
- O DualSense passa a abrir o app ao **soltar** ✕ (e não mais ao pressionar) apenas nos ícones da faixa. Nos outros controles, o comportamento não muda.

## Evidências

- A suíte confere:
  - segurar só nos ícones de apps, toque curto e X ao lado com foco;
  - passo de 60, soltar depois do X e X que sobrevive à leitura;
  - navegação entre ícone e X, fechar e confirmar pela leitura;
  - recolher por Esc e por foco, mouse, erro e janela que continua aberta.
- `--verify-apps-close-live`: a janela de teste criada pela própria verificação, fora da tela, é encontrada, fechada por `WM_CLOSE` e recusada na segunda tentativa. Apps do usuário não foram tocados.
- Render e relatórios.
- **Não comprovado:** segurar físico com o DualSense, apps reais pedindo para salvar e apps elevados.
