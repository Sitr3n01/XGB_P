// Fixtures e verificacoes deste painel, separadas do codigo de operacao.
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
        static List<MediaSessionState> ExampleMedia()
        {
            var music = new MediaSessionState { Key = "Browser.exe#0", AppId = "Browser.exe", AppName = "Navegador", Title = "Faixa de exemplo", Artist = "Artista de teste",
                Album = "Álbum", Status = "playing", Current = true, CanPlayPause = true, CanPrevious = true, CanNext = true, CanSeek = true, CanShuffle = true, CanRepeat = true,
                Position = TimeSpan.FromSeconds(72), Duration = TimeSpan.FromSeconds(215) };
            music.CoverKey = music.Key + "|" + music.Title;
            var video = new MediaSessionState { Key = "Video.exe#0", AppId = "Video.exe", AppName = "Vídeo", Title = "Vídeo de exemplo", Artist = "", Album = "",
                Status = "paused", CanPlayPause = true, CanSeek = true, Position = TimeSpan.FromSeconds(20), Duration = TimeSpan.FromSeconds(90) };
            video.CoverKey = video.Key + "|" + video.Title;
            return new List<MediaSessionState> { music, video };
        }
        void VerifyMedia()
        {
            var original = mediaSessions; var send = mediaSend; var sent = new List<MediaCommand>();
            try
            {
                mediaSend = delegate(MediaCommand c) { sent.Add(c); };
                mediaSessions = ExampleMedia(); mediaPinned = null; audioPending.Clear();
                SetPanel(0); Show(); window.UpdateLayout();
                if (mediaTransportRow.Count != 3 || mediaProgressRow.Count != 1 || mediaOptionsRow.Count != 5 || mediaSwitch == null)
                    throw new Exception("Falha: controles do player.");
                if (mediaTitle.Text != "Faixa de exemplo" || !mediaSubtitle.Text.Contains("Navegador") || mediaTotal.Text != "3:35")
                    throw new Exception("Falha: dados da faixa.");
                if (!mediaVolume.IsEnabled || Math.Abs(mediaVolume.Value - 70) > .5) throw new Exception("Falha: volume do app pelo mixer.");
                if (((ScrollViewer)window.FindName("BodyScroll")).ScrollableHeight > 0) throw new Exception("Falha: player deve caber na Home sem rolagem.");
                SaveRender("home-midia-exemplo");
                mediaPlay.Focus(); Activate();
                if (sent.Count != 1 || sent[0].Kind != "toggle" || sent[0].SessionKey != "Browser.exe#0" || (string)mediaPlay.ToolTip != "Reproduzir")
                    throw new Exception("Falha: reproduzir/pausar.");
                mediaNext.Focus(); Activate(); mediaPrevious.Focus(); Activate();
                if (sent[1].Kind != "next" || sent[2].Kind != "previous") throw new Exception("Falha: anterior/proxima.");
                mediaShuffle.Focus(); Activate();
                if (sent[3].Kind != "shuffle" || !sent[3].Shuffle || ((TextBlock)mediaShuffle.Content).Foreground != Brush("AccentText")) throw new Exception("Falha: aleatorio.");
                mediaRepeat.Focus(); Activate(); Activate(); Activate();
                if (sent[4].Repeat != 2 || sent[5].Repeat != 1 || sent[6].Repeat != 0) throw new Exception("Falha: ciclo de repetir.");
                mediaProgress.Focus(); Navigate("DIREITA");
                if (Math.Abs(mediaSeekTarget - 82) > .5 || Keyboard.FocusedElement != mediaProgress) throw new Exception("Falha: avancar na faixa.");
                SendSeek();
                if (sent[7].Kind != "seek" || Math.Abs(sent[7].Position.TotalSeconds - 82) > .5) throw new Exception("Falha: posicao da faixa.");
                mediaVolume.Focus(); Navigate("DIREITA");
                if (!AudioHasPending(mediaAudio.Key + "-volume") || Math.Abs(audioPending[mediaAudio.Key + "-volume|out"].Level - .75f) > .001) throw new Exception("Falha: volume do player.");
                mediaMute.Focus(); Activate();
                if (!audioPending.ContainsKey(mediaAudio.Key + "-mute|out") || !audioPending[mediaAudio.Key + "-mute|out"].Muted) throw new Exception("Falha: mute do player.");
                audioPending.Clear();
                mediaSwitch.Focus(); Activate(); window.UpdateLayout();
                if (mediaShown != "Video.exe#0" || mediaNext.IsEnabled || mediaVolume.IsEnabled || Keyboard.FocusedElement != mediaSwitch) throw new Exception("Falha: trocar fonte.");
                // O player fica acima do acesso rapido; subir pula os controles que a fonte nao oferece.
                FocusHome("utility-downloads"); Navigate("CIMA");
                if (Keyboard.FocusedElement != mediaSwitch) throw new Exception("Falha: subir ate as opcoes do player.");
                Navigate("CIMA"); Navigate("CIMA");
                if (Keyboard.FocusedElement != mediaPlay) throw new Exception("Falha: subir ate reproduzir pulando controles inativos.");
                Navigate("CIMA");
                // Os tres outros recentes sao one, two e three (ADR 0060).
                if ((string)((Control)Keyboard.FocusedElement).Tag != "recent-fixture:three") throw new Exception("Falha: recentes acima do player.");
                mediaSend = delegate { throw new InvalidOperationException("recusado"); };
                mediaPlay.Focus(); Activate();
                if (!((TextBlock)window.FindName("Feedback")).Text.Contains("recusado")) throw new Exception("Falha: erro de comando de midia.");
                mediaPlay.Focus(); mediaSessions = new List<MediaSessionState>(); RenderMedia(); window.UpdateLayout();
                if (mediaPlay != null || mediaTransportRow.Count != 0 || (string)((Control)Keyboard.FocusedElement).Tag != "utility-library") throw new Exception("Falha: fonte encerrada com foco no player.");

                // No Som, o mesmo player fica acima da saida de som e usa a navegacao do painel.
                mediaSend = delegate(MediaCommand c) { sent.Add(c); }; sent.Clear();
                mediaSessions = ExampleMedia(); SetPanel(6); window.UpdateLayout();
                var outputSelector = actions.Find(c => (string)c.Tag == "output");
                if (mediaPlay == null || audioRows.Count < 4 || audioRows[0][1] != mediaPlay || audioBindings.Count != 4 ||
                    mediaHost.TranslatePoint(new Point(0, 0), outputSelector).Y >= 0) throw new Exception("Falha: player no topo do Som.");
                if (!mediaVolume.IsEnabled || Math.Abs(mediaVolume.Value - 70) > .5) throw new Exception("Falha: volume do player no Som.");
                SaveRender("som-midia-exemplo");
                mediaPlay.Focus(); Navigate("DIREITA");
                if (Keyboard.FocusedElement != mediaNext) throw new Exception("Falha: direita no player do Som.");
                Navigate("ESQUERDA"); Navigate("ESQUERDA"); Navigate("ESQUERDA");
                if (Keyboard.FocusedElement != rail[6]) throw new Exception("Falha: esquerda do player volta ao trilho.");
                mediaPlay.Focus(); Navigate("BAIXO"); Navigate("BAIXO"); Navigate("BAIXO");
                if (Keyboard.FocusedElement != outputSelector) throw new Exception("Falha: descer do player para a saida de som.");
                mediaPlay.Focus(); Activate();
                mediaProgress.Focus(); Navigate("DIREITA"); SendSeek();
                if (sent.Count != 2 || sent[0].Kind != "toggle" || sent[1].Kind != "seek") throw new Exception("Falha: comandos do player no Som.");
                mediaVolume.Focus(); Navigate("DIREITA");
                if (!AudioHasPending(mediaAudio.Key + "-volume")) throw new Exception("Falha: volume do player no Som.");
                audioPending.Clear();
            }
            finally
            {
                mediaSend = send; mediaSessions = original; mediaPinned = null; mediaSeekTarget = -1; audioPending.Clear(); SetPanel(0);
            }
        }
        public void VerifyMediaLive()
        {
            // Leitura e renderizacao apenas: nenhum comando de midia nem ajuste de volume.
            mediaSend = delegate { throw new InvalidOperationException("Diagnóstico sem comandos de mídia."); };
            mediaSessions = Task.Factory.StartNew<List<MediaSessionState>>(delegate { return MediaSessions.Read(); }).Result;
            audioSnapshot = Task.Factory.StartNew<AudioSnapshot>(delegate { return CoreAudio.Read(); }).Result;
            var report = new List<string>(); report.Add(mediaSessions.Count + " sessoes de midia; leitura sem comandos");
            foreach (var s in mediaSessions) report.Add(s.Key + " | " + s.Describe() + " | mixer=" + audioSnapshot.Sessions.FindAll(a => MediaSessions.Matches(s, a.AppIdentity)).Count);
            var repeat = Task.Factory.StartNew<List<MediaSessionState>>(delegate { return MediaSessions.Read(); }).Result;
            foreach (var s in repeat) report.Add("segunda leitura: " + s.Key + " | chave preservada=" + mediaSessions.Exists(old => old.Key == s.Key));
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); SetPanel(0); window.UpdateLayout(); SaveRender("home-midia-real");
            if (audioPending.Count != 0) throw new Exception("Falha: renderizacao enfileirou alteracao de audio.");
            File.WriteAllLines(Path.Combine(Output, "midia-real.txt"), report.ToArray()); window.Hide();
        }
    }
}
