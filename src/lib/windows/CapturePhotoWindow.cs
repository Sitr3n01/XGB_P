// Apresentacao limitada a uma foto aberta pelo usuario no Fotos, somente no FSE confirmado.
using System;
using System.IO;
using System.Runtime.InteropServices;
using XGBP.Gaming;

namespace XGBP.Desktop
{
    public static class CapturePhotoPolicy
    {
        public static bool IsPhoto(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return Array.IndexOf(new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" }, Path.GetExtension(path).ToLowerInvariant()) >= 0; }
            catch (ArgumentException) { return false; }
        }
        public static bool IsPhotos(DesktopWindow window)
        {
            if (window == null || string.IsNullOrEmpty(window.ExecutablePath) || window.AppUserModelId == null ||
                !window.AppUserModelId.StartsWith("Microsoft.Windows.Photos_8wekyb3d8bbwe!", StringComparison.OrdinalIgnoreCase)) return false;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps", "Microsoft.Windows.Photos_");
            string executable = Path.GetFileName(window.ExecutablePath);
            return window.ExecutablePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(executable, "Photos.exe", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(executable, "Microsoft.Photos.exe", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(executable, "PhotosApp.exe", StringComparison.OrdinalIgnoreCase));
        }
        public static bool Matches(WindowObservation view, string file)
        {
            return IsPhoto(file) && view != null && view.Window != null && view.Visible && !view.Window.Minimized && IsPhotos(view.Window) &&
                view.Window.Handle != IntPtr.Zero && view.Window.ProcessId != 0 && view.Window.ProcessCreated > 0 &&
                string.Equals(view.Window.Title, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        }
        public static bool Same(DesktopWindow first, DesktopWindow second)
        {
            return first != null && second != null && first.Handle == second.Handle && first.ProcessId == second.ProcessId &&
                first.ProcessCreated > 0 && first.ProcessCreated == second.ProcessCreated;
        }
    }

    public static partial class DesktopWindows
    {
        public static WindowObservation CapturePhotoForeground()
        {
            IntPtr old = CapturePhotoDpi(new IntPtr(-4)); // coordenadas fisicas, sem mudar a configuracao de DPI.
            try
            {
                var result = Observe(GetForegroundWindow());
                if (result != null) result.Window.AppUserModelId = WindowAppId(result.Window.Handle, result.Window.ProcessId);
                return result;
            }
            finally { if (old != IntPtr.Zero) CapturePhotoDpi(old); }
        }
        // Sem alteracao de estilos, topmost ou foco. Revalidar modo, arquivo, janela, PID e criacao antes de cada pedido.
        public static bool PresentCapturePhoto(DesktopWindow expected, string file, bool fitMonitor)
        {
            if (GamingExperience.Active() != true) return false;
            IntPtr old = CapturePhotoDpi(new IntPtr(-4));
            if (old == IntPtr.Zero) return false;
            try
            {
                var current = CapturePhotoForeground();
                if (!CapturePhotoPolicy.Matches(current, file) || !CapturePhotoPolicy.Same(expected, current.Window) ||
                    !Belongs(expected) || GamingExperience.Active() != true || GetForegroundWindow() != expected.Handle) return false;
                if (current.CoversMonitor) return true;
                if (!fitMonitor) return ShowWindowAsync(expected.Handle, 3); // SW_SHOWMAXIMIZED.
                if (!current.HasMonitor || current.MonitorWidth <= 0 || current.MonitorHeight <= 0) return false;
                // O FSE pode ignorar maximizar. Uma unica tentativa de ocupar o monitor, sem ativar nem subir a janela.
                return CapturePhotoPosition(expected.Handle, IntPtr.Zero, current.MonitorLeft, current.MonitorTop,
                    current.MonitorWidth, current.MonitorHeight, 0x4014); // ASYNCWINDOWPOS | NOZORDER | NOACTIVATE.
            }
            finally { CapturePhotoDpi(old); }
        }
        [DllImport("user32.dll", EntryPoint = "SetThreadDpiAwarenessContext")] static extern IntPtr CapturePhotoDpi(IntPtr context);
        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        static extern bool CapturePhotoPosition(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
