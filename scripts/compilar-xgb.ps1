# Compila XGB_P.exe (produto) e XGB_P.Tests.exe (verificacao) sem instalar, iniciar ou mudar preferencias.
# -SemTestes compila so o produto. -Verificar roda a suite funcional/temporal (fixtures) na build de verificacao.
param([string]$PastaSaida = 'bin\app', [switch]$SemTestes, [switch]$Verificar)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\build-comum.ps1')
. (Join-Path $repo 'scripts\verificacao-comum.ps1')
if ($SemTestes -and $Verificar) { throw '-Verificar exige a build de verificacao; remova -SemTestes.' }
$bin = Resolve-XgbOutput -Repository $repo -Relative $PastaSaida
$product = Join-Path $bin 'XGB_P.exe'
$tests = Join-Path $bin 'XGB_P.Tests.exe'
foreach ($exe in @($product, $tests)) {
    $running = Get-Process ([IO.Path]::GetFileNameWithoutExtension($exe)) -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
    if ($running) { throw "Encerre $exe (pela bandeja) antes de recompilar." }
}
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$buildLock = [IO.File]::Open((Join-Path $bin '.build.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $snapshot = Get-XgbSourceSnapshot -Repository $repo
    $productHash = Invoke-XgbCompile -Repository $repo -Snapshot $snapshot -Executable $product
    $testsHash = $null
    if ($SemTestes) { Remove-Item -LiteralPath $tests -ErrorAction SilentlyContinue }
    else { $testsHash = Invoke-XgbCompile -Repository $repo -Snapshot $snapshot -Executable $tests -Tests }
    $after = Get-XgbSourceSnapshot -Repository $repo
    if ($after.Fingerprint -ne $snapshot.Fingerprint) { throw 'Os fontes mudaram durante a compilacao. Build nao validado.' }
    $compiler = Get-XgbCompiler
    [ordered]@{
        Version = 2; CompiledUtc = [DateTime]::UtcNow.ToString('o')
        ProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($product).ProductVersion
        ExecutableSha256 = $productHash; TestsExecutableSha256 = $testsHash
        CompilerSha256 = (Get-FileHash -LiteralPath $compiler.Csc -Algorithm SHA256).Hash; Sources = $snapshot
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $bin 'build.json') -Encoding UTF8
    Write-Host "Compilado: $product"
    if (-not $SemTestes) { Write-Host "Compilado: $tests" }
    if ($Verificar) {
        $receipt = Invoke-XgbVerification -Executable $tests -Flag '--verify-motion'
        Write-Host ('Execucao validada: ' + $receipt.RunId)
        Get-Content -LiteralPath (Join-Path $bin 'design-preview\verificacao.txt')
        Get-Content -LiteralPath (Join-Path $bin 'design-preview\animacoes-verificacao.txt')
    }
}
finally { $buildLock.Dispose() }
