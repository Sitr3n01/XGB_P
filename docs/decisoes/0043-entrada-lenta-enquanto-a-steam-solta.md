# 0043 — Entrada lenta enquanto a Steam solta o controle

- **Status:** Substituída na candidata pela [ADR 0074](0074-transferencia-rapida-do-steam-input-apos-soltar-ps.md); publicados anteriores preservados. Animação de 250 ms e bloqueio dependente da confirmação, sem liberação por timeout. Evidências abaixo são históricas.
- **Data:** 2026-10-25
- **Complementa:** ADRs 0022 (animações), 0041 e 0042.

## Contexto

Quando o PS também chega à Steam (o PS sozinho ainda não está silenciado no DSX), o Big Picture recebe o controle por cerca de 1,1 s depois de abrir a barra. A ADR 0042 mostra que esse tempo vem do processo `steam.exe` e não se reduz pelo código. No teste das 14:12–14:13, a confirmação veio entre 1104 e 1238 ms em todas as aberturas (**observado**). O usuário pediu uma entrada mais lenta e fluida durante esse intervalo, e que a barra só navegue depois que a Steam soltar o controle.

## Decisão

1. **Espera só quando a Steam viu o PS.** Ela começa junto com o ciclo da ADR 0041. Com o PS só no controle físico, nada muda: entrada de 250 ms e navegação imediata.
2. **A barra é avisada assim que a Steam confirma.** A thread do ciclo avisa a interface ao ver `ExecuteSteamURL` para o layout de desktop, ou ao fim do limite de espera dessa confirmação, sem esperar a liberação. Se o protocolo não puder ser despachado, o aviso é imediato. O log registra `steam input: navegacao liberada | <motivo> | <ms>`.
3. **Durante a espera:**
   - a barra entra em 1000 ms em vez de 250 ms, com a mesma curva que desacelera no fim;
   - o conteúdo do painel fica com 45% de opacidade e acende em 167 ms quando a Steam confirma;
   - analógicos, direcional e botões não fazem nada e não ficam guardados, porque a navegação é limpa ao liberar;
   - o círculo e o PS continuam fechando a barra.
   Com "Reduzir movimento" ou sem animações do Windows, a entrada é imediata e o painel apaga e acende sem transição.
4. **Limite de 3 s:** se o aviso não chegar, a navegação é liberada do mesmo jeito (`navegacao liberada | limite`).
5. Ao ocultar, a espera termina e o painel volta à opacidade normal, sem animação.

## Consequências e limites

- **A espera não tira o controle da Steam mais cedo.** Se o usuário apertar algo durante a entrada, o Big Picture ainda recebe o comando. A barra só deixa de agir ao mesmo tempo, e a entrada lenta sinaliza que é preciso esperar.
- Quem espera a animação terminar não deve ver conflito: a entrada acaba (1000 ms) perto da confirmação (cerca de 1,1 s), e o painel acende nesse momento.
- Fechar durante a espera continua seguindo a ADR 0041: a barra fica invisível até a Steam liberar.
- Silenciar o PS sozinho no DSX elimina a espera inteira (ADR 0042).

## Evidências

- Teste das 14:12–14:13: `bin/design-preview/preview.log` (aviso de PS no virtual, `desktop confirmado em 1104–1238 ms`) e `controller_ui.txt` da Steam (443510 → 769 a cada PS).
- Suíte: 33 verificações do ciclo da Steam, com painel apagado, sem navegação, círculo, limite, entrada lenta (1000 ms contra 250 ms) e abertura sem espera.
