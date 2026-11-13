# Diagnostico do despertar pelo PS do DualSense (ADR 0102, metodo BootPS). Somente leitura; nao precisa de administrador.
# Confere os elos da cadeia: chave de COD do BthEnum, RemoteWakeEnabled do controle, no HID presente nesta sessao,
# armamento atual e os outros dispositivos com despertar ligado (politica de 22/10).
# Saida: 0 presente e habilitado; 2 configuracao persistida, armamento atual indeterminado; 1 falha.
param([string[]]$Permitidos = @('HID\VID_046D&PID_C33C&MI_00\*', 'HID\VID_046D&PID_C33C&MI_01&Col01\*',
    'HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6*', 'HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0DF2*'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'despertar-ps-comum.ps1')
$cod = [byte[]](0x00,0x00,0x1F,0xFC,0x00,0x00,0x05,0x40, 0x00,0x00,0x1F,0xFC,0x00,0x00,0x05,0x80, 0x00,0x00,0x1F,0xFC,0x00,0x00,0x05,0x08)
$controle = '00001124.*054C.*(0CE6|0DF2)' # DualSense e DualSense Edge por Bluetooth
$falhas = 0
function Ok([string]$m) { Write-Host "  [ OK ] $m" -ForegroundColor Green }
function Falha([string]$m) { Write-Host "  [FALHA] $m" -ForegroundColor Red; $script:falhas++ }
function Aviso([string]$m) { Write-Host "  [ .. ] $m" -ForegroundColor Yellow }

Write-Host "`n=== 1. Chave de Class of Device (BthEnum) ===" -ForegroundColor Cyan
$v = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters' -Name 'WaitWake Capable COD Masks' -ErrorAction `
    SilentlyContinue).'WaitWake Capable COD Masks'
if ($null -eq $v) { Falha 'Chave ausente: o gamepad e barrado pela lista compilada do BthEnum. Rode scripts\ativar-despertar-ps.ps1 como administrador.' }
elseif (!(Test-BootPsBytes $v)) { Falha ('Chave divergente: ' + (($v | ForEach-Object { '{0:X2}' -f $_ }) -join ' ')) }
else { Ok '24 bytes corretos (teclado 0x0540, apontador 0x0580, gamepad 0x0508)' }

Write-Host "`n=== 2. Armamento persistido (RemoteWakeEnabled) ===" -ForegroundColor Cyan
$hid = @(Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\HID' -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match $controle })
$instancias = @()
foreach ($device in $hid) { $instancias += @(Get-ChildItem $device.PSPath -ErrorAction SilentlyContinue | ForEach-Object { 'HID\' + `
    $device.PSChildName + '\' + $_.PSChildName }) }
if ($instancias.Count -eq 0) { Falha 'O controle nunca foi pareado por Bluetooth nesta instalacao (nenhum no HID).' }
else {
    $armado = $false
    foreach ($id in $instancias) {
        $parameters = Get-ItemProperty ("HKLM:\SYSTEM\CurrentControlSet\Enum\" + $id + '\Device Parameters') -ErrorAction Stop
        $property = $parameters.PSObject.Properties['RemoteWakeEnabled']; $rw = if($null -ne $property){$property.Value}else{-1}
        if ($rw -eq 1) { $armado = $true; Ok "RemoteWakeEnabled = 1 ($id)" }
    }
    if (-not $armado) { Falha 'RemoteWakeEnabled ausente ou 0. Com o controle conectado, rode scripts\ativar-despertar-ps.ps1 como administrador.' }
}

Write-Host "`n=== 3. Presenca atual (nao e historico de conexao) ===" -ForegroundColor Cyan
$presente = $false
foreach ($id in $instancias) { $d = Get-PnpDevice -InstanceId $id -PresentOnly -ErrorAction SilentlyContinue; if ($d -and $d.Status -eq `
    'OK') { $presente = $true } }
if ($presente) { Ok 'No HID presente: o Windows pode armar o despertar' }
else { Aviso 'No HID ausente agora. Isso nao prova que nunca conectou nem exclui um pedido de despertar retido pelo Bluetooth.' }

Write-Host "`n=== 4. Armado agora ===" -ForegroundColor Cyan
$jogo=@()
$armamentoIndisponivel=$false
try { $jogo=@(Get-BootPsControllers | Where-Object {$_.Enabled -and (Get-PnpDevice -InstanceId $_.DeviceId -PresentOnly -ErrorAction SilentlyContinue)}) }
catch { $armamentoIndisponivel=$true; Aviso ('Nao foi possivel consultar o armamento por instancia: '+$_.Exception.Message) }
if ($jogo.Count -gt 0) { Ok ('Presente e habilitado: ' + (($jogo|ForEach-Object {$_.DeviceId}) -join ' | ')) }
elseif ($presente -and !$armamentoIndisponivel) { Falha 'Controle presente mas nao armado: confira o passo 2.' }
else { Aviso 'Armamento atual indeterminado; ausencia na lista de presentes nao exclui Bluetooth.' }

Write-Host "`n=== 5. Outros que acordam o PC (politica de 22/10) ===" -ForegroundColor Cyan
$fora = @()
foreach ($w in @(Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceWakeEnable -ErrorAction SilentlyContinue | Where-Object { $_.Enable })) {
    $id = $w.InstanceName -replace '_\d+$', ''
    $nome = (Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue).FriendlyName
    $ok = @($Permitidos | Where-Object { $id -like $_ }).Count -gt 0
    if ($ok) { Ok "$nome ($id)" } else { $fora += $id; Aviso "fora da politica: $nome ($id)" }
}
if ($fora.Count -gt 0) { Aviso 'Para aplicar a politica: scripts\politica-despertar.ps1 -WhatIf e depois sem -WhatIf, como administrador.' }

Write-Host "`n=== VEREDITO ===" -ForegroundColor Cyan
if ($falhas -gt 0) { Write-Host "  Cadeia quebrada em $falhas ponto(s)." -ForegroundColor Red; exit 1 }
if ($armamentoIndisponivel) { Write-Host '  Leitura incompleta; armamento atual indeterminado.' -ForegroundColor Yellow; exit 2 }
if ($jogo.Count -gt 0) { Write-Host '  Pronto: suspenda e aperte o PS.' -ForegroundColor Green; exit 0 }
Write-Host `
    ('  Configuracao persistida; controle ausente agora e armamento atual indeterminado. ' + `
    'Conecte o controle com o PC ligado para conferir.') -ForegroundColor Yellow
exit 2
