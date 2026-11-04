// Desempenho para a previa: sensores do HWiNFO, FPS do RTSS, modo de energia e dados basicos do Windows.
// Tudo e leitura, exceto PowerMode.Set, que so roda quando o usuario escolhe um modo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace XGBP.Hardware
{
    // Tipos do HWiNFO: 1 temperatura, 2 tensao, 3 ventoinha, 4 corrente, 5 potencia, 6 clock, 7 uso, 8 outro.
    public sealed class HwReading
    {
        public int Type;
        public string Sensor = "", Label = "", Unit = "";
        public double Value, Min, Max, Avg;
    }

    public sealed class HwInfoSnapshot
    {
        // ok | off (sem memoria compartilhada) | dead (desligada pelo HWiNFO) | stale (sem atualizar) | error
        public string Status = "off", Error;
        public uint Version, Revision;
        public DateTime PollTime;
        public readonly List<HwReading> Readings = new List<HwReading>();
        public string Describe()
        {
            var o = new StringBuilder();
            o.AppendLine("HWiNFO " + Status + (Error != null ? " | " + Error : "") + " | v" + Version + "." + Revision + " | leitura " + PollTime.ToLocalTime().ToString("HH:mm:ss") + " | " + Readings.Count + " valores");
            foreach (var r in Readings)
                o.AppendLine(string.Format(CultureInfo.InvariantCulture, "t{0} | {1} | {2} = {3:0.###} {4}", r.Type, r.Sensor, r.Label, r.Value, r.Unit));
            return o.ToString();
        }
    }

    // Memoria compartilhada do HWiNFO ("Shared Memory Support" nas configuracoes dele). Layout pelo cabecalho, sem tamanhos fixos.
    public static class HwInfo
    {
        const string MapName = "Global\\HWiNFO_SENS_SM2";
        const uint Signature = 0x53695748; // "HWiS"; "DEAD" quando o HWiNFO desliga o compartilhamento

        public static HwInfoSnapshot Read()
        {
            var s = new HwInfoSnapshot();
            try
            {
                using (var map = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read))
                using (var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                {
                    if (view.ReadUInt32(0) != Signature) { s.Status = "dead"; return s; }
                    s.Version = view.ReadUInt32(4); s.Revision = view.ReadUInt32(8);
                    s.PollTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(view.ReadInt64(12));
                    uint sOff = view.ReadUInt32(20), sSize = view.ReadUInt32(24), sNum = view.ReadUInt32(28);
                    uint rOff = view.ReadUInt32(32), rSize = view.ReadUInt32(36), rNum = view.ReadUInt32(40);
                    if (sSize < 136 || rSize < 316 || sNum > 4096 || rNum > 65536 ||
                        sOff + (long)sSize * sNum > view.Capacity || rOff + (long)rSize * rNum > view.Capacity)
                    { s.Status = "error"; s.Error = "layout inesperado"; return s; }
                    var names = new string[sNum];
                    for (int i = 0; i < sNum; i++) names[i] = Ansi(view, sOff + (long)i * sSize + 8, 128);
                    for (int i = 0; i < rNum; i++)
                    {
                        long b = rOff + (long)i * rSize;
                        uint sensor = view.ReadUInt32(b + 4);
                        s.Readings.Add(new HwReading {
                            Type = (int)view.ReadUInt32(b), Sensor = sensor < sNum ? names[sensor] : "",
                            Label = Ansi(view, b + 12, 128), Unit = Ansi(view, b + 268, 16),
                            Value = view.ReadDouble(b + 284), Min = view.ReadDouble(b + 292), Max = view.ReadDouble(b + 300), Avg = view.ReadDouble(b + 308) });
                    }
                    s.Status = (DateTime.UtcNow - s.PollTime).TotalSeconds > 15 ? "stale" : "ok";
                }
            }
            catch (FileNotFoundException) { s.Status = "off"; }
            catch (Exception ex) { s.Status = "error"; s.Error = ex.Message; }
            return s;
        }
        public static bool Running()
        {
            bool found = false;
            foreach (var name in new[] { "HWiNFO64", "HWiNFO32", "HWiNFO" })
                foreach (var p in Process.GetProcessesByName(name)) { found = true; p.Dispose(); }
            return found;
        }
        static string Ansi(UnmanagedMemoryAccessor view, long at, int max)
        {
            var bytes = new byte[max]; view.ReadArray(at, bytes, 0, max);
            int n = Array.IndexOf(bytes, (byte)0); if (n < 0) n = max;
            return Encoding.Default.GetString(bytes, 0, n).Trim();
        }
    }

    public sealed class FrameStats
    {
        public int Pid;
        public string App = "", Source = "RTSS";
        public double Fps, FrameTimeMs;
    }

    // Memoria compartilhada do RivaTuner Statistics Server. O RTSS mede os quadros do jogo; aqui so se le o resultado.
    public static class Rtss
    {
        const uint Signature = 0x52545353; // "RTSS"
        static readonly Dictionary<int, uint> lastTime = new Dictionary<int, uint>();

        public static FrameStats Read(int excludePid, int preferPid, out string status)
        {
            status = "off";
            try
            {
                using (var map = MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2", MemoryMappedFileRights.Read))
                using (var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                {
                    if (view.ReadUInt32(0) != Signature) return null;
                    uint size = view.ReadUInt32(8), offset = view.ReadUInt32(12), count = view.ReadUInt32(16);
                    if (size < 284 || count > 4096 || offset + (long)size * count > view.Capacity) { status = "error"; return null; }
                    status = "ok";
                    uint now = (uint)Environment.TickCount;
                    FrameStats best = null; bool bestPreferred = false;
                    var seen = new Dictionary<int, uint>();
                    for (int i = 0; i < count; i++)
                    {
                        long b = offset + (long)i * size;
                        int pid = (int)view.ReadUInt32(b);
                        if (pid == 0 || pid == excludePid) continue;
                        uint t0 = view.ReadUInt32(b + 268), t1 = view.ReadUInt32(b + 272), frames = view.ReadUInt32(b + 276), frameTime = view.ReadUInt32(b + 280);
                        if (frames == 0 || t1 <= t0) continue;
                        seen[pid] = t1;
                        // Atual se o periodo mudou desde a ultima leitura ou terminou ha pouco; jogo minimizado deixa de contar.
                        uint previous;
                        bool fresh = lastTime.TryGetValue(pid, out previous) ? previous != t1 || now - t1 < 3000 : now - t1 < 3000;
                        if (!fresh) continue;
                        var bytes = new byte[260]; view.ReadArray(b + 4, bytes, 0, 260);
                        int n = Array.IndexOf(bytes, (byte)0); if (n < 0) n = 260;
                        var f = new FrameStats { Pid = pid, App = Path.GetFileNameWithoutExtension(Encoding.Default.GetString(bytes, 0, n)),
                            Fps = 1000.0 * frames / (t1 - t0), FrameTimeMs = frameTime / 1000.0 };
                        bool preferred = pid == preferPid;
                        if (best == null || preferred && !bestPreferred || preferred == bestPreferred && f.Fps > best.Fps) { best = f; bestPreferred = preferred; }
                    }
                    lastTime.Clear(); foreach (var item in seen) lastTime[item.Key] = item.Value;
                    return best;
                }
            }
            catch (FileNotFoundException) { status = "off"; return null; }
            catch (Exception) { status = "error"; return null; }
        }
    }

    public sealed class PowerModeState
    {
        public Guid Mode;
        public bool OnBattery, Known, BalancedPlan = true;
        public string Error;
    }

    // Modo de energia do Windows 11 (Configuracoes > Energia), pela API publica de powrprof.h.
    public static class PowerMode
    {
        public static readonly Guid BestEfficiency = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        public static readonly Guid Balanced = Guid.Empty;
        public static readonly Guid BestPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");
        static readonly Guid BalancedScheme = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");

        public static PowerModeState Read()
        {
            var s = new PowerModeState();
            try
            {
                s.OnBattery = OnBattery();
                Guid mode;
                uint r = s.OnBattery ? PowerGetUserConfiguredDCPowerMode(out mode) : PowerGetUserConfiguredACPowerMode(out mode);
                if (r != 0) throw new System.ComponentModel.Win32Exception((int)r);
                s.Mode = mode; s.Known = true;
                IntPtr scheme;
                if (PowerGetActiveScheme(IntPtr.Zero, out scheme) == 0)
                {
                    // Fora do plano Equilibrado, o Windows nao oferece o modo de energia.
                    s.BalancedPlan = (Guid)Marshal.PtrToStructure(scheme, typeof(Guid)) == BalancedScheme;
                    LocalFree(scheme);
                }
            }
            catch (Exception ex) { s.Error = ex.Message; }
            return s;
        }
        public static void Set(Guid mode)
        {
            uint r = OnBattery() ? PowerSetUserConfiguredDCPowerMode(ref mode) : PowerSetUserConfiguredACPowerMode(ref mode);
            if (r != 0) throw new System.ComponentModel.Win32Exception((int)r);
        }
        public static string Name(Guid mode)
        {
            if (mode == BestEfficiency) return "Melhor eficiência de energia";
            if (mode == Balanced) return "Equilibrado";
            if (mode == BestPerformance) return "Desempenho Máximo";
            return "Modo " + mode;
        }
        static bool OnBattery()
        {
            SYSTEM_POWER_STATUS status;
            return GetSystemPowerStatus(out status) && status.ACLineStatus == 0;
        }

        struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }
        [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
        [DllImport("powrprof.dll")] static extern uint PowerGetUserConfiguredACPowerMode(out Guid mode);
        [DllImport("powrprof.dll")] static extern uint PowerGetUserConfiguredDCPowerMode(out Guid mode);
        [DllImport("powrprof.dll")] static extern uint PowerSetUserConfiguredACPowerMode(ref Guid mode);
        [DllImport("powrprof.dll")] static extern uint PowerSetUserConfiguredDCPowerMode(ref Guid mode);
        [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
    }

    // Dados que o Windows da sem o HWiNFO: nomes, uso da CPU, RAM, VRAM total e velocidade da memoria.
    public static class WindowsPerf
    {
        static long lastIdle, lastTotal;
        static string cpuName, gpuName;
        static double gpuMemory = -1;
        static int memorySpeed = -1;

        public static double? CpuUsage()
        {
            long idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return null;
            long total = kernel + user; // o tempo de kernel ja inclui o ocioso
            double? usage = null;
            if (lastTotal != 0 && total > lastTotal) usage = Math.Max(0, Math.Min(100, 100.0 * (1 - (double)(idle - lastIdle) / (total - lastTotal))));
            lastIdle = idle; lastTotal = total;
            return usage;
        }
        public static void Memory(out double usedMb, out double totalMb, out double load)
        {
            var m = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(m)) { usedMb = totalMb = load = 0; return; }
            totalMb = m.ullTotalPhys / 1048576.0; usedMb = (m.ullTotalPhys - m.ullAvailPhys) / 1048576.0; load = m.dwMemoryLoad;
        }
        public static string CpuName()
        {
            if (cpuName != null) return cpuName;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    cpuName = key == null ? "" : Regex.Replace(((key.GetValue("ProcessorNameString") as string) ?? "").Trim(), @"\s+", " ");
            }
            catch { cpuName = ""; }
            return cpuName;
        }
        // Placa com mais memoria dedicada (a dedicada, nao a integrada), lida do registro do driver de video.
        public static void Gpu(out string name, out double memoryMb)
        {
            if (gpuMemory < 0)
            {
                gpuName = ""; gpuMemory = 0;
                try
                {
                    using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                        if (cls != null) foreach (var sub in cls.GetSubKeyNames())
                        {
                            if (!Regex.IsMatch(sub, @"^\d{4}$")) continue;
                            try
                            {
                                using (var key = cls.OpenSubKey(sub))
                                {
                                    if (key == null) continue;
                                    double bytes = Bytes(key.GetValue("HardwareInformation.qwMemorySize")), legacy = Bytes(key.GetValue("HardwareInformation.MemorySize"));
                                    double size = Math.Max(bytes, legacy) / 1048576.0;
                                    string desc = key.GetValue("DriverDesc") as string;
                                    if (desc != null && size > gpuMemory) { gpuMemory = size; gpuName = desc.Trim(); }
                                }
                            }
                            catch { }
                        }
                }
                catch { }
            }
            name = gpuName; memoryMb = gpuMemory;
        }
        // Taxa configurada dos modulos (MT/s), lida uma vez pelo WMI.
        public static int MemorySpeed()
        {
            if (memorySpeed >= 0) return memorySpeed;
            memorySpeed = 0;
            try
            {
                using (var search = new ManagementObjectSearcher("SELECT ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory"))
                    foreach (ManagementObject m in search.Get())
                        using (m)
                        {
                            object value = m["ConfiguredClockSpeed"] ?? m["Speed"];
                            if (value != null) memorySpeed = Math.Max(memorySpeed, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                        }
            }
            catch { }
            return memorySpeed;
        }
        static double Bytes(object value)
        {
            if (value is long) return (long)value;
            if (value is int) return (uint)(int)value;
            var bytes = value as byte[];
            if (bytes != null && bytes.Length >= 8) return BitConverter.ToUInt64(bytes, 0);
            if (bytes != null && bytes.Length >= 4) return BitConverter.ToUInt32(bytes, 0);
            return 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        class MEMORYSTATUSEX
        {
            public uint dwLength = 64, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    }

    public sealed class PerfSnapshot
    {
        public string HwStatus = "off", HwError, RtssStatus = "off", TempUnit = "°C";
        public bool HwRunning;
        public uint HwVersion;
        public int HwReadings;
        public string CpuName = "", GpuName = "";
        public double? CpuUsage, CpuTemp, CpuClock, CpuClockPeak, CpuClockEffective, CpuPower, CpuVoltage;
        public double? GpuUsage, GpuTemp, GpuHotspot, GpuMemTemp, GpuClock, GpuMemClock, GpuPower, GpuVoltage, GpuFan, VramUsed, VramTotal;
        public double RamUsed, RamTotal, RamLoad;
        public double? RamClock, RamSpeed, Tcas, Trcd, Trp, Tras, Trfc, CommandRate, Fclk, Uclk, DimmTemp;
        public FrameStats Frames;
        public PowerModeState Power = new PowerModeState();

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            Func<double?, string> v = x => x.HasValue ? x.Value.ToString("0.###", c) : "-";
            return string.Join(Environment.NewLine, new[] {
                "HWiNFO=" + HwStatus + (HwError != null ? " (" + HwError + ")" : "") + " aberto=" + HwRunning + " v" + HwVersion + " valores=" + HwReadings + " | RTSS=" + RtssStatus,
                "CPU " + CpuName + " | uso=" + v(CpuUsage) + " temp=" + v(CpuTemp) + TempUnit + " clock=" + v(CpuClock) + " pico=" + v(CpuClockPeak) + " efetivo=" + v(CpuClockEffective) + " W=" + v(CpuPower) + " V=" + v(CpuVoltage),
                "GPU " + GpuName + " | uso=" + v(GpuUsage) + " temp=" + v(GpuTemp) + " hotspot=" + v(GpuHotspot) + " memTemp=" + v(GpuMemTemp) + " clock=" + v(GpuClock) + " mem=" + v(GpuMemClock) + " W=" + v(GpuPower) + " V=" + v(GpuVoltage) + " fan=" + v(GpuFan) + " VRAM=" + v(VramUsed) + "/" + v(VramTotal) + " MB",
                "RAM " + v(RamUsed) + "/" + v(RamTotal) + " MB carga=" + v(RamLoad) + " clock=" + v(RamClock) + " MT/s=" + v(RamSpeed) + " CL=" + v(Tcas) + "-" + v(Trcd) + "-" + v(Trp) + "-" + v(Tras) + " tRFC=" + v(Trfc) + " CR=" + v(CommandRate) +
                    " FCLK=" + v(Fclk) + " UCLK=" + v(Uclk) + " DIMM=" + v(DimmTemp),
                "FPS " + (Frames == null ? "-" : Frames.App + " pid=" + Frames.Pid + " fps=" + Frames.Fps.ToString("0.0", c) + " ft=" + Frames.FrameTimeMs.ToString("0.00", c) + "ms via " + Frames.Source),
                "Energia " + (Power.Known ? PowerMode.Name(Power.Mode) + " (" + Power.Mode + ")" : "?") + " bateria=" + Power.OnBattery + " planoEquilibrado=" + Power.BalancedPlan + (Power.Error != null ? " erro=" + Power.Error : "") });
        }
    }

    public static class Performance
    {
        public static PerfSnapshot Read(int excludePid, int preferPid, out HwInfoSnapshot hw)
        {
            var p = new PerfSnapshot();
            p.CpuName = WindowsPerf.CpuName();
            string gpu; double vram; WindowsPerf.Gpu(out gpu, out vram);
            p.GpuName = gpu; if (vram > 0) p.VramTotal = vram;
            p.CpuUsage = WindowsPerf.CpuUsage();
            WindowsPerf.Memory(out p.RamUsed, out p.RamTotal, out p.RamLoad);
            int speed = WindowsPerf.MemorySpeed(); if (speed > 0) p.RamSpeed = speed;
            hw = HwInfo.Read();
            p.HwStatus = hw.Status; p.HwError = hw.Error; p.HwVersion = hw.Version;
            p.HwRunning = hw.Status != "off" || HwInfo.Running();
            if (hw.Status == "ok" || hw.Status == "stale") Fill(p, hw.Readings);
            string rtss; var frames = Rtss.Read(excludePid, preferPid, out rtss);
            p.RtssStatus = rtss; if (frames != null) p.Frames = frames;
            p.Power = PowerMode.Read();
            return p;
        }

        // Traduz os rotulos do HWiNFO (variam por fabricante) nos campos do painel. Ordem dos padroes = preferencia.
        public static void Fill(PerfSnapshot p, List<HwReading> all)
        {
            p.HwReadings = all.Count;
            var cpu = all.FindAll(r => r.Sensor.StartsWith("CPU [#0]", StringComparison.OrdinalIgnoreCase));
            var usage = Find(cpu, 7, "^Total CPU Usage$");
            if (usage != null) p.CpuUsage = usage.Value;
            var temp = Find(cpu, 1, @"^CPU \(Tctl/Tdie\)$", "^CPU Package$", @"^CPU \(Tdie\)$", @"^CPU Die \(average\)$", "^Core Max$", @"^CPU \(Tctl\)$");
            if (temp != null) { p.CpuTemp = temp.Value; if (temp.Unit.Length > 0) p.TempUnit = temp.Unit; }
            var cores = cpu.FindAll(r => r.Type == 6 && Regex.IsMatch(r.Label, @"^([PE]-)?core \d+ clock", RegexOptions.IgnoreCase));
            // O clock principal e a media dos nucleos, como o Gerenciador de Tarefas; o efetivo (com os estados de repouso) vira detalhe.
            var effective = Find(cpu, 6, "^Average Effective Clock$");
            if (cores.Count > 0)
            {
                double sum = 0, max = 0;
                foreach (var c in cores) { sum += c.Value; max = Math.Max(max, c.Value); }
                p.CpuClock = sum / cores.Count; p.CpuClockPeak = max; p.CpuClockEffective = Value(effective);
            }
            else p.CpuClock = Value(effective);
            p.CpuPower = Value(Find(cpu, 5, "^CPU Package Power$", "^CPU PPT$", "^CPU Power$", "^Package Power$"));
            // AM5 informa o VDDCR_VDD pela SVI3 e AM4 o "CPU Core Voltage" pela SVI2; sem eles, o Vcore da placa-mae e por fim o maior VID.
            p.CpuVoltage = Value(Find(cpu, 2, "^CPU VDDCR_VDD Voltage", "^CPU Core Voltage", "^Vcore$", "^CPU VID$", @"^Core VIDs \(avg\)$") ?? Find(all, 2, "^Vcore$")) ??
                Max(cpu, 2, @"^Core \d+ VID$");

            var gpu = GpuReadings(all, p.GpuName);
            p.GpuUsage = Value(Find(gpu, 7, "^GPU Core Load$", "^GPU Utilization$", "^GPU D3D Usage$", "^GPU Usage$", "^GPU Load$"));
            p.GpuTemp = Value(Find(gpu, 1, "^GPU Temperature$", "^GPU Core Temperature$"));
            p.GpuHotspot = Value(Find(gpu, 1, @"^(?!.*\(max\)).*hot ?-?spot"));
            p.GpuMemTemp = Value(Find(gpu, 1, "^GPU Memory Junction Temperature$", "^GPU Memory Temperature$"));
            p.GpuClock = Value(Find(gpu, 6, "^GPU Clock$", @"^GPU Clock \(Effective\)$", "^GPU Effective Clock$", "^GPU Core Clock$"));
            p.GpuMemClock = Value(Find(gpu, 6, "^GPU Memory Clock$"));
            // "Total Board Power (TBP) Limit" tambem e medido em W: o padrao precisa terminar no rotulo.
            p.GpuPower = Value(Find(gpu, 5, @"^Total Board Power( \(TBP\))?$", "^GPU Power$", @"^Total Graphics Power( \(TGP\))?$", "^GPU ASIC Power$", "^GPU PPT$", "^GPU Core Power"));
            p.GpuVoltage = Value(Find(gpu, 2, "^GPU Core Voltage", "^GPU Voltage$"));
            p.GpuFan = Value(Find(gpu, 3, "^GPU Fan"));
            var vram = Find(gpu, 0, "^GPU Memory Allocated$", "^GPU D3D Memory Dedicated$", "^GPU Memory Used$");
            if (vram != null) p.VramUsed = vram.Unit.Trim().Equals("GB", StringComparison.OrdinalIgnoreCase) ? vram.Value * 1024 : vram.Value;
            var vramLoad = Find(gpu, 7, "^GPU Memory Usage$");
            if (!p.VramTotal.HasValue && p.VramUsed.HasValue && vramLoad != null && vramLoad.Value > 0) p.VramTotal = p.VramUsed / (vramLoad.Value / 100);

            var clock = Find(all, 6, "^Memory Clock$");
            if (clock != null) { p.RamClock = clock.Value; p.RamSpeed = Math.Round(clock.Value * 2); }
            p.Tcas = Value(Find(all, 0, "^Tcas$")); p.Trcd = Value(Find(all, 0, "^Trcd$"));
            p.Trp = Value(Find(all, 0, "^Trp$")); p.Tras = Value(Find(all, 0, "^Tras$"));
            p.Trfc = Value(Find(all, 0, "^Trfc$"));
            p.CommandRate = Value(Find(all, 0, "^Command Rate$"));
            p.Fclk = Value(Find(cpu, 6, @"^Infinity Fabric Clock \(FCLK\)$"));
            p.Uclk = Value(Find(cpu, 6, @"^Memory Controller Clock \(UCLK\)$"));
            p.DimmTemp = Max(all.FindAll(r => r.Sensor.StartsWith("DDR", StringComparison.OrdinalIgnoreCase)), 1, ".");

            // FPS pelo HWiNFO (RTSS ou driver); o RTSS direto tem prioridade. O sensor PresentMon do HWiNFO fica de fora:
            // ele segue o app em primeiro plano, que com a barra aberta e a propria barra ("Framerate Presented" nao casa aqui).
            var fps = Find(all, 0, @"^Framerate( \(.*\))?$");
            if (fps != null && fps.Value > 0)
            {
                var ft = Find(all, 0, "^Frame ?Time$");
                p.Frames = new FrameStats { Fps = fps.Value, FrameTimeMs = ft != null ? ft.Value : 1000 / fps.Value, App = "", Source = "HWiNFO" };
            }
        }
        static List<HwReading> GpuReadings(List<HwReading> all, string gpuName)
        {
            // Com mais de uma GPU, fica a que tem o nome da placa dedicada; senao, a primeira com consumo.
            var groups = new List<string>();
            foreach (var r in all)
            {
                var m = Regex.Match(r.Sensor, @"^GPU \[#\d+\]", RegexOptions.IgnoreCase);
                if (m.Success && !groups.Contains(m.Value)) groups.Add(m.Value);
            }
            string chosen = null;
            if (!string.IsNullOrEmpty(gpuName))
                chosen = groups.Find(g => all.Exists(r => r.Sensor.StartsWith(g, StringComparison.Ordinal) && r.Sensor.IndexOf(gpuName, StringComparison.OrdinalIgnoreCase) >= 0));
            if (chosen == null) chosen = groups.Find(g => all.Exists(r => r.Sensor.StartsWith(g, StringComparison.Ordinal) && r.Type == 5));
            if (chosen == null && groups.Count > 0) chosen = groups[0];
            return chosen == null ? new List<HwReading>() : all.FindAll(r => r.Sensor.StartsWith(chosen, StringComparison.Ordinal));
        }
        static HwReading Find(List<HwReading> list, int type, params string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var re = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                foreach (var r in list) if ((type == 0 || r.Type == type) && re.IsMatch(r.Label)) return r;
            }
            return null;
        }
        static double? Value(HwReading r) { return r == null ? (double?)null : r.Value; }
        static double? Max(List<HwReading> list, int type, string pattern)
        {
            double? max = null;
            foreach (var r in list)
                if (r.Type == type && Regex.IsMatch(r.Label, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) max = Math.Max(max ?? r.Value, r.Value);
            return max;
        }
    }
}
