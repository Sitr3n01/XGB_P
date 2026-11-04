# 0057 — Apresentação com custo limitado e agenda por necessidade

- **Status:** Aceita e implementada na candidata local rc.2; validação física e lançamento pendentes.
- **Data:** 2026-10-27
- **Preserva:** direção visual, acessibilidade, ações imediatas, ativação/FSE, HID somente leitura, foco, Steam Input, histórico e recuperação.

## Contexto

O usuário autorizou aplicar as otimizações necessárias enquanto acompanha pelo celular, sem disponibilidade para testar fisicamente. A análise do 7700X/RX 9070 XT encontrou baixo consumo médio na carga já medida, mas transições com medição repetida do conteúdo, capturas síncronas de pixels e temporizadores de apresentação ativos sem necessidade.

Consumo médio baixo não demonstra intervalos regulares entre quadros. Nenhuma mudança na prévia pode garantir ausência de travamento com todos os recursos do PC saturados.

## Decisão

1. Medir e posicionar o conteúdo no tamanho final da transição. Animar a aparência da altura por duas superfícies arredondadas, translação e recorte, mantendo o texto sem escala. Retomar da altura visual corrente em uma interrupção. Descartar superfícies/recortes ao concluir, ocultar, reduzir movimento ou mudar a resolução.
2. Copiar comandos retidos de desenho para as saídas decorativas, preservando transformações, recortes, opacidade, efeitos e fontes locais. Não rasterizar por RenderTargetBitmap no caminho de produção dessas saídas. Limitar a oito cópias simultâneas e 2.048 elementos por cópia; excesso dispensa a cópia decorativa, sem adiar a ação.
3. Ativar os oito temporizadores de apresentação conforme visibilidade, painel e operações pendentes. Preservar áudio/seek/feedback pendentes. PS, teclado pendente/visível, espera Steam e saída do FSE mantêm o ciclo de 40 ms; o watchdog do teclado automático ocioso no FSE usa 1 s. HID, ativação e observação/histórico de jogos continuam independentes.
4. Aplicar snapshots puros de mídia, desempenho e rede na prioridade Background da Dispatcher. Comandos e entrada mantêm a fila Normal. Não coalescer nem descartar apertos/solturas ou confirmações; preservar o limite já existente de uma leitura em voo por subsistema.
5. Evitar listas temporárias na seleção de mídia, formatar seu relógio apenas quando muda o segundo/estado e reutilizar máscaras de borda equivalentes. Abrir a barra atualiza sessões sem invalidar o TTL/backoff do catálogo; a faixa de status é atualizada imediatamente.
6. Acrescentar --verify-performance ao gate obrigatório. Medir a mesma candidata entregue com um microbenchmark independente, cenários substituídos e ordem AB/BA alternada. Preservar amostras, hashes, custos e limitações.
7. Entregar rc.2 isolada. Preservar principal, candidata anterior, residente existente e dados. Sem commit/push, instalação ou alteração de Windows/AMD/Steam/DSX/HidHide.

## Consequências

O conteúdo deixa de ser redimensionado a cada passo da animação de altura. A cópia vetorial reduz o tempo de captura medido, mas aloca mais objetos gerenciados que a captura antiga; as amostras não medem o buffer nativo da captura antiga. Os limites de tamanho/quantidade e o descarte são parte do contrato.

A construção de controles ao trocar de painel continua síncrona. A comparação não demonstra 240 FPS, estabilidade de jogo com GPU saturada ou latência do DualSense. O ensaio anterior com Neva não é repetido automaticamente. A candidata permanece separada do núcleo/ativador empacotado final.

## Resultados medidos

**08:42:05–08:43:09 UTC−03**. Quatro pares com ordem alternada AB/BA, um processo novo por execução, sem testes simultâneos. Ambos usam o mesmo harness, SHA-256 **DC0AACD0114244121A36226669006DCF3FBA936285B132C43D8FFF210089B676**.

Por versão: 80 transições Home/Energia, 80 capturas de saída e 32 lotes de 10.000 seleções de mídia. Mídia em pausa, fixtures, janela fora da tela, quatro transições de aquecimento e GC antes da fase medida. Um Decorator neutro conta Measure/Arrange do conteúdo em ambas as versões. As alocações vêm do AppDomain; CPU é tempo acumulado do processo, com resolução grosseira, e inclui o trabalho de todos os seus threads.

| Métrica | Referência | rc.2 | Leitura |
|---|---:|---:|---|
| Measure/Arrange médios do conteúdo por transição | 36,7125 / 36,7125 | 1,5 / 1,5 | **95,9% menos passagens**; p95 39 → 2. |
| CPU média na janela de uma transição | 202,734 ms | 187,500 ms | 7,5% menor nesta medição; p95 permaneceu 296,875 ms. Não é tempo bloqueado de um quadro. |
| Alocação gerenciada média por transição | 12.603.285,5 B | 12.089.512,5 B | Redução modesta, cerca de 4,1%; construção de controles permanece. |
| Captura de saída: mediana / p95 | 3,0386 / 3,4930 ms | 1,2957 / 1,8491 ms | **57,4% menor mediana**, 47,1% menor p95. |
| Captura: alocação gerenciada média | 68.403,2 B | 563.475,5 B | **Aumentou**: cópia de objetos de desenho. Não inclui o buffer nativo de pixels antigo; não afirma economia total de RAM/GPU. |
| Seleção de mídia, 10.000 chamadas: bytes médios | 1.845.269 B | 241.646 B | **86,9% menos alocação**, incluindo custo fixo de reflexão do harness. |
| Seleção de mídia, 10.000 chamadas: mediana | 1,0866 ms | 0,8222 ms | Medição curta de seleção, não de leitura SMTC nem de FPS. |
| SetPanel síncrono: mediana | 12,1896 ms | 12,1316 ms | Praticamente igual; não demonstra orçamento de 4,17 ms de 240 Hz. |

O tempo total de cada janela de transição inclui uma espera solicitada de 230 ms; não deve ser usado como tempo de renderização. Os números não são testes A/B de gameplay. Quatro pares não caracterizam toda a variabilidade do sistema nem substituem profiling de quadro no compositor.

## Evidências

Relatório e continuidade, amostras/recibos.
