# 0075 — Versão 1.0.7 e backup da 1.0.6

- **Status:** Publicada em `.exe` para teste; validação física pendente
- **Data:** 2026-10-29
- **Revisa:** versão da ADR 0073; incorpora a correção da ADR 0074.

## Contexto e decisão

O usuário pediu preservar a 1.0.6 como backup, criar em `.exe` a 1.0.7 com a correção de Steam Input, documentar e fazer commit.

1. Preservar integralmente `.exe` anterior em `bin/versao-1.0.7-base-20261029-092621/exe`, incluindo seis logs. Os 22 arquivos foram conferidos por hash antes da publicação. Executável anterior: **EDA3958B72778BD38A112B33B78B3AA7C14C7596151B974058D261A8291FEB05**.
2. Versionar produto 1.0.7/arquivo 1.0.7.0 nos sete arquivos de versão e incluir relatórios Steam Input/teclado no preparador. Oito fontes de build diferem da candidata da ADR 0074; outros 129 permanecem iguais. Nenhuma nova alteração da lógica de input nesta publicação.
3. Gerar pelo gate completo e publicar somente a lista fechada em `.exe`, preservando seus logs. A entrega passa a 18 arquivos, com ZIP. Retirar apenas o ZIP 1.0.6 já conferido no backup.
4. Preservar `bin/app` e legado 1.0.2 e o residente desse caminho. Antes/depois da cópia, PID 25472, criação 28/10 19:27:19 e caminho `bin/app/XGB_P.exe` foram lidos no mesmo handle. Nenhum processo usava `.exe`; o publicador recusa caminho desconhecido ou residente na pasta de destino.
5. HidHide somente consultado: `.exe/XGB_P.exe` já autorizado, inversão desligada, ocultação ligada. Sem configuração de driver, DSX, Steam, inicialização, energia ou FSE. Nenhum residente novo iniciado pelo agente.
6. Fazer commit local do estado reproduzível da 1.0.7, incluindo a continuidade 1.0.5/1.0.6 que ainda estava sem commit. Não fazer push. Binários locais e backups permanecem ignorados pelo Git; fontes, decisões e recibos são versionados.

## Resultado e limites

Publicada às **09:29:32 UTC−03**, executável **45A30F1CB8268D9F8DD8855B5E4C2938B700A38624F421D548D878C28DB57FD7**. Gate 09:27:23–09:28:59, 20 execuções; Steam Input 95, teclado 93, transições 131 e sessão Steam 46 verificações. `ReleaseApproved=false`.

O teste deve encerrar o residente antigo pela bandeja e abrir `.exe/XGB_P.exe` pelo Explorer. A publicação não certifica ganho físico de latência nem exclusividade global da entrada virtual. Relatório, guia/teste/reversão, recibos.
