# Jogos recentes na prévia

Implementação da [ADR 0019](../decisoes/0019-jogos-recentes-locais-e-sessoes.md). Steam é o primeiro provedor automático, incluindo jogos externos adicionados à sua biblioteca. Catálogo e observador aceitam também jogos locais de qualquer origem; não existem provedores automáticos para as outras lojas nesta entrega.

## Uso

**Publicada na 1.0.6 (ADR 0072):** toda nova abertura aguarda também o encerramento da sessão Steam anterior, inclusive após fechamento manual. Com o lançador atual, Genshin → Neva pode aguardar cerca de 25 segundos. O prazo é 45 segundos; falha ou timeout cancela sem forçar processos. Guia atual e comandos do driver.

**Revisão publicada na 1.0.5 (ADR 0070):** as opções de iniciar outro jogo passam a **Fechar jogo e iniciar** e **Cancelar**, sem ícones. A troca pede fechamento normal e só inicia o destino depois de confirmar a saída. Confirmação própria, erro ou timeout impedem a abertura; nenhum encerramento é forçado automaticamente. No modo Xbox, fechar a barra sem jogo ou encerrar o último jogo retorna ao Início do Big Picture; PS continua abrindo a barra. Os trechos históricos abaixo sobre preservar sessões simultâneas descrevem as versões publicadas anteriores. Detalhes e teste físico.

Abra a prévia com o script habitual; PS/Win+G mostram/ocultam no modo Xbox, e Win+G também no desktop com a opção ligada (ADR 0048). A Home lê o histórico do perfil Steam selecionado e mostra até três jogos disponíveis. As imagens são extraídas automaticamente dos atalhos/executáveis, com símbolo de jogo como reserva (ADR 0021). Ferramentas e atalhos com executável ausente não preenchem os recentes.

O acompanhamento continua com a barra oculta, enquanto o processo da prévia está rodando. Pela [ADR 0049](../decisoes/0049-persistencia-de-sessoes-sem-foco.md), duas leituras consecutivas da mesma instância do executável de runtime (caminho exato e PID + criação) registram seu uso mesmo sem foco/janela. Polling normal a cada 2 s. Identificação só por nome ou outro executável da pasta continua exigindo duas leituras com foco; interpretadores/auxiliares não confirmam pelo novo caminho em segundo plano. Ausência do processo ou intervalo maior que 8 s reinicia a contagem. O registro permanece após fechar e reiniciar a prévia, sem gravação a cada tick. O XGB_P não inventa partidas anteriores à observação. Uma data importada é informação da Steam; o tooltip distingue essa origem da sessão observada no PC.

**Voltar ao jogo permanece na Home** (ADR 0020), mostrando o atual ou, sem janela aberta, o último jogo executado conhecido. Selecionar restaura a janela ou inicia o jogo novamente. Pela ADR 0025, esse jogo também lidera a lista de recentes, que mostra os três jogos mais recentes. Indisponível, conserva o nome com o cartão desabilitado; sem histórico, mostra “Nenhum jogo executado ainda”. Um recente já aberto também retoma sua janela. Um jogo fechado é iniciado pela Steam, conservando suas opções; um cadastro local usa seu executável/argumentos. Se outro jogo estiver aberto, a Home pede confirmação, inicialmente focada em **Cancelar**, sem fechar a sessão existente. ○/Esc cancela. Falha de despacho mantém a barra; após 180 s sem janela correspondente, a barra informa que não confirmou a abertura.

**Jogos abertos (ADR 0024):** "Voltar ao jogo" é sempre o jogo aberto com foco mais recente. Em "Jogos recentes", o destaque vem primeiro, depois os outros abertos, com "Em execução" ou "Pausado", e então os fechados. Só os jogos abertos ganham dois botões à direita:

| Botão | Efeito |
|---|---|
| ⏸ Pausar | minimiza o jogo e suspende o processo e os descendentes (técnica do Nyrna); o botão vira ▶ |
| ▶ Retomar | desfaz a pausa; "Voltar ao jogo" num jogo pausado também retoma e traz a janela |
| ✕ Fechar | pergunta antes (o foco começa em Cancelar) e pede ao jogo para fechar pela janela; se ele não sair em 10 s, oferece **Forçar fechamento** |

Ao descer de outra seção, o foco entra no cartão do jogo. Direita leva a ⏸ e depois a ✕. Jogos online podem desconectar durante a pausa. Jogos como administrador ou com anti-cheat podem negar pausar ou fechar; nesse caso, a barra mostra o motivo e nada muda. Ao encerrar, a prévia retoma os jogos que ela pausou. Se ela for morta à força, reabra a prévia e use ▶, ou encerre o jogo pelo Gerenciador de Tarefas.

O jogo continua "aberto" enquanto houver um processo dele, mesmo com a janela minimizada, pausada, "não respondendo" ou ainda carregando. Assim, "Voltar ao jogo" nunca inicia de novo um jogo que já está aberto. Também contam como o jogo outros executáveis da pasta de instalação Steam (exceto auxiliares como crash e setup) e, quando o Windows esconde o caminho do processo, o mesmo nome do executável de runtime.

