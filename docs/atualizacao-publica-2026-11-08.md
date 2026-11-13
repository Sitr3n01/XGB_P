# Atualização da cópia pública — 08/11/2026

Os fontes da candidata pública passam de **2.1.0 para 3.0.0**, incorporando o desenvolvimento até 08/11. A base pública era o commit `9119316`, com as ADRs 0089–0094 já portadas. A atualização foi preparada na cópia local; não cria commit, tag, push ou release no GitHub.

## Diferenças incorporadas

| Área | Base pública | Candidata 3.0.0 |
|---|---|---|
| Home | Atalhos anteriores | Voltar ao jogo antes do Acesso rápido; quatro retângulos sem texto, seis quadrados; apps centralizados |
| Programas | Sem painel de pesquisa | Menu Iniciar, pesquisa por nome/alias, ranking e histórico, teclado pelo controle, opções de abertura |
| Navegação | Trilho lateral | Trilho ou título original com seta; escolha persistida e aplicada na hora |
| AMD | Conjunto inicial ADLX | Mais opções globais, seletores flutuantes, sliders com atraso e alinhamento comum |
| Feedback AMD | Sucessos no rodapé | Sucessos rotineiros no histórico; erros, recusas e resultados incertos continuam visíveis |
| Teclado | Digitação sem foco | Composição com foco na abertura manual sobre jogos; Menu envia e Círculo cancela |
| Fones | Bateria Bluetooth | Também LIGHTSPEED pelo agente local do G HUB, somente GET |
| Microfone | Leitura nos painéis | Avisos do Core Audio, ícone vermelho no mudo e alerta de gravação sem voz |
| DualSense | Bateria sem o novo vetor | SVG aprovado, com traço adicional de 0,35 DIP e licença própria |
| TV | Sem integração | Núcleo Tizen, importação local, DPAPI, comandos, confirmação de tela e automação optativa |
| Despertar | Sem diagnóstico integrado | Prontidão, scripts reversíveis por instância Sony/Bluetooth e registro por CycleId |
| Configurações | Organização anterior | Dez seções por assunto; Sobre lê a versão do executável |

As ADRs **0095–0114** foram reescritas para a documentação pública, com as decisões finais e sem relatos, caminhos ou recibos da máquina. Estados intermediários de layout não substituem as escolhas finais das ADRs 0099, 0105 e 0112.

## Adaptações da distribuição pública

- Preservados `src/app`, `src/lib`, `tests` e os workflows públicos. O produto e `XGB_P.Tests.exe` continuam separados; o produto recusa modos de teste.
- Verificações novas e diagnósticos foram mantidos em `tests/`; verificações centrais/renderização também foram retiradas do arquivo principal do produto. Foram reduzidos 17 limites existentes de linhas/largura; nenhum foi ampliado.
- O inventário de fontes cobre o SVG. A inspeção estática também examina arquivos novos ainda não adicionados ao Git.
- A CI inclui os 11 casos BootPS com registro/WMI substituídos. A bateria local conhece os novos modos de programas, despertar e TV; rede e equipamentos reais ficam fora da CI.
- O pacote usa uma lista explícita: executável de produto, guia, licença Apache, NOTICE e licença Controllercons. Não inclui testes, dados de configuração ou scripts de instalação da ponte.

## Higiene e arte de terceiros

A ponte com identidade de pacote da Microsoft, seus manifestos/certificados e binários não foram importados. Também ficaram fora sons, logs reais, capturas do desktop, evidências brutas, configurações e credenciais locais. Exemplos da TV usam modelo e identificadores fictícios. Preferências específicas de dispositivos da máquina não são padrões de distribuição.

