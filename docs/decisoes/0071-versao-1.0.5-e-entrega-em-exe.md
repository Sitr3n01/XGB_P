# 0071 — Versão 1.0.5 e entrega em .exe

- **Status:** Aceita; publicada localmente em `.exe`, validação física pendente
- **Data:** 2026-10-28
- **Revisa:** versão da ADR 0069; incorpora a ADR 0070.

## Contexto

A correção da Home estava na candidata separada `XGB_P-1.0.4-20261028-133533`, sem atualizar os executáveis publicados. O usuário pediu: **“crie a v1.0.5 na pasta .exe, documente tudo o que foi feito e me passe os comandos para liberar oHidHide.”** Confirmou o encerramento do residente pela bandeja; o log registra saída normal às 13:43:10, e a ausência de instâncias foi conferida antes da publicação.

## Decisão

1. Produto **1.0.5**, arquivo/assembly/manifests **1.0.5.0**. Alterar os mesmos sete arquivos de versão: `AssemblyInfo.cs`, `ConnectionsPanel.cs`, dois manifests, `ProductReleaseVerification.cs`, `quality/policy.json` e `scripts/compilar-xgb.ps1`. Os outros 122 arquivos do inventário permanecem iguais à candidata da ADR 0070.
2. Gerar candidata isolada pelo gate completo; publicar apenas a lista fechada de 15 arquivos em `.exe`, com guia/manifestos/recibos e ZIP 1.0.5. Backup integral da `.exe` anterior, logs preservados por hash. O ZIP anterior sai da pasta de entrega depois de copiado e conferido no backup.
3. A 1.0.5 contém as opções sem ícones **Fechar jogo e iniciar** / **Cancelar**, fechamento normal e saída confirmada antes de abrir o próximo, além do retorno ao Início ao fechar o último jogo ou a barra sem jogo ativo no modo Xbox. PS continua abrindo a barra. As demais proteções da ADR 0070 são preservadas.
4. `bin/app` e `bin/XgbDesignPreview.exe` continuam na 1.0.2. O pedido especificou `.exe`; a entrada existente de inicialização não é migrada.
5. Conferir HidHide somente por leitura e entregar comandos ao usuário. `.exe/XGB_P.exe` já está liberado; a atualização no mesmo caminho mantém essa autorização. Nenhuma alteração de dispositivos/DSX/Steam/HidHide pelo agente.
6. Não iniciar residente pelo agente. Sem commit/push, instalação, certificado, energia, jogo ou ativação de FSE. Publicação local autorizada não equivale a aprovação física final.

## Consequências

Abrir `.exe/XGB_P.exe` pelo Explorer usa a 1.0.5; a inicialização existente ainda usa a 1.0.2. A candidata antiga é histórico de validação, não a entrega corrente. Configurações/histórico/diário permanecem no perfil existente; as conferências do agente sobre AppData dizem respeito à visão do próprio pacote.

## Evidências e reversão

Relatório completo, guia e comandos HidHide, evidências. Backup `bin/versao-1.0.5-base-20261028-134417/exe`, contendo a 1.0.4 anterior (`C43BB38F…`). Encerrar normalmente, conferir a recuperação de pausas e restaurar executável/metadados correspondentes, preservando logs e dados atuais. Não usar reset/clean nem apagar diretórios de dados.
