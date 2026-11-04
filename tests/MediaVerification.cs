// Regressao dos conflitos de midia: alvos ficticios e workers controlados, sem comandos reais.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Audio;
using XGBP.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyMediaConflicts()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) { checks++; if (!ok) throw new Exception("Midia: " + message); };
            var keys = new MediaSessionKeys(); var first = new object(); var second = new object();
            string a = keys.Get(10, "App", first), b = keys.Get(20, "App", second);
            check(a != b && keys.Get(20, "App", second) == b && keys.Get(10, "App", first) == a, "ordem da enumeracao nao muda identidade");
            keys.Retain(new HashSet<long> { 20 });
            check(keys.Get(20, "App", second) == b && keys.Get(10, "App", new object()) != a, "encerrar/recriar nao reaproveita chave antiga");
            var identity = ExampleMedia()[0]; identity.AppPaths.Add(@"C:\App\Browser.exe");
            check(MediaSessions.Matches(identity, @"C:\App\Browser.exe") && !MediaSessions.Matches(identity, @"C:\Outro\Browser.exe"), "caminho conhecido impede falso positivo por nome");
            check(MediaSessions.SameCover(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }) &&
                !MediaSessions.SameCover(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }), "capa de mesmo tamanho compara conteudo");

            var jobs = new List<Action>(); var completed = new List<Action>(); var sent = new List<MediaCommand>();
            using (var fifo = new MediaCommandQueue(c => sent.Add(c), work => jobs.Add(work), done => completed.Add(done)))
            {
                fifo.Enqueue(new MediaCommand { Kind = "repeat", Repeat = 2 }, e => { });
                fifo.Enqueue(new MediaCommand { Kind = "repeat", Repeat = 1 }, e => { });
                check(jobs.Count == 1 && fifo.Pending, "uma unica drenagem FIFO"); jobs[0]();
                check(sent.Count == 2 && sent[0].Repeat == 2 && sent[1].Repeat == 1 && !fifo.Pending, "ordem dos gestos preservada");
                foreach (var done in completed) done();
            }
            jobs.Clear(); sent.Clear(); int callbacks = 0;
            using (var fifo = new MediaCommandQueue(c => { sent.Add(c); if (c.Kind == "fail") throw new IOException("fixture"); }, work => jobs.Add(work), done => done()))
            {
                fifo.Enqueue(new MediaCommand { Kind = "fail" }, e => { if (e != null) callbacks++; });
                fifo.Enqueue(new MediaCommand { Kind = "next" }, e => { if (e == null) callbacks++; }); jobs[0]();
                check(sent.Count == 2 && callbacks == 2 && !fifo.Pending, "erro nao bloqueia o comando seguinte");
            }
            jobs.Clear(); sent.Clear();
            using (var fifo = new MediaCommandQueue(c => sent.Add(c), work => jobs.Add(work), done => done()))
            {
                fifo.Enqueue(new MediaCommand { Kind = "next" }, e => { }); fifo.Dispose(); jobs[0]();
                check(sent.Count == 0, "encerramento cancela comandos ainda nao enviados");
            }

            var oldMedia = mediaSessions; var oldAudio = audioSnapshot; var oldSend = mediaSend; var oldRead = mediaRead; var oldQueue = mediaCommands;
            long oldRevision = mediaRevision; DateTime oldNextRead = mediaNextRead;
            var oldPending = new Dictionary<string, AudioChange>(audioPending); var oldFlight = new Dictionary<string, AudioChange>(audioInFlight);
            var oldDeferred = mediaDeferred; long oldDeferredRevision = mediaDeferredRevision;
            try
            {
                mediaSend = c => sent.Add(c); sent.Clear();
                mediaCommands = new MediaCommandQueue(c => mediaSend(c), work => work(), done => done());
                mediaRead = delegate { throw new Exception("Leitura real proibida na regressao"); };
                Action reset = delegate {
                    mediaEditing = null; CancelMediaSeek(); mediaDeferred = null; mediaPinned = null;
                    audioPending.Clear(); audioInFlight.Clear(); audioSnapshot = ExampleAudio(); mediaSessions = ExampleMedia();
                    sent.Clear(); SetPanel(0); Show(); ClearFeedback();
                };
                Action drain = delegate { foreach (var change in audioPending.Values) audioInFlight[change.Key] = change; audioPending.Clear(); };
                foreach (int panelId in new[] { 0, 6 })
                {
                    reset(); SetPanel(panelId); window.UpdateLayout();
                    mediaVolume.Focus(); Activate();
                    check(!mediaAudio.Muted && audioPending.Count == 0 && sent.Count == 0, "confirmar volume nao muta nem envia transporte, painel " + panelId);
                    Navigate("DIREITA");
                    check(audioPending.Count == 1 && !mediaAudio.Muted && Math.Abs(mediaVolume.Value - 75) < .5, "ajustar volume envia so volume, painel " + panelId);
                    Back(); check(Keyboard.FocusedElement == mediaMute && panel == panelId && window.IsVisible, "circulo no volume volta ao alto-falante dentro do player");
                    Back(); check(Keyboard.FocusedElement == rail[RailIndex(panelId)], "segundo circulo usa a navegacao normal");
                    audioPending.Clear(); mediaProgress.Focus(); Activate();
                    check(sent.Count == 0, "confirmar posicao nao reproduz/pausa");
                    Navigate("DIREITA"); Back();
                    check(Keyboard.FocusedElement == mediaPlay && sent.Count == 0, "circulo na posicao volta a reproduzir sem acionar");
                    SendSeek(); check(sent.Count == 1 && sent[0].Kind == "seek" && sent[0].SessionKey == "Browser.exe#0", "posicao conserva a fonte do gesto");
                }
                reset(); mediaVolume.Value = 25; string volumeKey = mediaAudio.Key + "-volume|out"; drain();
                SyncMediaVolume();
                check(AudioHasPending(mediaAudio.Key + "-volume") && Math.Abs(mediaVolume.Value - 25) < .5, "snapshot antigo nao desfaz volume em voo");
                SetPanel(6); SyncAudioBindings();
                var mixer = audioBindings.Find(x => x.Key == "app-app:Browser.exe");
                check(mixer != null && Math.Abs(mixer.Slider.Value - 25) < .5, "player e mixer compartilham a intencao pelo alvo nativo");
                mediaMute.Focus(); Activate(); drain(); SyncMediaVolume();
                check(mediaAudio.Muted, "snapshot antigo nao desfaz mute em voo");
                Activate(); SyncMediaVolume();
                check(!mediaAudio.Muted && !audioPending[mediaAudio.Key + "-mute|out"].Muted, "segundo clique solicita desmutar, nao mutar outra vez");
                audioSnapshot.Sessions.Find(s => s.Id == "session-browser").Level = .25f;
                audioSnapshot.Sessions.Find(s => s.Id == "session-browser").Muted = true;
                CompleteAudioRead(true, audioRevision, new List<AudioChange>()); SyncMediaVolume();
                check(audioInFlight.Count == 0 && !mediaAudio.Muted && Math.Abs(mediaVolume.Value - 25) < .5, "snapshot confirmado limpa voo sem perder intencao mais nova");
                audioPending.Clear(); SyncMediaVolume(); check(mediaAudio.Muted, "sem intencao pendente volta a refletir mudanca externa");

                reset(); audioSnapshot.Sessions.Add(new AudioSessionState { Id = "session-video", DeviceId = "out", AppIdentity = "Video.exe", AppName = "Video", Level = .2f });
                mediaVolume.Value = 25; mediaSwitch.Focus(); SwitchMedia();
                check(mediaShown == "Video.exe#0" && Math.Abs(mediaVolume.Value - 20) < .5, "fonte B nao herda valor pendente de A");
                mediaVolume.Value = 40;
                check(audioPending.Count == 2 && audioPending.Values.ExistsForMedia("session-browser", .25f) && audioPending.Values.ExistsForMedia("session-video", .4f), "duas fontes na mesma saida mantem comandos distintos");
                var changedVideo = audioSnapshot.Sessions.Find(s => s.Id == "session-video");
                var sameTarget = new AudioBinding { Key = "mixer-video", SessionIds = new[] { changedVideo.Id }, Slider = new Slider { Maximum = 100, Value = 60 }, Muted = false };
                QueueBindingAudio(sameTarget, false);
                check(audioPending.Count == 2 && audioPending.Values.ExistsForMedia("session-video", .6f), "ultima intencao do mesmo alvo substitui so aquele alvo");

                reset(); mediaProgress.Focus(); QueueSeek(80); mediaSwitch.Focus(); SwitchMedia(); SendSeek();
                check(sent.Count == 0 && mediaSeekTarget < 0, "trocar fonte cancela seek antigo");
                mediaPinned = "Browser.exe#0"; RenderMedia(); QueueSeek(80); mediaPinned = "Video.exe#0"; SendSeek();
                check(sent.Count == 0, "mudanca automatica tambem nao retargeta seek");
                mediaPinned = "Browser.exe#0"; QueueSeek(80); mediaSessions[0].CoverKey += "-nova-faixa"; SendSeek();
                check(sent.Count == 0, "troca de faixa invalida seek atrasado");

                reset(); mediaVolume.Focus(); var slider = mediaVolume;
                var next = new List<MediaSessionState> { ExampleMedia()[1] };
                CompleteMediaRead(next, null, mediaRevision); RenderMedia();
                check(ReferenceEquals(mediaVolume, slider) && mediaDeferred == next && mediaSessions.Count == 2, "snapshot estrutural e adiado durante edicao");
                Back(); mediaNextRead = DateTime.UtcNow.AddMinutes(1); MediaTick();
                check(mediaDeferred == null && mediaShown == "Video.exe#0" && mediaSessions.Count == 1, "apos sair do slider aplica snapshot adiado");
                var current = mediaSessions; long stale = mediaRevision; mediaRevision++;
                CompleteMediaRead(ExampleMedia(), null, stale);
                check(ReferenceEquals(current, mediaSessions), "leitura anterior a uma operacao nao substitui estado novo");

                reset(); mediaSessions[0].AppPaths.Add(@"C:\App\Browser.exe");
                audioSnapshot.Sessions[1].AppIdentity = @"C:\Outro\Browser.exe"; SyncMediaVolume();
                check(!mediaVolume.IsEnabled && !mediaMute.IsEnabled, "identidade divergente nao recebe volume/mute");
                reset(); audioSnapshot.Sessions[1].AppIdentity = @"C:\App\Browser.exe";
                audioSnapshot.Sessions.Add(new AudioSessionState { Id = "other-browser", DeviceId = "out", AppIdentity = @"C:\Outro\Browser.exe", Level = .4f }); SyncMediaVolume();
                check(!mediaVolume.IsEnabled, "nome de exe ambiguo nao controla instalacoes diferentes");

                reset(); mediaSessions[0].Cover = new byte[] { 1, 2, 3 }; UpdateMedia(mediaSessions[0]); var previous = mediaCoverBytes;
                mediaSessions[0].Cover = new byte[] { 1, 2, 4 }; UpdateMedia(mediaSessions[0]);
                check(!ReferenceEquals(previous, mediaCoverBytes) && ReferenceEquals(mediaCoverBytes, mediaSessions[0].Cover), "UI percebe capa alterada de mesmo comprimento");
                SaveRender("midia-corrigida");
            }
            finally
            {
                mediaCommands.Dispose(); mediaCommands = oldQueue;
                mediaSend = oldSend; mediaRead = oldRead; mediaSessions = oldMedia; audioSnapshot = oldAudio;
                mediaEditing = null; mediaPinned = null; CancelMediaSeek(); mediaRevision = oldRevision; mediaNextRead = oldNextRead;
                mediaDeferred = oldDeferred; mediaDeferredRevision = oldDeferredRevision;
                audioPending.Clear(); foreach (var item in oldPending) audioPending[item.Key] = item.Value;
                audioInFlight.Clear(); foreach (var item in oldFlight) audioInFlight[item.Key] = item.Value;
                SetPanel(0); ClearFeedback();
            }
            File.WriteAllText(Path.Combine(Output, "midia-conflitos-verificacao.txt"), "OK: " + checks + " verificacoes. Sliders sem mute/transporte implicitos, circulo retorna ao player na Home/Som; intencoes em voo e snapshot antigo, clique duplo de mute, player/mixer e fontes separadas, coalescencia por alvo, seek vinculado a fonte/faixa, leitura antiga, edicao sem reconstrucao, FIFO/erro/encerramento, identidade COM estavel, identidade de audio e capa de mesmo tamanho. Todos os comandos/sessoes substituidos; sem setters reais nem modo Xbox.\r\n", Encoding.UTF8);
        }
    }
    static class MediaVerificationValues
    {
        internal static bool ExistsForMedia(this IEnumerable<AudioChange> values, string id, float level)
        {
            foreach (var value in values) if (value.Kind == "session-volume" && value.SessionIds != null && Array.IndexOf(value.SessionIds, id) >= 0 && Math.Abs(value.Level - level) < .001) return true;
            return false;
        }
    }
}
