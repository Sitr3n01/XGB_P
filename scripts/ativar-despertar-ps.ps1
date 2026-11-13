# Ativa o despertar apenas das instancias HID Bluetooth DualSense/Edge, com backup exato (ADR 0114).
# EXECUTADO PELO USUARIO como administrador. -WhatIf nao escreve nem cria backup.
# -Desfazer restaura somente o que esta registrado no backup desta versao.
[CmdletBinding(SupportsShouldProcess=$true)]
param([switch]$Desfazer,[string]$ArquivoBackup=(Join-Path $env:LOCALAPPDATA 'XGB_P\wake-policy\bootps-backup-v1.json'))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'despertar-ps-comum.ps1')
$admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole( `
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if(!$admin -and !$WhatIfPreference){throw 'Execute no PowerShell como administrador, ou use -WhatIf.'}
$backupPath=[IO.Path]::GetFullPath($ArquivoBackup)
$backup=Read-BootPsBackup $backupPath
$current=Get-BootPsConfig
$controllers=@(Get-BootPsControllers)
function Ensure-BootPsBackup {
    if($null -ne $script:backup -and $script:backup.Active){return}
    if($null -ne $script:backup){
        $archive=$backupPath+'.'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')+'.restored.json'
        Save-BootPsBackup $archive $script:backup
    }
    $script:backup=[pscustomobject]@{SchemaVersion=1;Computer=$env:COMPUTERNAME;OwnerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;
        CreatedUtc=[DateTime]::UtcNow.ToString('o');Active=$true;OriginalKey=$current;KeyChanged=$false;DeviceChanges=@();RestoredUtc=$null}
    Save-BootPsBackup $backupPath $script:backup
}
if($Desfazer){
    if($null -eq $backup -or !$backup.Active){throw `
        'Nenhum backup ativo desta versao. A configuracao historica foi preservada; nao e possivel inventar seu estado anterior.'}
    $missing=@($backup.DeviceChanges | Where-Object {$id=$_.InstanceName;@($controllers|Where-Object {$_.InstanceName -eq $id}).Count -ne 1})
    if($missing.Count){throw 'A restauracao precisa das instancias originais presentes. Conecte os controles antes de repetir -Desfazer. O backup foi mantido.'}
    $performed=$false
    $remaining=0
    foreach($d in @($backup.DeviceChanges)){
        if($PSCmdlet.ShouldProcess($d.InstanceName,"Restaurar Enable=$($d.PriorEnabled)")){Set-BootPsController $d.InstanceName `
            $d.PriorEnabled;$performed=$true}
        else{$remaining++}
    }
    if($backup.KeyChanged){
        if($PSCmdlet.ShouldProcess('Politica Bluetooth original','Restaurar existencia, tipo e dados do backup')){Set-BootPsConfig `
            $backup.OriginalKey;$performed=$true}
        else{$remaining++}
    }
    if($performed -and !$remaining -and !$WhatIfPreference){$backup.Active=$false;$backup.RestoredUtc=[DateTime]::UtcNow.ToString('o'); `
        Save-BootPsBackup $backupPath $backup;Write-Host 'Estado anterior restaurado. Reinicie se a tabela Bluetooth mudou.'}
    elseif($remaining){Write-Host 'Restauracao nao concluida; o backup ativo foi mantido.'}
    return
}
$changed=$false
if(!(Test-BootPsConfig $current)){
    if($PSCmdlet.ShouldProcess('Politica Bluetooth','Gravar os 24 bytes esperados, preservando backup')){
        Ensure-BootPsBackup;$backup.KeyChanged=$true;Save-BootPsBackup $backupPath $backup
        Set-BootPsConfig ([pscustomobject]@{Exists=$true;Kind='Binary';Data=[Convert]::ToBase64String((Get-BootPsExpectedBytes))})
        $changed=$true
    }
    if($changed){Write-Host 'Tabela gravada. Reinicie, conecte o DualSense e rode novamente para conferir o armamento.';return}
}
if(!$controllers.Count){Write-Host `
    ('Nenhuma instancia Bluetooth DualSense/Edge armavel esta presente. Conecte o controle com ' + `
    'o PC ligado e rode novamente. A ausencia atual nao prova que ele nunca conectou.');return}
foreach($c in $controllers){
    if($c.Enabled){Write-Host "Ja habilitado: $($c.DeviceId)";continue}
    if($PSCmdlet.ShouldProcess($c.DeviceId,'Habilitar despertar somente desta instancia Sony/Bluetooth')){
        Ensure-BootPsBackup
        if(!@($backup.DeviceChanges|Where-Object {$_.InstanceName -eq $c.InstanceName}).Count){
            $backup.DeviceChanges=@($backup.DeviceChanges)+@([pscustomobject]@{InstanceName=$c.InstanceName;PriorEnabled=[bool]$c.Enabled})
            Save-BootPsBackup $backupPath $backup
        }
        Set-BootPsController $c.InstanceName $true
        Write-Host "Despertar habilitado: $($c.DeviceId)"
    }
}
Write-Host 'Confira com scripts\verificar-despertar-ps.ps1. A configuracao permite wake pelo dispositivo; a confirmacao de PS fica no registro de retomada.'
