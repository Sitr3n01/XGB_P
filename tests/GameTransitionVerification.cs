// Regressao de dois/tres jogos, Steam tardia e saida. Todas as acoes/foco/contexto sao substituidos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyGameTransitions()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) {
                if (!ok) throw new Exception("Transicoes de jogos: " + why); checks.Add("OK: " + why);
            };
            VerifyGameContextParser(check);
            VerifyGameContextCursor(check);
            var oldView = gamesView; var oldNow = feedbackNow; var oldRead = foregroundRead;
            var oldAvailable = appAvailable; var oldOwns = appOwnsForeground; var oldFront = gameForeground;
            var oldSwitch = switchGameWindow; var oldRelay = gameRelayFocus; var oldFallback = gameFallback;
            var oldCursor = gameContextCursor; var oldExited = gameHasExited; var oldObserve = observeWindow;
            var oldControllerFocus = controllerFocus;
            var oldMode = activationRead; var oldModeValue = activationMode; int oldEpoch = activationEpoch;
            var oldClose = gameCloseWindow; var oldResume = gameResume; var oldPrevious = previousWindow;
            var oldSteamLaunch = steamLaunch; var oldFindSteam = findBigPicture; var oldSteamAvailable = existingWindowAvailable;
            var oldSteamRunning = steamInputAvailable;
            bool oldSnapshotFresh = gamesSnapshotFresh; long oldSnapshotVersion = gamesSnapshotVersion;
            try
            {
                DateTime now = new DateTime(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc);
                IntPtr front = IntPtr.Zero, fallback = new IntPtr(9000), otherApp = new IntPtr(9001);
                string context = null; int sent = 0, relays = 0, closeRequests = 0, resumes = 0, launches = 0;
                bool deliver = true, recoverContext = true, relayAccepted = true;
                var exited = new HashSet<string>(); var unavailable = new HashSet<IntPtr>();
                GameActivity a = null, b = null, c = null;
                var steam = new DesktopWindow { Handle = new IntPtr(770), ProcessId = 77, ProcessCreated = 1, AppName = "Steam" };
                feedbackNow = delegate { return now; }; foregroundRead = delegate { return front; };
                controllerFocus = delegate { return front == handle; };
                activationRead = delegate { return true; }; activationMode = true;
                appAvailable = delegate(DesktopWindow target) { return !unavailable.Contains(target.Handle); };
                appOwnsForeground = delegate(DesktopWindow target) { return front == target.Handle; };
                gameForeground = delegate(DesktopWindow target) { return front == target.Handle && !unavailable.Contains(target.Handle); };
                gameFallback = delegate(IntPtr hwnd) { return hwnd == fallback; };
                gameHasExited = delegate(GameActivity game) { return exited.Contains(game.Key); };
                gameRelayFocus = delegate { relays++; if (relayAccepted) front = handle; return relayAccepted; };
                gameContextCursor = delegate { return delegate { return context; }; };
                observeWindow = delegate(IntPtr hwnd) {
                    var game = gamesView == null ? null : gamesView.Activities.Find(g => g.Window != null && g.Window.Handle == hwnd);
                    return game == null ? null : new WindowObservation { Window = game.Window };
                };
                switchGameWindow = delegate(DesktopWindow target) {
                    sent++; if (deliver) front = target.Handle;
                    if (recoverContext) context = target == steam ? "769" : gameTransition.AppId;
                };
                gameCloseWindow = delegate { closeRequests++; };
                gameResume = delegate { resumes++; return null; };
                steamLaunch = delegate(string uri) { launches++; check(uri == SteamStartPageUri || uri == SteamHomeUri, "retorno so despacha destinos Steam previstos"); };
                findBigPicture = delegate { return new WindowObservation { Window = steam }; };
                existingWindowAvailable = delegate { return true; }; steamInputAvailable = delegate { return true; };
                Action<int> step = delegate(int milliseconds) { now = now.AddMilliseconds(milliseconds); TickGameExitRoute(); TickGameTransition(); };
                Action reset = delegate {
                    Hide(); ClearFeedback(); steamInputPending = steamHideDeferred = steamHideForGame = false;
                    closingGame = null; closingActivity = null; gameBusy = null; observedForegroundGame = null;
                    gamesSnapshotFresh = true; gamesSnapshotVersion = long.MaxValue; gameConfirmation = null;
                    gameCloseConfirmation = null; gameCloseForce = false; pausedByPreview.Clear();
                    exited.Clear(); unavailable.Clear(); sent = relays = closeRequests = resumes = launches = 0;
                    deliver = recoverContext = relayAccepted = true; context = null; SetPanel(0);
                    gamesView = ExampleGames(); a = gamesView.Current; a.Key = gamesView.Entries[0].Key = "steam:1:2420660";
                    a.Window.ProcessCreated = 1; a.Processes.Add(new ProcessIdentity(a.Pid, 1)); a.LastFocused = 20;
                    b = TransitionFixture("shortcut:1:3899276376", 600, 601, 10);
                    c = TransitionFixture("steam:1:333", 700, 701, 5);
                    gamesView.Activities.Add(b); gamesView.Entries[1].Key = b.Key;
                    front = a.Window.Handle; Show(); front = handle;
                };
                Action finish = delegate { step(0); step(0); step(800); };
                Action closeActive = delegate {
                    AskCloseGame(a.Key, "resume"); ConfirmCloseGame();
                    check(closeRequests == 1 && closingGame == a.Key && !window.IsVisible && gameExitRoute != null && !gameExitRoute.Ready,
                        "pedido aceito oculta e arma retorno sem antecipar a saida");
                };
                Action exitActive = delegate {
                    exited.Add(a.Key); gamesView.Activities.Remove(a); gamesView.Current = b; front = fallback;
                    CheckGameClose(true, gameCommandVersion);
                };

                reset(); steamInputPending = true; ResumeOpenGame(b.Key, b);
                step(1000); check(sent == 0 && window.IsVisible, "jogo aguarda toda a liberacao Steam, mesmo com navegacao ja liberada");
                steamInputPending = false; step(0);
                check(sent == 1 && !window.IsVisible && gameTransition != null, "primeiro foco oculta mas nao conclui a transicao");
                step(300); check(gameTransition != null, "uma observacao breve nao anuncia estabilidade");
                step(500); check(gameTransition == null, "destino e contexto estaveis concluem sem relancar o jogo");

                reset(); ResumeOpenGame(b.Key, b); step(100); context = SteamInput.DesktopAppId; step(300);
                check(gameTransition != null && relays == 1 && sent == 1, "desktop residual detectado enquanto Windows ainda mostra o jogo");
                finish(); check(sent == 2 && gameTransition == null && front == b.Window.Handle, "uma ponte sem Alt refaz a escolha e estabiliza o jogo");
                reset(); recoverContext = false; context = SteamInput.DesktopAppId; ResumeOpenGame(b.Key, b); step(400); finish();
                step(9000); check(sent == 2 && relays == 1 && gameTransition == null && !window.IsVisible,
                    "desktop persistente termina com limite e sem ciclo infinito ou falsa conclusao");

                // ClientUI com o jogo na frente (19:54:57): refazer o foco devolve a interface da Steam; aviso sem ponte (ADR 0066).
                Func<FeedbackKind?> notice = delegate { FeedbackNotice n; return feedbackNotices.TryGetValue("0", out n) ? n.Kind : (FeedbackKind?)null; };
                reset(); ResumeOpenGame(b.Key, b); step(100); context = SteamInput.ClientUiAppId; step(300);
                check(gameTransition != null && relays == 0 && sent == 1, "ClientUI com o jogo na frente nao refaz o foco");
                step(400);
                check(gameTransition == null && relays == 0 && sent == 1 && !window.IsVisible && front == b.Window.Handle && observedForegroundGame == null &&
                    notice() == FeedbackKind.Attention, "ClientUI persistente termina com aviso, sem ponte e sem anunciar estabilidade");
                reset(); ResumeOpenGame(b.Key, b); step(100); context = SteamInput.ClientUiAppId; step(300); context = gameTransition.AppId; step(500);
                check(gameTransition == null && relays == 0 && sent == 1 && notice() == FeedbackKind.Success, "ClientUI passageira seguida do jogo conclui sem aviso");
                reset(); recoverContext = false; context = SteamInput.DesktopAppId; ResumeOpenGame(b.Key, b); step(400);
                context = SteamInput.ClientUiAppId; finish();
                check(gameTransition == null && relays == 1 && sent == 2 && notice() == FeedbackKind.Attention, "ClientUI depois da ponte tambem termina com aviso, sem segunda ponte");

                reset(); ResumeOpenGame(b.Key, b); front = otherApp; step(200);
                check(gameTransition == null && relays == 0 && sent == 1 && front == otherApp, "escolha de outro app cancela apos ocultar sem recuperar foco");
                reset(); deliver = false; ResumeOpenGame(b.Key, b); Hide(); step(1000);
                check(gameTransition == null && sent == 1, "fechamento manual invalida pedidos tardios");
                reset(); deliver = false; ResumeOpenGame(b.Key, b); SetPanel(5); step(1000);
                check(gameTransition == null && sent == 1, "troca de painel cancela a transicao");
                reset(); ResumeOpenGame(b.Key, b); activationEpoch++; step(1000);
                check(gameTransition == null, "mudanca do ciclo FSE cancela a confirmacao pendente");
                reset(); ResumeOpenGame(b.Key, b); activationRead = delegate { return false; }; step(1000);
                check(gameTransition == null && relays == 0, "saida real da API cancela antes do observador atualizar o cache");
                activationRead = delegate { return true; };
                reset(); ResumeOpenGame(b.Key, b); activationRead = delegate { return null; }; step(1000);
                check(gameTransition == null && relays == 0, "API incerta cancela sem recuperar foco");
                activationRead = delegate { return true; };
                reset(); ResumeOpenGame(b.Key, b); now = now.AddMilliseconds(200); front = handle;
                Show(); front = handle; ResumeOpenGame(a.Key, a); finish();
                check(front == a.Window.Handle && gameTransition == null, "escolha nova substitui a antiga por identidade");

                reset(); OpenApp(b.Window); finish();
                check(sent == 1 && gameTransition == null, "icone de app que pertence a jogo usa o mesmo fluxo de jogos");
                reset(); unavailable.Add(b.Window.Handle); ResumeOpenGame(b.Key, b);
                check(sent == 0 && window.IsVisible && gameTransition == null, "janela indisponivel nao recebe comando nem provoca relancamento");
                reset(); ResumeOpenGame(b.Key, b); gamesView.Activities.Remove(b);
                var recycled = TransitionFixture(b.Key, b.Pid, 601, 10); recycled.Processes.Clear(); recycled.Processes.Add(new ProcessIdentity(b.Pid, 2));
                gamesView.Activities.Add(recycled); step(200);
                check(sent == 1 && gameTransition == null && relays == 0, "PID reutilizado nao herda confirmacao ou recuperacao da escolha antiga");
                reset(); deliver = false; ResumeOpenGame(b.Key, b); step(300); step(300); step(300); step(9000);
                check(sent == 3 && relays == 0 && window.IsVisible, "recusa de foco tem tres pedidos e preserva a barra");

                reset(); closeActive(); step(1000);
                check(sent == 0 && gameExitRoute != null && !gameExitRoute.Ready, "confirmacao do proprio jogo conserva o destino enquanto ele existe");
                exitActive(); finish();
                check(front == b.Window.Handle && gameTransition == null && gameExitRoute == null, "fechar Neva entrega o Genshin sobrevivente");

                reset(); var swapped = a; a = b; b = swapped; gamesView.Current = a;
                Hide(); front = a.Window.Handle; Show(); front = handle; closeActive(); exitActive(); finish();
                check(front == b.Window.Handle, "fechar Genshin entrega o Neva sobrevivente");

                reset(); AskCloseGame(b.Key, "recent-close-" + b.Key); ConfirmCloseGame();
                check(closeRequests == 1 && gameExitRoute == null, "fechar jogo em segundo plano nao arma troca do jogo atual");
                exited.Add(b.Key); gamesView.Activities.Remove(b); CheckGameClose(true, gameCommandVersion); step(1000);
                check(sent == 0, "saida do jogo de fundo nao ativa outro destino");

                reset(); gamesView.Activities.Add(c); c.LastFocused = 30; closeActive(); exitActive(); finish();
                check(front == c.Window.Handle, "entre tres jogos o sobrevivente usado mais recentemente recebe o foco");
                reset(); b.Paused = true;
                pausedByPreview[b.Key] = new PausedGame { Pid = b.Pid, Members = new Dictionary<uint, long> { { b.Pid, 1 } } };
                var resumeWithoutFocus = gameResume;
                gameResume = delegate { resumes++; front = b.Window.Handle; return null; };
                closeActive(); exitActive(); finish();
                check(resumes == 1 && !b.Paused && front == b.Window.Handle && gameTransition == null, "retorno retoma pausa propria mesmo se o jogo ganhar foco na chamada nativa");
                gameResume = resumeWithoutFocus;
                reset(); b.Paused = true;
                var focusBeforeResume = controllerFocus; controllerFocus = delegate { return false; };
                gameResume = delegate { resumes++; front = b.Window.Handle; return null; };
                ResumeOpenGame(b.Key, b); finish();
                check(resumes == 1 && front == b.Window.Handle && gameTransition == null && !window.IsVisible,
                    "retomada explicita aceita somente o proprio destino que tomou foco antes do callback");
                controllerFocus = focusBeforeResume; gameResume = resumeWithoutFocus;
                reset(); b.Paused = true;
                pausedByPreview[b.Key] = new PausedGame { Pid = b.Pid, Members = new Dictionary<uint, long> { { b.Pid, 1 } } };
                var contextBeforeResume = gameContextCursor; int cursors = 0;
                gameContextCursor = delegate { int generation = ++cursors; return delegate { return generation == 1 ? SteamInput.DesktopAppId : null; }; };
                gameResume = delegate {
                    resumes++; var refreshed = TransitionFixture(b.Key, b.Pid, 601, b.LastFocused);
                    gamesView.Activities.Remove(b); b = refreshed; gamesView.Activities.Add(b); front = b.Window.Handle; return null;
                };
                closeActive(); exitActive(); step(400);
                check(relays == 1, "snapshot novo da mesma instancia preserva o contexto observado antes da retomada");
                finish(); check(front == b.Window.Handle && gameTransition == null, "retomada e recuperacao concluem com snapshot renovado");
                gameContextCursor = contextBeforeResume; gameResume = resumeWithoutFocus;
                reset(); b.Paused = true; closeActive(); exitActive(); finish();
                check(resumes == 0 && sent == 0 && launches == 0, "pausa externa nao e desfeita e nao equivale a nenhum jogo aberto");

                reset(); closeActive(); front = otherApp; step(200); exitActive(); finish();
                check(gameExitRoute == null && sent == 0 && front == fallback, "app escolhido durante o fechamento cancela o retorno posterior");
                reset(); closeActive(); gamesView.Activities.Remove(a); front = fallback; CheckGameClose(false, gameCommandVersion); step(500);
                check(sent == 0 && closingGame != null, "snapshot falho nao confirma saida nem transfere foco");
                CheckGameClose(true, gameCommandVersion - 1); check(sent == 0 && closingGame != null, "snapshot anterior ao pedido tambem nao confirma saida");
                CheckGameClose(true, gameCommandVersion); check(sent == 0 && closingGame != null, "ausencia no inventario sem termino nativo nao confirma saida");
                closeDeadline = now.AddSeconds(-1); CheckGameClose();
                check(gameExitRoute == null && gameCloseForce && !window.IsVisible, "timeout conserva oferta de forcar sem retorno automatico");
                reset(); closeActive(); exited.Add(a.Key); gamesView.Activities.Clear(); front = fallback;
                CheckGameClose(true, gameCommandVersion); finish();
                check(launches == 0 && sent == 0, "jogo conhecido omitido pelo catalogo mas ainda vivo impede falso retorno ao Inicio");

                reset(); closeActive(); steamInputPending = true;
                // Simula ocultacao diferida da propria operacao: o callback tardio nao invalida o retorno.
                GameNavigationUi(delegate { window.Show(); front = handle; Hide(true); });
                exited.Add(a.Key); gamesView.Activities.Remove(a); CheckGameClose(true, gameCommandVersion);
                check(gameExitRoute != null && steamHideDeferred && sent == 0, "saida confirmada aguarda ocultacao/liberacao diferida");
                CompleteSteamInput(new SteamInputCycle()); front = fallback; finish();
                check(front == b.Window.Handle && gameTransition == null, "fim do ciclo diferido preserva a intencao de retornar ao sobrevivente");

                reset(); observedForegroundGame = a; Hide(); gamesView.Activities.Remove(a); exited.Add(a.Key); front = fallback;
                ObserveGameExit(gamesView, true, gameCommandVersion, 0); finish();
                check(front == b.Window.Handle && sent == 1, "fechamento nativo do ultimo jogo observado tambem entrega o sobrevivente");
                reset(); observedForegroundGame = a; Hide(); gamesView.Activities.Remove(a); exited.Add(a.Key); front = otherApp;
                ObserveGameExit(gamesView, true, gameCommandVersion, 0); finish();
                check(sent == 0 && front == otherApp, "fechamento nativo nao disputa foco com outro app");

                reset(); gamesView.Activities.Remove(b); closeActive(); exitActive(); finish();
                check(front == steam.Handle && launches == 1 && gameTransition == null, "ultimo jogo retorna explicitamente ao Inicio do Big Picture");
                reset(); gamesView.Activities.Remove(b); closeActive(); front = steam.Handle; step(100);
                check(gameExitRoute != null && launches == 0, "Steam adiantada pelo shell nao cancela nem antecipa a saida do ultimo jogo");
                exited.Add(a.Key); gamesView.Activities.Clear(); CheckGameClose(true, gameCommandVersion); finish();
                check(front == steam.Handle && launches == 1 && gameTransition == null,
                    "Steam ja na frente recebe Inicio explicito depois de confirmar o ultimo fechamento");
                // Encerramento lento (04/11 15:29): o jogo segue listado sem caminho/criacao com o Big Picture ja na frente (ADR 0090).
                Func<GameActivity, GameActivity> exiting = delegate(GameActivity g) {
                    var copy = new GameActivity { Key = g.Key, Pid = g.Pid }; copy.Pids.Add(g.Pid); copy.Processes.Add(new ProcessIdentity(g.Pid, 0));
                        return copy;
                };
                gameHasExited = delegate(GameActivity g) { return g.Processes.Exists(p => !p.Known) ? (bool?)null : exited.Contains(g.Key); };
                reset(); gamesView.Activities.Remove(b); closeActive(); gamesView.Activities.Remove(a); gamesView.Activities.Add(exiting(a));
                front = steam.Handle; step(100); step(150);
                check(gameExitRoute != null && launches == 0, "copia sem identidade do proprio jogo saindo nao conta como outro jogo aberto");
                exited.Add(a.Key); CheckGameClose(true, gameCommandVersion); finish();
                check(front == steam.Handle && launches == 1 && gameTransition == null && closingGame == null,
                    "identidade original confirmada conclui com a copia ainda listada");
                reset(); gamesView.Activities.Remove(b); closeActive(); gamesView.Activities.Remove(a);
                var stranger = exiting(a); stranger.Pid = stranger.Pids[0] = 999; stranger.Processes[0] = new ProcessIdentity(999, 0);
                gamesView.Activities.Add(stranger); front = steam.Handle; step(100);
                check(gameExitRoute == null && launches == 0, "mesma chave com outro PID sem identidade continua tratada como outro jogo");
                reset(); gamesView.Activities.Remove(b); Hide(); observedForegroundGame = a; front = steam.Handle;
                var tearing = new GamesView(); tearing.Activities.Add(exiting(a));
                ObserveGameExit(tearing, true, gameCommandVersion, 0); gamesView = tearing;
                check(gameExitRoute == null && observedForegroundGame == a, "saida nativa lenta guarda a identidade original sem consumir a observacao");
                exited.Add(a.Key); ObserveGameExit(tearing, true, gameCommandVersion, 0); finish();
                check(front == steam.Handle && launches == 1 && gameTransition == null, "saida nativa lenta com a copia listada tambem retorna ao Inicio");
                gameHasExited = delegate(GameActivity game) { return exited.Contains(game.Key); };
                reset(); gamesView.Activities.Remove(b); closeActive(); front = otherApp; step(100);
                exited.Add(a.Key); gamesView.Activities.Clear(); CheckGameClose(true, gameCommandVersion); finish();
                check(front == otherApp && launches == 0 && gameTransition == null, "ultimo fechamento continua respeitando outro aplicativo escolhido");
                reset(); gamesView.Activities.Remove(b); Hide(); observedForegroundGame = null;
                var emptyView = new GamesView(); exited.Add(a.Key); front = steam.Handle;
                ObserveGameExit(emptyView, true, gameCommandVersion, 0); gamesView = emptyView; finish();
                check(front == steam.Handle && launches == 1 && gameTransition == null, "saida nativa do ultimo jogo retorna ao Inicio mesmo sem ultimo foco observado");
                reset(); gamesView.Activities.Add(c); closeActive(); exitActive();
                exited.Add(b.Key); gamesView.Activities.Remove(b); front = fallback; finish(); finish();
                check(front == c.Window.Handle && gameTransition == null, "destino que sai durante a entrega permite escolher outro sobrevivente");
                reset(); relayAccepted = false; closeActive(); exitActive(); step(1000);
                check(sent == 0 && gameTransition == null && !window.IsVisible, "recusa da ponte termina sem janela transparente residente");
                reset(); var normalSwitch = switchGameWindow;
                switchGameWindow = delegate { throw new IOException("fixture: recusa"); }; ResumeOpenGame(b.Key, b);
                check(window.IsVisible && gameTransition == null, "erro no pedido inicial preserva a barra");
                switchGameWindow = normalSwitch;
                reset(); recoverContext = false; context = SteamInput.DesktopAppId; ResumeOpenGame(b.Key, b); step(400);
                switchGameWindow = delegate { throw new IOException("fixture: recusa na ponte"); }; step(0);
                check(gameTransition == null && !window.IsVisible && window.Opacity == 1 && window.IsHitTestVisible,
                    "erro durante a recuperacao nao deixa a ponte transparente ativa");
                switchGameWindow = normalSwitch;
                reset(); var initialSwitch = switchGameWindow;
                switchGameWindow = delegate(DesktopWindow target) {
                    if (target == b.Window)
                    {
                        front = handle; Show(); front = handle;
                        switchGameWindow = initialSwitch; ResumeOpenGame(a.Key, a);
                        throw new IOException("fixture: retorno antigo depois da escolha nova");
                    }
                    initialSwitch(target);
                };
                ResumeOpenGame(b.Key, b); finish();
                check(front == a.Window.Handle && gameTransition == null, "erro reentrante do destino antigo nao cancela a escolha nova");
                switchGameWindow = normalSwitch;
                reset(); gamesView.Activities.Remove(b); closeActive(); exitActive();
                var normalSteamLaunch = steamLaunch;
                steamLaunch = delegate {
                    front = otherApp; CancelGameNavigation("fixture: escolha durante o despacho Steam");
                };
                finish();
                check(sent == 0 && front == otherApp && gameTransition == null && !window.IsVisible,
                    "despacho Steam reentrante nao envia foco apos cancelamento");
                steamLaunch = normalSteamLaunch;

                // Botao ainda apertado nao vai para o jogo: a entrega espera a soltura, no maximo 500 ms (12:43:00, ADR 0068).
                // Eventos com horario anterior so atualizam os botoes apertados, sem acionar a barra.
                var oldPad = controllerNow; DateTime pad = new DateTime(2026, 10, 28, 12, 0, 0); controllerNow = delegate { return pad; };
                Action<string, bool> button = delegate(string name, bool down) {
                    OnController(new ButtonEvent { Device = "teste", Button = name, Down = down, Time = pad.AddSeconds(-0.2) });
                };
                var oldLaunch = gameLaunch; var oldSwitchApp = switchApp; var oldAppForeground = appForeground; var oldReturn = returnWindowFocus;
                try
                {
                    reset(); button("X", true); ResumeOpenGame(b.Key, b); step(150);
                    check(sent == 0 && window.IsVisible && gameTransition != null, "X ainda apertado segura a entrega do foco ao jogo");
                    button("X", false);
                    check(sent == 1, "soltar o X entrega o foco na hora");
                    finish(); check(gameTransition == null && front == b.Window.Handle, "transicao conclui depois da soltura");
                    reset(); button("X", true); ResumeOpenGame(b.Key, b); pad = pad.AddMilliseconds(600); step(150);
                    check(sent == 1, "botao preso nao segura a entrega alem de 500 ms");
                    button("X", false); finish();
                    reset(); int returned = 0; returnWindowFocus = delegate { returned++; return true; };
                    previousWindow = new WindowObservation { Window = a.Window }; front = handle;
                    button("CIRCULO", true); Hide(true);
                    check(window.IsVisible && window.Opacity == 0 && steamHideDeferred && returned == 0, "circulo apertado segura o retorno ao jogo");
                    button("CIRCULO", false);
                    check(!window.IsVisible && returned == 1 && window.Opacity == 1 && !steamHideDeferred, "soltar o circulo devolve o foco uma vez");
                    reset(); returned = 0; previousWindow = new WindowObservation { Window = a.Window }; front = handle;
                    button("CIRCULO", true); Hide(true); pad = pad.AddMilliseconds(600); TickController();
                    check(!window.IsVisible && returned == 1, "botao preso nao segura o retorno alem de 500 ms");
                    button("CIRCULO", false); returnWindowFocus = oldReturn;

                    // Abrir outro jogo requer saida confirmada de todos os jogos que a pergunta identifica.
                    var extra = new GameEntry { Key = "steam:1:999", Name = "Outro jogo", Source = "Steam", Kind = "Game", Available = true, LaunchUri = "steam://rungameid/999" };
                    var order = new List<string>();
                    Action<int> tickLaunch = delegate(int milliseconds) { now = now.AddMilliseconds(milliseconds); TickGameLaunchRoute(); };
                    gameLaunch = delegate(GameEntry e) { order.Add("abrir:" + e.Key); };
                    switchApp = delegate(DesktopWindow w) { order.Add("foco:" + w.Handle.ToInt64()); front = w.Handle; };
                    appForeground = delegate(DesktopWindow w) { return front == w.Handle; };
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; steamInputPending = true; button("X", true);
                    RequestGameLaunch(extra.Key);
                    check(gameConfirmation == extra.Key && gameLaunchRoute == null && closeRequests == 0, "pedido de abertura com jogo ativo exige escolha explicita");
                    ConfirmGame();
                    check(order.Count == 0 && window.IsVisible && gameLaunchRoute != null && closeRequests == 0, "troca espera a Steam liberar o controle antes de fechar");
                    steamInputPending = false; tickLaunch(100);
                    check(order.Count == 0 && closeRequests == 0, "troca espera soltar o X antes do fechamento");
                    button("X", false);
                    check(order.Count == 0 && closeRequests == 1 && closingGame == a.Key && !window.IsVisible && gameExitRoute == null,
                        "fechar e iniciar pede fechamento normal sem abrir jogo nem armar retorno concorrente");
                    gamesView.Activities.Remove(a); front = fallback; CheckGameClose(false, gameCommandVersion);
                    check(order.Count == 0 && closingGame == a.Key, "snapshot falho nao libera a abertura");
                    CheckGameClose(true, gameCommandVersion - 1);
                    check(order.Count == 0 && closingGame == a.Key, "snapshot anterior ao fechamento nao libera a abertura");
                    CheckGameClose(true, gameCommandVersion);
                    check(order.Count == 0 && closingGame == a.Key, "sumir da lista sem termino nativo nao libera a abertura");
                    exited.Add(a.Key); CheckGameClose(true, gameCommandVersion);
                    check(order.Count == 0 && closeRequests == 2 && closingGame == b.Key, "com dois jogos abertos fecha cada instancia antes de iniciar o escolhido");
                    exited.Add(b.Key); gamesView.Activities.Clear(); CheckGameClose(true, gameCommandVersion);
                    check(order.Count == 1 && order[0] == "abrir:" + extra.Key && gameLaunchRoute == null && launchingGame == extra.Key,
                        "novo jogo so e despachado uma vez depois da saida confirmada de todos");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    gamesView.Activities.RemoveAll(g => SteamGameContext.AppId(g.Key) != null); RequestGameLaunch(extra.Key);
                    check(order.Count == 1 && order[0] == "abrir:" + extra.Key, "sem jogo aberto o primeiro lancamento continua direto");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); CancelGame();
                    check(order.Count == 0 && closeRequests == 0 && gameLaunchRoute == null, "Cancelar preserva o jogo atual e nao agenda abertura");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); ConfirmGame(); closeDeadline = now.AddSeconds(-1); CheckGameClose(); tickLaunch(100);
                    check(order.Count == 0 && gameLaunchRoute == null && gameCloseForce, "confirmacao propria ou timeout cancela a abertura sem forcar o processo");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    var regularClose = gameCloseWindow; gameCloseWindow = delegate { throw new IOException("fixture: fechamento recusado"); };
                    RequestGameLaunch(extra.Key); ConfirmGame(); gameCloseWindow = regularClose;
                    check(order.Count == 0 && gameLaunchRoute == null && window.IsVisible, "erro no fechamento preserva a barra e cancela a abertura");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear(); steamInputPending = true;
                    RequestGameLaunch(extra.Key); ConfirmGame(); Hide(); steamInputPending = false; tickLaunch(200);
                    check(order.Count == 0 && closeRequests == 0 && gameLaunchRoute == null && launchingGame == null, "fechar a barra manualmente cancela a troca pendente");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); ConfirmGame(); Show(); tickLaunch(100);
                    check(order.Count == 0 && gameLaunchRoute == null, "reabrir a barra cancela o inicio automatico apos o fechamento ja solicitado");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); ConfirmGame(); front = otherApp; tickLaunch(100);
                    check(order.Count == 0 && gameLaunchRoute == null && front == otherApp, "outro app escolhido durante a troca impede abertura tardia");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    gameCloseWindow = delegate { closeRequests++; front = otherApp; };
                    RequestGameLaunch(extra.Key); ConfirmGame(); gameCloseWindow = regularClose;
                    check(order.Count == 0 && gameLaunchRoute == null && window.IsVisible && front == otherApp,
                        "outro app que toma foco no worker de fechamento nao recebe ocultacao ou retorno tardio");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); ConfirmGame(); exited.Add(a.Key); gamesView.Activities.Remove(a);
                    gamesView.Activities.Add(c); front = fallback; CheckGameClose(true, gameCommandVersion);
                    check(order.Count == 0 && closeRequests == 1 && gameLaunchRoute == null, "jogo novo nao contemplado pela pergunta nao e fechado automaticamente");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); ConfirmGame(); exited.Add(a.Key); gamesView.Activities.Remove(a);
                    var reused = TransitionFixture(b.Key, b.Pid, 601, 10); reused.Processes.Clear(); reused.Processes.Add(new ProcessIdentity(b.Pid, 2));
                    gamesView.Activities.Remove(b); gamesView.Activities.Add(reused); front = fallback; CheckGameClose(true, gameCommandVersion);
                    check(order.Count == 0 && closeRequests == 1 && gameLaunchRoute == null, "PID reutilizado nao recebe fechamento autorizado para a instancia anterior");
                    reset(); gamesView.Entries.Add(extra); launchingGame = null; order.Clear();
                    RequestGameLaunch(extra.Key); ConfirmGame(); activationEpoch++; tickLaunch(100);
                    check(order.Count == 0 && gameLaunchRoute == null, "mudanca do ciclo FSE cancela a abertura apos fechamento");

                    // Fechar a barra ociosa e uma navegacao explicita; abrir pelo PS continua mostrando a barra.
                    reset(); launchingGame = unconfirmedLaunch = null; gamesView.Activities.Clear(); gamesView.Current = null; Dismiss();
                    check(launches == 1 && !window.IsVisible && front == steam.Handle, "fechar barra sem jogo retorna explicitamente ao Inicio do Big Picture");
                    Show(); front = handle;
                    check(window.IsVisible && launches == 1, "reabrir a barra sem jogo nao redireciona nem impede o PS");
                    reset(); launchingGame = unconfirmedLaunch = null; gamesView.Activities.Clear(); steamInputPending = true; Dismiss();
                    check(launches == 0 && steamNavigationUri == SteamStartPageUri, "fechamento sem jogo aguarda a liberacao completa da Steam");
                    steamInputPending = false; TrySteamNavigation();
                    check(launches == 1 && !window.IsVisible, "liberacao da Steam conclui o retorno sem jogo");
                    reset(); launchingGame = unconfirmedLaunch = null; gamesView.Activities.Clear(); button("CIRCULO", true); Dismiss();
                    check(launches == 0 && window.IsVisible, "circulo ainda apertado nao vaza para o Big Picture no retorno sem jogo");
                    button("CIRCULO", false);
                    check(launches == 1 && !window.IsVisible, "soltar o circulo conclui o retorno ao Inicio na hora");
                    reset(); launchingGame = unconfirmedLaunch = null; gamesView.Activities.Clear(); gamesSnapshotFresh = false;
                    check(!ReturnHomeWithoutGame() && launches == 0, "leitura falha nao equivale a nenhum jogo aberto");
                    gamesSnapshotFresh = true; gamesSnapshotVersion = gameCommandVersion - 1;
                    check(!ReturnHomeWithoutGame() && launches == 0, "snapshot anterior a comando nao autoriza retorno sem jogo");
                    gamesSnapshotVersion = long.MaxValue; launchingGame = extra.Key;
                    check(!ReturnHomeWithoutGame() && launches == 0, "abertura em andamento nao e tratada como ausencia de jogo");
                    launchingGame = null; unconfirmedLaunch = extra.Key;
                    check(!ReturnHomeWithoutGame() && launches == 0, "abertura incerta tambem impede redirecionamento ocioso");
                    unconfirmedLaunch = null; activationMode = false; activationRead = delegate { return false; };
                    check(!ReturnHomeWithoutGame() && launches == 0, "fechar a barra no desktop nao navega automaticamente na Steam");
                    activationMode = true; activationRead = delegate { return null; };
                    check(!ReturnHomeWithoutGame() && launches == 0, "API incerta nao autoriza retorno automatico");
                    activationRead = delegate { return true; };
                }
                finally
                {
                    controllerNow = oldPad; ClearGuide(); EndGameLaunchRoute(); launchingGame = null; activationMode = true;
                    gameLaunch = oldLaunch; switchApp = oldSwitchApp; appForeground = oldAppForeground; returnWindowFocus = oldReturn;
                }

                reset(); activationMode = false; closeActive = delegate { AskCloseGame(a.Key, "resume"); ConfirmCloseGame(); };
                closeActive(); exitActive(); finish();
                check(gameExitRoute == null && sent == 0 && launches == 0, "redirecionamento automatico nao e aplicado no desktop");
            }
            finally
            {
                steamInputPending = steamHideDeferred = steamHideForGame = false;
                CancelGameNavigation("Fim da verificacao."); Hide(); pausedByPreview.Clear();
                gameBusy = closingGame = gameCloseConfirmation = null; closingActivity = observedForegroundGame = null;
                gamesView = oldView; feedbackNow = oldNow; foregroundRead = oldRead; appAvailable = oldAvailable;
                appOwnsForeground = oldOwns; gameForeground = oldFront; switchGameWindow = oldSwitch;
                gameRelayFocus = oldRelay; gameFallback = oldFallback; gameContextCursor = oldCursor; gameHasExited = oldExited;
                controllerFocus = oldControllerFocus;
                observeWindow = oldObserve; activationRead = oldMode; activationMode = oldModeValue; activationEpoch = oldEpoch;
                gameCloseWindow = oldClose; gameResume = oldResume; previousWindow = oldPrevious;
                steamLaunch = oldSteamLaunch; findBigPicture = oldFindSteam; existingWindowAvailable = oldSteamAvailable;
                steamInputAvailable = oldSteamRunning; ClearFeedback(); SetPanel(0);
                gamesSnapshotFresh = oldSnapshotFresh; gamesSnapshotVersion = oldSnapshotVersion;
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes de transicoes/saida de jogos.");
            checks.Add("Foco, processos de jogos, Steam/contexto/protocolos e relogio substituidos. Nenhum jogo, FSE ou overlay real operado.");
            File.WriteAllLines(Path.Combine(Output, "transicoes-jogos-verificacao.txt"), checks, Encoding.UTF8);
        }
        static GameActivity TransitionFixture(string key, uint pid, int hwnd, long focused)
        {
            var game = new GameActivity { Key = key, Pid = pid, LastFocused = focused, Started = focused,
                Window = new DesktopWindow { Handle = new IntPtr(hwnd), ProcessId = pid, ProcessCreated = 1, Title = key, AppName = key } };
            game.Pids.Add(pid); game.Processes.Add(new ProcessIdentity(pid, 1)); return game;
        }
        // As fixtures antigas tambem aguardam estabilidade, sem dormir nem operar uma janela real.
        void SettleGameTransitionFixture()
        {
            if (!verify || gameTransition == null) return;
            var old = feedbackNow; DateTime now = old(); feedbackNow = delegate { return now; };
            try { TickGameTransition(); now = now.AddMilliseconds(800); TickGameTransition(); }
            finally { feedbackNow = old; }
        }
        static void VerifyGameContextParser(Action<bool, string> check)
        {
            check(SteamGameContext.AppId("steam:1:2420660") == "2420660" && SteamGameContext.AppId("shortcut:1:3899276376") == "3899276376",
                "appid distingue jogo Steam e atalho unsigned");
            check(SteamGameContext.AppId("local:game") == null && SteamGameContext.AppId("steam:1:bad") == null, "jogo externo/identidade invalida nao inventa appid");
            string game = "OnFocusWindowChanged to game window type: AppID 2420660, neva\n";
            string client = "OnFocusWindowChanged to window type: k_nGameIDControllerConfigs_ClientUI, AppID 769\n";
            check(SteamGameContext.Last(game + client) == "769" && SteamGameContext.Last(client + game) == "2420660", "ultima escolha efetiva da Steam determina o contexto");
            check(SteamGameContext.Last(game + "OnFocusWindowChanged to window type: guide, AppID 443510\n") == "2420660", "Guia temporario nao substitui contexto do jogo");
            check(SteamGameContext.Last("OnFocusWindowChanged to game window type: AppID -395690920, genshin\n") == "3899276376", "atalho signed normaliza para unsigned");
            check(SteamGameContext.Last("OnFocusWindowChanged URL Forcing to window type: k_EWindowTypeGame, 413080\n") == "413080", "contexto ainda forcado e observado");
            check(SteamGameContext.Last(game + "OnFocusWindowChanged to window type: client, AppID 7") == "2420660", "linha incompleta nao inventa mudanca de contexto");
            check(SteamGameContext.Last("Loaded Config for App ID 769\n") == null, "carga de perfil nao equivale a contexto de foco");
            check(GameControl.Exited(new ProcessIdentity(0, 0)) == null, "identidade desconhecida nao prova saida");
            using (var own = Process.GetCurrentProcess())
            {
                long created = GameControl.Capture(new[] { (uint)own.Id })[0].Created;
                check(GameControl.Exited(new ProcessIdentity((uint)own.Id, created)) == false, "consulta nativa somente leitura identifica processo proprio vivo");
                check(GameControl.Exited(new ProcessIdentity((uint)own.Id, created + 1)) == true, "PID com outra criacao nao pertence mais a identidade antiga");
            }
        }
        static void VerifyGameContextCursor(Action<bool, string> check)
        {
            string folder = Path.Combine(Output, "game-transition-tests"); Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "controller.txt");
            string selected = "OnFocusWindowChanged to game window type: AppID 2420660, neva\n";
            File.WriteAllText(file, selected, Encoding.UTF8);
            var cursor = new SteamConsoleTail(file);
            check(SteamGameContext.Last(cursor.ReadNew()) == null, "cursor nao usa um foco Steam anterior a escolha");
            File.AppendAllText(file, selected + "OnFocusWindowChanged to window type: client, AppID 769\n", Encoding.UTF8);
            check(SteamGameContext.Last(cursor.ReadNew()) == "769", "cursor e parser observam a regressao apos a escolha");
            File.WriteAllText(file, selected, new UTF8Encoding(false));
            check(SteamGameContext.Last(cursor.ReadNew()) == "2420660", "rotacao por arquivo menor permite observar o contexto novo");
            check(new SteamConsoleTail(Path.Combine(folder, "ausente.txt")).ReadNew() == null, "log ausente permanece incerto");
        }
    }
}
