# 0028 — Luz noturna e atalho do DSX nas Configurações; contorno vermelho em fechar

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu três coisas:

- ligar e desligar o filtro amarelo do Windows (Luz noturna) pela aba Configurações;
- um botão que leve ao DSX, para ajustar o controle quando precisar;
- um contorno vermelho nos botões de fechar apps e jogos, com o mouse ou o seletor em cima.

**Observado nesta máquina, só com leitura:**

- **Luz noturna:** ligada. O Windows não tem API pública para ela. O estado fica no CloudStore do usuário: `HKCU\…\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\…`, valor `Data`, com 43 bytes. Detalhes em registro.md.
- **DSX:** app da Steam 1812620 (`appmanifest_1812620.acf`). O `DSX.exe` da raiz é o launcher: pelo log dele, ele inicializa a Steam API, pega um ticket de autenticação e abre `Main_v3_Beta\DSX.exe` por uma tarefa agendada temporária. Abrir o executável principal direto pularia essa autenticação.

## Decisão

1. **Seção "TELA"** nas Configurações, logo depois de Conexões, com a linha **Luz noturna**:
   - mostra "Ligada" (em verde), "Desligada", "Aplicando…", "Indisponível" ou "Lendo…" e alterna ao selecionar, como os rádios;
   - o estado é lido junto com as Conexões (a cada 3 s, só com Configurações aberto);
   - a escrita (`src/lib/display/NightLight.cs`) só acontece com o blob no formato conhecido. Ela insere ou remove `10 00`, ajusta o tamanho e grava um carimbo de tempo mais novo, como fazem as ferramentas conhecidas. Fora do formato, a linha fica desabilitada e nada é escrito;
   - só muda a chave do usuário (HKCU), sem elevação. A agenda da Luz noturna e a intensidade não são tocadas.
2. **Seção "CONTROLE"** com **DSX · ajustes do controle**:
   - com o DSX aberto e janela visível, traz a janela para frente pelo mesmo caminho do alternador de apps, com a confirmação de foreground;
   - fechado, só na bandeja ou sem leitura das janelas, abre `steam://rungameid/1812620`, o mesmo que "Jogar" na biblioteca;
   - a barra se esconde. Um erro da Steam vira aviso, e a barra continua aberta;
   - só conta um `DSX.exe` dentro de `steamapps\common\DSX\`.
3. **Botões de fechar** (✕ dos jogos abertos no destaque e na lista e X de fechar apps na faixa) usam o estilo **`CloseAction`**:
   - contorno vermelho `#E81123` (o vermelho do fechar das janelas do Windows) com o mouse em cima ou com o foco;
   - o brilho breve do foco também fica vermelho.
   
   Pausar, as confirmações e os outros botões continuam com o verde.

## Consequências

- A Luz noturna depende de um formato não documentado. Se a Microsoft mudar o blob, a linha mostra "Indisponível" em vez de escrever algo errado.
- **Inferido, não testado:** que o Windows aplica a mudança na hora, ao ver o valor novo. O agente não alterou a Luz noturna real.
- Abrir o DSX pela Steam com ele já na bandeja depende do comportamento do próprio DSX, como ao clicar em Jogar.
- As novas seções deixam a página principal de Configurações um pouco mais longa. Ela continua rolando como antes.

## Evidências

- `--verify` (com substitutos), em `configuracoes-extras-verificacao.txt`, cobre:
  - o blob: ligado observado, desligar e religar em memória, carimbo mais novo, mesmo estado sem mudança e formato desconhecido recusado;
  - a Luz noturna: ligar/desligar, foco, verde, erro, indisponível e leitura pendente;
  - o DSX: aberto vem para frente, fechado ou com a leitura falhando abre pela Steam, erro da Steam e `DSX.exe` fora da Steam ignorado;
  - o contorno vermelho com o mouse (estilo) e com o foco (jogos e apps, na verificação da Home), com o pausar continuando verde.
- `--verify-connections-live` (só leitura) lê o estado real da Luz noturna e faz a ida e volta do blob real só em memória. Também informa se há processo e janela do DSX, sem abrir nada.
- Renders e relatórios.
- **Não comprovado:** ligar/desligar a Luz noturna de verdade, abrir o DSX pela barra e o contorno com o mouse real.
