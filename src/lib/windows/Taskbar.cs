// Barra de tarefas do Windows pela API publica de appbar: le e muda so a ocultacao automatica,
// a mesma chave de Configuracoes > Personalizacao > Barra de tarefas. Sem injecao nem escrita no registro (ADR 0091).
using System;
using System.Runtime.InteropServices;

namespace XGBP.Desktop
{
    public static class Taskbar
    {
        const uint GetStateMessage = 4, SetStateMessage = 10, AutoHideFlag = 1, AlwaysOnTopFlag = 2;

        // null: sem barra de tarefas (Explorer reiniciando) ou leitura incerta.
        public static bool? AutoHide()
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) return null;
            var data = new AppBarData { Size = Marshal.SizeOf(typeof(AppBarData)), Window = tray };
            uint state = (uint)SHAppBarMessage(GetStateMessage, ref data).ToInt64();
            return (state & AutoHideFlag) != 0;
        }

        // Devolve se a releitura ja confirma o estado pedido; o chamador registra e volta a conferir na saida.
        public static bool SetAutoHide(bool on)
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) throw new InvalidOperationException("A barra de tarefas não está disponível agora.");
            var data = new AppBarData { Size = Marshal.SizeOf(typeof(AppBarData)), Window = tray };
            uint state = (uint)SHAppBarMessage(GetStateMessage, ref data).ToInt64();
            data.Param = new IntPtr((state & AlwaysOnTopFlag) | (on ? AutoHideFlag : 0));
            SHAppBarMessage(SetStateMessage, ref data);
            return AutoHide() == on;
        }

        // Area de trabalho do monitor principal, para o log mostrar o espaco reservado antes e depois.
        public static string WorkArea()
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            IntPtr monitor = MonitorFromPoint(new Point(), 1);
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return "indisponivel";
            return string.Format("trabalho {0},{1}-{2},{3} de {4}x{5}", info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom,
                info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
        }

        [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] struct AppBarData { public int Size; public IntPtr Window; public uint Callback, Edge; public Rect Bounds;
            public IntPtr Param; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string title);
        [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint message, ref AppBarData data);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point point, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
