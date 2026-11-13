// Entrada da candidata e do host de verificacao; ciclo de vida separado da janela WPF.
using System;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool startup = Array.IndexOf(args, "--startup") >= 0;
#if XGB_VERIFY
            bool liveAudio = Array.IndexOf(args, "--verify-audio-live") >= 0;
            bool liveApps = Array.IndexOf(args, "--verify-windows-live") >= 0;
            bool liveMedia = Array.IndexOf(args, "--verify-media-live") >= 0;
            bool livePerf = Array.IndexOf(args, "--verify-perf-live") >= 0;
            bool liveStatus = Array.IndexOf(args, "--verify-status-live") >= 0;
            bool liveGames = Array.IndexOf(args, "--verify-games-live") >= 0;
            bool liveControl = Array.IndexOf(args, "--verify-games-control-live") >= 0;
            bool liveClose = Array.IndexOf(args, "--verify-apps-close-live") >= 0;
            bool liveConnections = Array.IndexOf(args, "--verify-connections-live") >= 0;
            bool liveStores = Array.IndexOf(args, "--verify-stores-live") >= 0;
            bool liveSounds = Array.IndexOf(args, "--verify-sounds-live") >= 0;
            bool liveLaunch = Array.IndexOf(args, "--verify-launch-live") >= 0;
            bool liveActivation = Array.IndexOf(args, "--verify-activation-live") >= 0;
            bool liveKeyboard = Array.IndexOf(args, "--verify-keyboard-live") >= 0;
            bool liveAccent = Array.IndexOf(args, "--verify-accent-live") >= 0;
            bool livePrograms = Array.IndexOf(args, "--verify-programs-live") >= 0;
            bool liveWake = Array.IndexOf(args, "--verify-wake-live") >= 0;
            bool liveTv = Array.IndexOf(args, "--verify-tv-live") >= 0;
            bool coreTv = Array.IndexOf(args, "--verify-tv-core") >= 0;
            bool integrationTv = Array.IndexOf(args, "--verify-tv-integration") >= 0;
            bool releaseCheck = Array.IndexOf(args, "--verify-release") >= 0;
            bool observerCheck = Array.IndexOf(args, "--verify-observer-live") >= 0;
            bool performanceCheck = Array.IndexOf(args, "--verify-performance") >= 0;
            // Fora da bateria: despacha os protocolos reais do Steam Input (ADR 0041).
            bool liveSteamInput = Array.IndexOf(args, "--verify-steam-input-live") >= 0;
            bool checkMotion = Array.IndexOf(args, "--verify-motion") >= 0;
            bool titleCheck = Array.IndexOf(args, "--verify-sliding-title") >= 0;
            bool inspectMotion = Array.IndexOf(args, "--inspect-motion") >= 0;
            bool verify = titleCheck || coreTv || integrationTv || livePrograms || liveWake || liveTv || performanceCheck || observerCheck || releaseCheck || inspectMotion || checkMotion || Array.IndexOf(args, "--verify") >= 0 || liveAudio || liveApps || liveMedia || livePerf || liveStatus || liveGames || liveControl || liveClose || liveConnections || liveStores || liveSounds || liveLaunch || liveActivation || liveSteamInput || liveKeyboard || liveAccent;
            if (!verify && Array.Exists(args, a => a.StartsWith("--verify", StringComparison.Ordinal))) return 2;
#else
            // Build de produto: os modos de verificacao existem somente em XGB_P.Tests.exe.
            bool verify = false, inspectMotion = false;
            if (Array.Exists(args, a => a.StartsWith("--verify", StringComparison.Ordinal) || a == "--inspect-motion")) return 2;
