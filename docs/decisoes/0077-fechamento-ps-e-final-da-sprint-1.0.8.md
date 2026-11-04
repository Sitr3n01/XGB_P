# 0077 — Fechamento pelo PS e build final da sprint 1.0.8

- **Status:** Aceita; implementada, validada e publicada. Teste físico final pendente.
- **Data:** 2026-10-29

## Contexto

Após a primeira entrega 1.0.8, o usuário relatou que o DualSense não controlava a Steam mesmo com ela em foco. A análise somente leitura encontrou sete fechamentos pelo PS entre 11:01:16 e 11:01:53: o foco foi entregue de 22 a 109 ms antes da última soltura. O log da Steam passava por ClientUI 769 e voltava a desktop 413080/empty.vdf. Os nove ciclos da barra tinham recebido `/0`; não havia recusa do gate nessa sessão.

`ButtonsStillHeld()` considera os botões comuns, mas exclui o PS, acompanhado em `guideDownDevices`. O retorno ocioso a Home consultava apenas a primeira coleção. O usuário autorizou corrigir, manter a versão 1.0.8, documentar e fechar esta sprint. Relatou também ausência da animação de fechamento: `Dismiss()` desviava para `ReturnHomeWithoutGame()` antes da animação e o alternador ocultava a barra diretamente.

## Decisão

1. Preservar produto/arquivo/assembly 1.0.8/1.0.8.0 e identificar a revisão final por hash e manifesto, sem criar 1.0.9.
2. Fechar sem jogo usando a mesma animação de saída de 167 ms. Som e animação são executados uma vez; navegação/transferência de foco aguardam o término visual e a última soltura do PS físico e virtual.
3. A espera específica do PS não expira aos 500 ms dos outros botões. O pedido Steam conserva prazo total de oito segundos; sem conclusão cancela e restaura a barra, sem entregar pelo timeout. Dispositivo perdido não equivale a soltura. Novo PS durante despacho/foco pendente, reabertura, outro app ou mudança de contexto cancelam o pedido antigo.
4. Movimento reduzido elimina somente a animação. Foco negado, indisponibilidade e erro restauram a apresentação em vez de deixar a barra apagada. O retorno pendente continua bloqueando comandos do conteúdo.
5. Preparar cursor novo do log antes do despacho. Após foreground confirmado, observar ClientUI 769 estável por pelo menos 250 ms, com leituras recentes, dentro de 1,8 segundo. Essa etapa é somente observação em worker e não retém a navegação da Steam. Foco Windows e confirmação de entrada têm mensagens/logs distintos. Leitura inválida, perfil desktop/Guia, linha parcial, timeout ou outro foco não confirmam entrada. Não forçar 769 permanentemente nem disputar foco com outro app.
6. Publicar exatamente a candidata aprovada em `.exe` e `bin/app`, preservar a primeira 1.0.8 e a 1.0.7, manter a inicialização no mesmo caminho e congelar o build desta sprint por SHA-256/fingerprint.

## Consequências e limites

O caminho que devolvia foco com PS pressionado e pulava a saída foi corrigido. A confirmação posterior evidencia uma eventual divergência restante; ela não injeta comandos nem garante exclusividade global do controle. Ausência de confirmação termina com aviso e registro, sem novo perfil forçado. Validação física com Steam/DSX/DualSense e jogos permanece necessária.

“Final da sprint” significa escopo implementado e build identificado/validado, não certificação automática de hardware ou ausência absoluta de defeitos. Qualquer correção posterior exige nova evidência e novo build; não deve ser apresentada como o mesmo hash final.

## Evidências

Relatório final, evidências e recibos, guia. Bateria completa com 20 execuções, 31 verificações novas de retorno/saída e suites anteriores aprovadas. Sem nova versão, configuração de sistema, jogo/FSE novo ou energia real.
