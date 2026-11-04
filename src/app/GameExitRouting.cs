// Saida confirmada entrega o jogo sobrevivente; janelas escolhidas pelo usuario cancelam o retorno.
using System;
using System.Collections.Generic;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class GameExitRoute
        {
            public GameActivity Source;
            public GameActivity Destination;
            public DesktopWindow HomeWindow;
            public Func<string> ContextCursor;
            public int Epoch, Mode;
            public bool Ready, Resuming;
            public DateTime Deadline;
            public readonly HashSet<string> Skipped = new HashSet<string>();
            public readonly List<GameActivity> KnownOthers = new List<GameActivity>();
        }
        GameExitRoute gameExitRoute;
        GameActivity observedForegroundGame, closingActivity;
        Func<GameActivity, bool?> gameHasExited = AllGameProcessesExited;
        static bool? AllGameProcessesExited(GameActivity activity)
        {
            if (activity == null || activity.Processes.Count == 0) return null;
            bool uncertain = false;
            foreach (var identity in activity.Processes)
            {
                bool? exited = GameControl.Exited(identity);
                if (exited == false) return false;
                if (exited == null) uncertain = true;
            }
            return uncertain ? null : (bool?)true;
        }
        bool GameWasForeground(GameActivity activity)
        {
            if (activity.Window == null) return false;
            return appOwnsForeground(activity.Window) || window.IsVisible && previousWindow != null &&
                previousWindow.Window.Handle == activity.Window.Handle && previousWindow.Window.ProcessId == activity.Window.ProcessId &&
                previousWindow.Window.ProcessCreated == activity.Window.ProcessCreated;
        }
        void ArmGameExitRoute(GameActivity source, bool ready)
        {
            if (activationMode != true || disposed || systemPowerBusy) return;
            gameExitRoute = new GameExitRoute { Source = source, Ready = ready, Epoch = gameNavigationEpoch,
                Mode = activationEpoch, Deadline = feedbackNow().AddSeconds(ready ? 8 : 20) };
            if (gamesView != null) gameExitRoute.KnownOthers.AddRange(gamesView.Activities.FindAll(a => !SameGameInstance(a, source)));
            gameTransitionTimer.Start();
            WriteLog("jogos: retorno apos saida armado | " + source.Key + "; saidaConfirmada=" + ready);
        }
        void ObserveGameExit(GamesView next, bool fresh, long readVersion, uint foregroundPid)
        {
            if (!fresh || readVersion < gameCommandVersion) return;
            var before = observedForegroundGame;
            if (before == null && gamesView != null && gamesView.Activities.Count == 1 && next.Activities.Count == 0)
                before = gamesView.Activities[0];
            var remaining = before == null ? null : next.Activity(before.Key);
            if (before != null && !SameGameInstance(before, remaining))
            {
                observedForegroundGame = null; // Consumir uma vez, mesmo quando o usuario ja abriu outro app/barra.
                if (gameExitRoute == null && closingGame == null && gameTransition == null && !window.IsVisible &&
                    gameHasExited(before) == true) ArmGameExitRoute(before, true);
            }
            if (foregroundPid != 0)
            {
                var focused = next.Activities.Find(a => a.Window != null && a.Window.ProcessId == foregroundPid);
                if (focused != null) observedForegroundGame = focused;
            }
        }
        void TickGameExitRoute()
        {
            var route = gameExitRoute; if (route == null) return;
            try
            {
                bool? mode = activationRead();
                if (disposed || systemPowerBusy || mode != true || route.Mode != activationEpoch || route.Epoch != gameNavigationEpoch ||
                    feedbackNow() >= route.Deadline)
                { StopGameExitRoute("contexto mudou ou prazo terminou"); return; }
                IntPtr front = foregroundRead();
                // O shell pode entregar a Steam antes de o observador confirmar a saida do ultimo jogo.
                // Aceitar somente essa janela exata como destino; outro app continua cancelando.
                bool otherKnown = route.KnownOthers.Exists(a => gameHasExited(a) != true) || gamesView != null &&
                    gamesView.Activities.Exists(a => !SameGameInstance(a, route.Source) && gameHasExited(a) != true);
                var home = otherKnown ? null : findBigPicture();
                route.HomeWindow = home != null && existingWindowAvailable(home.Window) ? home.Window : null;
                if (front != IntPtr.Zero && front != handle && !gameFallback(front) &&
                    (route.Source.Window == null || !appOwnsForeground(route.Source.Window)) &&
                    (route.Destination == null || route.Destination.Window == null || !appOwnsForeground(route.Destination.Window)) &&
                    (route.HomeWindow == null || !appOwnsForeground(route.HomeWindow)))
                { StopGameExitRoute("outra janela ja esta em primeiro plano"); return; }
                if (!route.Ready || route.Resuming || steamInputPending || gameBusy != null) return;
                if (window.IsVisible)
                {
                    if (!steamHideDeferred) StopGameExitRoute("barra reaberta pelo usuario");
                    return;
                }
                if (gamesView == null || gameHasExited(route.Source) != true) return;
                var ordered = new List<GameActivity>(gamesView.Activities);
                ordered.RemoveAll(a => a.Key == route.Source.Key || route.Skipped.Contains(a.Key));
                ordered.Sort(delegate(GameActivity a, GameActivity b) {
                    int order = b.LastFocused.CompareTo(a.LastFocused);
                    if (order == 0) order = b.Started.CompareTo(a.Started);
                    return order == 0 ? string.CompareOrdinal(a.Key, b.Key) : order;
                });
                var target = ordered.Find(a => a.Window != null && (verify || a.Window.ProcessCreated > 0) &&
                    appAvailable(a.Window) && (!a.Paused || pausedByPreview.ContainsKey(a.Key)));
                if (target != null) { StartGameExitDestination(route, target); return; }
                // Processo aberto sem janela ou pausado por outra ferramenta nao equivale a nenhum jogo aberto.
                bool otherOpen = gamesView.Activities.Exists(a => !SameGameInstance(a, route.Source) && gameHasExited(a) != true);
                if (!otherOpen) otherOpen = route.KnownOthers.Exists(a => gameHasExited(a) != true);
                if (otherOpen)
                { Feedback("Há outro jogo aberto, mas sua janela ou pausa exige intervenção antes do retorno.", FeedbackKind.Attention); StopGameExitRoute("destino indisponivel"); return; }
                BeginGameTransition(null, false, route);
            }
            catch (Exception ex)
            { WriteLog("jogos: retorno apos saida falhou | " + ex.Message); StopGameExitRoute("leitura/operacao incerta"); }
        }
        void StartGameExitDestination(GameExitRoute route, GameActivity target)
        {
            if (!SameGameInstance(route.Destination, target)) route.ContextCursor = null;
            route.Destination = target;
            if (!target.Paused) { BeginGameTransition(target, false, route, route.ContextCursor); return; }
            route.ContextCursor = CreateGameContextCursor();
            route.Resuming = true; gameBusy = target.Key; ++gameCommandVersion; UpdateGamesPolling();
            GameWork(() => ExecuteGameAction(target.Key, target, ProcessAction.Resume), delegate(string warning, Exception error) {
                gameBusy = null; route.Resuming = false; UpdateGamesPolling();
                if (gameExitRoute != route) return;
                if (error != null || warning != null)
                { Feedback(error == null ? warning : error.Message, error == null ? FeedbackKind.Attention : FeedbackKind.Error); StopGameExitRoute("retomada recusada"); return; }
                target.Paused = false;
                // Voltar ao timer confere de novo foco, modo e prazo depois do worker.
                TickGameExitRoute();
            });
        }
        bool RetryGameExitTransition(GameTransition transition)
        {
            var route = transition.Exit;
            if (route == null || feedbackNow() >= route.Deadline) return false;
            // Nao saltar sobre uma janela temporariamente indisponivel de um processo ainda vivo.
            if (gameHasExited(transition.Activity) != true) return false;
            route.Skipped.Add(transition.Key);
            FinishGameTransition("O destino saiu; conferindo os jogos restantes.", FeedbackKind.Info);
            route.Epoch = gameNavigationEpoch; gameExitRoute = route; gameTransitionTimer.Start();
            TickGameExitRoute(); return true;
        }
        void StopGameExitRoute(string reason)
        {
            if (gameExitRoute == null) return;
            WriteLog("jogos: retorno apos saida cancelado | " + reason);
            gameExitRoute = null;
            if (gameTransition == null) gameTransitionTimer.Stop();
        }
    }
}
