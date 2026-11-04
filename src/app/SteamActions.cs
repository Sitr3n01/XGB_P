// Navegacao real da Steam, apenas por protocolos existentes. Nao instala nem modifica o cliente.
using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Threading;
using XGBP.Gaming;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const string SteamHomeUri = "steam://open/bigpicture";
        const string SteamStartPageUri = "steam://open/library";
        const string SteamDownloadsUri = "steam://open/downloads";
        const string SteamSettingsUri = "steam://open/settings";
        const string WindowsSettingsUri = "ms-settings:";
        Action<string> steamLaunch = LaunchSteamProtocol;
        Action<string> settingsLaunch = LaunchSteamProtocol;
        Func<bool?> xboxMode = GamingExperience.Active;
        string lastSteamUri;
        string lastSettingsUri;
        DispatcherTimer steamNavigationTimer;
        string steamNavigationUri, steamNavigationDestination;
        DateTime steamNavigationDeadline;
        long steamNavigationVersion;
        int steamNavigationMode;
        bool steamNavigationStarted;
        bool steamNavigationDispatching;
        bool steamNavigationClosing, steamNavigationExitComplete, steamNavigationHandoff, steamClosingVisual;

        static string SteamLibraryUri(bool? xboxActive)
        {
            // O Big Picture atual trata bigpicture/games como Home. A colecao nativa all-games
            // abre a grade completa da Biblioteca; nao depende da ultima aba nem envia teclas.
            return xboxActive == true ? "steam://open/library/collection/all-games" : "steam://open/games";
        }

        static void LaunchSteamProtocol(string uri)
        {
            if (SteamFastNavigation.TrySend(uri)) { WriteLog("Steam: despacho local | " + uri); return; }
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        }

        void OpenSteamHome()
        {
            NavigateSteamPage(SteamStartPageUri, "Início do Big Picture");
        }
        bool ReturnHomeWithoutGame()
        {
            if (activationMode != true || activationRead() != true || !gamesSnapshotFresh || gamesSnapshotVersion < gameCommandVersion ||
                gamesView == null || gamesView.Activities.Count != 0 || launchingGame != null || unconfirmedLaunch != null ||
                gameLaunchRoute != null || closingGame != null || gameBusy != null) return false;
            BeginReleaseWait(); NavigateSteamPage(SteamStartPageUri, "Início do Big Picture", true); return true;
        }

        // Abrir o modo Big Picture nao navega para Home. A pagina e um pedido separado,
        // entregue antes da transferencia de foco e somente depois de liberar o Steam Input.
        void NavigateSteamPage(string uri, string destination, bool closing = false)
        {
            CancelAppSwitch("Troca de janela substituída pela navegação da Steam.");
            steamNavigationUri = uri; steamNavigationDestination = destination;
            steamNavigationVersion = gameNavigationEpoch;
            steamNavigationMode = activationEpoch;
            steamNavigationDeadline = feedbackNow().AddSeconds(8);
            steamNavigationStarted = false;
            steamNavigationClosing = steamClosingVisual = closing; steamNavigationExitComplete = !closing;
            if (steamNavigationTimer == null)
            {
                steamNavigationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                steamNavigationTimer.Tick += delegate { TrySteamNavigation(); TickSteamReturn(); };
            }
            steamNavigationTimer.Start();
            if (closing) AnimateDismiss(delegate { steamNavigationExitComplete = true; TrySteamNavigation(); });
            TrySteamNavigation();
        }
        void CancelSteamNavigation()
        {
            steamNavigationUri = steamNavigationDestination = null;
            steamNavigationClosing = false;
            steamReturn = null;
            if (steamNavigationTimer != null) steamNavigationTimer.Stop();
            if (!steamNavigationHandoff) RestoreSteamClosingVisual();
        }
        void TrySteamNavigation()
        {
            if (steamNavigationUri == null || steamNavigationDispatching) return;
            if (disposed || steamNavigationVersion != gameNavigationEpoch || steamNavigationMode != activationEpoch ||
                !window.IsVisible || motionClosing && !steamNavigationClosing)
            { CancelSteamNavigation(); return; }
            string destination = steamNavigationDestination;
            long version = steamNavigationVersion;
            steamNavigationDispatching = true;
            try
            {
                if (feedbackNow() >= steamNavigationDeadline)
                    throw new TimeoutException("O Big Picture não ficou disponível a tempo.");
                if (!steamNavigationExitComplete) return;
                if (steamInputPending)
                { if (!ControllerFocused()) CancelSteamNavigation(); return; }
                // PS e acompanhado por dispositivo, fora de buttonsHeldAt. Nunca entregar ainda pressionado.
                if (guideDownDevices.Count > 0 || ButtonsStillHeld())
                { if (!ControllerFocused()) CancelSteamNavigation(); return; }
                var existing = findBigPicture();
                bool available = existing != null && existingWindowAvailable(existing.Window);
                // A janela que acabamos de abrir pode tomar o foco antes do proximo tick.
                // Aceitar apenas essa identidade; outra janela escolhida cancela a navegacao.
                if (!ControllerFocused() && !(steamNavigationStarted && available && appOwnsForeground(existing.Window)))
                { CancelSteamNavigation(); return; }
                if (!available)
                {
                    if (!steamNavigationStarted)
                    {
                        steamNavigationStarted = true; steamLaunch(SteamHomeUri);
                        WriteLog("Steam: abertura do Big Picture solicitada; aguardando janela | " + destination);
                    }
                    return;
                }
                string uri = steamNavigationUri;
                var context = CreateSteamReturnCursor();
                var dispatchTime = Stopwatch.StartNew();
                steamLaunch(uri);
                WriteLog("Steam: navegacao solicitada | " + uri + " | despachoMs=" + dispatchTime.ElapsedMilliseconds + " | " + existing.Describe());
                // ShellExecute pode bombear mensagens WPF. Um fechamento ou escolha nova durante
                // o despacho invalida este retorno; o timer nunca repete um pedido em andamento.
                if (version != gameNavigationEpoch || steamNavigationUri != uri || !window.IsVisible ||
                    motionClosing && !steamNavigationClosing || disposed) return;
                if (guideDownDevices.Count > 0) { CancelSteamNavigation(); return; }
                steamNavigationUri = steamNavigationDestination = null; steamNavigationClosing = false;
                steamNavigationTimer.Stop();
                // O alternador confirma foreground e conserva geometria. A pagina foi solicitada,
                // mas sua renderizacao nao e confirmada por esse teste de foco.
                steamNavigationHandoff = true;
                try { OpenApp(existing.Window, delegate { BeginSteamReturn(existing.Window, context); }); }
                finally { steamNavigationHandoff = false; }
                if (switchingApp == null && window.IsVisible) RestoreSteamClosingVisual();
            }
            catch (SteamNavigationBusyException) { } // Nao sobrescrever comando de outro remetente; tentar no proximo tick.
            catch (Exception ex)
            {
                if (version != gameNavigationEpoch) return;
                CancelSteamNavigation();
                Feedback("Não foi possível abrir " + destination + ". Tente novamente.", FeedbackKind.Error);
                WriteLog("Steam: navegacao falhou | " + destination + " | " + ex.Message);
            }
            finally { steamNavigationDispatching = false; }
        }
        void OpenSteamDownloads() { OpenSteam(SteamDownloadsUri, "Downloads da Steam"); }
        void OpenSteamSettings() { OpenSteam(SteamSettingsUri, "Configurações da Steam"); }
        void OpenWindowsSettings()
        {
            try
            {
                settingsLaunch(WindowsSettingsUri);
                WriteLog("Configuracoes do Windows: solicitacao enviada | " + WindowsSettingsUri);
                Hide();
            }
            catch (Exception ex)
            {
                Feedback("Não foi possível abrir as Configurações do Windows.", FeedbackKind.Error);
                WriteLog("Configuracoes do Windows: falha ao abrir | " + ex.Message);
            }
        }
        void OpenSteamLibrary()
        {
            bool? active = xboxMode();
            WriteLog("Biblioteca: modo Xbox=" + (active.HasValue ? active.Value.ToString() : "indisponivel"));
            if (active == true) NavigateSteamPage(SteamLibraryUri(active), "Biblioteca da Steam");
            else OpenSteam(SteamLibraryUri(active), "Biblioteca da Steam");
        }

        void OpenSteam(string uri, string destination)
        {
            try
            {
                steamLaunch(uri);
                WriteLog("Steam: solicitacao enviada | " + uri);
                // O shell aceitou a solicitacao; isso nao confirma a tela renderizada pela Steam.
                Hide();
            }
            catch (Exception ex)
            {
                Feedback("Não foi possível abrir " + destination + ". Confira se a Steam está instalada.", FeedbackKind.Error);
                WriteLog("Steam: falha ao abrir " + uri + " | " + ex.Message);
            }
        }

        void ConfigureSteamVerification()
        {
            ConfigureWindowFocusVerification();
            steamReturnContextLog = delegate { return delegate { return "OnFocusWindowChanged to window type: client, AppID 769\n"; }; };
            // Exercita os handlers reais sem abrir a Steam durante a verificacao automatica.
            steamLaunch = delegate(string uri) { lastSteamUri = uri; };
            settingsLaunch = delegate(string uri) { lastSettingsUri = uri; };
            xboxMode = delegate { return false; };
        }

