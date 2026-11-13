// Aba AMD (ADR 0081): captura do Radeon ReLive, gravacao, microfone do Windows e ultimas capturas. Design aprovado em 29/10.
// Estado e comandos em AmdActions.cs; as metricas da AMD ficam so na logica, porque repetem o painel Desempenho.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XGBP.Amd;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int AmdPanel = 8;
        readonly List<List<Control>> amdRows = new List<List<Control>>();
        TextBlock amdStatusText;
        Slider amdMicSlider;
        bool amdSyncing;

        static string AmdClock(TimeSpan t) { return (int)Math.Max(0, t.TotalMinutes) + ":" + Math.Max(0, t.Seconds).ToString("00"); }
        bool AmdOperational { get { var s = amdRelive; return s != null && s.Installed && s.Running && s.Enabled; } }
        string AmdStatus()
        {
            var s = amdRelive;
            if (s == null) return "Lendo o AMD Software…";
            if (!s.Installed) return "AMD Software não encontrado";
            if (!s.Running || !s.Enabled) return "Radeon ReLive desligado";
            if (amdRecording) return "Gravando · " + AmdClock(AmdElapsed());
            return s.InstantReplay ? "Repetição instantânea ativa · últimos " + AmdClock(TimeSpan.FromSeconds(s.ReplaySeconds)) : "Repetição instantânea desligada";
        }
        TimeSpan AmdElapsed() { return amdRecordingSince == DateTime.MinValue ? TimeSpan.Zero : amdNow() - amdRecordingSince; }

        void BuildAmdPanel()
        {
            amdRows.Clear(); amdSignature = AmdSignature(); amdMicSlider = null;
            var s = amdRelive; bool ready = AmdOperational;
            // Estado em uma linha: gravando em vermelho; senao, a repeticao instantanea que esta valendo.
            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 10) };
            status.Children.Add(new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center,
                Fill = amdRecording && ready ? Brush("CloseRed") : ready && s.InstantReplay ? Brush("AccentText") : Brush("Muted") });
            amdStatusText = Text(AmdStatus(), 15, amdRecording && ready ? "TextPrimary" : "Muted"); status.Children.Add(amdStatusText);
            body.Children.Add(status);

            if (s == null || s.Installed)
            {
                Caption("CAPTURA");
                bool replay = ready && s.InstantReplay;
                var main = new List<string[]> {
                    new[] { "amd-replay", "replay", "Salvar repetição", replay ? "Últimos " + AmdClock(TimeSpan.FromSeconds(s.ReplaySeconds)) : "Desligada" },
                    new[] { "amd-shot", "photo", "Captura de tela", "Foto da tela" },
                    new[] { "amd-record", amdRecording && ready ? "stop" : "record", amdRecording && ready ? "Parar" : "Gravar", amdRecording && ready ? "Encerra e salva a gravação" : "Vídeo" } };
                AmdTileRow(main, tag => tag == "amd-replay" ? replay : ready);
                // GIF e repeticao no jogo so quando ativados no Adrenalin.
                var extra = new List<string[]>();
                if (ready && s.InstantGif) extra.Add(new[] { "amd-gif", "gif", "Salvar GIF", "Instantâneo" });
                if (ready && s.InGameReplay) extra.Add(new[] { "amd-ingame", "ingame", "Repetição no jogo", "Mostrar agora" });
                if (extra.Count > 0) AmdTileRow(extra, tag => true);

                Caption("GRAVAÇÃO");
                AmdToggleRow(RadeonRelive.ToggleReplay, "Repetição instantânea", "ligada", "desligada", st => st.InstantReplay,
                    st => st.InstantReplay ? "Ativado · " + AmdClock(TimeSpan.FromSeconds(st.ReplaySeconds)) : "Desativado");
                AmdToggleRow(RadeonRelive.ToggleDesktop, "Gravar a área de trabalho", "ligada", "desligada", st => st.Desktop, null);
                AmdToggleRow(RadeonRelive.ToggleMicrophone, "Microfone na gravação", "ligado", "desligado", st => st.Microphone, null);
                if (AmdMicSilenced) // ADR 0107: o ReLive grava o microfone, mas o Windows esta no mudo
                {
                    var warn = Text("O microfone do Windows está mudo: a gravação sai sem a sua voz.", 14, "Critical");
                    warn.Margin = new Thickness(2, 0, 0, 8); warn.Tag = "amd-mic-warning"; body.Children.Add(warn);
                }
                AmdToggleRow(RadeonRelive.ToggleCamera, "Câmera na gravação", "ligada", "desligada", st => st.Camera, null);
                if (s != null && !ready) ConnectionNote("Ative Gravar e transmitir no AMD Software para usar a captura.");
            }
            else ConnectionNote("O AMD Software não foi encontrado neste PC. O microfone do Windows continua disponível.");

            Caption("MICROFONE DO WINDOWS");
            if (amdMic == null) ConnectionNote("Nenhum microfone ativo no Windows.");
            else AmdMicrophone(amdMic);

            if (s != null && s.Installed)
            {
                Caption("ÚLTIMAS CAPTURAS");
                var gallery = new Grid(); var galleryRow = new List<Control>();
                for (int i = 0; i < 3; i++) gallery.ColumnDefinitions.Add(new ColumnDefinition());
                for (int i = 0; i < Math.Min(3, amdCaptures.Count); i++)
                {
                    var card = AmdCaptureCard(amdCaptures[i]); card.Tag = "amd-capture-" + i;
                    card.Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 2 ? 0 : 4, 6);
                    Grid.SetColumn(card, i); gallery.Children.Add(card); galleryRow.Add(card);
                }
                if (galleryRow.Count > 0) { body.Children.Add(gallery); amdRows.Add(galleryRow); }
                else ConnectionNote("Nenhuma captura ainda.");
                amdRows.Add(new List<Control> { ConnectionButton(body, "Abrir pasta das capturas", "↗", "amd-folder", delegate { OpenAmdTarget(s.OutputFolder, "a pasta das capturas"); }) });
                var software = ConnectionButton(body, "Abrir o AMD Software", "↗", "amd-software", delegate { OpenAmdTarget(Path.Combine(s.InstallFolder ?? "", "RadeonSoftware.exe"), "o AMD Software"); });
                software.IsEnabled = s.InstallFolder != null; amdRows.Add(new List<Control> { software });
                ConnectionNote("Qualidade, atalhos e transmissão ficam no AMD Software. A barra se oculta antes de capturar.");
            }
        }

        void AmdTileRow(List<string[]> specs, Func<string, bool> enabled)
        {
            var grid = new Grid(); var row = new List<Control>();
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i]; string tag = spec[0];
                var tile = AmdTile(spec[1], spec[2], spec[3]);
                tile.Tag = tag; tile.IsEnabled = enabled(tag);
                tile.Opacity = tile.IsEnabled ? 1 : .4;
                tile.Click += delegate { InvokeSoundAction(tile, delegate { AmdTileAction(tag); }); };
                tile.Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 2 ? 0 : 4, 6);
                Grid.SetColumn(tile, i); grid.Children.Add(tile); row.Add(tile);
            }
            body.Children.Add(grid); amdRows.Add(row);
        }
        void AmdTileAction(string tag) { AmdCaptureAction(tag); }
        Button AmdTile(string iconKind, string label, string detail)
        {
            var icon = RecordingIcon(iconKind, 32);
            var button = new Button { Content = icon, Style = (Style)window.FindResource("Action"), MinHeight = 72, Padding = new Thickness(12),
                HorizontalContentAlignment = HorizontalAlignment.Center };
            button.ToolTip = label + " · " + detail;
            ToolTipService.SetShowOnDisabled(button, true);
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, detail);
            WireMotion(button);
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            actions.Add(button);
            return button;
        }
        void AmdToggleRow(string command, string label, string on, string off, Func<ReliveState, bool> field, Func<ReliveState, string> describe)
        {
            var s = amdRelive; bool pending = amdToggle != null && amdToggle.Command == command;
            string detail = s == null ? "Lendo…" : pending ? "Aplicando…" : describe != null ? describe(s) : null;
            var row = ToggleSetting(body, label, s == null ? (bool?)null : field(s), TagOf(command), delegate { AmdToggleAction(command, label, on, off, field); }, detail);
            row.IsEnabled = AmdOperational && amdToggle == null;
            amdRows.Add(new List<Control> { row });
        }
        void AmdMicrophone(MicrophoneState mic)
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 6) }; body.Children.Add(block);
            var label = new DockPanel { Margin = new Thickness(0, 0, 0, 3) }; block.Children.Add(label);
            int volume = amdMicVolume >= 0 ? amdMicVolume : mic.Volume;
            var value = Text(mic.Muted ? "Mudo" : volume + "%", 14, mic.Muted ? "Critical" : "Muted"); value.MinWidth = 44; value.TextAlignment = TextAlignment.Right;
            DockPanel.SetDock(value, Dock.Right); label.Children.Add(value);
            var name = Text(mic.Name, 15, "TextPrimary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; label.Children.Add(name);
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); block.Children.Add(row);
            var mute = new Button { Width = 34, Height = 34, Style = (Style)window.FindResource("MuteButton"), Tag = "amd-mic-mute", Content = RecordingIcon(mic.Muted ? "microphone-muted" : "microphone", 18) };
            System.Windows.Automation.AutomationProperties.SetName(mute, mic.Muted ? "Ativar o microfone" : "Silenciar o microfone");
            WireMotion(mute);
            mute.Click += delegate { InvokeSoundAction(mute, ToggleMicMute); };
            mute.GotKeyboardFocus += delegate { block.BringIntoView(); };
            row.Children.Add(mute); actions.Add(mute);
            amdSyncing = true;
            var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Height = 34, Value = volume,
                Margin = new Thickness(6, 0, 0, 0), Style = (Style)window.FindResource("VolumeSlider"), Tag = "amd-mic-volume" };
            amdSyncing = false;
            System.Windows.Automation.AutomationProperties.SetName(slider, mic.Name);
            slider.GotKeyboardFocus += delegate { block.BringIntoView(); };
            slider.ValueChanged += delegate {
                if (amdSyncing) return;
                int v = (int)Math.Round(slider.Value);
                if (!mic.Muted) value.Text = v + "%";
                QueueMicVolume(v);
            };
            Grid.SetColumn(slider, 1); row.Children.Add(slider); actions.Add(slider); amdMicSlider = slider;
            amdRows.Add(new List<Control> { mute, slider });
        }
        Button AmdCaptureCard(ReliveCapture c)
        {
            var stack = new StackPanel();
            var thumb = new Grid { Height = 72, ClipToBounds = true };
            ImageSource image; amdThumbs.TryGetValue(ThumbKey(c), out image);
            int hash = (c.Game ?? "").GetHashCode();
            var tint = Color.FromRgb((byte)(60 + (hash & 0x3F)), (byte)(70 + ((hash >> 6) & 0x3F)), (byte)(90 + ((hash >> 12) & 0x3F)));
            thumb.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = image != null ? (Brush)new ImageBrush(image) { Stretch = Stretch.UniformToFill } :
                new LinearGradientBrush(tint, Color.FromRgb((byte)(tint.R / 3), (byte)(tint.G / 3), (byte)(tint.B / 3)), 35) });
            var badge = new Border { Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 5, 5) };
            var badgeText = new StackPanel { Orientation = Orientation.Horizontal };
            var kind = RecordingIcon(c.Video ? "video" : "photo", 13); badgeText.Children.Add(kind);
            if (c.Video && c.Length.HasValue) { kind.Margin = new Thickness(0, 0, 4, 0); badgeText.Children.Add(Text(AmdClock(c.Length.Value), 12, "TextPrimary")); }
            badge.Child = badgeText; thumb.Children.Add(badge);
            stack.Children.Add(thumb);
            var game = Text(c.Game, 13, "TextPrimary"); game.TextWrapping = TextWrapping.NoWrap; game.TextTrimming = TextTrimming.CharacterEllipsis; game.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(game);
            stack.Children.Add(Text(c.Modified.ToString("dd/MM HH:mm"), 12, "Muted"));
            var button = new Button { Content = stack, Style = (Style)window.FindResource("Action"), Padding = new Thickness(6), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            string label = (c.Video ? (c.Replay ? "Repetição de " : "Vídeo de ") : "Foto de ") + c.Game;
            System.Windows.Automation.AutomationProperties.SetName(button, label + " · " + c.Modified.ToString("dd/MM HH:mm"));
            WireMotion(button);
            button.Click += delegate { InvokeSoundAction(button, delegate { OpenAmdTarget(c.Path, label); }); };
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            actions.Add(button);
            return button;
        }

        // Reconstroi so o conteudo, preservando rolagem e foco no painel (o foco no trilho fica onde esta).
        void RebuildAmd(string focusTag)
        {
            if (panel != AmdPanel || amdMicSlider != null && amdMicSlider.IsMouseCaptureWithin) return;
            var focused = Keyboard.FocusedElement as Control; bool inside = focused != null && actions.Contains(focused);
            string tag = focusTag ?? (inside ? focused.Tag as string : null);
            var scroll = (ScrollViewer)window.FindName("BodyScroll"); double offset = scroll.VerticalOffset;
            CancelElementMotion(body); body.Children.Clear(); actions.Clear(); flyout = null; flyoutSelector = null; panelFill = null;
            BuildAmdPanel();
            window.UpdateLayout(); scroll.ScrollToVerticalOffset(offset);
            if (!inside && focusTag == null) return;
            var target = tag == null ? null : actions.Find(c => (string)c.Tag == tag && c.IsEnabled);
            if (target == null) target = actions.Find(c => c.IsEnabled);
            if (target != null) target.Focus();
        }
        // O cronometro da gravacao anda sem reconstruir o painel.
        void AmdClockTick()
        {
            if (panel != AmdPanel || !window.IsVisible || !amdRecording) return;
            if (amdStatusText != null) amdStatusText.Text = AmdStatus();
        }
        // Linhas: CIMA/BAIXO entre linhas, ESQUERDA/DIREITA dentro da linha; a primeira coluna volta ao trilho.
        bool NavigateAmd(Control focused, string key)
        {
            int r = amdRows.FindIndex(line => line.Contains(focused)); if (r < 0) return false;
            var row = amdRows[r]; int c = row.IndexOf(focused);
            if (focused is Slider && (key == "ESQUERDA" || key == "DIREITA")) return false;
            if (key == "ESQUERDA") { if (c > 0) row[c - 1].Focus(); else FocusNavigation(RailIndex(panel)); return true; }
            if (key == "DIREITA") { for (int n = c + 1; n < row.Count; n++) if (row[n].IsEnabled && row[n].Focus()) break; return true; }
            int step = key == "CIMA" ? -1 : key == "BAIXO" ? 1 : 0; if (step == 0) return false;
            for (int n = r + step; n >= 0 && n < amdRows.Count; n += step)
            {
                var target = amdRows[n][Math.Min(c, amdRows[n].Count - 1)];
                if (target.IsEnabled && target.Focus()) return true;
                var any = amdRows[n].Find(x => x.IsEnabled); if (any != null && any.Focus()) return true;
            }
            return true;
        }
    }
}
