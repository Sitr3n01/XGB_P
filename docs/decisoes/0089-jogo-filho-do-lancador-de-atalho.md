# 0089 — Jogo aberto como filho do lançador de um atalho externo

- **Status:** Aceita. Publicada no arquivo privado em 04/11 (2.0.2); neste repositório, na versão seguinte à 2.1.0 (ainda não lançada). Teste físico pendente.
- **Data:** 2026-11-04
- **Revisa:** em parte o item "não associar todo descendente de launcher a um jogo" da [ADR 0019](0019-jogos-recentes-locais-e-sessoes.md).

## Contexto

Em 04/11, o usuário trocou o alvo do atalho "Genshin Impact" na Steam: em vez da cadeia de scripts anterior, o atalho passou a abrir o unlocker de FPS (um executável próprio fora da pasta do jogo). O unlocker abre o `GenshinImpact.exe` como filho, numa pasta própria.

**Observado:**

- A Steam deu um novo AppID ao atalho: 3899276376 virou 3166698800. O mapeamento do `catalog.json` ficou preso à chave antiga.
- Sem mapeamento, o catálogo só reconhece o alvo do atalho, que não tem janela. O jogo, em outra pasta, fica fora da sessão.
- No log, "Voltar ao jogo" respondeu "janela ainda não está disponível" e o primeiro "Fechar" respondeu "não tem janela para fechar normalmente". Só o fechamento forçado funcionou, porque ele já encerra a árvore de processos.

Evidências no arquivo privado do projeto.

A ADR 0019 evitou associar descendentes de lançador a um jogo para não atribuir a um jogo os processos de um lançador genérico. O mapeamento explícito continua sendo o caminho preferido, mas ele quebra sempre que o alvo do atalho muda, e o usuário não tem como saber disso.

## Decisão

1. **Só atalho externo com alvo próprio.** `GameEntry.AdoptChildren` fica verdadeiro para um atalho da Steam cujo alvo existe e não é interpretador (`wscript`, `cscript`, `cmd`, `powershell`, `javaw`). Um mapeamento com `RuntimePaths` desliga a regra: o executável declarado vale sozinho, como na ADR 0019. Um mapeamento sem `RuntimePaths` (só nome, por exemplo) não muda nada.
2. **Quais filhos são adotados.** Entram os descendentes dos processos do alvo até 3 níveis (`GameProcessIndex.MaxChildDepth`) que atendam a todas estas condições:
   - nenhum jogo do catálogo reivindica o processo, nem de forma ambígua;
   - o caminho e a criação são legíveis;
   - o processo não está na lista de auxiliares (`GameFiles.Helper`: crash, setup, webview, anti-cheat e outros);
   - ele nasceu depois do pai vivo com aquele PID, o que barra PID reaproveitado.

   O pai vem de um snapshot Toolhelp (`GameControl.Parents`, o mesmo que `Tree` já usava).
3. **Janela.** A janela de um filho adotado só vale quando os processos do próprio alvo não têm janela. Nesse caso, o dono dela vira o processo principal (`Pid`), para que a pausa observada e o foco sigam o jogo, não o lançador.
4. **Sessão.** Os filhos adotados entram em `Pids` e `Processes`. Assim o foco no jogo conta como foco do atalho, e pausar e fechar alcançam lançador e jogo, como `CheckedTree` já fazia. A sessão guarda as instâncias adotadas (PID + criação). Elas continuam valendo se o lançador sair antes do jogo; outro processo com o mesmo PID não herda a adoção.

## Consequências

- Com a candidata, o Genshin pelo unlocker volta a ter janela na barra: "Voltar ao jogo" foca o jogo e o fechamento normal manda o pedido de fechar à janela dele. Isso vale sem mapeamento e também quando o atalho muda de novo.
- **Lançador que continua aberto.** O jogo só conta como fechado quando todos os processos da sessão saem. Com `AutoClose` desligado, o unlocker continua aberto depois do jogo. A barra continua mostrando o jogo como aberto, sem janela, e a Steam também. Recomendação: ligar o `AutoClose` do unlocker.
- **Lançador com janela própria.** Ela tem prioridade. Um lançador que fica visível ao lado do jogo continua recebendo o foco; para esse caso, o mapeamento explícito resolve.
- **Filho que não é o jogo.** Um filho sem dono, como um navegador aberto pela primeira vez pelo lançador, pode virar a janela do jogo se o alvo não tiver janela. O alcance é só o atalho externo sem mapeamento.
- O mapeamento entregue ao usuário (chave 3166698800 com o `GenshinImpact.exe`) continua válido e tem precedência: com ele, a regra fica desligada para esse atalho.
- `GameProcessIndex.For` não mudou. A equivalência com o oráculo (`ReleaseVerification`) continua a mesma. A adoção fica em `Descendants`.
- **Verificação:** `tests/GameLauncherVerification.cs` cobre:
  - lançador sozinho e a janela do filho;
  - processo principal, árvore e foco;
  - lançador que sai antes e PID reaproveitado;
  - filho mais antigo que o pai e filho sem pai conhecido;
  - prioridade da janela do alvo e filho reivindicado por outro jogo;
  - mapeamento, auxiliar e profundidade;
  - o catálogo: atalho, mapeamento com e sem executável, e wrapper.
