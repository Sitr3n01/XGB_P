// Painel AMD (ADRs 0082 e 0100): ajustes graficos globais pelo ADLX do driver, com o logotipo da AMD no trilho.
// Gravacao fica no painel 8 (AmdPanel.cs). Cada mudanca rele todo o estado: o driver pode mudar outro recurso junto.
// Subopcoes so com o recurso ligado e alinhadas as demais linhas; seletores aplicam ao escolher, sliders ao parar de mover.
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
        // Substituiveis: a verificacao nunca chama o driver real nem muda ajustes da GPU.
        Func<GraphicsState> tuningRead = AdlxGraphics.Read;
        Action<string, bool> tuningSet = AdlxGraphics.SetEnabled;
        Action<string, string, int> tuningValue = AdlxGraphics.SetValue;
        Action tuningReset = AdlxGraphics.ResetShaderCache;
        Func<string> tuningInstall = RadeonRelive.InstallFolder;
        readonly DispatcherTimer amdTuningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        // Slider: aplica 600 ms depois do ultimo movimento, para nao mandar cada passo ao driver.
        readonly DispatcherTimer amdSliderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        GraphicsState amdTuning; string amdTuningError, amdTuningPending, amdTuningSignature, amdTuningFolder, amdTuningOpen, amdSliderId;
        int amdSliderValue, amdSliderRun; DateTime amdSliderLast;
        bool amdTuningBusy, amdResetConfirm, amdOpenRendered;

        void StartAmdTuning()
        {
            amdSliderTimer.Tick += delegate { FlushAmdSlider(); };
            if (verify) { tuningRead = delegate { return null; }; tuningSet = delegate { }; tuningValue = delegate { }; tuningReset = delegate { }; tuningInstall =
                delegate { return null; }; return; }
            amdTuningTimer.Tick += delegate { if (panel == AmdTuningPanel && window.IsVisible) ReadAmdTuning(); };
            amdTuningTimer.Start();
        }
        // Entrada no painel: lista fechada e sem confirmacao pendente.
        void EnterAmdTuning() { amdTuningOpen = null; amdResetConfirm = false; BuildAmdTuningPanel(); }
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
            Task.Factory.StartNew(delegate { Action done; try { done = work(); } catch (Exception ex) { string m = ex.Message; done = delegate { amdTuningBusy =
                false; WriteLog("amd ajustes: " + m); }; } Dispatch(done); });
        }
        void ApplyAmdTuning(GraphicsState state, string error, string folder)
        {
            if (state != null) { amdTuning = state; amdTuningError = null; }
            else if (error != null) { amdTuningError = error; WriteLog("amd ajustes: leitura falhou | " + error); }
            if (folder != null) amdTuningFolder = folder;
            // Com um slider em movimento, a releitura espera: reconstruir interromperia o arraste.
            if (panel == AmdTuningPanel && window.IsVisible && amdSliderId == null && TuningSignature() != amdTuningSignature) RebuildAmdTuning(null);
        }
        string TuningSignature()
        {
            var text = new System.Text.StringBuilder((amdTuningError ?? "") + "|" + amdTuningPending + "|" + amdTuningFolder + "|" + amdTuningOpen + "|" +
                amdResetConfirm);
            if (amdTuning != null)
                foreach (var f in amdTuning.Features)
                {
                    text.Append("|" + f.Key + f.Supported + f.Enabled + f.Error + "/" + f.Min + "/" + f.Max + "/" + f.Step);
                    foreach (var v in f.Values) text.Append("," + v.Key + "=" + v.Value);
                }
            return text.ToString();
        }

        // Um ajuste por vez: aplica no worker, rele e confirma pelo estado real (ou avisa que o driver nao aplicou).
        void RunAmdChange(string id, string start, string name, Action apply, Func<GraphicsState, bool> applied, string done)
        {
            var before = amdTuning;
            var notice = BeginFeedback(start, AmdTuningScope, 15);
            if (id == "resetcache") { notice.Pinned = true; RenderFeedback(); }
            amdTuningPending = id; RebuildAmdTuning(id == "resetcache" ? "amd-reset" : "amd-tune-" + id);
            var read = tuningRead; amdTuningBusy = true;
            RunAmdTuning(delegate {
                Exception failure = null, readFailure = null; GraphicsState after = null;
                try { apply(); } catch (Exception ex) { failure = ex; }
                try { after = read(); } catch (Exception ex) { readFailure = ex; }
                return delegate {
                    amdTuningBusy = false; amdTuningPending = null;
                    if (after != null) { amdTuning = after; amdTuningError = null; }
else if (readFailure != null) { amdTuningError = readFailure.Message; WriteLog("amd ajustes: releitura falhou | " + id + " | " + readFailure.Message); }
if (failure != null) { FinishFeedback(notice, failure.Message, FeedbackKind.Error); WriteLog("amd ajustes: falhou | " + id + " | " + failure.Message); }
                    else if (after == null) FinishFeedback(notice, "A alteração foi enviada, mas não foi possível confirmar: " + name +
                        ". Confira o resultado no AMD Software.", FeedbackKind.Warning);
                    else if (!applied(after)) FinishFeedback(notice, "O driver da AMD não aplicou: " + name + ". Um perfil do Adrenalin ou outro ajuste pode impedir.",
                        FeedbackKind.Warning);
                    else
                    {
                        string effects = TuningSideEffects(before, after, TuningKey(id));
                        FinishFeedback(notice, done + effects, effects.Length > 0 ? FeedbackKind.Warning : FeedbackKind.Success);
                    }
                    if (panel == AmdTuningPanel && window.IsVisible) RebuildAmdTuning(id == "resetcache" ? "amd-reset" : "amd-tune-" + id);
                };
            });
        }
        bool AmdTuningIdle()
        {
            if (amdTuningPending == null) return true;
            const string text = "Aguarde o ajuste anterior terminar.";
            WriteLog(text); RecordHistory(AmdTuningScope, text, FeedbackKind.Info); return false;
        }
        void ToggleAmdTuning(string key)
        {
            var f = amdTuning == null ? null : amdTuning.Find(key);
            if (f == null || !f.Supported || f.Error != 0 || !AmdTuningIdle()) return;
            bool expect = !f.Enabled; string name = TuningFull(key); var set = tuningSet;
            WriteLog("amd ajustes: " + key + " -> " + (expect ? "ligado" : "desligado"));
            RunAmdChange(key, (expect ? "Ligando: " : "Desligando: ") + name + "…", name, delegate { set(key, expect); },
                s => { var now = s.Find(key); return now != null && now.Enabled == expect; }, name + ": " + TuningOn(key, expect) + ".");
        }
        void ApplyAmdValue(string id, int value)
        {
            string key = TuningKey(id), field = TuningField(id);
            var f = amdTuning == null ? null : amdTuning.Find(key);
            if (f == null || !f.Supported || f.Error != 0 || !AmdTuningIdle()) return;
            string name = TuningFieldFull(id), text = TuningValueText(id, value, f); var set = tuningValue;
            WriteLog("amd ajustes: " + id + " -> " + value);
            RunAmdChange(id, "Aplicando: " + name + "…", name, delegate { set(key, field, value); },
                s => { var now = s.Find(key); return now != null && now.Get(field) == value; }, name + ": " + text + ".");
        }
        // O driver liga/desliga dependencias sozinho (por exemplo, RSR e nitidez); a mensagem conta o que mudou junto.
        static string TuningSideEffects(GraphicsState before, GraphicsState after, string key)
        {
            if (before == null || after == null) return "";
            var changed = new List<string>();
            foreach (var toggle in AdlxGraphics.Toggles)
            {
                if (toggle == key) continue;
                var b = before.Find(toggle); var a = after.Find(toggle);
                if (a != null && b != null && a.Enabled != b.Enabled) changed.Add((a.Enabled ? "ligou " : "desligou ") + TuningFull(toggle));
            }
            return changed.Count == 0 ? "" : " O driver também " + string.Join(" e ", changed.ToArray()) + ".";
        }

        // Seletor: abre a lista flutuante com o foco na opcao atual; escolher aplica uma vez.
        void ToggleAmdChoice(string id)
        {
            var f = amdTuning == null ? null : amdTuning.Find(TuningKey(id));
            var options = TuningOptions(id, f);
            if (TuningStateText(f) != null || options == null || f.Get(TuningField(id)) < 0 || !AmdTuningIdle()) return;
            if (amdTuningOpen != null) MotionCloseFlyout();
            bool open = amdTuningOpen != id; amdTuningOpen = open ? id : null;
            // Foco na opcao atual; valor fora da lista (nivel ainda nao escolhido) comeca pela primeira.
            int current = f.Get(TuningField(id));
            int focus = Array.IndexOf(options.Value.Key, current) >= 0 ? current : options.Value.Key[0];
            RebuildAmdTuning(open ? "amd-tune-" + id + "-opt-" + focus : "amd-tune-" + id);
            ActionSound(open ? "lista" : "voltar");
        }
        void ChooseAmdValue(string id, int value)
        {
            MotionCloseFlyout(); amdTuningOpen = null;
            var f = amdTuning == null ? null : amdTuning.Find(TuningKey(id));
            if (f == null || f.Get(TuningField(id)) == value || !AmdTuningIdle()) { RebuildAmdTuning("amd-tune-" + id); return; }
            ApplyAmdValue(id, value);
        }
        void QueueAmdSlider(string id, int value)
        {
            amdSliderId = id; amdSliderValue = value;
            amdSliderTimer.Stop(); amdSliderTimer.Start();
        }
        // Aplica o ultimo valor do slider; com outro ajuste em andamento, tenta de novo no proximo intervalo.
        void FlushAmdSlider()
        {
            if (amdSliderId == null || disposed) { amdSliderTimer.Stop(); return; }
            if (amdTuningPending != null) return;
            amdSliderTimer.Stop();
            string id = amdSliderId; int value = amdSliderValue; amdSliderId = null;
            var f = amdTuning == null ? null : amdTuning.Find(TuningKey(id));
            if (f != null && f.Get(TuningField(id)) == value) return;
            ApplyAmdValue(id, value);
        }
        void RequestAmdReset() { if (!AmdTuningIdle()) return; amdResetConfirm = true; RebuildAmdTuning("amd-reset-cancel"); }
        void CancelAmdReset() { amdResetConfirm = false; RebuildAmdTuning("amd-reset"); }
        void ConfirmAmdReset()
        {
            amdResetConfirm = false;
            if (!AmdTuningIdle()) { RebuildAmdTuning("amd-reset"); return; }
            var reset = tuningReset; WriteLog("amd ajustes: redefinir cache do sombreador");
            RunAmdChange("resetcache", "Redefinindo o cache do sombreador…", "Redefinir cache do sombreador", delegate { reset(); }, s => true,
                "Cache do sombreador redefinido. Os jogos recompilam os shaders na próxima abertura.");
        }
        // ○ fecha a lista aberta ou cancela a confirmacao antes de voltar ao trilho.
        bool AmdTuningBack()
        {
            if (panel != AmdTuningPanel) return false;
        if (amdTuningOpen != null) { string id = amdTuningOpen; MotionCloseFlyout(); amdTuningOpen = null; RebuildAmdTuning("amd-tune-" + id); return true; }
            if (amdResetConfirm) { CancelAmdReset(); return true; }
            return false;
        }
        // Lista aberta: o foco circula entre o seletor e as opcoes. Sliders andam pelo passo do driver; segurar acelera o FPS.
        bool NavigateAmdTuning(Control focused, string key)
        {
            if (panel != AmdTuningPanel) return false;
            var slider = focused as Slider; string tag = focused == null ? null : focused.Tag as string;
            if (slider != null && tag != null && tag.StartsWith("amd-tune-", StringComparison.Ordinal) && (key == "ESQUERDA" || key == "DIREITA"))
            {
                var now = DateTime.UtcNow;
                amdSliderRun = (now - amdSliderLast).TotalMilliseconds < 250 ? amdSliderRun + 1 : 0; amdSliderLast = now;
                bool fps = TuningUnit(tag.Substring(9)) == " FPS";
                double step = slider.SmallChange * (fps && amdSliderRun >= 12 ? 10 : fps && amdSliderRun >= 4 ? 5 : 1);
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value + (key == "DIREITA" ? step : -step)));
                return true;
            }
            if (amdTuningOpen == null) return false;
            if (key == "ESQUERDA" || key == "DIREITA") return true;
            string selector = "amd-tune-" + amdTuningOpen;
            var ring = actions.FindAll(c => { var t = c.Tag as string; return t == selector || t != null && t.StartsWith(selector + "-opt-",
                StringComparison.Ordinal); });
            int pos = ring.IndexOf(focused);
            if (pos < 0) return false;
            ring[(pos + (key == "CIMA" ? -1 : 1) + ring.Count) % ring.Count].Focus();
            return true;
        }

        FrameworkElement AmdLogoIcon(double size, Brush fill)
        {
            var path = new Path { Data = Geometry.Parse(AmdLogo), Stretch = Stretch.Uniform, Fill = fill };
            return new Viewbox { Width = size, Height = size, Child = path };
        }
        void BuildAmdTuningPanel()
        {
            amdOpenRendered = false;
            var s = amdTuning;
            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 10) };
            var logo = AmdLogoIcon(14, Brush("TextPrimary")); logo.Margin = new Thickness(0, 0, 9, 0); logo.VerticalAlignment = VerticalAlignment.Center;
            status.Children.Add(logo);
            status.Children.Add(Text(s != null ? s.Gpu.Replace("AMD ", "") + " · ajustes globais" : amdTuningError != null ? "Driver da AMD indisponível" :
                "Lendo o driver da AMD…", 15, "Muted"));
            body.Children.Add(status);
            if (s != null && amdTuningError != null)
                ConnectionNote("Não foi possível atualizar os ajustes da AMD; os valores abaixo são da última leitura. " + amdTuningError);
            if (s == null)
            {
                if (amdTuningError != null) ConnectionNote(amdTuningError);
            }
            else
            {
                foreach (var group in AmdTuningGroups)
                {
                    Caption(group[0]);
                    for (int i = 1; i < group.Length; i++) AmdTuningRow(s, group[i], false);
                }
                Caption("MANUTENÇÃO");
                AmdResetRow(s.Find("resetcache"));
                ConnectionNote("Ajustes globais do AMD Software. Perfis por jogo do Adrenalin têm prioridade; sincronização, anti-al" +
                    "iasing, filtragem e tesselação valem para jogos abertos depois da mudança.");
                ConnectionNote("Filtragem de textura, formato de superfície, buffer triplo OpenGL, 10 bits e FSR OTA ficam só no AMD Software.");
            }
            if (!amdOpenRendered) amdTuningOpen = null; // o recurso da lista aberta sumiu (desligado pelo driver)
            var software = ConnectionButton(body, "Abrir o AMD Software", "↗", "amd-tuning-software",
                delegate { OpenAmdTarget(System.IO.Path.Combine(amdTuningFolder ?? "", "RadeonSoftware.exe"), "o AMD Software"); });
            software.IsEnabled = amdTuningFolder != null;
            amdTuningSignature = TuningSignature();
        }
        static string TuningStateText(GraphicsFeature f) { return f == null || f.Error != 0 ? "Indisponível" : !f.Supported ? "Não suportado" : null; }
        void AmdTuningRow(GraphicsState s, string id, bool sub)
        {
            string field = TuningField(id); var f = s.Find(TuningKey(id));
            if (field == null)
            {
                if (sub && (f == null || f.Error != 0 || !f.Supported)) return; // subopcao de outro recurso sem suporte: some
                AmdToggleRow(f, id);
            }
            else if (TuningOptions(id, f) != null) AmdChoiceRow(f, id);
            else AmdSliderRow(f, id);
            foreach (var child in TuningChildren(id, f)) AmdTuningRow(s, child, true);
        }
        void AmdToggleRow(GraphicsFeature f, string key)
        {
            string state = TuningStateText(f) ?? (amdTuningPending == key ? "Aplicando…" : f.Enabled ? "Ativado" : "Desativado");
            var row = ToggleSetting(body, TuningName(key), TuningStateText(f) != null ? (bool?)null : f.Enabled, "amd-tune-" + key, delegate { ToggleAmdTuning(key);
                }, state);
            row.IsEnabled = TuningStateText(f) == null;
        }
        void AmdChoiceRow(GraphicsFeature f, string id)
        {
            string tag = "amd-tune-" + id, title = TuningFieldTitle(id); int current = f == null ? -1 : f.Get(TuningField(id));
            string unusable = TuningStateText(f) ?? (current < 0 ? "Indisponível" : null);
            bool pending = amdTuningPending == id, open = amdTuningOpen == id && unusable == null && !pending;
            string value = unusable ?? (pending ? "Aplicando…" : TuningValueText(id, current, f));
        var row = ConnectionButton(body, title, value + (unusable == null && !pending ? (open ? "   ▴" : "   ▾") : ""), tag, delegate { ToggleAmdChoice(id); });
            row.ToolTip = title + " · " + value; System.Windows.Automation.AutomationProperties.SetName(row, title + " · " + value);
            row.IsEnabled = unusable == null; row.Opacity = row.IsEnabled ? 1 : .45;
            if (!open) return;
            amdOpenRendered = true;
            var options = TuningOptions(id, f).Value; var choices = new StackPanel();
            Flyout(body, choices, row);
            for (int i = 0; i < options.Key.Length; i++)
            {
                int v = options.Key[i];
                FlyoutChoice(PanelButton(choices, (v == current ? "✓  " : "    ") + options.Value[i], tag + "-opt-" + v, delegate { ChooseAmdValue(id, v); }));
            }
        }
        void AmdSliderRow(GraphicsFeature f, string id)
        {
            string field = TuningField(id), unit = TuningUnit(id), title = TuningFieldTitle(id);
            int min = f.Min, max = f.Max;
            if (id == "chill.min" && f.Get("max") >= min) max = Math.Min(max, f.Get("max")); // minimo nunca passa do maximo
            if (id == "chill.max" && f.Get("min") <= max) min = Math.Max(min, f.Get("min"));
            int value = Math.Max(min, Math.Min(max, amdSliderId == id ? amdSliderValue : f.Get(field)));
            bool pending = amdTuningPending == id;
            // Todas as linhas usam a mesma largura e as mesmas bordas internas; o template do slider ja acrescenta 6 DIP.
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 5) }; body.Children.Add(block);
            var label = new Grid { Margin = new Thickness(12, 0, 12, 0) }; block.Children.Add(label);
            label.ColumnDefinitions.Add(new ColumnDefinition()); label.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = Text(title, 16, "TextPrimary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
            var number = Text(pending ? "Aplicando…" : value + unit, 14, "Muted");
            number.Margin = new Thickness(12, 0, 0, 0); number.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(number, 1); label.Children.Add(name); label.Children.Add(number);
            int small = unit == "%" && f.Step == 1 ? 5 : f.Step; // porcentagem de 1 em 1 anda de 5 em 5, como o mixer
            var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = f.Step, IsSnapToTickEnabled = true, SmallChange = small,
                LargeChange = small * 10, Value = value, Height = 28, Margin = new Thickness(6, 0, 6, 0),
                Style = (Style)window.FindResource("VolumeSlider"), Tag = "amd-tune-" + id };
            string accessible = TuningFieldFull(id);
            System.Windows.Automation.AutomationProperties.SetName(slider, accessible); slider.ToolTip = accessible;
            slider.GotKeyboardFocus += delegate { block.BringIntoView(); };
            slider.ValueChanged += delegate {
                int v = (int)Math.Round(slider.Value);
                number.Text = v + unit; QueueAmdSlider(id, v);
            };
            block.Children.Add(slider); actions.Add(slider);
        }
        void AmdResetRow(GraphicsFeature f)
        {
            string unusable = TuningStateText(f);
            if (amdResetConfirm && unusable == null)
            {
                body.Children.Add(Text("Redefinir o cache do sombreador?", 21, "TextPrimary"));
            var note = Text("Os jogos recompilam os shaders na próxima abertura; os primeiros minutos podem ter engasgos. Nenhum " +"ajuste muda.", 15, "Muted");
                note.Margin = new Thickness(0, 4, 0, 10); body.Children.Add(note);
                Action("Cancelar", CancelAmdReset).Tag = "amd-reset-cancel";
                Action("Confirmar · Redefinir", ConfirmAmdReset).Tag = "amd-reset-confirm";
                return;
            }
            var row = ConnectionButton(body, "Redefinir cache do sombreador", unusable ?? (amdTuningPending == "resetcache" ? "Redefinindo…" : "↺"), "amd-reset",
                RequestAmdReset);
            row.IsEnabled = unusable == null; row.Opacity = row.IsEnabled ? 1 : .45;
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