## Cadastro e reversão

Os arquivos ficam em `%LOCALAPPDATA%\XGB_P\games\`:

| Arquivo | Uso |
|---|---|
| `catalog.json` | mapeamentos, correções e perfil opcional |
| `history.json` | últimos usos observados pelo XGB_P; criado somente após uma sessão confirmada |
| `history.json.bak` | versão anterior à última gravação atômica |
| `images/*.png` | cache regenerável dos ícones locais extraídos |

O cadastro pode ser editado com a prévia encerrada. Para reverter apenas esta funcionalidade, encerre a prévia e renomeie a pasta `games` para preservar os dados. Isso remove mapeamentos e histórico próprio na execução seguinte; o histórico original da Steam continua disponível. Para reverter a execução inteira, use **Encerrar prévia** na bandeja/janela.

Exemplo de cadastro para um executável de qualquer origem:

```json
{
  "Version": 1,
  "SteamProfile": null,
  "Entries": [
    {
      "Key": "local:meu-jogo",
      "Name": "Meu jogo",
      "Kind": "Game",
      "LaunchFile": "D:\\Jogos\\Meu jogo\\Game.exe",
      "Arguments": "",
      "WorkingDirectory": "D:\\Jogos\\Meu jogo",
      "RuntimePaths": ["D:\\Jogos\\Meu jogo\\Game.exe"],
      "RequiredFiles": [],
      "Hidden": false
    }
  ]
}
```

Ele entra nos recentes quando uma sessão for observada, mesmo que seja iniciado fora do XGB_P. Não recebe uma data fictícia só porque foi cadastrado.

Para corrigir uma entrada Steam, use a `GameKey` do diagnóstico em `Key`, por exemplo `steam:<perfil>:<appid>` ou `shortcut:<perfil>:<id>`. Campos omitidos preservam os metadados da origem. `RuntimePaths` deve conter o **executável do jogo**, não um interpretador genérico. `RequiredFiles` permite conferir uma cadeia de lançamento conhecida. `Kind` aceita `Game`, `Tool` ou `Unknown`; `Hidden: true` oculta uma entrada. Não declare a mesma runtime em dois jogos, pois a associação fica ambígua.

Nesta máquina foi acrescentado somente o mapeamento do Genshin já investigado. O cadastro não altera seus scripts ou os arquivos da Steam. Com várias contas e nenhuma seleção inequívoca, preencha `SteamProfile` com o nome numérico da pasta desejada em `Steam\userdata`; a seleção não faz login nem troca a conta conectada na Steam.

## Imagens automáticas

Todo jogo conhecido exibido na Home usa o mesmo resolvedor: imagem personalizada/atalho Steam → atalho correspondente no desktop → executável próprio do jogo → símbolo genérico. Também funciona para cadastros `local:`. A resolução usa caminho/URI, sem depender do nome do atalho. Não executa jogos nem baixa imagens.

Opcionalmente, acrescente `"IconLocation": "D:\\Imagens\\Meu jogo.png"` à entrada em `catalog.json`. Aceita PNG/JPEG/BMP/ICO ou recurso como `"D:\\Jogos\\Game.exe,0"`. Arquivo ausente ou inválido permite tentar as fontes seguintes. Para wrappers, a reserva procura a runtime mapeada, evitando o ícone genérico de WScript/CMD/Steam.

O cache normal fica em `games/images`, com PNGs 64×64; a Home mostra 32×32 no mesmo espaço anterior, preservando o tamanho dos cartões. Mudanças de caminho, tamanho, data ou índice da origem invalidam o cache. Para forçar nova extração, encerre a prévia e renomeie somente `images`, preservando `catalog.json` e `history.json`; na execução seguinte os ícones serão gerados novamente. Isso não desliga o resolvedor.

## Diagnóstico e validação

Com a prévia encerrada, execute a verificação completa:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validar-design.ps1 -Verificar
```

Diagnóstico dos jogos reais, sem lançamento ou gravação de sessão:

```powershell
Start-Process .\bin\XgbDesignPreview.exe -ArgumentList '--verify-games-live' -WindowStyle Hidden -Wait
```

Saídas em `bin/design-preview/`: `jogos-reais.txt` (inclui procedência da imagem), `home-jogos-reais.png`, `jogos-verificacao.txt` e `games-images-live-cache/*.png`. O diagnóstico não grava histórico ou cache fora de `bin`. O render usa fontes controladas nas outras seções; ele não documenta áudio/mídia/janelas reais.

Para validar runtime: com a prévia normal rodando, abra um jogo pela Steam, mantenha sua janela em primeiro plano por alguns segundos e abra a barra. Confira nome/retomada, minimizar, reabrir e encerrar. Depois confira persistência dos recentes ao reiniciar a prévia. Teste o lançamento pelo cartão e confirme o jogo real; despacho do protocolo e teste automatizado não garantem essa execução.
