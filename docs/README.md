# Documentação do XGB_P

## Comece por aqui

| Documento | Para quê |
|---|---|
| [Instalação e uso](guias/instalacao-e-uso.md) | Instalar, liberar no HidHide, usar, atualizar e desinstalar |
| [Requisitos](requisitos.md) | Escopo, controles, recursos, dependências e critério de aprovação |
| [Arquitetura](arquitetura.md) | Componentes, ativação, foco e Steam Input, jogos, dados e build |
| [Roadmap](roadmap.md) | Situação, pendências antes da aprovação e melhorias planejadas |
| [Desenvolvimento](guias/desenvolvimento.md) | Ferramentas, as duas builds, gates e release |

## Decisões

[Índice das ADRs](decisoes/README.md). Cada decisão importante tem um registro com contexto, decisão, consequências e evidências. Decisão substituída não é apagada: o registro novo aponta para o antigo.

## Referências técnicas

Fatos do Windows, da Steam, do DualSense e da AMD dos quais o código depende, com a data em que foram observados:

| Área | Documentos |
|---|---|
| Ativação e modo Xbox | [ativação por PS e Win+G](referencia/ativacao-ps-wing.md), [inicialização com o Windows](referencia/inicializacao-windows.md) |
| Controle | [DualSense pelo HID](referencia/dualsense-hid.md) |
| Steam | [Biblioteca no Big Picture](referencia/biblioteca-steam-big-picture.md), [janela da Steam e escala](referencia/janela-steam-e-escala.md) |
| Jogos | [jogos recentes](referencia/jogos-recentes.md), [imagens dos jogos](referencia/imagens-jogos.md), [fechamento, pausa e acesso negado](referencia/fechamento-pausa-e-acesso-negado.md), [lojas](referencia/lojas-windows.md) |
| Windows | [janelas](referencia/janelas-windows.md), [áudio](referencia/audio-windows.md), [mídia](referencia/midia-windows.md), [rede](referencia/rede-windows.md), [conexões](referencia/conexoes-windows.md), [desempenho](referencia/desempenho-windows.md) |
| AMD | [capturas, miniaturas, Fotos e áudio](referencia/capturas-miniaturas-fotos-audio-2026-10-29.md), [perfis por jogo (investigação)](referencia/amd-perfis-por-jogo-2026-10-29.md) |

## Design

[Visão geral](design/README.md), [paleta](design/paleta.md), [animações](design/animacoes.md), [som](design/som.md) e [Início](design/home.md).

## Guias de teste

- [Matriz física de aprovação](guias/primeiros-testes-lancamento.md)
- [Testes do controle](guias/testes-de-controle.md)
- [Jogos recentes](guias/jogos-recentes.md)

## Sobre o histórico

O repositório público começa na v2.1.0. Registros de sessão, evidências brutas (logs, capturas de tela e medições desta máquina), a análise de binários e a ponte local de ativação ficam num arquivo privado, porque contêm dados da máquina do autor. Nas ADRs e referências, menções a esse material aparecem como texto sem link.
