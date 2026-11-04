# 0022 — Animações da prévia com direção Xbox mais expressiva

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu um mapa de animações em sprints, escolheu "Xbox mais expressivo" e aprovou implementar as cinco sprints. A prévia tinha transições instantâneas e reconstruções periódicas de conteúdo real.

## Decisão

Implementar apresentação compartilhada em 83/167/250 ms: entrada lateral, foco ampliado, pressão, painéis, listas flutuantes, rolagem e estados. O [catálogo](../design/animacoes.md) define efeitos e limites.

Preservar execução imediata e ocultação imediata nos despachos externos. Separar entrada de painel de atualização de dados. Substituir animações interrompidas pelo valor visual corrente e remover clocks de elementos descartados. Nunca animar valores de sliders que possam produzir setters de áudio.

Respeitar animações desativadas no Windows e oferecer "Reduzir movimento" na sessão. Manter o contorno de foco independente do movimento. Sem sons, saída HID ou recursos residentes.

## Consequências

- O movimento não decide a stack definitiva nem muda a ativação manual Win+Shift+G.
- A suíte original permanece determinística; `--verify-motion` exercita clocks reais com fixtures e grava sequências WPF por sprint.
- Render offscreen e testes de foco não comprovam fluidez do compositor, jogos ou input físico.

## Evidências

**Complemento de 2026-10-24:** retirada do indicador padrão `FocusVisualStyle` em `Action` e `VolumeSlider`, preservando o foco verde e a navegação. Correção solicitada pelo usuário após a entrega das animações. Continuidade e distinção entre validações anteriores/finais.

- Plano integral aprovado nesta conversa.
- `src/app/Motion.cs`, `MotionVerification.cs`, XAML e integrações nos painéis.
- Registro e sequências das cinco sprints.
