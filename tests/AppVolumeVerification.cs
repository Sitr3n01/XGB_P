// Volume do app em foco ao abrir a barra: mixer, janela, jogo e diario substituidos; nenhum audio real alterado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Audio;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyAppVolume()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) { if (!ok) throw new Exception("Volume do app: " + why); checks.Add("OK: " + why); };
            Func<float, float, bool> near = delegate(float a, float b) { return Math.Abs(a - b) < .005f; };
            var oldRead = appVolumeRead; var oldWrite = appVolumeWrite; var oldRun = appVolumeRun; var oldSleep = appVolumeSleep;
            var oldPrevious = previousWindow; var oldView = gamesView; var oldPreferences = launchPreferences; var oldLaunchWrite = launchWrite;
            var table = new List<SessionLevel>();
            Func<string, uint, float, SessionLevel> add = delegate(string app, uint pid, float level) {
                var s = new SessionLevel { DeviceId = "saida", SessionId = app + "|" + pid, SessionKey = app, ProcessId = pid, Level = level };
                table.Add(s); return s;
            };
            try
            {
                lock (appVolumeLock) { appVolumeDucked.Clear(); appVolumePending.Clear(); }
                if (File.Exists(appVolumeJournal)) File.Delete(appVolumeJournal);
                appVolumeRead = delegate(ICollection<uint> pids, string exe) { return table.FindAll(s => pids == null || pids.Contains(s.ProcessId)); };
                appVolumeWrite = delegate(IList<SessionLevel> items) {
                    int matched = 0;
                    foreach (var item in items)
                    {
                        var s = table.Find(t => t.DeviceId == item.DeviceId && t.SessionId == item.SessionId);
                        if (s != null) { s.Level = item.Level; matched++; }
                    }
                    return matched;
                };
                appVolumeRun = delegate(Action work) { work(); }; appVolumeSleep = delegate { };
                launchPreferences = new LaunchPreferenceState(); launchWrite = delegate { };
                var gameWindow = new DesktopWindow { Handle = new IntPtr(4242), ProcessId = 500, ProcessCreated = 1, Title = "Jogo", AppName = "Jogo" };
                var game = new GameActivity { Key = "steam:1:42", Pid = 500, Window = gameWindow }; game.Pids.Add(500); game.Pids.Add(501);
                gamesView = new GamesView(); gamesView.Activities.Add(game); gamesView.Current = game;
                previousWindow = new WindowObservation { Window = gameWindow };
                var main = add("jogo", 500, .8f); var child = add("filho", 501, 1f); var other = add("musica", 600, .6f);

                ReduceAppVolumeOnOpen();
                check(near(main.Level, .4f) && near(child.Level, .5f) && near(other.Level, .6f) && File.Exists(appVolumeJournal),
                    "abrir reduz pela metade o jogo e os processos dele, sem tocar os outros apps");
                ReduceAppVolumeOnOpen(); check(near(main.Level, .4f) && near(child.Level, .5f), "nova abertura nao reduz sobre o nivel ja reduzido");
                RestoreAppVolumeOnClose("teste");
                check(near(main.Level, .8f) && near(child.Level, 1f) && !File.Exists(appVolumeJournal), "fechar devolve o volume original e apaga o diario");
                ReduceAppVolumeOnOpen(); main.Level = .7f; RestoreAppVolumeOnClose("teste");
                check(near(main.Level, .7f) && near(child.Level, 1f), "ajuste feito no mixer com a barra aberta e mantido");
                main.Level = .8f;

                launchPreferences = new LaunchPreferenceState { KeepAppVolume = true }; ReduceAppVolumeOnOpen();
                check(near(main.Level, .8f), "opcao desligada nao muda o som"); launchPreferences = new LaunchPreferenceState();
                previousWindow = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(1), ProcessId = OwnProcessId } };
                    ReduceAppVolumeOnOpen();
                previousWindow = null; ReduceAppVolumeOnOpen();
                check(near(main.Level, .8f) && near(other.Level, .6f), "sem app na frente, ou com a propria barra, nada muda");
                previousWindow = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(77), ProcessId = 600, ProcessCreated = 1 } };
                ReduceAppVolumeOnOpen(); check(near(other.Level, .3f) && near(main.Level, .8f), "app comum em foco tambem e reduzido sozinho");
                RestoreAppVolumeOnClose("teste"); check(near(other.Level, .6f), "app comum volta ao fechar");
                previousWindow = new WindowObservation { Window = gameWindow };

                int steps = 0; appVolumeSleep = delegate { if (++steps == 2) RestoreAppVolumeOnClose("teste"); };
                ReduceAppVolumeOnOpen(); appVolumeSleep = delegate { };
                check(near(main.Level, .8f) && near(child.Level, 1f) && !File.Exists(appVolumeJournal),
                    "fechar no meio da rampa de reducao devolve o original");

                // O Windows guarda o nivel por app: quem fecha com o som reduzido volta ao original quando reabrir.
                ReduceAppVolumeOnOpen(); table.Remove(main); RestoreAppVolumeOnClose("teste");
                check(File.Exists(appVolumeJournal) && near(child.Level, 1f), "sessao encerrada antes de fechar a barra fica no diario sem erro");
                var reopened = add("jogo", 502, .4f); lock (appVolumeLock) RecoverAppVolume("teste");
                check(near(reopened.Level, .8f) && !File.Exists(appVolumeJournal), "app reaberto com o nivel deixado pela barra volta ao original");
                table.Remove(reopened); main = add("jogo", 500, .8f);
                ReduceAppVolumeOnOpen(); table.Remove(main); RestoreAppVolumeOnClose("teste");
                var changed = add("jogo", 503, .95f); lock (appVolumeLock) RecoverAppVolume("teste");
                check(near(changed.Level, .95f) && !File.Exists(appVolumeJournal), "nivel diferente do deixado pela barra e escolha do usuario");
                table.Remove(changed); main = add("jogo", 500, .4f);

                GameFiles.Write(appVolumeJournal, new AppVolumeJournal { Entries = new List<AppVolumeEntry> {
                    new AppVolumeEntry { DeviceId = "saida", SessionId = "jogo|1", SessionKey = "jogo", Original = .8f, Target = .4f,
                        SavedUtc = DateTime.UtcNow.ToFileTimeUtc() },
                    new AppVolumeEntry { DeviceId = "saida", SessionId = "filho|1", SessionKey = "filho", Original = .2f, Target = .1f,
                        SavedUtc = DateTime.UtcNow.AddDays(-8).ToFileTimeUtc() } } });
                child.Level = .1f;
                lock (appVolumeLock) { LoadAppVolumeJournal(); RecoverAppVolume("teste"); }
                check(near(main.Level, .8f) && near(child.Level, .1f) && !File.Exists(appVolumeJournal),
                    "diario de uma queda restaura pelo identificador persistente e descarta entradas antigas");
                File.WriteAllText(appVolumeJournal, "{invalido", Encoding.UTF8);
                lock (appVolumeLock) LoadAppVolumeJournal(); check(File.Exists(appVolumeJournal),
                    "diario ilegivel nao derruba a barra nem e apagado na leitura");
                File.Delete(appVolumeJournal); child.Level = 1f;

                Hide(); Show(); previousWindow = new WindowObservation { Window = gameWindow }; ReduceAppVolumeOnOpen();
                check(near(main.Level, .4f), "reducao com a barra aberta pelo caminho normal");
                Hide(); check(near(main.Level, .8f) && near(child.Level, 1f), "ocultar a barra restaura pelo caminho normal");
                Show(); SetPanel(5); window.UpdateLayout();
                var toggle = actions.Find(c => (string)c.Tag == "app-volume") as SettingToggle;
                check(toggle != null && toggle.State == true, "interruptor aparece ligado por padrao em Configuracoes");
                toggle.Focus(); window.UpdateLayout(); SaveRender("configuracoes-volume-do-app");
                ToggleAppVolumeSetting(); check(launchPreferences.KeepAppVolume, "desligar salva a preferencia");
            }
            finally
            {
                appVolumeRead = oldRead; appVolumeWrite = oldWrite; appVolumeRun = oldRun; appVolumeSleep = oldSleep;
                previousWindow = oldPrevious; gamesView = oldView; launchPreferences = oldPreferences; launchWrite = oldLaunchWrite;
                lock (appVolumeLock) { appVolumeDucked.Clear(); appVolumePending.Clear(); }
                foreach (string path in new[] { appVolumeJournal, appVolumeJournal + ".bak" }) if (File.Exists(path)) File.Delete(path);
                Hide(); SetPanel(0);
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do volume do app ao abrir a barra.");
            checks.Add("Mixer do Windows, janela da frente, jogos e diario substituidos; nenhum volume real alterado.");
            File.WriteAllLines(Path.Combine(Output, "volume-app-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}
