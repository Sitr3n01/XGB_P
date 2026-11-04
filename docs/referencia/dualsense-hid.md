# Referência: DualSense via HID

## Identificação

| Item | Valor |
|---|---|
| VID/PID | `054C:0CE6` (DualSense), `054C:0DF2` (DualSense Edge) |
| Caminho USB | `\\?\hid#vid_054c&pid_0ce6&mi_03#…` |
| Caminho Bluetooth | `\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#…` |
| Virtual do DSX (emulação DualSense) | `HID\VID_054C&PID_0CE6&MI_03\4&10F36466&0&0000`, sob `USB\VID_054C&PID_0CE6\2&22E30EF8&0&1`. É a mesma porta do barramento **Nefarius ViGEm G2** que hospeda o Xbox 360 virtual (`USB\VID_045E&PID_028E\2&22E30EF8&0&1`) |

## Abertura

`CreateFile(path, GENERIC_READ, FILE_SHARE_READ|FILE_SHARE_WRITE, OPEN_EXISTING, FILE_FLAG_OVERLAPPED)`
- Abre em modo compartilhado e **convive com a Steam e com o DSX**.
- **Nunca** mande relatórios de saída (LED, vibração, gatilhos adaptativos). Isso briga com a Steam e com o DSX.
- `ERROR_ACCESS_DENIED (5)` = escondido pelo HidHide.
- O tamanho do relatório vem de `HidP_GetCaps(...).InputReportByteLength`: 64 no USB/virtual, 78 no Bluetooth.
- Uma leitura pode trazer **vários relatórios** (ex.: 128 bytes = 2 × 64). Processe em blocos de `InputReportByteLength`.

## Layouts de entrada

| Relatório | Tamanho | Offset dos botões (`b0`, `b1`, `b2`) |
|---|---|---|
| USB / virtual `0x01` | 64 | 8, 9, 10 |
| Bluetooth completo `0x31` | 78 | 9, 10, 11 (um byte de tag a mais). O DSX ou a Steam ativam esse modo |
| Bluetooth simples `0x01` | 10 | 5, 6, 7 (antes de algum app ativar o modo completo) |

| Byte | Bits |
|---|---|
| `b0` | `0x0F` direcional (0=cima, 1=cima-dir … 7=cima-esq, **8=repouso**), `0x10` quadrado, `0x20` X, `0x40` círculo, `0x80` triângulo |
| `b1` | `0x01` L1, `0x02` R1, `0x04` L2 (digital), `0x08` R2, `0x10` Create, `0x20` Options, `0x40` L3, `0x80` R3 |
| `b2` | `0x01` **PS**, `0x02` **clique do touchpad**, `0x04` **microfone (mute)** |

No USB, os analógicos ficam nos offsets 1–4 (LX, LY, RX, RY) e os gatilhos em 5–6 (L2, R2).

**Ampliação do decoder em 2026-10-25 (ADR 0036):** offsets relativos ao começo de cada relatório, incluindo seu ID:

| Layout | LX / LY / RX / RY | L2 / R2 |
|---|---|---|
| USB/virtual `0x01`, 64 B | 1 / 2 / 3 / 4 | 5 / 6 |
| Bluetooth completo `0x31`, 78 B | 2 / 3 / 4 / 5 | 6 / 7 |
| Bluetooth simples `0x01`, 10 B | 1 / 2 / 3 / 4 | 8 / 9 |

Layout conferido nas estruturas `PS5SimpleStatePacket_t` e `PS5StatePacketCommon_t` do [driver oficial do SDL](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c), consultado em 2026-10-25. Eixos são bytes 0–255, centrados em 128; gatilhos 0–255. Não é necessário enviar feature/output report para decodificar relatórios que DSX/Steam já disponibilizam. Não foi usado REA nesta mudança.

O decoder valida limites do buffer e requer os dez bytes no relatório simples. Testes sintéticos incluem os três layouts com offset inicial não zero. No teste físico desta sessão, o virtual entregou os quatro sentidos do analógico esquerdo e o clique do touchpad ao leitor atualizado. Isso não comprova os outros eixos/gatilhos no hardware; continuidade e limites.

