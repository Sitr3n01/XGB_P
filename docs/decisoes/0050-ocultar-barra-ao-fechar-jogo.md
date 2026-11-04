# 0050 — Ocultar a barra após solicitar o fechamento de um jogo

- **Status:** Aceita
- **Data:** 2026-10-26
- **Complementa:** ADRs 0024, 0035 e 0039

**Revisão de 27/10:** a [ADR 0063](0063-transicao-e-retorno-entre-jogos.md) acrescenta retorno ao jogo sobrevivente após saída ativa confirmada no FSE. O comportamento sem redirecionamento descrito abaixo é o estado anterior dessa etapa; ocultação e respeito às confirmações continuam válidos.

## Contexto

O usuário confirmou que o fechamento funciona e pediu que a barra deixe de ficar em evidência após fechar um jogo. Ela permanecia aberta durante a espera pela saída, inclusive quando o jogo podia apresentar uma confirmação própria.

## Decisão

- Após o pedido de fechamento normal ou forçado ser aceito, ocultar a barra pelo caminho existente `Hide(true)`, respeitando o retorno de foco por identidade e a liberação pendente do Steam Input.
- Continuar confirmando a saída do jogo em segundo plano. Aceitar o comando não significa que o jogo já terminou.
- Se o jogo continuar aberto após o prazo, preparar a oferta de fechamento forçado para a próxima abertura manual. Não reabrir a barra automaticamente e não forçar sem confirmação.
- Cancelar, falha do pedido ou ausência de janela para fechamento normal mantêm a interface visível. Sem pedido aceito, a barra não se oculta por esse fluxo.
- A pausa e as APIs de fechar/forçar permanecem como estavam; esta decisão altera apenas a visibilidade após o comando.

## Consequências

O jogo fica visível para suas próprias confirmações. O retorno de foco usa a janela anterior, quando ainda é válida e a barra ainda tem o primeiro plano; não promete ativar outro jogo selecionado na lista. Havendo ciclo Steam Input pendente, o mecanismo existente torna a barra transparente e termina de ocultá-la após a liberação.

## Evidências

As verificações existentes de jogos e feedback cobrem ocultação após pedido normal/forçado, sucesso somente após observação válida posterior, timeout sem reabrir, erro visível e ausência de janela. Comandos e sessões são substituídos. Bateria completa aprovada em `bin/fechar-ocultar`; publicação, reversão e limites. Teste físico com jogo no novo build pendente.
