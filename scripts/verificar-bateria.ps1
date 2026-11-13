# Compila numa pasta e roda toda a verificacao automatica na build XGB_P.Tests.exe, parando na primeira falha:
# suites funcional e temporal, testes reais seguros (so processos e janelas criados pelo teste)
# e diagnosticos so de leitura. Nao instala pacote, nao altera configuracoes e nao toca jogos, apps,
# radios, redes ou dispositivos do usuario. -SemLeituras pula os diagnosticos que leem o estado real do PC.
param([string]$PastaSaida = 'bin\verificacao', [ValidateRange(1, 10)][int]$RepetirPausa = 2, [switch]$SemLeituras)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\verificacao-comum.ps1')
. (Join-Path $repo 'scripts\build-comum.ps1')
$batteryStarted = [DateTime]::UtcNow
Set-Location $repo
$saida = Resolve-XgbOutput -Repository $repo -Relative $PastaSaida
$exe = Join-Path $saida 'XGB_P.Tests.exe'
$log = Join-Path $saida 'design-preview\preview.log'
function Show-Error {
    if (Test-Path -LiteralPath $log) {
        $text = Get-Content -LiteralPath $log -Raw; $i = $text.LastIndexOf('ERRO:')
        if ($i -ge 0) { $text.Substring($i, [Math]::Min(900, $text.Length - $i)) }
    }
}
$ErrorActionPreference = 'Continue'
$out = powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\compilar-xgb.ps1') -Verificar -PastaSaida $PastaSaida 2>&1
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
$out | ForEach-Object { $t = "$_"; $t.Substring(0, [Math]::Min(120, $t.Length)) }
if ($code -ne 0) { Show-Error; throw "Suites falharam ($code)." }
$flags = @('--verify-performance', '--verify-release')
for ($n = 0; $n -lt $RepetirPausa; $n++) { $flags += '--verify-games-control-live' }
$flags += '--verify-apps-close-live'
if (-not $SemLeituras) {
    $flags += '--verify-games-live', '--verify-connections-live', '--verify-stores-live', '--verify-audio-live', '--verify-windows-live',
        '--verify-media-live', '--verify-perf-live', '--verify-status-live', '--verify-sounds-live', '--verify-launch-live',
        '--verify-activation-live', '--verify-keyboard-live', '--verify-accent-live', '--verify-observer-live',
        '--verify-programs-live', '--verify-wake-live', '--verify-tv-live'
}
$receipts = @()
foreach ($flag in $flags) {
    try { $receipt = Invoke-XgbVerification -Executable $exe -Flag $flag }
    catch { Show-Error; throw }
    $receipts += $receipt
    "$flag -> aprovado | execucao $($receipt.RunId)"
}
$buildMetadata = Get-Content -LiteralPath (Join-Path $saida 'build.json') -Raw | ConvertFrom-Json
if ((Get-XgbSourceSnapshot -Repository $repo).Fingerprint -ne $buildMetadata.Sources.Fingerprint) {
    throw 'Fontes mudaram durante a bateria. Os resultados nao aprovam a arvore atual.'
}
$functional = @(Get-ChildItem -LiteralPath (Join-Path $saida 'design-preview') -Filter 'execucao-*.json' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } |
    Where-Object { $_.Mode -eq '--verify-motion' -and $_.Success -eq $true -and
        $_.ExecutableSha256 -eq $buildMetadata.TestsExecutableSha256 -and [DateTime]::Parse($_.StartedUtc).ToUniversalTime() -ge $batteryStarted })
if ($functional.Count -ne 1) { throw 'A bateria nao possui exatamente uma suite funcional/temporal nova.' }
[ordered]@{
    Version = 2; StartedUtc = $batteryStarted.ToString('o'); CompletedUtc = [DateTime]::UtcNow.ToString('o')
    Success = $true; ReadOnlyDiagnosticsIncluded = (-not $SemLeituras)
    ExecutableSha256 = $buildMetadata.ExecutableSha256; TestsExecutableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    SourceFingerprint = $buildMetadata.Sources.Fingerprint; Executions = @($functional) + @($receipts)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $saida 'design-preview\bateria-completa.json') -Encoding UTF8
'XGB_P.exe       SHA-256: ' + $buildMetadata.ExecutableSha256
'XGB_P.Tests.exe SHA-256: ' + (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
