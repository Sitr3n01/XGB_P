# Guia: testes de controle (DualSense + DSX)

Duas ferramentas, ambas **somente leitura**:

| Ferramenta | O que lê | Saída |
|---|---|---|
| `tools/monitorar-botao-guia.ps1` | botão Guia pelo XInput (`XInputGetStateEx`, ordinal 100 do `xinput1_4.dll`). Só existe com o DSX em **emulação Xbox 360** | `%USERPROFILE%\GameBarProbe\logs\guia.log` |
| `bin/DualSenseReader.exe` (fonte em `tools/dualsense-reader/`) | o DualSense **direto pelo HID**: físico USB/BT e o virtual do DSX | `%USERPROFILE%\GameBarProbe\logs\dualsense.log` |

Para correlacionar com o shell, use também o `probe.log` da sonda.

## Rodar

Compilar (gera `bin\DualSenseReader.exe`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\compilar.ps1
```

Só listar os dispositivos e o acesso a cada um:

```powershell
.\bin\DualSenseReader.exe 0
```

Ler por N minutos (procura dispositivos novos a cada 2 s e tenta de novo os bloqueados):

```powershell
.\bin\DualSenseReader.exe 20
```

Monitor do Guia por N minutos:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\monitorar-botao-guia.ps1 -Minutos 15
```

## HidHide

O DSX esconde o DualSense **físico** com o HidHide. O DualSense **virtual** do DSX (emulação DualSense) **não fica escondido**, então ler o virtual não exige nada.

Para ler o físico pelo Bluetooth, libere o executável. É uma mudança de configuração de segurança, então rode **como administrador**:

```powershell
& "C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe" --app-reg "$PWD\bin\DualSenseReader.exe"
```

Para desfazer, troque `--app-reg` por `--app-unreg`. Para ver a configuração atual, que é só leitura: `--app-list`, `--dev-list`, `--cloak-state`.

> A liberação feita em 2026-10-23 aponta para o caminho **antigo** `...\Github\gamebar-sonda\tools\DualSenseReader.exe`. Ver estado da máquina.

## Protocolo padrão

Deixe uns 5 s entre as ações:

1. PS: 3 toques curtos, 1 segurado (cerca de 1 s) e 2 toques duplos.
2. Microfone (mute): 2 curtos e 1 segurado.
3. Touchpad: 2 cliques.

Repita em cada modo do DSX (**Xbox 360** e **DualSense**) e anote a hora da troca.

## Resultados de referência (2026-10-23, Bluetooth)

| Modo do DSX | `DualSenseReader` | Shell → sonda |
|---|---|---|
| Xbox 360 | físico BT (`0x31`, 78 B, ~723/s) com HidHide liberado: PS e MUTE com duração | todos os `SHORTPRESS` e `LONGPRESS` do PS. Nada de DOUBLEPRESS |
| DualSense | físico BT e virtual (`0x01`, 64 B, ~658/s): mesmos eventos no mesmo milissegundo | nada |

**Tempos observados:**
- toque curto: 120–295 ms;
- segurado: 618–1020 ms;
- intervalo entre toques rápidos: 78–130 ms;
- o LONGPRESS do shell dispara em torno de 700 ms.

O leitor classifica gestos com `LongPressMs = 600` e `DoublePressGapMs = 350`. Toques simples muito rápidos aparecem como "DUPLO".

**Pendente:** nenhum clique de touchpad foi registrado. Falta confirmar se houve clique ou se há erro de decodificação.

## Limitações conhecidas do leitor

- O rótulo `USB` vale para qualquer caminho com `&mi_`, inclusive o **virtual do DSX**. O virtual fica sob a porta `USB\VID_054C&PID_0CE6\2&22E30EF8&0&1` do barramento Nefarius ViGEm G2.
- Um dispositivo bloqueado só é reaberto quando o HidHide libera o **caminho exato** do executável.
