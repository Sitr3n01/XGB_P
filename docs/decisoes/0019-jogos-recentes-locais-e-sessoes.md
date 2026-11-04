# 0019 — Jogos recentes locais e sessões observadas

- **Status:** Aceita; implementação na prévia autorizada pelo usuário em 2026-10-23
- **Data:** 2026-10-23

**Revisão pela ADR 0049:** a persistência também aceita duas leituras consecutivas da runtime exata com PID + criação, sem foco. Nome/pasta apenas mantêm a exigência de confirmação por foco. Não há histórico retroativo.

**Revisão pela ADR 0020:** Voltar ao jogo permanece com o atual ou o último jogo executado conhecido, mesmo fechado, e o exclui dos recentes. A descrição original de ocultar o cartão sem janela foi substituída.

## Contexto

O usuário pediu uma lógica de identificação dos jogos recentes do PC, baseada no workflow do projeto. A Home prevê três recentes, sem repetir o jogo atual, mas ainda usa exemplos (ADRs 0010 e 0016).

A investigação encontrou jogos Steam, ferramentas na biblioteca, atalhos externos com caminhos ausentes e Genshin iniciado por script. Apenas ordenar manifestos ou processos não atende a esses casos. [Fontes e limites observados](../referencia/jogos-recentes.md).

## Decisão

### 1. Separar catálogo, disponibilidade, atividade e histórico

Criar uma biblioteca reutilizável `src/lib/games/`, em C# 5/.NET Framework, sem SDK, NuGet, serviço, administrador ou dependência do RTSS. A prévia apenas consome snapshots gerenciados, como os outros painéis.

| Componente proposto | Responsabilidade |
|---|---|
| `SteamCatalog` | descobrir bibliotecas, ler manifestos, perfil selecionado, histórico e atalhos externos |
| `GameCatalog` | identidades estáveis, classificação, caminhos/aliases, disponibilidade e metadados |
| `GameSessions` | associar janelas/processos aos jogos conhecidos e acompanhar sessões |
| `RecentGamesStore` | histórico próprio, procedência e persistência local |
| `RecentGames` na prévia | apresentação, foco, retomada e lançamento solicitado pelo usuário |

Cada jogo precisa de `GameKey`, origem/perfil, nome, classificação (`Game`, `Tool`, `Unknown`), alvo de lançamento, executáveis de runtime, disponibilidade, arte opcional e evidências de último uso. PID/HWND não são persistidos como identidade. Homônimos não são unificados por título; cópias em lojas diferentes preservam a origem, salvo associação explícita.

### 2. Steam como primeiro provedor, incluindo jogos externos

- Descobrir a instalação pelo registro e bibliotecas pelo `libraryfolders.vdf`, sem fixar C: ou D:.
- Ler `appmanifest_*.acf` para nome/AppID/pasta. Manifesto e pasta existentes indicam instalação candidata; não garantem executável íntegro ou lançamento bem-sucedido.
- Ler `localconfig.vdf` para o histórico inicial dos apps e **VDF binário** `shortcuts.vdf` para jogos externos e seu `LastPlayTime`. Usar parsers com estrutura/limites, não regex como parser definitivo.
- Não assumir que `ActiveProcess` é atual: validar o processo e o perfil; sem cliente conectado, usar último perfil validado/cache identificado. Se houver ambiguidade, apresentar escolha de perfil, sem misturar históricos.
- Classificar ferramentas e redistribuíveis; excluir explicitamente DSX, Wallpaper Engine e Steamworks Common Redistributables no primeiro recorte desta máquina. Metadados de tipo ainda não validados não viram classificação automática. Permitir correção local de casos desconhecidos.
- Separar alvo inicial de executável real. Genshin recebe mapeamento explícito para o executável identificado; não associar todo `wscript.exe`, `cmd.exe`, `javaw.exe` ou todo descendente de launcher a um jogo.
- Atalho com alvo ausente fica indisponível, sem apagá-lo da Steam. Wrapper só fica pronto quando a cadeia conhecida e o executável final também estiverem disponíveis. Indisponibilidade de unidade é distinta de remoção confirmada.
- Jogos fora da Steam poderão usar cadastro local explícito de executável/launcher. Provedores Epic/Xbox/outros são expansões separadas; a primeira entrega não promete inventário universal.

### 3. Histórico importado e sessões observadas preservam procedência

Guardar separadamente a última indicação importada da Steam e a última sessão observada localmente. Para ordenação, usar a evidência válida mais recente, conservando fonte e confiança. Data importada não deve aparecer como sessão confirmada neste PC.

O observador roda enquanto a prévia manual estiver aberta, inclusive oculta. No produto, respeita o modo Xbox; no desktop fica inativo. Importar histórico existente não exige monitoramento permanente no desktop. Sessões com XGB_P desligado só podem ser recuperadas pelas fontes externas disponíveis; não inventar histórico retroativo.

Fluxo proposto:

