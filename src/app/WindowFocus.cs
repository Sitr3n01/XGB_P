// Foco por identidade e diagnostico de janela; nunca aplica geometria ou configuracao de tela.
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        internal bool InspectionWindow;
        Func<IntPtr> foregroundRead = GetForegroundWindow;
        Func<IntPtr, WindowObservation> observeWindow = DesktopWindows.Observe;
        Func<WindowObservation> findBigPicture = DesktopWindows.FindSteamBigPicture;
        Func<DesktopWindow, bool> returnWindowFocus = DesktopWindows.FocusOnly;
        Func<DesktopWindow, bool> existingWindowAvailable = DesktopWindows.Available;
        Func<IntPtr, string> takeForeground = delegate(IntPtr own) { return DesktopWindows.TakeForeground(own, true); };
        Func<bool> barElevated = delegate { return LaunchSettings.Elevated; };
        // App de tras (ADR 0044): a frente pode nao ser um app (Visao de Tarefas, janela vazia do shell).
        static readonly uint OwnProcessId = CurrentProcessId();
        Func<DesktopWindow, bool> previousIsApp = DesktopWindows.Available;
        Func<WindowObservation> frontApp = delegate { return DesktopWindows.FrontApp(OwnProcessId); };
        Func<DesktopWindow, bool> restoreWindowFocus = DesktopWindows.RestoreAndFocus;
        Func<string, bool> steamOwned = DesktopWindows.SteamOwned;
        Func<List<string>> shellCloakedApps = delegate { return DesktopWindows.ShellCloakedApps(OwnProcessId); };
        // O app de tras e da Steam (ou desconhecido): so entao a Steam disputa o controle e vale a espera da ADR 0043.
        bool previousSteam = true;
        // Atalho da Steam fora das pastas dela (Genshin pelo VBS): a Steam acompanha o jogo e abre a propria
        // interface sobre ele no PS sem o ciclo (19:54:57, contexto 769 com o jogo na frente; ADR 0066).
        bool SteamDisputes(DesktopWindow window)
        {
            return window == null || steamOwned(window.ExecutablePath) || SteamManagedGame(window);
        }
        bool SteamManagedGame(DesktopWindow window)
        {
            return window != null && gamesView != null &&
                gamesView.Activities.Exists(a => SteamGameContext.AppId(a.Key) != null && GameOwnsWindow(a, window));
        }
        // Mesma janela ou processo com a mesma criacao: um PID reutilizado nao herda o jogo antigo.
        static bool GameOwnsWindow(GameActivity game, DesktopWindow window)
        {
            if (game.Window != null && game.Window.Handle == window.Handle && game.Window.ProcessId == window.ProcessId &&
                game.Window.ProcessCreated == window.ProcessCreated) return true;
            return window.ProcessCreated > 0 && game.Processes.Exists(p => p.Pid == window.ProcessId && p.Created == window.ProcessCreated);
        }
        string lastShellCloaked;
        static uint CurrentProcessId() { using (var process = System.Diagnostics.Process.GetCurrentProcess()) return (uint)process.Id; }
        // A barra ja teve o primeiro plano nesta abertura: se perder depois, outra janela foi escolhida e e respeitada.
        bool foregroundSinceOpen, elevationHintShown;
        DateTime foregroundRetryAt = DateTime.MinValue;
        readonly DispatcherTimer windowTraceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        Func<DateTime> windowFocusNow = delegate { return DateTime.UtcNow; };
        WindowObservation previousWindow;
        DesktopWindow tracedPreviousWindow;
        string lastPreviousTrace, lastSteamTrace;
        string lastWindowTraceError;
        IntPtr lastObservedForeground;
        DateTime windowTraceUntil;

        void StartWindowFocus()
        {
            windowTraceTimer.Tick += delegate { TraceWindowChanges(); };
            window.Activated += delegate { foregroundSinceOpen = true; };
            if (verify) ConfigureWindowFocusVerification();
        }
        // Depois do handle: a camada sai do Alt+Tab/Visao de tarefas. Teste do modo Xbox, ver ADR 0040.
        void MarkOverlayWindow()
        {
            if (InspectionWindow) return; // a inspecao explicita precisa aparecer no inventario de janelas
            try { DesktopWindows.MarkToolWindow(handle); }
            catch (Exception ex) { WriteLog("janela: estilo de ferramenta indisponivel | " + ex.Message); }
        }
        void ConfigureWindowFocusVerification()
        {
            foregroundRead = delegate { return IntPtr.Zero; };
            observeWindow = delegate { return null; }; findBigPicture = delegate { return null; };
            returnWindowFocus = delegate { return false; }; existingWindowAvailable = delegate { return false; };
            takeForeground = delegate { return "substituido"; }; foregroundRetryAt = DateTime.MinValue;
            barElevated = delegate { return false; }; elevationHintShown = false;
            previousIsApp = delegate { return true; }; frontApp = delegate { return null; }; restoreWindowFocus = delegate { return false; };
            steamOwned = delegate { return true; }; shellCloakedApps = delegate { return new List<string>(); };
            previousSteam = true; lastShellCloaked = null;
            ConfigureSteamInputVerification();
            previousWindow = null; tracedPreviousWindow = null;
            lastPreviousTrace = lastSteamTrace = null; windowTraceTimer.Stop();
        }
        void CapturePreviousWindow()
        {
            previousWindow = null; tracedPreviousWindow = null; previousForegroundPid = 0;
            try
            {
                IntPtr previous = foregroundRead();
                if (previous != IntPtr.Zero && previous != handle) previousWindow = observeWindow(previous);
                // Visao de Tarefas fecha ao perder o foco e a janela vazia do shell recusa o retorno (14:35:23–14:35:32):
                // o destino passa a ser o app de cima, o que o Windows mostraria. Tambem quando a barra oculta ainda e a
                // janela da frente (retorno anterior recusado, 14:35:04).
                if (previous != IntPtr.Zero && (previousWindow == null || !previousIsApp(previousWindow.Window)))
                {
                    var app = frontApp();
                    WriteLog("janela: frente nao e app | hwnd=" + previous.ToInt64() + " \"" + (previousWindow == null ? "" : previousWindow.Window.Title) +
                        "\"; retorno para " + (app == null ? "nenhum" : app.Window.Handle.ToInt64() + " \"" + app.Window.Title + "\""));
                    previousWindow = app;
                }
                previousSteam = SteamDisputes(previousWindow == null ? null : previousWindow.Window);
                if (activationMode == true)
                {
                    string hidden = string.Join("; ", shellCloakedApps().ToArray());
                    if (hidden != lastShellCloaked) { lastShellCloaked = hidden; WriteLog("janela: escondidas pelo shell | " + (hidden.Length == 0 ? "nenhuma" : hidden)); }
                }
                if (previousWindow != null) previousForegroundPid = (int)previousWindow.Window.ProcessId;
                else previousForegroundPid = 0;
                tracedPreviousWindow = previousWindow == null ? null : previousWindow.Window;
                lastPreviousTrace = lastSteamTrace = null;
                TraceWindowChanges("barra: antes de abrir");
                if (!verify) windowTraceTimer.Start();
            }
            catch (Exception ex) { WriteLog("janela: falha ao capturar foco | " + ex.Message); }
        }
        // A barra aberta precisa do primeiro plano: sem ele o controle segue com a Steam/jogo e a barra ignora a entrada.
        void TakeBarForeground(string reason)
        {
            IntPtr before = foregroundRead();
            string result;
            try { result = takeForeground(handle); }
            catch (Exception ex) { result = "falha | " + ex.Message; }
            WriteLog("foco: barra | " + reason + " | " + result + "; antes=" + before.ToInt64() + "; foreground=" + foregroundRead().ToInt64());
            // Steam/DSX elevados isolam a fila de entrada: so uma barra tambem elevada consegue o foco (ADR 0040). Um aviso por abertura.
            if (result.Contains("erro 5") && !result.Contains("concedido") && !elevationHintShown && !barElevated())
            {
                elevationHintShown = true;
                Feedback("A Steam ou o jogo roda como administrador. Para a barra receber o controle, ligue “Sempre executar como administrador” em Configurações.", FeedbackKind.Attention);
            }
        }
        // Pedido negado na abertura: o proximo aperto tenta de novo, no maximo a cada 500 ms, ate a barra ter o foco uma vez.
        bool RetakeControllerFocus(string button)
        {
            if (ControllerFocused()) return true;
            if (foregroundSinceOpen || controllerNow() < foregroundRetryAt) return false;
            foregroundRetryAt = controllerNow().AddMilliseconds(500);
            TakeBarForeground("entrada " + button);
            return ControllerFocused();
        }
        void ReleasePreviousWindow(bool restore)
        {
            var previous = previousWindow; previousWindow = null;
            try
            {
                TraceWindowChanges("barra: antes de ocultar");
                // Se outra janela ja ganhou foco, respeitar essa escolha. Nao minimizar/redimensionar.
                if (restore && previous != null && foregroundRead() == handle)
                {
                    // Minimizou com a barra na frente (jogo que minimiza ao perder o foco): restaurar. Minimizada antes da barra, nao.
                    var current = observeWindow(previous.Window.Handle);
                    bool minimizedMeanwhile = !previous.Window.Minimized && current != null && current.Window.Minimized &&
                        current.Window.ProcessId == previous.Window.ProcessId && current.Window.ProcessCreated == previous.Window.ProcessCreated;
                    bool accepted = minimizedMeanwhile ? restoreWindowFocus(previous.Window) : returnWindowFocus(previous.Window);
                    WriteLog("foco: retorno solicitado | hwnd=" + previous.Window.Handle.ToInt64() + "; aceito=" + accepted +
                        (minimizedMeanwhile ? "; restaurada (minimizou com a barra na frente)" : ""));
                }
                else if (restore && previous != null) WriteLog("foco: retorno dispensado; outra janela ja esta em primeiro plano");
            }
            catch (Exception ex) { WriteLog("foco: falha ao devolver | " + ex.Message); }
            windowTraceUntil = windowFocusNow().AddSeconds(2);
            if (!verify) windowTraceTimer.Start();
        }
        void TraceWindowChanges() { TraceWindowChanges(null); }
        void TraceWindowChanges(string reason)
        {
            if (disposed) { windowTraceTimer.Stop(); return; }
            try
            {
                IntPtr foreground = foregroundRead();
                if (reason == null && foreground != lastObservedForeground) reason = "foco mudou";
                lastObservedForeground = foreground;
                WindowObservation previous = tracedPreviousWindow == null ? null : observeWindow(tracedPreviousWindow.Handle);
                // O HWND pode ter sido reciclado; nao atribuir sua nova geometria ao app anterior.
                if (previous != null && (previous.Window.ProcessId != tracedPreviousWindow.ProcessId ||
                    previous.Window.ProcessCreated != tracedPreviousWindow.ProcessCreated || previous.Window.ClassName != tracedPreviousWindow.ClassName)) previous = null;
                TraceWindow("anterior", previous, reason, ref lastPreviousTrace);
                TraceWindow("Big Picture", findBigPicture(), reason, ref lastSteamTrace);
                lastWindowTraceError = null;
            }
            catch (Exception ex)
            {
                if (lastWindowTraceError != ex.Message) WriteLog("janela: observacao indisponivel | " + ex.Message);
                lastWindowTraceError = ex.Message;
            }
            if (!window.IsVisible && windowFocusNow() >= windowTraceUntil) windowTraceTimer.Stop();
        }
        void TraceWindow(string name, WindowObservation observation, string reason, ref string last)
        {
            string description = observation == null ? "ausente" : observation.Describe();
            if (reason != null || description != last)
            {
                last = description;
                WriteLog("janela: " + name + " | " + (reason ?? "mudou") + " | " + description +
                    "; foreground=" + foregroundRead().ToInt64());
            }
        }
    }
}
