// Depois de devolver a frente a um app: conferir se ele tambem recebeu o foco de entrada (ADR 0115).
using System;
using System.Windows.Threading;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // A ativacao do destino e assincrona: ler antes disso acusaria uma falta que nao existe.
        readonly DispatcherTimer inputFocusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        Func<DesktopWindow, bool?> inputFocusRead = DesktopWindows.HasInputFocus;
        Func<DesktopWindow, string> inputFocusRestore = DesktopWindows.RestoreInputFocus;
        DesktopWindow inputFocusTarget;
        string inputFocusReason;

        void StartInputFocus() { inputFocusTimer.Tick += delegate { CheckInputFocus(); }; }
        void WatchInputFocus(DesktopWindow target, string reason)
        {
            inputFocusTarget = target; inputFocusReason = reason;
            inputFocusTimer.Stop();
            if (target != null && !verify) inputFocusTimer.Start();
        }
        // Uma conferencia por entrega. Barra reaberta ou outra janela na frente cancelam: a escolha nova e respeitada.
        void CheckInputFocus()
        {
            inputFocusTimer.Stop();
            var target = inputFocusTarget; inputFocusTarget = null;
            if (target == null || disposed || window.IsVisible || foregroundRead() != target.Handle) return;
            try
            {
                bool? ready = inputFocusRead(target);
                string result = ready == null ? "indisponivel" : ready.Value ? "com foco de entrada" :
                    "sem foco de entrada; " + inputFocusRestore(target) + "; depois=" + inputFocusRead(target);
                WriteLog("foco: entrada do app | " + inputFocusReason + " | hwnd=" + target.Handle.ToInt64() + " | " + result);
            }
            catch (Exception ex) { WriteLog("foco: entrada do app nao conferida | " + ex.Message); }
        }
    }
}
