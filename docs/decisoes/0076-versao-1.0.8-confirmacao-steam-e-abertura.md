# 0076 — Versão 1.0.8: confirmação Steam e abertura

- **Status:** Aceita; publicada para teste, validação física pendente
- **Data:** 2026-10-29

## Contexto

O usuário relatou alertas de Steam Input sobre o Genshin e engasgos ao reabrir a barra. Primeiro pediu apenas diagnóstico; depois autorizou implementar, publicar 1.0.8 em `.exe` e apagar versões anteriores à 1.0.7. Em resposta à pergunta sobre a inicialização, autorizou também atualizar `bin/app` para 1.0.8 e remover executáveis antigos.

Nos logs da 1.0.7, cinco ciclos em 29/10 (10:21:58, 10:22:17, 10:22:39, 10:22:44 e 10:22:48) terminaram com desktop e liberação confirmados, mas contexto final recusado. O `controller_ui.txt` registra foco desktop, carga Guia 443510 e carga desktop 413080. O leitor antigo invalidava o desktop na carga Guia, mas ignorava a carga desktop seguinte. A recusa era reproduzível com essa sequência, mesmo sem mudança real de foco para jogo/Big Picture.

O engasgo visual não possuía medição de frames no log antigo. O código iniciava a entrada e podia reconstruir listas durante ela; a confirmação Steam também acrescentava um fade de 167 ms independente. São causas plausíveis de variação visual, não prova de um gargalo de GPU.

## Decisão

1. Separar foco selecionado de carregamento de perfil no leitor. Aceitar Guia → desktop somente com foco desktop confirmado no mesmo trecho novo do ciclo. Carga isolada não estabelece foco; mudança real de foco ou carga divergente não é apagada pela carga desktop. Cursor, identidade do arquivo e recusa de linhas parciais permanecem.
2. Depois de confirmar `/0`, consultar a conclusão da transição a cada 25 ms, por até 500 ms, saindo imediatamente quando confirmado. Não acrescentar espera ao sucesso imediato, repetir a forçada ou liberar pelo timeout. Cancelamento, Steam indisponível e contexto divergente mantêm bloqueio.
3. Manter entrada visual de 250 ms. Agrupar reconstruções não forçadas de apps/jogos durante a entrada e aplicar somente o snapshot atual ao final, em prioridade de apresentação. Estado das operações e cancelamentos continuam imediatos; reconstrução inicial e comandos explícitos permanecem disponíveis.
4. Atenuar imediatamente quando o ciclo começa. Ao confirmar, terminar a opacidade junto da entrada, ou usar ajuste de 83 ms se ela já terminou. Ocultar/interromper cancela observação de renderização; reabertura não aplica snapshot antigo.
5. Registrar callbacks WPF: primeiro callback desde o pedido de abrir, maior intervalo, quantidade e atualizações adiadas. Não chamar essa telemetria de FPS do compositor.
6. Publicar o mesmo build em `.exe` e `bin/app`, mantendo a entrada de inicialização e autorizações HidHide por caminho. Preservar a 1.0.7 integralmente e remover somente executáveis/ZIPs identificados anteriores a ela. Não apagar fontes, documentação, logs ou histórico Git.

## Consequências

O erro conhecido de interpretação foi corrigido sem enfraquecer o bloqueio por falta de confirmação. A animação concentra menos reconstruções durante a entrada; a melhora física ainda precisa ser conferida no Genshin e Big Picture. Não existe garantia de neutralização global do controle virtual.

A remoção solicitada substitui a decisão anterior de manter os binários da 1.0.6 e anteriores. Os documentos antigos continuam históricos; seus caminhos de executáveis/ZIPs podem ter sido removidos conforme o manifesto da limpeza. A reversão disponível é a 1.0.7. Não houve pedido de novo commit/push nesta entrega.

## Evidências

Relatório, recibos e recortes, guia de teste/reversão. Gate de 20 execuções; Steam Input 104, desempenho 121, teclado 93, transições 131 e sessão Steam 46 verificações. Nenhuma sessão de jogo/FSE ou energia real foi iniciada pelo agente.
