// Fixtures e verificacoes deste painel, separadas do codigo de operacao.
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
        static AudioSnapshot ExampleAudio()
        {
            var s = new AudioSnapshot();
            s.Devices.Add(new AudioDeviceState { Id = "out", Name = "Alto-falantes · exemplo de teste", Flow = 0, Available = true, Level = .65f });
            s.Devices.Add(new AudioDeviceState { Id = "in", Name = "Microfone · exemplo de teste", Flow = 1, Available = true, Level = .8f, Muted = true });
            for (int r = 0; r < 3; r++) { s.Defaults[0, r] = "out"; s.Defaults[1, r] = "in"; }
            s.Sessions.Add(new AudioSessionState { Id = "session-steam", DeviceId = "out", AppName = "Steam", AppIdentity = "Steam.exe", ProcessId = 123, Level = .5f });
            s.Sessions.Add(new AudioSessionState { Id = "session-browser", DeviceId = "out", AppName = "Navegador", AppIdentity = "Browser.exe", ProcessId = 456, Level = .7f });
            return s;
        }
        void VerifyAudioPanel()
        {
            SetPanel(6); Show(); window.UpdateLayout();
            if (audioBindings.Count != 4) throw new Exception("Falha: apps nao estao no painel principal.");
            audioBindings[0].Slider.Focus(); Navigate("DIREITA");
            if (!audioPending.ContainsKey("master-volume") || Math.Abs(audioPending["master-volume"].Level - .7f) > .001) throw new Exception("Falha: volume master.");
            audioBindings[1].Mute.Focus(); Activate();
            if (!audioPending.ContainsKey("mic-mute") || audioPending["mic-mute"].Muted || (string)audioBindings[1].Mute.ToolTip != "Silenciar microfone") throw new Exception("Falha: mute do microfone.");
            Navigate("DIREITA");
            if (Keyboard.FocusedElement != audioBindings[1].Slider) throw new Exception("Falha: navegacao mute/slider.");
            // ✕/Enter no slider nao muta nem mexe no volume; o foco continua nele (ADR 0085).
            Activate();
            if (audioPending["mic-mute"].Muted || audioBindings[1].Muted || audioPending.ContainsKey("mic-volume") ||
                Keyboard.FocusedElement != audioBindings[1].Slider) throw new Exception("Falha: slider do microfone mutou.");
            var app = audioBindings[2]; bool appMuted = app.Muted; app.Slider.Focus(); Activate();
            if (app.Muted != appMuted || AudioHasPending(app.Key + "-mute") || AudioHasPending(app.Key + "-volume") ||
                Keyboard.FocusedElement != app.Slider) throw new Exception("Falha: slider do app mutou.");
            audioPending.Clear();
            var original = audioSnapshot; audioSnapshot = ExampleAudio();
            audioSnapshot.Sessions.Add(new AudioSessionState { Id = "steam-other-output", DeviceId = "other", AppName = "Steam", AppIdentity = "Steam.exe", ProcessId = 123, Level = .5f });
            audioSnapshot.Sessions[0].Grouping = Guid.NewGuid();
            audioSnapshot.Sessions[2].Grouping = Guid.NewGuid();
            Guid group = Guid.NewGuid(); audioSnapshot.Sessions[1].Grouping = group;
            audioSnapshot.Sessions.Add(new AudioSessionState { Id = "browser-child", DeviceId = "out", AppName = "Navegador", AppIdentity = "Child.exe", ProcessId = 789, Grouping = group, Level = .7f });
            BuildAudioPanel(true);
            if (AudioApps().Count != 2) throw new Exception("Falha: apps duplicados por saida/processo.");
            var steam = audioBindings.Find(b => b.Key == "app-app:Steam.exe"); steam.Slider.Value = 25;
            if (audioPending.Count != 2) throw new Exception("Falha: roteamento de multiplas saidas.");
            foreach (var pending in audioPending.Values)
                if (pending.Kind != "session-volume" || pending.SessionIds.Length != 1 || Math.Abs(pending.Level - .25f) > .001) throw new Exception("Falha: alvo das sessoes.");
            audioSnapshot.Sessions.Add(new AudioSessionState { Id = "unrelated", DeviceId = "out", AppName = "Steam", AppIdentity = "Different.exe", ProcessId = 234, Level = 1 });
            if (AudioApps().Count != 3) throw new Exception("Falha: nome igual agrupou programas diferentes.");
            audioSnapshot.Sessions.RemoveAll(s => s.Id == "unrelated");
            audioPending.Clear(); window.UpdateLayout();
            double micY = audioBindings[1].Slider.TranslatePoint(new Point(0, 0), body).Y;
            double mixerY = audioAppsScroll.TranslatePoint(new Point(0, 0), body).Y;
            ToggleAudioDevices(1); window.UpdateLayout();
            if (audioExpanded != 1 || audioBindings.Count != 4 || !actions.Exists(c => (string)c.Tag == "pick-in")) throw new Exception("Falha: lista de entrada.");
            // A lista flutua: o slider do microfone e o mixer ficam onde estavam.
            if (Math.Abs(audioBindings[1].Slider.TranslatePoint(new Point(0, 0), body).Y - micY) > .5 ||
                Math.Abs(audioAppsScroll.TranslatePoint(new Point(0, 0), body).Y - mixerY) > .5) throw new Exception("Falha: lista flutuante empurrou os controles.");
            SaveRender("som-lista-flutuante");
            FocusAudioTag("input");
            for (int i = 0; i < 6; i++)
            {
                Navigate(i % 2 == 0 ? "BAIXO" : "CIMA"); Navigate("BAIXO");
                var trapped = (string)((Control)Keyboard.FocusedElement).Tag;
                if (trapped != "input" && !trapped.StartsWith("pick-", StringComparison.Ordinal)) throw new Exception("Falha: foco preso na lista aberta.");
            }
            FocusAudioTag("pick-in"); Activate();
            if (!audioPending.ContainsKey("default-1") || audioPending["default-1"].DeviceId != "in" || audioExpanded != -1) throw new Exception("Falha: selecao de entrada.");
            audioPending.Clear(); ToggleAudioDevices(0); AudioBack();
            if (audioExpanded != -1 || (string)((Control)Keyboard.FocusedElement).Tag != "output") throw new Exception("Falha: fechar lista inline.");
            for (int i = 0; i < 15; i++) audioSnapshot.Sessions.Add(new AudioSessionState { Id = "extra" + i, AppIdentity = "extra" + i, DeviceId = "out", AppName = "Teste " + i, Level = 1 });
            BuildAudioPanel(true); window.UpdateLayout();
            audioAppsScroll.ScrollToBottom(); window.UpdateLayout();
            if (audioAppsScroll.ScrollableHeight <= 0 || audioAppsScroll.VerticalOffset <= 0) throw new Exception("Falha: scroll independente dos apps.");
            SaveRender("som-mixer-rolagem");
            var last = audioBindings[audioBindings.Count - 1]; last.Slider.Focus(); window.UpdateLayout();
            double offset = audioAppsScroll.VerticalOffset;
            string tag = (string)last.Slider.Tag;
            audioSnapshot.Sessions.Add(new AudioSessionState { Id = "new", AppIdentity = "zzz", DeviceId = "out", AppName = "ZZZ", Level = 1 });
            RefreshAudioView(); window.UpdateLayout();
            if ((string)((Control)Keyboard.FocusedElement).Tag != tag || Math.Abs(audioAppsScroll.VerticalOffset - offset) > 1) throw new Exception("Falha: preservar foco/scroll no tracking.");
            audioSnapshot = new AudioSnapshot(); BuildAudioPanel(true);
            if (audioBindings[0].Slider.IsEnabled || audioBindings[1].Mute.IsEnabled) throw new Exception("Falha: estado sem dispositivos.");
            audioSnapshot = original; BuildAudioPanel(true); audioPending.Clear();
        }
        public void VerifyAudioLive()
        {
            // Leitura e renderizacao apenas: nenhum setter ou lancamento de configuracoes.
            audioSnapshot = Task.Factory.StartNew<AudioSnapshot>(delegate { return CoreAudio.Read(); }).Result;
            // O player no topo do Som tambem mostra a midia real, sem comandos.
            mediaSend = delegate { throw new InvalidOperationException("Diagnóstico sem comandos de mídia."); };
            try { mediaSessions = Task.Factory.StartNew<List<XGBP.Media.MediaSessionState>>(delegate { return XGBP.Media.MediaSessions.Read(); }).Result; }
            catch (AggregateException ex) { mediaSessions = null; mediaError = ex.InnerException.Message; }
            File.WriteAllText(Path.Combine(Output, "audio-leitura-real.txt"), audioSnapshot.Describe());
            var groups = AudioApps(); var report = new List<string>();
            report.Add(audioSnapshot.Sessions.Count + " sessoes -> " + groups.Count + " linhas; leitura sem setters");
            foreach (var group in groups) report.Add(group.Name + " | " + group.Key + " | sessoes=" + group.Sessions.Count);
            File.WriteAllLines(Path.Combine(Output, "audio-mixer-real.txt"), report.ToArray());
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); SetPanel(6); window.UpdateLayout(); SaveRender("som-real"); SaveRender("mixer-real");
            audioAppsScroll.ScrollToBottom(); window.UpdateLayout(); SaveRender("mixer-real-final");
            ToggleAudioDevices(1); window.UpdateLayout(); SaveRender("microfones-reais");
            ToggleAudioDevices(0); window.UpdateLayout(); SaveRender("saidas-reais");
            if (audioPending.Count != 0) throw new Exception("Falha: renderizacao enfileirou alteracao de audio.");
            window.Hide(); WriteLog("audio: leitura e renders reais concluidos, sem alteracoes");
        }
    }
}
