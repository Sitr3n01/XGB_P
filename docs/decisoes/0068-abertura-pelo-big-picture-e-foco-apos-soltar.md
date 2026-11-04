# 0068 — Abertura pelo Big Picture e foco só depois de soltar o botão

- **Status:** Aceita; publicada na 1.0.4 (ADR 0069), teste físico pendente
- **Data:** 2026-10-28
- **Revisa:** o diagnóstico da ADR 0066 (gatilho do contexto 769); complementa as ADRs 0063 e 0066.

## Contexto

No teste físico da 1.0.3 (28/10, 12:40–12:44), a barra da Steam apareceu sobre o Genshin de forma inconsistente: só o rodapé "MENU / SELECIONAR / VOLTAR". O usuário pediu a análise dos logs e a verificação de vazamento do input do controle para o jogo nas transições. Depois, autorizou: **"Corrigir os dois"**.

Observado:

- `Steam/logs/webhelper.txt`: o overlay do Genshin (`gamepadoverlay_uid28864`) foi criado oculto às 12:42:19 e ficou **visível às 12:42:32**, no instante em que o XGB_P pediu `steam://rungameid/2420660` (Neva). Ele nunca mais foi ocultado; só foi removido quando o Genshin fechou, às 12:44:19. A partir daí, todo foco no Genshin terminou em 769: as 6 trocas pela barra, os fechamentos e até a volta da Ferramenta de Captura às 12:44:00. O Neva ficou limpo nas 5 trocas.
- O mesmo nos testes de 27/10: o overlay do Neva ficou visível às 16:20:10, quando o Genshin foi iniciado; o do Genshin às 19:54:35, quando o Neva foi iniciado, e só sumiu às 19:55:28, depois de um PS que chegou à Steam sem proteção.
- O Big Picture vir à frente sozinho, sem o Guia (12:42:40), não ocultou o overlay.
- Leitor do DualSense contra o log da barra: em 2 de 8 trocas, o foco foi para o jogo com o X ainda apertado. Às 12:43:00 (Neva), o foco saiu 23 ms depois do X, que foi solto 97 ms depois. Às 12:43:07 (Genshin), 28 ms depois, solto 43 ms depois. Nas outras 6, a barra esperava a Steam liberar o controle, e o X já estava solto.
- O fechamento pelo círculo devolve o foco depois da animação de 167 ms. Os círculos registrados duraram de 108 a 293 ms. Nenhum fechamento por círculo ocorreu nesse teste.

Inferido, não observado: que a Steam mostra o overlay do jogo aberto para exibir a abertura do outro; que iniciar com o Big Picture na frente evita isso; o que faz o overlay desenhar só o rodapé no Genshin.

A ADR 0066 atribuía o 769 ao PS que chegava à Steam sem o ciclo. Esse PS não cria o estado: às vezes era o que o desfazia. Com o PS sempre protegido na 1.0.3, o overlay ficou preso até o jogo fechar. O aviso da ADR 0066 funcionou nas 6 trocas para o Genshin.

## Decisão

1. **Abertura.** No modo Xbox confirmado, ao abrir pela Steam (`steam://`) um jogo que ainda não está aberto, com outro jogo `steam:`/`shortcut:` aberto e o Big Picture disponível, a barra espera a Steam liberar o controle e o botão ser solto, traz o Big Picture para a frente (até três pedidos) e só então despacha a abertura. Big Picture que não vem à frente em 4 s ou falha ao ativar: abertura direta, como antes. Fechar/reabrir a barra, trocar de painel, de modo ou de escolha cancela, com o aviso "Abertura cancelada.". Sem outro jogo da Steam aberto, no desktop ou para jogos fora da Steam, nada muda.
2. **Foco só depois de soltar.** A primeira entrega de foco ao jogo numa transição e o retorno ao fechar a barra esperam a soltura dos botões apertados (exceto PS e analógicos), no máximo 500 ms. A soltura dispara a entrega na hora. No fechamento, a barra some (opacidade 0) e só devolve o foco depois, pelo mesmo adiamento do ciclo da Steam.
3. A proteção do PS e o aviso de 769 da ADR 0066 continuam. Não há recuperação automática do overlay já preso: o Big Picture à frente não o oculta, e o XGB_P não envia o Guia.
4. Candidata separada; preservar `.exe`, `bin/app`, a raiz, residentes, configurações e dados. Commit a pedido do usuário, sem push.

## Consequências

Abrir um segundo jogo passa a mostrar o Big Picture por um instante antes da abertura. Um botão preso atrasa a entrega em até 500 ms. Se o overlay já estiver preso (por exemplo, por uma abertura feita fora da barra), ele continua preso até o jogo fechar; a barra só avisa.

## Evidências

Evidências do teste da 1.0.3 e continuidade. Fixtures substituem foco, Steam, protocolos, relógio e eventos do controle; nenhum jogo, FSE ou overlay real foi operado.
