# Registra quando o botao Guia (Xbox / PS emulado) e apertado e solto, lendo o XInput
# (XInputGetStateEx, ordinal 100 do xinput1_4.dll, o unico que expoe o Guia).
# So le o estado do controle; para sozinho depois de -Minutos.
# Saida: %USERPROFILE%\GameBarProbe\logs\guia.log (uma linha por aperto, com a duracao).
param([int]$Minutos = 10)

$ErrorActionPreference = 'Stop'
$logDir = Join-Path $env:USERPROFILE 'GameBarProbe\logs'
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir 'guia.log'

Add-Type -Namespace GameBarProbeTools -Name XInput -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct XINPUT_STATE {
    public uint dwPacketNumber;
    public ushort wButtons;
    public byte bLeftTrigger;
    public byte bRightTrigger;
    public short sThumbLX;
    public short sThumbLY;
    public short sThumbRX;
    public short sThumbRY;
}
[DllImport("xinput1_4.dll", EntryPoint = "#100")]
public static extern uint GetStateEx(uint userIndex, ref XINPUT_STATE state);
'@

$GUIDE = 0x0400
$down = @{}
$stamp = { (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') }
Add-Content -Path $log -Value ("{0} | monitor iniciado por {1} min" -f (& $stamp), $Minutos)
$end = (Get-Date).AddMinutes($Minutos)
$seen = @{}
while ((Get-Date) -lt $end) {
    for ($i = 0; $i -lt 4; $i++) {
        $st = New-Object GameBarProbeTools.XInput+XINPUT_STATE
        $rc = [GameBarProbeTools.XInput]::GetStateEx([uint32]$i, [ref]$st)
        if ($rc -ne 0) { continue }
        if (-not $seen.ContainsKey($i)) { $seen[$i] = $true; Add-Content -Path $log -Value ("{0} | controle {1} conectado" -f (& $stamp), $i) }
        $isDown = ($st.wButtons -band $GUIDE) -ne 0
        if ($isDown -and -not $down.ContainsKey($i)) {
            $down[$i] = Get-Date
            Add-Content -Path $log -Value ("{0} | controle {1} | GUIA apertado" -f (& $stamp), $i)
        } elseif (-not $isDown -and $down.ContainsKey($i)) {
            $ms = [int]((Get-Date) - $down[$i]).TotalMilliseconds
            $down.Remove($i)
            Add-Content -Path $log -Value ("{0} | controle {1} | GUIA solto ({2} ms)" -f (& $stamp), $i, $ms)
        }
    }
    Start-Sleep -Milliseconds 15
}
Add-Content -Path $log -Value ("{0} | monitor encerrado" -f (& $stamp))
