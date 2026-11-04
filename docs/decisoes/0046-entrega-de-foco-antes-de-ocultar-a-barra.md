# 0046 — Entrega de foco antes de ocultar a barra

- **Status:** Aceita; implementação e bateria aprovadas, novo teste físico no FSE pendente
- **Data:** 2026-10-25
- **Complementa:** ADRs 0014, 0040 e 0045.

**Especialização de 27/10:** jogos passam pela [ADR 0063](0063-transicao-e-retorno-entre-jogos.md), com espera completa de Steam Input, estabilidade posterior à ocultação e recuperação limitada. O alternador genérico de apps continua seguindo esta decisão.

## Contexto

No teste do usuário com o build `FBA55332`, a lista passou a mostrar os apps escondidos pelo shell. Selecionar Firefox (16:16:00.704) e Claude (16:16:17.646) não trocou o app; ambos terminaram no aviso de ausência de confirmação, com a Steam novamente na frente. A barra estava elevada e o FSE foi confirmado entre 16:15:46 e 16:16:32.

O código fazia somente `SetForegroundWindow`, sem registrar seu retorno, e ocultava a própria janela imediatamente. Portanto, não preservava o contexto de foreground enquanto aguardava a entrega assíncrona. **O resultado nativo desses dois pedidos não foi registrado**: recusa do Windows e interferência da ocultação antecipada são hipóteses, não causas individualmente comprovadas.

## Decisão

1. Ao selecionar um app, manter a barra visível até observar o destino em foreground, visível e sem camuflagem. Ocultar sem devolver foco ao app de trás e conferir novamente antes de anunciar sucesso.
2. O backend tenta foco direto; sem observar o destino, liga brevemente a fila da thread atual à thread do destino, pede subida/foreground e desliga em `finally`. O mesmo tipo de ligação já era usado no retorno da barra, mas não no alternador. Retorno booleano da API não substitui a observação posterior.
3. Não acrescentar Alt, Alt+Tab, injeção ou escrita de camuflagem. A exceção do Alt neutro da ADR 0044 permanece apenas no caminho já autorizado de obtenção de foco da própria barra. Preservar a restauração de minimizadas somente por escolha explícita.
4. Fazer no máximo três pedidos por seleção, com a barra ainda visível/em foreground, mesmo painel e mesmo ciclo FSE; esperar até 1800 ms. Fora desse contexto, não disputar o foco. Fechamento/reabertura da barra, navegação, outra seleção ou ciclo FSE cancelam o pedido. Outro app escolhido é respeitado. Timeout não reabre/ativa a barra.
5. Registrar destino/popup, restauração, retorno direto, ligação/recusa da fila e foreground; registrar observações do alvo e da frente no pedido, repetição e desfecho. Assim, a próxima reprodução distingue recusa de foco, camuflagem persistente e falha após ocultar.

## Consequências

Na entrega lenta, a barra permanece com o feedback de alternância. Se o Windows negar, ela conserva o contexto do usuário e informa a ausência de confirmação. Não há alteração de modo Xbox, pacote, serviço, tarefa, HidHide, DSX ou Steam. As restrições nativas de foco continuam válidas; a correção precisa de teste real com os apps que falharam.

## Evidências

- Validação, build e limites e continuidade.
- [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow), [AttachThreadInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput). Não há uso de REA/análise de binários nesta revisão.
