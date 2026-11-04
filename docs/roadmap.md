# Roadmap — XGB_P V.2.1.0

## Situação

A 2.1.0 é a primeira versão pública. O comportamento do app é o mesmo da 2.0.2, que está em uso diário na máquina do autor; a 2.1.0 muda a forma de compilar, verificar e entregar ([CHANGELOG](../CHANGELOG.md)). Ela sai como **pre-release** até a matriz física abaixo ter resultado registrado.

| Etapa | Estado |
|---|---|
| Recursos da barra (Início, Biblioteca, Som, Gravação, Desempenho, AMD, Configurações, Energia, teclado) | Implementados |
| Gate automático (bateria de 20 execuções, recibos, catracas) | Aprovado na máquina do autor |
| CI pública (build, verificações estáticas, suítes com dados de teste, Gitleaks, CodeQL) | Configurada; primeira execução no GitHub pendente |
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
4. **Primeira execução da CI no GitHub:** as suítes com dados de teste rodam numa máquina sem áudio, controle ou modo Xbox; qualquer dependência do ambiente que aparecer vira correção ou ajuste documentado.

## Melhorias planejadas

- **Compilador com builds reprodutíveis.** O `csc` do .NET Framework (C# 5) não tem `/deterministic`; cada build sai com hash diferente. Migrar para o compilador Roslyn com `/langversion:5`, `/deterministic` e `/pathmap` depois da validação física, para não trocar compilador e estrutura na mesma versão. Até lá, a integridade da release vem do `SHA256SUMS.txt` e do atestado de proveniência.
- **Assinatura de código** do executável, para remover o aviso do SmartScreen.
- **Reduzir a dívida de largura de linha** registrada em `quality/policy.json` (1.126 linhas acima de 160 caracteres em 114 arquivos) e os cinco arquivos acima de 500 linhas.
- **Cobertura medida** de linhas e ramificações; hoje o gate não mede cobertura.
- **Mover para `tests/`** as verificações que ainda vivem em arquivos de `src/app` sob `#if XGB_VERIFY`.

## Fora do escopo

- Instalador e atualização automática.
- Distribuir ou documentar a ponte de ativação por Win+G ([ADR 0002](decisoes/0002-identidade-do-pacote-da-game-bar.md)).
- Ajustes da AMD por jogo: a [investigação de leitura](referencia/amd-perfis-por-jogo-2026-10-29.md) está registrada, mas a implementação foi suspensa pelo autor.
- Suporte a controles além do DualSense lido pelo HID e do controle Xbox virtual do DSX.
