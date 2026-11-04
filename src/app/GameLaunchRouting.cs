// Troca explicita: fechar normalmente, observar a saida por identidade e so entao iniciar.
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class GameLaunchRoute
        {
            public string Key;
            public GameActivity Closing;
            public readonly List<GameActivity> Sources = new List<GameActivity>();
            public int Mode, Panel;
            public DateTime Deadline, SteamReadyAt;
            public bool WaitingSteam, ReleaseArmed;
            public FeedbackNotice Notice;
        }
        readonly DispatcherTimer gameLaunchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        GameLaunchRoute gameLaunchRoute;
        bool gameLaunchTimerWired;

        void BeginGameLaunchRoute(string key)
        {
            if (launchingGame != null || gameLaunchRoute != null || closingGame != null || gameBusy != null || gameOperations.Stopped)
            { Feedback("Aguarde a operação anterior terminar.", FeedbackKind.Pending); return; }
            var entry = gamesView == null ? null : gamesView.Find(key);
            if (entry == null || !entry.Available || !verify && !GameFiles.Ready(entry))
            { Feedback("O jogo está indisponível. Confira o caminho ou abra a Biblioteca.", FeedbackKind.Attention); return; }
            if (gamesView.Activity(key) != null || gamesView.Activities.Count == 0 && SteamSessionCanLaunch()) { RequestGameLaunch(key); return; }
            CancelAppSwitch("Troca de jogo solicitada.");
            if (!gameLaunchTimerWired) { gameLaunchTimerWired = true; gameLaunchTimer.Tick += delegate { TickGameLaunchRoute(); }; }
            var route = new GameLaunchRoute { Key = key, Mode = activationEpoch, Panel = panel, Deadline = feedbackNow().AddSeconds(30) };
            route.Sources.AddRange(gamesView.Activities);
            gameLaunchRoute = route; BeginReleaseWait(); UpdateGamesPolling();
            WriteLog("jogos: fechar antes de iniciar | " + key + " | abertos=" + route.Sources.Count);
            gameLaunchTimer.Start(); TickGameLaunchRoute();
        }
        void TickGameLaunchRoute()
        {
            var route = gameLaunchRoute; if (route == null) { gameLaunchTimer.Stop(); return; }
            try
            {
                bool? mode = activationRead();
                if (disposed || systemPowerBusy || route.Mode != activationEpoch || route.Panel != panel || !mode.HasValue || mode != activationMode)
                { CancelGameLaunchRoute("Abertura cancelada: o contexto mudou ou o fechamento não foi confirmado."); return; }
                if (feedbackNow() >= route.Deadline)
                { CancelGameLaunchRoute(route.WaitingSteam ? "A Steam não confirmou o fim da sessão anterior. Abertura cancelada." :
                    "Abertura cancelada: o fechamento não foi confirmado."); return; }
                var front = foregroundRead();
                bool ownGame = route.Sources.Exists(a => a.Window != null && appOwnsForeground(a.Window));
                if (front != IntPtr.Zero && front != handle && !gameFallback(front) && !ownGame)
                {
                    var steam = findBigPicture();
                    if (steam == null || !existingWindowAvailable(steam.Window) || !appOwnsForeground(steam.Window))
                    { CancelGameLaunchRoute("Abertura cancelada: outra janela foi escolhida.", FeedbackKind.Info); return; }
                }
                if (route.Closing != null || closingGame != null || gameBusy != null || steamInputPending || ButtonsStillHeld()) return;
                if (!gamesSnapshotFresh || gamesSnapshotVersion < gameCommandVersion || gamesView == null) return;
                // Uma instancia nova, inclusive PID reutilizado, nao foi autorizada para fechamento.
                if (gamesView.Activities.Exists(a => !route.Sources.Exists(source => SameGameInstance(source, a))))
                { CancelGameLaunchRoute("A lista de jogos mudou. Confira os jogos abertos antes de iniciar outro."); return; }
                foreach (var source in route.Sources)
                {
                    if (gameHasExited(source) == true) continue;
                    var current = gamesView.Activity(source.Key);
                    if (!SameGameInstance(source, current)) return; // Ausencia na lista nao prova saida.
                    route.Closing = current;
                    gameCloseConfirmation = current.Key; gameCloseForce = false;
                    ConfirmCloseGame(); return;
                }
                if (gamesView.Activities.Count != 0) return; // Esperar tambem o snapshot posterior ao fechamento.
                if (!SteamSessionCanLaunch())
                {
                    route.SteamReadyAt = DateTime.MinValue;
                    route.ReleaseArmed = false;
                    if (!route.WaitingSteam)
                    {
                        route.WaitingSteam = true; route.Deadline = feedbackNow().AddSeconds(45);
                        route.Notice = BeginFeedback((route.Sources.Count > 0 ? "Jogo fechado. " : "") + "Aguardando a Steam concluir a sessão anterior…", "0", 46);
                        route.Notice.Pinned = true; RenderFeedback(); // espera longa: aparece no Inicio (ADR 0078)
                        WriteLog("jogos: aguardando fim da sessao Steam | " + route.Key);
                    }
                    return;
                }
                if (route.WaitingSteam)
                {
                    if (route.SteamReadyAt == DateTime.MinValue) route.SteamReadyAt = feedbackNow().AddMilliseconds(750);
                    if (feedbackNow() < route.SteamReadyAt) return;
                    if (!route.ReleaseArmed) { BeginReleaseWait(); route.ReleaseArmed = true; }
                    if (ButtonsStillHeld()) return;
                }
                string key = route.Key; EndGameLaunchRoute();
                WriteLog("jogos: saida nativa e sessao Steam confirmadas antes de iniciar | " + key);
                RequestGameLaunch(key);
            }
            catch (Exception ex)
            { if (gameLaunchRoute == route) CancelGameLaunchRoute("Não foi possível concluir a troca de jogo. " + ex.Message, FeedbackKind.Error); }
        }
        void CancelGameLaunchRoute(string reason, FeedbackKind kind = FeedbackKind.Warning)
        {
            if (gameLaunchRoute == null) return;
            EndGameLaunchRoute(); WriteLog("jogos: abertura apos fechamento cancelada | " + reason);
            Feedback(reason, kind);
        }
        void EndGameLaunchRoute() { gameLaunchRoute = null; gameLaunchTimer.Stop(); UpdateGamesPolling(); }
    }
}
