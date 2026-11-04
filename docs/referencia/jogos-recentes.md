# Jogos recentes: fontes locais e limites observados

**Revisão de 2026-10-25 — Genshin:** o relato atual é desaparecimento dos recentes da Home após fechar. Na coleta, o mapeamento `shortcut:888398940:3899276376`, a cadeia `GenshinKeeperHidden.vbs` → `GenshinKeeper.cmd` → `GenshinImpact.exe` e os arquivos estavam válidos. O catálogo retornou Genshin entre os três recentes elegíveis; `history.json` não continha registro dele, apenas Wuthering Waves. A data de Genshin vinha da Steam: 22/10 às 21:11:29 (UTC−03:00). O jogo estava fechado e o desaparecimento não foi reproduzido. A [ADR 0049](../decisoes/0049-persistencia-de-sessoes-sem-foco.md) corrige confirmação/persistência sem exigir foco quando há duas leituras da runtime exata e identidade de processo; não comprova nem altera falhas transitórias de disponibilidade. Entrega, validação e limites. O restante desta referência preserva a coleta histórica de 23/10.

Coleta somente de leitura em **2026-10-23**, antes e depois da reinicialização informada pelo usuário. Windows `10.0.26300`; Steam.exe `11.15.70.27`. Último boot informado pelo Windows: **22:30:13, America/Sao_Paulo**. Nenhum jogo, launcher ou prévia foi iniciado nesta investigação.

Não houve análise de binários nem chamada ao REA; endereço e ID REA não se aplicam. A leitura dos arquivos de configuração abaixo não constitui API pública estável da Valve. Não foram copiados tokens, credenciais ou configurações completas para o repositório.

## Observado: catálogo Steam

- `HKCU\Software\Valve\Steam\SteamPath` aponta para `C:\Program Files (x86)\Steam`.
- `steamapps\libraryfolders.vdf` registra essa biblioteca e `D:\SteamLibrary`.
- Os quatro `appmanifest_*.acf` encontrados representam **DSX**, **Steamworks Common Redistributables**, **Wallpaper Engine** e **Wuthering Waves**. Os três diretórios de instalação de DSX, Wallpaper Engine e Wuthering Waves existem.
- Portanto, contar todo manifesto como jogo incluiria utilitários e redistribuíveis.
- Na coleta após o boot, não havia processo `steam.exe`, `DSX.exe` ou `XgbDesignPreview.exe`. O registro `ActiveProcess` ainda continha PID e usuário: ele não comprova sessão ativa. O perfil correspondente pode ser candidato ao último perfil, mas precisa ser distinguido de uma conta atualmente conectada.

## Observado: histórico e atalhos externos

No perfil indicado pelo registro existem `config\localconfig.vdf` textual e `config\shortcuts.vdf` binário. Uma segunda pasta em `userdata` não contém esses dois arquivos.

- `localconfig.vdf` contém blocos de aplicativos com `LastPlayed` e `Playtime`. Também contém `LastPlayedTimesSyncTime`; não foi encontrado campo `LastPlayedLocally` na coleta.
- O campo `LastPlayed` de Wuthering Waves corresponde a **2026-10-23 00:12:07 UTC** (**22/10 às 21:12:07** em São Paulo).
- Os cinco atalhos externos contêm nome, identificador numérico, executável, diretório inicial, opções e `LastPlayTime`. Suas coleções de tags estavam vazias.

| Atalho | Alvo de lançamento | `LastPlayTime` em UTC | Estado observado |
|---|---|---|---|
| Honkai: Star Rail | `C:\Games\Star Rail Games\StarRail.exe` | 2026-07-23 01:24:32 | executável ausente nesse caminho |
| Genshin Impact | `C:\Windows\System32\wscript.exe` + `C:\SteamLaunchers\GenshinKeeperHidden.vbs` | 2026-10-23 00:11:29 | interpretador, VBS, CMD e executável final existem |
| Black Myth Wukong | `C:\Games Comp\Black-Myth-Wukong-SteamRIP.com\Black Myth Wukong\b1.exe` | 2026-08-04 00:05:01 | executável ausente nesse caminho |
| CONTROL Resonant | `%USERPROFILE%\Documents\Piratao\CONTROL-Resonant-SteamRIP.com\CONTROL Resonant\CONTROLResonant.exe` | 2026-10-12 00:56:32 | executável existe |
| Lies Of P | `%USERPROFILE%\Documents\Piratao\Lies-of-P-SteamRIP.com\Lies of P\LOP.exe` | 2026-10-03 17:56:15 | executável ausente nesse caminho |

Essas datas são valores armazenados pelo cliente, não sessões observadas pelo XGB_P. Caminho ausente não prova desinstalação global: o jogo pode ter sido movido ou reinstalado em outro lugar.

### Genshin: lançamento e sessão têm identidades diferentes

A leitura do VBS mostra que ele chama `GenshinKeeper.cmd`. O CMD declara como alvo:

`D:\Games\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe`

O arquivo existe. O script inicia esse executável e acompanha seu processo. Ele também prevê espera de inicialização e intervalo entre processos. **Nenhum script foi executado nesta investigação.**

Consequência para o desenho: `wscript.exe` não deve ser identificado como jogo; `LaunchTarget` e `RuntimeExecutables` precisam ser campos separados. Esta cadeia específica foi lida manualmente; não se propõe executar ou interpretar scripts arbitrários para descobrir jogos automaticamente.

## Observado: rastros do Windows

`HKCU\System\GameConfigStore\Children` contém **82 subchaves**. Apenas **12** expõem `MatchedExeFullPath` na coleta; **5** desses caminhos ainda apontam para arquivos existentes.

