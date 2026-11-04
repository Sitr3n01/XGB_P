# Desempenho: HWiNFO, RTSS, modo de energia e reservas do Windows

A implementação fica em `src/lib/hardware/Performance.cs` e é usada pela prévia em `src/app/PerfPanel.cs` (ADR 0017). Foi escrita em C# 5, para .NET Framework x64.

Não há análise nem modificação de binários, então não há endereço ou ID do REA. Os layouts vêm da documentação pública do HWiNFO e do SDK do RTSS e foram conferidos contra o que este PC expõe, como indicado em cada caso.

## HWiNFO: memória compartilhada

| Item | Valor |
|---|---|
| Objeto | `Global\HWiNFO_SENS_SM2`, aberto só para leitura (`MemoryMappedFile.OpenExisting` com `MemoryMappedFileRights.Read`) |
| Pré-requisito | HWiNFO rodando com **Shared Memory Support** ligado. Segundo o anúncio do autor no fórum do HWiNFO, a opção passou a vir desligada e, na versão gratuita, desliga sozinha após 12 h |
| Assinatura | `0x53695748` ("HWiS") ativa; "DEAD" quando o HWiNFO desligou o compartilhamento |

Cabeçalho, com `#pragma pack(1)`:

| Offset | Campo |
|---|---|
| 0 | assinatura |
| 4 | versão |
| 8 | revisão |
| 12 | `poll_time` (`__time64_t`, UTC) |
| 20 / 24 / 28 | offset, tamanho e número de elementos de **sensor** |
| 32 / 36 / 40 | offset, tamanho e número de elementos de **leitura** |

- **Sensor:** `dwSensorID` (+0), `dwSensorInst` (+4), nome original ANSI de 128 bytes (+8) e nome do usuário (+136).
- **Leitura:**

  | Offset | Campo |
  |---|---|
  | +0 | tipo |
  | +4 | índice do sensor |
  | +8 | ID |
  | +12 | rótulo original (128 bytes) |
  | +140 | rótulo do usuário (128 bytes) |
  | +268 | unidade (16 bytes) |
  | +284 / +292 / +300 / +308 | valor, mínimo, máximo e média (`double`) |

  Tipos: 1 temperatura, 2 tensão, 3 ventoinha, 4 corrente, 5 potência, 6 clock, 7 uso, 8 outro.
- **Tamanhos:** o leitor usa os do cabeçalho, que podem crescer com campos UTF-8 nas versões novas. Ele só confia nos campos até +316.
- **Rótulos:** o mapeamento usa o rótulo **original**, então renomear um sensor no HWiNFO não quebra o painel.

**Observado em 2026-10-23, antes de ligar a opção:** HWiNFO64 aberto e o objeto inexistente (`FileNotFoundException`), porque o compartilhamento estava desligado.

**Observado em 2026-10-23, 20:57, com a opção ligada pelo usuário:**
- HWiNFO64 v8.29, a instância de `C:\Program Files\HWiNFO Portable`. Memória compartilhada versão 2, revisão 1, com **349 leituras**.
- Hardware: Ryzen 7 7700X em placa GIGABYTE B650M AORUS ELITE AX, com Super I/O ITE IT8689E; RX 9070 XT ASRock Taichi OC; iGPU Radeon 610M; 2 × DDR5 Kingston KF556C36-16.
- Despejo completo: `bin\design-preview\hwinfo-real.txt` (`--verify-perf-live`).

Mapeamento atual, coberto por testes com dados controlados (AM4/RDNA3, Intel/NVIDIA e os rótulos reais AM5/RDNA4 desta máquina). A coluna "Observado aqui" mostra o rótulo que de fato casou.

