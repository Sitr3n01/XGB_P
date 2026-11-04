// Ciclo de vida do observador e da agenda do teclado; separado da construcao visual.
using System;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void EnsureKeyboardWatcher()
        {
            RefreshPresentationTimers();
            if (verify || disposed || kbWatcherFactory == null) return;
            bool want = kbVisible || kbSuspended || kbPending != IntPtr.Zero || kbPrefs.AutoOpen && activationMode == true;
            if (want && kbWatcher == null)
            {
                try { kbWatcher = kbWatcherFactory(); WriteLog("teclado: detector de campos ligado"); }
                catch (Exception ex) { WriteLog("teclado: detector indisponivel | " + ex.Message); kbNextWatcherCheck = controllerNow().AddSeconds(30); }
            }
            else if (!want && kbWatcher != null)
            {
                kbWatcher.Dispose(); kbWatcher = null; kbFocus = null; kbLastFocusLog = null;
                WriteLog("teclado: detector de campos desligado");
            }
        }
    }
}
