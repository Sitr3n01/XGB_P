# Verificacoes estaticas compartilhadas pelo gate local e pela CI. Nao compila nem executa o aplicativo.
function Invoke-XgbStaticChecks {
    param([Parameter(Mandatory = $true)][string]$Repository, [Parameter(Mandatory = $true)]$Policy,
        [Parameter(Mandatory = $true)]$Snapshot)
    $checks = New-Object 'System.Collections.Generic.List[string]'
    if ($Policy.version -ne 2) { throw 'Versao de politica desconhecida.' }

    # Tamanho de arquivo e largura de linha: tetos legados so podem cair (catraca).
    foreach ($file in $Snapshot.Files) {
        $cap = $Policy.maxNewFileLines
        $legacy = $Policy.legacyLineCaps.PSObject.Properties[$file.Path]
        if ($legacy) { $cap = $legacy.Value }
        if ($file.Lines -gt $cap) { throw "$($file.Path): $($file.Lines) linhas excedem o teto $cap." }
        $lines = [IO.File]::ReadAllLines((Join-Path $Repository $file.Path))
        $long = @($lines | Where-Object { $_.Length -gt $Policy.maxLineLength }).Count
        $allowedLong = 0
        $legacyLong = $Policy.legacyLongLines.PSObject.Properties[$file.Path]
        if ($legacyLong) { $allowedLong = $legacyLong.Value }
        if ($long -gt $allowedLong) {
            throw "$($file.Path): $long linhas com mais de $($Policy.maxLineLength) caracteres (permitido: $allowedLong)."
        }
        if ($file.Path.EndsWith('.cs')) {
            $text = [IO.File]::ReadAllText((Join-Path $Repository $file.Path))
            if ($text -match '\b(WriteProcessMemory|CreateRemoteThread|VirtualAllocEx|HidD_SetOutputReport|HidD_SetFeature)\s*\(') {
                throw "API proibida em $($file.Path)."
            }
        }
    }
    $checks.Add('Tetos de tamanho e largura de linha sem ampliar a divida legada; nenhuma API de injecao/saida HID proibida.')

    $scripts = @(Get-ChildItem -LiteralPath (Join-Path $Repository 'scripts') -Filter '*.ps1' -File)
    $scripts += @(Get-ChildItem -LiteralPath (Join-Path $Repository 'tools') -Filter '*.ps1' -File -Recurse)
    foreach ($script in $scripts) {
        if (@([IO.File]::ReadAllBytes($script.FullName) | Where-Object { $_ -gt 127 }).Count -gt 0) { throw "$($script.Name) nao esta em ASCII." }
        $tokens = $null; $parseErrors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
        if ($parseErrors.Count -gt 0) { throw "$($script.Name): $($parseErrors[0].Message)" }
    }
    $checks.Add('Scripts ASCII e sintaxe aprovada pelo Windows PowerShell 5.1.')

    # Windows PowerShell transforma avisos de stderr em NativeCommandError; o codigo de saida e a autoridade.
    $ErrorActionPreference = 'Continue'
    $tracked = @(& git -C $Repository ls-files --cached --others --exclude-standard 2>$null)
    $gitCode = $LASTEXITCODE
    $diff = & git -C $Repository diff --check 2>&1
    $diffCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($gitCode -ne 0) { throw 'Repositorio git indisponivel para as verificacoes de conteudo.' }
    if ($diffCode -ne 0) { throw "git diff --check falhou: $diff" }
    $binaries = @($tracked | Where-Object { $_ -match '\.(exe|dll|pdb|zip|msix|appx|appxbundle|pfx|cer|pvk|snk|wav|mp3|ogg)$' })
    if ($binaries.Count -gt 0) { throw ('Binario ou certificado versionado: ' + ($binaries -join ', ')) }
    # A ponte com identidade de pacote da Microsoft e estritamente local e nunca entra neste repositorio.
    $publisher = 'CN=Microsoft ' + 'Corporation'
    foreach ($path in $tracked) {
        if ($path -notmatch '\.(cs|ps1|xml|manifest|xaml|svg|json|md|yml|yaml|toml|txt)$') { continue }
        $content = [IO.File]::ReadAllText((Join-Path $Repository $path))
        if ($content.Contains($publisher)) { throw "Identidade de publicador da Microsoft encontrada em $path." }
    }
    $checks.Add('git diff --check sem erros; nenhum binario, certificado, som ou identidade de pacote da Microsoft versionado.')
    $checks.ToArray()
}
