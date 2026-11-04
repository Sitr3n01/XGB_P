# 0055 — Destaque do Windows com contraste

- **Status:** Aceita; implementada, bateria completa e leitura real aprovadas; troca real de cor e teste físico pendentes
- **Data:** 2026-10-26
- **Revisa:** ADR 0009 somente quanto à origem da cor de destaque. Preserva a paleta escura, as cores semânticas e a exceção do teclado da ADR 0053.

## Contexto

O usuário restringiu o tema às cores de destaque e autorizou implementar com atenção ao contraste. Pediu um commit antes do trabalho: checkpoint `4303d1036ef0daddde7f95b11d4bc97afc274f09`, contendo o teclado aprovado e a correção da Biblioteca. Não foi solicitado push.

## Decisão

- Consultar `UISettings.GetColorValue(UIColorType.Accent)` e acompanhar `ColorValuesChanged`. Aplicar na Dispatcher WPF; conferir novamente ao abrir a barra. A mudança de cor não reconstrói controles, muda foco, abre a barra ou executa ações.
- Usar a cor original quando ela já satisfaz o contraste. Caso necessário, clarear pela menor mistura com branco em RGB linear, comparando os bytes sRGB finais sem arredondar a razão. Derivar três tons por função: preenchimento, foco e texto.
- Alvos internos: **3,1:1** para preenchimento/foco e **4,6:1** para texto, com margem sobre os mínimos de referência 3:1 e 4,5:1. Comparar contra todas as superfícies relevantes; incluir `MeterTrack` no preenchimento. Compor superfícies translúcidas sobre branco como cenário conservador.
- O cursor branco dos sliders recebe borda neutra escura de 1 DIP, para permanecer delimitado diante de destaques claros. Tamanho e comportamento mantidos.
- `AccentState` centraliza os três valores como propriedades WPF. Pincéis compartilhados ligam-se explicitamente a esse objeto, sem depender do contexto de herança do template. Os nomes históricos `XboxGreen`/`FocusGreen` permanecem compatíveis, mas deixam de significar verde fixo. O brilho breve de foco acompanha o pincel pela mesma ligação.
- Falha transitória mantém a última cor válida da sessão. Sem leitura inicial válida, usar a família do verde base `#107C10`, também com contraste calculado. Tentar novamente ao reabrir se a assinatura de notificações falhou. Encerrar a assinatura ao sair.
- Superfícies, texto principal, erros, avisos, fechar vermelho e o teclado cinza aprovado mantêm seus papéis. Sem novas preferências ou escrita nas configurações do Windows.

## Validação e limites

529 verificações novas: 512 cores, âncoras sRGB, contraste, referências já criadas em XAML/C#, preservação de foco/controles, Dispatcher, falhas/recuperação e barra oculta. Bateria completa aprovada, incluindo a suíte temporal e novo diagnóstico somente leitura. O diagnóstico consulta a API real e registra/remove o observador real, sem mudar a cor do Windows.

Os renders são da árvore WPF com cores substituídas. Razões de contraste das cores não equivalem a uma auditoria completa de acessibilidade, percepção na TV ou validação de todo pixel antialiasado. A notificação provocada por uma troca real nas Configurações e a avaliação física no FSE permanecem pendentes.

**Instrumentação da remoção:** o caminho de desassinatura é executado no diagnóstico, mas `StopAccentTheme()` captura exceções e não expõe uma confirmação independente. O relatório não deve ser usado para alegar prova externa de descarte do observador; não foi observada falha de remoção. Consolidação e limites.

**Revisão no candidato local (ADR 0056):** StopAccentTheme passa a retornar sucesso/falha e registrar erro, sem perder a referência em caso de recusa da API. O diagnóstico falha quando a desassinatura retorna erro. Isso melhora a observabilidade do código; permanece uma confirmação da API, sem prova externa de descarte ou troca real de cor. O principal publicado anteriormente continua com a versão histórica descrita acima.

Continuidade, publicação e reversão · evidências.

## Fontes

- [Microsoft: UISettings.GetColorValue](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.getcolorvalue).
- [Microsoft: ColorValuesChanged](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.colorvalueschanged).
- [W3C: contraste mínimo de texto](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).
- [W3C: contraste de elementos gráficos e estados](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).
