# Lojas e launchers de jogos no Windows

Como a barra encontra e abre as lojas (`src/lib/games/GameStores.cs`, ADR 0029). Tudo é só leitura até o usuário escolher uma loja.

## Detecção

A ordem da tela é fixa: Xbox, Steam e Epic primeiro, depois os demais launchers e, por último, a Microsoft Store. Só aparecem as lojas instaladas.

| Loja | Como é detectada | O que abre |
|---|---|---|
| Xbox | pacote `Microsoft.GamingApp_8wekyb3d8bbwe` (`GetPackagesByPackageFamily`) | `shell:AppsFolder\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App` |
| Steam | protocolo `steam` | modo Xbox: `steam://open/bigpicture/store`; desktop: `steam://url/StoreFrontPage` |
| Epic Games | protocolo `com.epicgames.launcher` ou caminho padrão | `com.epicgames.launcher://store` |
| EA app | protocolo `origin2`/`origin` (`EALauncher.exe`) ou caminho padrão | o executável, igual ao atalho "EA" do Menu Iniciar |
| Ubisoft Connect | protocolo `uplay` ou caminho padrão | `UbisoftConnect.exe`, igual ao atalho do Menu Iniciar |
| GOG GALAXY | protocolo `goggalaxy` ou `GOG Galaxy\GalaxyClient.exe` | o executável |
| Battle.net | protocolo `battlenet` ou `Battle.net\Battle.net Launcher.exe` | o executável |
| Riot Client | `rc_default` em `%ProgramData%\Riot Games\RiotClientInstalls.json` | `RiotClientServices.exe` sem argumentos |
| Amazon Games | protocolo `amazon-games` ou `%LOCALAPPDATA%\Amazon Games\App\Amazon Games.exe` | o executável |
| Rockstar Games Launcher | `Rockstar Games\Launcher\Launcher.exe` | o executável |
| itch | protocolo `itch` ou `%LOCALAPPDATA%\itch\itch.exe` | o executável |
| Heroic Games Launcher | protocolo `heroic` ou `%LOCALAPPDATA%\Programs\heroic\Heroic.exe` | o executável |
| Microsoft Store | pacote `Microsoft.WindowsStore_8wekyb3d8bbwe` | `shell:AppsFolder\Microsoft.WindowsStore_8wekyb3d8bbwe!App` |

- **Protocolo:** o executável sai de `HKCR\<esquema>\shell\open\command` e só vale se existir no disco. Um protocolo de uma loja desinstalada não faz a loja aparecer.
- **Abertura:** `ShellExecute` do alvo. Os launchers abrem uma instância só: com o launcher já aberto ou na bandeja, a janela dele aparece.
- **Steam:** as duas rotas estão no `steamui/sp.js` instalado. `open/bigpicture/store` fica na tabela de rotas, e `StoreFrontPage` é uma página nomeada da rota `url`. A rota `open/storefront` exige o modo grande (`bRequiresLargeMode`), por isso não é usada no desktop.
- **Epic:** `com.epicgames.launcher://store` é inferido, porque é o formato dos links "Obter" da própria Epic. Não foi aberto pelo agente.

**Observado em 2026-10-24 (só leitura):**

- Instalados: Xbox (`Microsoft.GamingApp` 2610.1001.16.0), Microsoft Store, Steam, Epic Games (em `Program Files`, não em `x86`), EA app, Ubisoft Connect e Riot Client.
- Ausentes: GOG, Battle.net, Amazon, Rockstar, itch e Heroic.
- Os atalhos do Menu Iniciar do EA app e do Ubisoft Connect apontam para os mesmos executáveis dos protocolos.

## Ícones

`SHCreateItemFromParsingName` + `IShellItemImageFactory.GetImage(64×64, SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY)`, o mesmo ícone do Menu Iniciar, para executáveis e para `shell:AppsFolder\AUMID`.

- O bitmap é um DIB de 32 bits com alfa pré-multiplicado.
- **Observado:** a altura vem positiva (de baixo para cima). Sem inverter, os ícones saem de cabeça para baixo.
- A conversão copia para ARGB, inverte quando a altura do `DIBSECTION` é positiva e guarda PNG em cache por origem.
- Sem ícone, a tela usa o símbolo de sacola (Segoe MDL2 `E719`).
- A leitura roda numa thread STA, porque os ícones vêm do shell.
