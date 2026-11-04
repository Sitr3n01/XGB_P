# Microbenchmark isolado com fixtures. Nao inicia residente/FSE, nao opera jogos e nao mede FPS real.
# -Executavel aponta para XGB_P.Tests.exe: o benchmark usa os fixtures que so existem na build de verificacao.
param([Parameter(Mandatory=$true)][string]$Executavel, [Parameter(Mandatory=$true)][string]$PastaSaida)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = [IO.Path]::GetFullPath((Join-Path $repo $Executavel))
$outputFolder = [IO.Path]::GetFullPath((Join-Path $repo $PastaSaida))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'bin')).TrimEnd('\') + '\'
if (-not $exe.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or
    -not ($outputFolder.TrimEnd('\')+'\').StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Executavel e saida devem ficar em bin.' }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Executavel nao encontrado.' }
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$source = Join-Path $repo 'tools\performance\PreviewBench.cs'
$bench = Join-Path $outputFolder 'PreviewBench.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /optimize+ /warn:4 /warnaserror+ /codepage:65001 "/out:$bench" /r:System.dll /r:System.Core.dll /r:System.Xaml.dll "/r:$framework\WPF\WindowsBase.dll" "/r:$framework\WPF\PresentationCore.dll" "/r:$framework\WPF\PresentationFramework.dll" $source
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar microbenchmark.' }
$started = [DateTime]::UtcNow
$process = Start-Process -FilePath $bench -ArgumentList @(('"'+$exe+'"'),('"'+$outputFolder+'"')) -WindowStyle Hidden -PassThru
$processHandle = $process.Handle
if ($processHandle -eq [IntPtr]::Zero) { throw 'Handle do microbenchmark indisponivel.' }
if (-not $process.WaitForExit(60000)) { throw 'Microbenchmark excedeu 60 s; processo de teste preservado para diagnostico.' }
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Microbenchmark falhou ($($process.ExitCode))." }
$samples = @(Import-Csv -LiteralPath (Join-Path $outputFolder 'samples.csv'))
$culture = [Globalization.CultureInfo]::InvariantCulture
$metrics = @($samples | Group-Object scenario | ForEach-Object {
    $group = $_; $metricsForGroup = [ordered]@{ Scenario=$group.Name; Samples=$group.Count }
    foreach($column in @('elapsed_ms','allocated_bytes','layout_passes','cpu_ms','measure_passes','arrange_passes','set_panel_ms')) {
        [double[]]$values = @($group.Group | ForEach-Object {[double]::Parse($_.$column,$culture)})
        [Array]::Sort($values)
        $metricsForGroup[$column] = [ordered]@{Mean=($values|Measure-Object -Average).Average;Median=$values[[int][Math]::Floor($values.Count/2)];P95=$values[[Math]::Max(0,[int][Math]::Ceiling(.95*$values.Count)-1)];Min=$values[0];Max=$values[$values.Count-1]}
    }
    [pscustomobject]$metricsForGroup
})
[ordered]@{StartedUtc=$started.ToString('o');CompletedUtc=[DateTime]::UtcNow.ToString('o');Executable=$exe;ExecutableSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;BenchmarkSourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;Scope='WPF fixtures offscreen; no compositor/game FPS or physical input measurement';Metrics=$metrics} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputFolder 'summary.json') -Encoding UTF8
$metrics | ConvertTo-Json -Depth 4
