# Imagens dos jogos: extração local

Implementação em `src/lib/games/GameImages.cs`; integração em `RecentGames.cs` e metadados em `GameCatalog.cs`. C# 5, .NET Framework, sem SDK/NuGet. Não houve engenharia reversa de binários ou chamada ao REA; endereço e ID REA não se aplicam.

## Contratos usados

- [SHDefExtractIconW — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shdefextracticonw): extrai o recurso de ícone indicado e aceita tamanho em pixels. Índice negativo representa ID de recurso. Cada HICON obtido é liberado por `DestroyIcon` após copiar seus pixels.
- Leitura dos metadados `.lnk` pelo COM `WScript.Shell.CreateShortcut`: propriedades `TargetPath`, `Arguments` e `IconLocation`, sem `Save`, `Run` ou `Exec` no leitor de produção. A suíte cria e salva exclusivamente um `.lnk` sintético dentro de `bin` para verificar esse contrato.
- `.url` é lido como configuração `[InternetShortcut]`, campos `URL`, `IconFile` e `IconIndex`. Nenhuma URI é aberta durante extração.
- `System.Drawing` decodifica arquivos de imagem/ICO e normaliza PNG transparente. `BitmapImage` no WPF lê os bytes com `OnLoad`, é congelado e não mantém o arquivo original aberto.

## Procedência e cache

Prioridade e regras na [ADR 0021](../decisoes/0021-imagens-locais-automaticas-dos-jogos.md). `GameEntry` expõe `IconLocation`, `IconPng`, `IconKey` e `IconSource`; a imagem resolvida é apenas dado gerenciado entregue à UI.

As fontes são limitadas a arquivos locais conhecidos. Caminhos UNC são recusados no resolvedor; links HTTP não são baixados. Não há varredura ampla do disco ou associação por título parecido. O cache é PNG 64×64; não copia EXE/DLL nem o pacote com identidade Microsoft.

Fingerprint inclui versão (`game-icon-v1`), resolução, caminho, tamanho/data do arquivo e índice do ícone. Cache em memória evita repetição, inclusive de falhas enquanto a origem permanecer igual. Novo arquivo ou metadados alterados geram nova tentativa; PNG inválido no disco é reconstruído. Se o cache não puder ser gravado, a imagem extraída continua utilizável. Cache não é prova de autenticidade dos binários.

## Observado nesta máquina — 2026-10-23

No Windows 10.0.26300, Steam.exe 11.15.70.27, `--verify-games-live` extraiu:

| Jogo | Origem selecionada |
|---|---|
| Wuthering Waves | `D:\SteamLibrary\steamapps\common\Wuthering Waves\Wuthering Waves.exe` |
| Genshin Impact | `D:\Games\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe` |
| CONTROL Resonant | `%USERPROFILE%\Documents\Piratao\CONTROL-Resonant-SteamRIP.com\CONTROL Resonant\CONTROLResonant.exe` |

O caminho personalizado do Genshin no atalho Steam está ausente; a resolução conseguiu usar o executável real em vez de WScript. Não foram encontrados atalhos desses três jogos no desktop nesta coleta. A leitura de `.lnk` e `.url` foi validada com arquivos sintéticos em `bin`, não com esses jogos no desktop.

Render inspecionado com os três ícones e cartão Voltar ao jogo nas proporções existentes. Outras seções do render live usam fixtures, como antes. Nenhum jogo foi iniciado ou modificado. A execução normal permanece para validação do usuário por Win+Shift+G.
