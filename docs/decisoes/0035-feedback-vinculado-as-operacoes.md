# 0035 — Feedback vinculado às operações

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto e causas confirmadas

O usuário relatou mensagem de início persistente mesmo com jogo aberto e pediu revisão do rodapé operacional. A inspeção confirmou:

1. `CheckGameLaunch` limpava `launchingGame` ao observar o runtime, mas não atualizava o texto "Iniciando…".
2. As mensagens eram um único texto sem identidade, contexto ou expiração. Uma resposta tardia podia substituir uma ação mais recente ou aparecer em outro painel.
3. Lançamento e fechamento eram conferidos também após falha de leitura, usando o snapshot preservado. Leituras iniciadas antes do comando não eram distinguidas das posteriores.
4. Encerramento forçado anunciava "encerrado" após pedir o kill, antes da próxima observação da saída. Pausa/retomada anunciavam conclusão antes de confirmar o estado observado dos threads.
5. O timeout de abertura reabria a barra após 180 segundos, podendo interromper um jogo não identificado. Na Home, o texto do rodapé tinha uma linha com reticências, cortando informações importantes.

## Decisão

- Centralizar o rodapé em `OperationFeedback.cs`, com estados **informação, pendente, sucesso, aviso e erro**, ícone e cor complementar. O texto sempre expressa o significado; a cor não é o único indicador.
- Cada operação recebe uma referência própria. A conclusão só altera o slot se essa mesma operação ainda for a mais recente do contexto. Respostas substituídas continuam no log e não ressuscitam mensagens expiradas.
- Contexto por painel e, nas Conexões, por subpágina. Atualizar operação da Home não contamina Configurações; concluir Wi-Fi não escreve no Bluetooth.
- Mostrar o texto imediatamente, com quebra de linha também na Home. Ajustar a altura com o mecanismo de movimento existente; preservar movimento reduzido.
- Sucesso desaparece em **5 s**, informação em **6 s**, aviso/erro em **12 s**. O relógio corre mesmo com a barra oculta, para não mostrar conclusões antigas na próxima abertura. Pendências têm limite próprio e nunca são apresentadas como sucesso apenas por tempo decorrido.
- Lançar: "abertura solicitada/aguardando processo"; após 10 s, esclarecer que o jogo pode já estar aberto e ainda não ter sido identificado. Runtime observado substitui a pendência por "aberto". Após 180 s, informar impossibilidade de confirmar e permitir nova tentativa, **sem reabrir a barra**. Identificação tardia pode atualizar uma mensagem ainda vigente, sem reviver aviso expirado.
- Fechar, inclusive forçar: aguardar observação da ausência do processo. Pausar/retomar: separar o pedido da confirmação observada. Preservar o registro de processos pausados e o mecanismo de retomada segura existente.
- Só uma observação válida, iniciada depois do comando relevante, pode confirmar. Falha de leitura não transforma o snapshot antigo em evidência nova. Há fallback de incerteza se o worker atrasar/falhar; ele não infere sucesso nem rouba foco.
- Consultar jogos a cada **500 ms** enquanto há operação pendente, voltando a **2 s** depois. Fechamento de janela usa 500 ms enquanto a leitura da Home está ativa; o rodapé tem timer de 200 ms.
- Para apps abertos, sucesso significa **janela fechada/ativada**, sem afirmar que o processo residente do aplicativo terminou.
- Capturar a referência do feedback nos callbacks de rádio, Wi-Fi, Bluetooth e Luz noturna. Seus textos mantêm a distinção de pedido aceito, como "conexão solicitada"; não inventam prova de conexão física.

O fechamento normal ainda pode pedir confirmação dentro do jogo e o fluxo existente de oferecer fechamento forçado após espera é mantido; nunca há encerramento forçado automático. Essa revisão substitui a pendência O1 sobre reabrir a barra no timeout: não reabrir.

## Evidências e limites

O registro consolidado da revisão reúne os fluxos, tempos, mapa dos arquivos, build/backup e pendências para a próxima sessão.

`FeedbackVerification.cs` reproduz a persistência de início, snapshots falhos/anteriores ao pedido, resposta atrasada, mudança de painel, expiração oculta, abertura tardia, timeout sem foco, worker sem leitura, falha de despacho, fechamento normal/forçado e confirmação de pausa. Comandos, snapshots e relógio são substituídos. A bateria completa inclui os testes nativos seguros nos processos próprios, sem operar jogos reais do usuário.

Renders de pendência, sucesso e erro longo são conferidos. Isso prova o comportamento determinístico e o layout; os tempos e a detecção com jogos reais, launchers, anti-cheat e privilégios diferentes dependem da validação do usuário. Não modifica o algoritmo de identificação de jogos, as APIs de controle nativo ou as preferências de áudio/inicialização.
