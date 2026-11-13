# Funcoes compartilhadas de build: inventario canonico dos fontes e compilacao com o csc do .NET Framework.
# O produto (XGB_P.exe) e a build de verificacao (XGB_P.Tests.exe) saem dos MESMOS fontes de src;
# a segunda soma tests/ e o simbolo XGB_VERIFY.

function Get-XgbSourceSnapshot {
    param([Parameter(Mandatory = $true)][string]$Repository)
    $items = @(Get-ChildItem -LiteralPath (Join-Path $Repository 'src') -Recurse -File |
        Where-Object { $_.Extension -eq '.cs' -or $_.Extension -eq '.xaml' -or $_.Extension -eq '.svg' -or $_.Extension -eq '.manifest' })
    $items += @(Get-ChildItem -LiteralPath (Join-Path $Repository 'tests') -Filter '*.cs' -File)
    $items += @(Get-ChildItem -LiteralPath (Join-Path $Repository 'scripts') -Filter '*.ps1' -File)
    $items += @(Get-ChildItem -LiteralPath (Join-Path $Repository 'quality') -Filter '*.json' -File)
    # Ordem ordinal explicita: Sort-Object cultural difere entre .NET Framework e PowerShell 7.
    [string[]]$orderedPaths = @($items | ForEach-Object { $_.FullName })
    [Array]::Sort($orderedPaths, [StringComparer]::Ordinal)
    $rows = @($orderedPaths | ForEach-Object {
        $sourcePath = $_
        [pscustomobject]@{
            Path = $sourcePath.Substring($Repository.Length + 1).Replace('\', '/')
            Sha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
            Lines = [IO.File]::ReadAllLines($sourcePath).Length
        }
    })
    $canonical = ($rows | ForEach-Object { $_.Path + ':' + $_.Sha256 }) -join "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-', '') }
    finally { $sha.Dispose() }
    [pscustomobject]@{ Version = 2; Fingerprint = $hash; Files = $rows }
}

function Get-XgbCompiler {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $csc = Join-Path $framework 'csc.exe'
    $winmd = Join-Path $env:WINDIR 'System32\WinMetadata'
    if (-not (Test-Path -LiteralPath $csc)) { throw "Compilador nao encontrado: $csc" }
    if (-not (Test-Path -LiteralPath (Join-Path $winmd 'Windows.Media.winmd'))) { throw "Metadados WinRT nao encontrados: $winmd" }
    [pscustomobject]@{ Csc = $csc; Framework = $framework; Wpf = (Join-Path $framework 'WPF'); WinMetadata = $winmd }
}

# Compila um executavel a partir do inventario. -Tests inclui tests/*.cs e define XGB_VERIFY.
function Invoke-XgbCompile {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)]$Snapshot,
        [Parameter(Mandatory = $true)][string]$Executable,
        [switch]$Tests
    )
    $compiler = Get-XgbCompiler
    $app = Join-Path $Repository 'src\app'
    $sources = @($Snapshot.Files | Where-Object {
        $_.Path.EndsWith('.cs') -and ($_.Path.StartsWith('src/') -or ($Tests -and $_.Path.StartsWith('tests/')))
    } | ForEach-Object { Join-Path $Repository $_.Path })
    $options = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', '/warn:4', '/warnaserror+',
        "/out:$Executable", "/resource:$app\DesignPreview.xaml,DesignPreview.xaml",
        "/resource:$app\DualSense.svg,DualSense.svg", "/win32manifest:$app\DesignPreview.manifest")
    if ($Tests) { $options += '/define:XGB_VERIFY' }
    foreach ($assembly in @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Windows.Forms.dll',
        'System.Drawing.dll', 'System.Management.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll')) { $options += "/r:$assembly" }
    foreach ($assembly in @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll', 'UIAutomationClient.dll',
        'UIAutomationTypes.dll', 'UIAutomationProvider.dll')) { $options += ('/r:' + (Join-Path $compiler.Wpf $assembly)) }
    foreach ($assembly in @('System.Runtime.dll', 'System.Runtime.WindowsRuntime.dll',
        'System.Runtime.InteropServices.WindowsRuntime.dll')) { $options += ('/r:' + (Join-Path $compiler.Framework $assembly)) }
    foreach ($metadata in @('Windows.Foundation', 'Windows.Media', 'Windows.Storage', 'Windows.ApplicationModel',
        'Windows.Networking', 'Windows.Devices', 'Windows.UI')) { $options += ('/r:' + (Join-Path $compiler.WinMetadata ($metadata + '.winmd'))) }
    & $compiler.Csc @options @sources
    if ($LASTEXITCODE -ne 0) { throw "csc falhou ($LASTEXITCODE): $Executable" }
    (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
}

# Saidas de build e verificacao ficam sempre dentro de bin deste repositorio.
function Resolve-XgbOutput {
    param([Parameter(Mandatory = $true)][string]$Repository, [Parameter(Mandatory = $true)][string]$Relative)
    $path = [IO.Path]::GetFullPath((Join-Path $Repository $Relative))
    $allowed = [IO.Path]::GetFullPath((Join-Path $Repository 'bin')).TrimEnd('\') + '\'
    if (-not ($path.TrimEnd('\') + '\').StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A saida deve ficar dentro de bin neste repositorio.'
    }
    $path
}
