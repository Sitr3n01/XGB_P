// Apps residentes na bandeja (como o DSX): a janela principal existe, oculta e em geral minimizada.
// Somente leitura e o mesmo pedido de restaurar da bandeja; sem mensagens privadas, cliques simulados ou injecao.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace XGBP.Desktop
{
    public static partial class DesktopWindows
    {
        // null: existe processo com esse nome cujo caminho nao foi possivel conferir.
        public static bool? ProcessRunning(string name, Func<string, bool> executable)
        {
            bool unknown = false;
            foreach (var process in Process.GetProcessesByName(name))
                using (process)
                {
                    string path = ProcessPath((uint)process.Id);
                    if (path == null) unknown = true;
                    else if (executable(path)) return true;
                }
            return unknown ? (bool?)null : false;
        }
        // Janela de topo sem dono, com barra de titulo e titulo, de um processo do executavel pedido, visivel ou nao.
        public static DesktopWindow FindTrayWindow(string name, Func<string, bool> executable)
        {
            var paths = new Dictionary<uint, string>();
            foreach (var process in Process.GetProcessesByName(name))
                using (process)
                {
                    string path = ProcessPath((uint)process.Id);
                    if (path != null && executable(path)) paths[(uint)process.Id] = path;
                }
            if (paths.Count == 0) return null;
            DesktopWindow found = null;
            EnumWindows(delegate(IntPtr hwnd, IntPtr arg) {
                uint pid; GetWindowThreadProcessId(hwnd, out pid);
                string path; if (!paths.TryGetValue(pid, out path) || GetWindow(hwnd, 4) != IntPtr.Zero) return true;
                long style = GetWindowLongPtr(hwnd, -16).ToInt64(), extended = GetWindowLongPtr(hwnd, -20).ToInt64();
                if ((style & 0x00C00000) != 0x00C00000 || (extended & 0x80) != 0 || (extended & 0x08000000) != 0) return true; // CAPTION; sem TOOLWINDOW/NOACTIVATE
                var title = new StringBuilder(256); GetWindowText(hwnd, title, title.Capacity);
                if (string.IsNullOrWhiteSpace(title.ToString())) return true;
                found = new DesktopWindow { Handle = hwnd, ProcessId = pid, Title = title.ToString(), AppName = title.ToString(), ClassName = WindowClass(hwnd),
                    ExecutablePath = path, ProcessCreated = ProcessCreation(pid), Minimized = IsIconic(hwnd) };
                return false;
            }, IntPtr.Zero);
            return found;
        }
        public static bool IsShown(DesktopWindow item) { return Exists(item) == true && IsWindowVisible(item.Handle); }
        // Restaurar, como o clique no icone da bandeja; o app decide o resto. Elevado so aceita de outro processo elevado.
        public static void Reveal(DesktopWindow item)
        {
            if (Exists(item) != true) throw new InvalidOperationException("A janela não existe mais.");
            if (!ShowWindowAsync(item.Handle, IsIconic(item.Handle) ? 9 : 5)) throw new InvalidOperationException("O Windows recusou mostrar a janela.");
        }
    }
}
