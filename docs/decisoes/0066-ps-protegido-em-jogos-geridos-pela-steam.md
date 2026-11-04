# 0066 — PS protegido em jogos geridos pela Steam

- **Status:** Aceita; publicada na 1.0.3; **diagnóstico revisado pela ADR 0068**
- **Data:** 2026-10-28
- **Revisa:** a regra de "app da Steam atrás" das ADRs 0043/0044 (só pela pasta) e a recuperação do contexto 769 da ADR 0063 (item 4).

> **Revisão de 28/10, depois do teste físico da 1.0.3.** O gatilho do 769 não é o PS sem proteção. O `webhelper.txt` mostra que o overlay do jogo aberto fica visível no instante em que a barra inicia o outro jogo pela Steam (16:20:10, 19:54:35 e 12:42:32) e não é mais ocultado. O PS que chegava à Steam sem o ciclo às vezes era o que o ocultava (19:55:28). Com todo PS protegido na 1.0.3, o overlay do Genshin ficou preso até o jogo fechar. Os itens 1 a 3 continuam valendo; a prevenção e a espera pela soltura estão na [ADR 0068](0068-abertura-pelo-big-picture-e-foco-apos-soltar.md). O contexto abaixo é o registro original.

## Contexto

Depois da 1.0.2, o usuário relatou que a alternância entre Neva e Genshin ainda deixa uma camada invisível que impede o controle e parece o overlay da Steam ativo. Pediu a investigação das causas antes de qualquer alteração e, depois do diagnóstico e do desenho, autorizou: **"Implementar 1 a 3"**.

Observado nos logs do teste da 1.0.2 (`.exe/logs/xgb.log`, `Steam/logs/controller_ui.txt` e `controller.txt`, 27/10, 19:54–19:55):

- A Steam carrega a configuração do Guia (443510) em todo aperto do PS, inclusive sobre o Genshin.
- Depois que o jogo recebe o foco, a Steam registra o contexto dele e, menos de 1 s depois, o contexto 769 (interface da própria Steam). No `controller.txt`, o estado de leitura passa de 1 para 2, o mesmo do Big Picture na frente. Enquanto isso, o Windows mantém o jogo em primeiro plano (log do XGB_P e detector do teclado).
- Nesse teste, o 769 aconteceu em todas as entregas ao Genshin precedidas de um PS sem o ciclo de proteção (19:54:57, 19:55:08, 19:55:13 e 19:55:26), mesmo depois da recuperação da ADR 0063. Quando o Big Picture veio à frente por conta própria (19:55:28), a entrega seguinte ao Genshin (19:55:36) ficou limpa. As entregas ao Neva nesse teste ficaram no contexto dele.
- No teste anterior (16:21–16:22), o mesmo aconteceu com o Neva: o PS que abriu a barra sobre o Genshin e o PS que a fechou com o Neva na frente passaram sem proteção. Daí em diante, todo foco no Neva terminou em 769.
- O Genshin é um atalho da Steam (`shortcut:888398940:3899276376`, iniciado pelo `GenshinKeeperHidden.vbs`), fora das pastas da Steam. `SteamOwned` decidia só pelo caminho, então o ciclo `forceinputappid` não rodava sobre ele. O fechamento por PS nunca rodava o ciclo.

Inferido, não observado: que o 769 com o jogo na frente seja o overlay da Steam aberto dentro do jogo, por que ele fica invisível e a regra exata da Steam para alternar o overlay com o Guia. O log do overlay daquele horário foi sobrescrito.

## Decisão

1. **Abertura.** A Steam disputa o PS quando a janela da frente é da Steam pela pasta (como antes), quando a frente é desconhecida (como antes) **ou** quando pertence a um jogo em execução com chave `steam:` ou `shortcut:`. A comparação usa a janela ou o processo com a mesma criação; PID reutilizado não herda o jogo. Apps comuns, jogos locais e de outras lojas continuam sem o ciclo.
2. **Fechamento por PS.** Se a Steam viu o PS, a barra está visível sem o foco e a frente é um jogo gerido pela Steam (pelo catálogo), a barra retoma o foco, roda o ciclo e usa o fechamento adiado existente: some na hora e devolve o foco a esse jogo depois da liberação. Com a barra na frente, com app comum ou sem conseguir o foco, o fechamento continua como antes.
3. **769 com o jogo na frente.** Na transição para um jogo (não para o Início), se o último contexto da Steam for 769 depois de 750 ms de janela estável, com leitura recente, a transição termina com o aviso "A Steam ficou com o controle sobre o jogo." e uma linha própria no log, sem ponte de foco e sem anunciar estabilidade. Outro contexto divergente (por exemplo, 413080 residual) continua com a única ponte da ADR 0063. Um 769 passageiro seguido do contexto do jogo conclui normalmente.
4. Não enviar Guia, teclas ou saída HID; não alterar perfis da Steam/DSX, fixar appid de jogo, ocultar/matar janelas da Steam ou trazer o Big Picture à frente automaticamente. A recuperação pelo Big Picture fica de fora até haver evidência física.
5. Candidata separada; preservar `.exe`, `bin/app`, a raiz, o residente, configurações e dados. Sem publicação, instalação ou FSE/jogo novo nesta implementação; commit só a pedido do usuário, sem push.

## Consequências

Abrir a barra sobre um atalho da Steam passa a ter a entrada lenta (~1,1 s, painel a 45%) até a Steam confirmar o desktop. Um atalho da Steam iniciado fora dela também ganha esse atraso, sem outro efeito: acompanhar o `console_log` da Steam para distinguir os dois casos fica para depois, se o atraso incomodar. O fechamento com um jogo da Steam na frente devolve o controle ~1,1 s depois.

Se a Steam já tiver assumido o controle antes da correção, a barra não consegue desfazer: os PS protegidos nunca chegam à Steam, e a recuperação automática foi deliberadamente excluída. O aviso informa o estado em vez de repetir uma disputa de foco inútil.

A Steam também vê o PS nos atalhos do DSX (PS + botão), que não abrem a barra nem rodam o ciclo; não há 769 ligado a eles nos logs, mas não é garantido. O formato do `controller_ui.txt` é interno da Steam: se mudar, o item 3 volta a ser só confirmação de janela estável.

## Evidências

Continuidade, validação e candidata. Fixtures substituem foco, Steam, protocolo, contexto e relógio; nenhum jogo, FSE ou overlay real foi operado. O teste físico com Neva/Genshin permanece pendente.
