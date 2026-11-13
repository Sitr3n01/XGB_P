# Regressao isolada do ativador: registro/WMI substituidos; arquivos somente na pasta temporaria do teste.
param([string]$PastaSaida)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(!$PastaSaida){$PastaSaida=Join-Path $repo 'bin\verificacao-bootps-scripts'}
$testRoot=Join-Path ([IO.Path]::GetFullPath($PastaSaida)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$common=Join-Path $PSScriptRoot 'despertar-ps-comum.ps1'
. $common
$checks=0
function Check([bool]$value,[string]$why){$script:checks++;if(!$value){throw "BootPS isolado: $why"}}
$sony='HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\fixture_0'
$edge='HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0DF2\fixture_0'
$other='HID\VID_045E&PID_0B13\fixture_0'
$usb='HID\VID_054C&PID_0CE6\fixture_0'
Check ((Test-BootPsController $sony) -and (Test-BootPsController $edge) -and !(Test-BootPsController $other) -and !( `
    Test-BootPsController $usb)) 'selecao Sony/Bluetooth sem outros controles ou Sony USB'
$exact=Get-BootPsExpectedBytes;$reversed=[byte[]]($exact[23..0])
Check ((Test-BootPsBytes $exact) -and !(Test-BootPsBytes $reversed) -and !(Test-BootPsBytes ([byte[]]($exact+0)))) 'bytes exatos e ordem estrita'
$mockPath=Join-Path $testRoot 'mock.ps1'
$mock=@'
function Get-BootPsConfig { return $global:BootPsFixture.Config }
function Set-BootPsConfig($State) { $global:BootPsFixture.Config=$State;$global:BootPsFixture.KeyWrites++ }
function Get-CimInstance { [CmdletBinding()]param($Namespace,$ClassName) if($Namespace -ne 'root\wmi' -or $ClassName -ne `
    'MSPower_DeviceWakeEnable'){throw 'Consulta nao prevista no teste'}; return $global:BootPsFixture.Devices }
function Set-CimInstance { [CmdletBinding()]param($InputObject,$Property) $global:BootPsFixture.DeviceWrites++;$InputObject.Enable=[bool]$Property.Enable }
'@
[IO.File]::WriteAllText($mockPath,$mock,[Text.Encoding]::ASCII)
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ativar-despertar-ps.ps1'))
$dot=". (Join-Path `$PSScriptRoot 'despertar-ps-comum.ps1')"
if(!$source.Contains($dot)){throw 'Ponto de isolamento alterado; revisar o teste antes de executar.'}
$source=$source.Replace($dot,". '$($common.Replace("'","''"))'`r`n. '$($mockPath.Replace("'","''"))'")
$tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$errors)
$admin=$ast.Find({param($n) $n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left -is `
    [Management.Automation.Language.VariableExpressionAst] -and $n.Left.VariablePath.UserPath -eq 'admin'},$true)
if($null -eq $admin){throw 'Guarda administrativa alterada; revisar o isolamento.'}
$source=$source.Substring(0,$admin.Extent.StartOffset)+'$admin=$true # apenas neste arquivo de teste'+$source.Substring($admin.Extent.EndOffset)
$harness=Join-Path $testRoot 'ativador-isolado.ps1';[IO.File]::WriteAllText($harness,$source,[Text.Encoding]::ASCII)
$backup=Join-Path $testRoot 'policy\backup.json'
$global:BootPsFixture=[pscustomobject]@{Config=[pscustomobject]@{Exists=$false;Kind='None';Data=$null};KeyWrites=0;DeviceWrites=0;Devices=@(
    [pscustomobject]@{InstanceName=$sony;Enable=$false},[pscustomobject]@{InstanceName=$other; `
        Enable=$false},[pscustomobject]@{InstanceName=$usb;Enable=$false})}
try {
    & $harness -ArquivoBackup $backup -WhatIf
    Check (!(Test-Path -LiteralPath $backup) -and $global:BootPsFixture.KeyWrites -eq 0 -and $global:BootPsFixture.DeviceWrites -eq 0) `
        'WhatIf nao altera nem cria backup'
    & $harness -ArquivoBackup $backup
    $first=Read-BootPsBackup $backup
    Check ($global:BootPsFixture.KeyWrites -eq 1 -and !$first.OriginalKey.Exists -and $first.KeyChanged -and `
        $global:BootPsFixture.DeviceWrites -eq 0) 'backup antecede a tabela; reinicio precede armamento'
    & $harness -ArquivoBackup $backup
    Check ($global:BootPsFixture.DeviceWrites -eq 1 -and $global:BootPsFixture.Devices[0].Enable -and `
        !$global:BootPsFixture.Devices[1].Enable -and !$global:BootPsFixture.Devices[2].Enable) 'somente a instancia Bluetooth Sony e habilitada'
    $saved=Read-BootPsBackup $backup
    Check (@($saved.DeviceChanges).Count -eq 1 -and !$saved.DeviceChanges[0].PriorEnabled -and !$saved.OriginalKey.Exists) `
        'backup original mantido ao armar e salvo antes da mudanca'
    & $harness -ArquivoBackup $backup
    Check ($global:BootPsFixture.DeviceWrites -eq 1 -and $global:BootPsFixture.KeyWrites -eq 1) 'ativacao repetida idempotente'
    & $harness -ArquivoBackup $backup -Desfazer -WhatIf
    Check ((Read-BootPsBackup $backup).Active -and $global:BootPsFixture.DeviceWrites -eq 1 -and $global:BootPsFixture.KeyWrites -eq 1) `
        'reversao WhatIf preserva o backup ativo e todos os estados'
    & $harness -ArquivoBackup $backup -Desfazer
    $saved=Read-BootPsBackup $backup
    Check (!$global:BootPsFixture.Config.Exists -and !$global:BootPsFixture.Devices[0].Enable -and !$saved.Active) `
        'reversao restaura ausencia da chave e permissao exata'
    $custom=[Convert]::ToBase64String([byte[]](1,2,3,4));$global:BootPsFixture.Config=[pscustomobject]@{Exists=$true;Kind='Binary';Data=$custom}
    & $harness -ArquivoBackup $backup
    & $harness -ArquivoBackup $backup -Desfazer
    Check ($global:BootPsFixture.Config.Exists -and $global:BootPsFixture.Config.Kind -eq 'Binary' -and `
        $global:BootPsFixture.Config.Data -eq $custom) 'reversao restaura dados personalizados em vez de apagar a chave'
    & $harness -ArquivoBackup $backup
    & $harness -ArquivoBackup $backup
    $present=$global:BootPsFixture.Devices;$global:BootPsFixture.Devices=@();$refused=$false
    try{& $harness -ArquivoBackup $backup -Desfazer}catch{$refused=$true}
    Check ($refused -and (Read-BootPsBackup $backup).Active) 'instancia ausente nao produz falsa restauracao'
    $global:BootPsFixture.Devices=$present
    & $harness -ArquivoBackup $backup -Desfazer
    $result=[ordered]@{Success=$true;Checks=$checks;SystemMutations=$false;RegistryAndWmi='substituidos';Artifacts=$testRoot}
    [IO.File]::WriteAllText((Join-Path $testRoot 'resultado.json'),($result|ConvertTo-Json),[Text.Encoding]::ASCII)
    $result|ConvertTo-Json
} finally {Remove-Variable -Name BootPsFixture -Scope Global -ErrorAction SilentlyContinue}
