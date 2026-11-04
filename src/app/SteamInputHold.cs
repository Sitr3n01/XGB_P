// Abertura da barra: a Steam vai na hora para o layout de desktop, sem esperar perceber a troca de foco (ADR 0041).
// O layout e liberado com a barra ainda na frente; assim a proxima troca de foco volta a escolher pelo app.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed class SteamInputCycle
    {
        public bool ForceSent, Forced, ReleaseSent, Released, Repeated, LogReadable;
        public int ForceMs, ReleaseMs;
        public string Error;
        public bool DesktopConfirmed, Ready, CanForget, Cancelled;
        public SteamInputAttempt Attempt;
    }
    sealed partial class Preview
    {
        const int SteamInputTimeoutMs = 2500;
        Func<bool> steamInputAvailable = SteamInput.Running;
        Action<string> steamInputLaunch = LaunchSteamProtocol;
        Func<Func<string>> steamInputLog = delegate { return new SteamConsoleTail(SteamInput.ConsoleLog()).ReadNew; };
        Action<int> steamInputWait = Thread.Sleep;
        Action<Func<SteamInputCycle>> steamInputRun;
        // Leva o aviso de "a Steam soltou o controle" da thread do ciclo para a interface.
        Action<Action> steamInputNotify;
        // A entrada visual e normal; o painel permanece apagado e sem navegar ate a confirmacao.
        const int SteamGateCapMs = 3000, SteamBarGateCapMs = 8000;
        const double SteamGateOpacity = .45;
        bool steamGate;
        DateTime steamGateSince, steamGateUntil;
        string steamInputMarker = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings", "steam-input-forcado.txt");
        bool steamInputPending, steamInputDoneThisOpening, steamHideDeferred;
        bool steamHideForGame;
        volatile bool steamInputCancelled;
        readonly object steamInputDispatchSync = new object();
        Action<string> steamBarInputLaunch = LaunchBarSteamInput;
        Func<Func<string>> steamBarConsoleLog = delegate { return new SteamInputLogCursor(SteamInput.ConsoleLog()).ReadNew; };
        Func<Func<string>> steamBarContextLog = delegate { return new SteamInputLogCursor(SteamInput.ControllerLog()).ReadNew; };
        SteamInputAttempt steamInputAttempt;
        long steamInputEpoch;
        bool steamGateFailed;

        static void LaunchBarSteamInput(string uri)
        {
            if (!SteamFastNavigation.InputSupported(uri)) throw new ArgumentException("Comando fora do ciclo da barra.");
            if (!SteamInput.Running()) throw new IOException("A Steam encerrou antes do despacho.");
            if (SteamFastNavigation.TrySendInput(uri)) { WriteLog("steam input: despacho local | " + uri); return; }
            if (!SteamInput.Running()) throw new IOException("A Steam encerrou antes do protocolo.");
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        }

        void StartSteamInput()
        {
            window.Activated += delegate { MaybeHoldSteamInput(); };
            if (verify) { ConfigureSteamInputVerification(); return; }
            steamInputRun = RunSteamInputWorker;
            steamInputNotify = Dispatch;
            RecoverSteamInput();
        }
        void ConfigureSteamInputVerification()
        {
            steamInputAvailable = delegate { return false; };
            steamInputLaunch = delegate { };
            steamBarInputLaunch = delegate { };
            steamBarConsoleLog = steamBarContextLog = delegate { return delegate { return null; }; };
            steamInputLog = delegate { return delegate { return null; }; };
            steamInputWait = delegate { };
            steamInputRun = delegate(Func<SteamInputCycle> body) { CompleteSteamInput(body()); };
            steamInputNotify = delegate(Action action) { action(); };
            steamInputMarker = Path.Combine(Output, "steam-input-fixture", "steam-input-forcado.txt");
            steamInputPending = steamHideDeferred = steamInputCancelled = steamGuidePending = steamGuideOpening = false;
            if (steamInputAttempt != null) steamInputAttempt.Cancelled = true;
            steamInputAttempt = null;
            EndSteamGate();
        }
        void RunSteamInputWorker(Func<SteamInputCycle> body)
        {
            bool queued = steamInputWork.Enqueue(delegate {
                SteamInputCycle result;
                try { result = body(); }
                catch (Exception ex) { result = new SteamInputCycle { Error = ex.Message }; }
                Dispatch(delegate { CompleteSteamInput(result); });
            });
            if (!queued) { steamInputPending = false; FailSteamGate("fila ocupada; diario preservado"); }
        }
        // Encerramento anterior no meio do ciclo: libera o layout. Sem a Steam aberta nao ha layout forcado a liberar.
        void RecoverSteamInput()
        {
            if (!File.Exists(steamInputMarker)) return;
            try
            {
                if (steamInputAvailable())
                {
                    var read = steamBarConsoleLog(); steamBarInputLaunch(SteamInput.Release);
                    bool confirmed = false;
                    for (int waited = 0; waited <= SteamInputTimeoutMs; waited += 25)
                    {
                        if (SteamInputObservation.Executed(read(), SteamInput.Release)) { confirmed = true; break; }
                        steamInputWait(25);
                    }
                    if (!confirmed) throw new IOException("A Steam nao confirmou a recuperacao; diario preservado.");
                }
                File.Delete(steamInputMarker);
                WriteLog("steam input: layout liberado apos encerramento anterior");
            }
            catch (Exception ex) { WriteLog("steam input: falha ao liberar apos encerramento anterior | " + ex.Message); }
        }
        // So com a barra em primeiro plano: liberar com outra janela na frente deixaria a Steam no layout de desktop.
        // So quando o PS que abriu a barra chegou a Steam: sem ele, a Steam troca de layout sozinha na hora (ADR 0042).
        void MaybeHoldSteamInput()
        {
            // Com outro app na frente, a Steam ja fica no desktop e nao volta ao Big Picture no PS (14:35:04, 443510 -> 413080).
            if (!steamGuideOpening || !previousSteam || !window.IsVisible || steamInputDoneThisOpening || steamHideDeferred || !ControllerFocused()) return;
            if (steamInputPending) { if (!steamGate) BeginSteamGate(); return; }
            bool available;
            try { available = steamInputAvailable(); } catch (Exception) { available = false; }
            if (!available) return;
            steamInputPending = steamInputDoneThisOpening = true;
            bool unresolved = File.Exists(steamInputMarker);
            try { Directory.CreateDirectory(Path.GetDirectoryName(steamInputMarker)); File.WriteAllText(steamInputMarker, DateTime.Now.ToString("o")); }
            catch (Exception ex)
            {
                steamInputPending = false; BeginSteamGate(); FailSteamGate("marcador indisponivel | " + ex.Message);
                return;
            }
            BeginSteamGate();
            var attempt = new SteamInputAttempt { Epoch = ++steamInputEpoch, UnresolvedBefore = unresolved, GuideHeld = guideDownDevices.Count > 0, Focused = ControllerFocused };
            steamInputAttempt = attempt;
            steamInputRun(delegate { return RunSteamInputAttempt(attempt); });
        }
        // PS de fechar que a Steam viu com um jogo dela na frente e a barra sem foco (16:21:46, o Neva ficou preso):
        // a barra retoma o foco, forca/libera e so entao devolve o foco a esse jogo (ADR 0066). Com a barra na frente, nada muda.
        void HoldSteamInputForClose()
        {
            if (!steamGuidePending || steamInputPending || steamHideDeferred) return;
            IntPtr front = foregroundRead();
            if (front == IntPtr.Zero || front == handle) return;
            WindowObservation game;
            try { game = observeWindow(front); } catch (Exception) { game = null; }
            if (game == null || !SteamManagedGame(game.Window)) return;
            previousWindow = game; tracedPreviousWindow = game.Window; previousSteam = true;
            steamGuideOpening = true; steamInputDoneThisOpening = false;
            TakeBarForeground("fechamento");
            MaybeHoldSteamInput();
            WriteLog("steam input: PS de fechar com jogo da Steam na frente | hwnd=" + front.ToInt64() + "; ciclo=" + (steamInputDoneThisOpening ? "iniciado" : "nao iniciado"));
        }
        void BeginSteamGate()
        {
            steamGate = true; steamGateFailed = false; steamGateSince = controllerNow(); steamGateUntil = steamGateSince.AddMilliseconds(SteamBarGateCapMs);
            ClearHeld();
            MotionTo(MotionPart("PanelContent"), UIElement.OpacityProperty, SteamGateOpacity, 0);
        }
        // Desktop e /0 confirmados para esta abertura: passa a navegar, sem reproduzir comandos antigos.
        void SteamInputReady(string reason)
        {
            if (!steamGate) return;
            steamGate = false; ClearHeld();
            if (window.IsVisible) MotionTo(MotionPart("PanelContent"), UIElement.OpacityProperty, 1, OpeningGateFadeMs());
            WriteLog("steam input: navegacao liberada | " + reason + " | " + (int)(controllerNow() - steamGateSince).TotalMilliseconds + " ms");
        }
        // Ao ocultar: sem animacao, o proximo abrir comeca com o painel normal.
        void EndSteamGate()
        {
            CancelSteamInputAttempt();
            steamGate = false;
            var content = window.FindName("PanelContent") as FrameworkElement;
            if (content == null) return;
            // So o clock de opacidade do painel; as animacoes dos filhos seguem.
            for (int i = motionClocks.Count - 1; i >= 0; i--)
                if (motionClocks[i].Target == content && motionClocks[i].Property == UIElement.OpacityProperty)
                { ApplyClock(content, UIElement.OpacityProperty, null); motionClocks[i].Clock.Controller.Remove(); motionClocks.RemoveAt(i); }
            content.SetCurrentValue(UIElement.OpacityProperty, 1.0);
        }
        void TickSteamGate()
        {
            if (steamInputAttempt != null && !ControllerFocused()) { steamInputAttempt.Cancelled = true; FailSteamGate("primeiro plano perdido"); }
            if (steamGate && !steamGateFailed && controllerNow() >= steamGateUntil)
            { if (steamInputAttempt != null) steamInputAttempt.Cancelled = true; FailSteamGate("limite de confirmacao"); }
        }
        void FailSteamGate(string reason)
        {
            if (!steamGate || steamGateFailed) return;
            steamGateFailed = true; ClearHeld();
            Feedback("A Steam não confirmou a entrada. Feche a barra e tente novamente.", FeedbackKind.Attention);
            WriteLog("steam input: navegacao bloqueada | " + reason);
        }
        void CancelSteamInputAttempt()
        { ++steamInputEpoch; if (steamInputAttempt != null) steamInputAttempt.Cancelled = true; }
        void UpdateSteamInputGuide()
        {
            var attempt = steamInputAttempt; if (attempt == null) return;
            bool down = guideDownDevices.Count > 0;
            if (down && !attempt.GuideHeld && attempt.DispatchStarted) attempt.Cancelled = true;
            attempt.GuideHeld = down;
        }
        SteamInputCycle RunSteamInputAttempt(SteamInputAttempt attempt)
        {
            return SteamInputTransfer.Run(attempt, steamInputDispatchSync, steamBarInputLaunch,
                steamBarConsoleLog, steamBarContextLog, steamInputWait, steamInputAvailable);
        }
        // Thread propria: protocolo e leitura do console_log da Steam, sem tocar a interface.
        SteamInputCycle SteamInputCycleBody()
        {
            if (steamInputCancelled) return new SteamInputCycle { CanForget = true };
            return RunSteamInputAttempt(new SteamInputAttempt { Focused = delegate { return true; } });
        }
        bool WaitSteamInput(Func<string> read, Func<string, bool> done, SteamInputCycle result)
        {
            for (int waited = 0; ; waited += 50)
            {
                string text = read();
                if (text != null) { result.LogReadable = true; if (done(text)) return true; }
                if (waited >= SteamInputTimeoutMs) return false;
                steamInputWait(50);
            }
        }
        void CompleteSteamInput(SteamInputCycle result)
        {
            bool current = result.Attempt == null || result.Attempt == steamInputAttempt && result.Attempt.Epoch == steamInputEpoch && !result.Attempt.Cancelled;
            steamInputPending = false;
            steamInputAttempt = null;
            if (current && result.Ready) SteamInputReady("desktop e liberacao confirmados");
            else if (current) FailSteamGate(result.Error ?? "transferencia nao confirmada");
            // Entrega incerta ou /0 sem confirmacao: o diario fica para recuperar no proximo inicio.
            if (result.CanForget)
                try { if (File.Exists(steamInputMarker)) File.Delete(steamInputMarker); } catch (Exception) { }
            WriteLog("steam input: " + (!result.ForceSent ? "layout nao solicitado" :
                "desktop " + (result.Forced ? "confirmado em " + result.ForceMs + " ms" : "sem confirmacao em " + result.ForceMs + " ms") +
                "; liberacao " + (!result.ReleaseSent ? "nao despachada" : result.Released ? "confirmada em " + result.ReleaseMs + " ms" : "sem confirmacao em " + result.ReleaseMs + " ms")) +
                (result.Repeated ? "; liberacao repetida" : "") + (result.LogReadable ? "" : "; console_log ilegivel") +
                (result.Error != null ? "; erro: " + result.Error : "") + "; foreground=" + foregroundRead().ToInt64());
            if (steamHideDeferred)
            {
                bool gameHide = steamHideForGame; steamHideForGame = steamHideDeferred = false;
                if (gameHide) GameNavigationUi(delegate { Hide(true); }); else Hide(true);
                return;
            }
            if (!current && window.IsVisible && !steamInputDoneThisOpening) MaybeHoldSteamInput();
            else if (!current && window.IsVisible) FailSteamGate(result.Error ?? "abertura cancelada");
            TrySteamNavigation(); // Pedido ja escolhido: nao aguardar mais um tick de 150 ms.
            if (result.ReleaseSent && window.IsVisible && !ControllerFocused())
                WriteLog("steam input: liberado sem a barra na frente; a Steam escolhe de novo na proxima troca de foco");
        }
        // Fechar no meio do ciclo: a barra some, mas so devolve o foco depois que a Steam liberar.
        // O mesmo vale para o botao que fechou, ainda apertado (ADR 0068).
        // A opacidade volta no fim do Hide, ja com a janela oculta, para o fundo escurecido nao piscar.
        bool DeferHideForSteamInput(bool restorePrevious)
        {
            if (steamHideDeferred) { if (restorePrevious) return true; steamHideDeferred = false; return false; }
            if (!restorePrevious || !window.IsVisible) return false;
            if (!steamInputPending) { if (releaseWaitFinishing) return false; BeginReleaseWait(); if (!ButtonsStillHeld()) return false; }
            steamHideDeferred = true; motionClosing = true;
            steamHideForGame = gameNavigationInternal;
            StopDescription(); ClearHeld();
            window.Opacity = 0; window.IsHitTestVisible = false;
            WriteLog(steamInputPending ? "steam input: fechamento aguarda a liberacao da Steam" : "entrada: fechamento aguarda soltar o botao");
            return true;
        }
        // Botao solto (ou limite de 500 ms) com a Steam ja liberada: conclui o fechamento adiado, sem abrir outra espera.
        bool releaseWaitFinishing;
        void TickReleaseWait()
        {
            if (!steamHideDeferred || steamInputPending || ButtonsStillHeld()) return;
            bool gameHide = steamHideForGame; steamHideForGame = steamHideDeferred = false;
            releaseWaitFinishing = true;
            try { if (gameHide) GameNavigationUi(delegate { Hide(true); }); else Hide(true); }
            finally { releaseWaitFinishing = false; }
        }
        void ReopenDeferredWindow()
        {
            if (!steamHideDeferred) return;
            CancelSteamInputAttempt(); steamInputDoneThisOpening = false;
            steamHideForGame = steamHideDeferred = false; RestoreDeferredWindow();
            WriteLog("steam input: barra reaberta antes da liberacao");
        }
        void RestoreDeferredWindow() { window.Opacity = 1; window.IsHitTestVisible = true; }
        void ReleaseSteamInputOnExit()
        {
            steamInputCancelled = true;
            CancelSteamInputAttempt();
            if (!steamInputPending && !File.Exists(steamInputMarker)) return;
            try
            {
                lock (steamInputDispatchSync)
                {
                    if (steamInputAvailable()) steamBarInputLaunch(SteamInput.Release);
                    // Ainda pode haver limpeza enfileirada. A proxima inicializacao confirma /0.
                    else File.Delete(steamInputMarker);
                }
                WriteLog("steam input: liberacao solicitada ao encerrar; diario preservado ate confirmacao");
            }
            catch (Exception ex) { WriteLog("steam input: falha ao liberar ao encerrar | " + ex.Message); }
        }
    }
}
