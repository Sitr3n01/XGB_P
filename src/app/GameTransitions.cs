// Transicao de jogo: Steam liberada, janela exata e entrega estavel. Sem teclado simulado.
using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class GameTransition
        {
            public string Key, AppId, Context;
            public GameActivity Activity;
            public DesktopWindow Target;
            public int Epoch, Mode, Panel, Attempts, Recoveries, ContextGeneration, ContextReads;
            public bool Hidden, Relay, Home, HomeSent, SteamStarted, ContextBusy;
            public DateTime Deadline, ReadyAt, RetryAt, StableSince, ContextAt;
            public Func<string> ReadContext;
            public FeedbackNotice Notice;
            public GameExitRoute Exit;
        }
        readonly DispatcherTimer gameTransitionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        GameTransition gameTransition;
        bool gameNavigationInternal;
        Action<DesktopWindow> switchGameWindow = RequestGameSwitch;
        Func<DesktopWindow, bool> gameForeground = DesktopWindows.IsGameForeground;
        Func<IntPtr, bool> gameFallback = DesktopWindows.GamingFallback;
        Func<bool> gameRelayFocus;
        Func<Func<string>> gameContextCursor = delegate {
            var tail = new SteamConsoleTail(SteamInput.ControllerLog());
            return delegate { return SteamGameContext.Last(tail.ReadNew()); };
        };
        static void RequestGameSwitch(DesktopWindow target)
        { WriteLog("jogos: pedido de foco exato | " + target.Key + " | " + DesktopWindows.ActivateGameWithReport(target)); }
        void StartGameTransitions()
        {
            gameTransitionTimer.Tick += delegate { TickGameExitRoute(); TickGameTransition(); };
            gameRelayFocus = delegate { DesktopWindows.TakeForeground(handle, false); return foregroundRead() == handle; };
            if (!verify) return;
            switchGameWindow = delegate(DesktopWindow target) { switchApp(target); };
            gameForeground = delegate(DesktopWindow target) { return appForeground(target); };
            gameContextCursor = delegate { return delegate { return null; }; };
            gameFallback = delegate { return false; };
            gameRelayFocus = delegate { return false; };
            gameHasExited = delegate { return true; };
        }
        void GameNavigationUi(Action action)
        {
            bool before = gameNavigationInternal; gameNavigationInternal = true;
            try { action(); } finally { gameNavigationInternal = before; }
        }
        void CancelGameNavigation(string reason)
        {
            if (gameNavigationInternal) return;
            gameNavigationEpoch++;
            CancelGameLaunchRoute("Abertura cancelada pela navegação.", FeedbackKind.Info);
            gameExitRoute = null;
            FinishGameTransition(reason, FeedbackKind.Info);
            if (gameExitRoute == null && gameTransition == null) gameTransitionTimer.Stop();
        }
        GameActivity GameForWindow(DesktopWindow item)
        {
            return gamesView == null || item == null ? null : gamesView.Activities.Find(a => a.Window != null &&
                a.Window.Handle == item.Handle && a.Window.ProcessId == item.ProcessId && a.Window.ProcessCreated == item.ProcessCreated);
        }
        void BeginGameTransition(GameActivity activity, bool resumed, GameExitRoute exit = null, Func<string> initialContext = null)
        {
            int nextEpoch = gameNavigationEpoch + 1;
            CancelAppSwitch("Transição de jogo substituída.");
            if (nextEpoch != gameNavigationEpoch) return; // Cancelar pode bombear uma escolha mais recente.
            var target = activity == null ? (exit == null ? null : exit.HomeWindow) : activity.Window;
            if (activity != null && (target == null || !appAvailable(target)))
            {
                if (exit != null && gameHasExited(activity) == true)
                {
                    exit.Skipped.Add(activity.Key); exit.Epoch = gameNavigationEpoch;
                    gameExitRoute = exit; gameTransitionTimer.Start(); TickGameExitRoute(); return;
                }
                Feedback("O jogo está aberto, mas sua janela ainda não está disponível.", FeedbackKind.Attention); return;
            }
            var t = new GameTransition {
                Key = activity == null ? null : activity.Key, Activity = activity, Target = target,
                Home = activity == null, AppId = activity == null ? SteamInput.ClientUiAppId : SteamGameContext.AppId(activity.Key),
                Epoch = gameNavigationEpoch, Mode = activationEpoch, Panel = panel, Exit = exit,
                Deadline = exit == null ? feedbackNow().AddSeconds(8) : exit.Deadline,
                ReadyAt = feedbackNow().AddMilliseconds(resumed && !verify ? 300 : 0)
            };
            gameTransition = t; BeginReleaseWait();
            t.Notice = BeginFeedback(t.Home ? "Voltando ao Início do Big Picture…" : "Alternando para o jogo…", "0", 10);
            ResetGameContext(t);
            if (initialContext != null) t.ReadContext = initialContext;
            gameTransitionTimer.Start(); TickGameTransition();
        }
        void ResetGameContext(GameTransition t)
        {
            t.ContextGeneration++; t.Context = null; t.ContextReads = 0; t.ContextBusy = false;
            t.ReadContext = CreateGameContextCursor();
        }
        Func<string> CreateGameContextCursor()
        {
            try { return gameContextCursor(); }
            catch (Exception ex)
            { WriteLog("jogos: cursor Steam indisponivel | " + ex.Message); return delegate { return null; }; }
        }
        void PollGameContext(GameTransition t)
        {
            if (t.ContextBusy || t.ReadContext == null) return;
            t.ContextBusy = true;
            int generation = t.ContextGeneration; var read = t.ReadContext;
            Action<string> apply = delegate(string context) {
                if (gameTransition != t || generation != t.ContextGeneration) return;
                t.ContextBusy = false; t.ContextReads++; t.ContextAt = feedbackNow();
                if (context != t.Context) WriteLog("jogos: contexto Steam | " + t.Key + " | " + (context ?? "sem leitura nova"));
                t.Context = context;
            };
            if (verify) { apply(read()); return; }
            Task.Factory.StartNew(delegate {
                string context = null;
                try { context = read(); } catch (Exception ex) { WriteLog("jogos: contexto Steam indisponivel | " + ex.Message); }
                Dispatch(delegate { apply(context); });
            });
        }
        bool GameTransitionContextValid(GameTransition t)
        {
            bool? mode = activationRead();
            return !disposed && !systemPowerBusy && t.Epoch == gameNavigationEpoch && t.Mode == activationEpoch &&
                t.Panel == panel && mode.HasValue && mode == activationMode && (t.Exit == null || mode == true);
        }
        bool BeginGameRelay(GameTransition t)
        {
            // So o destino ja escolhido, ausencia de frente ou GamingFallbackWindow autorizam a ponte.
            IntPtr front = foregroundRead();
            if (front != IntPtr.Zero && front != handle && !gameFallback(front) &&
                (t.Target == null || !appOwnsForeground(t.Target))) return false;
            t.Relay = true; t.Hidden = false; t.StableSince = DateTime.MinValue;
            SuspendKeyboard(); steamGuideOpening = false;
            window.Opacity = 0; window.IsHitTestVisible = false;
            window.Show();
            if (gameTransition != t || !GameTransitionContextValid(t)) return false;
            return gameRelayFocus(); // Sem Alt, sem restaurar uma janela de terceiro.
        }
        void TickGameTransition()
        {
            var t = gameTransition; if (t == null) return;
            try
            {
                if (!GameTransitionContextValid(t) || t.Exit == null && !t.Hidden && !window.IsVisible)
                { FinishGameTransition("Transição de jogo cancelada.", FeedbackKind.Info); return; }
                if (feedbackNow() >= t.Deadline)
                { FinishGameTransition("Não foi possível confirmar a transição do jogo. Confira a janela e a Steam.", FeedbackKind.Warning); return; }
                IntPtr front = foregroundRead();
                if (front != IntPtr.Zero && front != handle && !gameFallback(front) &&
                    (t.Target == null || !appOwnsForeground(t.Target)))
                { FinishGameTransition("Outra janela foi escolhida. Transição cancelada.", FeedbackKind.Info); return; }
                if (steamInputPending || feedbackNow() < t.ReadyAt) return;
                if (t.Home)
                {
                    if (!PrepareGameHome(t)) return;
                    if (gameTransition != t || !GameTransitionContextValid(t)) return;
                }
                else
                {
                    var current = gamesView == null ? null : gamesView.Activity(t.Key);
                    if (current == null || !SameGameInstance(t.Activity, current) || !appAvailable(t.Target))
                    {
                        if (t.Exit != null && RetryGameExitTransition(t)) return;
                        FinishGameTransition("O jogo saiu ou sua janela mudou durante a transição.", FeedbackKind.Warning); return;
                    }
                }
                if (t.Attempts == 0 && front == handle)
                {
                    // O X que escolheu, ainda apertado, iria para o jogo como um aperto novo (ADR 0068).
                    if (ButtonsStillHeld()) return;
                    t.Attempts++; t.RetryAt = feedbackNow().AddMilliseconds(300);
                    switchGameWindow(t.Target);
                    if (gameTransition != t || !GameTransitionContextValid(t)) return;
                }
                PollGameContext(t);
                if (gameTransition != t || !GameTransitionContextValid(t)) return;
                if (gameForeground(t.Target))
                {
                    if (!t.Hidden)
                    {
                        t.Hidden = true; GameNavigationUi(delegate { Hide(); });
                        if (gameTransition != t || !gameForeground(t.Target)) return;
                    }
                    if (t.StableSince == DateTime.MinValue) t.StableSince = feedbackNow();
                    double stable = (feedbackNow() - t.StableSince).TotalMilliseconds;
                    bool mismatch = t.AppId != null && t.Context != null && t.Context != t.AppId;
                    // ClientUI com o jogo na frente: a interface da Steam ficou sobre ele, e refazer o foco a traz de volta
                    // (19:54:57 e 19:55:08). Sem ponte: aviso e fim (ADR 0066). Outro contexto ainda permite uma ponte.
                    bool steamUi = mismatch && !t.Home && t.Context == SteamInput.ClientUiAppId;
                    if (steamUi && stable >= 750 && (feedbackNow() - t.ContextAt).TotalMilliseconds <= 500)
                    {
                        WriteLog("jogos: Steam assumiu o controle com o jogo na frente | " + t.Key + " | Steam=" + t.Context + "; recuperacoes=" + t.Recoveries);
                        FinishGameTransition("A Steam ficou com o controle sobre o jogo.", FeedbackKind.Attention);
                        return;
                    }
                    if (mismatch && !steamUi && stable >= 350 && t.Recoveries == 0)
                    {
                        t.Recoveries++; ResetGameContext(t);
                        WriteLog("jogos: recuperacao limitada do contexto Steam | " + t.Key);
                        if (!BeginGameRelay(t) && gameTransition == t) FinishGameTransition("O Windows não liberou a recuperação da transição.", FeedbackKind.Attention);
                        return;
                    }
                    if (stable < 750 || t.ContextReads == 0 || (feedbackNow() - t.ContextAt).TotalMilliseconds > 500 || mismatch) return;
                    if (t.Activity != null) observedForegroundGame = t.Activity;
                    WriteLog("jogos: transicao estavel | " + t.Key + " | Steam=" + (t.Context ?? "nao confirmada") + "; recuperacoes=" + t.Recoveries);
                    FinishGameTransition(t.Home ? "Início do Big Picture em primeiro plano." : "Jogo em primeiro plano.", FeedbackKind.Success);
                    return;
                }
                t.StableSince = DateTime.MinValue;
                if (front != handle)
                {
                    if (t.Exit != null && t.Attempts == 0 || t.Hidden && t.Recoveries == 0)
                    {
                        if (t.Hidden) t.Recoveries++;
                        if (!BeginGameRelay(t) && gameTransition == t) FinishGameTransition("O Windows não liberou o retorno ao jogo.", FeedbackKind.Attention);
                    }
                    return;
                }
                if (t.Attempts >= 3 || feedbackNow() < t.RetryAt) return;
                t.Attempts++; t.RetryAt = feedbackNow().AddMilliseconds(300);
                switchGameWindow(t.Target);
            }
            catch (Exception ex)
            {
                WriteLog("jogos: transicao falhou | " + ex.Message);
                if (gameTransition == t) FinishGameTransition("Não foi possível alternar para o jogo. " + ex.Message, FeedbackKind.Error);
            }
        }
        bool PrepareGameHome(GameTransition t)
        {
            if (t.HomeSent) return t.Target != null && appAvailable(t.Target);
            if (!window.IsVisible || foregroundRead() != handle)
            { if (!BeginGameRelay(t) && gameTransition == t) FinishGameTransition("O Windows não liberou o retorno ao Início.", FeedbackKind.Attention); return false; }
            var existing = findBigPicture();
            if (existing == null || !existingWindowAvailable(existing.Window))
            {
                if (!t.SteamStarted)
                {
                    t.SteamStarted = true;
                    if (!steamInputAvailable()) { FinishGameTransition("A Steam não está aberta para receber o retorno.", FeedbackKind.Attention); return false; }
                    steamLaunch(SteamHomeUri);
                }
                return false;
            }
            t.Target = existing.Window;
            if (!t.HomeSent)
            {
                t.HomeSent = true; steamLaunch(SteamStartPageUri);
                // O despacho pode bombear a UI. Uma escolha nova invalida esse retorno.
                if (gameTransition != t || !GameTransitionContextValid(t)) return false;
            }
            return true;
        }
        void FinishGameTransition(string message, FeedbackKind kind)
        {
            var t = gameTransition; if (t == null) return;
            gameTransition = null;
            if (t.Relay && window.IsVisible) GameNavigationUi(delegate { Hide(); });
            FinishFeedback(t.Notice, message, kind);
            if (gameExitRoute == null && gameTransition == null) gameTransitionTimer.Stop();
        }
        static bool SameGameInstance(GameActivity before, GameActivity after)
        {
            if (before == null || after == null || before.Key != after.Key || before.Pid != after.Pid) return false;
            var root = before.Processes.Find(p => p.Pid == before.Pid);
            return root == null ? before == after : root.Known && after.Processes.Exists(p => p.Pid == root.Pid && p.Created == root.Created);
        }
    }
}