#endif
            bool created;
            using (var instance = new Mutex(true, verify ? "Local\\XGBPDesignVerify" : "Local\\XGBPDesignPreview", out created))
            {
                if (!created) {
                    if (verify) { Preview.WriteLog("verificacao bloqueada: outra instancia possui o mutex"); Preview.FlushLog(); return 3; }
                    if (!startup) Forms.MessageBox.Show("O XGB_P já está rodando. Use PS no modo Xbox ou Win+G conforme a opção da bandeja.", "XGB_P");
                    return 0;
                }
                Preview preview = null;
#if XGB_VERIFY
                VerificationReceipt receipt = null;
                bool verificationSucceeded = false;
#endif
                try
                {
#if XGB_VERIFY
                    if (verify) receipt = new VerificationReceipt(args);
#endif
                    if (!verify)
                    {
                        if (startup && !LaunchSettings.StartupAllowed(XGBP.Desktop.StartupRegistration.Read(LaunchSettings.Exe)))
                        { Preview.WriteLog("abertura: inicializacao desativada ou nao registrada; saindo"); return 0; }
                        if (LaunchSettings.NeedsElevation(LaunchSettings.Load().AlwaysAdmin, LaunchSettings.Elevated))
                        {
                            if (Array.IndexOf(args, "--elevated") >= 0) throw new InvalidOperationException("O Windows não concedeu os privilégios de administrador solicitados.");
                            instance.ReleaseMutex(); // a copia elevada assume a instancia
                            return LaunchSettings.RequestElevation(startup);
                        }
                    }
                    var app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    preview = new Preview(app, verify, startup);
                    preview.InspectionWindow = inspectMotion;
                    preview.Start();
#if XGB_VERIFY
                    if (verify && !inspectMotion)
                    {
                        if (performanceCheck) preview.VerifyPerformance();
                        else if (titleCheck) preview.VerifySlidingTitle();
                        else if (observerCheck) preview.VerifyObserverLive();
                        else if (releaseCheck) preview.VerifyReleaseReadiness();
                        else if (liveAccent) preview.VerifyAccentLive();
                        else if (livePrograms) preview.VerifyProgramsLive();
                        else if (liveWake) preview.VerifyWakeLive();
                        else if (liveTv) preview.VerifyTvLive();
                        else if (coreTv) preview.VerifyTvCore();
                        else if (integrationTv) preview.VerifyTvIntegration();
                        else if (checkMotion) { preview.Verify(); preview.VerifyMotion(); preview.VerifySlidingTitle(); }
                        else if (liveAudio) preview.VerifyAudioLive(); else if (liveApps) preview.VerifyOpenAppsLive(); else if (liveMedia) preview.VerifyMediaLive();
                        else if (livePerf) preview.VerifyPerfLive(); else if (liveStatus) preview.VerifyStatusLive(); else if (liveGames) preview.VerifyGamesLive(); else if (liveControl) preview.VerifyGameControlLive(); else if (liveClose) preview.VerifyAppCloseLive(); else if (liveConnections) preview.VerifyConnectionsLive(); else if (liveStores) preview.VerifyStoresLive(); else if (liveSounds) preview.VerifySoundsLive(); else if (liveLaunch) preview.VerifyLaunchLive(); else if (liveActivation) preview.VerifyActivationLive(); else if (liveSteamInput) preview.VerifySteamInputLive(); else if (liveKeyboard) preview.VerifyKeyboardLive(); else preview.Verify();
                        verificationSucceeded = true;
                        return 0;
                    }
                    if (inspectMotion) preview.InspectMotion();
#endif
                    app.Run();
                    return 0;
                }
                catch (Exception ex)
                {
                    Preview.WriteLog("ERRO: " + ex);
                    // Retomar os jogos antes do aviso: a mensagem espera o usuario.
                    if (preview != null) preview.Dispose();
                    if (!verify) Forms.MessageBox.Show(ex.Message, "XGB_P · erro");
                    return 1;
                }
                // Qualquer saida, inclusive por erro, retoma os jogos pausados pela previa e libera atalho e bandeja.
                finally {
                    if (preview != null) preview.Dispose();
#if XGB_VERIFY
                    bool flushed = Preview.FlushLog();
                    if (receipt != null) receipt.Complete(verificationSucceeded && flushed);
#else
                    Preview.FlushLog();
#endif
                }
            }
        }
    }
}
