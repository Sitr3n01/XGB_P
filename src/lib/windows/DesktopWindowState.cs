// Observacao de geometria e foco, sem escrever posicao, tamanho, DPI ou resolucao.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace XGBP.Desktop
{
    public sealed class WindowObservation
    {
        public DesktopWindow Window;
        public int Left, Top, Width, Height, MonitorLeft, MonitorTop, MonitorWidth, MonitorHeight;
        public int NormalLeft, NormalTop, NormalWidth, NormalHeight, ShowCommand;
        public uint Dpi, Cloaked;
        public bool Maximized, Visible, HasPlacement, HasMonitor, HasCloak;
        // Aproximacao geometrica no contexto DPI do observador; nao comprova fullscreen exclusivo.
        public bool CoversMonitor
        {
            get { return HasMonitor && !Window.Minimized && Math.Abs((long)Left - MonitorLeft) <= 8 &&
                Math.Abs((long)Top - MonitorTop) <= 8 && Math.Abs((long)Width - MonitorWidth) <= 16 &&
                Math.Abs((long)Height - MonitorHeight) <= 16; }
        }
        public string Describe()
        {
            return "hwnd=" + Window.Handle.ToInt64() + "; pid=" + Window.ProcessId + "; criado=" + Window.ProcessCreated +
                "; titulo=" + (Window.Title ?? "").Replace('\r', ' ').Replace('\n', ' ') +
                "; ret=" + Left + "," + Top + " " + Width + "x" + Height +
                "; normal=" + (HasPlacement ? NormalLeft + "," + NormalTop + " " + NormalWidth + "x" + NormalHeight + "; show=" + ShowCommand : "indisponivel") +
                "; monitor=" + (HasMonitor ? MonitorLeft + "," + MonitorTop + " " + MonitorWidth + "x" + MonitorHeight : "indisponivel") +
                "; minimizada=" + Window.Minimized + "; maximizada=" + Maximized + "; visivel=" + Visible +
                "; camuflada=" + (HasCloak ? CloakName(Cloaked) : "indisponivel") +
                "; ocupaMonitor=" + CoversMonitor + "; dpiJanela=" + Dpi + "; coordenadas=GetWindowRect/contexto-do-observador";
        }
        // DWMWA_CLOAKED: visivel para o Windows, mas fora da composicao. No modo Xbox, o shell esconde assim a janela de tras.
        public static string CloakName(uint value)
        {
            if (value == 0) return "nao";
            if (value == 1) return "app";
            if (value == 2) return "shell";
            if (value == 4) return "herdada";
            return "0x" + value.ToString("X");
        }
    }

    public static partial class DesktopWindows
    {
        public static WindowObservation Observe(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return null;
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            WindowRect rect; if (pid == 0 || !GetWindowRect(hwnd, out rect)) return null;
            var title = new StringBuilder(2048); GetWindowText(hwnd, title, title.Capacity);
            var identity = new DesktopWindow { Handle = hwnd, ProcessId = pid, ProcessCreated = ProcessCreation(pid),
                ClassName = WindowClass(hwnd), ExecutablePath = ProcessPath(pid), Title = title.ToString(), Minimized = IsIconic(hwnd) };
            identity.AppName = identity.Title;
            var result = new WindowObservation { Window = identity, Left = rect.Left, Top = rect.Top,
                Width = rect.Right - rect.Left, Height = rect.Bottom - rect.Top, Visible = IsWindowVisible(hwnd),
                Maximized = IsZoomed(hwnd), Dpi = GetDpiForWindow(hwnd) };
            uint cloaked;
            result.HasCloak = DwmGetWindowAttribute(hwnd, 14, out cloaked, 4) == 0;
            if (result.HasCloak) result.Cloaked = cloaked;
            var placement = new WindowPlacement { Length = Marshal.SizeOf(typeof(WindowPlacement)) };
            result.HasPlacement = GetWindowPlacement(hwnd, ref placement);
            if (result.HasPlacement)
            {
                result.ShowCommand = placement.ShowCommand; result.NormalLeft = placement.Normal.Left; result.NormalTop = placement.Normal.Top;
                result.NormalWidth = placement.Normal.Right - placement.Normal.Left; result.NormalHeight = placement.Normal.Bottom - placement.Normal.Top;
            }
            var monitor = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            result.HasMonitor = GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor);
            if (result.HasMonitor)
            {
                result.MonitorLeft = monitor.Monitor.Left; result.MonitorTop = monitor.Monitor.Top;
                result.MonitorWidth = monitor.Monitor.Right - monitor.Monitor.Left; result.MonitorHeight = monitor.Monitor.Bottom - monitor.Monitor.Top;
            }
            // Nao aceitar identidade reciclada entre a primeira e a ultima consulta.
            return Belongs(identity) ? result : null;
        }

        public static bool IsSteamBigPicture(DesktopWindow item, string steamRoot)
        {
            if (item == null || string.IsNullOrWhiteSpace(steamRoot) || string.IsNullOrEmpty(item.Title) ||
                item.Title.IndexOf("Big Picture", StringComparison.OrdinalIgnoreCase) < 0 ||
                !item.Title.StartsWith("Steam", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(item.ExecutablePath)) return false;
            string root = Path.GetFullPath(steamRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(item.ExecutablePath);
            if (string.Equals(path, Path.Combine(root, "steam.exe"), StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(Path.GetFileName(path), "steamwebhelper.exe", StringComparison.OrdinalIgnoreCase) &&
                path.StartsWith(root + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "cef" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public static WindowObservation FindSteamBigPicture()
        { using (var query = new AppWindowQuery()) return FindSteamBigPicture(query); }
        internal static WindowObservation FindSteamBigPicture(AppWindowQuery query)
        {
            string root = null;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) if (key != null) root = key.GetValue("SteamPath") as string;
            if (string.IsNullOrWhiteSpace(root)) return null;
            WindowObservation found = null; bool ambiguous = false, uncertain = false;
            bool complete = EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                try
                {
                    if (EligibleReason(hwnd, 0, query) != null) return true;
                    // Filtrar titulo antes das consultas de processo; nao extrair icones na thread de UI.
                    var title = new StringBuilder(2048); GetWindowText(hwnd, title, title.Capacity);
                    if (title.ToString().IndexOf("Big Picture", StringComparison.OrdinalIgnoreCase) < 0) return true;
                    var candidate = Observe(hwnd);
                    if (candidate == null || string.IsNullOrEmpty(candidate.Window.ExecutablePath)) { uncertain = true; return true; }
                    if (candidate != null && IsSteamBigPicture(candidate.Window, root))
                    {
                        if (found != null) { ambiguous = true; return false; }
                        found = candidate;
                    }
                }
                catch (Exception) { uncertain = true; } // Nenhuma excecao atravessa o callback nativo.
                return true;
            }, IntPtr.Zero);
            if (ambiguous) throw new InvalidOperationException("Há mais de uma janela do Big Picture. Escolha a janela em Aplicativos abertos.");
            if (!complete) throw new InvalidOperationException("Não foi possível consultar as janelas da Steam.");
            if (found == null && uncertain) throw new InvalidOperationException("Uma janela candidata ao Big Picture não pôde ser identificada. Tente novamente.");
            return found;
        }

        // Devolver foco nao restaura uma janela que o usuario minimizou durante a abertura da barra.
        public static bool FocusOnly(DesktopWindow item)
        {
            if (item == null || item.ProcessCreated == 0 || !Available(item) || IsIconic(item.Handle)) return false;
            if (SetForegroundWindow(item.Handle)) return true;
            // Negado mesmo com a barra na frente (barra elevada no modo Xbox, 2026-10-25): repete com a fila do destino ligada.
            uint pid;
            uint target = GetWindowThreadProcessId(item.Handle, out pid), current = GetCurrentThreadId();
            if (target == 0 || target == current || !AttachThreadInput(current, target, true)) return false;
            try { SetForegroundWindow(item.Handle); }
            finally { AttachThreadInput(current, target, false); }
            return GetForegroundWindow() == item.Handle;
        }

        // Primeiro plano para uma janela propria. O Windows so o concede ao processo que recebeu a ultima entrada de
        // teclado/mouse, e a leitura HID do controle nao conta. Se o pedido direto for negado, liga por um instante a
        // fila de entrada desta thread a do app em primeiro plano, repete o pedido e desliga.
        // Recusada a fila (erro 5: app elevado acima da barra ou janela de app da Store, observado no ApplicationFrameHost),
        // e keyUnlock ligado: Alt com uma tecla sem uso no meio, para nao abrir menus. O Windows libera o primeiro plano
        // quando recebe Alt (ver LockSetForegroundWindow); o app da frente recebe esse Alt (ADR 0044).
        public static string TakeForeground(IntPtr own, bool keyUnlock)
        {
            if (own == IntPtr.Zero || !IsWindow(own)) return "janela invalida";
            IntPtr before = GetForegroundWindow();
            if (before == own) return "ja em primeiro plano";
            if (SetForegroundWindow(own) && GetForegroundWindow() == own) return "concedido";
            uint pid;
            uint target = before == IntPtr.Zero ? 0 : GetWindowThreadProcessId(before, out pid), current = GetCurrentThreadId();
            if (target == 0 || target == current) return "negado";
            if (AttachThreadInput(current, target, true))
            {
                try { BringWindowToTop(own); SetForegroundWindow(own); }
                finally { AttachThreadInput(current, target, false); }
                return GetForegroundWindow() == own ? "concedido com fila anexada" : "negado mesmo com fila anexada";
            }
            string refused = "fila nao anexada (erro " + Marshal.GetLastWin32Error() + ")";
            if (!keyUnlock) return "negado; " + refused;
            int sent = SendNeutralAlt();
            SetForegroundWindow(own);
            return GetForegroundWindow() == own ? refused + "; concedido com Alt neutro" : refused + "; negado mesmo com Alt neutro (" + sent + " de 4 teclas)";
        }
        // Alt, tecla 0xE8 (sem uso) apertada e solta, Alt solto: sem a tecla do meio, soltar o Alt ativaria a barra de menus.
        static int SendNeutralAlt()
        {
            var keys = new[] { Key(0x12, 0), Key(0xE8, 0), Key(0xE8, 2), Key(0x12, 2) };
            return (int)SendInput((uint)keys.Length, keys, Marshal.SizeOf(typeof(NativeInput)));
        }
        static NativeInput Key(ushort vk, uint flags) { return new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Vk = vk, Flags = flags } } }; }
        // Primeiro app (janela que conta em Apps abertos) de cima para baixo: o destino quando a frente nao e um app
        // (Visao de Tarefas, janela vazia do shell).
        public static WindowObservation FrontApp(uint excludedProcess)
        {
            using (var query = new AppWindowQuery()) return FrontApp(excludedProcess, query);
        }
        static WindowObservation FrontApp(uint excludedProcess, AppWindowQuery query)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                try { if (EligibleReason(hwnd, excludedProcess, query) == null) { found = hwnd; return false; } } catch (Exception) { }
                return true;
            }, IntPtr.Zero);
            return found == IntPtr.Zero ? null : Observe(found);
        }
        // Janela que minimizou enquanto a barra estava na frente (jogo que minimiza ao perder o foco): restaura e devolve o foco.
        public static bool RestoreAndFocus(DesktopWindow item)
        {
            if (item == null || item.ProcessCreated == 0 || !Belongs(item)) return false;
            if (IsIconic(item.Handle)) ShowWindowAsync(item.Handle, 9); // SW_RESTORE
            if (SetForegroundWindow(item.Handle)) return true;
            uint pid;
            uint target = GetWindowThreadProcessId(item.Handle, out pid), current = GetCurrentThreadId();
            if (target == 0 || target == current || !AttachThreadInput(current, target, true)) return false;
            try { SetForegroundWindow(item.Handle); }
            finally { AttachThreadInput(current, target, false); }
            return GetForegroundWindow() == item.Handle;
        }
        // Apps escondidos pelo shell (DWM_CLOAKED_SHELL) que de resto contariam: no modo Xbox, os que ficam atras.
        public static List<string> ShellCloakedApps(uint excludedProcess)
        {
            var list = new List<string>();
            using (var query = new AppWindowQuery())
            {
            EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                try
                {
                    if (query.Cloak(hwnd) != 2 || EligibleReason(hwnd, excludedProcess, query) != null) return true;
                    uint pid; GetWindowThreadProcessId(hwnd, out pid);
                    var title = new StringBuilder(256); GetWindowText(hwnd, title, title.Capacity);
                    string path = ProcessPath(pid);
                    list.Add(hwnd.ToInt64() + " " + (path == null ? "pid " + pid : Path.GetFileName(path)) + " \"" + title + "\"");
                }
                catch (Exception) { }
                return true;
            }, IntPtr.Zero);
            }
            return list;
        }
        // Processo da Steam (cliente, Big Picture) ou jogo de uma biblioteca da Steam: so ai a Steam disputa o controle.
        public static bool SteamOwned(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath)) return false;
            string path = executablePath.Replace('/', '\\');
            if (path.IndexOf(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string root = null;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) if (key != null) root = key.GetValue("SteamPath") as string;
            if (string.IsNullOrWhiteSpace(root)) return false;
            root = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        // Janela de ferramenta (WS_EX_TOOLWINDOW): fora do Alt+Tab e da Visao de tarefas; nao muda tamanho nem posicao.
        public static void MarkToolWindow(IntPtr own)
        {
            long style = GetWindowLongPtr(own, -20).ToInt64();
            if ((style & 0x80) == 0) SetWindowLongPtr(own, -20, new IntPtr(style | 0x80));
        }

        [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct WindowPoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct WindowPlacement
        { public int Length, Flags, ShowCommand; public WindowPoint Min, Max; public WindowRect Normal; }
        [StructLayout(LayoutKind.Sequential)] struct MonitorInfo
        { public int Size; public WindowRect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
        [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)] struct NativeInput { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, NativeInput[] inputs, int size);
        [DllImport("user32.dll", SetLastError = true)] static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hwnd);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    }
}
