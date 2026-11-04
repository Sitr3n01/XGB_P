# Testa recusas do contrato sem abrir apps, trocar configuracoes ou reutilizar relatorios.
param([string]$PastaSaida = 'bin\infraestrutura')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\verificacao-comum.ps1')
$checks = New-Object 'System.Collections.Generic.List[string]'
$now = [DateTime]::UtcNow
function New-FixtureReceipt {
    [pscustomobject]@{ Version = 1; Success = $true; RunId = 'fixture'; Mode = '--verify'; ExecutableSha256 = 'ABC'
        StartedUtc = $now.ToString('o'); CompletedUtc = $now.ToString('o') }
}
$receipt = New-FixtureReceipt
Assert-XgbVerificationReceipt -Receipt $receipt -RunId 'fixture' -Flag '--verify' -ExpectedHash 'ABC' -Started $now
$checks.Add('OK: recibo desta execucao aceito')
foreach ($field in @('Version', 'Success', 'RunId', 'Mode', 'ExecutableSha256', 'StartedUtc', 'CompletedUtc')) {
    $receipt = New-FixtureReceipt
    switch ($field) {
        'Version' { $receipt.Version = 2 }
        'Success' { $receipt.Success = $false }
        'RunId' { $receipt.RunId = 'outra-execucao' }
        'Mode' { $receipt.Mode = '--verify-audio-live' }
        'ExecutableSha256' { $receipt.ExecutableSha256 = 'OUTRO-BUILD' }
        'StartedUtc' { $receipt.StartedUtc = $now.AddDays(-1).ToString('o') }
        'CompletedUtc' { $receipt.CompletedUtc = $now.AddSeconds(-1).ToString('o') }
    }
    $rejected = $false
    try { Assert-XgbVerificationReceipt -Receipt $receipt -RunId 'fixture' -Flag '--verify' -ExpectedHash 'ABC' -Started $now }
    catch { $rejected = $true }
    if (-not $rejected) { throw "Contrato aceitou $field adulterado." }
    $checks.Add("OK: $field adulterado recusado")
}
$output = [IO.Path]::GetFullPath((Join-Path $repo $PastaSaida))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'bin')).TrimEnd('\') + '\'
if (-not ($output.TrimEnd('\') + '\').StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Saida fora de bin.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$checks | Set-Content -LiteralPath (Join-Path $output 'infraestrutura-verificacao.txt') -Encoding ASCII
$checks
