# 0018 — Faixa de status no estilo do guia do Xbox

- **Status:** Aceita
- **Data:** 2026-10-23

**Revisões de 2026-10-26:** a bateria passou à política física/validade e ao novo carregamento da [ADR 0051](0051-bateria-fisica-do-dualsense.md), que substitui a seleção e os estados de bateria descritos abaixo. Por pedido posterior do usuário, o vetor Ethernet foi refinado com cantos suaves, contatos curtos e um pequeno cabo central, preservando tamanho, alinhamento, estados, dicas e nomes acessíveis.

## Contexto

A faixa do topo da prévia mostrava o estado do controle só como texto ("DualSense · aguardando" ou "HID · 100% · completa"), com a hora em 23 px ao lado. O usuário pediu, com uma captura do guia do Xbox como referência:

- um ícone do DualSense com o indicativo de bateria;
- a hora em escala maior, na fonte padrão do projeto;
- um indicativo de Wi-Fi ou de rede Ethernet.

No guia do Xbox, os ícones de estado ficam no canto superior direito, junto da hora grande.

A primeira versão desta decisão punha os ícones numa linha **acima** da hora. O usuário achou a posição estranha e pediu os ícones **à esquerda da hora, justificados com a linha baixa do relógio**. A decisão abaixo já é a revisada.

## Decisão

- **Layout:** no canto superior direito, numa só linha, ficam os ícones (rede, controle, bateria) e, à direita deles, a hora em **46 px SemiBold, Segoe UI** (a fonte da janela).
  - **O pé de cada ícone fica na linha de base dos dígitos**, como letras numa linha.
  - A margem de baixo dos ícones é a altura da linha da hora menos a linha de base, calculada com `FormattedText` na fonte e no tamanho do próprio `TextBlock`. Ela acompanha qualquer mudança de fonte.
  - Os ícones ficam 20 px antes da hora e têm escala de 1,15, para manter a proporção da referência.
  - O bloco não invade o painel: a verificação confere que ele termina antes do trilho.
  - Não há texto de estado na faixa. O texto completo (por exemplo, "HID · 85% · descarregando") fica na dica, no nome acessível e no painel Controle e energia.
- **Ícones em vetor**, no traço dos ícones do trilho, sem depender de glifos de fonte:

  | Ícone | Estados |
  |---|---|
  | DualSense de frente (empunhaduras, touchpad, analógicos, direcional e botões de ação) | branco com controle; cinza e apagado enquanto nenhum controle informou a bateria |
  | Bateria ao lado do DualSense | preenchimento proporcional à carga; ≤ 15% sem carregar em `Critical`; raio quando o cabo está ligado (carregando ou completa); contorno `Critical` com "!" nos erros de tensão, temperatura ou carga |
  | Rede | porta RJ45 na Ethernet; três ondas no Wi-Fi e no celular, acesas conforme as barras de sinal (0–5 → 0–3 ondas); selo `Critical` com "!" quando conectado sem internet; ondas apagadas e traço diagonal sem conexão; oculto se a leitura falhar |

- **Bateria:** vem do relatório de entrada HID, como antes (ADR 0004, só leitura). Cada dispositivo, físico ou virtual do DSX, guarda a própria leitura.
  - Vale o último que informou.
  - Quando a leitura de um dispositivo para ("leitura parou"), a bateria dele sai. Se não restar nenhum, o ícone volta a "aguardando".
  - A antiga indicação "última leitura", depois de 5 minutos, saiu: a bateria só é publicada quando muda, então o aviso aparecia mesmo com o controle ativo.
- **Rede:** `Windows.Networking.Connectivity.NetworkInformation`, a API pública do Windows, só leitura.
  - Lida em worker a cada 5 s, só com a barra visível, e logo ao abrir.
  - O perfil de internet vem primeiro; sem ele, vale o primeiro perfil com acesso local.
  - **O SSID não é lido**: no Windows 11 24H2 a leitura dele exige permissão de localização. A dica mostra só o tipo, o sinal e se há internet.
- **Cor nova:** `Critical` `#FF99A4`, o vermelho de alerta do tema escuro do Fluent, para bateria baixa, erro e falta de internet.
- **Diagnóstico:** `--verify-status-live` lê a rede e, por até 4 s, a bateria pelo HID (entrada apenas), e gera `status-real.png` e `status-real.txt`.

## Consequências

- O topo deixa de mostrar a porcentagem da bateria. A carga se lê pelo preenchimento; o número fica na dica e no painel Controle e energia.
- A bateria continua sendo o relato do dispositivo lido (em geral o virtual do DSX), não uma medição própria do controle físico.
- O build passa a referenciar `Windows.Networking.winmd`, dos metadados do próprio Windows, e inclui `src/lib/network/NetworkStatus.cs`.
- Uma rede só local, por exemplo um adaptador virtual do Hyper-V, aparece como Ethernet sem internet quando não há outra conexão. Esse caso não foi refinado.

## Evidências

- **`--verify` com estados controlados:**
  - hora em ≥ 44 px na Segoe UI, sem invadir o painel;
  - ícones à esquerda da hora; o pé da linha e o de cada ícone (rede, DualSense, bateria) a até 1 px da linha de base dos dígitos;
  - controle aguardando, 85% (largura do preenchimento conferida), 15% em `Critical`, carregando com raio, erro com "!";
  - troca para outro dispositivo e perda do último;
  - Ethernet, Wi-Fi com 3 e 5 barras, sem internet, sem conexão e leitura ilegível.
  - Renders `faixa-status.png` (Ethernet e 85%) e `faixa-status-wifi.png` (Wi-Fi com 3 barras e carregando).
- **`--verify-status-live` neste PC (2026-10-23, 21:20 e 21:26, após a revisão do layout):** `ethernet com internet | IANA 6` e `DualSense#0-USB | HID · 100% · completa`. O render mostra a porta Ethernet, o DualSense com a bateria cheia e o raio, todos apoiados na linha de base da hora.
- Fatos técnicos da rede em [rede-windows.md](../referencia/rede-windows.md).
