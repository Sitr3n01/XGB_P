# Mídia em reprodução no Windows (SMTC)

Implementação local em `src/lib/media/MediaSessions.cs`, consumida pela prévia em `src/app/MediaPlayer.cs` (ADR 0015). Feita em C# 5 e .NET Framework x64. Usa API pública e não analisa nem modifica binários, então não há endereço ou ID do REA associado.

## API

| Uso | Chamada |
|---|---|
| Gerenciador | `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()` (guardado e reutilizado) |
| Sessões | `GetSessions()`; atual: `GetCurrentSession()` |
| Faixa | `TryGetMediaPropertiesAsync()` → título, artista, álbum, `Thumbnail` |
| Estado | `GetPlaybackInfo()` → status, `Controls.Is*Enabled`, `IsShuffleActive`, `AutoRepeatMode`, `PlaybackRate` |
| Linha do tempo | `GetTimelineProperties()` → `StartTime`, `EndTime`, `Position`, `LastUpdatedTime` |
| Comandos | `TryTogglePlayPauseAsync`, `TrySkipPreviousAsync`, `TrySkipNextAsync`, `TryChangeShuffleActiveAsync`, `TryChangeAutoRepeatModeAsync`, `TryChangePlaybackPositionAsync` |
| Nome do app | `AppInfo.GetFromAppUserModelId(...).DisplayInfo.DisplayName`; *fallback*: descrição do executável ou o próprio ID |

**Observado:**
- A API funciona num processo desktop comum, sem identidade de pacote nem capacidade declarada.
- Os comandos `Try*` devolvem `false` quando o app recusa.
- A API pública não fornece um ID persistente de sessão. **Desde a ADR 0047**, o backend usa chaves locais por identidade COM retida, em vez de `AppUserModelID#ordem`. Elas duram apenas durante a presença daquele objeto/processo; ausência/recriação não reaproveita chaves. Duas leituras reais de Spotify/Firefox preservaram as chaves nesta revisão; não é contrato de estabilidade entre processos/provedores.

## Compilação sem SDK

- **Referências:** `Windows.Foundation`, `Windows.Media`, `Windows.Storage` e `Windows.ApplicationModel.winmd`, de `%WINDIR%\System32\WinMetadata`, mais `System.Runtime.dll` de `Framework64\v4.0.30319`.
- **Observado:** `System.Runtime.WindowsRuntime.dll` (`AsTask`, `AsStreamForRead`) falha com CS0012, porque espera o assembly unificado `Windows` do SDK, que os metadados do sistema separam por namespace.
- **Solução adotada:** esperar `IAsyncOperation<T>` pelo evento `Completed`, com tempo limite de 3 s, e ler a capa por `DataReader`. É WinRT puro.

## Posição

- O app informa a posição só de vez em quando. `LastUpdatedTime` é o momento da última atualização.
- A posição exibida é calculada assim: `posição + (agora − LastUpdatedTime) × taxa`, somente com status `Playing`, limitada à duração.
- **Observado:** o Spotify não informou `PlaybackRate` (nulo); usa-se 1. Depois de um seek ou de pausar, a prévia ajusta a posição na hora, e a leitura seguinte confirma.
- O seek é absoluto: `StartTime + posição desejada`, em ticks.
- ADR 0047: o gesto captura sessão/faixa; troca de fonte/faixa invalida o pedido pendente. Transporte usa FIFO e leituras anteriores a uma ação são descartadas por revisão.

## Capa

- `Thumbnail.OpenReadAsync()` e leitura completa por `DataReader`, até 8 MB.
- O cache usa título/artista/álbum como chave. A capa é relida nos primeiros 4 s de uma faixa nova, porque alguns apps trocam o título antes da imagem.
- A UI decodifica a 100 px de largura.
- ADR 0047: o worker compara conteúdo e reutiliza a referência somente quando os bytes são iguais. A UI detecta nova capa mesmo com comprimento idêntico.
- **Observado:**
  - Spotify: PNG de 57–177 KB.
  - Firefox: miniatura PNG do vídeo, de 660–920 KB.

## Volume pelo mixer

O SMTC identifica a origem pelo AppUserModelID; o Core Audio, pelo executável da sessão (`AppIdentity`). A correspondência é feita assim:

| ID da mídia | Regra | Exemplo observado |
|---|---|---|
| termina em `.exe` | nome do arquivo de `AppIdentity` igual ao ID | — |
| `Nome_Editor!App` (pacote) | `AppIdentity` contém `\WindowsApps\Nome_` e `__Editor\` | `SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify` → `…\WindowsApps\SpotifyAB.SpotifyMusic_1.304.258.0_x64__zpdnekdrzrea0\Spotify.exe` |
| outro ID explícito | executáveis das janelas visíveis cuja `PKEY_AppUserModel_ID` (`{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}`, 5) é igual ao ID; lido por `SHGetPropertyStoreForWindow`, cache de 10 s | `308046B0AF4A39CB` → `C:\Program Files\Mozilla Firefox\firefox.exe` |

- O volume e o mute do player usam a mesma fila e as mesmas `AudioChange` de sessão do painel Som, um comando por dispositivo.
- Sem sessão correspondente, o controle fica inativo.
- ADR 0047: com caminho conhecido, nome igual em outra pasta não é correspondência. Sem caminho conhecido, múltiplas instalações candidatas ao mesmo nome de exe desabilitam o ajuste. Intenções pendentes/em voo são projetadas por alvo nativo e revisão; volume e mute não se alteram mutuamente.
- **Inferência:** um app com ID explícito e sem janela visível não é casado; um pacote com executável fora de `WindowsApps` também não.

## Sessões observadas (2026-10-23, Windows 10.0.26300)

| App | Controles oferecidos | Notas |
|---|---|---|
| Spotify (Store) | anterior, reproduzir/pausar, próxima, posição, aleatório, repetir, parar | aleatório informado como ativo; repetir `None`; capa PNG |
| Firefox (vídeo de site) | reproduzir/pausar, posição, parar | sem anterior/próxima/aleatório/repetir; nome resolvido como "Firefox" |

O que cada app oferece pode mudar com a versão dele ou com o site em reprodução.
