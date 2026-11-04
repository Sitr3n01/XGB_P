# Conexões no Windows: rádios, Wi-Fi e Bluetooth

Fatos usados pela [ADR 0027](../decisoes/0027-conexoes-wifi-e-bluetooth.md). Coleta de 2026-10-24 no Windows 10.0.26300. **Observado** indica algo lido nesta máquina; **inferido** indica algo da documentação ou do desenho que não foi exercitado aqui.

## Rádios — `Windows.Devices.Radios`

- `Radio.GetRadiosAsync()` lista os rádios com `Kind` (WiFi, Bluetooth) e `State` (On, Off, Disabled). `SetStateAsync(On/Off)` faz o mesmo liga/desliga das Configurações rápidas.
- `Radio.RequestAccessAsync()` respeita a privacidade "Permitir que apps controlem os rádios". **Observado:** "permitido" para o app de área de trabalho sem pacote, e Wi-Fi e Bluetooth "ligado".
- C# 5 sem SDK: referência a `Windows.Devices.winmd` de `System32\WinMetadata`. A espera das operações é feita com `Completed` e um `ManualResetEvent` de 5 s, o mesmo padrão do player de mídia.
- **Inferido:** `Disabled` indica chave física, política ou adaptador desativado, e não muda pela API.

## Wi-Fi — Native Wifi API (`wlanapi.dll`)

| Função | Uso |
|---|---|
| `WlanOpenHandle(2)`, `WlanEnumInterfaces` | primeiro adaptador. Sem o serviço WLAN AutoConfig (1062), a página diz que não há adaptador |
| `WlanGetProfileList` | perfis salvos (`WLAN_PROFILE_INFO`, 516 bytes: nome WCHAR[256] + flags; flag 1 = política de grupo) |
| `WlanGetAvailableNetworkList(0)` | alcance (`WLAN_AVAILABLE_NETWORK`, 628 bytes: nome do perfil em 0, sinal em 604, segurança em 608, `dwFlags` em 620; flag 1 = conectada) |
| `WlanScan` | só ao entrar na página Wi-Fi |
| `WlanConnect` | modo perfil (0), `dot11_BSS_type_any` (3) |
| `WlanDisconnect`, `WlanDeleteProfile` | desconectar e esquecer (apaga também a senha salva) |

- **Observado:** 36 perfis salvos, 2 em alcance (sinal 92–93%), nenhum conectado, com a localização permitida.
- No Windows 11 24H2+, a lista de alcance exige a permissão de localização para apps da área de trabalho. Negada, ela retorna 5 (acesso negado). A página mostra só uma nota: ela lista apenas as redes salvas em alcance (ADR 0027, revisão), e sem essa lista nenhuma pode ser dada como em alcance. **Inferido:** mensagem da Microsoft sobre o 24H2; não reproduzido aqui porque a localização está permitida.
- Nenhuma senha é lida. Redes novas ficam para `ms-settings:network-wifi`.

## Bluetooth — `Windows.Devices.Enumeration` e áudio

- **Pareados:** o seletor de `BluetoothDevice`/`BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)` com `DeviceInformationKind.AssociationEndpoint`, e as propriedades `System.Devices.Aep.IsConnected` e `System.Devices.Aep.ContainerId`.
- **Observado:** o mesmo celular aparece como clássico e LE. O `ContainerId` une as duas entradas, como nas Configurações.
- **Tipo:** `BluetoothDevice.FromIdAsync(id).ClassOfDevice`, por classe maior e menor (gamepad e joystick viram controle). O DualSense também é reconhecido pelo nome.
- **Remover:** `DeviceInformation.CreateFromIdAsync(id, [], AssociationEndpoint).Pairing.UnpairAsync()` em cada entrada do aparelho.
- **Conectar/desconectar áudio:**
  - encontrar os endpoints de áudio, em qualquer estado, com o mesmo `PKEY_Device_ContainerId` ({8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C}, 2);
  - seguir `IMMDevice.Activate(IDeviceTopology)` → `GetConnector(0)` → `GetDeviceIdConnectedTo` → `IMMDeviceEnumerator.GetDevice` → `Activate(IKsControl)`;
  - enviar `KsProperty` com `KSPROPSETID_BtAudio` {7FA06C40-B8F6-4C7E-8556-E8C33A12E54D}, `KSPROPERTY_ONESHOT_RECONNECT` (0) ou `KSPROPERTY_ONESHOT_DISCONNECT` (1) e `KSPROPERTY_TYPE_GET`.
- **Observado:** o fone pareado tem um filtro com `IKsControl`, obtido sem enviar a propriedade. O celular e o DualSense não têm.
- **Inferido, não testado:** o efeito do oneshot no fone, porque o agente não conecta nem desconecta aparelhos reais.

## Limites

- A prévia roda sem elevação. Ligar/desligar rádios, conectar e remover dependem do Windows aceitar a operação. Toda falha vira mensagem, e o estado só muda depois da leitura seguinte.
- O DualSense conectado por Bluetooth cai ao desligar o Bluetooth. Por isso há confirmação, e ele não pode ser removido pela barra.
