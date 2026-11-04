# Contrato compartilhado de verificacao: conclusao nova, modo e SHA-256 do executavel.
function Assert-XgbVerificationReceipt {
    param($Receipt, [string]$RunId, [string]$Flag, [string]$ExpectedHash, [DateTime]$Started)
    if ($Receipt.Version -ne 1 -or $Receipt.Success -ne $true -or $Receipt.RunId -ne $RunId -or
        $Receipt.Mode -ne $Flag -or $Receipt.ExecutableSha256 -ne $ExpectedHash) {
        throw "$Flag produziu recibo invalido ou de outro build."
    }
    $begin = [DateTime]::Parse($Receipt.StartedUtc).ToUniversalTime()
    $end = [DateTime]::Parse($Receipt.CompletedUtc).ToUniversalTime()
    if ($begin -lt $Started.AddSeconds(-2) -or $end -lt $begin -or $end -gt [DateTime]::UtcNow.AddSeconds(2)) {
        throw "$Flag produziu datas de execucao invalidas."
    }
}
function Invoke-XgbVerification {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$Flag,
        [ValidateRange(5, 600)][int]$TimeoutSeconds = 180
    )
    $runId = [Guid]::NewGuid().ToString('N')
    $expectedHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
    $receiptPath = Join-Path (Split-Path -Parent $Executable) ('design-preview\execucao-' + $runId + '.json')
    if (Test-Path -LiteralPath $receiptPath) { throw 'ID de verificacao ja existente.' }
    $started = [DateTime]::UtcNow
    $child = Start-Process -FilePath $Executable -ArgumentList @($Flag, '--run-id', $runId) -PassThru -WindowStyle Hidden
    try {
        if (-not $child.WaitForExit($TimeoutSeconds * 1000)) {
            # Somente o filho criado por esta chamada, nunca uma previa do usuario.
            $child.Kill()
            $child.WaitForExit()
            throw "$Flag excedeu o limite de $TimeoutSeconds segundos."
        }
        $child.Refresh()
        if ($child.ExitCode -ne 0) { throw "$Flag terminou com codigo $($child.ExitCode)." }
        if (-not (Test-Path -LiteralPath $receiptPath)) { throw "$Flag nao produziu recibo desta execucao." }
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        Assert-XgbVerificationReceipt -Receipt $receipt -RunId $runId -Flag $Flag -ExpectedHash $expectedHash -Started $started
        $receipt
    }
    finally { $child.Dispose() }
}
