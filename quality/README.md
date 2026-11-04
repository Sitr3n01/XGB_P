# Gate de qualidade do XGB_P

Execute a partir da raiz do repositório:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verificar-qualidade.ps1
```

O gate compila o `XGB_P.exe` e o `XGB_P.Tests.exe`, roda as verificações estáticas, o contrato dos recibos e a bateria completa, e grava `reports/quality-gate.json` e `reports/quality-gate.md` (ignorados pelo Git). Não inicia a barra normalmente, não substitui o app em uso e não altera configurações. `scripts/preparar-candidato.ps1` roda o gate numa pasta nova de `bin/candidatos` e empacota o resultado.

A CI do GitHub roda um subconjunto, `scripts/verificar-ci.ps1`: tudo o que não depende do estado real da máquina.

## Política

`policy.json` define:

| Campo | Significado |
|---|---|
| `releaseVersion` | Versão da candidata; precisa bater com o executável e com a tag da release |
| `maxNewFileLines` | Teto de linhas para arquivo sem registro legado |
| `legacyLineCaps` | Teto por arquivo que já passava do limite; **só pode diminuir** |
| `maxLineLength` | Largura máxima de linha |
| `legacyLongLines` | Quantidade de linhas acima da largura, por arquivo; **só pode diminuir** |
| `requiredVerificationModes` | Modos que a bateria local precisa aprovar |
| `ciVerificationModes` | Modos que a CI roda na build de verificação |
| `minimumPauseRuns` | Rodadas mínimas de pausar/retomar/fechar em processos próprios |
| `manualApprovalRequired` | O que só a validação física aprova |

Arquivo abaixo do teto ainda pode ter problema de projeto: o teto impede a dívida de crescer, não certifica o código. A política não mede duplicação nem cobertura.

## Evidência

O inventário inclui fontes, XAML, manifesto, scripts e a política, inclusive arquivos ainda não rastreados, em ordem ordinal explícita (não varia com a cultura nem com a versão do PowerShell). Build e bateria conferem a impressão SHA-256 dos fontes antes e depois.

Recibo de sucesso exige execução nova, GUID, modo, hash exato do executável e intervalo UTC válido; logs antigos não bastam. O contrato do recibo tem testes negativos para cada campo adulterado, e o mutex de verificação ocupado faz a execução falhar.

Além da compilação sem avisos, a bateria exige as suítes de UI com substitutos, verificações temporais, recuperação, concorrência, falhas e carga, duas rodadas de pausar/retomar/encerrar em processos próprios, o fechamento de janela própria e todos os diagnósticos somente leitura. O teste que despacha protocolos reais do Steam Input (`--verify-steam-input-live`) fica fora da bateria automática.

O teste do observador mede a enumeração de processos e janelas e a associação de sessões com histórico isolado: 3 aquecimentos, 20 amostras, cerca de 30 segundos. Limites: p95 abaixo de 1000 ms, CPU abaixo de 5% de um núcleo e no máximo 16 handles retidos depois do aquecimento e da coleta. São tetos para detectar regressão grosseira, não um orçamento validado do app inteiro.

## Interpretação

- `AutomatedPassed` / `ReadyForManualTests`: todos os requisitos automáticos passaram.
- `ReleaseApproved`: sempre falso no gate. Só a matriz física aprova uma versão ([roteiro](../docs/guias/primeiros-testes-lancamento.md)).

Energia real nunca é executada pelo gate.
