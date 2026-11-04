# 0017 — Desempenho funcional com HWiNFO, RTSS e modo de energia

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

O painel Desempenho da prévia mostrava valores fixos de exemplo. O usuário, que se descreve como entusiasta e já usa o HWiNFO, pediu um painel funcional com:

- uso, temperatura, clock e consumo dos componentes;
- frequência e latência (CL) da memória, VRAM, FPS "e derivados";
- um seletor do **modo de energia do Windows**, com as mesmas três opções de Configurações > Energia.

O botão "Detalhes da sessão" podia sair. Também pediu dois ajustes de texto:

- remover a dica "Padrão para jogos, mídia e chamadas" das listas de saída e microfone;
- trocar "Início do Big Picture" por "Início" na Home.

## Decisão

- **Sensores pelo HWiNFO, só leitura:** a memória compartilhada `Global\HWiNFO_SENS_SM2`, que o próprio HWiNFO publica quando "Shared Memory Support" está ligado. Ela é lida a cada ~1 s em worker, só com o painel visível.
  - O layout é lido do cabeçalho (offsets, tamanhos e contagens), sem tamanhos fixos.
  - Os rótulos variam por fabricante, então cada campo tem uma lista de rótulos aceitos, em ordem de preferência. A verificação cobre rótulos de AMD, Intel e NVIDIA.
  - Ligar o compartilhamento é uma configuração do HWiNFO, **feita pelo usuário**.
- **FPS pelo RTSS, só leitura:** a memória compartilhada `RTSSSharedMemoryV2` do RivaTuner Statistics Server.
  - Vale o app que estava em primeiro plano antes de abrir a barra; se ele não estiver sendo medido, vale o app com mais FPS.
  - A própria prévia é ignorada, e um jogo que parou de apresentar quadros deixa de contar.
  - Se o HWiNFO publicar um rótulo "Framerate", ele serve de reserva.
  - O sensor PresentMon do HWiNFO **não é usado**: ele segue o app em primeiro plano, que com a barra aberta é a própria barra.
  - O XGB_P não injeta nada: quem mede é o RTSS, instalado pelo usuário.
- **Reserva do Windows, sem o HWiNFO:**

  | Dado | Fonte |
  |---|---|
  | Nome da CPU | registro |
  | Uso da CPU | `GetSystemTimes` |
  | RAM | `GlobalMemoryStatusEx` |
  | GPU dedicada e VRAM total | registro do driver de vídeo |
  | Taxa da memória (MT/s) | WMI `Win32_PhysicalMemory`, lido uma vez |

  O que depende do HWiNFO fica "—", e o rodapé do painel explica como ativar o compartilhamento.
- **Layout:** seletor de modo no topo, depois quatro cartões com quatro colunas alinhadas. Em cada coluna, o número fica grande e a unidade menor e apagada. Cabem sem rolagem.

  | Cartão | Colunas | Cabeçalho e linhas extras |
  |---|---|---|
  | CPU | uso, temperatura, clock médio dos núcleos (o mesmo do Gerenciador de Tarefas), consumo | barra de uso; pico, clock efetivo e tensão do núcleo |
  | GPU | uso, temperatura, clock, consumo | barra de uso; VRAM com %, clock e temperatura da junção, com barra; hotspot, tensão do núcleo e ventoinha |
  | Memória | em uso, carga, MT/s com o clock real, CL | timings CL-RCD-RP-RAS e command rate no cabeçalho; FCLK, UCLK:MCLK, tRFC e temperatura dos DIMMs |
  | FPS | quadros/s, frametime | app e fonte |

  O rodapé indica a fonte dos sensores.
