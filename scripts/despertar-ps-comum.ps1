# Funcoes do BootPS. Dot-source nao le nem altera registro/dispositivos.
Set-StrictMode -Version 2
$script:BootPsKey='SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters'
$script:BootPsValue='WaitWake Capable COD Masks'
$script:BootPsControllerPattern='^HID\\\{00001124-0000-1000-8000-00805F9B34FB\}_VID&0002054C_PID&(0CE6|0DF2)\\[^\\]+$'
function Get-BootPsExpectedBytes {
    return ,([byte[]](0,0,31,252,0,0,5,64,0,0,31,252,0,0,5,128,0,0,31,252,0,0,5,8))
}
function Test-BootPsBytes([byte[]]$Bytes) {
    $expected=Get-BootPsExpectedBytes
    if($null -eq $Bytes -or $Bytes.Length -ne $expected.Length){return $false}
    for($i=0;$i -lt $expected.Length;$i++){if($Bytes[$i] -ne $expected[$i]){return $false}}
    return $true
}
function Test-BootPsController([string]$InstanceName) {
    return (($InstanceName -replace '_\d+$','') -match $script:BootPsControllerPattern)
}
function Get-BootPsConfig {
    $key=[Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($script:BootPsKey)
    try {
        if($null -eq $key -or $key.GetValueNames() -notcontains $script:BootPsValue){return [pscustomobject]@{Exists=$false;Kind='None';Data=$null}}
        $kind=$key.GetValueKind($script:BootPsValue).ToString()
        $data=$key.GetValue($script:BootPsValue,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        if($kind -eq 'Binary' -or $kind -eq 'None'){$data=[Convert]::ToBase64String([byte[]]$data)}
        return [pscustomobject]@{Exists=$true;Kind=$kind;Data=$data}
    } finally {if($null -ne $key){$key.Dispose()}}
}
function Test-BootPsConfig($State) {
    return ($State.Exists -and $State.Kind -eq 'Binary' -and (Test-BootPsBytes ([Convert]::FromBase64String($State.Data))))
}
function Set-BootPsConfig($State) {
    $key=[Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($script:BootPsKey,$true)
    if($null -eq $key){throw 'A chave de parametros do Bluetooth nao existe.'}
    try {
        if(!$State.Exists){$key.DeleteValue($script:BootPsValue,$false);return}
        $kind=[Microsoft.Win32.RegistryValueKind][Enum]::Parse([Microsoft.Win32.RegistryValueKind],$State.Kind)
        $data=$State.Data
        if($kind -eq [Microsoft.Win32.RegistryValueKind]::Binary -or $kind -eq `
            [Microsoft.Win32.RegistryValueKind]::None){$data=[Convert]::FromBase64String($State.Data)}
        elseif($kind -eq [Microsoft.Win32.RegistryValueKind]::DWord){$data=[int]$State.Data}
        elseif($kind -eq [Microsoft.Win32.RegistryValueKind]::QWord){$data=[long]$State.Data}
        elseif($kind -eq [Microsoft.Win32.RegistryValueKind]::MultiString){$data=[string[]]$State.Data}
        $key.SetValue($script:BootPsValue,$data,$kind)
    } finally {$key.Dispose()}
}
function Get-BootPsControllers {
    foreach($w in @(Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceWakeEnable -ErrorAction Stop)){
        if(Test-BootPsController $w.InstanceName){[pscustomobject]@{InstanceName=$w.InstanceName;DeviceId=($w.InstanceName -replace `
            '_\d+$','');Enabled=[bool]$w.Enable}}
    }
}
function Set-BootPsController([string]$InstanceName,[bool]$Enabled) {
    if(!(Test-BootPsController $InstanceName)){throw 'Alvo recusado: nao e um HID Bluetooth DualSense/Edge.'}
    $rows=@(Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceWakeEnable -ErrorAction Stop | Where-Object {$_.InstanceName -eq $InstanceName})
    if($rows.Count -ne 1){throw "Instancia ausente ou ambigua; conecte o controle: $InstanceName"}
    Set-CimInstance -InputObject $rows[0] -Property @{Enable=$Enabled} -ErrorAction Stop | Out-Null
    $check=@(Get-BootPsControllers | Where-Object {$_.InstanceName -eq $InstanceName})
    if($check.Count -ne 1 -or $check[0].Enabled -ne $Enabled){throw "O Windows nao confirmou a permissao de despertar: $InstanceName"}
}
function Save-BootPsBackup([string]$Path,$Value) {
    $full=[IO.Path]::GetFullPath($Path);[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))
    $temp=$full+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    try {
        [IO.File]::WriteAllText($temp,(ConvertTo-Json -InputObject $Value -Depth 10),[Text.UTF8Encoding]::new($false))
        if([IO.File]::Exists($full)){[IO.File]::Replace($temp,$full,[NullString]::Value)}else{[IO.File]::Move($temp,$full)}
    } finally {if([IO.File]::Exists($temp)){[IO.File]::Delete($temp)}}
}
function Read-BootPsBackup([string]$Path) {
    if(!(Test-Path -LiteralPath $Path)){return $null}
    $b=Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json
    if($b.SchemaVersion -ne 1 -or $b.Computer -ne $env:COMPUTERNAME -or $b.OwnerSid -ne `
        [Security.Principal.WindowsIdentity]::GetCurrent().User.Value){throw 'Backup de outra maquina/usuario ou formato invalido.'}
    foreach($d in @($b.DeviceChanges)){if(!(Test-BootPsController $d.InstanceName) -or $d.PriorEnabled -isnot [bool]){throw `
        'Backup contem alvo ou permissao invalidos.'}}
    return $b
}
