# 0088 — Versão 2.0.2 em `.exe` e `bin/app`

- **Status:** Aceita; publicada em `.exe` e `bin/app`. Teste físico pendente.
- **Data:** 2026-11-01

## Contexto

Depois da 2.0.0 ([ADR 0086](0086-versao-2.0.0-e-entrega-em-exe.md)), a única mudança de código foi a abertura automática do teclado virtual só com o modo Xbox lido na hora e estável ([ADR 0087](0087-teclado-automatico-so-com-modo-xbox-estavel.md)).

O usuário pediu para lançar essa correção como **v2.0.2** em `.exe` e `bin/app`, deixar a inicialização do Windows na nova versão e preparar a entrega para lançamento. Antes, encerrou o XGB_P pela bandeja: o log de `bin/app` registra a saída normal às 16:08:35.896.

## Decisão

1. **Versão 2.0.2:** produto `2.0.2` e arquivo `2.0.2.0` nos mesmos 7 arquivos de versão da 2.0.0: `AssemblyInfo.cs`, texto "Sobre" em `ConnectionsPanel.cs`, os dois manifestos, os contratos de `ProductReleaseVerification.cs`, o cabeçalho de `compilar-xgb.ps1` e `quality/policy.json`. Não houve 2.0.1; a numeração segue o pedido do usuário.
2. **Limite de tamanho do gate:** a correção da ADR 0087 levou `VirtualKeyboard.cs` a 1102 linhas, acima do teto legado de 1090. O teto não foi ampliado: a detecção automática passou para `KeyboardAutoOpen.cs` (55 linhas), e `VirtualKeyboard.cs` ficou com 1058.
3. **Backup integral antes:** `.exe` (40 arquivos) e `bin/app` (41 arquivos), ambos 2.0.0 `DE924B25…` com logs, copiados para `bin/versao-2.0.2-base-20261101-161202`, sem divergências.
4. **Candidata pelo gate:** `bin/candidatos/XGB_P-2.0.2-20261101-161241`, 20 execuções aprovadas. Só a candidata aprovada é publicada.
5. **Publicação nas duas pastas de uma vez:** os 24 artefatos da candidata, mais `manifest.json` e `candidate.json`, nas duas raízes. A `.exe` recebe também `XGB_P-2.0.2.zip`, no lugar do `XGB_P-2.0.0.zip`, que fica no backup. Os logs ficam como estavam.
6. **Inicialização:** a entrada `XGB_P` em HKCU\…\Run já apontava para `bin\app\XGB_P.exe --startup` e estava habilitada. Trocar o arquivo nesse caminho basta para o Windows abrir a 2.0.2; a entrada não foi alterada. Assim, o `LEIA-ME.md` publicado nas duas pastas já descreve as duas pastas na 2.0.2, sem a ressalva da 2.0.0.
7. **HidHide:** os dois caminhos já estavam autorizados. A configuração foi só lida.

## Consequências

A partir do próximo login, ou ao abrir pelo Explorer, roda a 2.0.2. `ReleaseApproved` continua `false`: o gate aprova a automação e libera os testes manuais, mas não substitui o teste físico no modo Xbox com DualSense, Steam e jogos. O executável não tem assinatura Authenticode e o pacote com identidade da Microsoft continua estritamente local ([ADR 0002](0002-identidade-do-pacote-da-game-bar.md)).

## Evidências

Backup, gate, publicação, inicialização e HidHide. Guia da 2.0.2.
