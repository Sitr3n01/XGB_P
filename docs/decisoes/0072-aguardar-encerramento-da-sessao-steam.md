# 0072 — Aguardar o encerramento da sessão Steam antes de iniciar outro jogo

- **Status:** Aceita; publicada na 1.0.6 conforme a ADR 0073, validação física pendente
- **Data:** 2026-10-28
- **Revisa:** critério de liberação da abertura da ADR 0070; preserva fechamento normal, Home e proteção do controle.

## Contexto

Depois da publicação da 1.0.5, o usuário relatou a Steam aparecendo sobre o Neva após trocar de jogo. Pediu analisar os logs, recebeu a proposta de aguardar também a sessão Steam e autorizou: **“ok, pode corrigir”**.

No teste da 1.0.5, o Genshin foi confirmado fechado às 15:22:40 e 15:28:11; o Neva abriu às 15:22:43 e 15:28:13. Somente às 15:23:08 e 15:28:37 a Steam registrou `Game process removed` para o atalho do Genshin, mudou para ClientUI 769 e trouxe a janela exata do Big Picture à frente. O log do XGB_P confirma esse foco e, na segunda ocorrência, Neva oculto pelo shell antes do próximo PS. O registro prova foco/estado; não identifica por si só cada elemento desenhado na camada relatada.

`C:/SteamLaunchers/GenshinKeeper.cmd` mantém 25 leituras de ausência do processo antes de sair, para tolerar reinício durante a abertura. O VBS aguarda esse CMD. A Steam concluiu o acompanhamento depois do término nativo que a barra já havia confirmado. A coincidência se repete nas duas ocorrências e aponta para esse encerramento tardio como gatilho; a eliminação física do problema ainda exige teste da candidata.

## Decisão

1. **Separar processo nativo e sessão Steam.** O observador conserva sessões de jogos geridos pela Steam mesmo depois de desaparecerem do catálogo de processos. A barreira vale para toda nova abertura pela barra, inclusive recentes escolhidos após fechamento manual/nativo. Não há encerramento de lançadores nem alteração do `GenshinKeeper`.
2. **Usar eventos de ciclo de vida.** Ler `Game process added`, `updated` e `removed` do `console_log.txt`, correlacionados por AppID, comando e PIDs observados da sessão. O processo de runtime pode diferir do lançador: quando o jogo foi observado, a remoção precisa corresponder a um PID conhecido; na reconstrução inicial sem runtime observado, aceitar o encerramento do comando da sessão única, sem confundir PIDs conhecidos de sessões anteriores. Sobreposição ambígua do mesmo AppID não libera abertura.
3. **Conferir atualidade.** Leitura incremental em worker, até 256 KiB por lote, linhas completas/UTF-8, bootstrap até o fim do arquivo e identidade nativa do arquivo/cliente. Eventos anteriores à criação do cliente atual não são herdados. A interface só usa leitura posterior ao comando e com menos de cinco segundos; falha, atraso ou troca/truncamento do log durante sessão ativa impedem confirmação. Estado e histórico de identidades são limitados; perda de evidência não vira sessão encerrada.
4. **Esperar no máximo 45 segundos pela Steam**, depois de o fechamento nativo terminar. Mostrar “Aguardando a Steam concluir a sessão anterior…”; quando houve fechamento na mesma operação, prefixar “Jogo fechado.”. Aguardar mais 750 ms após a liberação observada e respeitar o ciclo Steam Input e soltura do botão, pelo limite existente de 500 ms. Só então despachar uma abertura.
5. **Cancelar com segurança.** Ao esgotar o prazo, informar que a Steam não confirmou e cancelar, sem abrir por cima nem forçar processos. Reabrir/navegar na barra, mudar de modo ou escolher outro app continua cancelando a escolha pendente. Isso não apaga a pendência real: uma nova tentativa consulta novamente o observador.
6. **Validar separadamente e publicar somente com autorização.** A correção foi primeiro preparada em candidata separada, com metadados 1.0.5 e hash próprio, preservando a entrega então publicada. Depois, o usuário pediu a versão 1.0.6 em `.exe`: a [ADR 0073](0073-versao-1.0.6-e-entrega-em-exe.md) registra a publicação com backup, após o encerramento normal pelo usuário. `bin/app`, caminho legado, configurações HidHide/Steam/DSX, lançadores, energia e FSE permanecem preservados. Sem commit/push.

## Consequências

Com o lançador atual, Genshin → Neva pode aguardar cerca de 25 segundos adicionais. O tempo é determinado pela confirmação, não por uma espera fixa. Sem sessão pendente e com observação atual, a primeira abertura permanece direta. Ferramentas da Steam fora da classificação de jogo, como DSX, não bloqueiam a fila.

Não há disputa de foco para recuperar um jogo já afetado nem mudança do atraso protetor do lançador. O próximo jogo inicia depois de a sessão anterior estar liberada; o teste físico precisa conferir se a Steam permanece fora da frente durante o intervalo que antes falhava.

## Implementação e evidências

- `src/lib/games/SteamSessionLog.cs`: parser e leitor incremental somente leitura, com identidade do arquivo e linhas parciais.
- `src/lib/games/SteamSessionRelease.cs`: estado das sessões, cliente, correlação/limites e confirmação independente do processo nativo.
- `src/app/SteamSessionObservation.cs`: aplicação de snapshots na UI e logs de confirmação/estado.
- `RecentGames.cs`: reutilização do inventário de processos do worker, observação contínua e barreira em toda abertura.
- `GameLaunchRouting.cs`: espera de 45 segundos, cancelamentos, estabilização e soltura do botão.
- `SteamSessionReleaseVerification.cs` / `DesignPreview.cs`: suíte dedicada e integração na bateria.

Evidências e candidata: regressões, replay de cópia dos logs, gate e preservação. Fixtures/replay não substituem FSE, Steam e controle reais. A publicação da 1.0.5 e seus registros anteriores permanecem históricos.
