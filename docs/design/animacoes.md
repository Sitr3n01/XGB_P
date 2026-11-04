# Animações da prévia — Xbox mais expressivo

Implementado em 2026-10-24, a partir do plano aprovado pelo usuário. Decisão: [ADR 0022](../decisoes/0022-animacoes-da-previa.md).

**Revisão de 27/10 (rc.2):** altura passa a ser apresentada por desenho/recorte com conteúdo no tamanho final; saídas usam desenhos retidos, limitados a oito cópias/2.048 elementos. Agenda acompanha painel/visibilidade/pendências. Tempos, direção visual, foco, movimento reduzido e comandos imediatos preservados. [ADR 0057](../decisoes/0057-apresentacao-com-custo-limitado.md) e comparação, custos e limites. A medição não demonstra FPS do compositor nem fluidez física sob jogo.

Estado final e histórico do contorno pontilhado: continuidade da sessão. Os vídeos da primeira entrega precedem essa correção; a suíte funcional final está nas evidências de foco.

## Catálogo por sprint

Os tempos são escolhas do XGB_P, inspiradas no [motion do Windows](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion) e em seus [tempos e curvas](https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing). Não são medições ou tokens extraídos do guia oficial do Xbox. Distâncias usam as unidades da cena responsiva (1 DIP, com redução ou ampliação só fora de 880–1440 de altura efetiva; ADR 0023). A entrada lateral parte de `-(margem + largura da barra)`.

| Sprint | Comportamento entregue |
|---|---|
| 1 — Entrar e sair | Trilho/painel entram juntos da esquerda em 250 ms; blocos visíveis sobem 12 unidades em 167 ms, com intervalos de 20 ms limitados a 60 ms. A faixa de status aparece em 167 ms (a faixa de dicas saiu na ADR 0023). Saída manual anda 48 unidades para a esquerda e desaparece em 167 ms. Steam, jogo e alternância de app ocultam imediatamente. |
| 2 — Foco e seleção | Cartões ampliam para 1,02; ícones para 1,04 em 167 ms. Contorno verde imediato e brilho local de 83 ms. Pressão para 0,98 em 83 ms, sem aguardar para executar a ação. Hover por opacidade da superfície em 83 ms. Folga de desenho compensada na área de rolagem preserva as medidas dos cartões. |
| 3 — Navegação | Painel/título deslocam 24 unidades na direção da navegação em 167 ms. Atalhos da Home usam eixo horizontal, invertido no retorno. Listas flutuantes entram 8 unidades abaixo em 167 ms e saem em 83 ms por cópia visual sem hit test. Confirmação de outro jogo entra em 167 ms. Rolagem por mouse/setas suaviza em 167 ms quando preserva a visibilidade do foco; revelar alvo fora da viewport é imediato. Setas e espessura da barra respondem em 83 ms. |
| 4 — Dados da Home | Imagem local substitui símbolo genérico em 167 ms. Entradas e remoções de jogos/apps só animam quando a composição muda. Atualizações preservam identidade do foco e rolagem; não repetem a entrada do painel. Nome do acesso rápido muda em 83 ms. Barras de desempenho interpolam em 167 ms; números mostram a leitura recebida imediatamente. |
| 5 — Som e detalhes | Volume manual é imediato; atualização externa suaviza apenas o preenchimento em 83 ms, sem animar o valor que gera comandos. Ícones de mute/transporte mudam em 83 ms; capa/metadados em 167 ms. Progresso visual acompanha a posição informada e respeita busca/arraste. Estado de rede/bateria só anima quando muda, em 83 ms; relógio é direto. Mensagens entram em 167 ms e erros permanecem legíveis. |

## Cancelamento e acessibilidade

- `Motion.cs` concentra clocks, tempos, curvas, entrada/saída, foco, rolagem e apresentação. Clocks da mesma propriedade são substituídos pelo valor visual corrente. Conclusão/cancelamento confirmam o valor final antes de liberar o clock.
- Reconstruções removem clocks da árvore lógica e visual descartada, inclusive antes de os templates serem medidos. Saídas usam cópias locais temporárias na camada `MotionOverlay`, sem foco nem ações.
- A janela distingue oculta, aberta e fechando. Reabrir invalida o callback antigo. Durante o fechamento, navegação é ignorada; atalho ou Esc podem reabrir. Despachos externos conservam a ocultação imediata original.
- O contorno verde independe das animações. Navegação continua por tag/identidade; Som continua entrando pelo seletor de saída.
- **Correção de foco (2026-10-24):** os estilos base `Action` e `VolumeSlider` usam `FocusVisualStyle` nulo. Isso retira o adorner preto pontilhado do tema WPF; o foco verde dos templates e a navegação por teclado/controle continuam ativos. Botões do trilho, cartões, mídia e mutes herdam a correção de `Action`.
- **Configurações > Reduzir movimento** vale apenas para a sessão. As animações desativadas no Windows sempre prevalecem (`SystemParameters.ClientAreaAnimation`); mudanças são acompanhadas enquanto o processo roda. Não grava preferências no Windows ou em disco.
- Reduzir movimento conclui clocks, restaura escalas e desliga o timer de apresentação de 33 ms. Ocultar libera clocks, cópias visuais e esse timer. Os workers preexistentes conservam seu comportamento.
- **Trilho em descanso ([ADR 0030](../decisoes/0030-trilho-em-descanso-e-sons-da-interface.md)):**
  - com o foco no painel, os ícones do trilho vão a 90% do tamanho e 55% de opacidade em 167 ms; voltar ao trilho ou passar o mouse nele os desperta;
  - cada ícone tem uma escala própria depois da escala de foco, então encolhe no lugar;
  - com movimento reduzido, a mudança é imediata.
- **Escala de foco e translação (2026-10-24, sessão de debug 1):** a translação de abertura entra num `TransformGroup` com a escala do botão, em vez de substituí-la. Toda leitura da escala passa por `MotionScale`.
- Referências: [controle/TV](https://learn.microsoft.com/en-us/windows/uwp/ui-input/gamepad-and-remote-interactions) e [XAG 117](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/117).

## Verificação

A partir da raiz, executar os testes funcionais e temporais:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validar-design.ps1 -ValidarAnimacoes
```

Testar candidata sem interromper a prévia existente:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validar-design.ps1 -ValidarAnimacoes -PastaSaida bin\motion-candidate
```

`--verify-motion` executa primeiro a suíte funcional original, depois os testes temporais. Ao lado do executável, `design-preview/` recebe `animacoes-verificacao.txt`, PNGs estáveis e sequências em `motion-frames/`, com tempos reais/custo de render em `timing.csv`. Comandos externos são substituídos. São capturas da árvore WPF, não gravações do desktop/compositor.

Os testes cobrem interrupção/reabertura, troca rápida, foco/escala/recorte, menus e retorno, snapshot idêntico sem reentrada, progresso sem comandos, barras/números, movimento reduzido, preferência do Windows, ocultação sem clocks e despacho externo imediato. Evidências da entrega.

## Limites

Continua sendo a prévia manual WPF. Stack final, residência, overlay/input em jogos e validação física na TV/DualSense são trabalhos separados. Não há som de interface, vibração, escrita HID, instalação ou novas ações de energia. O player preserva a resposta otimista preexistente, reconciliada pela leitura seguinte do backend; a animação não confirma a aceitação de comandos pelo aplicativo externo.
