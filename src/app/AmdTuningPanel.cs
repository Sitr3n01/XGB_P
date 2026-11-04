// Painel AMD (ADR 0082): ajustes graficos globais pelo ADLX do driver, com o logotipo da AMD no trilho. Design aprovado em 29/10.
// Gravacao fica no painel 8 (AmdPanel.cs). Cada mudanca rele todo o estado: o driver pode mudar outro recurso junto.
// Metricas e FPS da AMD so na logica (AdlxGraphics.ReadMetrics/ReadFps), porque repetem o painel Desempenho.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using XGBP.Amd;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int AmdTuningPanel = 9;
        const string AmdTuningScope = "9";
        // Logotipo da AMD (seta), em unidades de 36: canto grande no alto a direita e o menor embaixo a esquerda.
        const string AmdLogo = "M0,0 L36,0 36,36 25.5,25.5 25.5,10.5 10.5,10.5 Z M10.5,14.9 L0,25.5 0,36 10.5,36 21.1,25.5 10.5,25.5 Z";
        static readonly string[][] AmdTuningGroups = {
            new[] { "UPSCALING E QUADROS", "fsr", "framegen", "afmf", "rsr" },
            new[] { "LATÊNCIA E DESEMPENHO", "antilag", "boost", "chill", "frtc" },
            new[] { "IMAGEM", "sharpening", "enhancedsync" } };
        // Substituiveis: a verificacao nunca chama o driver real nem muda ajustes da GPU.
        Func<GraphicsState> tuningRead = AdlxGraphics.Read;
        Action<string, bool> tuningSet = AdlxGraphics.SetEnabled;
        Func<string> tuningInstall = RadeonRelive.InstallFolder;
        readonly DispatcherTimer amdTuningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        GraphicsState amdTuning; string amdTuningError, amdTuningPending, amdTuningSignature, amdTuningFolder;
        bool amdTuningBusy;

        static string TuningName(string key)
        {
            switch (key)
            {
                case "fsr": return "FSR 4 (upgrade)";
                case "framegen": return "Geração de quadros FSR (upgrade)";
                case "afmf": return "Fluid Motion Frames";
                case "rsr": return "Radeon Super Resolution";
                case "antilag": return "Anti-Lag";
                case "boost": return "Radeon Boost";
                case "chill": return "Radeon Chill";
                case "frtc": return "Limite de FPS";
                case "sharpening": return "Nitidez da imagem";
                case "enhancedsync": return "Enhanced Sync";
            }
            return key;
        }
        static bool TuningFeminine(string key) { return key == "framegen" || key == "sharpening"; }
        static string TuningOn(string key, bool on) { return on ? (TuningFeminine(key) ? "ligada" : "ligado") : (TuningFeminine(key) ? "desligada" : "desligado"); }
        // Valores guardados no driver; so aparecem com o recurso ligado.
        static string TuningValue(GraphicsFeature f)
        {
            if (!f.Enabled || f.Value < 0) return null;
            switch (f.Key)
            {
                case "chill": return f.Value2 >= 0 ? f.Value + "–" + f.Value2 + " FPS" : f.Value + " FPS";
                case "frtc": return f.Value + " FPS";
                case "sharpening": return f.Value + "%";
                case "rsr": return "nitidez " + f.Value + "%";
                case "boost": return "mín. " + f.Value + "%";
            }
            return null;
        }

        void StartAmdTuning()
        {
            if (verify) { tuningRead = delegate { return null; }; tuningSet = delegate { }; tuningInstall = delegate { return null; }; return; }
            amdTuningTimer.Tick += delegate { if (panel == AmdTuningPanel && window.IsVisible) ReadAmdTuning(); };
            amdTuningTimer.Start();
        }
        // Leitura em worker; a mudanca usa o mesmo caminho para reler logo depois de aplicar.
        void ReadAmdTuning()
        {
            if (amdTuningBusy || disposed) return;
            amdTuningBusy = true;
            var read = tuningRead; var install = tuningInstall;
            RunAmdTuning(delegate {
                GraphicsState state = null; string error = null, folder = null;
                try { state = read(); } catch (Exception ex) { error = ex.Message; }
                try { folder = install(); } catch (Exception) { }
                return delegate { amdTuningBusy = false; ApplyAmdTuning(state, error, folder); };
            });
        }
        void RunAmdTuning(Func<Action> work)
        {
            if (verify) { work()(); return; }
            Task.Factory.StartNew(delegate { Action done; try { done = work(); } catch (Exception ex) { string m = ex.Message; done = delegate { amdTuningBusy = false; WriteLog("amd ajustes: " + m); }; } Dispatch(done); });
        }
        void ApplyAmdTuning(GraphicsState state, string error, string folder)
        {
            if (state != null) { amdTuning = state; amdTuningError = null; }
            else if (error != null) { amdTuningError = error; WriteLog("amd ajustes: leitura falhou | " + error); }
            if (folder != null) amdTuningFolder = folder;
            if (panel == AmdTuningPanel && window.IsVisible && TuningSignature() != amdTuningSignature) RebuildAmdTuning(null);
        }
        string TuningSignature()
        {
            var text = new System.Text.StringBuilder((amdTuningError ?? "") + "|" + amdTuningPending + "|" + amdTuningFolder);
            if (amdTuning != null) foreach (var f in amdTuning.Features) text.Append("|" + f.Key + f.Supported + f.Enabled + f.Value + "/" + f.Value2 + f.Error);
            return text.ToString();
        }

        void ToggleAmdTuning(string key)
        {
            var before = amdTuning; var f = before == null ? null : before.Find(key);
            if (f == null || !f.Supported || f.Error != 0) return;
            if (amdTuningPending != null) { Feedback("Aguarde o ajuste anterior terminar.", FeedbackKind.Pending); return; }
            bool expect = !f.Enabled; string name = TuningName(key);
            var notice = BeginFeedback((expect ? "Ligando: " : "Desligando: ") + name + "…", AmdTuningScope, 15);
            amdTuningPending = key; RebuildAmdTuning("amd-tune-" + key);
            WriteLog("amd ajustes: " + key + " -> " + (expect ? "ligado" : "desligado"));
            var set = tuningSet; var read = tuningRead;
            amdTuningBusy = true;
            RunAmdTuning(delegate {
                Exception failure = null; GraphicsState after = null;
                try { set(key, expect); } catch (Exception ex) { failure = ex; }
                try { after = read(); } catch (Exception) { }
                return delegate {
                    amdTuningBusy = false; amdTuningPending = null;
                    if (after != null) { amdTuning = after; amdTuningError = null; }
                    var now = after == null ? null : after.Find(key);
                    if (failure != null) { FinishFeedback(notice, failure.Message, FeedbackKind.Error); WriteLog("amd ajustes: falhou | " + key + " | " + failure.Message); }
                    else if (now == null || now.Enabled != expect) FinishFeedback(notice, "O driver da AMD não aplicou: " + name + ". Um perfil do Adrenalin ou outro ajuste pode impedir.", FeedbackKind.Warning);
                    else FinishFeedback(notice, name + ": " + TuningOn(key, expect) + "." + TuningSideEffects(before, after, key), FeedbackKind.Success);
                    if (panel == AmdTuningPanel && window.IsVisible) RebuildAmdTuning("amd-tune-" + key);
                };
            });
        }
        // O driver liga/desliga dependencias sozinho (por exemplo, RSR e nitidez); a mensagem conta o que mudou junto.
        static string TuningSideEffects(GraphicsState before, GraphicsState after, string key)
        {
            if (before == null || after == null) return "";
            var changed = new List<string>();
            foreach (var b in before.Features)
            {
                if (b.Key == key) continue;
                var a = after.Find(b.Key);
                if (a != null && a.Enabled != b.Enabled) changed.Add((a.Enabled ? "ligou " : "desligou ") + TuningName(b.Key));
            }
            return changed.Count == 0 ? "" : " O driver também " + string.Join(" e ", changed.ToArray()) + ".";
        }

        FrameworkElement AmdLogoIcon(double size, Brush fill)
        {
            var path = new Path { Data = Geometry.Parse(AmdLogo), Stretch = Stretch.Uniform, Fill = fill };
            return new Viewbox { Width = size, Height = size, Child = path };
        }
        void BuildAmdTuningPanel()
        {
            amdTuningSignature = TuningSignature();
            var s = amdTuning;
            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 10) };
            var logo = AmdLogoIcon(14, Brush("TextPrimary")); logo.Margin = new Thickness(0, 0, 9, 0); logo.VerticalAlignment = VerticalAlignment.Center;
            status.Children.Add(logo);
            status.Children.Add(Text(s != null ? s.Gpu.Replace("AMD ", "") + " · ajustes globais" : amdTuningError != null ? "Driver da AMD indisponível" : "Lendo o driver da AMD…", 15, "Muted"));
            body.Children.Add(status);
            if (s == null)
            {
                if (amdTuningError != null) ConnectionNote(amdTuningError);
            }
            else
            {
                foreach (var group in AmdTuningGroups)
                {
                    Caption(group[0]);
                    for (int i = 1; i < group.Length; i++) AmdTuningRow(s.Find(group[i]), group[i]);
                }
                ConnectionNote("Ajustes globais do AMD Software. Perfis por jogo do Adrenalin têm prioridade; FSR 4 e geração de quadros valem só em jogos compatíveis.");
            }
            var software = ConnectionButton(body, "Abrir o AMD Software", "↗", "amd-tuning-software",
                delegate { OpenAmdTarget(System.IO.Path.Combine(amdTuningFolder ?? "", "RadeonSoftware.exe"), "o AMD Software"); });
            software.IsEnabled = amdTuningFolder != null;
        }
        void AmdTuningRow(GraphicsFeature f, string key)
        {
            bool pending = amdTuningPending == key;
            string state = f == null || f.Error != 0 ? "Indisponível" : !f.Supported ? "Não suportado" : pending ? "Aplicando…" : f.Enabled ? "Ativado" : "Desativado";
            string value = f != null && f.Error == 0 && !pending ? TuningValue(f) : null;
            var row = ToggleSetting(body, TuningName(key), f == null || f.Error != 0 || !f.Supported ? (bool?)null : f.Enabled,
                "amd-tune-" + key, delegate { ToggleAmdTuning(key); }, value != null ? state + " · " + value : state);
            row.IsEnabled = f != null && f.Error == 0 && f.Supported;
        }
        void RebuildAmdTuning(string focusTag)
        {
            if (panel != AmdTuningPanel) return;
            var focused = Keyboard.FocusedElement as Control; bool inside = focused != null && actions.Contains(focused);
            string tag = focusTag ?? (inside ? focused.Tag as string : null);
            var scroll = (ScrollViewer)window.FindName("BodyScroll"); double offset = scroll.VerticalOffset;
            CancelElementMotion(body); body.Children.Clear(); actions.Clear(); flyout = null; flyoutSelector = null; panelFill = null;
            BuildAmdTuningPanel();
            window.UpdateLayout(); scroll.ScrollToVerticalOffset(offset);
            if (!inside && focusTag == null) return;
            var target = tag == null ? null : actions.Find(c => (string)c.Tag == tag && c.IsEnabled);
            if (target == null) target = actions.Find(c => c.IsEnabled);
            if (target != null) target.Focus();
        }
    }
}
