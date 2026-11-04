# 0024 — Pausar e fechar jogos abertos, com tracking que não perde o jogo

- **Status:** Aceita
- **Data:** 2026-10-24
- **Complementa:** ADRs 0019 e 0020

## Contexto

O usuário pediu, para a Home:

- pausar o processo de um jogo aberto, com base no [Nyrna](https://github.com/Merrit/nyrna);
- fechar o jogo;
- "Voltar ao jogo" sempre com o jogo aberto em foco;
- os outros jogos abertos com as mesmas opções;
- botões no mesmo padrão visual, à direita do jogo, só quando ele está aberto;
- um tracking que nunca deixe de ver um jogo aberto.

**Observado no código anterior:** o jogo só contava como "em execução" quando tinha uma janela visível que passasse pelos filtros do alternador. Um jogo pausado, "não respondendo", carregando ou com a janela oculta por instantes parecia fechado. Nesse caso, "Voltar ao jogo" pedia um novo lançamento pela Steam. Processos cujo caminho o Windows não entrega à prévia eram descartados.

**O Nyrna no Windows** (lido no repositório, licença GPL-3.0):

- minimiza a janela e espera cerca de 500 ms;
- chama `NtSuspendProcess` no processo e, de forma recursiva, nos filhos (pelo snapshot da Toolhelp);
- para retomar, chama `NtResumeProcess` e restaura a janela;
- considera o processo suspenso quando há threads em espera com o motivo `Suspended`.

O XGB_P usa a mesma técnica, com código próprio. Não houve cópia de código.

## Decisão

1. **Tracking** (`src/lib/games/GameSessions.cs`):
   - Um jogo está aberto enquanto existir um processo dele. A janela não é exigida.
   - Contam como processos do jogo:
     - o executável de runtime;
     - outro executável dentro da pasta de instalação, exceto auxiliares (crash, setup, CEF, anti-cheat, redistribuíveis, webview);
     - quando o Windows nega o caminho (por exemplo, por anti-cheat), um processo com o mesmo nome de arquivo do runtime.
   - A janela é escolhida nesta ordem: a do foreground, uma não minimizada, qualquer uma dos processos do jogo. Se ela sair da enumeração (pausada ou fantasma de "não respondendo"), continua sendo a do jogo enquanto o handle existir no mesmo processo (`DesktopWindows.Belongs`).
   - A identidade da sessão fica estável (PID + criação). O fim do processo encerra o jogo na hora, como antes.
2. **Destaque:** "Voltar ao jogo" é o jogo aberto com foco mais recente; sem foco registrado, o último aberto. Sem jogo aberto, vale a ADR 0020 (último jogo executado). Os outros abertos ficam no topo de "Jogos recentes", antes dos fechados, e nunca saem da lista. Pela [ADR 0025](0025-tres-recentes-com-o-destaque.md), o próprio destaque passou a liderar a lista.
3. **Botões:** pausar/retomar (⏸/▶) e fechar (✕) à direita do cartão. Seguem o padrão dos atalhos da Home: 52 px de largura, a altura do cartão, só ícone, nome na dica e na acessibilidade. Aparecem somente com o jogo aberto, no destaque e nos recentes. Ao descer de outra seção, o foco entra pelo cartão, nunca direto em pausar ou fechar.
4. **Pausar** (`src/lib/games/GameControl.cs`):
   - Minimiza a janela, espera 500 ms e suspende o processo e os descendentes. Um filho só entra se nasceu depois do pai, o que evita PID reaproveitado.
   - O estado "pausado" é lido dos threads a cada leitura. **Correção da ADR 0026:** vale a maioria dos threads (um thread preso no kernel só suspende ao voltar ao modo usuário), e a pausa feita pela própria prévia vale até a leitura mostrar zero threads suspensos. Por isso vale também para pausas feitas por outra ferramenta ou antes de reiniciar a prévia.
   - "Voltar ao jogo" num jogo pausado retoma o processo e traz a janela.
   - Ao encerrar, a prévia retoma os jogos que ela pausou, se ainda forem o mesmo processo.
5. **Fechar:**
   - Pede confirmação, que começa em Cancelar.
   - Envia o fechamento normal da janela (`WM_CLOSE`), o que permite ao jogo salvar ou pedir confirmação. Um jogo pausado é retomado antes.
   - Se o processo não sair em 10 s, a prévia oferece forçar o encerramento (`TerminateProcess` no processo e nos descendentes), também com Cancelar.
6. **Ações** só por escolha do usuário. Nenhuma sondagem de permissão em segundo plano: os handles de suspensão e encerramento só são abertos no momento da ação. Falhas viram mensagem, e o estado não muda.

## Consequências

- **Anti-cheat:** jogos com anti-cheat de kernel (por exemplo, Genshin Impact e Wuthering Waves) podem negar a pausa ou o encerramento, ou registrar o acesso como suspeito. Use a pausa por sua conta e prefira jogos offline. Jogos online podem desconectar enquanto estão pausados.
- **Jogos como administrador:** a prévia roda sem elevação. Jogos elevados (o Genshin pede administrador) devem negar pausar, forçar e o `WM_CLOSE` (isolamento UIPI). A mensagem explica o motivo; o XGB_P não tenta elevar.
- **Áudio** do jogo pausado para. Ao retomar, o jogo pode corrigir o tempo decorrido (inferido, depende do jogo).
- Executáveis dentro da pasta de instalação passam a contar como o jogo. Um utilitário próprio do jogo que fique aberto sozinho o mantém como "aberto".
- O lançamento é confirmado pelo processo de runtime observado, não mais pela janela.

## Evidências

- `--verify` (fixtures) cobre:
  - janela fora da enumeração e processo vivo sem janela;
  - processo sem caminho, pasta de instalação e auxiliar de crash;
  - destaque pelo foco e troca de foco entre dois jogos;
  - botões só nos abertos, altura e alinhamento;
  - navegação e pausa com minimização e descendentes;
  - voltar retomando, alternar pausa, confirmar e cancelar fechar, forçar após o prazo e saída confirmada;
  - pausa negada e jogo aberto sem janela que não é relançado.
- `--verify-games-control-live` exercita `GameControl` de verdade só num `cmd` com `ping` filho criado pelo próprio teste: árvore, pausa detectada pelos threads, retomada e encerramento.
- Renders e relatórios.
- **Não comprovado:** pausar ou fechar jogos reais (o agente não tocou em jogos), comportamento de cada anti-cheat, jogos em tela cheia exclusiva e retomada visual após longas pausas.
