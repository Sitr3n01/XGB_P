# 0009 — Linguagem visual baseada no Xbox

- **Status:** Aceita
- **Data:** 2026-10-23

**Destaques revisados pela ADR 0055 (26/10/2026):** acompanhar a cor de destaque do Windows com tons de contraste por função. O verde passa a ser reserva quando a API está indisponível; superfícies escuras e cores semânticas permanecem. A exceção cinza do teclado abaixo é preservada.

**Revisão localizada em 26/10/2026:** a [ADR 0053](0053-design-do-teclado-baseado-no-windows.md), aprovada pelo usuário, usa seleção dupla cinza no teclado virtual. O verde continua sendo a referência da barra e dos demais painéis.

## Contexto

O usuário definiu que a Game Bar customizada deve seguir a filosofia visual do Xbox e forneceu três imagens do guia do console como referência. As referências mostram superfícies escuras neutras, texto branco, foco destacado e cor de destaque personalizável (azul em duas imagens, verde na terceira).

## Decisão

Adotar o tema escuro inspirado no guia Xbox, com **verde como destaque padrão do XGB_P**. A primeira alteração aplica apenas a paleta: substitui fundos azulados por preto/cinza, o destaque verde-lima por verde Xbox e mantém o layout e a navegação existentes.

Usar `#107C10` como verde base do projeto, `#32A832` no contorno de foco e `#6CCB5F` em pequenos textos de destaque para legibilidade. Essas escolhas compõem a paleta do XGB_P; não afirmam que todos os tons sejam tokens oficiais ou valores extraídos das fotografias.

Centralizar os recursos de cor no XAML, compartilhados pelos elementos estáticos e painéis criados em C#. Indicadores e volume usam a mesma família de verde; seleção e hover mantêm fundos neutros.

## Consequências

- A linguagem visual de referência passa a ser Xbox: hierarquia simples, conteúdo legível na TV e foco explícito para navegação por controle.
- O verde é usado como destaque, preservando áreas de conteúdo em cinza neutro e textos principais em branco.
- A revisão de estrutura, tamanhos, espaçamento, ícones e demais interações fica para os próximos pedidos de design.
- Essa revisão não muda o escopo funcional, a stack final ou a integração com o sistema.

## Evidências

- Pedido e três referências visuais enviados pelo usuário nesta conversa em 2026-10-23.
- [Paleta e diretrizes](../design/paleta.md).
- `src/app/DesignPreview.xaml` e recursos compartilhados com `DesignPreview.cs`.
