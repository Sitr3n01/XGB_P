# 0069 — Versão 1.0.4 e entrega em .exe

- **Status:** Aceita; publicada em `.exe`, validação física pendente
- **Data:** 2026-10-28
- **Revisa:** a versão da ADR 0067; incorpora a ADR 0068.

## Contexto

As correções da ADR 0068 (segundo jogo aberto pelo Big Picture e foco só depois de soltar o botão) estavam só na candidata `XGB_P-1.0.3-20261028-130214`. O `.exe` tinha a 1.0.3 sem elas. O usuário escolheu: **"Versione como 1.0.4 com as correções da ADR 0068, gere pelo gate, publique em .exe com backup e faça commit."** Nenhum XGB_P estava em execução antes da publicação.

## Decisão

1. Produto **1.0.4**, assembly/arquivo/manifests **1.0.4.0**, nos mesmos sete arquivos de versão. A lógica das ADRs 0066 e 0068 entra sem mudança. Guia novo `docs/guias/xgb-p-1.0.4.md`, com o comportamento da ADR 0068; o guia da 1.0.3 fica como estava.
2. Candidata isolada com gate completo, publicada **somente em `.exe`** pelo mesmo procedimento da ADR 0067: backup da pasta inteira, lista fechada conferida por hash, ZIP `XGB_P-1.0.4.zip` e `.exe/logs` preservado.
3. `bin/app/XGB_P.exe` e `bin/XgbDesignPreview.exe` continuam na 1.0.2: o pedido nomeou só `.exe`, e `bin/app` é o caminho da inicialização do Windows.
4. Não iniciar residente pelo agente; sem configuração, inicialização ou dados alterados. O HidHide já libera `.exe\XGB_P.exe` desde o comando que o usuário executou em 28/10. Commit a pedido do usuário, sem push.

## Consequências

A inicialização do Windows ainda abre a 1.0.2 de `bin/app`. A candidata 1.0.3-20261028-130214 fica superada; a liberação dela no HidHide pode ser revertida pelo usuário com `--app-unreg`.

O registro `publicacao.json` saiu com a versão vazia. No script de publicação, a variável dos dados de versão do executável tinha o mesmo nome, sem diferença de maiúsculas, que o parâmetro de texto da versão. O registro foi corrigido, com nota, e o script ajustado. Os arquivos publicados não foram afetados, e a versão foi conferida no executável (1.0.4 / 1.0.4.0).

## Validação e reversão

Evidências da publicação. Para reverter, encerrar a instância, conferir a recuperação e o Steam Input e restaurar os arquivos de `bin/versao-1.0.4-base-20261028-131446/exe` (a 1.0.3), sem apagar `.exe/logs`, catálogo, histórico ou diário de pausas.