| Campo | Rótulos aceitos, em ordem de preferência | Observado aqui |
|---|---|---|
| Uso da CPU | `Total CPU Usage` | `Total CPU Usage` |
| Temperatura da CPU | `CPU (Tctl/Tdie)`, `CPU Package`, `CPU (Tdie)`, `CPU Die (average)`, `Core Max` | `CPU (Tctl/Tdie)`, no sensor `CPU [#0]: …: Enhanced` |
| Clock da CPU | média de `Core N Clock` (`P-core`/`E-core` incluídos), como o Gerenciador de Tarefas. O pico é o maior `Core N Clock`. `Average Effective Clock` vira detalhe ("efetivo"); sem núcleos, é o clock principal | `Core N Clock (perf #x/y)`, média de 5,28–5,39 GHz, com o Gerenciador marcando 5,30 GHz. O efetivo, que desconta os estados de repouso, ficou em 1,3–1,9 GHz no desktop |
| Consumo da CPU | `CPU Package Power`, `CPU PPT`, `CPU Power` | `CPU Package Power`, cerca de 100 W com 22–32% de uso |
| Tensão da CPU | `CPU VDDCR_VDD Voltage…` (SVI3, AM5), `CPU Core Voltage…` (SVI2, AM4), `Vcore`, `CPU VID`, `Core VIDs (avg)`; senão, o `Vcore` de qualquer sensor (Super I/O da placa); por fim, o maior `Core N VID` | `CPU VDDCR_VDD Voltage (SVI3 TFN)`, 1,37–1,39 V. O `Vcore` da placa (1,416 V) fica no sensor ITE, fora do grupo da CPU |
| GPU | a do grupo `GPU [#N]` cujo nome contém o da placa dedicada (do registro); senão, a primeira com consumo | `GPU [#0]` (RX 9070 XT). A iGPU aparece como `GPU [#1]` |
| Uso da GPU | `GPU Core Load`, `GPU Utilization`, `GPU D3D Usage` | `GPU Utilization`, contador do driver, o mesmo do Adrenalin |
| Temperaturas da GPU | `GPU Temperature`; hotspot pelo primeiro `hot ?-?spot` sem `(Max)`; junção da memória por `GPU Memory Junction Temperature` ou `GPU Memory Temperature` | `GPU Temperature`, `GPU Hot Spot Temperature` (existe também `… (Max)`) e `GPU Memory Junction Temperature` |
| Clocks da GPU | `GPU Clock`, `GPU Memory Clock` | os dois; a memória a 2505 MHz |
| Tensão da GPU | `GPU Core Voltage…`, `GPU Voltage` | `GPU Core Voltage (VDDCR_GFX)` |
| Consumo da GPU | `Total Board Power` / `(TBP)` exato, `GPU Power`, `Total Graphics Power` / `(TGP)`, `GPU ASIC Power`, `GPU PPT` | `Total Board Power (TBP)`. **Armadilha:** `Total Board Power (TBP) Limit` também é do tipo potência (340 W), por isso o padrão termina no rótulo |
| VRAM em uso | `GPU Memory Allocated`, `GPU D3D Memory Dedicated` | `GPU D3D Memory Dedicated`. **Armadilha:** `GPU Memory Usage` veio do tipo "outro", em MB e com valor absurdo (402458); só é usado quando é do tipo uso (%) |
| Ventoinha da GPU | `GPU Fan` (RPM) | `GPU Fan` = 0 RPM no desktop: a placa desliga as ventoinhas em baixa carga |
| Memória | `Memory Clock` (MHz real; MT/s = 2 ×), `Tcas`, `Trcd`, `Trp`, `Tras`, `Trfc`, `Command Rate` | sensor `Memory Timings`: 2994,6 MHz (5989 MT/s), 32-40-40-80, tRFC 824, 1T |
| Fabric | `Infinity Fabric Clock (FCLK)` e `Memory Controller Clock (UCLK)`, no grupo da CPU. A prévia mostra UCLK:MCLK como 1:1 ou 1:2 | FCLK 1996 MHz; UCLK = MCLK, ou seja 1:1 |
| Temperatura dos DIMMs | maior temperatura dos sensores `DDR…` | `SPD Hub Temperature` dos dois `DDR5 DIMM [#N]`, 56–58 °C |
| FPS (reserva) | `Framerate`, `Frame Time` | `Framerate` existe no grupo da GPU (driver) e no sensor `RTSS`, ambos em 0 sem jogo |

**Uso da GPU no desktop (observado):** o contador do driver (`GPU Utilization`) oscilou entre 13% e 49%, enquanto `GPU D3D Usage` ficou em 2–3%. A prévia mostra o do driver, que é o número do Adrenalin.
- **Inferido:** em jogo, os dois convergem. A diferença no desktop vem de trabalho fora da fila 3D.

