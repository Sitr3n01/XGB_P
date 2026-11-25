// Transferencia por escolha explicita do usuario. Sem Alt/teclado, sem desbloquear/camuflar janelas do shell.
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace XGBP.Desktop
{
    public static partial class DesktopWindows
    {
        // O pedido aceito leva cerca de 15 ms para aparecer em GetForegroundWindow. Ligar a fila com ele pendente deixa o
        // destino na frente, mas sem janela ativa nem foco: o usuario precisava clicar no jogo (ADR 0115).
        const int DeliveryMs = 150, DeliveryStepMs = 5;
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
                delegate { BringWindowToTop(target); },
                delegate { Thread.Sleep(DeliveryStepMs); });
        }

        // Dependencias locais: permite reproduzir recusa e resposta assincrona sem tocar apps reais ou regras de foco.
        internal static string TransferAppForeground(IntPtr target, Func<IntPtr> foreground, Func<bool> request,
            Func<string> connect, Action disconnect, Action raise, Action wait)
        {
            IntPtr before = foreground();
            bool direct = request();
            int waited = 0;
            // So o pedido aceito merece espera; o recusado segue para a fila, sem ativacao pendente.
            if (direct) while (foreground() != target && waited < DeliveryMs) { wait(); waited += DeliveryStepMs; }
            string prefix = "direto=" + direct + (waited > 0 ? "; espera=" + waited + " ms" : "");
            if (foreground() == target) return prefix + "; foregroundAntes=" + before + "; foreground=" + foreground();
            string refused = connect();
            if (refused != null) return prefix + "; " + refused + "; foregroundAntes=" + before + "; foreground=" + foreground();
            bool attached;
            try { raise(); attached = request(); }
            finally { disconnect(); }
            return prefix + "; comFila=" + attached + "; foregroundAntes=" + before + "; foreground=" + foreground();
        }

        // Primeiro plano nao basta: o teclado vai para a janela ativa da thread do destino. null quando nao pode ser lido.
        public static bool? HasInputFocus(DesktopWindow item)
        {
            if (Exists(item) != true) return null;
            uint pid, thread = GetWindowThreadProcessId(item.Handle, out pid);
            var info = new GuiThreadInfo { Size = Marshal.SizeOf(typeof(GuiThreadInfo)) };
            if (thread == 0 || !GetGUIThreadInfo(thread, ref info)) return null;
            return info.Active != IntPtr.Zero;
        }
        // So para o destino ja em primeiro plano e sem janela ativa: devolve o foco de entrada, sem trocar de janela.
        public static string RestoreInputFocus(DesktopWindow item)
        {
            if (Exists(item) != true || GetForegroundWindow() != item.Handle) return "destino mudou";
            uint pid, target = GetWindowThreadProcessId(item.Handle, out pid), current = GetCurrentThreadId();
            if (target == 0 || target == current) return "fila indisponivel";
            if (!AttachThreadInput(current, target, true)) return "fila recusada (erro " + Marshal.GetLastWin32Error() + ")";
            try { SetFocus(item.Handle); }
            finally { AttachThreadInput(current, target, false); }
            return "pedido enviado";
        }

        [StructLayout(LayoutKind.Sequential)] struct GuiThreadInfo
        { public int Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public WindowRect CaretRect; }
        [DllImport("user32.dll", SetLastError = true)] static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
        [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr hwnd);
    }
}
