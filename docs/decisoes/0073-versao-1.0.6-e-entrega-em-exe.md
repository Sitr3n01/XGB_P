# 0073 — Versão 1.0.6 e entrega em .exe

- **Status:** Aceita; publicada em `.exe`, validação física pendente
- **Data:** 2026-10-28
- **Revisa:** versão da ADR 0071; incorpora a ADR 0072.

## Contexto

A espera pela sessão Steam estava na candidata separada `XGB_P-1.0.5-20261028-155518`, ainda sem substituir a 1.0.5 publicada. O usuário pediu **criar a versão 1.0.6 na pasta `.exe` com essa correção e entregar comandos de terminal para o driver**. Confirmou o encerramento do residente pela bandeja. O log registra saída normal às 16:01:39 e foi conferida a ausência de instâncias antes da cópia.

## Decisão

1. Produto **1.0.6**, arquivo/assembly/manifests **1.0.6.0**, nos sete arquivos de versão habituais. A lógica da ADR 0072 permanece igual à candidata aprovada para testes. O preparador passa a incluir `steam-sessao-verificacao.txt` na entrega/ZIP: oito arquivos diferentes no inventário, outros 125 iguais.
2. Gerar candidata isolada pelo gate completo e publicar a lista fechada de **16 arquivos** somente em `.exe`. Incluir guia, relatório específico de sessão, manifestos/recibos e ZIP. Backup integral da 1.0.5 anterior, com logs preservados por hash; retirar da entrega apenas o ZIP anterior já salvo no backup.
3. Manter `bin/app` e `bin/XgbDesignPreview.exe` na 1.0.2. O pedido especifica `.exe`; a inicialização existente não é migrada.
4. HidHide consultado somente por leitura. CLI 1.5.230.0, driver Running, lista normal, ocultação ativa e caminho `.exe/XGB_P.exe` já autorizado. Nenhum dispositivo de jogo presente foi encontrado. Entregar comandos ao usuário para registrar o caminho e conferir modo/driver/dispositivos; o agente não altera HidHide, serviços, DSX ou lista de dispositivos.
5. Não iniciar residente pelo agente. Não alterar GenshinKeeper, configurações, instalação, certificados, energia ou FSE. Sem commit/push, que não foram pedidos. A publicação local não certifica funcionamento físico do controle ou resolução visual no Neva.

## Consequências

`.exe/XGB_P.exe` passa a conter a correção da sessão Steam; pode aguardar os aproximadamente 25 segundos do lançador antes de abrir o próximo jogo. O prazo de 45 segundos, as confirmações e os cancelamentos da ADR 0072 permanecem. A autorização HidHide por caminho continua válida após atualizar o executável.

## Evidências e reversão

Relatório, guia/terminal HidHide, evidências. Backup integral `bin/versao-1.0.6-base-20261028-160155/exe`, com a 1.0.5 anterior **B0D4F614…**. Para reverter, encerrar normalmente, conferir a recuperação de pausas e restaurar os arquivos/metadados correspondentes do backup, preservando os logs e os dados atuais. Não precisa revogar a autorização HidHide do mesmo caminho.