- **Modo de energia:** o seletor abre uma lista flutuante, igual à de dispositivos do Som, com o foco preso na lista e ○/Esc ou clique fora para fechar. As opções são "Melhor eficiência de energia", "Equilibrado" e "Desempenho Máximo".
  - **API:** `PowerGetUserConfiguredACPowerMode` e `PowerSetUserConfiguredACPowerMode` (ou as variantes DC, na bateria). São funções públicas de `powrprof.h` (SDK 10.0.26100, Windows 11). Os GUIDs são os de `winnt.h`.
  - **Quando aplica:** só quando o usuário escolhe uma opção, e escolher o modo atual não reaplica.
  - **Fora do plano Equilibrado:** o seletor fica inativo, como no Windows.
  - O XGB_P não usa as funções `*OverlayScheme`, que não estão nos cabeçalhos.
- **Textos:** a dica das listas sai. Na Home, o botão mostra só "Início"; a dica e o nome acessível continuam "Início do Big Picture". "Detalhes da sessão" sai do Desempenho.

## Consequências

- **Sem compartilhamento ligado no HWiNFO**, temperatura, clocks, consumo, VRAM em uso e timings ficam "—".
- **Limite de 12 h:** na versão gratuita, o HWiNFO desliga o compartilhamento depois de 12 horas e ele precisa ser reativado. O painel avisa quando isso acontece (assinatura `DEAD`).
- **FPS:** sem o RTSS, não há FPS. A medição própria sem injeção continua sendo o spike S5 do produto. Esta prévia não o conclui.
- **Modo de energia:** a mudança vale para o Windows inteiro e persiste, como em Configurações. Para desfazer, basta escolher o modo anterior no painel ou em Configurações > Sistema > Energia.
- **Build:** passa a referenciar também `System.Management.dll`, do .NET Framework.

## Evidências

- **Sondagem só leitura de 2026-10-23 neste PC:**
  - HWiNFO64 aberto, sem a memória compartilhada (`FileNotFound`);
  - RTSS aberto, com `RTSSSharedMemoryV2` legível (assinatura `RTSS`, versão `0x00020015`, 256 entradas, nenhuma com quadros no momento);
  - plano ativo Equilibrado (`381b4222-…`) e modo configurado para AC Desempenho Máximo (`ded574b5-…`), igual ao `ActiveOverlayAcPowerScheme` do registro.
- **Diagnóstico `--verify-perf-live`, que não muda o modo:** AMD Ryzen 7 7700X, AMD Radeon RX 9070 XT com 15,9 GB, RAM 31,1 GB a 6000 MT/s e modo Desempenho Máximo.
- **`--verify` com dados controlados:**
  - valores formatados e ausência de rolagem;
  - lista flutuante sem mover os cartões e com o foco preso;
  - aplicação só pela escolha, com erro tratado;
  - estado sem HWiNFO e estado fora do plano Equilibrado;
  - mapeamento de rótulos AMD, Intel e NVIDIA;
  - rótulos reais AM5/RDNA4, com as armadilhas `Total Board Power (TBP) Limit`, `GPU Hot Spot Temperature (Max)`, `GPU Memory Usage` em MB e PresentMon.
- **Rótulos reais (20:57, depois que o usuário ligou o compartilhamento no HWiNFO 8.29):**
  - 349 leituras; todos os campos do painel preenchidos, exceto FPS (sem jogo).
  - Ajustes: tensão pela SVI3 (`CPU VDDCR_VDD Voltage`), TBP sem o limite, hotspot sem o máximo, junção da VRAM, tensão da GPU, FCLK/UCLK, tRFC e DIMMs.
  - **Clock da CPU (pedido do usuário, comparando com o Gerenciador de Tarefas):** o efetivo (1,3–1,9 GHz) dava a impressão de clock baixo. O número principal passou a ser a média dos núcleos (5,28–5,39 GHz, contra 5,30 GHz no Gerenciador), e o efetivo virou detalhe.
  - Render real conferido, sem rolagem. Detalhes em [desempenho-windows.md](../referencia/desempenho-windows.md#hwinfo-memória-compartilhada).
- **Ainda não validado:** FPS com um jogo rodando (depende do usuário abrir um jogo com o RTSS) e a escrita do modo de energia (depende de uma escolha do usuário).
- Fatos técnicos em [desempenho-windows.md](../referencia/desempenho-windows.md).
