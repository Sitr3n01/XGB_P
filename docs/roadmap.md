# Roadmap — XGB_P V.3.0.0

## Situação

Os fontes da candidata 3.0.0 incorporam as mudanças até 08/11, preservando a estrutura pública. Este porte ainda não foi publicado no GitHub. [Comparação, limites e validação](atualizacao-publica-2026-11-08.md). Aprovação final depende da matriz física.

| Etapa | Estado |
|---|---|
| Recursos da barra (Início, Programas, Biblioteca, Som, Gravação, Desempenho, AMD, Configurações, Controle e energia, teclado) e integrações opcionais (G HUB, TV Samsung, despertar pelo PS) | Implementados |
| Gate automático (CI, substitutos, recibos, catracas) | Resultado do porte no relatório da atualização |
| CI pública (build, verificações estáticas, suítes com dados de teste, Gitleaks, CodeQL) | Configurada. Primeira execução no `main` (2.1.0, 04/11): Gitleaks e CodeQL aprovados; o job de build e suítes falhou em `--verify-motion` por depender da tela do runner. Corrigido em 13/11, sem execução no GitHub ainda (item 4) |
| Matriz física no modo Xbox | Pendente |
| Aprovação da versão | Pendente da matriz física |

## Pendente antes da aprovação

1. **Matriz física**, registrando APROVADO, FALHOU ou NÃO TESTADO por caso ([roteiro](guias/primeiros-testes-lancamento.md)):
   - abrir e fechar pelo PS no modo Xbox, com Steam e DSX, sobre jogos reais;
   - trocar de jogo e fechar o ativo e o de fundo, conferindo a ordem dos eventos da sessão Steam;
   - teclado virtual entrando no modo Xbox com um campo já em foco no desktop (não deve abrir) e num campo novo já no modo Xbox (deve abrir);
   - Gravação com uma captura real, foto aberta no Fotos, ajustes AMD num jogo sem perfil próprio, slider do mixer, DSX pela bandeja e emulação Xbox;
   - áudio, bateria do controle, TV e DPI.
2. **Energia real:** suspender e retomar, reiniciar e desligar, quando for possível interromper o PC. O gate nunca dispara esses efeitos.
3. **Sessão longa** na build final, com jogo em uso real e controle físico.
4. **CI no GitHub: correção feita, falta a primeira execução.** Na execução de 04/11 (`main` na 2.1.0) e na do PR do Dependabot em 09/11, a suíte com dados de teste falhou em "player deve caber na Home sem rolagem" (`tests/MediaPlayerVerification.cs`). Causa: as suítes pedem uma janela de 1920 × 1080, mas o Windows limita a janela à tela (o WPF devolve tela + 20 px), e a cena é recalculada pelo tamanho real; numa tela menor a barra encolhe e a Home rola. Na máquina do autor (1920 × 1080) o pedido cabia por pouco. Em 13/11, a build de testes passou a elevar o limite pelo `WM_GETMINMAXINFO` e a começar em 1920 × 1080 (`tests/VerificationScene.cs`); o produto continua cobrindo o monitor principal. Uma asserção nova confere que a janela chega a 3840 × 2160 numa tela de 1080p. Na primeira execução do PR #2 apareceu uma segunda dependência: a suíte do Steam Input dependia das animações do Windows estarem ligadas. Ela também foi corrigida e reproduzida localmente. Detalhes e a reprodução local no [relatório do porte](atualizacao-publica-2026-11-08.md#ci-no-github-tela-do-runner). Até a próxima execução no GitHub, o selo de CI do README continua mostrando a falha antiga.

Além da matriz anterior, testar pesquisa de programas, teclado manual sobre jogos, G HUB ligado/desligado/carregando, mudo no DSX, trilho persistido, TV local e retomadas PS/teclado/Unknown. TV real e energia física não foram ensaiadas neste porte.

## Melhorias planejadas

- **Compilador com builds reprodutíveis.** O `csc` do .NET Framework (C# 5) não tem `/deterministic`; cada build sai com hash diferente. Migrar para o compilador Roslyn com `/langversion:5`, `/deterministic` e `/pathmap` depois da validação física, para não trocar compilador e estrutura na mesma versão. Até lá, a integridade da release vem do `SHA256SUMS.txt` e do atestado de proveniência.
- **Assinatura de código** do executável, para remover o aviso do SmartScreen.
- **Reduzir a dívida de largura de linha** registrada em `quality/policy.json` (limites por arquivo, sem ampliar os tetos) e os cinco arquivos acima de 500 linhas.
- **Cobertura medida** de linhas e ramificações; hoje o gate não mede cobertura.
- **Mover para `tests/`** as verificações que ainda vivem em arquivos de `src/app` sob `#if XGB_VERIFY`.

## Fora do escopo

- Instalador e atualização automática.
- Distribuir ou documentar a ponte de ativação por Win+G ([ADR 0002](decisoes/0002-identidade-do-pacote-da-game-bar.md)).
- Ajustes da AMD por jogo: a [investigação de leitura](referencia/amd-perfis-por-jogo-2026-10-29.md) está registrada, mas a implementação foi suspensa pelo autor.
- Suporte a controles além do DualSense lido pelo HID e do controle Xbox virtual do DSX.