#if XGB_VERIFY
        void VerifySteamActions()
        {
            VerifySteamFastNavigation();
            VerifySteamReturn();
            var oldAvailable = appAvailable; var oldSwitch = switchApp; var oldForeground = appForeground;
            var oldFocus = controllerFocus; var oldNow = feedbackNow; var oldOwns = appOwnsForeground;
            var steam = new XGBP.Desktop.WindowObservation { Window = new XGBP.Desktop.DesktopWindow {
                Handle = new IntPtr(200), ProcessId = 20, ProcessCreated = 2, ClassName = "steam", Title = "Steam — Modo Big Picture" } };
            bool inFront = false;
            findBigPicture = delegate { return steam; }; existingWindowAvailable = delegate { return true; };
            appAvailable = delegate { return true; }; appForeground = delegate { return inFront; };
            switchApp = delegate { inFront = true; }; controllerFocus = delegate { return true; };
            try
            {
            SetPanel(0); Show(); window.UpdateLayout();
            actions[0].Focus(); Activate();
            if (lastSteamUri != SteamStartPageUri || window.IsVisible) throw new Exception("Falha: inicio navega para Home, mesmo com Big Picture aberto.");
            Show(); SelectRail(1);
            if (lastSteamUri != "steam://open/games" || window.IsVisible) throw new Exception("Falha: biblioteca pelo trilho.");
            Show(); FocusHome("utility-library"); Activate();
            if (lastSteamUri != "steam://open/games" || window.IsVisible) throw new Exception("Falha: biblioteca pela Home.");
            Show(); FocusHome("utility-downloads"); Activate();
            if (lastSteamUri != SteamDownloadsUri || window.IsVisible) throw new Exception("Falha: downloads pela Home.");
            xboxMode = delegate { return true; };
            Show(); SelectRail(1);
            if (lastSteamUri != "steam://open/library/collection/all-games" || window.IsVisible)
                throw new Exception("Falha: grade completa da biblioteca pelo trilho no modo Xbox.");
            Show(); FocusHome("utility-library"); Activate();
            if (lastSteamUri != "steam://open/library/collection/all-games" || window.IsVisible)
                throw new Exception("Falha: grade completa da biblioteca pela Home no modo Xbox.");
            // Sem despacho aceito, preservar a interface para tentar de novo (inclusive no FSE).
            Show(); steamLaunch = delegate { throw new InvalidOperationException("falha de biblioteca simulada"); };
            SelectRail(1);
            if (!window.IsVisible || !((TextBlock)window.FindName("Feedback")).Text.Contains("Biblioteca da Steam"))
                throw new Exception("Falha: preservar previa quando a biblioteca nao pode abrir.");
            steamLaunch = delegate(string uri) { lastSteamUri = uri; };
            xboxMode = delegate { return null; };
            Show(); OpenSteamLibrary();
            if (lastSteamUri != "steam://open/games") throw new Exception("Falha: biblioteca com API indisponivel.");
            Show();
            steamLaunch = delegate { throw new InvalidOperationException("falha de despacho simulada"); };
            OpenSteamHome();
            if (!window.IsVisible || !((TextBlock)window.FindName("Feedback")).Text.StartsWith("Não foi possível"))
                throw new Exception("Falha: preservar previa quando o protocolo falha.");
            steamLaunch = delegate(string uri) { lastSteamUri = uri; };
            // Foco negado: o protocolo sozinho nao autoriza ocultar a interface.
            Show(); inFront = false; switchApp = delegate { }; OpenSteamHome();
            if (!window.IsVisible || switchingApp == null) throw new Exception("Falha: aguardar foreground da Steam.");
            inFront = true; CheckAppSwitch();
            if (window.IsVisible || switchingApp != null) throw new Exception("Falha: ocultar apos foreground confirmado.");
            switchApp = delegate { inFront = true; };
            // Nao entregar o foco antes da liberacao do ciclo, inclusive ao escolher outro destino.
            Show(); steamInputPending = true; lastSteamUri = null; OpenSteamHome();
            if (lastSteamUri != null || !window.IsVisible) throw new Exception("Falha: Home espera Steam Input.");
            xboxMode = delegate { return true; }; OpenSteamLibrary(); steamInputPending = false; TrySteamNavigation();
            if (lastSteamUri != SteamLibraryUri(true) || window.IsVisible) throw new Exception("Falha: ultimo destino vence a espera.");
            // Fechar, navegar, reabrir, sair do modo ou escolher outro app invalida o pedido.
            for (int cancel = 0; cancel < 5; cancel++)
            {
                Show(); steamInputPending = true; lastSteamUri = null; OpenSteamHome();
                if (cancel == 0) Hide();
                if (cancel == 1) SetPanel(5);
                if (cancel == 2) Show();
                if (cancel == 3) activationEpoch++;
                if (cancel == 4) { controllerFocus = delegate { return false; }; }
                steamInputPending = false; TrySteamNavigation(); controllerFocus = delegate { return true; };
                if (lastSteamUri != null || steamNavigationUri != null) throw new Exception("Falha: cancelar navegacao tardia " + cancel);
            }
            SetPanel(0); Show(); steamInputPending = true; lastSteamUri = null; OpenSteamHome();
            OpenApp(steam.Window); steamInputPending = false; TrySteamNavigation();
            if (lastSteamUri != null) throw new Exception("Falha: selecionar app cancela navegacao pendente.");
            // Inicio sem janela: abrir o modo uma vez e aguardar antes de pedir a pagina.
            Show(); findBigPicture = delegate { return null; }; OpenSteamHome();
            if (lastSteamUri != SteamHomeUri || !window.IsVisible) throw new Exception("Falha: abertura do modo aguarda janela.");
            lastSteamUri = null; TrySteamNavigation();
            if (lastSteamUri != null) throw new Exception("Falha: nao repetir abertura do Big Picture.");
            findBigPicture = delegate { return steam; }; TrySteamNavigation();
            if (lastSteamUri != SteamStartPageUri || window.IsVisible) throw new Exception("Falha: nova janela recebe destino Home.");
            Show(); findBigPicture = delegate { return null; }; OpenSteamHome();
            findBigPicture = delegate { return steam; }; controllerFocus = delegate { return false; };
            appOwnsForeground = delegate { return true; }; TrySteamNavigation();
            if (lastSteamUri != SteamStartPageUri || window.IsVisible) throw new Exception("Falha: nova Steam em foreground recebe Home.");
            controllerFocus = delegate { return true; }; appOwnsForeground = oldOwns;
            // ShellExecute bombeia mensagens: o timer nao deve reenviar o pedido em andamento.
            Show(); int dispatched = 0;
            steamLaunch = delegate(string uri) { lastSteamUri = uri; dispatched++; TrySteamNavigation(); };
            OpenSteamHome();
            if (dispatched != 1 || window.IsVisible) throw new Exception("Falha: despacho reentrante deve enviar uma vez.");
            // Um fechamento processado durante o despacho nao permite transferir foco depois.
            Show(); inFront = false; steamLaunch = delegate { Hide(); }; OpenSteamHome();
            if (inFront || window.IsVisible || switchingApp != null) throw new Exception("Falha: retorno tardio depois de fechar nao ativa Steam.");
            Show(); dispatched = 0; steamLaunch = delegate(string uri) { lastSteamUri = uri; dispatched++; };
            steamInputPending = true; OpenSteamHome();
            CompleteSteamInput(new SteamInputCycle());
            if (dispatched != 1 || window.IsVisible) throw new Exception("Falha: fim do Steam Input despacha sem esperar tick.");
            // Canal ocupado preserva a barra e o pedido, sem fallback concorrente.
            Show(); steamLaunch = delegate { throw new SteamNavigationBusyException(); }; OpenSteamHome();
            if (!window.IsVisible || steamNavigationUri == null) throw new Exception("Falha: aguardar canal ocupado sem ocultar.");
            steamLaunch = delegate(string uri) { lastSteamUri = uri; }; TrySteamNavigation();
            if (window.IsVisible || lastSteamUri != SteamStartPageUri) throw new Exception("Falha: canal liberado conclui o pedido.");
            Show(); findBigPicture = delegate { return null; }; OpenSteamHome();
            feedbackNow = delegate { return steamNavigationDeadline; }; TrySteamNavigation();
            if (!window.IsVisible || steamNavigationUri != null) throw new Exception("Falha: limite de abertura sem ocultar.");
            feedbackNow = oldNow;
            Show(); findBigPicture = delegate { throw new InvalidOperationException("enumeracao incerta"); }; lastSteamUri = null; OpenSteamHome();
            if (lastSteamUri != null || !window.IsVisible) throw new Exception("Falha: enumeracao incerta nao abre outro Big Picture.");
            }
            finally
            {
                steamInputPending = false; CancelSteamNavigation(); switchingApp = null; appSwitchTimer.Stop();
                appAvailable = oldAvailable; switchApp = oldSwitch; appForeground = oldForeground;
                controllerFocus = oldFocus; feedbackNow = oldNow; appOwnsForeground = oldOwns;
                ConfigureSteamVerification();
            }
        }
