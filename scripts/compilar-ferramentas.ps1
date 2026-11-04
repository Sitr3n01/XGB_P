# Compila as ferramentas de diagnostico somente leitura: bin\DualSenseReader.exe e bin\ModoXboxMonitor.exe.
# Nao instala nem registra nada.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repo 'scripts\build-comum.ps1')
$csc = (Get-XgbCompiler).Csc
$bin = Resolve-XgbOutput -Repository $repo -Relative 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

# Leitor do DualSense pelo HID (somente leitura).
& $csc /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 /warnaserror+ `
    "/out:$bin\DualSenseReader.exe" "$repo\src\lib\dualsense\DualSenseHid.cs" "$repo\tools\dualsense-reader\DualSenseReader.cs"
if ($LASTEXITCODE -ne 0) { throw "csc (DualSenseReader) falhou com codigo $LASTEXITCODE" }

# Monitor do modo Xbox pela API oficial.
& $csc /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 /warnaserror+ `
    "/out:$bin\ModoXboxMonitor.exe" "$repo\src\lib\gaming\GamingExperience.cs" "$repo\tools\modo-xbox-monitor\ModoXboxMonitor.cs"
if ($LASTEXITCODE -ne 0) { throw "csc (ModoXboxMonitor) falhou com codigo $LASTEXITCODE" }

Write-Host "OK: $bin\DualSenseReader.exe e $bin\ModoXboxMonitor.exe"
