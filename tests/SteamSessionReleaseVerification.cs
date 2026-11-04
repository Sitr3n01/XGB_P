// Regressao do fim da sessao: arquivos isolados, relogio e efeitos substituidos.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifySteamSessionRelease()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) {
                if (!ok) throw new Exception("Sessao Steam: " + why); checks.Add("OK: " + why);
            };
            VerifySteamSessionLog(check);
            VerifySteamSessionWait(check);
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do encerramento da sessao Steam.");
            checks.Add("Somente fixtures e arquivos dentro da pasta de teste. Sem protocolo, jogo, Steam ou configuracao real operado.");
            File.WriteAllLines(Path.Combine(Output, "steam-sessao-verificacao.txt"), checks, Encoding.UTF8);
        }
        void VerifySteamSessionLog(Action<bool, string> check)
        {
            string folder = Path.Combine(Output, "steam-session-tests"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "console.txt");
            DateTime now = new DateTime(2026, 10, 28, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
            const uint app = 3899276376, rootPid = 42, launcher = 10;
            const string command = "\"\"wscript.exe\" \"GenshinKeeperHidden.vbs\"\"";
            Func<string, uint, uint, string, string> line = delegate(string action, uint id, uint pid, string cmd) {
                return "[" + now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "] Game process " + action +
                    (action == "removed" ? ":" : " :") + " AppID " + id + " " + cmd + ", ProcID " + pid + (action == "removed" ? " " : ", IP 0.0.0.0:0") + "\n";
            };
            var steam = new GameProcess { Pid = 20, Name = "steam.exe", Created = now.AddHours(-1).ToFileTimeUtc() };
            var processes = new List<GameProcess> { steam };
            var game = TransitionFixture("shortcut:1:" + app, rootPid, 43, 1);
            game.Processes.Clear(); game.Processes.Add(new ProcessIdentity(rootPid, now.AddSeconds(-1).ToFileTimeUtc()));
            var view = new GamesView(); view.Entries.Add(new GameEntry { Key = game.Key, Name = "Genshin", Kind = "Game" });
            view.Activities.Add(game);
            SteamSessionRelease monitor = null;
            Action reset = delegate { File.WriteAllText(path, "", Encoding.UTF8); monitor = new SteamSessionRelease(path); view.Activities.Clear(); view.Activities.Add(game); };
            Func<SteamReleaseSnapshot> read = delegate { return monitor.Update(processes, view, now); };
            reset();
            var parsed = SteamSessionEvent.Parse(line("added", app, launcher, command).TrimEnd('\n'));
            check(parsed != null && parsed.AppId == app.ToString() && parsed.Pid == launcher && parsed.Action == "added", "parser reconhece o evento e atalho unsigned");
            check(SteamSessionEvent.Parse(line("removed", app, rootPid, command).Replace(app.ToString(), "-395690920").TrimEnd('\n')).AppId == app.ToString(), "parser normaliza atalho signed");
            check(SteamSessionEvent.Parse("OnFocusWindowChanged AppID 769") == null &&
                SteamSessionEvent.Parse(line("removed", app, rootPid, command).Replace("ProcID 42", "ProcID 0").TrimEnd('\n')) == null,
                "contexto 769 e identidade invalida nao equivalem a fim de sessao");
            File.AppendAllText(path, line("added", app, launcher, command));
            var added = read();
            check(!added.Ready && added.Confirmed.Count == 0, "sessao adicionada permanece ocupada e nao anuncia encerramento");
            view.Activities.Clear(); now = now.AddSeconds(26);
            check(!read().Ready, "processo nativo ausente nao libera o lancador de 25 segundos");
            File.AppendAllText(path, line("removed", 2420660, rootPid, command));
            check(!read().Ready, "remocao de outro AppID nao libera a sessao anterior");
            File.AppendAllText(path, line("removed", app, 99, command));
            check(!read().Ready, "PID de outra sessao nao libera o jogo observado");
            File.AppendAllText(path, line("removed", app, rootPid, "\"outro.exe\""));
            check(!read().Ready, "comando de outro lancador nao libera a sessao");
            string removed = line("removed", app, rootPid, command);
            File.AppendAllText(path, removed.TrimEnd('\n'));
            check(!read().Ready, "linha parcial nao confirma fim da sessao");
            File.AppendAllText(path, "\n");
            var ended = read();
            check(ended.Ready && ended.Confirmed.Count == 1 && read().Confirmed.Count == 0, "evento correto libera uma vez e leitura seguinte nao repete confirmacao antiga");
            reset(); File.AppendAllText(path, line("added", app, launcher, command)); read();
            // Reproduz o snapshot que ainda contem o jogo no instante em que a Steam remove a sessao.
            File.AppendAllText(path, line("removed", app, rootPid, command));
            check(read().Ready, "snapshot nativo ainda presente nao recria sessao ja confirmada para a mesma identidade");
            reset(); view.Activities.Clear();
            File.AppendAllText(path, line("added", app, launcher, command) + line("removed", app, rootPid, command));
            check(read().Ready, "bootstrap aceita que a Steam troque o PID do lancador pelo runtime no encerramento");
            reset(); view.Activities.Clear();
            File.AppendAllText(path, line("removed", app, rootPid, command) + line("added", app, launcher, command));
            check(!read().Ready, "remocao antiga nao libera a sessao adicionada depois");
            reset(); File.AppendAllText(path, line("added", app, launcher, command)); read();
            File.AppendAllText(path, line("added", app, 11, command) + line("removed", app, rootPid, command));
            check(!read().Ready, "duas sessoes sobrepostas do mesmo AppID sao incertas");
            reset(); read();
            File.AppendAllText(path, line("removed", app, rootPid, command));
            check(read().Ready, "jogo observado antes da linha added exige remocao nova do PID exato");
            reset(); File.AppendAllText(path, line("added", app, launcher, command)); read();
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                check(!read().Readable, "log sem acesso nao reutiliza estado pronto anterior");
            check(!read().Ready, "reabrir o arquivo preserva a pendencia");
            File.WriteAllText(path, "", Encoding.UTF8);
            check(!read().Ready, "truncamento com sessao pendente impede confirmacao falsa");
            processes[0] = new GameProcess { Pid = steam.Pid, Name = "steam.exe", Created = now.AddSeconds(1).ToFileTimeUtc() };
            view.Activities.Clear();
            check(read().Ready, "cliente Steam novo nao herda pendencia do processo anterior");
            processes[0] = steam;
            reset(); File.AppendAllText(path, line("added", app, launcher, command)); read();
            string replaced = path + ".replaced"; File.Copy(path, replaced, true); File.Delete(path); File.Move(replaced, path);
            check(!read().Ready, "substituicao do arquivo por outro do mesmo tamanho e detectada pela identidade nativa");
            reset(); view.Activities.Clear();
            var tool = new GameEntry { Key = "steam:1:123", Name = "Ferramenta", Kind = "Tool" }; view.Entries.Add(tool);
            File.AppendAllText(path, line("added", 123, 100, "\"DSX.exe\""));
            check(read().Ready, "ferramentas da Steam nao bloqueiam jogos"); view.Entries.Remove(tool);
            reset(); view.Activities.Clear();
            File.AppendAllText(path, line("added", app, launcher, command));
            now = now.AddSeconds(2); processes[0] = new GameProcess { Pid = 20, Name = "steam.exe", Created = now.ToFileTimeUtc() };
            check(read().Ready, "eventos de cliente anterior sao ignorados pelo horario de criacao"); processes[0] = steam;
            reset(); view.Activities.Clear();
            var filler = new StringBuilder(); for (int i = 0; i < 20000; i++) filler.Append("linha sem evento\n");
            File.WriteAllText(path, filler.ToString() + line("added", app, launcher, command), Encoding.UTF8);
            check(!read().CaughtUp, "bootstrap grande e lido em lotes sem bloquear a interface");
            check(!read().Ready, "fim do bootstrap preserva a sessao ainda ativa");
            var missing = new SteamSessionRelease(Path.Combine(folder, "ausente.txt"));
            check(!missing.Update(processes, view, now).Ready, "log ausente nao permite uma abertura presumida");
            check(missing.Update(new List<GameProcess>(), view, now).Ready, "sem cliente Steam nao ha sessao antiga para liberar");
            processes[0] = new GameProcess { Pid = 20, Name = "steam.exe" };
            check(!read().Ready, "cliente sem identidade de criacao permanece incerto");
        }
        void VerifySteamSessionWait(Action<bool, string> check)
        {
            var oldView = gamesView; var oldRead = steamSessionRead; var oldNow = feedbackNow; var oldLaunch = gameLaunch;
            var oldClose = gameCloseWindow; var oldExit = gameHasExited; var oldMode = activationRead; var oldModeValue = activationMode;
            var oldFront = foregroundRead; var oldFind = findBigPicture; var oldAvailable = existingWindowAvailable;
            var oldOwns = appOwnsForeground; var oldFocus = controllerFocus; var oldFallback = gameFallback;
            var oldPad = controllerNow; bool oldFresh = gamesSnapshotFresh; long oldVersion = gamesSnapshotVersion;
            long oldSteamVersion = steamSessionVersion;
            try
            {
                DateTime now = new DateTime(2026, 10, 28, 16, 0, 0, DateTimeKind.Utc); IntPtr front = handle;
                bool exited = false; int launches = 0, closes = 0;
                var pending = new SteamReleaseSnapshot { Readable = true, CaughtUp = true }; pending.Pending.Add("Genshin");
                var release = pending;
                steamSessionRead = delegate { return release; }; feedbackNow = controllerNow = delegate { return now; };
                activationRead = delegate { return true; }; activationMode = true;
                foregroundRead = delegate { return front; }; controllerFocus = delegate { return front == handle; };
                findBigPicture = delegate { return new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(777), ProcessId = 77, ProcessCreated = 1 } }; };
                existingWindowAvailable = delegate { return true; }; appOwnsForeground = delegate(DesktopWindow w) { return w.Handle == front; };
                gameFallback = delegate(IntPtr w) { return w == IntPtr.Zero; };
                gameHasExited = delegate { return exited; }; gameCloseWindow = delegate { closes++; }; gameLaunch = delegate { launches++; };
                Action reset = delegate {
                    Hide(); EndGameLaunchRoute(); launchingGame = unconfirmedLaunch = closingGame = gameBusy = null;
                    closingActivity = observedForegroundGame = null; gameExitRoute = null; ClearGuide(); ClearFeedback();
                    exited = false; launches = closes = 0; release = pending;
                    gamesSnapshotFresh = true; gamesSnapshotVersion = long.MaxValue; steamInputPending = false;
                    steamSessionVersion = long.MaxValue;
                    gamesView = ExampleGames(); gamesView.Current.Key = gamesView.Entries[0].Key = "shortcut:1:3899276376";
                    gamesView.Entries[1].Key = "steam:1:2420660"; SetPanel(0); Show(); front = handle;
                };
                Action<int> tick = delegate(int ms) { now = now.AddMilliseconds(ms); TickGameLaunchRoute(); };
                Func<SteamReleaseSnapshot> ready = delegate { return new SteamReleaseSnapshot { Readable = true, CaughtUp = true, Utc = now }; };
                reset(); SelectGame("steam:1:2420660"); ConfirmGame();
                check(closes == 1 && launches == 0 && !window.IsVisible, "troca continua pedindo fechamento normal e ocultando a barra");
                exited = true; gamesView.Activities.Clear(); CheckGameClose(true, gameCommandVersion);
                check(gameLaunchRoute != null && gameLaunchRoute.WaitingSteam && launches == 0, "fim nativo nao despacha o Neva com sessao Steam pendente");
                tick(26000); check(launches == 0, "25 segundos sozinhos nao equivalem a confirmacao");
                release = ready(); tick(0); check(launches == 0, "liberacao observada aguarda breve estabilizacao");
                tick(800); tick(1000);
                check(launches == 1 && gameLaunchRoute == null && launchingGame == "steam:1:2420660", "liberacao real libera uma unica abertura");
                reset(); gamesView.Activities.Clear(); gamesView.Current = null; RequestGameLaunch("steam:1:2420660");
                check(closes == 0 && launches == 0 && gameLaunchRoute.WaitingSteam && window.IsVisible,
                    "recente escolhido apos fechamento manual tambem espera a sessao");
                RenderGames(true); SettleMotion(); window.UpdateLayout();
                check(gameResumeRow.Count == 1, "espera apos fechamento manual apresenta ultimo jogo sem botoes de processo");
                SaveRender("home-aguardando-sessao-steam");
                release = new SteamReleaseSnapshot { Readable = true, CaughtUp = false }; tick(1000);
                check(launches == 0, "bootstrap incompleto nao libera o jogo");
                release = new SteamReleaseSnapshot { Error = "log ilegivel" }; tick(44001);
                check(launches == 0 && gameLaunchRoute == null, "45 segundos sem confirmacao cancelam sem abrir por cima");
                reset(); gamesView.Activities.Clear(); release = ready(); steamSessionVersion = gameCommandVersion - 1;
                RequestGameLaunch("steam:1:2420660"); tick(1000);
                check(launches == 0, "leitura Steam anterior ao comando nao libera a abertura");
                steamSessionVersion = long.MaxValue; release.Utc = now.AddSeconds(-6); tick(1000);
                check(launches == 0, "snapshot antigo nao equivale a sessao liberada agora");
                release = ready(); tick(0); tick(800);
                check(launches == 1, "leitura atual e posterior ao comando libera a mesma escolha pendente");
                reset(); gamesView.Activities.Clear(); RequestGameLaunch("steam:1:2420660"); Show(); release = ready(); tick(1000);
                check(launches == 0 && gameLaunchRoute == null, "reabrir cancela resposta tardia do observador");
                release = pending; RequestGameLaunch("steam:1:2420660");
                check(gameLaunchRoute != null && launches == 0, "cancelar a escolha nao apaga a pendencia real da Steam");
                front = new IntPtr(999); tick(100); release = ready(); tick(1000);
                check(launches == 0 && gameLaunchRoute == null, "outro aplicativo escolhido impede abertura tardia");
                reset(); gamesView.Activities.Clear(); RequestGameLaunch("steam:1:2420660"); activationEpoch++; tick(100);
                check(launches == 0 && gameLaunchRoute == null, "mudanca de modo cancela a espera");
                reset(); gamesView.Activities.Clear(); RequestGameLaunch("steam:1:2420660"); release = ready(); tick(0);
                buttonsHeldAt["fixture:X"] = now; tick(800);
                check(launches == 0, "botao apertado no fim da espera nao vai para o jogo");
                buttonsHeldAt.Clear(); ButtonsReleased();
                check(launches == 1, "soltura apos liberacao conclui a abertura");
                reset(); gamesView.Activities.Clear(); release = ready(); RequestGameLaunch("steam:1:2420660");
                check(launches == 1 && gameLaunchRoute == null, "sem sessao pendente primeira abertura continua direta");
            }
            finally
            {
                EndGameLaunchRoute(); Hide(); launchingGame = unconfirmedLaunch = closingGame = gameBusy = null;
                closingActivity = observedForegroundGame = null; gameExitRoute = null; ClearGuide();
                gamesView = oldView; steamSessionRead = oldRead; feedbackNow = oldNow; gameLaunch = oldLaunch;
                gameCloseWindow = oldClose; gameHasExited = oldExit; activationRead = oldMode; activationMode = oldModeValue;
                foregroundRead = oldFront; findBigPicture = oldFind; existingWindowAvailable = oldAvailable; appOwnsForeground = oldOwns;
                controllerFocus = oldFocus; gameFallback = oldFallback; controllerNow = oldPad;
                gamesSnapshotFresh = oldFresh; gamesSnapshotVersion = oldVersion; SetPanel(0); ClearFeedback();
                steamSessionVersion = oldSteamVersion;
            }
        }
    }
}
