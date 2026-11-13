// Player de midia da Home. Leitura do SMTC em worker; comandos somente por escolha do usuario.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Audio;
using XGBP.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        readonly DispatcherTimer mediaTimer = new DispatcherTimer();
        readonly List<Control> mediaTransportRow = new List<Control>(), mediaProgressRow = new List<Control>(), mediaOptionsRow = new List<Control>();
        List<MediaSessionState> mediaSessions;
        string mediaPinned, mediaShown, mediaLayout, mediaCoverKey, mediaError;
        bool mediaBusy, mediaSync;
        DateTime mediaNextRead = DateTime.MinValue, mediaSeekAt = DateTime.MaxValue;
        double mediaSeekTarget = -1;
        string mediaSeekSession, mediaSeekTrack;
        long mediaRevision;
        List<MediaSessionState> mediaDeferred;
        long mediaDeferredRevision;
        Slider mediaEditing;
        byte[] mediaCoverBytes;
        TextBlock mediaClockLabel;
        long mediaClockSecond;
        bool mediaClockKnown;
        MediaCommandQueue mediaCommands;
        const double MediaCoverSize = 46;
        Border mediaHost, mediaCover;
        TextBlock mediaTitle, mediaSubtitle, mediaElapsed, mediaTotal;
        Button mediaPrevious, mediaPlay, mediaNext, mediaShuffle, mediaRepeat, mediaSwitch, mediaMute;
        Slider mediaProgress, mediaVolume;
        AudioBinding mediaAudio;
        Func<List<MediaSessionState>> mediaRead = MediaSessions.Read;
        Action<MediaCommand> mediaSend = MediaSessions.Send;

        void StartMedia()
        {
            mediaCommands = new MediaCommandQueue(c => mediaSend(c), work => { if (verify) work(); else Task.Factory.StartNew(work); },
                done => { if (verify) done(); else Dispatch(done); });
#if XGB_VERIFY
            if (verify) { mediaSessions = ExampleMedia(); return; }
#endif
            mediaTimer.Interval = TimeSpan.FromMilliseconds(250);
            mediaTimer.Tick += delegate { MediaTick(); }; mediaTimer.Start();
        }
        void MediaTick()
        {
            if (disposed) return;
            if (!MediaInteracting() && mediaDeferred != null)
            {
                var deferred = mediaDeferred; mediaDeferred = null;
                if (mediaDeferredRevision == mediaRevision) { mediaSessions = deferred; RenderMedia(); }
            }
            if (mediaSeekTarget >= 0 && DateTime.UtcNow >= mediaSeekAt && (mediaProgress == null || !mediaProgress.IsMouseCaptureWithin)) SendSeek();
            if (!MediaPanel() || !window.IsVisible) return;
            UpdateMediaClock();
            if (!mediaBusy && (mediaCommands == null || !mediaCommands.Pending) && DateTime.UtcNow >= mediaNextRead) ReadMedia();
        }
        // O player aparece na Home e no topo do painel Som; os dois nunca estao visiveis juntos.
        bool MediaPanel() { return panel == 0 || panel == 6; }
        void ReadMedia()
        {
            mediaBusy = true;
            long revision = mediaRevision;
            Task.Factory.StartNew(delegate {
                List<MediaSessionState> found = null; string error = null;
                try { found = mediaRead(); } catch (Exception ex) { error = ex.Message; }
                DispatchPresentation(delegate { CompleteMediaRead(found, error, revision); });
            });
        }
        void CompleteMediaRead(List<MediaSessionState> found, string error, long revision)
        {
            mediaBusy = false; mediaNextRead = DateTime.UtcNow.AddSeconds(1);
            if (revision != mediaRevision) { mediaNextRead = DateTime.MinValue; return; }
            if (error != null && error != mediaError) WriteLog("midia: leitura falhou | " + error);
            mediaError = error;
            if (found != null && MediaInteracting()) { mediaDeferred = found; mediaDeferredRevision = revision; return; }
            if (found != null) mediaSessions = found;
            if (MediaPanel() && window.IsVisible) RenderMedia();
        }

        List<MediaSessionState> MediaChoices()
        {
            var list = new List<MediaSessionState>();
            if (mediaSessions != null) foreach (var s in mediaSessions)
                if (s.Error == null && s.Status != "closed" && (s.Title.Length > 0 || s.Playing)) list.Add(s);
            return list;
        }
        MediaSessionState MediaSelected()
        {
            // A fonte escolhida pelo usuario prevalece; sem escolha, segue a sessao atual do Windows.
            MediaSessionState current = null, playing = null, first = null;
            if (mediaSessions != null) foreach (var item in mediaSessions)
            {
                if (item.Error != null || item.Status == "closed" || item.Title.Length == 0 && !item.Playing) continue;
                if (item.Key == mediaPinned) return item;
                if (first == null) first = item;
                if (current == null && item.Current) current = item;
                if (playing == null && item.Playing) playing = item;
            }
            mediaPinned = null;
            return current ?? playing ?? first;
        }

        // Monta o cartao para a midia atual; o painel registra as tres linhas na sua navegacao.
        Border CreateMediaCard()
        {
            // Medidas compactas para caber na Home sem rolagem.
            mediaHost = new Border { Background = Brush("SurfaceCard"), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 7, 10, 6) };
            var media = MediaSelected();
            FillMediaCard(media);
            if (media != null) UpdateMedia(media); else mediaShown = null;
            return mediaHost;
        }
        void FillMediaCard(MediaSessionState media)
        {
            CancelElementMotion(mediaHost);
            foreach (var row in MediaRows()) row.Clear();
            mediaPrevious = mediaPlay = mediaNext = mediaShuffle = mediaRepeat = mediaSwitch = mediaMute = null;
            mediaProgress = mediaVolume = null; mediaAudio = null; mediaCoverKey = null; mediaCoverBytes = null; mediaShown = null;
            mediaLayout = MediaLayoutKey(media);
            mediaHost.Child = media == null ? EmptyMedia() : ActiveMedia(mediaLayout == "multi");
        }
        string MediaLayoutKey(MediaSessionState media)
        {
            return media == null ? "empty|" + (mediaSessions == null ? mediaError ?? "loading" : "none") : MediaChoices().Count > 1 ? "multi" : "single";
        }
        List<Control>[] MediaRows() { return new[] { mediaTransportRow, mediaProgressRow, mediaOptionsRow }; }
        void BuildMediaPlayer()
        {
            body.Children.Add(CreateMediaCard());
            foreach (var row in MediaRows()) { homeRows.Add(row); actions.AddRange(row); }
        }
        void RenderMedia()
        {
            if (!MediaPanel() || mediaHost == null) return;
            if (MediaInteracting()) return; // Nao substituir um controle capturado nem retargetar um gesto.
            var media = MediaSelected();
            if (MediaLayoutKey(media) != mediaLayout)
            {
                // No Som a estrutura inteira e refeita, como numa mudanca de dispositivos ou apps.
                if (panel == 6) { RebuildAudioView(); return; }
                RebuildMedia(media);
            }
            if (media != null) UpdateMedia(media); else mediaShown = null;
        }
        void RebuildMedia(MediaSessionState media)
        {
            var focused = Keyboard.FocusedElement as Control;
            bool hadFocus = MediaControl(focused);
            string tag = hadFocus ? focused.Tag as string : null;
            foreach (var row in MediaRows()) foreach (var c in row) actions.Remove(c);
            FillMediaCard(media);
            actions.Clear(); foreach (var row in homeRows) actions.AddRange(row);
            if (!hadFocus) return;
            window.UpdateLayout();
            var target = actions.Find(c => (string)c.Tag == tag && c.IsEnabled && MediaControl(c));
            if (target == null && media != null) { UpdateMedia(media); target = FirstEnabled(mediaTransportRow) ?? FirstEnabled(mediaOptionsRow); }
            if (target != null) target.Focus(); else FocusHome("utility-library");
        }
        bool MediaControl(Control c)
        {
            return c != null && (mediaTransportRow.Contains(c) || mediaProgressRow.Contains(c) || mediaOptionsRow.Contains(c));
        }
        static Control FirstEnabled(IList<Control> row)
        {
            foreach (var c in row) if (c.IsEnabled) return c;
            return null;
        }
        static Control NearestEnabled(IList<Control> row, int column)
        {
            Control best = null; int distance = int.MaxValue, wanted = Math.Min(column, row.Count - 1);
            for (int i = 0; i < row.Count; i++)
                if (row[i].IsEnabled && Math.Abs(i - wanted) < distance) { best = row[i]; distance = Math.Abs(i - wanted); }
            return best;
        }
        // Esquerda/direita dentro das linhas do player no painel Som; a Home usa a navegacao propria.
        bool NavigateMediaRow(Control focused, string key)
        {
            if (key != "ESQUERDA" && key != "DIREITA") return false;
            List<Control> row = null;
            foreach (var r in MediaRows()) if (r.Contains(focused)) row = r;
            if (row == null) return false;
            var slider = focused as Slider;
            if (slider != null && NudgeMedia(slider, key == "DIREITA" ? 1 : -1)) return true;
            int step = key == "DIREITA" ? 1 : -1, next = row.IndexOf(focused) + step;
            while (next >= 0 && next < row.Count && !row[next].IsEnabled) next += step;
            if (next >= 0 && next < row.Count) { audioColumn = next; row[next].Focus(); }
            else if (key == "ESQUERDA") FocusNavigation(panel);
            return true;
        }

        FrameworkElement EmptyMedia()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Background = Brush("SurfaceHover"), Child = Symbol("", 18) });
            string text = mediaSessions != null ? "Nada tocando agora" : mediaError != null ? "Mídia indisponível" : "Buscando mídia…";
            var label = Text(text, 15, "Muted"); label.Margin = new Thickness(12, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label); return row;
        }
        FrameworkElement ActiveMedia(bool multi)
        {
            var root = new StackPanel();
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MediaCoverSize) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mediaCover = new Border { Width = MediaCoverSize, Height = MediaCoverSize, CornerRadius = new CornerRadius(8), Background = Brush("SurfaceHover") };
            top.Children.Add(mediaCover);
            var info = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            mediaTitle = Text("", 16, "TextPrimary"); mediaTitle.FontWeight = FontWeights.SemiBold;
            mediaSubtitle = Text("", 13, "Muted"); mediaSubtitle.Margin = new Thickness(0, 1, 0, 0);
            foreach (var t in new[] { mediaTitle, mediaSubtitle }) { t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; info.Children.Add(t); }
            Grid.SetColumn(info, 1); top.Children.Add(info);
            var transport = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            mediaPrevious = MediaButton("", "Faixa anterior", "media-previous", 34, delegate { MediaDo("previous"); });
            mediaPlay = MediaButton("", "Reproduzir", "media-play", 40, delegate { MediaDo("toggle"); });
            mediaPlay.Style = (Style)window.FindResource("MediaPrimary");
            mediaNext = MediaButton("", "Próxima faixa", "media-next", 34, delegate { MediaDo("next"); });
            mediaPlay.Margin = new Thickness(4, 0, 4, 0);
            foreach (var b in new[] { mediaPrevious, mediaPlay, mediaNext }) { transport.Children.Add(b); mediaTransportRow.Add(b); }
            Grid.SetColumn(transport, 2); top.Children.Add(transport);
            root.Children.Add(top);

            var progress = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            mediaElapsed = Text("0:00", 12, "Muted"); mediaTotal = Text("0:00", 12, "Muted"); mediaTotal.TextAlignment = TextAlignment.Right;
            mediaElapsed.VerticalAlignment = mediaTotal.VerticalAlignment = VerticalAlignment.Center;
            mediaProgress = MediaSlider("media-progress", "Posição da faixa", 20);
            mediaProgress.Margin = new Thickness(2, 0, 2, 0);
            mediaProgress.ValueChanged += delegate { if (!mediaSync) QueueSeek(mediaProgress.Value); };
            Grid.SetColumn(mediaProgress, 1); Grid.SetColumn(mediaTotal, 2);
            progress.Children.Add(mediaElapsed); progress.Children.Add(mediaProgress); progress.Children.Add(mediaTotal);
            mediaProgressRow.Add(mediaProgress); root.Children.Add(progress);

            var options = new DockPanel { LastChildFill = false };
            mediaShuffle = MediaButton("", "Aleatório", "media-shuffle", 30, delegate { MediaDo("shuffle"); });
            mediaRepeat = MediaButton("", "Repetir", "media-repeat", 30, delegate { MediaDo("repeat"); });
            mediaOptionsRow.Add(mediaShuffle); mediaOptionsRow.Add(mediaRepeat);
            if (multi) { mediaSwitch = MediaButton("", "Trocar fonte", "media-switch", 30, SwitchMedia); mediaOptionsRow.Add(mediaSwitch); }
            foreach (var b in mediaOptionsRow) { b.Margin = new Thickness(0, 0, 4, 0); options.Children.Add(b); }
            mediaVolume = MediaSlider("media-volume", "Volume do app", 30);
            mediaVolume.Maximum = 100; mediaVolume.TickFrequency = 1; mediaVolume.IsSnapToTickEnabled = true; mediaVolume.Width = 120;
            mediaVolume.ValueChanged += delegate {
                if (mediaSync || mediaAudio == null || mediaAudio.SessionIds == null || mediaAudio.SessionIds.Length == 0) return;
                MotionCancelVolume(mediaVolume);
                QueueBindingAudio(mediaAudio, false);
            };
            mediaMute = MediaButton("", "Silenciar app", "media-mute", 30, ToggleMediaMute);
            mediaMute.Margin = new Thickness(0, 0, 2, 0);
            DockPanel.SetDock(mediaVolume, Dock.Right); DockPanel.SetDock(mediaMute, Dock.Right);
            options.Children.Add(mediaVolume); options.Children.Add(mediaMute);
            mediaOptionsRow.Add(mediaMute); mediaOptionsRow.Add(mediaVolume);
            mediaAudio = new AudioBinding { Key = "media-app", Slider = mediaVolume, Mute = mediaMute, Value = new TextBlock(), SessionIds = new string[0] };
            root.Children.Add(options);
            return root;
        }
        Button MediaButton(string glyph, string tip, string tag, double size, Action action)
        {
            var button = new Button { Content = Symbol(glyph, size >= 40 ? 17 : 15), Width = size, Height = size, Tag = tag, ToolTip = tip,
                Style = (Style)window.FindResource("MediaButton") };
            WireMotion(button);
            System.Windows.Automation.AutomationProperties.SetName(button, tip);
            button.Click += delegate { InvokeSoundAction(button, action); };
            button.GotKeyboardFocus += delegate { mediaHost.BringIntoView(); ShowHomeHint(button); };
            return button;
        }
        Slider MediaSlider(string tag, string name, double height)
        {
            var slider = new Slider { Minimum = 0, Maximum = 1, Height = height, Tag = tag, ToolTip = name, IsMoveToPointEnabled = true,
                Style = (Style)window.FindResource("VolumeSlider"), VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(slider, name);
            slider.GotKeyboardFocus += delegate { mediaEditing = slider; mediaHost.BringIntoView(); ShowHomeHint(slider); };
            slider.LostKeyboardFocus += delegate { if (mediaEditing == slider) mediaEditing = null; };
            return slider;
        }

        void UpdateMedia(MediaSessionState m)
        {
            if (mediaPlay == null) return;
            if (mediaShown != null && mediaShown != m.Key) CancelMediaSeek();
            mediaShown = m.Key;
            MotionText(mediaTitle, m.Title.Length > 0 ? m.Title : m.AppName, MotionNormal); mediaTitle.ToolTip = mediaTitle.Text;
            string artist = m.Artist.Length > 0 ? m.Artist : m.Album;
            MotionText(mediaSubtitle, (artist.Length > 0 ? artist + " · " : "") + m.AppName, MotionNormal); mediaSubtitle.ToolTip = mediaSubtitle.Text;
            string coverKey = m.CoverKey + "|" + (m.Cover == null ? 0 : m.Cover.Length);
            if (coverKey != mediaCoverKey || !ReferenceEquals(mediaCoverBytes, m.Cover))
            { mediaCoverKey = coverKey; mediaCoverBytes = m.Cover; mediaCover.Child = CoverImage(m.Cover) ?? Symbol("", 20); MotionReveal(mediaCover, MotionNormal); }
            bool refocus = false;
            SetMediaButton(mediaPlay, m.Playing ? "" : "", m.Playing ? "Pausar" : "Reproduzir", m.CanPlayPause, "SurfaceRail", ref refocus);
            SetMediaButton(mediaPrevious, "", "Faixa anterior", m.CanPrevious, "TextPrimary", ref refocus);
            SetMediaButton(mediaNext, "", "Próxima faixa", m.CanNext, "TextPrimary", ref refocus);
            SetMediaButton(mediaShuffle, "", "Aleatório · " + (m.Shuffle ? "ligado" : "desligado"), m.CanShuffle, m.Shuffle ? "AccentText" : "TextPrimary", ref refocus);
            SetMediaButton(mediaRepeat, m.Repeat == 1 ? "" : "", "Repetir · " + (m.Repeat == 1 ? "faixa" : m.Repeat == 2 ? "lista" : "desligado"),
                m.CanRepeat, m.Repeat != 0 ? "AccentText" : "TextPrimary", ref refocus);
            if (mediaSwitch != null) SetMediaButton(mediaSwitch, "", "Trocar fonte · " + MediaChoices().Count + " tocando", true, "TextPrimary", ref refocus);
            SetMediaEnabled(mediaProgress, m.CanSeek, ref refocus);
            mediaSync = true;
            try { mediaProgress.Maximum = Math.Max(1, m.Duration.TotalSeconds); }
            finally { mediaSync = false; }
            mediaTotal.Text = m.Duration > TimeSpan.Zero ? Clock(m.Duration) : "--:--";
            UpdateMediaClock();
            SyncMediaVolume(ref refocus);
            MotionResumeMedia();
            if (refocus)
            {
                var target = FirstEnabled(mediaTransportRow) ?? FirstEnabled(mediaOptionsRow);
                if (target != null) target.Focus(); else if (panel == 0) FocusHome("utility-library"); else FocusAudioTag("output");
            }
        }
        void SetMediaButton(Button button, string glyph, string tip, bool enabled, string color, ref bool refocus)
        {
            var symbol = (TextBlock)button.Content;
            bool changed = symbol.Text != glyph || symbol.Foreground != Brush(color);
            symbol.Text = glyph; symbol.Foreground = Brush(color);
            if (changed) MotionReveal(symbol, MotionFast);
            button.ToolTip = tip; System.Windows.Automation.AutomationProperties.SetName(button, tip);
            SetMediaEnabled(button, enabled, ref refocus);
        }
        static void SetMediaEnabled(Control control, bool enabled, ref bool refocus)
        {
            // Um controle desativado perde o foco; o player devolve o foco a uma acao valida.
            if (!enabled && control.IsKeyboardFocused) refocus = true;
            control.IsEnabled = enabled; control.Opacity = enabled ? 1 : .35;
        }
        FrameworkElement CoverImage(byte[] bytes)
        {
            if (bytes == null) return null;
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(bytes))
                { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 100; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
                return new Image { Source = bitmap, Stretch = Stretch.UniformToFill, Width = MediaCoverSize, Height = MediaCoverSize,
                    Clip = new RectangleGeometry(new Rect(0, 0, MediaCoverSize, MediaCoverSize), 8, 8) };
            }
            catch { return null; }
        }
        static string Clock(TimeSpan value)
        {
            return value.TotalHours >= 1 ? string.Format("{0}:{1:mm\\:ss}", (int)value.TotalHours, value) : string.Format("{0}:{1:ss}", (int)value.TotalMinutes, value);
        }
        void UpdateMediaClock()
        {
            if (mediaProgress == null || mediaShown == null) return;
            var m = MediaSelected(); if (m == null || m.Key != mediaShown) return;
            if (mediaSeekTarget >= 0 || mediaProgress.IsMouseCaptureWithin) { SetMediaClockText(TimeSpan.FromSeconds(mediaProgress.Value)); return; }
            var position = m.PositionAt(DateTimeOffset.Now);
            mediaSync = true;
            try { mediaProgress.Value = Math.Min(mediaProgress.Maximum, position.TotalSeconds); }
            finally { mediaSync = false; }
            SetMediaClockText(m.Duration > TimeSpan.Zero ? (TimeSpan?)position : null);
        }
        void SetMediaClockText(TimeSpan? value)
        {
            long second = value.HasValue ? value.Value.Ticks / TimeSpan.TicksPerSecond : 0;
            if (mediaClockLabel == mediaElapsed && mediaClockKnown == value.HasValue && mediaClockSecond == second) return;
            mediaClockLabel = mediaElapsed; mediaClockKnown = value.HasValue; mediaClockSecond = second;
            mediaElapsed.Text = value.HasValue ? Clock(value.Value) : "--:--";
        }

        void MediaDo(string kind)
        {
            var m = MediaSelected(); if (m == null) return;
            var command = new MediaCommand { Kind = kind, SessionKey = m.Key };
            var now = DateTimeOffset.Now;
            // Atualizacao otimista; a leitura seguinte confirma o estado informado pelo app.
            if (kind == "toggle") { if (!m.CanPlayPause) return; m.Position = m.PositionAt(now); m.UpdatedAt = now; m.Status = m.Playing ? "paused" : "playing"; }
            else if (kind == "shuffle") { if (!m.CanShuffle) return; command.Shuffle = !m.Shuffle; m.Shuffle = command.Shuffle; }
            else if (kind == "repeat") { if (!m.CanRepeat) return; command.Repeat = m.Repeat == 0 ? 2 : m.Repeat == 2 ? 1 : 0; m.Repeat = command.Repeat; }
            else if (kind == "previous" && !m.CanPrevious || kind == "next" && !m.CanNext) return;
            RunMedia(command); RenderMedia();
        }
        void QueueSeek(double seconds)
        {
            var media = MediaSelected(); if (media == null || !media.CanSeek) return;
            mediaRevision++; mediaSeekSession = media.Key; mediaSeekTrack = media.CoverKey;
            mediaSeekTarget = Math.Max(0, seconds); mediaSeekAt = DateTime.UtcNow.AddMilliseconds(350);
            if (mediaElapsed != null) SetMediaClockText(TimeSpan.FromSeconds(mediaSeekTarget));
            RefreshPresentationTimers();
        }
        void SendSeek()
        {
            var m = MediaSelected(); double target = mediaSeekTarget;
            string key = mediaSeekSession, track = mediaSeekTrack; CancelMediaSeek();
            if (target < 0 || m == null || !m.CanSeek || m.Key != key || m.CoverKey != track) return;
            m.Position = TimeSpan.FromSeconds(target); m.UpdatedAt = DateTimeOffset.Now;
            RunMedia(new MediaCommand { Kind = "seek", SessionKey = m.Key, Position = m.Position });
        }
        void RunMedia(MediaCommand command)
        {
            mediaRevision++;
            long commandRevision = mediaRevision;
            string scope = FeedbackScope;
            WriteLog("midia: " + command.Kind + " | " + command.SessionKey + (command.Kind == "seek" ? " | " + Clock(command.Position) : ""));
            mediaCommands.Enqueue(command, delegate(Exception error) {
                mediaNextRead = DateTime.MinValue;
                if (error != null)
                {
                    WriteLog("midia: comando falhou | " + command.SessionKey + " | " + error.Message);
                    var current = MediaSelected();
                    if (commandRevision == mediaRevision && current != null && current.Key == command.SessionKey)
                        NewFeedback("Mídia: " + error.Message, FeedbackKind.Error, scope);
                }
            });
        }
        void SwitchMedia()
        {
            var choices = MediaChoices(); var current = MediaSelected();
            if (choices.Count < 2 || current == null) return;
            CancelMediaSeek(); mediaRevision++; mediaDeferred = null;
            mediaPinned = choices[(choices.FindIndex(s => s.Key == current.Key) + 1) % choices.Count].Key;
            WriteLog("midia: fonte | " + mediaPinned);
            RenderMedia();
        }

        void SyncMediaVolume(ref bool refocus)
        {
            if (mediaVolume == null || mediaAudio == null) return;
            var m = MediaSelected();
            var ids = new List<string>();
            // O volume do player e o mesmo do mixer: as sessoes de audio do app de origem.
            if (m != null && audioSnapshot != null)
                foreach (var s in audioSnapshot.Sessions)
                    if (MediaSessions.Matches(m, s.AppIdentity)) ids.Add(s.Id);
            // Um ID SMTC que informa so o nome do exe nao pode escolher entre instalacoes diferentes.
            if (m != null && m.AppPaths.Count == 0 && m.AppId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && audioSnapshot != null)
            {
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in ids) paths.Add(audioSnapshot.Sessions.Find(s => s.Id == id).AppIdentity);
                if (paths.Count > 1) ids.Clear();
            }
            mediaAudio.MediaKey = m == null ? null : m.Key;
            mediaAudio.Key = "media-app:" + (m == null ? "none" : m.Key);
            mediaAudio.SessionIds = ids.ToArray();
            float level; bool muted;
            bool available = BindingAudioState(mediaAudio, out level, out muted);
            SetMediaEnabled(mediaVolume, available, ref refocus); SetMediaEnabled(mediaMute, available, ref refocus);
            mediaVolume.ToolTip = available ? "Volume de " + m.AppName : "Volume indisponível para esta fonte";
            if (available)
            {
                mediaSync = true;
                try
                {
                    if (!mediaVolume.IsMouseCaptureWithin) MotionVolume(mediaVolume, Math.Round(level * 100));
                    mediaAudio.Muted = muted;
                }
                finally { mediaSync = false; }
            }
            UpdateMediaMute(available && mediaAudio.Muted);
        }
        void SyncMediaVolume()
        {
            bool refocus = false; SyncMediaVolume(ref refocus);
            if (refocus)
            {
                var target = FirstEnabled(mediaTransportRow) ?? FirstEnabled(mediaOptionsRow);
                if (target != null) target.Focus();
            }
        }
        void ToggleMediaMute()
        {
            if (mediaAudio == null || !mediaMute.IsEnabled) return;
            mediaAudio.Muted = !mediaAudio.Muted; UpdateMediaMute(mediaAudio.Muted); QueueBindingAudio(mediaAudio, true);
        }
        void UpdateMediaMute(bool muted)
        {
            var symbol = (TextBlock)mediaMute.Content;
            bool changed = symbol.Text != (muted ? "" : "");
            symbol.Text = muted ? "" : ""; symbol.Foreground = Brush(muted ? "AccentText" : "TextPrimary");
            string tip = muted ? "Ativar som do app" : "Silenciar app";
            mediaMute.ToolTip = tip; System.Windows.Automation.AutomationProperties.SetName(mediaMute, tip);
            if (changed) MotionReveal(symbol, MotionFast);
        }

        bool NudgeMedia(Slider slider, int direction)
        {
            if (slider == mediaProgress) { if (slider.IsEnabled) slider.Value = Math.Max(0, Math.Min(slider.Maximum, slider.Value + direction * 10)); return true; }
            if (slider == mediaVolume) { if (slider.IsEnabled) slider.Value = Math.Max(0, Math.Min(100, slider.Value + direction * 5)); return true; }
            return false;
        }
        bool ActivateMedia()
        {
            if (!MediaPanel()) return false;
            var slider = Keyboard.FocusedElement as Slider;
            if (slider != null && (slider == mediaVolume || slider == mediaProgress)) { mediaEditing = slider; return true; }
            return false;
        }
        void CancelMediaSeek() { mediaSeekTarget = -1; mediaSeekAt = DateTime.MaxValue; mediaSeekSession = mediaSeekTrack = null; }
        bool MediaInteracting()
        {
            return mediaEditing != null && mediaEditing.IsKeyboardFocused ||
                mediaVolume != null && mediaVolume.IsMouseCaptureWithin || mediaProgress != null && mediaProgress.IsMouseCaptureWithin;
        }
        bool BackMedia()
        {
            var slider = Keyboard.FocusedElement as Slider;
            if (slider == null || slider != mediaVolume && slider != mediaProgress || !MediaPanel()) return false;
            Control target = slider == mediaVolume ? mediaMute : mediaPlay;
            mediaEditing = null;
            if (slider.IsMouseCaptureWithin) Mouse.Capture(null);
            if (target == null || !target.IsEnabled) target = FirstEnabled(mediaTransportRow) ?? FirstEnabled(mediaOptionsRow);
            if (target != null) target.Focus();
            return true;
        }

    }
}
