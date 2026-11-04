# 0058 — Navegação explícita da Home e entrega da Biblioteca

- **Status:** Aceita; implementada e publicada; teste físico no FSE pendente
- **Data:** 2026-10-27
- **Revisa:** ADR 0039 quanto à preservação da página pelo botão Início; complementa ADRs 0041, 0046 e 0054.

## Contexto

O usuário confirmou que Início do Big Picture mantinha a Biblioteca na tela e autorizou corrigir. O handler anterior apenas alternava para a janela existente. Os registros de 12:10:52, 12:10:55 e 12:10:58 confirmam a ativação daquela janela, sem pedido de navegação. Biblioteca usava protocolo e ocultação imediata, sem a confirmação de foco aplicada pelo alternador.

## Decisão

- Início reutiliza a janela do Big Picture, mas envia `steam://open/library` antes da transferência de foco. No cliente atual essa rota chama `Navigator.Home()`. `steam://open/bigpicture` somente solicita o modo da Steam; não define a página.
- Biblioteca no FSE continua usando `steam://open/library/collection/all-games`, cujo destino é **Coleções → Todos**. Não alegar seleção da aba Todos os jogos. Desktop/API indisponível preservam `steam://open/games`.
- Os dois destinos no Big Picture aguardam o fim do ciclo pendente de Steam Input antes de despachar navegação ou transferir o foco. O alternador existente confirma foreground antes de ocultar e observa novamente depois.
- Sem janela disponível, solicitar Big Picture uma vez e aguardar sua descoberta; só então enviar a página. Limite de oito segundos, consulta temporária a cada 150 ms. Enumeração incerta falha sem abrir outro cliente.
- Fechar/reabrir, navegar na barra, escolher outro app/destino, sair do ciclo FSE, perder foco para outro app ou descartar a prévia cancela pedidos antigos. A janela que acabamos de solicitar pode assumir o foco e receber a página, desde que sua identidade seja a descoberta pelo backend.
- Não alterar geometria, resolução, opções Steam/DSX/HidHide ou a política de ativação da barra. Não simular teclas para navegar na Steam.

## Consequências

O nome Início passa a corresponder à navegação real. A confirmação de foreground não confirma a página renderizada: esta exige observação visual separada. Falhas e timeout preservam a barra; o temporizador não permanece ativo sem pedido pendente. Downloads, Configurações e demais protocolos continuam pelo fluxo anterior.

## Evidências

Em 27/10, aproximadamente 12:56 UTC−03, a Steam real foi levada à coleção Todos e a rota `steam://open/library` voltou à Home com Jogos recentes. Teste no desktop, sem ativar FSE. A bateria completa passou em 20 execuções do build `A2A21EED…`; abrange handlers, foco, cancelamentos, abertura tardia e animações com substitutos. Uso físico pelo DualSense no modo Xbox permanece pendente.

Continuidade, publicação e reversão · [referência do cliente](../referencia/biblioteca-steam-big-picture.md).
