// Painel funcional de som. COM roda em workers MTA; a UI recebe apenas dados gerenciados.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class AudioBinding
        {
            public string Key, DeviceId;
            public string[] SessionIds;
            public Slider Slider;
            public TextBlock Value;
            public Button Mute;
            public bool Muted;
            public string MediaKey;
        }
        sealed class AudioAppGroup
        {
            public string Key, Name, IconPath;
            public readonly List<string> Sessions = new List<string>();
        }
        readonly DispatcherTimer audioTimer = new DispatcherTimer();
        readonly Dictionary<string, AudioChange> audioPending = new Dictionary<string, AudioChange>();
        readonly Dictionary<string, AudioChange> audioInFlight = new Dictionary<string, AudioChange>();
        long audioRevision;
        readonly List<AudioBinding> audioBindings = new List<AudioBinding>();
        AudioSnapshot audioSnapshot;
        string audioLayout;
        int audioExpanded = -1, audioColumn;
        ScrollViewer audioAppsScroll;
        readonly List<Control[]> audioRows = new List<Control[]>();
        readonly Dictionary<string, System.Windows.Media.ImageSource> audioIcons = new Dictionary<string, System.Windows.Media.ImageSource>(StringComparer.OrdinalIgnoreCase);
        bool audioBusy, audioSync, audioBaselineWritten, audioInitial = true;
        DateTime audioNextRead = DateTime.MinValue;
        Action<AudioChange> audioWrite = CoreAudio.Apply;
        Func<AudioSnapshot> audioRead = CoreAudio.Read;

        void StartAudio()
        {
#if XGB_VERIFY
            if (verify) { audioSnapshot = ExampleAudio(); return; }
#endif
            audioTimer.Interval = TimeSpan.FromMilliseconds(150);
            audioTimer.Tick += delegate { AudioTick(); };
            audioTimer.Start(); AudioTick();
        }
        void AudioTick()
        {
            if (audioBusy || disposed) return;
            // A Home tambem le o mixer enquanto o player mostra uma fonte, para o volume do app.
            bool wanted = window.IsVisible && (panel == 6 || panel == 0 && mediaShown != null);
            if (audioPending.Count == 0 && !audioInitial && ((!wanted && audioInFlight.Count == 0) || DateTime.UtcNow < audioNextRead)) return;
            audioBusy = true;
            audioInitial = false;
            var batch = new List<AudioChange>(audioPending.Values); audioPending.Clear();
            foreach (var change in batch) audioInFlight[change.Key] = change;
            long readRevision = audioRevision;
            Task.Factory.StartNew(delegate {
                AudioSnapshot snapshot = null; string error = null;
                var failed = new List<AudioChange>();
                var failures = new List<Tuple<AudioChange, string>>();
                try
                {
                    foreach (var change in batch)
                    {
                        try { audioWrite(change); WriteLog("audio aplicado: " + change.Kind + " | " + change.DeviceId + " | nivel=" + change.Level + " | mute=" + change.Muted); }
                        catch (Exception ex) { error = ex.Message; failed.Add(change); failures.Add(Tuple.Create(change, ex.Message)); WriteLog("audio falhou: " + change.Kind + " | " + ex); }
                    }
                    snapshot = audioRead();
                    if (!audioBaselineWritten)
                    {
                        File.WriteAllText(Path.Combine(Output, "audio-inicial-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), snapshot.Describe());
                        audioBaselineWritten = true;
                    }
                }
                catch (Exception ex) { error = ex.Message; WriteLog("audio leitura falhou: " + ex); }
                Dispatch(delegate {
                    audioBusy = false; audioNextRead = DateTime.UtcNow.AddSeconds(2);
                    if (snapshot != null) audioSnapshot = snapshot;
                    CompleteAudioRead(snapshot != null, readRevision, failed);
                    foreach (var failure in failures)
                    {
                        var selected = MediaSelected();
                        if (failure.Item1.MediaKey == null || selected != null && selected.Key == failure.Item1.MediaKey)
                            NewFeedback("Áudio: " + failure.Item2, FeedbackKind.Error, failure.Item1.FeedbackScope);
                    }
                    if (panel == 6 && window.IsVisible)
                    {
                        if (snapshot != null) RefreshAudioView();
                        if (error != null && failed.Count == 0) Feedback("Áudio: " + error, FeedbackKind.Error);
                    }
                    else if (panel == 0 && window.IsVisible)
                    {
                        if (snapshot != null) SyncMediaVolume();
                        if (error != null && failed.Count == 0 && batch.Count > 0) Feedback("Áudio: " + error, FeedbackKind.Error);
                    }
                });
            });
        }

        List<AudioAppGroup> AudioApps()
        {
            var groups = new List<AudioAppGroup>();
            if (audioSnapshot == null) return groups;
            foreach (var session in audioSnapshot.Sessions)
            {
                // Uma linha por aplicativo, inclusive quando ele tem sessoes em varias saidas.
                // GUID_NULL nunca agrupa aplicativos diferentes; nomes de exibicao nao sao identidade.
                string identity = session.AppIdentity ?? "pid:" + session.ProcessId;
                string key = session.SystemSounds ? "system" : "app:" + identity;
                var matching = groups.FindAll(g => g.Sessions.Exists(id => {
                    var other = audioSnapshot.Sessions.Find(s => s.Id == id);
                    return other != null && (session.SystemSounds && other.SystemSounds || !session.SystemSounds && !other.SystemSounds &&
                        (string.Equals(identity, other.AppIdentity ?? "pid:" + other.ProcessId, StringComparison.OrdinalIgnoreCase) ||
                         session.Grouping != Guid.Empty && session.Grouping == other.Grouping));
                }));
                var group = matching.Count == 0 ? null : matching[0];
                if (group == null)
                {
                    group = new AudioAppGroup { Key = key, Name = session.AppName, IconPath = session.IconPath };
                    groups.Add(group);
                }
                for (int i = 1; i < matching.Count; i++) { group.Sessions.AddRange(matching[i].Sessions); groups.Remove(matching[i]); }
                group.Sessions.Add(session.Id);
            }
            groups.Sort((a, b) => a.Key == "system" ? (b.Key == "system" ? 0 : -1) : b.Key == "system" ? 1 :
                string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return groups;
        }
        string AudioLayoutKey()
        {
            if (audioSnapshot == null) return "loading";
            string key = audioExpanded + "|" + audioSnapshot.Defaults[0, 1] + "|" + audioSnapshot.Defaults[1, 1];
            foreach (var d in audioSnapshot.Devices) key += "|" + d.Id + d.Name + d.Available;
            foreach (var g in AudioApps()) key += "|" + g.Key + g.Name + g.IconPath + string.Join(",", g.Sessions.ToArray());
            return key;
        }
        void RefreshAudioView()
        {
            if (AudioEditing()) return;
            if (AudioLayoutKey() != audioLayout) RebuildAudioView();
            else { SyncAudioBindings(); SyncMediaVolume(); }
        }
        bool AudioEditing()
        {
            // Uma reconstrucao durante o arraste perderia captura/foco e poderia aplicar outro valor.
            return audioBindings.Exists(b => b.Slider.IsMouseCaptureWithin) || audioPending.Count > 0 || audioInFlight.Count > 0 ||
                MediaInteracting() ||
                mediaVolume != null && mediaVolume.IsMouseCaptureWithin || mediaProgress != null && mediaProgress.IsMouseCaptureWithin;
        }
        void RebuildAudioView()
        {
            if (AudioEditing()) return;
            var focused = Keyboard.FocusedElement as Control;
            string tag = focused == null ? null : focused.Tag as string;
            bool contentFocus = focused != null && actions.Contains(focused);
            double offset = audioAppsScroll == null ? 0 : audioAppsScroll.VerticalOffset;
            BuildAudioPanel(false);
            if (contentFocus) FocusAudioTag(tag);
            if (audioAppsScroll != null) { window.UpdateLayout(); audioAppsScroll.ScrollToVerticalOffset(offset); }
            if (contentFocus)
            {
                var target = actions.Find(c => (string)c.Tag == tag);
                if (target != null) target.BringIntoView();
            }
        }
        void FocusAudioTag(string tag)
        {
            var target = actions.Find(c => (string)c.Tag == tag && c.IsEnabled);
            if (target != null) target.Focus(); else FocusFirstAction();
        }
        Button AudioButton(Panel parent, string label, string tag, Action callback)
        {
            var button = PanelButton(parent, label, tag, callback);
            audioRows.Add(new Control[] { button });
            return button;
        }
        void ToggleAudioDevices(int flow)
        {
            if (audioExpanded >= 0) MotionCloseFlyout();
            audioExpanded = audioExpanded == flow ? -1 : flow;
            ActionSound(audioExpanded >= 0 ? "lista" : "voltar");
            BuildAudioPanel(false); FocusAudioTag(flow == 0 ? "output" : "input");
            audioNextRead = DateTime.MinValue;
        }
        bool AudioBack()
        {
            if (panel != 6 || audioExpanded < 0) return false;
            int flow = audioExpanded; MotionCloseFlyout(); audioExpanded = -1; BuildAudioPanel(false);
            FocusAudioTag(flow == 0 ? "output" : "input"); return true;
        }
        void OpenWindowsAudio()
        {
            try { if (!verify) LaunchSteamProtocol("ms-settings:sound"); Hide(); }
            catch (Exception ex) { Feedback("Não foi possível abrir Som do Windows: " + ex.Message, FeedbackKind.Error); }
        }
        void AudioCaption(Panel parent, string label)
        {
            var text = Text(label, 12, "Muted"); text.FontWeight = FontWeights.SemiBold;
            text.Margin = new Thickness(0, 8, 0, 6); parent.Children.Add(text);
        }
        void AddDeviceSelector(Panel parent, int flow)
        {
            var selected = audioSnapshot.Default(flow);
            string fallback = flow == 0 ? "Escolher saída de som" : "Escolher microfone";
            string label = (selected == null ? fallback : selected.Name) + (audioExpanded == flow ? "   ▴" : "   ▾");
            var selector = AudioButton(parent, label, flow == 0 ? "output" : "input", delegate { ToggleAudioDevices(flow); });
            if (audioExpanded != flow) return;
            var choices = new StackPanel();
            Flyout(parent, choices, selector);
            foreach (var device in audioSnapshot.Devices)
            {
                if (device.Flow != flow) continue;
                var d = device;
                var b = AudioButton(choices, (selected != null && d.Id == selected.Id ? "✓  " : "    ") + d.Name, "pick-" + d.Id, delegate {
                    QueueAudio(new AudioChange { Kind = "default", DeviceId = d.Id, Flow = flow, Key = "default-" + flow });
                    AudioBack(); Feedback("Aplicando " + d.Name + "…");
                });
                FlyoutChoice(b);
            }
            if (!audioSnapshot.Devices.Exists(d => d.Flow == flow)) AudioCaption(choices, "Nenhum dispositivo conectado.");
        }
        void BuildAudioPanel(bool reset)
        {
            CancelElementMotion(body);
            if (reset) audioExpanded = -1;
            body.Children.Clear(); actions.Clear(); audioBindings.Clear(); audioRows.Clear(); audioAppsScroll = null; panelFill = null;
            flyout = null; flyoutSelector = null; mediaHost = null;
            audioLayout = AudioLayoutKey();
            ((TextBlock)window.FindName("PanelTitle")).Text = "Som";
            // Reconstruir apos aplicar uma troca encerra o aviso de aplicacao anterior.
            ClearFeedback(true);
            if (audioSnapshot == null)
            {
                Caption("Lendo dispositivos de áudio…"); AudioButton(body, "Abrir Som do Windows", "windows", OpenWindowsAudio); return;
            }
            var outer = (ScrollViewer)window.FindName("BodyScroll");
            outer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            // Altura do conteudo ate o limite da area segura; acima dele, so o mixer rola.
            var layout = new Grid { MaxHeight = panelBodyLimit };
            panelFill = layout;
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.Children.Add(layout);
            var devices = new StackPanel(); layout.Children.Add(devices);
            if (audioExpanded >= 0) Panel.SetZIndex(devices, 10); // a lista do microfone passa por cima do mixer
            // O player de midia fica no topo do Som, acima da saida de som.
            devices.Children.Add(CreateMediaCard()); mediaHost.Margin = new Thickness(0, 0, 0, 2);
            foreach (var row in MediaRows()) if (row.Count > 0) { audioRows.Add(row.ToArray()); actions.AddRange(row); }
            AudioCaption(devices, "SAÍDA DE SOM"); AddDeviceSelector(devices, 0);
            var output = audioSnapshot.Default(0);
            AddAudioVolume(devices, "Volume do PC", "master", output == null ? null : output.Id, null, null);
            AudioCaption(devices, "MICROFONE"); AddDeviceSelector(devices, 1);
            var input = audioSnapshot.Default(1);
            AddAudioVolume(devices, "Volume do microfone", "mic", input == null ? null : input.Id, null, null);
            var groups = AudioApps();
            var heading = new DockPanel { Margin = new Thickness(0, 12, 0, 8) };
            Grid.SetRow(heading, 1); layout.Children.Add(heading);
            var count = Text(groups.Count.ToString(), 13, "Muted"); DockPanel.SetDock(count, Dock.Right); heading.Children.Add(count);
            heading.Children.Add(Text("Mixer de aplicativos", 18, "TextPrimary"));
            var apps = new StackPanel();
            audioAppsScroll = new ScrollViewer { Content = apps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Padding = new Thickness(0, 0, 6, 0) };
            Grid.SetRow(audioAppsScroll, 2); layout.Children.Add(audioAppsScroll);
            foreach (var group in groups) AddAudioVolume(apps, group.Name, "app-" + group.Key, null, group.Sessions.ToArray(), group.IconPath);
            if (groups.Count == 0) AudioCaption(apps, "Apps aparecem aqui ao abrir uma sessão de áudio.");
            var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; Grid.SetRow(bottom, 3); layout.Children.Add(bottom);
            AudioButton(bottom, "Abrir Som do Windows  ↗", "windows", OpenWindowsAudio);
            SyncAudioBindings();
        }
        System.Windows.Media.ImageSource AudioIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            System.Windows.Media.ImageSource cached;
            if (audioIcons.TryGetValue(path, out cached)) return cached;
            try
            {
                // Icones locais do programa; falha de acesso usa o simbolo generico.
                if (File.Exists(path)) using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon != null)
                    {
                        cached = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(24, 24));
                        cached.Freeze();
                    }
                }
            }
            catch { cached = null; }
            if (audioIcons.Count >= 128) audioIcons.Clear();
            audioIcons[path] = cached; return cached;
        }
        void AddAudioVolume(Panel parent, string title, string key, string deviceId, string[] sessionIds, string iconPath)
        {
            var binding = new AudioBinding { Key = key, DeviceId = deviceId, SessionIds = sessionIds };
            var block = new StackPanel { Margin = new Thickness(0, 3, 0, sessionIds == null ? 4 : 10) }; parent.Children.Add(block);
            var label = new DockPanel { Margin = new Thickness(0, 0, 0, 3) }; block.Children.Add(label);
            binding.Value = Text("—", 14, "Muted"); binding.Value.MinWidth = 44; binding.Value.TextAlignment = TextAlignment.Right;
            DockPanel.SetDock(binding.Value, Dock.Right); label.Children.Add(binding.Value);
            if (sessionIds != null)
            {
                var image = AudioIcon(iconPath);
                string fallbackIcon = key == "app-system" ? "M 2,7 L 6,7 11,3 11,17 6,13 2,13 Z M 14,6 Q 19,10 14,14" :
                    "M 2,2 L 8,2 8,8 2,8 Z M 12,2 L 18,2 18,8 12,8 Z M 2,12 L 8,12 8,18 2,18 Z M 12,12 L 18,12 18,18 12,18 Z";
                FrameworkElement icon = image == null ? (FrameworkElement)new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse(fallbackIcon), Stroke = Brush("Muted"), StrokeThickness = 1.3, Height = 20 } :
                    new Image { Source = image, Width = 20, Height = 20 };
                icon.Width = 20; icon.Margin = new Thickness(0, 0, 8, 0); icon.VerticalAlignment = VerticalAlignment.Center; label.Children.Add(icon);
            }
            var name = Text(title, 15, "TextPrimary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.ToolTip = title; label.Children.Add(name);
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); block.Children.Add(row);
            binding.Mute = new Button { Width = 34, Height = 34, Style = (Style)window.FindResource("MuteButton"), Tag = key + "-mute" };
            WireMotion(binding.Mute);
            binding.Mute.Click += delegate { InvokeSoundAction(binding.Mute, delegate { ToggleAudioMute(binding); }); };
            binding.Mute.GotKeyboardFocus += delegate { block.BringIntoView(); };
            row.Children.Add(binding.Mute); actions.Add(binding.Mute);
            binding.Slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Height = 34,
                Margin = new Thickness(6, 0, 0, 0), Style = (Style)window.FindResource("VolumeSlider"), Tag = key + "-volume" };
            System.Windows.Automation.AutomationProperties.SetName(binding.Slider, title);
            // So ajusta o volume: o mute fica no botao ao lado, nao no ✕/Enter do slider (ADR 0085).
            binding.Slider.ToolTip = title;
            binding.Slider.GotKeyboardFocus += delegate { block.BringIntoView(); };
            binding.Slider.ValueChanged += delegate {
                if (audioSync) return;
                MotionCancelVolume(binding.Slider);
                binding.Value.Text = Math.Round(binding.Slider.Value) + "%";
                QueueBindingAudio(binding, false);
            };
            Grid.SetColumn(binding.Slider, 1); row.Children.Add(binding.Slider); actions.Add(binding.Slider);
            audioRows.Add(new Control[] { binding.Mute, binding.Slider }); audioBindings.Add(binding);
        }
        void ToggleAudioMute(AudioBinding b)
        {
            if (!b.Mute.IsEnabled) return;
            b.Muted = !b.Muted; UpdateMuteLabel(b); QueueBindingAudio(b, true);
        }
        void QueueBindingAudio(AudioBinding b, bool mute)
        {
            if (b.MediaKey != null && (MediaSelected() == null || MediaSelected().Key != b.MediaKey)) return;
            if (b.SessionIds == null)
            {
                QueueAudio(new AudioChange { Kind = mute ? "endpoint-mute" : "endpoint-volume", DeviceId = b.DeviceId,
                    Muted = b.Muted, Level = (float)(b.Slider.Value / 100), Key = b.Key + (mute ? "-mute" : "-volume") }); return;
            }
            var targets = new Dictionary<string, List<string>>();
            foreach (var id in b.SessionIds)
            {
                var s = audioSnapshot.Sessions.Find(item => item.Id == id); if (s == null) continue;
                if (!targets.ContainsKey(s.DeviceId)) targets[s.DeviceId] = new List<string>();
                targets[s.DeviceId].Add(id);
            }
            foreach (var target in targets)
                QueueAudio(new AudioChange { Kind = mute ? "session-mute" : "session-volume", DeviceId = target.Key, SessionIds = target.Value.ToArray(),
                    Muted = b.Muted, Level = (float)(b.Slider.Value / 100), Key = b.Key + (mute ? "-mute" : "-volume") + "|" + target.Key, MediaKey = b.MediaKey });
        }
        void UpdateMuteLabel(AudioBinding b)
        {
            string target = b.Key == "mic" ? "microfone" : b.Key == "master" ? "som" : "aplicativo";
            string label = (b.Muted ? "Ativar " : "Silenciar ") + target;
            if ((string)b.Mute.ToolTip == label && b.Mute.Content != null) return;
            b.Mute.ToolTip = label; System.Windows.Automation.AutomationProperties.SetName(b.Mute, label);
            // Geometria propria para manter icones claros mesmo sem uma fonte de simbolos instalada.
            var icon = new Grid { Width = 20, Height = 20 };
            string shape = b.Key == "mic" ? "M 8,2 L 12,2 12,11 Q 10,14 8,11 Z M 5,9 L 5,11 Q 5,17 10,17 Q 15,17 15,11 L 15,9 M 10,17 L 10,20 M 7,20 L 13,20" :
                "M 2,7 L 6,7 11,3 11,17 6,13 2,13 Z";
            icon.Children.Add(new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse(shape), Stroke = Brush("TextPrimary"), StrokeThickness = 1.5 });
            if (b.Muted)
                icon.Children.Add(new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse("M 2,2 L 18,18"), Stroke = Brush("AccentText"), StrokeThickness = 2 });
            else if (b.Key != "mic")
                icon.Children.Add(new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse("M 14,6 Q 19,10 14,14"), Stroke = Brush("TextPrimary"), StrokeThickness = 1.5 });
            b.Mute.Content = icon; b.Mute.Background = Brush(b.Muted ? "SurfaceSelected" : "SurfaceCard");
            MotionReveal(icon, MotionFast);
        }
        bool NavigateAudio(Control focused, string key)
        {
            if (panel != 6) return false;
            if (NavigateMediaRow(focused, key)) return true;
            var pair = audioBindings.Find(b => b.Mute == focused);
            if (pair != null && key == "DIREITA") { pair.Slider.Focus(); return true; }
            if (key != "CIMA" && key != "BAIXO") return false;
            var rows = audioRows;
            if (audioExpanded >= 0)
            {
                // Com uma lista de dispositivos aberta, o foco circula nela ate escolher ou voltar.
                string selector = audioExpanded == 0 ? "output" : "input";
                rows = audioRows.FindAll(r => { var tag = r[0].Tag as string; return tag == selector || tag != null && tag.StartsWith("pick-", StringComparison.Ordinal); });
            }
            int pos = rows.FindIndex(r => Array.IndexOf(r, focused) >= 0);
            if (pos < 0) return false;
            // Slider leva a slider e botao a botao; as linhas do player lembram a coluna.
            int wanted = focused is Slider ? 1 : 0;
            if (MediaControl(focused)) { if (rows[pos].Length > 1) audioColumn = Array.IndexOf(rows[pos], focused); wanted = audioColumn; }
            for (int i = 1; i <= rows.Count; i++)
            {
                var target = NearestEnabled(rows[(pos + (key == "CIMA" ? -i : i) + rows.Count) % rows.Count], wanted);
                if (target != null && target.Focus()) break;
            }
            return true;
        }
        bool AudioHasPending(string key)
        {
            foreach (var pending in audioPending.Keys) if (pending == key || pending.StartsWith(key + "|", StringComparison.Ordinal)) return true;
            foreach (var pending in audioInFlight.Keys) if (pending == key || pending.StartsWith(key + "|", StringComparison.Ordinal)) return true;
            return false;
        }
        void SyncAudioBindings()
        {
            if (audioSnapshot == null) return;
            audioSync = true;
            try
            {
                foreach (var b in audioBindings)
                {
                    bool muted; float level; bool available = BindingAudioState(b, out level, out muted);
                    b.Slider.IsEnabled = b.Mute.IsEnabled = available;
                    b.Slider.Opacity = b.Mute.Opacity = available ? 1 : .4;
                    if (!available) { b.Value.Text = "—"; UpdateMuteLabel(b); continue; }
                    if (!b.Slider.IsMouseCaptureWithin)
                    { MotionVolume(b.Slider, Math.Round(level * 100)); b.Value.Text = Math.Round(level * 100) + "%"; }
                    b.Muted = muted; UpdateMuteLabel(b);
                }
            }
            finally { audioSync = false; }
        }
        void QueueAudio(AudioChange change)
        {
            if (change.DeviceId == null) return;
            change.Revision = ++audioRevision; change.QueuedAt = DateTime.UtcNow; change.FeedbackScope = FeedbackScope;
            CoalesceAudio(change);
            audioPending[change.Key] = change;
            RefreshPresentationTimers();
        }
    }
}
