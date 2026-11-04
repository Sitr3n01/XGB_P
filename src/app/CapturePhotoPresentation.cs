// Apresentacao de fotos no FSE por escolha explicita. Nao acompanha apps ou janelas fora desta operacao.
using System;
using System.Diagnostics;
using System.Windows.Threading;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed class CapturePhotoRequest
    {
        internal string File;
        internal DesktopWindow Origin, Target;
        internal DateTime Started, Applied;
        internal int Epoch, Attempts;
        internal FeedbackNotice Notice;
    }
    sealed partial class Preview
    {
        readonly DispatcherTimer capturePhotoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        Func<WindowObservation> capturePhotoRead = DesktopWindows.CapturePhotoForeground;
        Func<DesktopWindow, string, bool, bool> capturePhotoApply = DesktopWindows.PresentCapturePhoto;
        Func<DateTime> capturePhotoNow = delegate { return DateTime.UtcNow; };
        CapturePhotoRequest capturePhotoPending;

        static ProcessStartInfo AmdOpenInfo(string target, bool maximize)
        { return new ProcessStartInfo(target) { UseShellExecute = true, WindowStyle = maximize ? ProcessWindowStyle.Maximized : ProcessWindowStyle.Normal }; }
        void StartCapturePhotoPresentation()
        {
            capturePhotoTimer.Tick += delegate { CheckCapturePhoto(); };
            if (verify) { capturePhotoRead = delegate { return null; }; capturePhotoApply = delegate { return false; }; }
        }
        void BeginCapturePhoto(string file, DesktopWindow origin)
        {
            capturePhotoPending = new CapturePhotoRequest { File = file, Origin = origin, Started = capturePhotoNow(), Epoch = activationEpoch,
                Notice = BeginFeedback("Abrindo a foto em tela cheia…", AmdScope, 6) };
            if (!verify) capturePhotoTimer.Start();
        }
        void EndCapturePhoto(string text, FeedbackKind kind)
        {
            var pending = capturePhotoPending; capturePhotoPending = null; capturePhotoTimer.Stop();
            if (pending == null) return;
            FinishFeedback(pending.Notice, text, kind);
            WriteLog("foto: apresentacao | " + text);
        }
        void CancelCapturePhoto()
        { EndCapturePhoto("Ajuste de exibição da foto cancelado.", FeedbackKind.Info); }
        void CheckCapturePhoto()
        {
            var pending = capturePhotoPending;
            if (pending == null) { capturePhotoTimer.Stop(); return; }
            if (disposed || pending.Epoch != activationEpoch || ReadGamingMode(activationRead) != true) { CancelCapturePhoto(); return; }
            var now = capturePhotoNow();
            if (now - pending.Started >= TimeSpan.FromSeconds(4))
            { EndCapturePhoto("A foto foi aberta, mas o Windows não confirmou a exibição em tela cheia.", FeedbackKind.Warning); return; }
            if (window.IsVisible) return; // ocultacao pode aguardar a liberacao da Steam.
            try
            {
                var current = capturePhotoRead();
                if (current == null) return;
                if (!CapturePhotoPolicy.Matches(current, pending.File))
                {
                    bool waiting = pending.Target == null && (CapturePhotoPolicy.IsPhotos(current.Window) ||
                        current.Window.Handle == handle || CapturePhotoPolicy.Same(current.Window, pending.Origin) ||
                        current.Window.ClassName == "Win32/GamingFallbackWindow");
                    if (!waiting && (pending.Target != null || now - pending.Started >= TimeSpan.FromMilliseconds(300))) CancelCapturePhoto();
                    return;
                }
                if (pending.Target != null && !CapturePhotoPolicy.Same(pending.Target, current.Window)) { CancelCapturePhoto(); return; }
                pending.Target = current.Window;
                if (current.CoversMonitor) { EndCapturePhoto("Foto aberta em tela cheia.", FeedbackKind.Success); return; }
                if (pending.Attempts > 0 && now - pending.Applied < TimeSpan.FromMilliseconds(750)) return;
                if (pending.Attempts >= 2) return;
                // Uma maximizacao, seguida no maximo de um ajuste ao monitor se o shell a ignorar.
                bool fit = pending.Attempts == 1;
                bool requested = capturePhotoApply(pending.Target, pending.File, fit);
                pending.Attempts++; pending.Applied = now;
                WriteLog("foto: pedido de apresentacao | " + (fit ? "monitor" : "maximizar") + "; aceito=" + requested + "; " + current.Describe());
            }
            catch (Exception ex)
            { WriteLog("foto: apresentacao falhou | " + ex.Message); EndCapturePhoto("Não foi possível ajustar a exibição da foto. Use a opção de tela cheia do Fotos.", FeedbackKind.Warning); }
        }
    }
}