Entre os rastros estão executáveis internos de jogos, launchers e caminhos antigos. Eles ajudam a investigar correspondências, mas não fornecem uma lista validada de instalações ou uma ordem de últimos jogos. Datas de alteração de chave/arquivo não foram usadas como horário de partida.

## Bases já disponíveis no XGB_P

- `src/lib/windows/DesktopWindows.cs`: enumera janelas, lê caminho de processo com acesso limitado e valida ativação. O DTO ainda não expõe o caminho obtido internamente. PID/HWND servem à sessão, não à identidade persistente do jogo.
- `src/app/OpenApps.cs`: consulta janelas apenas enquanto a Home está visível. Reutilizar somente esse polling perderia sessões ocorridas com a barra oculta.
- `src/lib/hardware/Performance.cs`: RTSS informa PID/app e quadros atuais. Isso pode corroborar uma sessão conhecida; FPS ou presença no RTSS, isoladamente, não classificam um aplicativo como jogo.
- `HomePreview.cs`: jogo atual e recentes continuam fixos; tags dos recentes usam títulos e deverão passar a usar identificadores estáveis.

## Contratos públicos consultados

- [GetRecentlyPlayedGames — Valve](https://partner.steamgames.com/doc/webapi/IPlayerService#GetRecentlyPlayedGames): serviço remoto com chave de autenticação e SteamID. A proposta local não depende dele.
- [GetForegroundWindow — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow): obtém a janela em primeiro plano; pode retornar nulo durante transições. Não classifica jogos.
- [SetWinEventHook — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook): a opção `WINEVENT_OUTOFCONTEXT` entrega eventos sem mapear o callback no processo observado. É alternativa futura ao polling, não componente implementado nesta coleta.

## Inferências e pendências

- Steam nativa + atalhos externos é uma fonte inicial adequada ao workflow Big Picture deste projeto; não cobre automaticamente todo jogo instalado no PC.
- Histórico importado não prova execução neste PC, jogo aberto com sucesso ou tempo realmente jogado. A semântica e a atualização dos campos precisam de comparação antes/depois em sessão real.
- Identificadores de atalhos precisam preservar os valores sem sinal e distinguir o identificador armazenado do game ID usado no protocolo. O lançamento de atalhos externos ainda não foi validado nesta etapa.
- Capas locais podem ser procuradas em `appcache\librarycache` e no diretório de arte do perfil. A existência do primeiro cache foi observada; a resolução por jogo ainda não foi implementada/validada.
- Jogos abertos, retomada, modo Xbox, anti-cheat e associação de processos filhos não foram testados em runtime nesta investigação.

Implementação posterior autorizada: [ADR 0019](../decisoes/0019-jogos-recentes-locais-e-sessoes.md), aceita e integrada à prévia. A coleta acima preserva o estado anterior à implementação.

## Tracking e controle de processos (ADR 0024)

**Observado no código anterior** (`GameSessions.Update` antes de 2026-10-24):

- O destaque e a retomada exigiam uma janela que passasse pelo filtro do alternador: visível, com título e não cloaked.
- Processos sem `PROCESS_QUERY_LIMITED_INFORMATION` eram descartados.
- Com a janela fora do filtro, o jogo aparecia como fechado, e selecioná-lo podia gerar um novo lançamento.

**Implementado:**

- O jogo está aberto enquanto existir um processo dele. A janela é a última válida enquanto o handle pertencer ao mesmo processo.
- Também contam executáveis da pasta de instalação (exceto auxiliares) e, sem caminho legível, o nome de arquivo do runtime.
- A pausa é lida pelos threads (`ThreadState.Wait` + `ThreadWaitReason.Suspended` em todos).

**Nyrna** (repositório `Merrit/nyrna`, branch `main`, lido em 2026-10-24, GPL-3.0):

- `win32_process_repository.dart` usa `NtSuspendProcess`/`NtResumeProcess` com `PROCESS_SUSPEND_RESUME` e percorre os filhos por `CreateToolhelp32Snapshot`.
- `active_window.dart` minimiza antes de suspender (espera de 500 ms no Windows) e restaura ao retomar.
- `NativeLibrary.cpp` considera suspenso o processo com algum thread em espera por suspensão. O XGB_P exige todos os threads, o que é mais conservador: um thread criado suspenso pelo próprio jogo não marca pausa.

**Teste real:** só num `cmd /c ping` criado pelo teste. A árvore cmd → conhost/ping foi pausada, detectada, retomada e encerrada. Jogos reais não foram pausados nem fechados pelo agente.

**Inferido, não testado:**

- Uma prévia sem elevação não consegue `PROCESS_SUSPEND_RESUME`/`PROCESS_TERMINATE` nem enviar `WM_CLOSE` (UIPI) a um jogo elevado.
- Anti-cheats de kernel podem retirar esses direitos do handle ou registrar o acesso.

## Verificação após a implementação

Cache instalado `appcache/appinfo.vdf`: magic **0x07564429**, tabela com 4.338 strings, 2.266.998 bytes na leitura. O leitor suporta v40/v41 e consulta `appinfo/common/type` apenas dos IDs instalados; layout conferido na [fonte primária SteamAppInfo](https://github.com/SteamDatabase/SteamAppInfo/blob/master/README.md). Campos de token/hashes são ignorados, sem registro/exportação. Não há escrita nesse cache nem dependência da biblioteca externa.

Com o mapeamento local explícito do Genshin, `--verify-games-live` encontrou três recentes elegíveis: **Wuthering Waves, Genshin Impact e CONTROL Resonant**. O restante ficou classificado como ferramenta/indisponível. Leitura de processos e janelas foi exercitada; confirmação de sessões e comandos foram testados com fixtures. Não houve lançamento de jogos reais nesta etapa. [Guia de operação](../guias/jogos-recentes.md).