1. Snapshot de processos/janelas em worker, inicialmente a cada cerca de 2 s. Corresponder caminho normalizado do executável aos aliases conhecidos; título, tela cheia ou uso de GPU não bastam.
2. Associar inicializador e processo principal somente com evidência específica. A existência do launcher é estado de inicialização, não partida confirmada.
3. Confirmar atividade por janela de jogo correspondente e primeiro plano observado; estabilizar por duas leituras como parâmetro inicial a validar. Um jogo já minimizado pode ser identificado como aberto, sem fabricar uma nova partida ou sua data de início.
4. Preservar a última janela de jogo válida **antes de a barra tomar o foco**. Abrir o XGB_P, Alt+Tab ou minimizar não encerra a sessão nem redefine o jogo atual como a própria barra.
5. Identificar instâncias por PID + criação do processo quando disponível, revalidando caminho e janela; acesso negado é estado desconhecido, não motivo para elevar automaticamente.
6. Encerrar a sessão quando os processos de runtime conhecidos terminarem, com margem curta configurável para recriação. Começar com cerca de 10 s e validar por jogo; não acompanhar o wrapper indefinidamente. Suspensão/reboot não entram como tempo jogado.

RTSS pode reforçar a associação de PID conhecido. Não é requisito nem classificador. Não ler memória dos jogos, injetar código ou modificar launchers.

### 4. Ordenação e comportamento da Home

- Selecionar até três jogos classificados, disponíveis para lançamento e com data válida, ordenados por último uso decrescente. Datas zero/ausentes não viram recentes; futuras ou inválidas são descartadas/sinalizadas no diagnóstico. Desempate determinístico por `GameKey`.
- Excluir o jogo apresentado em **Voltar ao jogo**. Com vários jogos abertos, priorizar o último jogo efetivamente usado antes da barra; os demais continuam acessíveis na faixa de apps.
- Não preencher vagas com exemplos ou simplesmente com jogos instalados. Sem histórico, mostrar o estado vazio e Biblioteca.
- Preservar foco por `GameKey`, inclusive durante atualização, e evitar reordenar a lista sob uma seleção em andamento. Resolver a ação pela identidade atual no clique, nunca pelo índice antigo.
- Arte: tentar arte local da Steam/atalho, depois ícone do executável e símbolo genérico. Nenhum download necessário.
- Datas em UTC no armazenamento e conversão para a hora local na apresentação.

**Voltar ao jogo** passa a restaurar/ativar a janela validada usando o backend existente, com confirmação de foreground. Sem sessão/janela confiável, ocultar a ação em vez de manter Forza de exemplo.

Selecionar um recente já aberto retoma sua janela. Para jogo fechado, solicitar lançamento pela Steam (`steam://rungameid/...`) e preservar as opções do cliente; atalhos externos exigem validação específica do game ID. Despacho aceito é estado **Iniciando**, não prova de jogo aberto. Observar o processo real para confirmar e liberar nova tentativa em falha/timeout. Impedir despachos duplicados enquanto houver uma solicitação pendente.

Se outro jogo estiver aberto, seguir o estado previsto em `design/home.md`: confirmar a intenção de abrir outro, sem encerrar automaticamente a sessão existente. Um jogo que se tornar indisponível entre listagem e clique recebe mensagem e acesso à biblioteca/detalhes.

### 5. Persistência e atualização

