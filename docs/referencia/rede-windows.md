# Rede: estado da conexão para a faixa de status

A implementação fica em `src/lib/network/NetworkStatus.cs` e é usada pela prévia em `src/app/StatusBar.cs` (ADR 0018). Foi escrita em C# 5, para .NET Framework x64, com os metadados WinRT do próprio Windows (`System32\WinMetadata\Windows.Networking.winmd`), sem SDK.

Não há análise nem modificação de binários, então não há endereço ou ID do REA.

## API

| Item | Valor |
|---|---|
| Perfil | `NetworkInformation.GetInternetConnectionProfile()`; se for nulo ou sem conectividade, o primeiro de `GetConnectionProfiles()` com nível diferente de `None` |
| Internet | `GetNetworkConnectivityLevel() == InternetAccess`. `LocalAccess` e `ConstrainedInternetAccess` contam como conectado sem internet |
| Tipo | `IsWlanConnectionProfile` → Wi-Fi; `IsWwanConnectionProfile` → celular; os demais → Ethernet. `NetworkAdapter.IanaInterfaceType` fica no log (6 Ethernet, 71 Wi-Fi) |
| Sinal | `GetSignalBars()` (0–5, `IReference<byte>` projetado como `byte?`), só no Wi-Fi e no celular |
| Chamada | síncrona, em worker, a cada 5 s com a barra visível |

**Não usado:** o SSID (`WlanConnectionProfileDetails.GetConnectedSsid`) e o nome do perfil, que no Wi-Fi costuma ser o próprio SSID.
- **Inferido**, da documentação da Microsoft sobre o Windows 11 24H2: as APIs que devolvem o SSID ou o BSSID passaram a exigir a permissão de localização para apps de desktop. Elas podem mostrar o pedido de permissão do sistema.
- A faixa não precisa desse dado, então ele não é lido.

## Observado em 2026-10-23

- `--verify-status-live` e o log da prévia: `ethernet com internet | IANA 6`, com o adaptador Realtek RTL8125 de 2,5 GbE.
- O Wi-Fi 6E (RTL8852CE) estava presente, mas sem ser o perfil de internet, então o caminho do Wi-Fi não foi exercitado com dados reais. Ele é coberto pela verificação com estados controlados.
- Nada foi configurado no Windows. A leitura não toca no SSID; por isso, **inferido**, não deve acionar o pedido de localização.
