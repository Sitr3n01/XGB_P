// Faixa de status como a do guia do Xbox: rede e bateria do DualSense em icones, a esquerda da hora grande.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using XGBP.Input;
using XGBP.Network;
using Shapes = System.Windows.Shapes;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const string WaitingText = "DualSense · aguardando";
        const string BatteryUnavailableText = "DualSense · bateria indisponível";
        static readonly TimeSpan BatteryMaxAge = TimeSpan.FromSeconds(5);
        Func<DateTime> batteryNow = delegate { return DateTime.UtcNow; };
        Func<NetworkState> networkRead = NetworkStatus.Read;
        NetworkState networkState;
        bool networkBusy;
        DateTime networkNextRead = DateTime.MinValue;
        string networkLogged, statusDrawn;
        readonly Dictionary<string, Battery> batteries = new Dictionary<string, Battery>();
        string batteryDevice;
        Canvas networkIcon;
        StackPanel batterySlot;
        TextBlock batteryPanelText;
        // Partes desenhadas, guardadas para a verificacao conferir o que aparece.
        Shapes.Rectangle batteryFill;
        Shapes.Path batteryBolt;
        TextBlock batteryMark;
        int networkLit;
#pragma warning disable 0414 // networkSlash: lido somente pelas verificacoes da build XGB_VERIFY
        bool networkBadge, networkSlash;