Propor `%LOCALAPPDATA%\XGB_P\games\` para catálogo/cache e histórico versionado, gravados atomicamente. A implementação deve registrar os caminhos e a reversão em `estado-da-maquina.md` quando passar a gravá-los. Nenhum arquivo externo foi criado nesta investigação.

Não gravar nos arquivos da Steam. Recarregar catálogo ao iniciar/abrir a Home e por mudança relevante, com debounce e leitura tolerante a arquivo temporariamente incompleto. Não reler toda a biblioteca a cada tick de sessão. Falha de leitura preserva o último snapshot com estado de indisponibilidade; não apaga o histórico nem confirma desinstalação.

## Consequências

- Mantém Big Picture/Steam responsáveis pelo lançamento e a Home compacta; não cria outra biblioteca completa.
- Cobre jogos externos já integrados ao workflow, mas exige distinguir wrapper e jogo e tratar atalhos antigos.
- Histórico inicial funciona sem conta Web/API key. Os formatos locais da Steam são dependência de compatibilidade e precisam de tratamento de falhas.
- Recentes da Steam podem incluir atividade que o XGB_P não observou; a procedência fica explícita no modelo e nos diagnósticos.
- Não modifica pacote Microsoft, HID, serviços, tarefas, segurança ou inicialização. Nenhum commit/push está autorizado por esta proposta.

## Evidências e sequência de implementação

1. Implementar leitor e diagnóstico **somente de leitura**, com fixtures sintéticas de VDF textual/binário, identificadores sem sinal, bibliotecas múltiplas, datas inválidas, ferramentas e arquivos ausentes. Comparar o resultado com o inventário desta coleta.
2. Integrar recentes reais à prévia, com histórico importado, arte/fallback, foco estável e estados vazios. Validar por render e navegação; comandos de lançamento substituídos na verificação automatizada.
3. Implementar observador de sessões e retomada. Validar abertura real a partir da Steam, jogo minimizado, troca de foco, launcher intermediário, processo recriado, jogo encerrado e reinicialização da prévia. Comparar campos Steam antes/depois para entender a atualização das datas.
4. Ativar lançamento de recentes após validar identidade/protocolo dos atalhos externos, confirmação de outro jogo aberto e resultado observado. Atualizar guia, requisitos, continuidade e estado da máquina conforme os efeitos realmente introduzidos.

As sessões reais exigem jogos abertos durante a validação. O produto residente e os spikes de overlay/input no modo Xbox continuam separados.

## Implementação e validação — 2026-10-23

- Criados `src/lib/games/ValveData.cs`, `GameCatalog.cs` e `GameSessions.cs`, integrados à Home por `RecentGames.cs`. Build C# 5 sem SDK/NuGet; referência adicional apenas a `System.Runtime.Serialization` do Framework.
- A classificação nativa usa `common/type` do cache local **appinfo v40/v41**, além das exclusões conhecidas; sem tipo disponível fica `Unknown`, corrigível por cadastro explícito. Não depende de uma lista fixa de jogos. Formato conferido contra o [projeto SteamAppInfo](https://github.com/SteamDatabase/SteamAppInfo/blob/master/README.md) e a versão v41 instalada.
- Atalhos externos recebem game ID de 64 bits derivado do identificador sem sinal armazenado. O Genshin tem mapeamento explícito no cadastro **local desta máquina**, preservando o lançamento via Steam e a cadeia VBS/CMD existente. Scripts não são interpretados/executados pelo leitor.
- Cadastros `local:` permitem acompanhar/iniciar executáveis de qualquer origem, mesmo sem Steam instalada. Provedores automáticos Epic/Xbox/GOG/etc. não foram implementados nesta entrega. Instruções e exemplo em [guia de jogos](../guias/jogos-recentes.md).
- Conforme solicitação posterior do usuário, o ícone de todos os recentes e da retomada foi padronizado no símbolo de jogo (`Segoe MDL2 Assets`, U+E7FC). Não se procuram/baixam capas nesta versão; revisa o item de arte proposto acima.
- O observador roda a cada 2 s, também com a prévia oculta. Releitura do catálogo em worker ao abrir e a cada 60 s; não há watcher de arquivos nesta versão. Caminhos compartilhados por jogos distintos são ambíguos e não confirmam uma sessão automaticamente.
- Duas leituras de foreground confirmam a sessão; mudar efetivamente entre jogos renova o último uso. Perder foco para a barra não renova a data. A instância usa PID + criação; retomada revalida também caminho e criação do processo. Uma lacuna de 10 s tolera troca interna de processo; não é calculado tempo jogado.
- Persistência de eventos observados em `history.json`, sem copiar o histórico completo da Steam. Escrita atômica com `.bak`; JSON ilegível preserva o arquivo e desabilita sua sobrescrita naquela execução.
- `catalog.json` guarda mapeamentos/correções; o snapshot descoberto é mantido em memória nesta versão. Não há cópia persistente da biblioteca Steam nem restauração automática do catálogo se as fontes estiverem ausentes ao iniciar.
- Home sem exemplos: até três recentes elegíveis, retomada apenas com janela de jogo, foco estável e ordenação preservada durante a seleção. Abrir outro jogo exige confirmação navegável pelo controle. Lançamentos duplicados ficam bloqueados durante até 180 s; processo com janela confirma abertura, erro/timeout permite nova tentativa.
- `-Verificar` passou: parsers, appinfo v40/v41, IDs, bibliotecas, ferramentas, wrapper, jogo local sem Steam, caminhos ausentes, persistência/corrupção, PID reutilizado, sessão/minimização/recriação, UI/foco/ícone, retomada, confirmação, despacho substituído, falha e timeout. Evidência `bin/design-preview/jogos-verificacao.txt`.
- `--verify-games-live` leu os arquivos reais sem iniciar jogos nem salvar eventos. Encontrou **Wuthering Waves, Genshin Impact e CONTROL Resonant** elegíveis; três atalhos com alvo ausente e ferramentas não aparecem nos recentes. Artefatos `jogos-reais.txt` e `home-jogos-reais.png`.
- **Limite:** lançamento real, retomada com jogo, controle físico e uso no modo Xbox ainda precisam da validação do usuário. Os comandos externos dos testes foram substituídos; nenhuma partida foi iniciada automaticamente.
- Resultados e renders finais preservados em evidências de jogos, sem builds. A resolução do launcher nativo usa `config/launch` do appinfo, evitando considerar um auxiliar arbitrário como instalação pronta após remover o alvo principal.
