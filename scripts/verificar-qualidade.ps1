# Gate local deterministico. Aprovacao automatica libera testes manuais; nao aprova lancamento.
param([string]$PastaSaida = 'bin\qualidade')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\build-comum.ps1')
. (Join-Path $repo 'scripts\verificacao-estatica.ps1')
$started = [DateTime]::UtcNow
$policy = Get-Content -LiteralPath (Join-Path $repo 'quality\policy.json') -Raw | ConvertFrom-Json
$output = Resolve-XgbOutput -Repository $repo -Relative $PastaSaida
$reportFolder = Join-Path $repo 'reports'
New-Item -ItemType Directory -Force -Path $reportFolder | Out-Null
New-Item -ItemType Directory -Force -Path $output | Out-Null
$checks = New-Object 'System.Collections.Generic.List[string]'
$failures = New-Object 'System.Collections.Generic.List[string]'
$snapshot = Get-XgbSourceSnapshot -Repository $repo
$battery = $null
try {
    foreach ($check in (Invoke-XgbStaticChecks -Repository $repo -Policy $policy -Snapshot $snapshot)) { $checks.Add($check) }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\verificar-infraestrutura.ps1') -PastaSaida $PastaSaida
    if ($LASTEXITCODE -ne 0) { throw 'Contrato de infraestrutura falhou.' }
    $checks.Add('Recibos adulterados, antigos, de outro modo/build ou sem sucesso sao recusados.')
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\verificar-bateria.ps1') -PastaSaida $PastaSaida
    if ($LASTEXITCODE -ne 0) { throw 'Bateria completa falhou.' }
    $battery = Get-Content -LiteralPath (Join-Path $output 'design-preview\bateria-completa.json') -Raw | ConvertFrom-Json
    if ($battery.Success -ne $true -or $battery.ReadOnlyDiagnosticsIncluded -ne $true) { throw 'Bateria incompleta.' }
    foreach ($mode in $policy.requiredVerificationModes) {
        if (@($battery.Executions | Where-Object { $_.Mode -eq $mode -and $_.Success -eq $true }).Count -eq 0) { throw "Modo obrigatorio ausente: $mode" }
    }
    if (@($battery.Executions | Where-Object { $_.Mode -eq '--verify-games-control-live' }).Count -lt $policy.minimumPauseRuns) {
        throw 'Repeticoes de pausa insuficientes.'
    }
    if ((Get-XgbSourceSnapshot -Repository $repo).Fingerprint -ne $snapshot.Fingerprint) { throw 'Fontes mudaram durante o gate.' }
    $checks.Add('Bateria completa, recibos novos, diagnosticos obrigatorios e fontes preservadas durante a execucao.')
}
catch { $failures.Add($_.Exception.Message); Write-Output ('BLOQUEADO: ' + $_.Exception.Message) }
$ready = $failures.Count -eq 0
$report = [ordered]@{
    Version = 2; CandidateVersion = $policy.releaseVersion; StartedUtc = $started.ToString('o'); CompletedUtc = [DateTime]::UtcNow.ToString('o')
    AutomatedPassed = $ready; ReadyForManualTests = $ready; ReleaseApproved = $false
    SourceFingerprint = $snapshot.Fingerprint; SourceFiles = $snapshot.Files.Count
    Checks = @($checks); Failures = @($failures); ManualApprovalPending = @($policy.manualApprovalRequired)
    Battery = $battery
    Limits = @('Sem cobertura de linhas/branches medida.', 'Carga sintetica nao comprova FSE/FPS ou estabilidade em jogos reais.',
        'As verificacoes rodam em XGB_P.Tests.exe, compilado dos mesmos fontes de src que XGB_P.exe.',
        'A ponte opcional de ativacao por Win+G nao faz parte deste repositorio.')
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportFolder 'quality-gate.json') -Encoding UTF8
$markdown = @('# Gate local de pre-lancamento', '',
    ('Candidata: ' + $policy.releaseVersion), ('Automacao aprovada: ' + $ready), 'Lancamento aprovado: False', '', '## Verificacoes')
$markdown += @($checks | ForEach-Object { '- ' + $_ })
$markdown += @('', '## Bloqueios automaticos')
$markdown += @($failures | ForEach-Object { '- ' + $_ })
$markdown += @('', '## Aprovacao humana pendente')
$markdown += @($policy.manualApprovalRequired | ForEach-Object { '- ' + $_ })
$markdown += @('', '## Limites')
$markdown += @($report.Limits | ForEach-Object { '- ' + $_ })
$markdown | Set-Content -LiteralPath (Join-Path $reportFolder 'quality-gate.md') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $reportFolder 'quality-gate.json') -Destination $output
Copy-Item -LiteralPath (Join-Path $reportFolder 'quality-gate.md') -Destination $output
if (-not $ready) { exit 1 }
Write-Output 'APROVADO para iniciar testes manuais locais. Lancamento continua pendente.'
