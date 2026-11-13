# Arquitetura do XGB_P · V.3.0.0

Aplicativo residente WPF, x64, em C# 5 sobre o .NET Framework 4.8 que vem com o Windows. Um único processo, sem serviço, driver ou runtime extra. A interface é uma `partial class Preview` em `src/app`; as integrações com o sistema ficam em bibliotecas por área em `src/lib`.

## Componentes

| Componente | Responsabilidade |
|---|---|
| `src/app` | Bandeja, janela da barra, painéis, política de ativação, foco, Steam Input, jogos, teclado virtual, animação e sons |
| `src/lib/dualsense` | Leitura HID somente leitura do DualSense (USB e Bluetooth, CRC), gestos e bateria do controle físico |
| `src/lib/gaming` | API oficial do modo Xbox (`IsGamingFullScreenExperienceActive`, notificação de mudança, saída do modo) |
| `src/lib/games` | Catálogo (Steam, atalhos e lojas), associação de processos, sessões, histórico, imagens e ações serializadas |
| `src/lib/windows` | Janelas e foco, alternância de apps, teclado (`SendInput`), energia do sistema, inicialização, log da ponte opcional |
| `src/lib/audio`, `src/lib/media` | Core Audio (dispositivos, volume, mixer) e sessões de mídia do Windows (SMTC) |
| `src/lib/hardware`, `src/lib/network`, `src/lib/display` | Métricas (HWiNFO, RTSS, modo de energia), estado da rede e rádios, Luz noturna |
| `src/lib/amd` | Captura do Radeon ReLive pela ferramenta de comandos da AMD e ajustes globais pelo ADLX |
| `src/lib/tv`, `src/lib/power` | TV Tizen local, credenciais DPAPI, fila/política de energia, observação de retomada e CycleId |
| `tests/` | Suítes de verificação, compiladas só no `XGB_P.Tests.exe` |

## Ativação

A API oficial decide entre modo Xbox e desktop, e **estado incerto bloqueia** ([ADR 0037](decisoes/0037-ps-e-wing-somente-no-modo-xbox.md)).

| Contexto | Comportamento |
|---|---|
| Modo Xbox confirmado | PS e a bandeja abrem e fecham a barra; Win+G também, se a ponte local existir |
| Desktop confirmado | O PS fica com a Steam; Win+G só abre se o usuário ligar a opção e a ponte existir |
| API incerta | Não abre |

O PS é lido direto do HID. Quando a Steam também recebe o PS, a barra abre na soltura e coordena o ciclo de Steam Input; quando só o controle físico o recebe, abre num toque curto, sem ciclo ([ADR 0042](decisoes/0042-ps-pelo-controle-fisico-e-abertura-ao-soltar.md)). Win+G chega por uma ponte local opcional que não faz parte deste repositório: o app só lê o log dela, e sem o log nada acontece ([ADR 0002](decisoes/0002-identidade-do-pacote-da-game-bar.md)).

## Foco e Steam Input

No modo Xbox, o shell mostra uma janela por vez e recusa foco a um app aberto por leitura HID. A barra é uma janela de ferramenta (`WS_EX_TOOLWINDOW`), pede o foco com `AttachThreadInput` e guarda a identidade da janela anterior para devolver o foco a ela ([ADR 0040](decisoes/0040-foco-da-barra-e-camada-uiaccess.md), [ADR 0046](decisoes/0046-entrega-de-foco-antes-de-ocultar-a-barra.md)). Como Steam e DSX rodam elevados, a opção "Sempre executar como administrador" é necessária para o foco funcionar sobre eles.

A Steam continua lendo o controle com a barra aberta. Ao abrir sobre um app ou jogo gerido pela Steam, a barra despacha `steam://forceinputappid/413080` (layout de desktop) e só libera a navegação quando o log da Steam confirma; ao fechar, libera com `/0` ainda na frente, porque a liberação só vale na próxima troca de foco ([ADR 0041](decisoes/0041-layout-de-desktop-da-steam-ao-abrir.md)). O ciclo espera a última soltura do PS, exige linhas novas e completas do log, e erro mantém o bloqueio ([ADR 0074](decisoes/0074-transferencia-rapida-do-steam-input-apos-soltar-ps.md)). Uma fila única ordena barra e teclado; gerações canceladas não liberam uma abertura nova.

