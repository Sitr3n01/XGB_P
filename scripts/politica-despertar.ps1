# Aplica a lista de despertar escolhida pelo usuario (ADR 0102).
# Exige -Permitidos explicito: nao distribui uma lista de dispositivos desta maquina.
# Os dispositivos armados fora dessa lista terao a permissao de despertar desligada.
# EXECUTADO PELO USUARIO, como administrador (regra 4). Use primeiro com -WhatIf para ver o que muda.
# Para devolver o despertar a um dispositivo: Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceWakeEnable |
#   Where-Object InstanceName -like '<id>*' | Set-CimInstance -Property @{Enable=$true}
[CmdletBinding(SupportsShouldProcess = $true)]
param([Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string[]]$Permitidos)
$ErrorActionPreference = 'Stop'
Import-Module CimCmdlets -WhatIf:$false # sem o ruido de aliases no -WhatIf
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole( `
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin -and -not $WhatIfPreference) { throw 'Execute este script no PowerShell como administrador (ou use -WhatIf para so conferir).' }
$mudados = 0
foreach ($w in @(Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceWakeEnable | Where-Object { $_.Enable })) {
    $id = $w.InstanceName -replace '_\d+$', ''
    $nome = (Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue).FriendlyName
    if (@($Permitidos | Where-Object { $id -like $_ }).Count -gt 0) { Write-Host "mantido: $nome ($id)"; continue }
    if ($PSCmdlet.ShouldProcess("$nome ($id)", 'Desligar o despertar')) {
        Set-CimInstance -InputObject $w -Property @{ Enable = $false }
        $mudados++
        Write-Host "desligado: $nome ($id)"
    }
}
Write-Host "Dispositivos alterados: $mudados. Confira com: powercfg /devicequery wake_armed"
