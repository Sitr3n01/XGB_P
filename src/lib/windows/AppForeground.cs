// Transferencia por escolha explicita do usuario. Sem Alt/teclado, sem desbloquear/camuflar janelas do shell.
using System;
using System.Runtime.InteropServices;

namespace XGBP.Desktop
{
    public static partial class DesktopWindows
    {
        static string RequestAppForeground(IntPtr target)
        {
            uint pid;
            uint destinationThread = GetWindowThreadProcessId(target, out pid), ownThread = GetCurrentThreadId();
            return TransferAppForeground(target, GetForegroundWindow,
                delegate { return SetForegroundWindow(target); },
                delegate {
                    if (destinationThread == 0 || destinationThread == ownThread) return "fila indisponivel";
                    return AttachThreadInput(ownThread, destinationThread, true) ? null : "fila recusada (erro " + Marshal.GetLastWin32Error() + ")";
                },
                delegate { AttachThreadInput(ownThread, destinationThread, false); },
                delegate { BringWindowToTop(target); });
        }

        // Dependencias locais: permite reproduzir recusa e resposta assincrona sem tocar apps reais ou regras de foco.
        internal static string TransferAppForeground(IntPtr target, Func<IntPtr> foreground, Func<bool> request,
            Func<string> connect, Action disconnect, Action raise)
        {
            IntPtr before = foreground();
            bool direct = request();
            if (foreground() == target) return "direto=" + direct + "; foregroundAntes=" + before + "; foreground=" + foreground();
            string refused = connect();
            if (refused != null) return "direto=" + direct + "; " + refused + "; foregroundAntes=" + before + "; foreground=" + foreground();
            bool attached;
            try { raise(); attached = request(); }
            finally { disconnect(); }
            return "direto=" + direct + "; comFila=" + attached + "; foregroundAntes=" + before + "; foreground=" + foreground();
        }
    }
}
