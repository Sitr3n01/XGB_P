# 0007 — Escopo e interação da v1

- **Status:** Aceita
- **Data:** 2026-10-23

> **Revisão parcial pela ADR 0010:** Início passa a ser uma Home própria com recentes e utilidades, incluindo uma ação explícita para o Big Picture. Demais itens desta decisão permanecem válidos.

> **Estado posterior:** Som foi separado em painel próprio pela 0012, refinado pela 0013; Home ganhou apps abertos pela 0014. O painel **Jogo atual** saiu do trilho pela [0016](0016-player-no-som-e-listas-flutuantes.md): o jogo fica na Home, em "Voltar ao jogo". A prévia de desktop é uma ferramenta separada autorizada pela 0008, não uma mudança da política do produto. O texto abaixo preserva a decisão inicial.

## Contexto

> **Gesto substituído pela ADR 0037 (2026-10-25):** PS no aperto e Win+G, exclusivamente no Xbox/FSE. Microfone não abre/fecha. Mantida a inatividade no desktop.

Para começar o design, quatro escolhas de UX eram do usuário: posição na tela, painéis da v1, gesto de abertura e o que fazer no desktop. Elas foram respondidas depois do primeiro esboço visual (trilho à esquerda + painel).

## Decisão

1. **Posição:** barra lateral **esquerda** (trilho de ícones + painel). O menu rápido da Steam continua abrindo à direita.
2. **Painéis da v1:**
   - **Jogo atual e áudio**;
   - **Desempenho**;
   - **Controle e energia**;
   - além das ações Início e Biblioteca, que abrem o Big Picture.

   Capturas ficam fora.
3. **Gesto:** **microfone segurado por 600 ms ou mais** abre e fecha. O toque curto no microfone continua sendo mute, e o PS continua com a Steam.
4. **Desktop:** os comandos `ms-gamebar://` (Win+G, capturas) são **ignorados**. Não há exceção à ADR 0005.

## Consequências

- Q1 e Q2 do roadmap estão resolvidas.
- **Sem capturas na v1:** Win+Alt+PrtScn e Win+Alt+R deixam de fazer qualquer coisa, já que a Game Bar oficial foi removida. A Steam continua tendo as capturas dela (F12 e gravação).
- O painel Desempenho exige medir FPS sem injeção (spike S5).
- O painel Controle depende de ler a bateria pelo HID (spike S6).

## Evidências

[docs/design/README.md](../design/README.md); tempos do gesto em [testes-de-controle.md](../guias/testes-de-controle.md) (segurado: 618–1020 ms).