**Revisão de bateria, 2026-10-26 ([ADR 0051](../decisoes/0051-bateria-fisica-do-dualsense.md)):** o USB completo exige 64 bytes; o Bluetooth completo `0x31` exige 78 bytes e CRC-32 de entrada válido, com semente 0xA1 e os últimos quatro bytes little-endian. O simples `0x01` também pode chegar preenchido até 78 bytes: é navegação sem bateria, não USB. Layout e validação conferidos no [SDL](https://raw.githubusercontent.com/libsdl-org/SDL/main/src/joystick/hidapi/SDL_hidapi_ps5.c) e no [hid-playstation](https://raw.githubusercontent.com/torvalds/linux/master/drivers/hid/hid-playstation.c). Não foi usado REA.

**Byte de status (bateria):** offset **53** no USB/virtual `0x01` completo e **54** no Bluetooth `0x31`. O relatório simples não tem esse byte. É o mesmo layout do driver `hid-playstation` do Linux.
- **Nibble baixo:** nível de 0 a 10. Percentual = `min(nível × 10 + 5, 100)`.
- **Nibble alto:** estado da carga.

| Estado | Significado |
|---|---|
| `0` | descarregando |
| `1` | carregando |
| `2` | completa (100%) |
| `A` | erro de tensão |
| `B` | erro de temperatura |
| `F` | erro de carga |

Leitura em src/lib/dualsense/DualSenseHid.cs (`DualSenseReport.DecodeBattery`).

A fonte da bateria é independente da navegação. `BatterySource` distingue USB físico, Bluetooth físico, virtual e incerto. A origem vem dos ancestrais PnP (`SetupDiGetDeviceInterfaceDetail`, [CM_Get_Parent](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_parent), [CM_Get_Device_ID](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_device_idw)), sem escrita/configuração. ViGEm é virtual; caminho USB sozinho não prova origem física. O ancestral normal `ROOT\ACPI_HAL` não torna o hardware virtual. A classificação nativa do DualSense real ainda precisa de validação com o controle presente.

Na UI, só origem física recente representa a bateria, com preferência USB e estabilidade na mesma prioridade. `BatteryChanged` renova a amostra ao menos a cada segundo de relatórios válidos, mesmo sem mudança de percentual; `ObservedUtc` registra esse horário. Depois de mais de 5 s sem renovar, a amostra expira na próxima atualização de status de 1 s. A perda usa evento `DeviceLost`, sem interpretar texto de log. Virtual/origem incerta/relatório simples sem física elegível deixam a bateria indisponível. Estados 1 e 2 desenham carregamento e carga completa separadamente.

## Medições (2026-10-23)

**Taxa de relatórios:** Bluetooth completo ~723/s; virtual do DSX ~640–658/s.

**Repasse do DSX:** o DSX repassa PS e microfone ao virtual no mesmo milissegundo do físico.

**Tempos do usuário:**
- toque curto: 120–295 ms;
- segurado: 618–1020 ms;
- intervalo entre toques rápidos: 78–130 ms.

**Touchpad:** nenhum clique registrado até agora (a confirmar).

## HidHide (configuração em 2026-10-23)

- **Cloak:** ligado.
- **Apps liberados:**
  - `HidHideCLI.exe`
  - `DSX.exe`
  - `SamsungTvOrchestrator.exe`
  - `…\Github\gamebar-sonda\tools\DualSenseReader.exe` (caminho antigo)
- **A Steam não está liberada:** no Bluetooth ela só vê o controle virtual do DSX.
- **Dispositivos escondidos:**
  - um USB `HID\VID_054C&PID_0CE6&MI_03\9&152AC2B&0&0000`;
  - três instâncias Bluetooth `…_VID&0002054C_PID&0CE6\B&D95C7F2&{3,4,5}&0000`.
- **CLI:** `C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe`, com `--app-list`, `--dev-list`, `--app-reg` e `--app-unreg`.
