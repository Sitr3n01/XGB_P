# 0011 — Início e Biblioteca da Steam ativos na prévia

- **Status:** Aceita
- **Data:** 2026-10-23

> **Rota da Biblioteca revisada pela ADR 0054 (2026-10-26):** no FSE, usar `steam://open/library/collection/all-games`. A rota anterior descrita nesta decisão levava à Home no cliente atual. As demais políticas de despacho, ocultação e erro permanecem.

## Contexto

O usuário autorizou adicionar funções reais à prévia e pediu um botão para a Home do Big Picture acima do jogo ativo. O pedido segue a discussão sobre a Biblioteca usar a Steam e respeitar o contexto de apresentação.

## Decisão

- Colocar **Início do Big Picture** no topo da Home, antes de Voltar ao jogo. Mover o acesso existente para essa posição, sem duplicá-lo na grade inferior.
- Ativar esse botão com `steam://open/bigpicture`.
- Ativar **Biblioteca** no trilho e na Home com o mesmo handler. Consultar `GamingExperience.Active()` no clique: `true` usa `steam://open/bigpicture/games`; `false` ou API indisponível usam `steam://open/games`.
- Ocultar a prévia depois que o shell aceitar a abertura do protocolo. Em erro de despacho, manter a barra aberta e informar a falha.
- Na decisão inicial, manter Downloads, recentes e ações de sistema simulados. **Downloads foi ativado na ampliação registrada em Evidências abaixo**; Configurações e outros recursos reais posteriores estão discriminados no documento de continuidade. Recentes/energia continuam simulados.

## Consequências

- A prévia passa a ter duas navegações reais e identifica isso na Home. Não instala nem altera a Steam, pacote, certificados ou configurações do Windows.
- A escolha genérica no desktop não garante que toda versão/estado da Steam preserve a interface ativa. O resultado visual deve ser validado no cliente real.
- A verificação automática substitui somente o lançamento externo para testar os mesmos handlers sem abrir a Steam. Testa as entradas do trilho/Home, seleção de rota, ocultação e erro.
- Atualiza o escopo simulado descrito na ADR 0010; mantém seus recentes de exemplo.

## Evidências

**Ampliação autorizada em 2026-10-23:** o usuário pediu ativar Downloads. O botão da Home passa a usar `steam://open/downloads`, pelo mesmo fluxo de despacho, ocultação e erro de Início/Biblioteca. A verificação automática cobre também esse botão, sem executar o lançamento externo. Recentes e ações de sistema continuam simulados.

- Pedido do usuário nesta conversa em 2026-10-23.
- **Configurações (ampliação em 2026-10-23):** o painel Configurações recebe acessos reais a `steam://open/settings` e `ms-settings:`. Ambos ocultam a barra após despacho bem-sucedido e preservam a UI com mensagem em erro. Não alteram preferências automaticamente. Testes exercitam os botões com lançamento externo substituído; render em `painel-5.png`, sem confirmação automatizada da tela de destino.
- `src/app/SteamActions.cs`, `HomePreview.cs` e referência de URIs.
- Render da Home em `bin/design-preview/painel-0.png` e verificação em `bin/design-preview/verificacao.txt`.
