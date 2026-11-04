// Fixtures e verificacoes deste painel, separadas do codigo de operacao.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using XGBP.Hardware;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static PerfSnapshot ExamplePerf()
        {
            return new PerfSnapshot { HwStatus = "ok", HwRunning = true, HwVersion = 8, HwReadings = 240, RtssStatus = "ok",
                CpuName = "Processador de exemplo · 8 núcleos", GpuName = "Placa de vídeo de exemplo",
                CpuUsage = 32, CpuTemp = 67, CpuClock = 4450, CpuClockPeak = 4550, CpuClockEffective = 3980, CpuPower = 88, CpuVoltage = 1.213,
                GpuUsage = 74, GpuTemp = 61, GpuHotspot = 75, GpuMemTemp = 70, GpuClock = 2610, GpuMemClock = 2500, GpuPower = 310, GpuVoltage = 1.05, GpuFan = 1450,
                VramUsed = 8396, VramTotal = 24560,
                RamUsed = 12697, RamTotal = 32768, RamLoad = 39, RamClock = 1800, RamSpeed = 3600, Tcas = 16, Trcd = 19, Trp = 19, Tras = 39, Trfc = 630, CommandRate = 1,
                Fclk = 1800, Uclk = 1800, DimmTemp = 42,
                Frames = new FrameStats { Pid = 4242, App = "JogoDeExemplo", Fps = 144, FrameTimeMs = 6.9 },
                Power = new PowerModeState { Known = true, Mode = PowerMode.BestPerformance } };
        }
        static HwReading R(int type, string sensor, string label, string unit, double value)
        {
            return new HwReading { Type = type, Sensor = sensor, Label = label, Unit = unit, Value = value };
        }
        void VerifyPerf()
        {
            var read = perfRead; var set = powerSet; var applied = new List<Guid>();
            try
            {
                powerSet = delegate(Guid g) { applied.Add(g); };
                perfSnapshot = ExamplePerf(); powerPending = null;
                SetPanel(3); window.UpdateLayout();
                if (TileText("cpu0") != "32%" || TileText("cpu1") != "67 °C" || TileText("cpu2") != "4,45 GHz" || TileText("gpu3") != "310 W" ||
                    TileText("ram2") != "3600 MT/s" || TileText("ram3") != "16" || perfText["ram-name"].Text != "timings 16-19-19-39 · 1T" ||
                    TileText("fps0") != "144" || TileText("fps1") != "6,9 ms" || perfText["vram"].Text != "VRAM  8,2 / 24,0 GB  ·  34%  ·  2500 MHz  ·  70 °C" ||
                    perfText["gpu-extra"].Text != "hotspot 75 °C  ·  núcleo 1,050 V  ·  ventoinha 1450 RPM" ||
                    perfText["cpu-extra"].Text != "pico 4,55 GHz  ·  efetivo 3,98 GHz  ·  núcleo 1,213 V" || perfText["ram-extra"].Text != "FCLK 1800 MHz  ·  UCLK 1:1  ·  tRFC 630  ·  DIMM 42 °C")
                    throw new Exception("Falha: valores do desempenho.");
                if (((ScrollViewer)window.FindName("BodyScroll")).ScrollableHeight > 0) throw new Exception("Falha: desempenho deve caber sem rolagem.");
                SaveRender("desempenho-exemplo");

                // Seletor de modo: lista flutuante, foco preso e aplicacao so pela escolha.
                rail[3].Focus(); Navigate("DIREITA");
                if ((string)((Control)Keyboard.FocusedElement).Tag != "power-mode" || !((TextBlock)perfModeButton.Content).Text.StartsWith("Desempenho Máximo"))
                    throw new Exception("Falha: entrada no seletor de modo.");
                var cardY = perfText["cpu0"].TranslatePoint(new Point(0, 0), body).Y;
                Activate(); window.UpdateLayout();
                if (!perfExpanded || actions.FindAll(c => ((string)c.Tag).StartsWith("power-", StringComparison.Ordinal)).Count != 4 ||
                    Math.Abs(perfText["cpu0"].TranslatePoint(new Point(0, 0), body).Y - cardY) > .5) throw new Exception("Falha: lista de modos flutuante.");
                SaveRender("desempenho-modos");
                for (int i = 0; i < 5; i++)
                {
                    Navigate("BAIXO");
                    if (!((string)((Control)Keyboard.FocusedElement).Tag).StartsWith("power-", StringComparison.Ordinal)) throw new Exception("Falha: foco preso na lista de modos.");
                }
                FocusPerfTag("power-efficiency");
                if ((string)((Control)Keyboard.FocusedElement).Tag != "power-efficiency") throw new Exception("Falha: focar modo eficiencia.");
                Activate();
                if (applied.Count != 1 || applied[0] != PowerMode.BestEfficiency || perfExpanded || !((TextBlock)perfModeButton.Content).Text.StartsWith("Melhor eficiência") ||
                    (string)((Control)Keyboard.FocusedElement).Tag != "power-mode") throw new Exception("Falha: aplicar modo de energia.");
                // As opcoes entram na arvore visual no layout seguinte, como entre dois toques do usuario.
                Activate(); window.UpdateLayout(); FocusPerfTag("power-efficiency");
                if ((string)((Control)Keyboard.FocusedElement).Tag != "power-efficiency") throw new Exception("Falha: focar opcao da lista.");
                Activate();
                if (applied.Count != 1 || perfExpanded) throw new Exception("Falha: escolher o modo atual nao deve reaplicar.");
                Activate(); Back();
                if (perfExpanded || (string)((Control)Keyboard.FocusedElement).Tag != "power-mode") throw new Exception("Falha: fechar lista de modos.");
                powerSet = delegate { throw new InvalidOperationException("recusado"); };
                Activate(); window.UpdateLayout(); FocusPerfTag("power-balanced"); Activate();
                if (!((TextBlock)window.FindName("Feedback")).Text.Contains("recusado") || !((TextBlock)perfModeButton.Content).Text.StartsWith("Melhor eficiência"))
                    throw new Exception("Falha: erro ao mudar o modo.");

                // Sem HWiNFO: so os dados do Windows e a instrucao para ativar o compartilhamento.
                perfSnapshot = new PerfSnapshot { HwStatus = "off", HwRunning = true, RtssStatus = "off", CpuName = "Processador de exemplo", CpuUsage = 12,
                    RamUsed = 8192, RamTotal = 32768, RamLoad = 25, RamSpeed = 3600, Power = new PowerModeState { Known = true, Mode = PowerMode.Balanced } };
                SetPanel(3); window.UpdateLayout();
                if (TileText("cpu0") != "12%" || TileText("cpu1") != "—" || TileText("gpu0") != "—" || TileText("ram2") != "3600 MT/s" || TileText("fps0") != "—" ||
                    !perfText["status"].Text.Contains("Shared Memory Support") || perfText["fps-name"].Text != "abra o RTSS para medir") throw new Exception("Falha: desempenho sem HWiNFO.");
                SaveRender("desempenho-sem-hwinfo");
                perfSnapshot.Power.BalancedPlan = false; UpdatePerf();
                if (perfModeButton.IsEnabled) throw new Exception("Falha: modo fora do plano Equilibrado.");

                // Rotulos reais variam por fabricante; o mapeamento escolhe pela ordem de preferencia.
                string amdCpu = "CPU [#0]: AMD Ryzen 7 5800X3D", amdGpu = "GPU [#0]: AMD Radeon RX 7900 XTX";
                var amd = new List<HwReading> {
                    R(7, amdCpu, "Total CPU Usage", "%", 20), R(6, amdCpu, "Core 0 Clock (perf #1/1)", "MHz", 4400), R(6, amdCpu, "Core 1 Clock (perf #2/2)", "MHz", 4500),
                    R(6, amdCpu, "Core 0 T0 Effective Clock", "MHz", 1000), R(6, amdCpu, "Average Effective Clock", "MHz", 3900),
                    R(1, amdCpu + ": Enhanced", "CPU (Tctl/Tdie)", "°C", 70), R(5, amdCpu + ": Enhanced", "CPU Package Power", "W", 95),
                    R(2, amdCpu + ": Enhanced", "CPU Core Voltage (SVI2 TFN)", "V", 1.25),
                    R(1, "GPU [#1]: AMD Radeon(TM) Graphics", "GPU Temperature", "°C", 45), R(5, "GPU [#1]: AMD Radeon(TM) Graphics", "GPU Power", "W", 5),
                    R(1, amdGpu, "GPU Temperature", "°C", 60), R(1, amdGpu, "GPU Hot Spot Temperature", "°C", 80), R(6, amdGpu, "GPU Clock", "MHz", 2600),
                    R(6, amdGpu, "GPU Memory Clock", "MHz", 2500), R(5, amdGpu, "GPU ASIC Power", "W", 300), R(7, amdGpu, "GPU Utilization", "%", 90),
                    R(8, amdGpu, "GPU Memory Allocated", "MB", 8000), R(3, amdGpu, "GPU Fan", "RPM", 1500),
                    R(6, "Memory Timings", "Memory Clock", "MHz", 1800), R(8, "Memory Timings", "Tcas", "T", 16), R(8, "Memory Timings", "Trcd", "T", 19),
                    R(8, "Memory Timings", "Trp", "T", 19), R(8, "Memory Timings", "Tras", "T", 39), R(8, "Memory Timings", "Command Rate", "T", 1) };
                var a = new PerfSnapshot { GpuName = "AMD Radeon RX 7900 XTX" }; Performance.Fill(a, amd);
                if (a.CpuUsage != 20 || a.CpuClock != 4450 || a.CpuClockPeak != 4500 || a.CpuClockEffective != 3900 || a.CpuTemp != 70 || a.CpuPower != 95 || a.CpuVoltage != 1.25 ||
                    a.GpuTemp != 60 || a.GpuHotspot != 80 || a.GpuClock != 2600 || a.GpuPower != 300 || a.GpuUsage != 90 || a.VramUsed != 8000 || a.GpuFan != 1500 ||
                    a.RamClock != 1800 || a.RamSpeed != 3600 || a.Tcas != 16 || a.Tras != 39 || a.CommandRate != 1 || a.Frames != null)
                    throw new Exception("Falha: mapeamento de sensores AMD.");
                string intel = "CPU [#0]: Intel Core i7-13700K", nv = "GPU [#0]: NVIDIA GeForce RTX 4080";
                var other = new List<HwReading> {
                    R(1, intel + ": DTS", "CPU Package", "°C", 65), R(5, intel, "CPU Package Power", "W", 120),
                    R(6, intel, "P-core 0 Clock (perf #5)", "MHz", 5300), R(6, intel, "E-core 8 Clock (perf #1)", "MHz", 4200),
                    R(7, nv, "GPU Core Load", "%", 97), R(5, nv, "GPU Power", "W", 280), R(1, nv, "GPU Hot Spot Temperature", "°C", 72),
                    R(8, nv, "GPU D3D Memory Dedicated", "MB", 9000), R(8, "RTSS", "Framerate", "FPS", 120), R(8, "RTSS", "Frame Time", "ms", 8.3),
                    R(2, "ASUS ROG STRIX Z790-E (Nuvoton NCT6798D)", "Vcore", "V", 1.28) };
                var b = new PerfSnapshot(); Performance.Fill(b, other);
                if (b.CpuTemp != 65 || b.CpuPower != 120 || b.CpuClock != 4750 || b.CpuClockPeak != 5300 || b.GpuUsage != 97 || b.GpuPower != 280 ||
                    b.GpuHotspot != 72 || b.VramUsed != 9000 || b.Frames == null || b.Frames.Fps != 120 || b.Frames.Source != "HWiNFO" || b.CpuVoltage != 1.28)
                    throw new Exception("Falha: mapeamento de sensores Intel/NVIDIA.");

                // Rotulos observados no HWiNFO 8.29 (Ryzen 7 7700X, RX 9070 XT, DDR5), com as armadilhas na frente das leituras certas.
                string am5 = "CPU [#0]: AMD Ryzen 7 7700X", rdna4 = "GPU [#0]: AMD Radeon RX 9070 XT: ASRock Radeon RX 9070 XT Taichi OC";
                var real = new List<HwReading> {
                    R(2, am5, "Core 0 VID", "V", 1.37), R(2, am5, "Core 4 VID", "V", 1.38), R(2, am5 + ": Enhanced", "CPU VDDCR_VDD Voltage (SVI3 TFN)", "V", 1.394),
                    R(6, am5, "Core 0 Clock (perf #3/4)", "MHz", 5265.5), R(6, am5, "Core 1 Clock (perf #1/1)", "MHz", 5290.5), R(6, am5, "Bus Clock", "MHz", 99.82),
                    R(6, am5, "Core 0 T0 Effective Clock", "MHz", 2451.3), R(6, am5, "Average Effective Clock", "MHz", 1328.755),
                    R(6, am5 + ": Enhanced", "Infinity Fabric Clock (FCLK)", "MHz", 1996.4), R(6, am5 + ": Enhanced", "Memory Controller Clock (UCLK)", "MHz", 2994.6),
                    R(6, "Memory Timings", "Memory Clock", "MHz", 2994.6), R(8, "Memory Timings", "Trfc", "T", 824),
                    R(2, "GIGABYTE B650M AORUS ELITE AX (ITE IT8689E)", "Vcore", "V", 1.416),
                    R(1, "DDR5 DIMM [#1]: Kingston KF556C36-16 (P0 CHANNEL A/DIMM 1)", "SPD Hub Temperature", "°C", 57.5),
                    R(1, "DDR5 DIMM [#3]: Kingston KF556C36-16 (P0 CHANNEL B/DIMM 1)", "SPD Hub Temperature", "°C", 56.5),
                    R(1, rdna4, "GPU Hot Spot Temperature (Max)", "°C", 59.75), R(1, rdna4, "GPU Hot Spot Temperature", "°C", 59),
                    R(1, rdna4, "GPU Memory Junction Temperature", "°C", 62), R(2, rdna4, "GPU Core Voltage (VDDCR_GFX)", "V", .892),
                    R(5, rdna4, "Total Board Power (TBP) Limit", "W", 340), R(5, rdna4, "Total Board Power (TBP)", "W", 68.551),
                    R(8, rdna4, "GPU Memory Usage", "MB", 402458), R(8, rdna4, "GPU D3D Memory Dedicated", "MB", 5454.156),
                    R(8, rdna4, "Framerate", "FPS", 0), R(8, "PresentMon", "Framerate Presented (avg)", "FPS", 240) };
                var d = new PerfSnapshot { GpuName = "AMD Radeon RX 9070 XT" }; Performance.Fill(d, real);
                if (d.CpuVoltage != 1.394 || d.CpuClock != 5278 || d.CpuClockPeak != 5290.5 || d.CpuClockEffective != 1328.755 || d.Fclk != 1996.4 || d.Uclk != 2994.6 || d.Trfc != 824 || d.DimmTemp != 57.5 || d.RamSpeed != 5989 ||
                    d.GpuHotspot != 59 || d.GpuMemTemp != 62 || d.GpuVoltage != .892 || d.GpuPower != 68.551 || d.VramUsed != 5454.156 || d.VramTotal.HasValue || d.Frames != null)
                    throw new Exception("Falha: mapeamento de sensores AM5/RDNA4.");
                if (UclkText(d) != "UCLK 1:1" || UclkText(new PerfSnapshot { RamClock = 3200, Uclk = 1600 }) != "UCLK 1:2") throw new Exception("Falha: proporcao UCLK.");
            }
            finally
            {
                perfRead = read; powerSet = set; powerPending = null; perfSnapshot = ExamplePerf(); SetPanel(0);
            }
        }
        public void VerifyPerfLive()
        {
            // Leitura e renderizacao apenas: o modo de energia nao e alterado.
            powerSet = delegate { throw new InvalidOperationException("Diagnóstico sem mudar o modo de energia."); };
            int own; using (var me = Process.GetCurrentProcess()) own = me.Id;
            HwInfoSnapshot hw = null;
            perfSnapshot = Task.Factory.StartNew<PerfSnapshot>(delegate {
                HwInfoSnapshot first; Performance.Read(own, 0, out first);
                Thread.Sleep(1000); // o uso da CPU vem da diferenca entre duas leituras
                return Performance.Read(own, 0, out hw);
            }).Result;
            File.WriteAllText(Path.Combine(Output, "desempenho-real.txt"), perfSnapshot.Describe() + Environment.NewLine, Encoding.UTF8);
            File.WriteAllText(Path.Combine(Output, "hwinfo-real.txt"), hw.Describe(), Encoding.UTF8);
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); SetPanel(3); window.UpdateLayout(); SaveRender("desempenho-real"); window.Hide();
            WriteLog("desempenho: leitura e render reais concluidos, sem mudar o modo de energia");
        }
    }
}
