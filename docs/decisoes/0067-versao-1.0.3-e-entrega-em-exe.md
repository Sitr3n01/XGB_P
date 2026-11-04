# 0067 — Versão 1.0.3 e entrega em .exe

- **Status:** Aceita; publicada em `.exe`, validação física pendente
- **Data:** 2026-10-28
- **Revisa:** a entrega em três caminhos da ADR 0065, só nesta versão; incorpora a ADR 0066.

## Contexto

Depois dos commits da ADR 0066, o usuário pediu: **"essa versão nova deve estar em .exe no projeto, e eu esqueci, versione ela para a V1.0.3, aí eu testo"**. O residente 2332 da inicialização do Windows (`bin/app`, iniciado às 11:17:51) estava em execução na conferência das 12:19 e encerrou normalmente às 12:30:06, segundo o log, antes da publicação; o agente não o operou. O `.exe/XGB_P.exe` da 1.0.2 não estava em uso: a abertura exclusiva do arquivo foi concedida antes da troca.

## Decisão

1. Produto **1.0.3**, assembly/arquivo/manifests **1.0.3.0**, Sobre, teste de versão, política e comentário do script de compilação: os mesmos sete arquivos de versão da 1.0.2. A lógica da ADR 0066 entra sem mudança.
2. Candidata isolada com gate completo antes de publicar. Guia novo `docs/guias/xgb-p-1.0.3.md`, copiado como `LEIA-ME.md`. O guia da 1.0.2 volta ao texto da 1.0.2.
3. Publicar **somente em `.exe`**: backup da pasta inteira antes, remoção apenas dos arquivos da entrega anterior, cópia da lista fechada da entrega e do ZIP `XGB_P-1.0.3.zip`, conferidos por hash. A pasta `.exe/logs` fica no lugar, sem alteração.
4. `bin/app/XGB_P.exe` e o caminho legado `bin/XgbDesignPreview.exe` continuam na 1.0.2: o pedido nomeou só `.exe`, e `bin/app` é o caminho da inicialização do Windows. Atualizar os dois juntos fica para quando o usuário pedir.
5. Não iniciar residente pelo agente; o usuário abre `.exe/XGB_P.exe` pelo Explorer. Sem migração de configuração, inicialização, HidHide ou dados. Commit a pedido do usuário, sem push.

## Consequências

Até atualizar `bin/app`, a inicialização do Windows abre a 1.0.2, e convivem duas versões no projeto. Para testar a 1.0.3, encerre antes o residente pela bandeja: há uma instância normal por sessão. O caminho `.exe` é o mesmo do teste de 27/10, em que o HidHide bloqueou o Bluetooth e o USB funcionou.

A publicação local não certifica FSE, DualSense, jogos, TV, energia física ou lançamento.

## Validação e reversão

Evidências da publicação. Para reverter, encerrar a instância, conferir a recuperação e o Steam Input e restaurar os arquivos de `bin/versao-1.0.3-base-20261028-123508/exe`, sem apagar `.exe/logs`, catálogo, histórico ou diário de pausas.
