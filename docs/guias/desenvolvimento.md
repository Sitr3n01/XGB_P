# Desenvolvimento

## Ferramentas

Nenhum SDK, NuGet ou IDE é necessário.

| Ferramenta | Onde | Uso |
|---|---|---|
| `csc.exe` do .NET Framework 4.x (C# 5) | `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319` | Compilação |
| Metadados WinRT | `%WINDIR%\System32\WinMetadata` | APIs de mídia, rede e dispositivos |
| Windows PowerShell 5.1 | sistema | Scripts de build e verificação |
| Git | — | Verificações estáticas (`git ls-files`, `git diff --check`) |

## Duas builds, um código

| Executável | Fontes | Uso |
|---|---|---|
| `XGB_P.exe` | `src/**/*.cs` | Produto. Recusa `--verify*` e `--inspect-motion` com código 2 |
| `XGB_P.Tests.exe` | `src/**/*.cs` + `tests/*.cs`, com `/define:XGB_VERIFY` | Verificação: suítes com dados de teste, diagnósticos da máquina e o microbenchmark |

As verificações fazem parte da `partial class Preview` e acessam o estado privado dela. Código de verificação que precisa ficar num arquivo de `src/app` vai dentro de `#if XGB_VERIFY`. O produto nunca deve referenciar símbolos de teste: se a build de produto não compila, o símbolo está do lado errado.

## Comandos

Compilar as duas builds em `bin\app`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\compilar-xgb.ps1
```

Compilar e rodar a suíte funcional e temporal (`--verify-motion`, com dados de teste):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\compilar-xgb.ps1 -Verificar
```

Gate de CI, o mesmo do GitHub Actions (verificações estáticas, contrato dos recibos, duas builds, `--verify-motion`, `--verify-performance`, `--verify-release` e a recusa dos modos de teste pelo produto):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verificar-ci.ps1
```

Bateria completa, com os diagnósticos que leem a máquina real e as rodadas em processos próprios:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verificar-bateria.ps1
```

Gate local completo (estáticas + recibos + bateria + modos obrigatórios), que gera `reports\quality-gate.md`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verificar-qualidade.ps1
```

Candidata local com gate, pacote e manifesto em `bin\candidatos`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\preparar-candidato.ps1
```

Ferramentas de diagnóstico somente leitura (`bin\DualSenseReader.exe`, `bin\ModoXboxMonitor.exe`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\compilar-ferramentas.ps1
```

Microbenchmark de apresentação contra a build de verificação:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\medir-performance.ps1 -Executavel bin\app\XGB_P.Tests.exe -PastaSaida bin\bench
```

## O que cada camada prova

| Camada | Onde roda | Prova | Não prova |
|---|---|---|---|
| Verificações estáticas | CI e local | Tetos de tamanho e largura, APIs proibidas, scripts ASCII e válidos, nada privado versionado | Comportamento |
| Suítes com dados de teste | CI e local | Lógica da UI, navegação, transições, contratos de Steam, jogos, energia e teclado com substitutos | Comportamento com Steam, DSX e jogos reais |
| Rodadas em processos próprios | Local | Pausar, retomar e fechar por identidade em processos criados pelo teste | Jogos reais e anti-cheat |
| Diagnósticos somente leitura | Local | Que as APIs reais respondem nesta máquina (áudio, rede, jogos, lojas, inicialização, observador) | Ação sobre o sistema |
| Matriz física | Manual | Uso real no modo Xbox | — |

A aprovação de uma versão depende da matriz física ([roteiro](primeiros-testes-lancamento.md)); o gate automático libera os testes manuais, não a versão.

## Política de qualidade

`quality/policy.json` ([detalhes](../../quality/README.md)):

- **Tamanho:** 500 linhas para arquivo novo; os arquivos maiores têm teto próprio em `legacyLineCaps`.
- **Largura:** 160 caracteres; as linhas longas existentes estão contadas por arquivo em `legacyLongLines`.
- Os dois registros legados **só podem diminuir**. Ao reduzir a dívida de um arquivo, baixe o número correspondente na política.

## Release

1. Atualize a versão em `quality/policy.json` (`releaseVersion`), `src/app/AssemblyInfo.cs`, `src/app/DesignPreview.manifest`, o texto de versão em `src/app/ConnectionsPanel.cs` e as verificações de `tests/ProductReleaseVerification.cs`.
2. Mova as entradas de `Unreleased` do `CHANGELOG.md` para uma seção `## [x.y.z] — data`.
3. Rode o gate local completo na máquina real e registre o resultado.
4. Crie a tag: `vX.Y.Z-rc.N` para pre-release, `vX.Y.Z` depois da matriz física. O workflow `release.yml` confere a tag contra a política, roda o gate de CI, empacota, gera o `SHA256SUMS.txt` e o atestado de proveniência e publica a release com as notas do CHANGELOG.

## Limites conhecidos do build

- O `csc` do .NET Framework não tem `/deterministic`: duas compilações dos mesmos fontes geram hashes diferentes. Por isso o `build.json` registra a impressão digital dos fontes junto com o hash do executável, e a release publica o atestado de proveniência. A migração para Roslyn está no [roadmap](../roadmap.md).
- O executável não tem assinatura Authenticode.
