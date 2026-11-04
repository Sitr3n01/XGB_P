// Adaptador da Home: identidade completa e registro de pausas ficam dentro da transacao do worker.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        PauseRecoveryStore pauseRecovery;
        void StartPauseRecovery()
        {
            pauseRecovery = new PauseRecoveryStore(Path.Combine(GameFiles.Folder, "paused-by-xgb.json"));
            try { pauseRecovery.Load(pausedByPreview); gameOperations.Execute(delegate { ResumePausedGames(); return null; }); }
            catch (Exception ex) { WriteLog("jogos: diario de pausa preservado | " + ex.Message); }
        }
        bool PersistPausedState()
        {
            if (verify || pauseRecovery == null) return true;
            try { pauseRecovery.Save(pausedByPreview); return true; }
            catch (Exception ex) { WriteLog("jogos: diario de pausa indisponivel | " + ex.Message); return false; }
        }
        void ResumeGame()
        {
            var featured = gamesView == null ? null : gamesView.Featured;
            if (featured == null) { Feedback("Nenhum jogo executado ainda."); return; }
            SelectGame(featured.Key);
        }
        void SelectGame(string key)
        {
            if (gamesView == null) return;
            if (gameLaunchRoute != null) { Feedback("Aguarde o fechamento antes de iniciar outro jogo.", FeedbackKind.Pending); return; }
            var open = gamesView.Activity(key);
            if (open != null) { ResumeOpenGame(key, open); return; }
            if (gamesView.Activities.Count > 0)
            { gameCloseConfirmation = null; gameCloseForce = false; gameConfirmation = key; RenderGames(true); FocusHome("game-cancel"); ActionSound("lista"); return; }
            RequestGameLaunch(key);
        }
        // Jogo aberto nunca e lancado de novo: retoma a pausa, se houver, e traz a janela.
        int gameNavigationEpoch;
        void ResumeOpenGame(string key, GameActivity open)
        {
            if (gameBusy != null || gameOperations.Stopped) { Feedback("Aguarde a operacao anterior terminar."); return; }
            if (launchingGame == key || unconfirmedLaunch == key)
            {
                FinishFeedback(gameLaunchFeedback, "Jogo aberto.", FeedbackKind.Success);
                launchingGame = unconfirmedLaunch = null; UpdateGamesPolling();
            }
            int intent = ++gameNavigationEpoch, mode = activationEpoch;
            if (!open.Paused) { BringGameWindow(open, false, intent, mode); return; }
            gameBusy = key; ++gameCommandVersion; pauseNotices.Remove(key); UpdateGamesPolling();
            var resumeContext = CreateGameContextCursor();
            var notice = BeginFeedback("Retomando o jogo...", "0");
            GameWork(() => ExecuteGameAction(key, open, ProcessAction.Resume), delegate(string warning, Exception error) {
                gameBusy = null; UpdateGamesPolling();
                if (error != null || warning != null)
                {
                    FinishFeedback(notice, error == null ? warning : error.Message, error == null ? FeedbackKind.Attention : FeedbackKind.Error);
                    return;
                }
                open.Paused = false;
                FinishFeedback(notice, "Retomada solicitada.", FeedbackKind.Info);
                WriteLog("jogos: retomado ao voltar | " + key);
                BringGameWindow(open, true, intent, mode, resumeContext);
            });
        }
        void BringGameWindow(GameActivity open, bool resumed, int intent, int mode, Func<string> context = null)
        {
            if (disposed || intent != gameNavigationEpoch || mode != activationEpoch || !window.IsVisible ||
                !ControllerFocused() && (open.Window == null || !appOwnsForeground(open.Window))) return;
            BeginGameTransition(open, resumed, null, context);
        }
        // Uma leitura incerta nunca apaga a recuperacao. A retomada verifica cada identidade no handle nativo.
        void ApplyPausedState(GamesView view)
        {
            foreach (var key in new List<string>(pausedByPreview.Keys))
            {
                PausedGame mine; if (!pausedByPreview.TryGetValue(key, out mine)) continue;
                var a = view.Activity(key); long created;
                bool sameRoot = a != null && a.Pids.Contains(mine.Pid) && (verify ||
                    mine.Members.TryGetValue(mine.Pid, out created) && a.Processes.Exists(p => p.Pid == mine.Pid && p.Created == created));
                if (!sameRoot || a.SuspendedShare == 0)
                {
                    RecoverPausedGame(key, mine, sameRoot ? "retomada externa" : "principal ausente ou incerto");
                    if (a != null) a.Paused = pausedByPreview.ContainsKey(key);
                    continue;
                }
                a.Paused = true;
            }
        }
        void RecoverPausedGame(string key, PausedGame registered, string reason)
        {
            try {
                string warning = gameOperations.Execute(() => ResumeRegistered(key, registered));
                WriteLog("jogos: recuperacao | " + reason + " | " + key + (warning == null ? "" : " | " + warning));
            }
            catch (Exception ex) { WriteLog("jogos: recuperacao pendente | " + key + " | " + ex.Message); }
        }
        static List<uint> GameRoots(GameActivity a)
        {
            var roots = new List<uint>(); if (a.Pid != 0) roots.Add(a.Pid);
            foreach (var pid in a.Pids) if (!roots.Contains(pid)) roots.Add(pid);
            return roots;
        }
        // Acoes de processo fora da thread da interface; na verificacao, sincronas e com substitutos.
        void GameWork(Func<string> work, Action<string, Exception> done)
        {
            if (verify) { string r = null; Exception x = null; try { r = gameOperations.Execute(work); } catch (Exception ex) { x = ex; } done(r, x); return; }
            Task.Factory.StartNew(delegate {
                string r = null; Exception x = null; try { r = gameOperations.Execute(work); } catch (Exception ex) { x = ex; }
                Dispatch(delegate { done(r, x); });
            });
        }
        // Pausar como o Nyrna: minimizar, esperar a janela sair e suspender o processo e os descendentes. Retomar desfaz a suspensao.
        void ToggleGamePause(string key)
        {
            var open = gamesView == null ? null : gamesView.Activity(key); var game = gamesView == null ? null : gamesView.Find(key);
            if (open == null || game == null) { Feedback("O jogo já foi fechado."); RenderGames(true); return; }
            if (gameBusy != null) { FinishFeedback(gameBusyFeedback, "Aguarde a ação anterior terminar.", FeedbackKind.Pending); return; }
            bool pause = !open.Paused; var roots = GameRoots(open); string name = game.Name;
            if (roots.Count == 0) { Feedback("Não foi possível identificar o processo do jogo.", FeedbackKind.Error); return; }
            gameBusy = key; long version = ++gameCommandVersion;
            var notice = BeginFeedback((pause ? "Pausando " : "Retomando ") + name + "…", "0"); UpdateGamesPolling();
            gameBusyFeedback = notice;
            if (pause && open.Window != null) {
                if (!verify && !DesktopWindows.Belongs(open.Window)) { gameBusy = null; Feedback("A janela do jogo mudou. Atualize a lista.", FeedbackKind.Attention); return; }
                gameMinimize(open.Window.Handle);
            }
            // Calculado aqui, na thread da interface: o registro de pausas nao e lido pelo worker.
            GameWork(delegate {
                if (pause && !verify) gameOperations.Delay(500);
                return ExecuteGameAction(key, open, pause ? ProcessAction.Suspend : ProcessAction.Resume);
            }, delegate(string warning, Exception error) {
                gameBusy = null;
                if (error != null) { FinishFeedback(notice, error.Message, FeedbackKind.Error); WriteLog("jogos: " + (pause ? "pausar" : "retomar") + " falhou | " + key + " | " + error.Message); UpdateGamesPolling(); RenderGames(true); return; }
                // Otimista ate a proxima leitura confirmar pelos threads.
                var now = gamesView == null ? null : gamesView.Activity(key); if (now != null) now.Paused = pause || warning != null;
                if (!string.IsNullOrEmpty(warning)) FinishFeedback(notice, warning, FeedbackKind.Attention);
                else
                {
                    pauseNotices[key] = new PauseNotice { Key = key, Name = name, Paused = pause, Version = version, Deadline = feedbackNow().AddSeconds(10), Notice = notice };
                    notice.Deadline = pauseNotices[key].Deadline;
                    FinishFeedback(notice, (pause ? "Pausa solicitada para " : "Retomada solicitada para ") + name + ". Aguardando confirmação…", FeedbackKind.Pending);
                }
                UpdateGamesPolling();
                RenderGames(true);
            });
        }
        void AskCloseGame(string key, string origin)
        {
            if (gamesView == null || gamesView.Activity(key) == null) { Feedback("O jogo já foi fechado."); RenderGames(true); return; }
            gameConfirmation = null; gameCloseConfirmation = key; gameCloseOrigin = origin; gameCloseForce = false; RenderGames(true); FocusHome("game-cancel");
            ActionSound("lista");
        }
        // Ocultar apos o pedido aceito, liberando a tela para o jogo; confirmar a saida em segundo plano.
        // Depois de 10 s sem sair, oferecer forcar na proxima abertura, sem reabrir a barra.
        void ConfirmCloseGame()
        {
            string key = gameCloseConfirmation; bool force = gameCloseForce;
            var open = gamesView == null ? null : gamesView.Activity(key);
            var game = gamesView == null ? null : gamesView.Find(key);
            string name = game == null ? "O jogo" : game.Name;
            if (open == null) { gameCloseConfirmation = null; Feedback(name + " ja foi fechado."); RenderGames(true); FocusHome("resume"); return; }
            if (gameBusy != null || gameOperations.Stopped) { Feedback("Aguarde a operacao anterior terminar."); return; }
            if (closingGame != null) { FinishFeedback(gameCloseFeedback, "Aguarde a confirmacao do fechamento de " + closingName + ".", FeedbackKind.Pending); return; }
            gameCloseConfirmation = null; gameCloseForce = false;
            gameCloseFeedback = BeginFeedback("Solicitando fechamento de " + name + "...", "0");
            if (!force && open.Window == null)
            {
                CancelGameLaunchRoute("Abertura cancelada: o jogo atual não tem janela para fechar normalmente.", FeedbackKind.Attention);
                gameCloseConfirmation = key; gameCloseForce = true; RenderGames(true); FocusHome("game-cancel");
                FinishFeedback(gameCloseFeedback, name + " nao tem janela para fechar normalmente.", FeedbackKind.Attention); return;
            }
            gameBusy = key; closeVersion = ++gameCommandVersion; pauseNotices.Remove(key);
            int intent = ++gameNavigationEpoch, mode = activationEpoch;
            var replacement = gameLaunchRoute;
            bool replacing = replacement != null && SameGameInstance(replacement.Closing, open);
            bool returnAfterExit = !replacing && (GameWasForeground(open) || gamesView.Activities.Count == 1);
            UpdateGamesPolling();
            GameWork(delegate {
                if (force) return ExecuteGameAction(key, open, ProcessAction.Kill);
                if (open.Paused)
                {
                    string warning = ExecuteGameAction(key, open, ProcessAction.Resume);
                    if (warning != null) throw new InvalidOperationException(warning);
                }
                if (verify) gameCloseWindow(open.Window.Handle); else DesktopWindows.Close(open.Window);
                return null;
            }, delegate(string warning, Exception error) {
                gameBusy = null;
                if (error != null)
                {
                    EndGameLaunchRoute();
                    FinishFeedback(gameCloseFeedback, error.Message, FeedbackKind.Error);
                    WriteLog("jogos: fechar falhou | " + key + " | " + error.Message);
                }
                else
                {
                    closingActivity = open;
                    closingGame = key; closingName = name; closeDeadline = feedbackNow().AddSeconds(10);
                    FinishFeedback(gameCloseFeedback, "Fechamento solicitado para " + name + ". Aguardando saida..." +
                        (warning == null ? "" : " " + warning), FeedbackKind.Pending);
                    WriteLog("jogos: fechamento solicitado | " + key + " | forcado=" + force);
                }
                UpdateGamesPolling(); RenderGames(true);
                if (intent != gameNavigationEpoch || mode != activationEpoch) return;
                if (error == null)
                {
                    if (replacing)
                    {
                        TickGameLaunchRoute();
                        if (gameLaunchRoute != replacement) return; // Cancelamento durante o worker nao devolve foco nem oculta outra escolha.
                    }
                    if (returnAfterExit) ArmGameExitRoute(open, false);
                    GameNavigationUi(delegate { Hide(true); });
                }
                else FocusHome(actions.Exists(c => (string)c.Tag == gameCloseOrigin) ? gameCloseOrigin : "resume");
            });
        }
        void CheckGameClose(bool fresh = true, long readVersion = long.MaxValue)
        {
            if (closingGame == null) return;
            if (!fresh || readVersion < closeVersion)
            {
                if (feedbackNow() >= closeDeadline.AddSeconds(2))
                {
                    FinishFeedback(gameCloseFeedback, "Não foi possível confirmar o fechamento de " + closingName + ". Confira o jogo antes de tentar novamente.", FeedbackKind.Warning);
                    closingGame = null; closingActivity = null; StopGameExitRoute("saida nao confirmada"); UpdateGamesPolling();
                    CancelGameLaunchRoute("Abertura cancelada: não foi possível confirmar o fechamento do jogo atual.");
                }
                return;
            }
            if (gamesView != null && gamesView.Activity(closingGame) == null && (verify && closingActivity == null || gameHasExited(closingActivity) == true))
            {
                WriteLog("jogos: fechado | " + closingGame);
                if (gameCloseFeedback != null) FinishFeedback(gameCloseFeedback, closingName + " fechado.", FeedbackKind.Success);
                else Feedback(closingName + " fechado.", FeedbackKind.Success);
                if (gameExitRoute != null && gameExitRoute.Source == closingActivity)
                { gameExitRoute.Ready = true; gameExitRoute.Deadline = feedbackNow().AddSeconds(8); }
                if (gameLaunchRoute != null && SameGameInstance(gameLaunchRoute.Closing, closingActivity)) gameLaunchRoute.Closing = null;
                closingGame = null; closingActivity = null; UpdateGamesPolling(); TickGameExitRoute(); TickGameLaunchRoute(); return;
            }
            if (feedbackNow() < closeDeadline) return;
            gameCloseConfirmation = closingGame; gameCloseForce = true; closingGame = null;
            StopGameExitRoute("jogo nao confirmou a saida; preservar a confirmacao dele");
            CancelGameLaunchRoute("Abertura cancelada: o jogo atual ainda não fechou.", FeedbackKind.Attention);
            FinishFeedback(gameCloseFeedback, closingName + " não fechou. Ele pode estar pedindo confirmação na própria tela.", FeedbackKind.Attention);
            UpdateGamesPolling();
            if (window.IsVisible && panel == 0) { RenderGames(true); FocusHome("game-cancel"); }
        }
        // Na saida tratada, tenta retomar os mesmos processos (PID e criacao). Recusas ficam no diario para a proxima abertura.
        void ResumePausedGames()
        {
            if (verify) { pausedByPreview.Clear(); return; }
            RecoverPausedRegistry();
        }
        void RecoverPausedRegistry()
        {
            foreach (var pair in new List<KeyValuePair<string, PausedGame>>(pausedByPreview))
                try
                {
                    string warning = ResumeRegistered(pair.Key, pair.Value);
                    WriteLog("jogos: recuperacao ao encerrar | " + pair.Key + (warning == null ? "" : " | " + warning));
                }
                catch (Exception ex) { WriteLog("jogos: retomar ao encerrar falhou | " + pair.Key + " | " + ex.Message); }
        }

        GameProcessService gameProcessService;
        void StartGameProcessService()
        {
            gameProcessService = new GameProcessService(pausedByPreview,
                activity => verify ? FixtureGameTree(activity) : GameControl.CheckedTree(GameIdentities(activity)),
                (targets, action) => verify ? FixtureGameAction(targets, action) : GameControl.Apply(targets, action),
                PersistPausedState);
        }
        List<ProcessIdentity> GameIdentities(GameActivity activity)
        {
            var result = new List<ProcessIdentity>();
            foreach (uint pid in GameRoots(activity))
            {
                var known = activity.Processes.Find(p => p.Pid == pid);
                if (known == null || !known.Known)
                    throw new InvalidOperationException("Nao foi possivel confirmar a identidade do processo do jogo. Nenhuma acao foi enviada.");
                result.Add(known);
            }
            return result;
        }
        List<ProcessIdentity> FixtureGameTree(GameActivity activity)
        {
            var result = new List<ProcessIdentity>();
            foreach (uint pid in gameTree(GameRoots(activity))) result.Add(new ProcessIdentity(pid, 1));
            return result;
        }
        ProcessActionResult FixtureGameAction(IList<ProcessIdentity> targets, ProcessAction action)
        {
            var pids = new List<uint>(); foreach (var target in targets) pids.Add(target.Pid);
            string warning = action == ProcessAction.Suspend ? gameSuspend(pids) :
                action == ProcessAction.Resume ? gameResume(pids) : gameKill(pids);
            var result = new ProcessActionResult { OperationWarning = warning };
            foreach (var target in targets) result.Items.Add(new ProcessActionItem { Identity = target, State = ProcessActionState.Applied });
            return result;
        }
        string ExecuteGameAction(string key, GameActivity activity, ProcessAction action)
        { return gameProcessService.Execute(key, activity, action); }
        void RetainPendingPause(string key, ProcessActionResult result)
        { gameProcessService.RetainPending(key, result); }
        string ResumeRegistered(string key, PausedGame registered)
        { return gameProcessService.ResumeRegistered(key, registered); }
    }
}