**PresentMon no HWiNFO (observado):** o HWiNFO 8.29 traz um sensor `PresentMon`, por ETW e sem injeção. Ele traz `Framerate Presented (avg/99%/1%)`, `Frame Time Presented`, `GPU Busy` e `CPU Busy`, mas o rótulo não diz qual processo está sendo medido.
- Em amostras de 2 s, o FPS caiu de cerca de 240 para 47–74 quando o primeiro plano passou a ser outro app (Fluxer).
- **Inferido:** o sensor segue o app em primeiro plano. Com a barra aberta, o primeiro plano é a própria barra, então o painel **não usa** esse sensor. O FPS continua vindo do RTSS, que escolhe o processo.
- **Para o S5:** o mesmo ETW, consumido pelo próprio núcleo com o PID do jogo, resolveria a atribuição.

Para conferir de novo, rode `--verify-perf-live` e leia `bin\design-preview\hwinfo-real.txt`. O arquivo lista todos os rótulos, valores e unidades.

## RTSS: memória compartilhada

| Item | Valor |
|---|---|
| Objeto | `RTSSSharedMemoryV2`, aberto só para leitura |
| Assinatura | `0x52545353` ("RTSS") |
| Cabeçalho | +8 tamanho da entrada de app; +12 offset do vetor; +16 número de entradas |
| Entrada de app | +0 PID; +4 caminho (260 bytes, ANSI); +268 `dwTime0`; +272 `dwTime1`; +276 `dwFrames`; +280 `dwFrameTime` (µs) |

O FPS é `1000 × dwFrames / (dwTime1 − dwTime0)`.

Uma entrada só conta se o período mudou desde a leitura anterior ou terminou há menos de 3 s, pelo `Environment.TickCount`. Assim, um jogo minimizado ou parado não fica com um FPS antigo.

**Observado em 2026-10-23:** versão `0x00020015`, entradas de 12416 bytes, 256 posições. Os apps hookados estavam presentes, mas nenhum com quadros, porque não havia jogo rodando. **Ainda não foi validado com um jogo.**

## Modo de energia

| Item | Valor |
|---|---|
| Leitura | `PowerGetUserConfiguredACPowerMode(GUID*)` / `…DCPowerMode`, de `powrprof.h` (SDK 10.0.26100, `NTDDI_WIN11_GA`) |
| Escrita | `PowerSetUserConfiguredACPowerMode(const GUID*)` / `…DCPowerMode`, de acordo com `GetSystemPowerStatus` (AC ou bateria) |
| GUIDs (`winnt.h`) | `GUID_POWER_MODE_BEST_EFFICIENCY` `961cc777-2547-4f9d-8174-7d86181b8a7a`; `GUID_POWER_MODE_NONE` (Equilibrado) `00000000-…`; `GUID_POWER_MODE_BEST_PERFORMANCE` `ded574b5-45a0-4f42-8737-46345c09c238`. O `3af9b8d9-…` está marcado como obsoleto no cabeçalho |
| Plano ativo | `PowerGetActiveScheme`. Fora do Equilibrado (`381b4222-f694-41f0-9685-ff5bb260df2e`), o seletor fica inativo |

**Observado em 2026-10-23:** plano Equilibrado; modo AC configurado Desempenho Máximo. `PowerGetEffectiveOverlayScheme` devolveu o mesmo GUID, e o registro `HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\ActiveOverlayAcPowerScheme` também.

As funções `*OverlayScheme` existem na DLL, mas não estão nos cabeçalhos do SDK, por isso não são usadas.

**A escrita não foi executada pelo agente.** Ela será validada quando o usuário escolher um modo na prévia. Para conferir depois, leia o registro acima, o log (`energia: modo -> …`) e Configurações > Energia.

## Reservas do Windows

| Dado | Fonte |
|---|---|
| Nome da CPU | `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0\ProcessorNameString` |
| Uso da CPU | diferença entre duas chamadas de `GetSystemTimes`; o tempo de kernel inclui o ocioso |
| RAM | `GlobalMemoryStatusEx` (total, disponível, carga) |
| GPU e VRAM total | `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\NNNN`: `DriverDesc` e `HardwareInformation.qwMemorySize` (ou `MemorySize`). Fica a placa com mais memória |
| Taxa da memória | WMI `Win32_PhysicalMemory.ConfiguredClockSpeed` (MT/s), lido uma vez |

**Observado em 2026-10-23:** AMD Ryzen 7 7700X; AMD Radeon RX 9070 XT com 16304 MB; RAM de 31,1 GB com 66% de carga; 6000 MT/s.
