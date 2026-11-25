# 0115 — Entrega do foco de entrada ao jogo

- **Status:** Aceita; candidata pública 3.1.0. Teste físico pendente.
- **Data:** 2026-11-25
- Complementa as ADRs [0046](0046-entrega-de-foco-antes-de-ocultar-a-barra.md) e [0063](0063-transicao-e-retorno-entre-jogos.md).

## Contexto

Depois de fechar a barra ou trocar de app, era preciso clicar com o mouse no jogo para ele voltar a receber a entrada. Um ensaio com duas janelas próprias e o código do projeto reproduziu o problema.

**Observado:**
- `SetForegroundWindow` aceito leva 15–16 ms para aparecer em `GetForegroundWindow`.
- O código lia a frente na hora, concluía que não houve entrega e repetia o pedido com `AttachThreadInput`.
- A combinação do pedido pendente com a fila deixou o destino em primeiro plano, mas sem janela ativa, sem foco e sem captura, em todas as 5 ocorrências. No único retorno entregue sem a fila, o foco veio normal.
- O pedido direto sozinho e a fila sozinha funcionaram (2/2 e 4/4).
- A confirmação da barra só comparava a janela da frente.

## Decisão

1. Depois de um pedido aceito, esperar a entrega, de 5 em 5 ms, por no máximo 150 ms. A fila só é usada se ela não vier. O pedido recusado segue para a fila sem espera.
2. 250 ms depois da entrega a um jogo, a um app ou ao fechar a barra, ler se a thread do destino tem janela ativa (`GetGUIThreadInfo`). Sem ela, e com o destino ainda na frente, devolver o foco de entrada uma vez. Barra reaberta, outra janela na frente ou leitura indisponível não mudam nada.
3. Registrar o resultado (`foco: entrada do app`), para o teste físico distinguir a rota.

Sem tecla simulada, sem clique e sem mudança de geometria.

## Consequências

- Com a correção, 8 de 8 retornos do ensaio vieram com foco e captura, entregues em até 5 ms de espera, sem a fila.
- 9 verificações novas (`tests/InputFocusVerification.cs`), com leitura e devolução do foco substituídas.
- Não validado: jogo real, modo Xbox, barra elevada e a rota do pedido recusado com jogo real. A devolução do foco recupera o teclado; a captura do mouse depende do jogo.

## Evidências

Ensaio local com janelas próprias, sem elevação, no desktop. Reversão: `AppForeground.cs` anterior e remover `InputFocus.cs` com as três chamadas.