#pragma warning restore 0414

        void StartStatus()
        {
            var icons = (StackPanel)window.FindName("StatusIcons");
            // Todos os icones apoiados embaixo, como letras numa linha: o pe de cada um fica na linha de base da hora.
            networkIcon = new Canvas { Width = 26, Height = 20, VerticalAlignment = VerticalAlignment.Bottom };
            batterySlot = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            icons.Children.Add(networkIcon); icons.Children.Add(batterySlot);
            // O pe dos icones fica na linha de base dos digitos: sobe da base da linha o espaco abaixo da linha de base.
            var margin = icons.Margin; margin.Bottom = ClockDescent(); icons.Margin = margin;
            // A faixa sobe o espaco interno da fonte: o topo dos digitos fica na margem segura, alinhado ao topo da barra.
            ((FrameworkElement)window.FindName("StatusArea")).Margin = new Thickness(0, -ClockInkTop(), 0, 0);
#if XGB_VERIFY
            if (verify) { networkRead = ExampleNetwork; networkState = ExampleNetwork(); }
#endif
            UpdateStatusIcons();
        }

        // Altura da linha da hora menos a linha de base, na fonte e no tamanho do proprio TextBlock.
        double ClockDescent()
        {
            var clockText = (TextBlock)window.FindName("Clock");
            var text = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(clockText.FontFamily, clockText.FontStyle, clockText.FontWeight, clockText.FontStretch), clockText.FontSize, Brushes.White, 1.0);
            return text.Height - text.Baseline;
        }

        // Do topo da linha ao topo dos digitos (algarismos alinhados do Segoe UI), arredondado para o pixel.
        double ClockInkTop()
        {
            var clockText = (TextBlock)window.FindName("Clock");
            var text = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(clockText.FontFamily, clockText.FontStyle, clockText.FontWeight, clockText.FontStretch), clockText.FontSize, Brushes.White, 1.0);
            return Math.Round(text.BuildGeometry(new Point()).Bounds.Top);
        }

        void UpdateStatus()
        {
            ((TextBlock)window.FindName("Clock")).Text = DateTime.Now.ToString("HH:mm");
            SelectBattery();
            UpdateStatusIcons(); NetworkTick();
        }

        void NetworkTick()
        {
            // A verificacao usa estados controlados; a leitura real so roda com a barra visivel, a cada 5 s.
            if (verify || disposed || networkBusy || networkIcon == null || !window.IsVisible || DateTime.UtcNow < networkNextRead) return;
            networkBusy = true;
            var read = networkRead;
            Task.Factory.StartNew(delegate {
                NetworkState found;
                try { found = read(); } catch (Exception ex) { found = new NetworkState { Kind = "error", Error = ex.Message }; }
                DispatchPresentation(delegate {
                    networkBusy = false; networkNextRead = DateTime.UtcNow.AddSeconds(5);
                    string text = found.Describe();
                    if (text != networkLogged) { networkLogged = text; WriteLog("rede: " + text); }
                    networkState = found; UpdateStatusIcons();
                });
            });
        }

        // Navegacao pode vir do virtual; bateria exige origem fisica e relatorio recente.
        void SetBattery(string device, Battery value)
        {
            Battery previous;
            if (batteries.TryGetValue(device, out previous) && value.ObservedUtc < previous.ObservedUtc) return;
            batteries[device] = value;
            UpdateStatus();
        }
        void ControllerLost(string device)
        {
            if (!batteries.Remove(device)) return;
            UpdateStatus();
        }
        static int BatteryRank(Battery b) { return b.ChargeState < 0 ? 0 : b.Source == BatterySource.PhysicalUsb ? 2 : b.Source == BatterySource.PhysicalBluetooth ? 1 : 0; }
        void SelectBattery()
        {
            DateTime now = batteryNow();
            foreach (var key in new List<string>(batteries.Keys))
                if (batteries[key].ObservedUtc == DateTime.MinValue || now - batteries[key].ObservedUtc > BatteryMaxAge || batteries[key].ObservedUtc > now)
                    batteries.Remove(key);
            Battery selected;
            string choice = batteryDevice != null && batteries.TryGetValue(batteryDevice, out selected) && BatteryRank(selected) > 0 ? batteryDevice : null;
            int rank = choice == null ? 0 : BatteryRank(batteries[choice]);
            foreach (var pair in batteries)
            {
                int candidate = BatteryRank(pair.Value);
                if (candidate > rank || candidate > 0 && candidate == rank && choice != batteryDevice && string.CompareOrdinal(pair.Key, choice) < 0)
                { choice = pair.Key; rank = candidate; }
            }
            batteryDevice = choice;
            batteryText = choice == null ? batteries.Count == 0 ? WaitingText : BatteryUnavailableText : BatteryText(batteries[choice]);
            if (batteryPanelText != null && panel == 4) batteryPanelText.Text = batteryText;
        }
        static string BatteryText(Battery b)
        {
            return "HID · " + (b.Percent >= 0 ? b.Percent + "%" : "?") + " · " + b.ChargeText;
        }

        void UpdateStatusIcons()
        {
            if (networkIcon == null) return;
            var n = networkState; var b = new Battery();
            bool has = batteryDevice != null && batteries.TryGetValue(batteryDevice, out b);
            string key = (n == null ? "?" : n.Kind + n.Internet + n.Bars) + "|" + (has ? b.Raw + ":" + b.Percent + ":" + b.ChargeState : "-");
            if (key == statusDrawn) return;
            string previous = statusDrawn; statusDrawn = key;
            DrawNetwork(n); DrawBattery(has, b);
            if (previous == null || previous.Split('|')[0] != key.Split('|')[0]) MotionReveal(networkIcon, MotionFast);
            if (previous == null || previous.Split('|')[1] != key.Split('|')[1]) MotionReveal(batterySlot, MotionFast);
        }

        void DrawNetwork(NetworkState s)
        {
            var c = networkIcon; c.Children.Clear(); networkLit = 0; networkBadge = networkSlash = false;
            if (s == null || s.Kind == "error") { c.Visibility = Visibility.Collapsed; return; }
            c.Visibility = Visibility.Visible;
            string name;
            if (s.Kind == "ethernet")
            {
                // Conector de rede com cantos suaves, contatos curtos e cabo central discreto.
                c.Children.Add(IconStroke("M 6,2.8 H 20 Q 21.5,2.8 21.5,4.3 V 11.5 Q 21.5,13 20,13 H 17 V 16 H 9 V 13 H 6 Q 4.5,13 4.5,11.5 V 4.3 Q 4.5,2.8 6,2.8 Z", "TextPrimary", 1.6));
                c.Children.Add(IconStroke("M 8.5,3.2 V 6.5 M 11.5,3.2 V 6.5 M 14.5,3.2 V 6.5 M 17.5,3.2 V 6.5", "TextPrimary", 1.2));
                c.Children.Add(IconStroke("M 13,16.5 V 19", "TextPrimary", 1.6));
                name = "Ethernet";
            }
            else
            {
                // Ondas do Wi-Fi: as apagadas mostram o sinal que falta, como na bandeja do Windows.
                networkLit = s.Kind == "none" ? 0 : s.Bars < 0 ? 3 : (int)Math.Round(s.Bars * 3 / 5.0);
                double[] radii = { 5.5, 10, 14.5 };
                for (int i = 0; i < radii.Length; i++) c.Children.Add(IconStroke(Arc(13, 17.5, radii[i]), i < networkLit ? "TextPrimary" : "MeterTrack", 2));
                c.Children.Add(IconFill(Dot(13, 17.5, 1.9), s.Kind == "none" ? "MeterTrack" : "TextPrimary"));
                if (s.Kind == "none") { c.Children.Add(IconStroke("M 4,2.5 L 22,19", "TextPrimary", 2)); networkSlash = true; }
                name = s.Kind == "none" ? "Sem conexão de rede" : (s.Kind == "celular" ? "Rede celular" : "Wi-Fi") + (s.Bars >= 0 ? " · sinal " + s.Bars + " de 5" : "");
            }
            if (s.Kind != "none" && !s.Internet)
            {
                // Selo de alerta: conectado, mas sem internet.
                var badge = new Shapes.Ellipse { Width = 11, Height = 11, Fill = Brush("Critical"), Stroke = Brush("SurfaceRail"), StrokeThickness = 1.5 };
                Canvas.SetLeft(badge, 17); Canvas.SetTop(badge, 10); c.Children.Add(badge);
                c.Children.Add(IconFill("M 21.8,12.4 H 23.2 V 16.4 H 21.8 Z M 21.8,17.3 H 23.2 V 18.7 H 21.8 Z", "SurfaceRail"));
                networkBadge = true;
                name += " · sem internet";
            }
            else if (s.Kind != "none") name += " · internet";
            c.ToolTip = name; AutomationProperties.SetName(c, name);
        }

        // So a bateria, sem o icone do controle: sem leitura de bateria, o espaco fica vazio.
        void DrawBattery(bool has, Battery b)
        {
            var slot = batterySlot; slot.Children.Clear(); batteryFill = null; batteryBolt = null; batteryMark = null;
            if (has) slot.Children.Add(BatteryIcon(b));
            slot.ToolTip = has ? batteryText : null; AutomationProperties.SetName(slot, batteryText);
        }

        FrameworkElement BatteryIcon(Battery b)
        {
            bool error = b.Percent < 0, charging = b.ChargeState == 1;
            string outline = error ? "Critical" : "TextPrimary";
            var shell = new Border { Width = 23, Height = 13, BorderThickness = new Thickness(1.6), BorderBrush = Brush(outline), CornerRadius = new CornerRadius(2.5),
                Padding = new Thickness(1.6) };
            if (error)
            {
                batteryMark = new TextBlock { Text = "!", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush("Critical"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -3, 0, -3) };
                shell.Child = batteryMark;
            }
            else
            {
                // Carga baixa fica em destaque enquanto o controle nao esta carregando.
                double inner = 23 - 3.2 - 3.2;
                batteryFill = new Shapes.Rectangle { HorizontalAlignment = HorizontalAlignment.Left, RadiusX = 1, RadiusY = 1,
                    Width = Math.Max(1.5, inner * Math.Min(100, b.Percent) / 100.0), Fill = Brush(charging ? "AccentText" : b.Percent <= 15 ? "Critical" : "TextPrimary") };
                shell.Child = batteryFill;
            }
            var nub = new Border { Width = 2.4, Height = 5.5, Background = Brush(outline), CornerRadius = new CornerRadius(0, 1.2, 1.2, 0), VerticalAlignment = VerticalAlignment.Center };
            var body = new StackPanel { Orientation = Orientation.Horizontal }; body.Children.Add(shell); body.Children.Add(nub);
            var icon = new Grid { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            icon.Children.Add(body);
            if (charging && !error)
            {
                // Selo discreto ao lado, sem cobrir o nivel: raio limpo, sem animacao continua.
                var badge = new Border { Width = 11, Height = 11, Background = Brush("SurfaceRail"), CornerRadius = new CornerRadius(5.5),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -3, -2) };
                batteryBolt = new Shapes.Path { Data = Geometry.Parse("M 5.8,0 L 0.8,5.4 H 3.5 L 2.5,10 L 7.5,4.3 H 4.8 Z"), Fill = Brush("AccentText"),
                    Width = 7, Height = 9, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                badge.Child = batteryBolt; icon.Children.Add(badge);
            }
            return icon;
        }

        Shapes.Path IconStroke(string data, string brush, double thickness)
        {
            return new Shapes.Path { Data = Geometry.Parse(data), Stroke = Brush(brush), StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        }
        Shapes.Path IconFill(string data, string brush) { return new Shapes.Path { Data = Geometry.Parse(data), Fill = Brush(brush) }; }
        // Coordenadas sempre com ponto decimal: Geometry.Parse nao aceita a virgula do pt-BR.
        static string Arc(double cx, double cy, double r)
        {
            double d = r * Math.Sqrt(.5);
            return string.Format(CultureInfo.InvariantCulture, "M {0:0.##},{1:0.##} A {2:0.##},{2:0.##} 0 0 1 {3:0.##},{1:0.##}", cx - d, cy - d, r, cx + d);
        }
        static string Dot(double cx, double cy, double r)
        {
            return string.Format(CultureInfo.InvariantCulture, "M {0:0.##},{1:0.##} A {2:0.##},{2:0.##} 0 1 1 {3:0.##},{1:0.##} A {2:0.##},{2:0.##} 0 1 1 {0:0.##},{1:0.##} Z",
                cx - r, cy, r, cx + r);
        }

#if XGB_VERIFY
        static NetworkState ExampleNetwork() { return new NetworkState { Kind = "ethernet", Internet = true, InterfaceType = 6 }; }
#endif
        Battery StatusSample(byte raw, int percent, int charge, string text, BatterySource source = BatterySource.PhysicalUsb)
        { return new Battery { Raw = raw, Percent = percent, ChargeState = charge, ChargeText = text, Source = source, ObservedUtc = batteryNow() }; }

#if XGB_VERIFY
        void VerifyStatus()
        {
            var read = networkRead; var saved = networkState;
            try
            {
                SetPanel(0); window.UpdateLayout(); UpdateStatus();
                var clockText = (TextBlock)window.FindName("Clock");
                if (clockText.FontSize < 44 || clockText.FontFamily.Source != "Segoe UI" || clockText.Text != DateTime.Now.ToString("HH:mm"))
                    throw new Exception("Falha: hora grande na fonte do projeto.");
                // A faixa fica no canto superior direito, ao lado da barra de altura total, sem cruzar o painel.
                if (SceneBox(MotionPart("StatusArea")).Left < SceneBox(MotionPart("Sidebar")).Right + 24) throw new Exception("Falha: faixa de status invade o painel.");
                // Icones a esquerda da hora, com o pe na linha de base dos digitos.
                var scene = (UIElement)window.FindName("Scene"); var icons = (FrameworkElement)window.FindName("StatusIcons");
                double iconsBottom = icons.TranslatePoint(new Point(0, icons.ActualHeight), scene).Y;
                double baseline = clockText.TranslatePoint(new Point(0, clockText.ActualHeight - ClockDescent()), scene).Y;
                if (icons.TranslatePoint(new Point(icons.ActualWidth, 0), scene).X > clockText.TranslatePoint(new Point(0, 0), scene).X - 12 ||
                    Math.Abs(iconsBottom - baseline) > 1) throw new Exception("Falha: icones na linha de base da hora.");
                SetBattery("DualSense#0-USB", StatusSample(0x05, 55, 0, "descarregando")); window.UpdateLayout();
                foreach (FrameworkElement part in new[] { (FrameworkElement)networkIcon, (FrameworkElement)batterySlot.Children[0] })
                    if (Math.Abs(part.TranslatePoint(new Point(0, part.ActualHeight), scene).Y - baseline) > 1) throw new Exception("Falha: pe de cada icone na linha de base.");

                // Controle: aguardando, carga normal, baixa, carregando, erro e perda do dispositivo.
                batteries.Clear(); batteryDevice = null; batteryText = WaitingText; statusDrawn = null; UpdateStatusIcons();
                if (batterySlot.Children.Count != 0 || batterySlot.ToolTip != null) throw new Exception("Falha: sem bateria enquanto aguarda o controle.");
                SetBattery("DualSense#0-USB", StatusSample(0x08, 85, 0, "descarregando"));
                if (batteryFill == null || Math.Abs(batteryFill.Width - 16.6 * .85) > .01 || batteryFill.Fill != Brush("TextPrimary") || batteryBolt != null ||
                    batterySlot.Children.Count != 1 || (string)batterySlot.ToolTip != "HID · 85% · descarregando") throw new Exception("Falha: bateria do controle.");
                window.UpdateLayout(); SaveRender("faixa-status");
                SetBattery("DualSense#0-USB", StatusSample(0x01, 15, 0, "descarregando"));
                if (batteryFill == null || batteryFill.Fill != Brush("Critical")) throw new Exception("Falha: bateria baixa.");
                SetBattery("DualSense#0-USB", StatusSample(0x11, 15, 1, "carregando"));
                if (batteryBolt == null || batteryFill.Fill != Brush("AccentText")) throw new Exception("Falha: bateria carregando.");
                SetBattery("DualSense#1-BT", StatusSample(0xB0, -1, 0xB, "erro de temperatura", BatterySource.PhysicalBluetooth));
                ControllerLost("DualSense#0-USB");
                if (batteryMark == null || batteryFill != null || batteryBolt != null) throw new Exception("Falha: erro da bateria.");
                ControllerLost("DualSense#1-BT");
                SetBattery("DualSense#0-USB", StatusSample(0x11, 15, 1, "carregando"));
                if (batteryBolt == null || batteryDevice != "DualSense#0-USB") throw new Exception("Falha: nova bateria fisica.");
                ControllerLost("DualSense#0-USB");
                if (batteryDevice != null || batterySlot.Children.Count != 0 || batteryText != WaitingText) throw new Exception("Falha: perda do controle.");

                // Rede: Ethernet, Wi-Fi pelas barras, sem internet, sem conexao e erro de leitura.
                Func<NetworkState, bool> show = delegate(NetworkState s) { networkState = s; UpdateStatusIcons(); return networkIcon.Visibility == Visibility.Visible; };
                if (!show(new NetworkState { Kind = "ethernet", Internet = true }) || networkLit != 0 || networkBadge || (string)networkIcon.ToolTip != "Ethernet · internet")
                    throw new Exception("Falha: rede Ethernet.");
                if (!show(new NetworkState { Kind = "wifi", Internet = true, Bars = 3 }) || networkLit != 2 || networkBadge || networkSlash ||
                    (string)networkIcon.ToolTip != "Wi-Fi · sinal 3 de 5 · internet") throw new Exception("Falha: Wi-Fi com 3 barras.");
                SetBattery("DualSense#0-USB", StatusSample(0x16, 65, 1, "carregando"));
                window.UpdateLayout(); SaveRender("faixa-status-wifi");
                if (!show(new NetworkState { Kind = "wifi", Internet = true, Bars = 5 }) || networkLit != 3) throw new Exception("Falha: Wi-Fi com sinal cheio.");
                if (!show(new NetworkState { Kind = "ethernet", Internet = false }) || !networkBadge || !((string)networkIcon.ToolTip).EndsWith("sem internet"))
                    throw new Exception("Falha: rede sem internet.");
                if (!show(new NetworkState()) || !networkSlash || networkBadge || networkLit != 0) throw new Exception("Falha: sem conexao.");
                if (show(new NetworkState { Kind = "error", Error = "teste" })) throw new Exception("Falha: rede ilegivel deve ocultar o icone.");
            }
            finally
            {
                networkRead = read; networkState = saved; batteries.Clear(); batteryDevice = null; batteryText = WaitingText; statusDrawn = null; UpdateStatus();
            }
        }
#endif

#if XGB_VERIFY
        public void VerifyStatusLive()
        {
            // So leitura: rede pela API publica e bateria pelo HID de entrada, sem relatorios de saida.
            networkState = NetworkStatus.Read();
            var monitor = new DualSenseMonitor();
            var readings = new Dictionary<string, Battery>();
            monitor.Log += WriteLog;
            monitor.BatteryChanged += delegate(string d, Battery value) { lock (readings) readings[d] = value; };
            monitor.DeviceLost += delegate(string d) { lock (readings) readings.Remove(d); };
            monitor.Start(); Thread.Sleep(4000);
            // Copia antes do Stop, que corretamente publica a perda de todos os leitores encerrados.
            Dictionary<string, Battery> snapshot; lock (readings) snapshot = new Dictionary<string, Battery>(readings);
            monitor.Stop();
            foreach (var pair in snapshot) SetBattery(pair.Key, pair.Value);
            bool has = batteryDevice != null;
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); SetPanel(0); statusDrawn = null; UpdateStatusIcons(); ((TextBlock)window.FindName("Clock")).Text = DateTime.Now.ToString("HH:mm");
            window.UpdateLayout(); SaveRender("status-real"); window.Hide();
            File.WriteAllText(System.IO.Path.Combine(Output, "status-real.txt"), "rede: " + networkState.Describe() + Environment.NewLine +
                "controle: " + (has ? batteryDevice + " | " + batteryText : batteryText) + Environment.NewLine +
                "fontes observadas: " + snapshot.Count + " (apenas origens fisicas recentes podem representar a bateria)" + Environment.NewLine, Encoding.UTF8);
            WriteLog("faixa de status: leitura real concluida (rede e bateria, so leitura)");
        }
#endif
    }
}
