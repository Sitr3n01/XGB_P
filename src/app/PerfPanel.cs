// Painel Desempenho: sensores do HWiNFO, FPS do RTSS e modo de energia do Windows. Leitura em worker a cada ~1 s.
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
        static readonly CultureInfo PtBr = new CultureInfo("pt-BR");
        readonly DispatcherTimer perfTimer = new DispatcherTimer();
        readonly Dictionary<string, TextBlock> perfText = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, ProgressBar> perfBars = new Dictionary<string, ProgressBar>();
        Func<PerfSnapshot> perfRead;
        Action<Guid> powerSet = PowerMode.Set;
        PerfSnapshot perfSnapshot;
        bool perfBusy, perfExpanded;
        DateTime perfNextRead = DateTime.MinValue;
        Guid? powerPending;
        int previousForegroundPid;
        string perfLogged;
        Button perfModeButton;

        void StartPerf()
        {
#if XGB_VERIFY
            if (verify) { perfRead = ExamplePerf; perfSnapshot = ExamplePerf(); return; }
#endif
            int own; using (var me = Process.GetCurrentProcess()) own = me.Id;
            perfRead = delegate { HwInfoSnapshot hw; return Performance.Read(own, previousForegroundPid, out hw); };
            perfTimer.Interval = TimeSpan.FromMilliseconds(250);
            perfTimer.Tick += delegate { PerfTick(); }; perfTimer.Start();
        }
        void PerfTick()
        {
            if (disposed || panel != 3 || !window.IsVisible || perfBusy || DateTime.UtcNow < perfNextRead) return;
            perfBusy = true;
            var read = perfRead;
            Task.Factory.StartNew(delegate {
                PerfSnapshot found = null; string error = null;
                try { found = read(); } catch (Exception ex) { error = ex.Message; }
                DispatchPresentation(delegate {
                    perfBusy = false; perfNextRead = DateTime.UtcNow.AddSeconds(1);
                    if (error != null) WriteLog("desempenho: leitura falhou | " + error);
                    if (found != null)
                    {
                        string state = "HWiNFO=" + found.HwStatus + " valores=" + (found.HwReadings > 0) + " RTSS=" + found.RtssStatus + " modo=" + found.Power.Mode;
                        if (state != perfLogged) { perfLogged = state; WriteLog("desempenho: " + state); }
                        perfSnapshot = found;
                    }
                    if (panel == 3 && window.IsVisible) UpdatePerf();
                });
            });
        }

        void BuildPerfPanel(bool reset)
        {
            CancelElementMotion(body);
            if (reset) perfExpanded = false;
            body.Children.Clear(); actions.Clear(); perfText.Clear(); perfBars.Clear(); flyout = null; flyoutSelector = null;
            ((TextBlock)window.FindName("PanelTitle")).Text = "Desempenho";
            AudioCaption(body, "MODO DE ENERGIA");
            perfModeButton = PanelButton(body, "", "power-mode", TogglePowerModes);
            if (perfExpanded)
            {
                var choices = new StackPanel();
                Flyout(body, choices, perfModeButton);
                foreach (var mode in new[] { PowerMode.BestEfficiency, PowerMode.Balanced, PowerMode.BestPerformance })
                {
                    var m = mode;
                    FlyoutChoice(PanelButton(choices, (m == CurrentPowerMode() ? "✓  " : "    ") + PowerMode.Name(m), PowerTag(m), delegate { ApplyPowerMode(m); }));
                }
            }
            var cpu = PerfCard("CPU", "cpu");
            PerfTiles(cpu, "cpu", "uso", "temperatura", "clock médio", "consumo"); PerfBar(cpu, "cpu"); PerfLine(cpu, "cpu-extra");
            var gpu = PerfCard("GPU", "gpu");
            PerfTiles(gpu, "gpu", "uso", "temperatura", "clock", "consumo"); PerfBar(gpu, "gpu");
            PerfLine(gpu, "vram"); PerfBar(gpu, "vram"); PerfLine(gpu, "gpu-extra");
            var ram = PerfCard("MEMÓRIA", "ram");
            PerfTiles(ram, "ram", "em uso", "carga", "frequência", "latência CL"); PerfBar(ram, "ram"); PerfLine(ram, "ram-extra");
            var fps = PerfCard("FPS", "fps");
            PerfTiles(fps, "fps", "quadros/s", "frametime");
            var status = Text("", 12, "Muted"); status.Margin = new Thickness(2, 2, 0, 0);
            body.Children.Add(status); perfText["status"] = status;
            UpdatePerf();
        }
        StackPanel PerfCard(string title, string key)
        {
            var card = new Border { Background = Brush("SurfaceCard"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 12), Margin = new Thickness(0, 0, 0, 10) };
            var stack = new StackPanel(); card.Child = stack;
            var head = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var name = Text(title, 12, "Muted"); name.FontWeight = FontWeights.SemiBold; name.VerticalAlignment = VerticalAlignment.Center;
            var detail = Text("", 13, "Muted"); detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
            detail.TextAlignment = TextAlignment.Right; detail.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(detail, 1);
            head.Children.Add(name); head.Children.Add(detail); stack.Children.Add(head);
            perfText[key + "-name"] = detail;
            body.Children.Add(card); return stack;
        }
        void PerfTiles(StackPanel parent, string key, params string[] labels)
        {
            // Quatro colunas em todos os cartoes, para os valores ficarem alinhados entre CPU, GPU, memoria e FPS.
            var grid = new UniformGrid { Columns = 4 };
            for (int i = 0; i < labels.Length; i++)
            {
                var tile = new StackPanel();
                var value = new TextBlock { FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Brush("TextPrimary"),
                    TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
                var label = Text(labels[i], 13, "Muted"); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
                tile.Children.Add(value); tile.Children.Add(label); grid.Children.Add(tile);
                perfText[key + i] = value; perfText[key + i + "-label"] = label;
            }
            parent.Children.Add(grid);
        }
        void PerfBar(StackPanel parent, string key)
        {
            var bar = new ProgressBar { Maximum = 100, Height = 4, Margin = new Thickness(0, 7, 0, 0), Foreground = Brush("XboxGreen"),
                Background = Brush("MeterTrack"), BorderThickness = new Thickness(0) };
            parent.Children.Add(bar); perfBars[key] = bar;
        }
        void PerfLine(StackPanel parent, string key)
        {
            var line = Text("", 13, "Muted"); line.TextWrapping = TextWrapping.NoWrap; line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.Margin = new Thickness(0, 6, 0, 0); parent.Children.Add(line); perfText[key] = line;
        }

        void UpdatePerf()
        {
            var p = perfSnapshot;
            if (p == null || panel != 3 || perfModeButton == null) return;
            UpdatePowerButton(p);
            SetPerfText("cpu-name", p.CpuName);
            SetTile("cpu0", N(p.CpuUsage, "0"), "%"); SetTile("cpu1", N(p.CpuTemp, "0"), " " + p.TempUnit);
            SetTile("cpu2", Ghz(p.CpuClock), " GHz"); SetTile("cpu3", N(p.CpuPower, "0"), " W");
            SetPerfBar("cpu", p.CpuUsage);
            SetPerfText("cpu-extra", Join(p.CpuClockPeak.HasValue ? "pico " + Ghz(p.CpuClockPeak) + " GHz" : null,
                p.CpuClockEffective.HasValue ? "efetivo " + Ghz(p.CpuClockEffective) + " GHz" : null, p.CpuVoltage.HasValue ? "núcleo " + N(p.CpuVoltage, "0.000") + " V" : null));

            SetPerfText("gpu-name", p.GpuName);
            SetTile("gpu0", N(p.GpuUsage, "0"), "%"); SetTile("gpu1", N(p.GpuTemp, "0"), " " + p.TempUnit);
            SetTile("gpu2", Ghz(p.GpuClock), " GHz"); SetTile("gpu3", N(p.GpuPower, "0"), " W");
            SetPerfBar("gpu", p.GpuUsage);
            double? vram = p.VramUsed.HasValue && p.VramTotal.HasValue && p.VramTotal.Value > 0 ? p.VramUsed.Value / p.VramTotal.Value * 100 : (double?)null;
            // Clock e temperatura da juncao ficam na linha da VRAM, ao lado do uso da propria memoria.
            SetPerfText("vram", Join("VRAM  " + (p.VramUsed.HasValue ? Gb(p.VramUsed.Value) : "—") + " / " + (p.VramTotal.HasValue ? Gb(p.VramTotal.Value) : "—") + " GB",
                vram.HasValue ? N(vram, "0") + "%" : null, p.GpuMemClock.HasValue ? N(p.GpuMemClock, "0") + " MHz" : null,
                p.GpuMemTemp.HasValue ? N(p.GpuMemTemp, "0") + " " + p.TempUnit : null));
            SetPerfBar("vram", vram);
            SetPerfText("gpu-extra", Join(p.GpuHotspot.HasValue ? "hotspot " + N(p.GpuHotspot, "0") + " " + p.TempUnit : null,
                p.GpuVoltage.HasValue ? "núcleo " + N(p.GpuVoltage, "0.000") + " V" : null, p.GpuFan.HasValue ? "ventoinha " + N(p.GpuFan, "0") + " RPM" : null));

            string timings = p.Tcas.HasValue ? string.Join("-", new[] { N(p.Tcas, "0"), N(p.Trcd, "0"), N(p.Trp, "0"), N(p.Tras, "0") }) +
                (p.CommandRate.HasValue ? " · " + N(p.CommandRate, "0") + "T" : "") : "";
            SetPerfText("ram-name", timings.Length > 0 ? "timings " + timings : "");
            SetTile("ram0", p.RamTotal > 0 ? Gb(p.RamUsed) : "—", " GB"); SetPerfText("ram0-label", p.RamTotal > 0 ? "de " + Gb(p.RamTotal) + " GB" : "em uso");
            SetTile("ram1", p.RamTotal > 0 ? N(p.RamLoad, "0") : "—", "%");
            SetTile("ram2", N(p.RamSpeed, "0"), " MT/s"); SetPerfText("ram2-label", p.RamClock.HasValue ? N(p.RamClock, "0") + " MHz" : "frequência");
            SetTile("ram3", N(p.Tcas, "0"), "");
            SetPerfBar("ram", p.RamTotal > 0 ? p.RamLoad : (double?)null);
            SetPerfText("ram-extra", Join(p.Fclk.HasValue ? "FCLK " + N(p.Fclk, "0") + " MHz" : null, UclkText(p),
                p.Trfc.HasValue ? "tRFC " + N(p.Trfc, "0") : null, p.DimmTemp.HasValue ? "DIMM " + N(p.DimmTemp, "0") + " " + p.TempUnit : null));

            var f = p.Frames;
            SetPerfText("fps-name", f != null ? (f.App.Length > 0 ? f.App + " · " : "") + f.Source :
                p.RtssStatus == "ok" ? "nenhum jogo medido agora" : p.RtssStatus == "off" ? "abra o RTSS para medir" : "RTSS ilegível");
            SetTile("fps0", f != null ? f.Fps.ToString("0", PtBr) : "—", "");
            SetTile("fps1", f != null ? f.FrameTimeMs.ToString("0.0", PtBr) : "—", " ms");
            SetPerfText("status", PerfStatus(p));
        }
        void UpdatePowerButton(PerfSnapshot p)
        {
            bool available = p.Power.Known && p.Power.BalancedPlan;
            string label = !p.Power.Known ? "Modo de energia indisponível" :
                p.Power.BalancedPlan ? PowerMode.Name(CurrentPowerMode()) : "Indisponível fora do plano Equilibrado";
            var text = (TextBlock)perfModeButton.Content;
            text.Text = label + (available ? perfExpanded ? "   ▴" : "   ▾" : "");
            perfModeButton.ToolTip = label; System.Windows.Automation.AutomationProperties.SetName(perfModeButton, "Modo de energia · " + label);
            if (!available && perfModeButton.IsKeyboardFocused) FocusNavigation(3);
            perfModeButton.IsEnabled = available; perfModeButton.Opacity = available ? 1 : .5;
        }
        static string PerfStatus(PerfSnapshot p)
        {
            string hw;
            if (p.HwStatus == "ok") hw = "Sensores: HWiNFO";
            else if (p.HwStatus == "stale") hw = "O HWiNFO parou de atualizar: abra a janela de sensores dele";
            else if (p.HwStatus == "dead") hw = "O HWiNFO desligou o compartilhamento (limite de 12 h da versão gratuita). Reative “Shared Memory Support”";
            else if (p.HwStatus == "error") hw = "HWiNFO ilegível: " + p.HwError;
            else hw = (p.HwRunning ? "HWiNFO sem compartilhamento: ative “Shared Memory Support” nas configurações dele" : "Abra o HWiNFO com “Shared Memory Support”") +
                " para temperatura, clock, consumo e timings";
            return hw + (p.Frames != null ? " · FPS: " + p.Frames.Source : p.RtssStatus == "ok" ? " · FPS: RTSS" : "");
        }
        void SetTile(string key, string number, string unit)
        {
            TextBlock t; if (!perfText.TryGetValue(key, out t)) return;
            t.Inlines.Clear(); t.Inlines.Add(new Run(number));
            // Unidade menor e apagada, para o numero ser o que se le de longe.
            if (number != "—" && unit.Length > 0) t.Inlines.Add(new Run(unit) { FontSize = 14, FontWeight = FontWeights.Normal, Foreground = Brush("Muted") });
        }
        string TileText(string key)
        {
            var text = new StringBuilder();
            foreach (var inline in perfText[key].Inlines) { var run = inline as Run; if (run != null) text.Append(run.Text); }
            return text.ToString();
        }
        void SetPerfText(string key, string value)
        {
            TextBlock t; if (!perfText.TryGetValue(key, out t)) return;
            t.Text = value ?? ""; t.ToolTip = string.IsNullOrEmpty(value) ? null : value;
        }
        void SetPerfBar(string key, double? percent)
        {
            ProgressBar b; if (!perfBars.TryGetValue(key, out b)) return;
            MotionTo(b, ProgressBar.ValueProperty, percent.HasValue ? Math.Max(0, Math.Min(100, percent.Value)) : 0, MotionNormal); b.Opacity = percent.HasValue ? 1 : .4;
        }
        static string UclkText(PerfSnapshot p)
        {
            // Proporcao UCLK:MCLK, como o BIOS e os entusiastas a chamam; fora de 1:1 e 1:2, o clock em MHz.
            if (!p.Uclk.HasValue) return null;
            if (p.RamClock.HasValue && p.Uclk.Value > 0)
            {
                double ratio = p.RamClock.Value / p.Uclk.Value;
                if (Math.Abs(ratio - 1) < .02) return "UCLK 1:1";
                if (Math.Abs(ratio - 2) < .04) return "UCLK 1:2";
            }
            return "UCLK " + N(p.Uclk, "0") + " MHz";
        }
        static string N(double? v, string format) { return v.HasValue ? v.Value.ToString(format, PtBr) : "—"; }
        static string Ghz(double? mhz) { return mhz.HasValue ? (mhz.Value / 1000).ToString("0.00", PtBr) : "—"; }
        static string Gb(double mb) { return (mb / 1024).ToString("0.0", PtBr); }
        static string Join(params string[] parts)
        {
            var list = new List<string>(); foreach (var s in parts) if (!string.IsNullOrEmpty(s)) list.Add(s);
            return string.Join("  ·  ", list.ToArray());
        }

        Guid CurrentPowerMode() { return powerPending ?? (perfSnapshot == null ? PowerMode.Balanced : perfSnapshot.Power.Mode); }
        static string PowerTag(Guid mode)
        {
            return mode == PowerMode.BestEfficiency ? "power-efficiency" : mode == PowerMode.Balanced ? "power-balanced" : mode == PowerMode.BestPerformance ? "power-performance" : "power-other";
        }
        void TogglePowerModes()
        {
            if (!perfModeButton.IsEnabled) return;
            if (perfExpanded) MotionCloseFlyout();
            perfExpanded = !perfExpanded; BuildPerfPanel(false); FocusPerfTag("power-mode");
            ActionSound(perfExpanded ? "lista" : "voltar");
        }
        bool PerfBack()
        {
            if (panel != 3 || !perfExpanded) return false;
            MotionCloseFlyout(); perfExpanded = false; BuildPerfPanel(false); FocusPerfTag("power-mode"); return true;
        }
        void FocusPerfTag(string tag)
        {
            var target = actions.Find(c => (string)c.Tag == tag && c.IsEnabled);
            if (target != null) target.Focus(); else FocusNavigation(3);
        }
        // Aplica o modo do Windows (o mesmo de Configuracoes > Energia) so por escolha do usuario.
        void ApplyPowerMode(Guid mode)
        {
            MotionCloseFlyout();
            perfExpanded = false;
            bool change = mode != CurrentPowerMode();
            if (change) powerPending = mode;
            BuildPerfPanel(false); FocusPerfTag("power-mode");
            if (!change) return;
            WriteLog("energia: modo -> " + PowerMode.Name(mode));
            var set = powerSet;
            Action<string> done = delegate(string error) {
                powerPending = null;
                if (error != null) Feedback("Não foi possível mudar o modo de energia: " + error, FeedbackKind.Error);
                else if (perfSnapshot != null) { perfSnapshot.Power.Mode = mode; perfSnapshot.Power.Known = true; }
                perfNextRead = DateTime.MinValue; UpdatePerf();
            };
            if (verify) { string error = null; try { set(mode); } catch (Exception ex) { error = ex.Message; } done(error); return; }
            Task.Factory.StartNew(delegate {
                string error = null;
                try { set(mode); } catch (Exception ex) { error = ex.Message; }
                Dispatch(delegate { done(error); });
            });
        }

    }
}
