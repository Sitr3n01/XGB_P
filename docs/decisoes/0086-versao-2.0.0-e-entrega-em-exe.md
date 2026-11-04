# 0086 — Versão 2.0.0 e entrega em `.exe` e `bin/app`

- **Status:** Aceita; publicada em `.exe` e `bin/app`. Teste físico pendente.
- **Data:** 2026-10-29

## Contexto

Depois da referência 1.0.8 (`E0AF6A55…`), o trabalho de 29/10 acumulou histórico de ações, DSX pela bandeja, emulação Xbox ([ADRs 0078–0080](0078-historico-de-acoes-e-inicio-so-critico.md)), os painéis Gravação e AMD ([0081](0081-aba-amd-captura-pelo-relive.md), [0082](0082-painel-amd-ajustes-pelo-adlx.md)), a revisão visual ([0083](0083-icones-gravacao-ordem-do-trilho-e-interruptores.md)), miniaturas/Fotos ([0084](0084-miniaturas-e-fotos-no-modo-xbox.md)) e o slider do mixer ([0085](0085-slider-do-mixer-sem-mute.md)). Tudo isso estava em candidatas ainda marcadas 1.0.8.

O usuário pediu para lançar essa versão em `.exe` como **v2.0.0**, guardar a 1.0.8 como backup e receber os comandos do HidHide. Pediu também para deixar de lado, por ora, os ajustes por jogo da AMD.

## Decisão

1. **Versão 2.0.0:** produto `2.0.0` e arquivo `2.0.0.0` em `AssemblyInfo.cs`, nos dois manifestos, na política do gate, no texto "Sobre" e nos contratos de `ProductReleaseVerification.cs`. Nenhuma lógica muda em relação à candidata 456BC8D5 ([ADR 0085](0085-slider-do-mixer-sem-mute.md)); só os 7 arquivos de versão diferem.
2. **Backup da 1.0.8:** a `.exe` anterior (build 1.0.8 `15554DEF…`, com logs) foi copiada inteira para `bin/versao-2.0.0-base-20261029-172358/exe` antes da publicação. A referência 1.0.8 original `E0AF6A55…` continua em `bin/app` e `bin/referencia-1.0.8`.
3. **`.exe` primeiro, depois `bin/app`:** o pedido nomeou a `.exe`, e `bin/app`, o caminho da inicialização do Windows, ficou na 1.0.8. Em seguida, o usuário pediu para atualizar também o `bin/app`. Antes, a pasta foi copiada inteira para `bin/versao-2.0.0-base-20261029-172358/app` (referência 1.0.8 `E0AF6A55…`, também preservada em `bin/referencia-1.0.8`).
4. **HidHide:** os caminhos `.exe\XGB_P.exe` e `bin\app\XGB_P.exe` já estavam autorizados, e trocar o arquivo neles mantém a autorização. Os comandos ficam no guia e são executados pelo usuário.
5. **Perfis por jogo da AMD:** suspensos a pedido do usuário. A investigação de leitura fica registrada em [referência](../referencia/amd-perfis-por-jogo-2026-10-29.md), sem implementação.

## Consequências

A inicialização do Windows (`bin/app/XGB_P.exe --startup`) passa a abrir a 2.0.0, sem mudar a entrada de inicialização. O `LEIA-ME.md` publicado nas duas pastas foi gerado antes da atualização do `bin/app` e ainda diz que ele ficou na 1.0.8; o guia em `docs/guias` está atualizado. O arquivo não foi trocado, para não divergir do hash registrado no manifesto.

`ReleaseApproved` continua `false`: o gate não substitui o teste físico no modo Xbox com DualSense, Steam e jogos.

## Evidências

Publicação, backup, gate e HidHide. Guia da 2.0.0.
