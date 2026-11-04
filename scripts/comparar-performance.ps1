# Comparacao alternada e repetida, somente fixtures WPF. Nao abre jogos nem muda o FSE.
param([Parameter(Mandatory=$true)][string]$Base, [Parameter(Mandatory=$true)][string]$Candidata,
    [Parameter(Mandatory=$true)][string]$PastaSaida, [ValidateRange(2,6)][int]$Repeticoes=4)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outputFolder = [IO.Path]::GetFullPath((Join-Path $repo $PastaSaida))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'bin')).TrimEnd('\') + '\'
if (-not ($outputFolder.TrimEnd('\')+'\').StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Saida fora de bin.' }
if (Test-Path -LiteralPath $outputFolder) { throw 'Escolha uma pasta nova para nao reutilizar resultados.' }
New-Item -ItemType Directory -Path $outputFolder | Out-Null
$executables = @{base=$Base; candidata=$Candidata}
$hashes = @{}
foreach($variant in $executables.Keys) { $hashes[$variant]=(Get-FileHash -LiteralPath (Join-Path $repo $executables[$variant]) -Algorithm SHA256).Hash }
$benchSource = Join-Path $repo 'tools\performance\PreviewBench.cs'
$benchHash = (Get-FileHash -LiteralPath $benchSource -Algorithm SHA256).Hash
$started = [DateTime]::UtcNow
$runs = @()
$allSamples = @()
for($repeat=0;$repeat -lt $Repeticoes;$repeat++) {
    $order = @('base','candidata')
    if($repeat % 2 -eq 1) { $order = @('candidata','base') }
    foreach($variant in $order) {
        $relative = Join-Path $PastaSaida ($variant+'-'+$repeat)
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\medir-performance.ps1') -Executavel $executables[$variant] -PastaSaida $relative | Out-Null
        if($LASTEXITCODE -ne 0) { throw "Microbenchmark falhou: $variant/$repeat." }
        $summary = Get-Content -LiteralPath (Join-Path $repo ($relative+'\summary.json')) -Raw | ConvertFrom-Json
        if($summary.ExecutableSha256 -ne $hashes[$variant] -or $summary.BenchmarkSourceSha256 -ne $benchHash) { throw 'Binario ou benchmark divergiu.' }
        $runs += [pscustomobject]@{Variant=$variant;Repeat=$repeat;Summary=$summary}
        $samples = @(Import-Csv -LiteralPath (Join-Path $repo ($relative+'\samples.csv')))
        foreach($sample in $samples) {
            $sample | Add-Member -NotePropertyName variant -NotePropertyValue $variant
            $sample | Add-Member -NotePropertyName run -NotePropertyValue $repeat
            $allSamples += $sample
        }
        Write-Output ("Medicao concluida: "+$variant+"/"+($repeat+1))
    }
}
$culture = [Globalization.CultureInfo]::InvariantCulture
$aggregate = @($allSamples | Group-Object variant,scenario | ForEach-Object {
    $group = $_; $row = [ordered]@{Variant=$group.Group[0].variant;Scenario=$group.Group[0].scenario;Samples=$group.Count}
    foreach($column in @('elapsed_ms','allocated_bytes','layout_passes','cpu_ms','measure_passes','arrange_passes','set_panel_ms')) {
        [double[]]$values = @($group.Group | ForEach-Object {[double]::Parse($_.$column,$culture)})
        [Array]::Sort($values)
        $row[$column]=[ordered]@{Mean=($values|Measure-Object -Average).Average;Median=$values[[int][Math]::Floor($values.Count/2)];P95=$values[[Math]::Max(0,[int][Math]::Ceiling(.95*$values.Count)-1)];Min=$values[0];Max=$values[$values.Count-1]}
    }
    [pscustomobject]$row
})
foreach($variant in $executables.Keys) {
    if((Get-FileHash -LiteralPath (Join-Path $repo $executables[$variant]) -Algorithm SHA256).Hash -ne $hashes[$variant]) { throw 'Executavel mudou durante comparacao.' }
}
if((Get-FileHash -LiteralPath $benchSource -Algorithm SHA256).Hash -ne $benchHash) { throw 'Benchmark mudou durante comparacao.' }
$allSamples | Export-Csv -LiteralPath (Join-Path $outputFolder 'samples.csv') -NoTypeInformation -Encoding UTF8
[ordered]@{Version=1;StartedUtc=$started.ToString('o');CompletedUtc=[DateTime]::UtcNow.ToString('o');Repetitions=$Repeticoes;
    Order='AB/BA alternated';ExecutableHashes=$hashes;BenchmarkSourceSha256=$benchHash;Runs=$runs;Aggregate=$aggregate;
    Scope='WPF fixtures, paused media, offscreen; counts content Measure/Arrange using a neutral Decorator; no real FPS/physical input or maximum hardware stress';
    Environment=[ordered]@{OS=[Environment]::OSVersion.Version.ToString();Cpu=(Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name);LogicalProcessors=[Environment]::ProcessorCount}} |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputFolder 'comparison.json') -Encoding UTF8
$aggregate | Select-Object Variant,Scenario,Samples,@{n='MeanCpuMs';e={$_.cpu_ms.Mean}},@{n='MeanMeasure';e={$_.measure_passes.Mean}},@{n='MeanBytes';e={$_.allocated_bytes.Mean}},@{n='MedianMs';e={$_.elapsed_ms.Median}} | ConvertTo-Json