#endif

#if XGB_VERIFY
        void VerifySettingsActions()
        {
            SetPanel(5); Show(); window.UpdateLayout();
            actions.Find(c => (string)c.Tag == "windows-settings").Focus(); Activate();
            if (lastSettingsUri != WindowsSettingsUri || window.IsVisible) throw new Exception("Falha: configuracoes do Windows.");
            Show(); actions.Find(c => (string)c.Tag == "steam-settings").Focus(); Activate();
            if (lastSteamUri != SteamSettingsUri || window.IsVisible) throw new Exception("Falha: configuracoes da Steam.");
            Show(); settingsLaunch = delegate { throw new InvalidOperationException("falha de despacho simulada"); };
            actions.Find(c => (string)c.Tag == "windows-settings").Focus(); Activate();
            if (!window.IsVisible || !((TextBlock)window.FindName("Feedback")).Text.StartsWith("Não foi possível")) throw new Exception("Falha: erro nas configuracoes do Windows.");
            steamLaunch = delegate { throw new InvalidOperationException("falha de despacho simulada"); };
            actions.Find(c => (string)c.Tag == "steam-settings").Focus(); Activate();
            if (!window.IsVisible || !((TextBlock)window.FindName("Feedback")).Text.Contains("Configurações da Steam")) throw new Exception("Falha: erro nas configuracoes da Steam.");
            ConfigureSteamVerification();
        }
#endif
    }
}
