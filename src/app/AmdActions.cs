// Aba AMD: leitura do ReLive e do microfone em worker, comandos com a barra oculta e confirmacao pelo arquivo novo
// na pasta de saida ou pelo registro (ADR 0081). Metricas da AMD so na logica: o painel Desempenho ja as mostra.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Amd;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed class AmdPending
    {
        public string Command, Baseline, Label, On, Off;
        public Func<ReliveState, bool> Field;
        public bool Expect, Sent;
        public FeedbackNotice Notice;
        public DateTime Since, Deadline;
    }
    sealed partial class Preview
    {
        const string AmdScope = "8";
        // Substituiveis: a verificacao nunca envia comando real nem le/altera a pasta, o registro ou o microfone reais.
        Func<ReliveState> amdRead = RadeonRelive.Read;
        Action<string> amdSend = RadeonRelive.Send;
        Func<string, int, List<ReliveCapture>> amdRecent = RadeonRelive.Recent;
        Func<string, byte[]> amdThumb = delegate(string path) { return CaptureThumbnails.Png(path, 256, 144); };
        Func<MicrophoneState> micRead = CoreAudio.ReadMicrophone;
        Action<string, int?, bool?> micSet = CoreAudio.SetMicrophone;
        Action<string, bool> amdOpen = delegate(string target, bool maximize) { Process.Start(AmdOpenInfo(target, maximize)); };
        Func<bool> amdElevated = delegate { return LaunchSettings.Elevated; };
        Func<DateTime> amdNow = delegate { return DateTime.Now; };
        Action<Action> amdAfterHide;
        readonly DispatcherTimer amdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        readonly DispatcherTimer amdMicTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        ReliveState amdRelive; MicrophoneState amdMic; List<ReliveCapture> amdCaptures = new List<ReliveCapture>();
        readonly Dictionary<string, ImageSource> amdThumbs = new Dictionary<string, ImageSource>();
        bool amdBusy, amdRecording; DateTime amdNextRead = DateTime.MinValue, amdRecordingSince, amdGrowSeen;
        string amdGrowPath, amdSignature; long amdGrowBytes;
        AmdPending amdCapture, amdToggle;
        int amdMicVolume = -1;

        void StartAmd()
        {
            StartCapturePhotoPresentation();
            amdAfterHide = verify ? (Action<Action>)delegate(Action run) { run(); } : delegate(Action run) {
                // A barra precisa sumir da tela: animacao feita, mais um quadro do compositor e o foco devolvido ao jogo.
                var wait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                wait.Tick += delegate { wait.Stop(); run(); }; wait.Start();
            };
            if (verify)
            {
                // Verificacao: nada real; os testes trocam por fixtures proprias.
                amdRead = delegate { return null; }; amdSend = delegate { }; amdRecent = delegate { return new List<ReliveCapture>(); };
                amdThumb = delegate { return null; }; micRead = delegate { return null; }; micSet = delegate { }; amdOpen = delegate { };
                return;
            }
            amdTimer.Tick += delegate { AmdTick(); }; amdTimer.Start();
            amdMicTimer.Tick += delegate { amdMicTimer.Stop(); ApplyMicVolume(); };
        }
        bool AmdWatching { get { return panel == AmdPanel && window.IsVisible || amdCapture != null || amdToggle != null || amdRecording; } }
        void AmdTick()
        {
            if (disposed || amdBusy || !AmdWatching || amdNow() < amdNextRead) { AmdClockTick(); return; }
            amdBusy = true; amdNextRead = amdNow().AddSeconds(1);
            var read = amdRead; var recent = amdRecent; var thumb = amdThumb; var mic = micRead; var known = new HashSet<string>(amdThumbs.Keys);
            Action<Action> run = verify ? (Action<Action>)delegate(Action a) { a(); } : delegate(Action a) { Task.Factory.StartNew(a); };
            run(delegate {
                ReliveState state = null; MicrophoneState microphone = null; List<ReliveCapture> captures = null; string error = null;
                var images = new Dictionary<string, byte[]>();
                try { state = read(); } catch (Exception ex) { error = "estado: " + ex.Message; }
                try { microphone = mic(); } catch (Exception ex) { error = "microfone: " + ex.Message; }
                if (state != null) try
                {
                    captures = recent(state.OutputFolder, 6);
                    foreach (var c in captures.GetRange(0, Math.Min(3, captures.Count)))
                        if (!known.Contains(ThumbKey(c))) try { images[ThumbKey(c)] = thumb(c.Path); } catch (Exception) { images[ThumbKey(c)] = null; }
                }
                catch (Exception ex) { error = "capturas: " + ex.Message; }
                Action apply = delegate {
                    amdBusy = false;
                    if (error != null) WriteLog("amd: leitura parcial | " + error);
                    foreach (var pair in images) amdThumbs[pair.Key] = Thumb(pair.Value);
                    AmdApply(state, microphone, captures);
                };
                if (verify) apply(); else Dispatch(apply);
            });
        }
        static string ThumbKey(ReliveCapture c) { return c.Path + "|" + c.Modified.Ticks; }
        static ImageSource Thumb(byte[] png)
        {
            if (png == null) return null;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = new MemoryStream(png); image.EndInit(); image.Freeze();
            return image;
        }

        void AmdApply(ReliveState state, MicrophoneState microphone, List<ReliveCapture> captures)
        {
            if (state != null) amdRelive = state;
            amdMic = microphone;
            if (captures != null) { foreach (var c in captures) c.Game = AmdGameName(c.Game); amdCaptures = captures; TrackRecording(captures); }
            ConfirmAmdCapture(); ConfirmAmdToggle();
            if (amdThumbs.Count > 64) amdThumbs.Clear();
            if (panel == AmdPanel && window.IsVisible && AmdSignature() != amdSignature) RebuildAmd(null);
            AmdClockTick();
        }
        // Gravacao em andamento: o video mais novo, sem "_replay_", crescendo entre leituras. Repeticoes salvas nao contam.
        void TrackRecording(List<ReliveCapture> captures)
        {
            var video = captures.Find(c => c.Video && !c.Replay);
            DateTime now = amdNow();
            if (video != null && video.Path == amdGrowPath) { if (video.Bytes > amdGrowBytes) amdGrowSeen = now; amdGrowBytes = video.Bytes; }
            else if (video != null && now - video.Modified < TimeSpan.FromSeconds(10)) { amdGrowPath = video.Path; amdGrowBytes = video.Bytes; amdGrowSeen = DateTime.MinValue; }
            bool growing = amdGrowSeen != DateTime.MinValue && now - amdGrowSeen < TimeSpan.FromSeconds(4);
            bool stopping = amdCapture != null && amdCapture.Command == RadeonRelive.StopRecording;
            if (growing && !amdRecording && !stopping)
            { amdRecording = true; amdRecordingSince = video.Created; WriteLog("amd: gravacao observada | " + video.Game); }
            // Parou de crescer depois de observada: terminou pela barra ou pelo atalho do Adrenalin.
            bool starting = amdCapture != null && amdCapture.Command == RadeonRelive.StartRecording;
            if (!growing && amdRecording && amdGrowSeen != DateTime.MinValue && !starting)
            { amdRecording = false; amdRecordingSince = DateTime.MinValue; WriteLog("amd: gravacao terminou"); }
        }
        string AmdSignature()
        {
            var s = amdRelive; var text = new System.Text.StringBuilder(s == null ? "?" : s.Installed + "|" + s.Running + s.Enabled + s.InstantReplay + s.ReplaySeconds + s.Desktop + s.Microphone + s.Camera + s.InstantGif + s.InGameReplay);
            text.Append("|" + amdRecording + (amdCapture != null) + (amdToggle == null ? "" : amdToggle.Command));
            text.Append("|" + (amdMic == null ? "-" : amdMic.Id + amdMic.Muted + (amdMicVolume >= 0 ? amdMicVolume : amdMic.Volume)));
            foreach (var c in amdCaptures.GetRange(0, Math.Min(3, amdCaptures.Count))) text.Append("|" + ThumbKey(c) + amdThumbs.ContainsKey(ThumbKey(c)) + c.Length);
            return text.ToString();
        }

        bool AmdReady()
        {
            var s = amdRelive;
            if (s == null) { Feedback("Lendo o AMD Software…", FeedbackKind.Pending); return false; }
            if (!s.Installed) { Feedback("O AMD Software não foi encontrado neste PC.", FeedbackKind.Error); return false; }
            if (!s.Running || !s.Enabled) { Feedback("O Radeon ReLive não está ativo. Ative Gravar e transmitir no AMD Software.", FeedbackKind.Attention); return false; }
            return true;
        }
        void AmdCaptureAction(string tag)
        {
            if (!AmdReady()) return;
            if (amdCapture != null) { Feedback("Aguarde a captura anterior terminar.", FeedbackKind.Pending); return; }
            string command = tag == "amd-replay" ? RadeonRelive.SaveReplay : tag == "amd-shot" ? RadeonRelive.Screenshot :
                tag == "amd-gif" ? RadeonRelive.SaveGif : tag == "amd-ingame" ? RadeonRelive.SaveInGameReplay :
                amdRecording ? RadeonRelive.StopRecording : RadeonRelive.StartRecording;
            var newest = amdCaptures.Count > 0 ? amdCaptures[0].Path : null;
            var pending = new AmdPending { Command = command, Baseline = newest, Since = amdNow() };
            pending.Notice = BeginFeedback(command == RadeonRelive.SaveReplay ? "Salvando a repetição…" : command == RadeonRelive.Screenshot ? "Capturando a tela…" :
                command == RadeonRelive.SaveGif ? "Salvando o GIF…" : command == RadeonRelive.SaveInGameReplay ? "Mostrando a repetição no jogo…" :
                command == RadeonRelive.StartRecording ? "Iniciando a gravação…" : "Parando a gravação…", AmdScope, 40);
            amdCapture = pending; WriteLog("amd: " + command + " | barra oculta antes do comando");
            // A barra sai da imagem antes: nada dela na foto, no inicio do video ou no fim da repeticao.
            AnimateDismiss(delegate { Hide(true); amdAfterHide(delegate { SendAmd(pending); }); });
        }
        void AmdToggleAction(string command, string label, string on, string off, Func<ReliveState, bool> field)
        {
            if (!AmdReady()) return;
            if (amdToggle != null) { Feedback("Aguarde a mudança anterior terminar.", FeedbackKind.Pending); return; }
            bool expect = !field(amdRelive);
            var pending = new AmdPending { Command = command, Label = label, On = on, Off = off, Field = field, Expect = expect, Since = amdNow() };
            pending.Notice = BeginFeedback((expect ? "Ligando: " : "Desligando: ") + label + "…", AmdScope, 10);
            amdToggle = pending; RebuildAmd(TagOf(command));
            SendAmd(pending);
        }
        void SendAmd(AmdPending pending)
        {
            var send = amdSend; Exception failure = null;
            Action done = delegate {
                if (failure != null)
                {
                    FinishFeedback(pending.Notice, failure.Message, failure is InvalidOperationException && failure.Message.StartsWith("O Radeon", StringComparison.Ordinal) ? FeedbackKind.Attention : FeedbackKind.Error);
                    WriteLog("amd: comando falhou | " + pending.Command + " | " + failure.Message);
                    if (amdCapture == pending) amdCapture = null; if (amdToggle == pending) amdToggle = null;
                    if (panel == AmdPanel && window.IsVisible) RebuildAmd(TagOf(pending.Command)); return;
                }
                pending.Sent = true; pending.Deadline = amdNow().AddSeconds(pending.Command == RadeonRelive.SaveReplay || pending.Command == RadeonRelive.SaveGif ? 25 : pending.Field != null ? 4 : 10);
                // A repeticao no jogo nao gera arquivo: entregue ao AMD Software, termina aqui.
                if (pending.Command == RadeonRelive.SaveInGameReplay) { FinishFeedback(pending.Notice, "Repetição enviada para a tela do jogo.", FeedbackKind.Info); if (amdCapture == pending) amdCapture = null; }
                WriteLog("amd: comando entregue ao AMD Software | " + pending.Command);
                if (pending.Command == RadeonRelive.StartRecording) { amdRecording = true; amdRecordingSince = amdNow(); amdGrowSeen = DateTime.MinValue; amdGrowBytes = 0; }
                if (pending.Command == RadeonRelive.StopRecording) { amdRecording = false; amdRecordingSince = DateTime.MinValue; }
                amdNextRead = amdNow().AddMilliseconds(600);
            };
            if (verify) { try { send(pending.Command); } catch (Exception ex) { failure = ex; } done(); return; }
            Task.Factory.StartNew(delegate { try { send(pending.Command); } catch (Exception ex) { failure = ex; } Dispatch(done); });
        }
        // Confirmacao pelo efeito: arquivo novo na pasta (repeticao, foto) ou video crescendo/parando (gravacao).
        void ConfirmAmdCapture()
        {
            var p = amdCapture; if (p == null || !p.Sent) return;
            var newest = amdCaptures.Count > 0 ? amdCaptures[0] : null;
            bool fresh = newest != null && newest.Path != p.Baseline;
            string message = null;
            if (p.Command == RadeonRelive.SaveReplay && fresh && newest.Video && newest.Replay) message = "Repetição salva · " + newest.Game + ".";
            else if (p.Command == RadeonRelive.Screenshot && fresh && !newest.Video && !IsGif(newest)) message = "Captura de tela salva · " + newest.Game + ".";
            else if (p.Command == RadeonRelive.SaveGif && fresh && IsGif(newest)) message = "GIF salvo · " + newest.Game + ".";
            else if (p.Command == RadeonRelive.StartRecording && amdGrowSeen != DateTime.MinValue && amdRecording) message = "Gravando · " + (newest == null ? "AMD" : newest.Game) + ".";
            else if (p.Command == RadeonRelive.StopRecording)
            {
                // O arquivo da gravacao e o proprio ponto de partida: confirma quando ele para de ser escrito.
                var video = amdCaptures.Find(c => c.Video && !c.Replay);
                if (video != null && video.Modified >= p.Since.AddSeconds(-3) && amdNow() - video.Modified > TimeSpan.FromSeconds(2)) message = "Gravação salva · " + video.Game + ".";
            }
            if (message != null)
            {
                FinishFeedback(p.Notice, message, FeedbackKind.Success); amdCapture = null;
                if (p.Command == RadeonRelive.StopRecording) amdGrowSeen = DateTime.MinValue; // o arquivo fechado nao volta a contar como gravacao
                return;
            }
            if (amdNow() < p.Deadline) return;
            AmdUnconfirmed(p, p.Command == RadeonRelive.StartRecording ? "O AMD Software não confirmou o início da gravação. Confira o indicador do ReLive." :
                p.Command == RadeonRelive.StopRecording ? "O AMD Software não confirmou o fim da gravação. Confira a pasta das capturas." :
                "O AMD Software não confirmou a captura. Confira a pasta das capturas.");
            amdCapture = null;
        }
        void ConfirmAmdToggle()
        {
            var p = amdToggle; if (p == null || !p.Sent || amdRelive == null) return;
            if (p.Field(amdRelive) == p.Expect)
            {
                FinishFeedback(p.Notice, p.Label + ": " + (p.Expect ? p.On : p.Off) + ".", FeedbackKind.Success);
                amdToggle = null; return;
            }
            if (amdNow() < p.Deadline) return;
            AmdUnconfirmed(p, "O AMD Software não confirmou: " + p.Label + ".");
            amdToggle = null;
        }
        // Sem efeito observado: com a barra sem elevacao, o Windows bloqueia a mensagem ao AMDRSServ.exe elevado.
        void AmdUnconfirmed(AmdPending p, string message)
        {
            bool elevated = amdElevated();
            WriteLog("amd: sem confirmacao | " + p.Command + " | elevado=" + elevated);
            FinishFeedback(p.Notice, elevated ? message : "O AMD Software roda como administrador. Para a barra comandá-lo, ligue “Sempre executar como administrador” em Configurações.",
                elevated ? FeedbackKind.Warning : FeedbackKind.Attention);
        }
        // O ReLive nomeia a pasta pelo jogo; quando nao o reconhece, usa "steamappid<numero>" (atalho fora da Steam: 2147483647).
        string AmdGameName(string folder)
        {
            if (folder == null || !folder.StartsWith("steamappid", StringComparison.OrdinalIgnoreCase)) return folder;
            string suffix = ":" + folder.Substring(10);
            var entry = gamesView == null ? null : gamesView.Entries.Find(e => e.Key != null && e.Key.StartsWith("steam:", StringComparison.Ordinal) && e.Key.EndsWith(suffix, StringComparison.Ordinal));
            return entry != null && !string.IsNullOrEmpty(entry.Name) ? entry.Name : "Jogo da Steam";
        }
        static bool IsGif(ReliveCapture c) { return c.Path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase); }
        static string TagOf(string command)
        {
            return command == RadeonRelive.ToggleReplay ? "amd-toggle-replay" : command == RadeonRelive.ToggleDesktop ? "amd-toggle-desktop" :
                command == RadeonRelive.ToggleMicrophone ? "amd-toggle-mic" : command == RadeonRelive.ToggleCamera ? "amd-toggle-camera" : null;
        }

        void ToggleMicMute()
        {
            var mic = amdMic; if (mic == null) return;
            var set = micSet; bool muted = !mic.Muted;
            ConnectionWork(delegate { set(mic.Id, null, muted); }, delegate(Exception error) {
                if (error != null) { Feedback("Não foi possível " + (muted ? "silenciar" : "ativar") + " o microfone. " + error.Message, FeedbackKind.Error); return; }
                mic.Muted = muted; WriteLog("amd: microfone " + (muted ? "mudo" : "ativo"));
                RebuildAmd("amd-mic-mute"); amdNextRead = DateTime.MinValue;
            });
        }
        void QueueMicVolume(int value)
        {
            amdMicVolume = value;
            if (verify) ApplyMicVolume(); else { amdMicTimer.Stop(); amdMicTimer.Start(); }
        }
        void ApplyMicVolume()
        {
            var mic = amdMic; int value = amdMicVolume; if (mic == null || value < 0) return;
            var set = micSet;
            ConnectionWork(delegate { set(mic.Id, value, null); }, delegate(Exception error) {
                if (amdMicVolume == value) amdMicVolume = -1;
                if (error != null) { Feedback("Não foi possível mudar o volume do microfone. " + error.Message, FeedbackKind.Error); return; }
                mic.Volume = value;
            });
        }
        void OpenAmdTarget(string target, string label)
        {
            CancelCapturePhoto();
            bool photo = XGBP.Desktop.CapturePhotoPolicy.IsPhoto(target) && ReadGamingMode(activationRead) == true;
            var origin = previousWindow == null ? null : previousWindow.Window;
            try { amdOpen(target, photo); WriteLog("amd: aberto | " + label); Hide(); if (photo) BeginCapturePhoto(target, origin); }
            catch (Exception ex) { Feedback("Não foi possível abrir " + label + ".", FeedbackKind.Error); WriteLog("amd: falha ao abrir " + label + " | " + ex.Message); }
        }
        // Metricas da AMD: comandos existentes, sem botao (o painel Desempenho cobre o mesmo).
        void SendAmdMetrics(bool overlay)
        {
            if (!AmdReady()) return;
            var pending = new AmdPending { Command = overlay ? RadeonRelive.ToggleMetricsOverlay : RadeonRelive.ToggleMetricsLog, Since = amdNow() };
            pending.Notice = BeginFeedback(overlay ? "Alternando as métricas da AMD…" : "Alternando o registro de desempenho da AMD…", AmdScope, 5);
            var send = amdSend;
            ConnectionWork(delegate { send(pending.Command); }, delegate(Exception error) {
                FinishFeedback(pending.Notice, error != null ? error.Message : overlay ? "Métricas da AMD alternadas." : "Registro de desempenho da AMD alternado.",
                    error != null ? FeedbackKind.Error : FeedbackKind.Info);
            });
        }
    }
}
