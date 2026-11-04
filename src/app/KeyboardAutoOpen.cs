// Abertura automatica do teclado virtual no modo Xbox (ADR 0052, revisada pela ADR 0087): o foco UIA num campo editavel
// abre o teclado so com o FSE lido na hora e depois da transicao de entrada no modo.
using System;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int KbModeSettleMs = 3000;

        void OnTextFocus(TextFocusInfo info)
        {
            if (disposed || info == null) return;
            string described = info.Describe();
            if (described != kbLastFocusLog) { kbLastFocusLog = described; WriteLog("teclado: foco | " + described); }
            kbFocus = info;
            if (info.Key != null && info.Key != kbDismissedKey) kbDismissedKey = null;
            // Entrando no modo Xbox, o app do desktop continua na frente ate a Steam assumir (logs: 1,8-12 s; ADR 0087).
            // O campo que ja tinha o foco ali conta como fechado ate o foco ir para outro elemento.
            if (KbModeSettling() && info.Editable && info.Key != null && info.Key != kbDismissedKey)
            {
                kbDismissedKey = info.Key;
                WriteLog("teclado: campo ignorado | entrada no modo Xbox | hwnd=" + info.Window.ToInt64());
            }
            if (kbVisible)
            {
                if (info.Window == kbTarget)
                {
                    kbPassword = info.Password; UpdateKeyboardStatus();
                    if (info.Editable || kbMode != "auto") kbAutoHideAt = DateTime.MaxValue; else KbScheduleAutoHide();
                }
                else if (kbMode == "auto")
                {
                    if (KeyboardAutoAllowed(info)) ShowKeyboard("auto", info.Window, "campo em outro app");
                    else if (!KbOwnWindow(info.Window)) KbScheduleAutoHide();
                }
                return;
            }
            if (KeyboardAutoAllowed(info)) ShowKeyboard("auto", info.Window, "campo de texto");
        }
        bool KeyboardAutoAllowed(TextFocusInfo info)
        {
            if (!kbPrefs.AutoOpen || info.Window == IntPtr.Zero || !info.Editable || info.Excluded) return false;
            if (!KbXboxModeConfirmed() || KbModeSettling()) return false;
            if (window.IsVisible || motionClosing || kbSuspended || kbPending != IntPtr.Zero) return false;
            if (info.Key != null && info.Key == kbDismissedKey) return false;
            return kbForeground() == info.Window;
        }
        // Leitura nova da API a cada decisao. Desktop, API incerta ou energia em andamento nao abrem (falha fechada).
        bool KbXboxModeConfirmed() { return ActivationAllowed() && activationMode == true; }
        bool KbModeSettling() { return activationMode == true && controllerNow() < activationModeSince.AddMilliseconds(KbModeSettleMs); }
        void KbScheduleAutoHide() { if (kbAutoHideAt == DateTime.MaxValue) kbAutoHideAt = controllerNow().AddMilliseconds(KbAutoHideMs); }
    }
}
