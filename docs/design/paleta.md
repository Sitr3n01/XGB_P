# Paleta e linguagem visual

Definidas em 2026-10-23 a partir das referências do guia Xbox enviadas pelo usuário ([ADR 0009](../decisoes/0009-linguagem-visual-xbox.md)). Os valores abaixo são a paleta do XGB_P inspirada nessas referências, não uma especificação oficial completa do Xbox.

## Direção

- Superfícies em preto e cinza neutro, sem dominante azul.
- Texto principal branco e informação secundária em cinza claro.
- Cor de destaque do Windows para foco, indicadores e destaques pontuais, com contraste ajustado (ADR 0055). Verde como reserva.
- Foco distinguível da seleção: item selecionado com fundo cinza; item em foco também recebe contorno na cor de destaque.
- Tamanhos e navegação da prévia preservados nesta revisão de cores.

## Exceção aprovada — teclado virtual

Em 26/10/2026, o usuário aprovou o desenho baseado na referência do teclado do Windows ([ADR 0053](../decisoes/0053-design-do-teclado-baseado-no-windows.md)). O teclado usa teclas retangulares de cantos de 2 DIP e foco duplo cinza, com valores próprios em `src/app/KeyboardVisuals.cs`. Essa exceção é preservada quando a barra acompanha o destaque do Windows.

| Elemento do teclado | Cor |
|---|---|
| Painel | `#1D1D1D` |
| Letras / comandos | `#363636` / `#303030` |
| Borda das teclas | `#151515` |
| Contorno de foco | `#777777`, 3 DIP nas teclas principais |
| Tecla em foco / hover / modificador ativo | `#3B3B3B` / `#444444` / `#545454` |
| Texto e vetores / informação secundária | `#F3F3F3` / `#AEAEAE` |

Proposta aprovada e renders implementados. Esses valores são escolhas do projeto baseadas na referência; não são apresentados como tokens oficiais extraídos do Windows.

## Cores

| Recurso | Cor | Uso |
|---|---|---|
| `XboxGreen` | Derivada do Windows; reserva com base `#107C10` | preenchimento dos indicadores e volume, mínimo 3:1 nos fundos verificados |
| `FocusGreen` | Derivada do Windows | contorno do item em foco, mínimo 3:1 nos fundos verificados |
| `AccentText` | Derivada do Windows | pequenos textos de destaque, mínimo 4,5:1 nos fundos verificados |
| `TextPrimary` | `#FFFFFF` | títulos, valores, ações e ícones |
| `Muted` | `#BFBFBF` | descrições e textos secundários |
| `SurfaceRail` | `#141414`, alfa `F2` | trilho lateral |
| `SurfacePanel` | `#1F1F1F`, alfa `FA` | painel principal |
| `SurfaceCard` | `#2B2B2B` | cartões, ações e feedback |
| `SurfaceHover` | `#383838` | passagem do mouse |
| `SurfaceSelected` | `#333333` | item selecionado / em foco |
| `SurfacePressed` | `#404040` | ação pressionada |
| `Divider` | `#404040` | bordas e separadores |
| `MeterTrack` | `#505050` | fundo dos indicadores; ondas apagadas do Wi-Fi |
| `Critical` | `#FF99A4` | alerta (vermelho de alerta do tema escuro do Fluent): bateria ≤ 15%, erro da bateria, rede sem internet (ADR 0018) |
| `Backdrop` | preto, alfa `48` | escurecimento do desktop |

Os recursos ficam no início de `src/app/DesignPreview.xaml`. Os painéis dinâmicos em C# consultam esses mesmos recursos. O slider usa preenchimento na cor de destaque, restante cinza e cursor branco com borda escura; o contorno de foco também acompanha o destaque.

## Destaque dinâmico — ADR 0055

`AccentTheme.cs` consulta `UISettings.GetColorValue(Accent)`, acompanha notificações e aplica na Dispatcher. `AccentState` fornece os três valores por ligação explícita, compartilhada pelos controles existentes. Os nomes Green são históricos; não representam mais uma cor fixa.

Preservar a cor original quando já passa; caso contrário, clarear em RGB linear até alvos internos de 3,1:1 para foco/preenchimento e 4,6:1 para texto. Incluir hover/seleção/pressionado e trilha dos indicadores nas comparações; fundos translúcidos são compostos sobre branco conservadoramente. Falha transitória mantém a última cor; ausência inicial usa verde com os mesmos ajustes. Não há escrita de preferências ou configurações do Windows.

Na leitura real de 26/10/2026, o destaque `#3F3F3F` gerou `#A1A1A1` / `#8D8D8D` / `#ADADAD`, com razões mínimas 3,121 / 3,124 / 4,620. Esses números verificam a paleta nos fundos considerados; não declaram acessibilidade completa da aplicação. Renders e limites.

Os renders atuais estão em `bin/design-preview/painel-0.png` e `painel-2.png` a `painel-6.png`, com cópias preservadas. A faixa de apps usa a mesma SurfacePanel no degradê, máscara de opacidade e estilo AppStripArrow; não depende de uma imagem de fundo nem de blur de captura de tela. Avaliação em TV real continua sendo parte da validação do usuário.
