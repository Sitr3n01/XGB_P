# 0078 — Histórico de ações e problemas; no Início, só o crítico

- **Status:** Aceita; implementada e publicada em `.exe` para teste (build 15554DEF). Teste físico pendente.
- **Data:** 2026-10-29

## Contexto

O rodapé de feedback (ADR 0035) mostrava toda operação no Início: andamento ("Abertura solicitada…", "Alternando…"), sucesso ("Jogo aberto.", "Janela fechada.") e avisos de verificação. Cada painel tinha uma única mensagem por vez, que sumia em 5 a 12 s. O único registro permanente era o `xgb.log`.

O usuário pediu que o rodapé do Início mostrasse só o crítico e que as Configurações ganhassem um histórico de ações e problemas. A investigação levantou cerca de 100 pontos que geram mensagens. O usuário aprovou estas recomendações:
- categorias;
- uma exceção para a espera da sessão Steam;
- histórico persistido.

## Decisão

1. **Categorias.** `FeedbackKind` ganha `Attention`: nada quebrou, mas a pessoa precisa agir. O novo valor entra no fim do enum, porque o histórico grava os valores numéricos.

   | Categoria | No Início | Exemplos |
   |---|---|---|
   | `Error` (falhou) | sim | iniciar/fechar/pausar negado, falha ao alternar, protocolos da Steam/lojas, "Algo falhou nesta ação", áudio/mídia |
   | `Attention` (precisa de ação) | sim | jogo/app pedindo confirmação, "A Steam ficou com o controle", "O Windows não liberou o retorno", Steam sem confirmar a entrada, administrador, jogo indisponível, abertura cancelada porque o jogo atual não fechou |
   | `Warning` (verificação) | não | "Não foi possível confirmar/verificar…", "O jogo saiu durante a transição", falha de leitura de apps |
   | `Pending`, `Success`, `Info` | não | andamento, concluído, cancelado pela navegação, "Aguarde a operação anterior" |

2. **Filtro só no Início.** O filtro vale apenas para o escopo `0`. Outros painéis continuam com o feedback contextual completo, como "Ligando o Wi-Fi…". Exceção marcada: `FeedbackNotice.Pinned` mantém visível, enquanto pendente, a espera "Aguardando a Steam concluir a sessão anterior…", que pode passar de 40 s.
3. **Histórico.** Uma entrada por operação, criada em `NewFeedback` e atualizada até o resultado em `FinishFeedback`, inclusive quando uma resposta atrasada já não aparece no rodapé. Repetições iguais em menos de 10 s viram contador. A área vem do painel: Início, Configurações, Wi-Fi, Bluetooth, Som, Desempenho, Controle e energia ou Lojas.
4. **Persistência.** O arquivo é `%LOCALAPPDATA%\XGB_P\history\actions.json`, com até 300 entradas e retenção de 7 dias.
   - **Gravação:** gravação atômica com `.bak`, feita num worker 2 s depois da primeira mudança. Ao encerrar, grava na hora. Uma sequência impede que uma gravação atrasada sobrescreva uma mais nova.
   - **Leitura:** descarta entradas inválidas, antigas ou do futuro. Uma operação que ficou pendente quando a barra encerrou volta como aviso "sem resultado".
5. **Configurações.** A seção ATIVIDADE abre a página **Histórico de ações e problemas**:
   - a entrada da seção mostra quantos problemas são novos;
   - a página lista os registros do mais recente para o mais antigo, até 60, com hora, área e contador;
   - um filtro alterna entre Tudo e Só problemas;
   - "Apagar histórico" pede confirmação;
   - abrir a página marca os problemas como vistos e destaca o que é novo desde a visita anterior.

## Consequências e limites

O Início fica silencioso nas operações normais. Quem quiser conferir um sucesso ou um aviso de verificação vai ao histórico. A classificação é uma decisão de produto e pode ser ajustada mensagem a mensagem. O histórico registra o texto mostrado ao usuário, não os detalhes técnicos, que continuam no `xgb.log`.

Um XGB_P iniciado por app de agente grava o histórico na cópia virtualizada do pacote (ver a memória de virtualização MSIX). O uso real é pelo Explorer ou pela inicialização do Windows.

## Evidências

Evidências e recibos:
- `historico-verificacao.txt`, com 24 verificações;
- `feedback-verificacao.txt`, com o filtro do Início;
- testes antigos ajustados para conferir o histórico quando a mensagem saiu do rodapé;
- renders `historico-acoes`, `historico-problemas`, `feedback-em-andamento` e `feedback-precisa-de-acao`.
