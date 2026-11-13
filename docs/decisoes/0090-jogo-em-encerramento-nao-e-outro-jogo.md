# 0090 — Jogo em encerramento não conta como outro jogo aberto

- **Status:** Aceita. No código desde a versão seguinte à 2.1.0 (ainda não lançada); teste físico pendente.
- **Data:** 2026-11-04
- **Corrige:** o retorno ao Início da [ADR 0070](0070-fechar-antes-de-iniciar-e-retorno-ao-inicio.md) e a saída confirmada da [ADR 0063](0063-transicao-e-retorno-entre-jogos.md).

## Contexto

O usuário relatou que, às vezes, fechar um jogo não leva ao Início do Big Picture.

**Observado nos logs do app (04/11):**

- 10 retornos armados, todos com o Genshin. 7 chegaram ao Início e 3 foram cancelados (11:51:22, 12:16:25 e 15:29:54) com `outra janela ja esta em primeiro plano`.
- As 3 falhas foram fechamentos normais em que o jogo levou de 2,0 a 2,6 s para sair. O fechamento forçado (cerca de 0,5 s) e a saída pelo menu do jogo funcionaram sempre. O único fechamento normal que funcionou saiu em 1,57 s.
- Nas 3 falhas, a leitura de processos logo antes do cancelamento registra `Identidade do jogo da Steam ainda não confirmada`, com o Big Picture já na frente.
- As falhas aconteceram com os dois AppIDs do atalho (3899276376 e 3166698800): não dependem da ADR 0089.

**Causa (código):** enquanto encerra, o processo continua na lista do Windows, mas `QueryFullProcessImageName` falha. `GameProcess.Read` o registra só pelo nome, sem caminho e sem criação. A atividade do jogo passa a ter o mesmo PID com identidade desconhecida. `SameGameInstance` a trata como outra instância e `gameHasExited` responde "incerto". O retorno a conta como **outro jogo aberto**, descarta o Big Picture como destino e, com ele na frente, cancela.

O mesmo engano afetava:

- a saída pelo menu do jogo, que consumia a observação sem armar o retorno;
- a lista `KnownOthers` do retorno armado pela saída nativa;
- o "Fechar jogo e iniciar", que cancelaria com "A lista de jogos mudou".

## Decisão

1. **`SourceExiting(origem, candidata)`:** é a própria origem saindo quando a candidata tem a mesma chave e todos os processos dela são identidades conhecidas da origem, ou PIDs da origem lidos sem criação.
2. **Retorno após saída:** `OtherOpenGame` exclui essa cópia. O Big Picture que o shell trouxe continua aceito como destino, e o retorno espera a confirmação da saída.
3. **Confirmação do fechamento:** com a cópia ainda listada, a saída vale quando a identidade original confirma o término (no mesmo handle, como antes).
4. **Saída nativa:** com a cópia na leitura, a identidade original fica guardada e é conferida de novo na próxima leitura. A observação não é consumida.
5. **Fechar e iniciar:** a cópia não conta como jogo novo.
6. Um processo com a mesma chave e **outro** PID sem identidade continua contando como outro jogo (comportamento anterior).

## Consequências

- O fechamento normal lento volta ao Início como o forçado.
- Um jogo que deixa o processo preso como zumbi passa a ser confirmado como fechado quando a identidade original já terminou.
- A Home pode mostrar o jogo por uma leitura a mais, até a cópia sumir da lista do Windows.
- **Verificação:** 5 casos novos em `tests/GameTransitionVerification.cs`, com o fixture de saída respondendo "incerto" para a identidade desconhecida, como o código real. São eles: fechamento lento com o Big Picture na frente, confirmação com a cópia listada, outro PID sem identidade, saída nativa lenta (observação guardada) e saída nativa com a cópia listada.

## Evidências

Logs do app da máquina do autor (11:51, 12:16 e 15:29 de 04/11), no arquivo privado do projeto.
