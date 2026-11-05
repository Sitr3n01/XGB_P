# 0094 — Bateria de fones Bluetooth na faixa de status

- **Status:** Aceita. No código desde a versão seguinte à 2.1.0 (ainda não lançada); teste físico pendente, com o fone conectado.
- **Data:** 2026-11-04

## Contexto

O usuário pediu a bateria do Logitech G435. Pelo receptor LIGHTSPEED não há leitura segura (ver evidências). Ele então pediu um leitor para fones Bluetooth, o G435 no modo Bluetooth ou outros.

**Observado (somente leitura, 04/11):**

- Fones e headsets informam o nível pelo perfil Hands-Free. O Windows guarda esse nível no nó do serviço, na propriedade `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`, um byte de 0 a 100 (a mesma das Configurações).
- Neste PC, o nó o nó Hands-Free AG de um fone pareado (`BTHENUM\{0000111E-…}`, classe System) tinha 100. Nenhum outro dos 43 nós Bluetooth tinha a propriedade.
- O valor continua lá com o fone desconectado: o diagnóstico real encontrou 1 fone com bateria e 0 conectados.

## Decisão

1. **Leitura** (`lib/network/BluetoothBattery.cs`): nós presentes dos enumeradores `BTHENUM`, `BTHLE` e `BTHLEDEVICE` pela SetupAPI. Só se leem a propriedade de bateria (tipo byte, de 0 a 100) e o `ContainerId`. Nada é enviado ao aparelho: nem GATT, nem HID, nem AT.
2. **É fone quando** o nó com bateria é de serviço de áudio do lado do fone: Hands-Free `111E`, Headset `1108`/`1131` ou A2DP `110B`. O gateway `111F` é o lado do celular e fica de fora. Num mesmo aparelho, o nó de áudio tem prioridade.
3. **Nome e conexão** vêm da mesma enumeração pública das Conexões (`System.Devices.Aep.IsConnected`/`ContainerId`), sem abrir o aparelho.
4. **Só o fone conectado aparece.** Entre vários, o primeiro conectado em ordem de nome.
5. **Faixa de status:** depois da bateria do DualSense, um ícone de fone (arco e conchas, no traço dos outros ícones) e o mesmo desenho de bateria, sem raio de carga (o Hands-Free não informa carga). O nível fica em destaque com 15% ou menos. A dica e o nome acessível mostram o nome do fone e a porcentagem. Na prévia da ADR 0093, ele acompanha rede e bateria ao lado do título.
6. **Conexões → Dispositivos Bluetooth:** "Conectado · 80% · Áudio". Um aparelho desconectado não mostra o nível guardado.
7. **Frequência:** a cada abertura da barra e a cada 30 s com ela visível, fora da thread da interface. Se a leitura falhar, o fone some da faixa em vez de mostrar um nível antigo.

## Consequências

- O G435 no Bluetooth e outros fones com Hands-Free aparecem sem configuração. Um fone que não informa bateria ao Windows (sem indicador Hands-Free) não aparece. É o mesmo limite das Configurações do Windows.
- A precisão é a do fone: muitos informam em degraus de 10% ou 20%.
- Com controle e fone, a faixa mostra duas baterias; só a do fone tem ícone próprio, e a do controle continua sem ícone, como desde a ADR 0051.
- **Verificação:** `tests/HeadsetStatusVerification.cs` tem 17 casos, cobrindo classificação, prioridade, ordem, desconectado, nível baixo, vários fones, alinhamento, acessibilidade e Conexões, sem leitura real. `--verify-status-live` e `--verify-connections-live` passam a registrar a leitura real, só de leitura.
