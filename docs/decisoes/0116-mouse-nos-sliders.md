# 0116 — Mouse nos sliders

- **Status:** Aceita; candidata pública 3.1.0. Teste com mouse físico pendente.
- **Data:** 2026-11-25

## Contexto

Os sliders (Som, player, Gravação, AMD e volume dos sons) tinham 34 px de altura, mas só a linha de 4 px, o marcador de 6 × 18 px e a borda de 2 px respondiam ao mouse. O clique na linha andava 1% (`LargeChange` padrão), e não havia roda. Na prática, os ajustes acabavam feitos pelas setas, de 5 em 5%.

## Decisão

- Toda a altura do slider recebe o mouse; o desenho não muda.
- Clicar leva ao ponto, e arrastar acompanha o ponteiro. Pegar o marcador não dá salto.
- A roda ajusta de 2 em 2 (ou pelo passo do driver, na AMD). Dentro de uma lista que rola, só o slider já clicado aceita a roda: passar por cima não muda o volume.
- A roda não avança a mídia.
- Na AMD, o valor só vai ao driver depois de soltar, para o painel não ser reconstruído sob o ponteiro.

O passo do controle e do teclado não muda.

## Consequências

33 verificações (`tests/SliderMouseVerification.cs`): área clicável medida no modelo real, conversão do ponto, limites, arraste, roda e ligação dos eventos, sem mover o ponteiro do usuário nem alterar volume real.

## Evidências

Medição da área clicável antes da mudança e suíte da 3.1.0.
