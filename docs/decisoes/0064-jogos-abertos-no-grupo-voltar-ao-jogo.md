# 0064 — Todos os jogos abertos em Voltar ao jogo

- **Status:** Aceita; implementada, aprovação física pendente
- **Data:** 2026-10-27
- **Revisa:** apresentação da Home nas ADRs 0024 e 0060; preserva a transição da ADR 0063.

## Contexto

O usuário pediu apenas layout/design: todos os jogos em execução em Voltar ao jogo, atual primeiro, e opções de fechamento somente com texto. Antes, os demais abertos disputavam as três vagas de Recentes e confirmar/cancelar usavam o símbolo genérico de jogo.

## Decisão

1. Título **VOLTAR AO JOGO**, cartão do atual com nome em destaque/status, e todos os demais abertos logo abaixo, por foco/início mais recente. Sem limite de três. Pausados, sem janela ou indisponíveis para novo lançamento continuam no grupo. Pausar/retomar e fechar permanecem ao lado de cada jogo.
2. **JOGOS RECENTES** contém até três jogos fechados elegíveis, sem repetir abertos. Sem abertos, conservar o cartão do último executado e excluí-lo dos recentes. Não inventar entradas.
3. `GamesView.OpenGames` e `ClosedRecent` são consultas de apresentação; não alteram sessões, histórico ou a consulta legada `Recent`. O preenchimento de imagens contempla todos os abertos, inclusive sem janela.
4. Linhas extras entram na navegação antes dos aplicativos. Foco/ação e origem de cancelamento acompanham a identidade ao reordenar. Listas extensas ou telas menores usam a rolagem existente do corpo, sem cortar ou comprimir jogos. As oito telas de referência verificam margens, proporção e altura limitada pela área segura.
5. Confirmar fechar/forçar e Cancelar usam somente texto, sem símbolo ou coluna vazia. Cancelar mantém foco inicial. O título desse estado passa a **FECHAR JOGO**. Os ícones de pausar/fechar junto aos cartões são preservados.
6. **Revisão após o usuário identificar cartões achatados:** todos os cartões de jogo usam o padding do destaque (`12,11,12,11`) e altura mínima de 76 DIP, verificada como igual entre principal, outros abertos e recentes. Principal mantém fonte/status e primeira posição. A compactação anterior de histórico e segundo aberto foi rejeitada. Não reduzir cartões para evitar rolagem em telas menores. Confirmações e demais painéis conservam seus tamanhos. Regras de transição/saída/Steam da ADR 0063 preservadas.
7. Candidata separada; preservar principais e residente. Sem operar jogos, instalação/configuração ou commit/push.

## Validação e limites

Snapshots/árvore WPF e renders verificam agrupamento, ordem, duplicatas, foco, pausa, confirmação sem imagem, migração para Recentes, lista longa e ausência de escrita de histórico. Gate inclui regressões de transição e escala. Controle físico/TV/FSE e overlay Neva/Genshin permanecem pendentes. Imagens com esses nomes são fixtures.

Continuidade · evidências.
