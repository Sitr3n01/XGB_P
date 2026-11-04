// PS so no FSE; desktop confirmado admite apenas Win+G, mediante preferencia explicita. API incerta falha fechada.
using System;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Gaming;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        Func<bool?> activationRead = delegate { return null; };
        Func<Action, IDisposable> activationObserver;
        IDisposable activationSubscription;
        ModeWatcher activationWatcher;
        bool? activationMode;
        bool activationStarted;
        bool activationDesktopPreference, desktopShowByWinG;
        int activationEpoch;
        DateTime activationModeSince = DateTime.MinValue; // ultima mudanca de modo vista (relogio do controle)

        void StartActivation()
        {
            if (verify) { activationRead = delegate { return true; }; return; } // Fixture; sem teclado/modo reais.
            activationRead = GamingExperience.Active;
            gamingModeCanExit = GamingExperience.CanSet;
            gamingModeExit = GamingExperience.Exit;
            activationObserver = delegate(Action action) {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GameBarProbe", "logs", "probe.log");
                return new ProbeActivationBridge(path, action, WriteLog);
            };
            activationWatcher = new ModeWatcher();
            activationWatcher.Log += delegate(string text) { WriteLog("modo Xbox: " + text); };
            activationWatcher.StateChanged += delegate(bool? active, string origin) { Dispatch(delegate { ActivationAllowed(); }); };
            activationWatcher.Start();
            ActivationAllowed();
        }
        bool ActivationAllowed()
        {
            if (systemPowerBusy) return false;
            bool? current;
            try { current = activationRead(); } catch (Exception) { current = null; }
            bool changedMode = activationStarted && current != activationMode;
            bool allowed = current == true || current == false && launchPreferences.DesktopWinG;
            if (!activationStarted || changedMode || activationDesktopPreference != launchPreferences.DesktopWinG)
            {
                if (!activationStarted || changedMode) activationModeSince = controllerNow();
                activationStarted = true; activationMode = current; activationDesktopPreference = launchPreferences.DesktopWinG;
                activationEpoch++; ClearHeld(); ClearGuide(); StopActivationObserver();
                if (changedMode || !allowed) { if (window.IsVisible) Hide(); }
                if (changedMode) KeyboardModeChanged();
                if (allowed)
                {
                    if (activationObserver != null && activationSubscription == null)
                    {
                        int epoch = activationEpoch;
                        try
                        {
                            activationSubscription = activationObserver(delegate {
                                DateTime queued = DateTime.UtcNow;
                                Dispatch(delegate { if (epoch == activationEpoch && (DateTime.UtcNow - queued).TotalSeconds <= 5) RequestActivation("Win+G"); });
                            });
                        }
                        catch (Exception ex) { WriteLog("ativacao: ponte Win+G indisponivel | " + ex.Message); }
                    }
                    WriteLog(current == true ? "ativacao: FSE ativo; PS e Win+G autorizados" : "ativacao: desktop confirmado; somente Win+G autorizado");
                }
                else
                {
                    WriteLog("ativacao: " + (current == false ? "desktop" : "API indisponivel") + "; barra inativa; ponte liberada");
                }
            }
            return allowed;
        }
        bool GuideActivationAllowed() { return ActivationAllowed() && activationMode == true; }
        void RequestActivation(string origin)
        {
            if (!ActivationAllowed()) return;
            if (activationMode != true && origin != "Win+G") return;
            desktopShowByWinG = origin == "Win+G";
            try { Toggle(); } finally { desktopShowByWinG = false; }
            WriteLog("ativacao: " + origin + " | " + (window.IsVisible && !motionClosing ? "abriu" : "ocultou"));
        }
        void StopActivationObserver()
        {
            var keyboard = activationSubscription;
            if (keyboard == null) return;
            try { keyboard.Dispose(); activationSubscription = null; }
            catch (Exception ex) { WriteLog("ativacao: falha ao liberar observador | " + ex.Message); }
        }
        void StopActivation()
        {
            activationEpoch++;
            if (activationWatcher != null) { activationWatcher.Dispose(); activationWatcher = null; }
            StopActivationObserver();
        }
#if XGB_VERIFY
        public void VerifyActivationLive()
        {
            bool? current = GamingExperience.Active();
            var preference = LaunchSettings.Load();
            string report = "API FSE: suportado=" + GamingExperience.Supported() + "; ativo=" + current +
                "; podeAlternar=" + GamingExperience.CanSet() + ".\r\nPolitica permite PS=" + (current == true) +
                "; Win+G=" + (current == true || current == false && preference.DesktopWinG) + "; preferencia desktop=" + preference.DesktopWinG +
                ". Diagnostico somente leitura; sem observador de teclado, sem alternar modo, sem abrir a barra.\r\n";
            File.WriteAllText(Path.Combine(Output, "ativacao-real.txt"), report, Encoding.UTF8);
        }
#endif
    }
}
