# 0074 — Transferência rápida do Steam Input após soltar o PS

- **Status:** Implementada e gate aprovado; candidata separada, sem publicação; validação física pendente
- **Data:** 2026-10-29; investigação iniciada em 28/10
- **Publicação posterior:** incorporada na 1.0.7, [ADR 0075](0075-versao-1.0.7-e-backup-da-1.0.6.md). Os dados da candidata abaixo permanecem históricos; teste físico pendente.
- **Revisa:** espera/animação da ADR 0043 e confirmação do ciclo da ADR 0041. Preserva o transporte do teclado e os destinos de navegação das ADRs 0052/0059.

## Contexto

O usuário pediu analisar a entrada lenta adotada para disfarçar o conflito de controle com a Steam, depois planejar uma solução sem esse atraso e sem duplicação. Autorizou investigar e implementar se viável.

O comportamento anterior anima em 1000 ms, bloqueia o HID e aceita `ExecuteSteamURL` como confirmação. Em erro ou timeout, libera mesmo sem confirmar. A latência do shell de cerca de 1,1 s costuma colocar o comando depois da soltura do PS. Acelerar apenas o transporte permite que a soltura posterior selecione novamente o contexto 769.

Nas interfaces de comando do DSX 3.2 (UDP e pipe/linha de comando) não há um comando confirmado para neutralizar a saída virtual com expiração automática. Trocar perfis a cada abertura não tem essa garantia de recuperação. A conclusão vale só para as superfícies examinadas. Evidências.

## Decisão

1. Implementar uma candidata separada de transferência Steam, sem editar DSX, perfis, HidHide, drivers ou processos residentes. Não implementar exclusividade fictícia por automação de UI ou troca de perfil.
2. Usar uma entrada normal de **250 ms**, respeitando movimento reduzido. O painel permanece atenuado e a navegação HID bloqueada enquanto a transferência não estiver confirmada; o fim da animação não equivale à liberação da entrada.
3. Esperar a última soltura do PS observada nas fontes de entrada antes de forçar o desktop. Perda de dispositivo, nova geração, perda de foco e encerramento invalidam a transferência; não aceitar confirmação antiga na reabertura.
4. Oferecer o canal local somente aos endereços exatos `steam://forceinputappid/413080` e `steam://forceinputappid/0`, com a mesma identidade de cliente/sessão e verificação de ocupação da ADR 0059. Não ampliar a lista de Home/Biblioteca nem acelerar implicitamente o teclado. Sem disponibilidade antes de escrever, usar o protocolo normal. Ocupação admite tentativa limitada antes de qualquer escrita; entrega incerta não repete a forçada por outro transporte.
5. Confirmar execução e ordem dos comandos em linhas completas novas do console, e o contexto desktop novo no log do controle. Guia/769/contexto divergente posterior, log ilegível, substituído/truncado ou confirmação antiga não liberam a navegação. A liberação do layout e seu diário de recuperação permanecem parte do ciclo.
6. Erro/timeout deixam a navegação protegida e mostram uma mensagem compreensível, com PS/círculo disponíveis para fechar. Não anunciar sucesso por ausência de resposta. Fechar/reabrir não deve ficar preso nem herdar sucesso de uma abertura cancelada.
7. Coordenar os trabalhos Steam da barra com os do teclado, preservando os protocolos do teclado. Um comando de um ciclo antigo não deve executar depois da transferência nova por simples inversão da ordem das threads.
8. Validar comportamento, falhas e cancelamentos com dependências substituídas e executar o gate completo em pasta isolada. Sem novo número de versão, publicação em `.exe`/`bin/app`, migração de inicialização, commit ou push.

## Consequências e limites

O objetivo é eliminar a espera artificial de 1 s e reduzir o custo de despacho no cliente reconhecido. A medição anterior de 30–138 ms é de Home/Biblioteca; não é medição do novo ciclo nem da experiência física. Cliente sem suporte/acesso mantém o caminho lento e o bloqueio por confirmação.

O ciclo confirma o contexto observado da Steam. **Não neutraliza globalmente a saída virtual do DSX**, nem impede todo jogo que leia o controle diretamente de receber eventos. O perfil de desktop atual está vazio, mas uma configuração futura com teclado/mouse pode produzir eventos adicionais. Não prometer isolamento universal ou zero latência.

O teste físico deve conferir a Steam por trás da barra, toques rápidos/longos, dois dispositivos, atalhos, teclado, fechamento e reconexão. Só os resultados desse teste podem demonstrar o comportamento do conjunto no FSE.

## Evidências e reversão

Investigação, candidata e validações. Fontes anteriores preservadas em `bin/steam-input-rapido-base-20261029-001315`. Reversão seletiva dos arquivos desta mudança; não usar reset amplo, pois havia trabalho anterior sem commit. Publicados e residente permanecem preservados.

Candidata `bin/candidatos/XGB_P-1.0.6-20261029-052613/XGB_P.exe`, SHA-256 **90BF2608EDCC4CCFF23B70F3FF00B45A81C85EB7F69BE747F1F6B2A6D02B3CF7**. Gate 05:26:13–05:27:49, 20 execuções; Steam Input 95, teclado 93, transições 131 e sessão Steam 46 verificações aprovadas. `ReleaseApproved=false`. Sem teste de latência/duplicação física nem operação dos residentes.
