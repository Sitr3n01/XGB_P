# 0059 — Despacho local da navegação Steam

- **Status:** Aceita; implementada e publicada; teste físico final no FSE pendente
- **Data:** 2026-10-27
- **Complementa:** ADR 0058; preserva a política de foco e o ciclo Steam Input.

## Contexto

Após aprovar os destinos corrigidos, o usuário pediu reduzir o atraso entre Home e Biblioteca. Os registros reais mostram cerca de 1,1 s entre confirmar e retornar do despacho, enquanto a confirmação de foco leva poucos milissegundos. O temporizador também podia reentrar durante `Process.Start` e enviar a mesma página duas vezes. Chamada direta ao executável, `-ifrunning` e `-silent` não removeram o custo de aproximadamente um segundo.

## Decisão

- Para **somente** `steam://open/library` e `steam://open/library/collection/all-games`, usar o mesmo canal de encaminhamento do `steam.exe` já aberto, observado por engenharia reversa: `TempAppCmdLine` temporário em **HKLM\SOFTWARE\Valve\Steam**, visões 64 e 32 bits, seguido da sinalização do evento existente `Global\Valve_SteamIPC_Class`. A Steam consome o comando e remove o valor. Isso é comunicação de runtime; não é uma preferência permanente.
- Admitir esse caminho apenas para o executável local `steam.exe` de SHA-256 `F8A4CA44008C516EEBECAFAA4BC26772B2A6EB0FEF8034BCE56A3311E21AB705` (FileVersion 11.15.70.27), com PID coincidente nas duas visões, caminho real e mesma sessão Windows. Cache do hash condicionado a caminho/tamanho/data do arquivo. Atualização do cliente invalida a otimização e usa o protocolo normal.
- Abrir apenas chaves/evento **existentes**, sem criar infraestrutura, elevar por conta própria ou alterar ACLs. Sem acesso, evento ou cliente reconhecido, voltar ao protocolo antes de tentar qualquer escrita. Na máquina atual a barra já roda elevada por preferência anterior do usuário.
- Se qualquer visão tiver comando pendente, não sobrescrevê-lo: manter o pedido e tentar no tick seguinte, respeitando o limite existente. Após tentar escrever, uma falha é entrega incerta: não apagar o conteúdo nem repetir pelo protocolo normal.
- Bloquear reentrância do despacho e conferir a geração ao retornar, impedindo duplicação e transferência de foco após cancelamento. Ao terminar Steam Input, tentar o pedido já escolhido imediatamente, sem mais um tick de 150 ms.
- Manter destinos, descoberta/abertura do Big Picture, confirmação de foco e cancelamentos da ADR 0058. Não aplicar a otimização aos protocolos do ciclo Steam Input, Downloads, Configurações, lançamento de jogos ou demais comandos.

## Consequências e limites

A comunicação é interna e não documentada pela Valve; por isso fica restrita ao cliente conferido e tem alternativa pelo protocolo. O par de valores do Registro não constitui uma transação com outros remetentes externos: a verificação evita sobrescrever conteúdo observado, mas existe a janela de concorrência inerente ao canal nativo. Não é fila confiável para comandos arbitrários. Confirmação no log da Steam mede recebimento, não frame renderizado.

## Evidências

Comparação AB/BA no desktop elevado, quatro comandos por transporte: log da Steam em **1.090–1.153 ms** pelo shell e **30–138 ms** pelo canal local; medianas **1.125,86 / 77,29 ms**. O retorno da chamada local ficou em 8,72–21,06 ms. Amostra pequena e cenário sem gameplay; não generalizar a latência total na TV/FSE.

Bateria completa: 20 execuções aprovadas, incluindo 16 verificações isoladas do canal e regressões de reentrância, cancelamento, ocupação e retomada imediata após Steam Input. Continuidade, REA, publicação e reversão.