O SVG do DualSense corresponde geometricamente ao [PS5 outline de Controllercons 2.1](https://github.com/controllercons/controllercons.github.io/blob/master/versions/2.1/svg/outline/ps5.svg). Copyright © 2023 Kieran McClung; [SIL OFL-1.1](../licenses/Controllercons-OFL-1.1.txt). A licença acompanha fontes e pacote; o código do app continua Apache-2.0. O traço de 0,35 DIP é aplicado pelo renderer WPF.

## Validação

Validação final aprovada em `bin/validacao-public-3.0.0-20261108`:

- CI local: limites, APIs proibidas, scripts ASCII/sintaxe, conteúdo distribuível, contrato dos recibos, duas builds e recusa dos modos de teste pelo produto.
- **Seis execuções com recibos novos:** `--verify-motion`, `--verify-performance`, `--verify-release`, duas rodadas de `--verify-games-control-live` e uma de `--verify-apps-close-live`. Os três últimos operam somente processos/janelas criados pelo teste.
- **11 verificações BootPS** com registro e WMI substituídos. A suíte funcional inclui **41 verificações de despertar**, **155 do núcleo TV** e **70 do título**, além das demais áreas.
- Conferência visual dos renders de Home, AMD e TV gerados por fixtures. Não são capturas do desktop real.
- Revisão dirigida dos **421 arquivos candidatos**: nenhum caminho privado, identidade proibida, fixture da TV identificada como local ou valor de credencial foi encontrado pelos padrões examinados. Links da documentação atualizada conferidos; geometria do SVG igual à origem. Essa revisão não substitui Gitleaks/CodeQL, que continuam configurados para execução no GitHub.
- Pacote gerado e conferido por entrada/hash em `bin/pacote-public-3.0.0-20261108/XGB_P-3.0.0-win-x64.zip`, com cinco arquivos e sem build de testes. `SHA256SUMS.txt` acompanha o ZIP.

| Artefato | Tamanho | SHA-256 |
|---|---|---|
| Produto (`XGB_P.exe`) | 1.041.408 bytes | `447C923ED801289A6EE28718525B671CE30ADBE5148C8ED0A10320271435C5A1` |
| Build de testes (`XGB_P.Tests.exe`) | 1.914.880 bytes | `06E846E0AD9BA82AE975028FAC930BFD52C71F9547CC91F88ACC09572E7AF25C` |
| ZIP local | 427.873 bytes | `7B96E4B318CD8C297B6A3448EF0FCE27B22F90FB674347513584551E6A578119` |
| Inventário de fontes/política | — | `9BA120EBA426247CE6D2EF76A29B83B303C8792BACF35F412D5E8CE26C0E5E03` |

Os 213 arquivos mapeados do aplicativo/bibliotecas/testes têm contraparte na estrutura pública. A interpretação de `XGB_VERIFY` confirmou que métodos de verificação, renderização de inspeção e classes de suíte não entram no produto. Recibos completos estão em `public-sync-validation.json` dentro da pasta de validação; revisão dirigida em `bin/public-sync-audit.json`. Artefatos de teste ficam em `bin/`, ignorados pelo Git.

O compilador C# 5 não produz builds determinísticas: uma compilação posterior da mesma árvore pode ter outro hash. O ZIP acima é um artefato local, sem atestado de proveniência do GitHub e sem publicação remota. A bateria local completa com diagnósticos de equipamentos/rede não foi executada neste porte.

Não foram executados comandos contra a TV, ativação/reversão da política real, suspensão/reinício/desligamento, alteração do HidHide ou instalação de pacote/certificado. A residente diária e os executáveis do projeto de uso não foram substituídos.

## Apresentação pública — 13/11/2026

Sem mudança no produto. Os READMEs (inglês e português) foram reescritos para a 3.0.0 no formato dos outros projetos do autor, a capa `docs/images/social-preview.png` foi refeita e as telas desatualizadas da 2.1.0 foram trocadas por renders da build de testes acima ([origem de cada imagem](images/README.md)). O NOTICE passou a citar Logitech e Samsung nas marcas e na declaração de não afiliação.

Conferências feitas para os textos, em 13/11:

- **Nenhuma release nem tag existe no GitHub.** O README deixou de mandar baixar a "última release" e passou a orientar a compilação a partir dos fontes; o selo de release foi trocado por um selo estático da versão candidata.
- **CI no GitHub:** a execução de 04/11 no `main` (2.1.0) e a do PR do Dependabot em 09/11 falharam no mesmo ponto de `--verify-motion`, "player deve caber na Home sem rolagem". Gitleaks e CodeQL passaram. Corrigido na seção seguinte; o README e o [roadmap](roadmap.md) não afirmam a CI aprovada no GitHub.
- Os renders do README vêm da validação acima. Depois da correção da CI, os 12 renders de origem foram gerados de novo e conferidos pixel a pixel na área recortada: são idênticos.

## CI no GitHub: tela do runner

**Causa.** As suítes pedem uma janela de 1920 × 1080 (`window.Width = 1920; window.Height = 1080`), e a cena é recalculada pelo tamanho real da janela (`SceneLayout`, `FitScene(e.NewSize)`). O Windows não deixa uma janela passar do tamanho máximo de arrasto, que por padrão é a tela; o WPF ainda reescreve `Width`/`Height` com o valor limitado. Conferido num programa isolado: nesta máquina (tela 1920 × 1080), um pedido de 3840 × 2160 voltou como 1940 × 1100. Numa tela menor, a cena encolhe e os painéis passam a rolar. Aqui o pedido de 1920 × 1080 cabia por pouco, por isso a falha só aparecia no runner.

**Reprodução local, em cópias descartáveis.** Um gancho de `WM_GETMINMAXINFO` limitou a janela a 1044 × 788, como numa tela de 1024 × 768. A resolução real do runner não foi conferida.

| Código | Sem correção | Com correção |
|---|---|---|
| 2.1.0 (`737704f`) | `--verify-motion` falhou em "Home/jogos: controle desce do principal para o outro aberto" | não testado |
| 3.0.0 | `--verify-motion` falhou em "Animacoes: cena nao acompanhou a resolucao" | `--verify-motion`, `--verify-performance` e `--verify-release` aprovados |

O ponto de falha muda com o código e com o tamanho da tela; nenhuma das falhas acontece com a janela no tamanho pedido. A falha da 2.1.0 é a de um teste que, no desenvolvimento anterior à cópia pública, já tinha falhado de forma intermitente, sem causa determinada. Isso sugere, sem provar, que parte dessa instabilidade vinha do tamanho da tela em uso.

**Correção (só na build de testes).** `tests/VerificationScene.cs` adiciona, na criação do HWND, um gancho que eleva o `ptMaxTrackSize` do `MINMAXINFO`. O gancho do WPF roda depois e lê o valor elevado, então a janela aceita o tamanho pedido. Em seguida a janela vai para 1920 × 1080. `SceneLayout.StartScene` registra o gancho dentro de `#if XGB_VERIFY`; o `XGB_P.exe` não muda e continua cobrindo o monitor principal. Em `tests/MotionVerification.cs`, uma asserção nova confere que a janela chega a 1920 × 1080 e a 3840 × 2160. Antes, nesta máquina, o caso "4K" rodava de fato em 1940 × 1100.

**Validação em 13/11.** `verificar-ci.ps1` foi aprovado na árvore real (`bin/ci-tela-fixa-20261113`). A cópia com a tela emulada passou nos três modos da CI.

**Segunda dependência: animações do Windows.** No PR #2, com a tela corrigida, o mesmo job passou do ponto anterior e falhou em "Steam Input: entrada de 250 ms com e sem espera da Steam". A barra respeita a opção do Windows de animar controles (`SystemParameters.ClientAreaAnimation`, em `Motion.SystemMotionOff`). Com ela desligada, não há animação de entrada e a duração lida é 0. As outras suítes de animação já forçam `motionSystemOverride = false`; a do Steam Input não forçava. Numa cópia descartável, simular as animações desligadas (com a tela de 1024 × 768 emulada) reproduziu exatamente esse erro. Isso indica que o runner tem as animações desligadas, sem que a configuração dele tenha sido conferida. A correção em `tests/SteamInputVerification.cs` força as animações ligadas durante essa asserção e restaura o valor anterior. Com as duas emulações juntas, `--verify-motion`, `--verify-performance` e `--verify-release` passaram, e `verificar-ci.ps1` foi aprovado na árvore real (`bin/ci-animacoes-20261113`).

## Pendências de lançamento

Validação física de Steam/FSE/DSX, jogos, áudio, G HUB, teclado, TV/DPI e energia permanece pendente. PS observado após retomada é correlação; `PhysicalCauseConfirmed=false`. A integração de TV ainda depende de configuração local prévia; não há assistente universal de instalação nesta versão. Esta atualização não equivale a `ReleaseApproved`.

Para revisar a alteração, use `git diff` e a lista de arquivos novos. Commit, tag e push continuam sendo passos separados, somente quando solicitados.
