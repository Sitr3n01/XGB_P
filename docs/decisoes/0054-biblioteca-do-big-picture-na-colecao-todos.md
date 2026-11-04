# 0054 — Biblioteca do Big Picture na coleção Todos

- **Status:** Aceita; implementada, bateria completa e destino real conferidos; teste físico no FSE pendente
- **Data:** 2026-10-26
- **Revisa:** ADRs 0006 e 0011 somente quanto à rota da Biblioteca no modo Xbox. Preserva ADRs 0039 e 0041–0046.

## Contexto

**Revisão de 27/10:** a [ADR 0058](0058-navegacao-explicita-home-e-biblioteca.md) preserva a rota da coleção, mas aguarda o ciclo Steam Input e confirma a entrega de foco antes de ocultar a barra.

O usuário relatou que Biblioteca abria a Home do Big Picture e autorizou a correção após o diagnóstico. A rota anterior, `steam://open/bigpicture/games`, é registrada no cliente, mas seu tratamento chega ao ramo de reserva `Home()`; o teste antigo apenas conferia essa mesma URL. [Leitura do cliente e posições](../referencia/biblioteca-steam-big-picture.md).

## Decisão

- Com `GamingExperience.Active() == true`, os acessos Biblioteca do trilho e do Acesso rápido enviam `steam://open/library/collection/all-games`. No cliente conferido, isso abre a Biblioteca em **Coleções → Todos**, com a grade completa de jogos.
- Usar a coleção nativa `all-games`, que também alimenta a aba Todos os jogos da Steam. Não depender da última aba visitada, de teclas simuladas, de automação de cliques ou de acesso ao navegador interno em execução.
- Preservar `steam://open/games` no desktop/API indisponível. Preservar ocultação após despacho aceito e manutenção da barra com erro se o despacho falhar.
- Manter o fluxo de foco e Steam Input existente. Não iniciar/encerrar Big Picture nem alternar FSE adicionalmente no handler da Biblioteca.
- Testar os dois acessos no modo Xbox substituído, sua ocultação e a falha de despacho. A evidência da tela de destino vem de um teste separado no Big Picture real.

## Evidências e limites

A bateria completa passou em 26/10/2026, 19:00–19:01, America/Sao_Paulo. A rota antiga foi reproduzida na Home; a nova abriu a grade de jogos na Steam real. Esse teste foi feito no **Big Picture sobre o desktop**, sem ativar o modo Xbox. O teste pelo DualSense com a barra no FSE continua pendente.

Continuidade, build, backup e reversão · capturas e relatórios.
