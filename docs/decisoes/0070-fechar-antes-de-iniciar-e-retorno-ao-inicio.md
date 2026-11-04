# 0070 — Fechar antes de iniciar e retorno ao Início do Big Picture

- **Status:** Aceita; publicada na 1.0.5 (ADR 0071), teste físico pendente
- **Data:** 2026-10-28
- **Revisa:** abertura simultânea da ADR 0068; complementa Home/saída das ADRs 0063 e 0064.

## Contexto

O usuário relatou novos problemas entre sessões simultâneas e pediu analisar o histórico/log, retirar os ícones das opções de abertura e oferecer **Fechar jogo e iniciar** ou **Cancelar**. Esclareceu o retorno sem jogo: **ao fechar o último jogo ou fechar a barra sem jogo ativo; o PS continua abrindo a barra**.

O teste da 1.0.4 em `.exe/logs/xgb.log` mostra abertura do Neva pela rota Big Picture às 13:19:03, seguida de contexto 769 nas duas voltas ao Genshin. `webhelper.txt` registra `gamepadoverlay_uid27684` visível de 13:19:03 até 13:19:44. Após o Neva fechar, o Genshin voltou a ter contexto próprio às 13:19:50. A tentativa da ADR 0068 não resolveu o caso observado. Isso não demonstra que toda coexistência falha nem que a nova política já foi validada fisicamente.

Ao fechar o último jogo, o retorno foi armado às 13:21:29, cancelado às 13:21:30 e a saída foi confirmada às 13:21:31. O Big Picture aparece na frente na sequência. O cancelamento não registra o HWND exato: a disputa com a Steam nesse instante é inferida pela ordem dos eventos e pela regra do código que rejeitava qualquer janela diferente do jogo/barra/fallback.

## Decisão

1. Abrir um jogo fechado com outro aberto exige confirmação na Home. As duas opções têm só texto, sem ícone nem coluna reservada. Foco inicial em Cancelar; ○/Esc cancela sem operações. A pergunta identifica os jogos que serão fechados e o destino. Se já houver vários abertos, todos os identificados na pergunta precisam encerrar antes da abertura.
2. A confirmação pede fechamento **normal**, pelas APIs existentes. Aguarda liberação da Steam e soltura do botão; confirma saída pela identidade nativa e por leitura válida posterior ao comando. Não basta sumir do catálogo. Só então despacha uma única abertura do destino. A abertura direta do segundo jogo por timeout da ADR 0068 deixa de existir.
3. Recusa, janela ausente ou prazo de fechamento esgotado cancelam a abertura. A oferta de forçar permanece no fluxo próprio, com nova escolha; nunca há encerramento forçado automático. Outra janela escolhida, reabertura/navegação da barra, mudança de modo ou jogo/instância nova também cancelam a abertura pendente. Um PID reutilizado não herda autorização.
4. Ao fechar a barra sem jogo no modo Xbox confirmado, pedir explicitamente o Início do Big Picture pelo fluxo existente de navegação/foco. Aguardar ciclo Steam e botão solto. Não redirecionar ao abrir a barra, no desktop, com API incerta, leitura inválida/anterior ao comando ou abertura pendente/incerta.
5. Ao fechar o último jogo, aceitar a **janela exata do Big Picture** como destino esperado mesmo se o shell a colocar na frente antes da confirmação. Pedir a página Início após confirmar a saída. Outro aplicativo continua cancelando o retorno. Saída nativa do último jogo também é observada sem exigir que ele tenha sido o último foco registrado.
6. Jogos simultâneos iniciados por fora continuam listados/retomáveis; não fechar nada sem a escolha explícita na barra. Conservar proteção do PS, espera pela soltura, leitura HID, preferências e identidade de processo.

## Consequências

A Home deixa de oferecer preservar a sessão atual ao iniciar outro jogo. O jogo pode exigir confirmação própria; nesse caso, o próximo não abre. O retorno sem jogos passa a escolher uma página específica, em vez de simplesmente devolver o foco à janela anterior.

Primeira entrega em candidata local separada, com metadados 1.0.4 e publicados/residente preservados. Depois, o usuário autorizou a [publicação em `.exe` como 1.0.5](0071-versao-1.0.5-e-entrega-em-exe.md), após encerrar o residente. `bin/app` e caminho legado continuam na 1.0.2; sem commit/push, configuração ou jogo/FSE operado pelo agente.

## Evidências

Registro e validação, com trechos dos logs, imagem da fixture, candidata/gate e roteiro físico. Testes substituem jogos, protocolos, foco e controle; os testes nativos do gate operam somente processos/janelas criados por eles.
