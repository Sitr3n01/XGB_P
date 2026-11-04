# Subconjunto do gate que roda em maquina limpa (GitHub Actions): verificacoes estaticas, contrato dos
# recibos, compilacao das duas builds e as suites com fixtures. Os diagnosticos que leem o estado real
# do PC (audio, jogos, rede, Steam, modo Xbox) so rodam no gate local (verificar-qualidade.ps1).
param([string]$PastaSaida = 'bin\ci')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\build-comum.ps1')
. (Join-Path $repo 'scripts\verificacao-comum.ps1')
. (Join-Path $repo 'scripts\verificacao-estatica.ps1')
$policy = Get-Content -LiteralPath (Join-Path $repo 'quality\policy.json') -Raw | ConvertFrom-Json
$output = Resolve-XgbOutput -Repository $repo -Relative $PastaSaida
$snapshot = Get-XgbSourceSnapshot -Repository $repo
Invoke-XgbStaticChecks -Repository $repo -Policy $policy -Snapshot $snapshot | ForEach-Object { 'OK: ' + $_ }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\verificar-infraestrutura.ps1') -PastaSaida $PastaSaida | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Contrato de infraestrutura falhou.' }
'OK: contrato dos recibos de verificacao.'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\compilar-xgb.ps1') -PastaSaida $PastaSaida
if ($LASTEXITCODE -ne 0) { throw 'Compilacao falhou.' }
$tests = Join-Path $output 'XGB_P.Tests.exe'
$product = Join-Path $output 'XGB_P.exe'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($product).ProductVersion -ne $policy.releaseVersion) { throw 'Versao do binario difere da politica.' }
foreach ($flag in $policy.ciVerificationModes) {
    $receipt = Invoke-XgbVerification -Executable $tests -Flag $flag -TimeoutSeconds 600
    "OK: $flag | execucao $($receipt.RunId)"
}
# O produto recusa qualquer modo de verificacao (codigo 2): os testes nao vao no executavel publicado.
$probe = Start-Process -FilePath $product -ArgumentList @('--verify-motion') -PassThru -WindowStyle Hidden
if (-not $probe.WaitForExit(30000)) { $probe.Kill(); throw 'XGB_P.exe nao recusou --verify-motion a tempo.' }
if ($probe.ExitCode -ne 2) { throw "XGB_P.exe aceitou um modo de verificacao (codigo $($probe.ExitCode))." }
'OK: XGB_P.exe recusa modos de verificacao.'
if ((Get-XgbSourceSnapshot -Repository $repo).Fingerprint -ne $snapshot.Fingerprint) { throw 'Fontes mudaram durante a CI.' }
'XGB_P.exe SHA-256: ' + (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
