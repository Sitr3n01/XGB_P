# Empacota o produto de uma pasta de build: ZIP enxuto (executavel, leia-me, licenca e avisos) + SHA256SUMS.
# Usado pela release no GitHub Actions e por preparar-candidato.ps1. Nunca inclui a build de verificacao.
param([string]$PastaBuild = 'bin\app', [string]$PastaSaida = 'bin\pacote')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\build-comum.ps1')
$policy = Get-Content -LiteralPath (Join-Path $repo 'quality\policy.json') -Raw | ConvertFrom-Json
$build = Resolve-XgbOutput -Repository $repo -Relative $PastaBuild
$output = Resolve-XgbOutput -Repository $repo -Relative $PastaSaida
$exe = Join-Path $build 'XGB_P.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build nao encontrada: $exe" }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
if ($version -ne $policy.releaseVersion) { throw "Versao do binario ($version) difere da politica ($($policy.releaseVersion))." }
$metadata = Get-Content -LiteralPath (Join-Path $build 'build.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $metadata.ExecutableSha256) { throw 'XGB_P.exe difere do build.json.' }

$name = 'XGB_P-' + $version + '-win-x64'
$staging = Join-Path $output $name
if (Test-Path -LiteralPath $staging) { throw "Pasta de pacote ja existe: $staging" }
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item -LiteralPath $exe -Destination $staging
Copy-Item -LiteralPath (Join-Path $repo 'docs\guias\instalacao-e-uso.md') -Destination (Join-Path $staging 'LEIA-ME.md')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $staging 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repo 'NOTICE') -Destination (Join-Path $staging 'NOTICE.txt')
$expected = @('XGB_P.exe', 'LEIA-ME.md', 'LICENSE.txt', 'NOTICE.txt')

$archive = Join-Path $output ($name + '.zip')
Compress-Archive -LiteralPath @($expected | ForEach-Object { Join-Path $staging $_ }) -DestinationPath $archive -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    if ($zip.Entries.Count -ne $expected.Count) { throw 'Quantidade inesperada de entradas no ZIP.' }
    foreach ($entry in $zip.Entries) {
        if ($expected -notcontains $entry.FullName) { throw "Arquivo inesperado no ZIP: $($entry.FullName)" }
        $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $staging $entry.FullName) -Algorithm SHA256).Hash) {
            throw "Hash incorreto dentro do ZIP: $($entry.FullName)"
        }
    }
}
finally { $zip.Dispose() }

# Formato do sha256sum (hash em minusculas, dois espacos, nome), conferivel com Get-FileHash ou sha256sum -c.
$sums = @(
    ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $archive)),
    ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() + '  XGB_P.exe')
)
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), [string[]]$sums)
[ordered]@{ Version = $version; Archive = $archive; Sums = (Join-Path $output 'SHA256SUMS.txt')
    ExecutableSha256 = $metadata.ExecutableSha256; SourceFingerprint = $metadata.Sources.Fingerprint } | ConvertTo-Json