Início e Biblioteca são pedidos explícitos ao Big Picture. Com o cliente da Steam reconhecido (caminho, processo, sessão e hash), um canal local entrega o comando em dezenas de milissegundos; cliente desconhecido usa o protocolo `steam://` ([ADR 0059](decisoes/0059-despacho-local-da-navegacao-steam.md)). Esse canal não é contrato público da Valve.

## Jogos

O catálogo junta bibliotecas da Steam, atalhos e lojas instaladas. Sessões são associadas por runtime exato com PID e horário de criação, e o histórico persiste sem gravar a cada ciclo ([ADR 0049](decisoes/0049-persistencia-de-sessoes-sem-foco.md)).

- **Pausar, retomar e fechar** conferem identidade no mesmo handle e rodam em transação serializada. Um diário grava a intenção antes da pausa e permite retomar os jogos na próxima abertura, se o app cair ([ADR 0024](decisoes/0024-pausar-e-fechar-jogos-abertos.md), [ADR 0056](decisoes/0056-estabilizacao-e-candidata-local.md)).
- **Trocar de jogo:** abrir um jogo fechado com outro aberto pede confirmação, fecha normalmente, espera a saída do processo e o fim da sessão na Steam (até 45 s) e só então inicia ([ADR 0070](decisoes/0070-fechar-antes-de-iniciar-e-retorno-ao-inicio.md), [ADR 0072](decisoes/0072-aguardar-encerramento-da-sessao-steam.md)). Erro ou prazo esgotado cancelam; nada é forçado.
- **Retorno:** no modo Xbox, fechar o jogo ativo entrega o sobrevivente usado mais recentemente; fechar o último volta ao Início do Big Picture ([ADR 0063](decisoes/0063-transicao-e-retorno-entre-jogos.md)).

## Operações, dados e energia

- Preferências em `%LOCALAPPDATA%\XGB_P\settings`, com gravação atômica e backup; catálogo, histórico e diário de pausas em `games`; histórico de ações em `history`.
- Suspender, reiniciar e desligar exigem uma segunda confirmação consumida uma única vez. Operações em andamento, teclado e Steam Input bloqueiam a ação; jogos pausados pelo app são retomados antes do pedido ([ADR 0062](decisoes/0062-preparacao-da-versao-1.0.1.md)).
- Toda operação externa tem prazo e mensagem de erro própria; não existe sucesso por timeout ([ADR 0035](decisoes/0035-feedback-vinculado-as-operacoes.md)).
- Log de uso em `logs\xgb.log` ao lado do executável, com rotação.

## Apresentação

As transições medem o conteúdo no tamanho final e animam por superfícies e recorte, sem redimensionar o texto a cada passo. Cópias decorativas são vetoriais e limitadas; os temporizadores de apresentação só rodam quando necessários ([ADR 0057](decisoes/0057-apresentacao-com-custo-limitado.md)). Observadores, filas e caches têm limites e cancelamento. Movimento reduzido do Windows prevalece.

## TV e retomada

O host usa o leitor HID existente e eventos de energia. A integração importa configuração local ou carrega `%LOCALAPPDATA%\XGB_P\tv`, sem um alvo fixo. Automação é optativa; rede não confirma energia e o toggle não se repete depois de entrega possível. Registros por ciclo ficam em `wake-trace`; PS pós-retomada não comprova a causa física. Ver os guias da [TV](guias/tv-samsung-integrada.md) e do [despertar](guias/despertar-pelo-ps.md).

## Build e verificação

- `XGB_P.exe` (produto) e `XGB_P.Tests.exe` (verificação) são compilados dos mesmos fontes de `src`. A build de verificação soma `tests/` e o símbolo `XGB_VERIFY`; o produto recusa qualquer `--verify*`.
- Cada execução de verificação grava um recibo com GUID, modo, SHA-256 do executável e janela UTC. O gate recusa recibos antigos, de outra build ou adulterados, e recusa o resultado se os fontes mudarem durante a bateria.
- Detalhes em [desenvolvimento](guias/desenvolvimento.md) e na [política de qualidade](../quality/README.md).
