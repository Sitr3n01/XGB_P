# Prepara uma candidata local isolada: gate completo, pacote e manifesto. Nao instala, publica, inicia a
# barra nem substitui o aplicativo em uso.
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$policy = Get-Content -LiteralPath (Join-Path $repo 'quality\policy.json') -Raw | ConvertFrom-Json
$relative = 'bin\candidatos\XGB_P-' + $policy.releaseVersion + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$folder = [IO.Path]::GetFullPath((Join-Path $repo $relative))
if (Test-Path -LiteralPath $folder) { throw 'A pasta desta candidata ja existe.' }
New-Item -ItemType Directory -Path $folder | Out-Null
# Os dados do usuario nao podem mudar durante o gate (as verificacoes usam substitutos).
function Get-UserDataHashes {
    $rows = @()
    foreach ($name in @('settings\launch.json', 'settings\ui-sounds.json', 'settings\keyboard.json', 'settings\appearance.json', 'games\catalog.json',
        'games\history.json', 'games\paused-by-xgb.json', 'settings\steam-input-forcado.txt', 'settings\steam-input-teclado.txt', 'history\actions.json')) {
        $path = Join-Path (Join-Path $env:LOCALAPPDATA 'XGB_P') $name
        $rows += [pscustomobject]@{ Name = $name; Sha256 = $(if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path).Hash } else { $null }) }
    }
    ConvertTo-Json -InputObject @($rows) -Compress
}
$dataBefore = Get-UserDataHashes
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\verificar-qualidade.ps1') -PastaSaida $relative
if ($LASTEXITCODE -ne 0) { throw "Candidata bloqueada. Consulte $folder\quality-gate.md" }
if ($dataBefore -ne (Get-UserDataHashes)) {
    throw 'Dados do usuario mudaram durante a preparacao. Nenhum valor sera restaurado automaticamente; confira a origem.'
}
$package = Join-Path $relative 'pacote'
$result = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\empacotar.ps1') -PastaBuild $relative -PastaSaida $package
if ($LASTEXITCODE -ne 0) { throw 'Empacotamento falhou.' }
$packaged = ($result -join "`n") | ConvertFrom-Json
$build = Get-Content -LiteralPath (Join-Path $folder 'build.json') -Raw | ConvertFrom-Json
[ordered]@{
    Version = $policy.releaseVersion; CreatedUtc = [DateTime]::UtcNow.ToString('o'); LocalTestingOnly = $true
    AutomatedGatePassed = $true; ReleaseApproved = $false
    SourceFingerprint = $build.Sources.Fingerprint; ExecutableSha256 = $build.ExecutableSha256
    TestsExecutableSha256 = $build.TestsExecutableSha256; Archive = $packaged.Archive
    ArchiveSha256 = (Get-FileHash -LiteralPath $packaged.Archive -Algorithm SHA256).Hash; UserDataPreserved = $true
    PendingManualApproval = @($policy.manualApprovalRequired)
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $folder 'candidate.json') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $folder 'candidate.json')
