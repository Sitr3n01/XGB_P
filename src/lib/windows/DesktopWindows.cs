// Janelas do desktop: enumeracao e alternancia por APIs publicas, sem hooks ou injecao.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace XGBP.Desktop
{
    public sealed class DesktopWindow
    {
        public IntPtr Handle;
        public uint ProcessId;
        public string Title, AppName, ClassName, ExecutablePath;
        public string AppUserModelId;
        public byte[] IconPng;
        public bool Minimized;
        public long ProcessCreated;
        public string Key { get { return ProcessId + ":" + Handle.ToInt64(); } }
    }

    public static partial class DesktopWindows
    {
        // Lido por varios workers ao mesmo tempo (na previa: jogos, apps abertos e DSX): acesso sempre sob trava.
        // Sem ela o Dictionary corrompia e prendia uma thread em laco (sessao de debug 1, 2026-10-24).
        static readonly Dictionary<string, byte[]> Icons = new Dictionary<string, byte[]>();

        public static List<DesktopWindow> Read(uint excludedProcess)
        {
            using (var query = new AppWindowQuery()) return Read(excludedProcess, query);
        }
        internal static List<DesktopWindow> Read(uint excludedProcess, AppWindowQuery query)
        {
            var result = new List<DesktopWindow>(); var live = new HashSet<string>();
            if (!EnumWindows(delegate(IntPtr hwnd, IntPtr arg) {
                try
                {
                    if (EligibleReason(hwnd, excludedProcess, query) != null) return true;
                    uint pid; GetWindowThreadProcessId(hwnd, out pid);
                    var title = new StringBuilder(2048); GetWindowText(hwnd, title, title.Capacity);
                    var item = new DesktopWindow { Handle = hwnd, ProcessId = pid, Title = title.ToString(), ClassName = WindowClass(hwnd), Minimized = IsIconic(hwnd) };
                    string path = ProcessPath(pid); item.ExecutablePath = path; item.ProcessCreated = ProcessCreation(pid); item.AppName = item.Title;
                    item.AppUserModelId = WindowAppId(hwnd, pid);
                    if (path != null)
                    {
                        try
                        {
                            var info = FileVersionInfo.GetVersionInfo(path);
                            string name = string.IsNullOrWhiteSpace(info.FileDescription) ? info.ProductName : info.FileDescription;
                            if (!string.IsNullOrWhiteSpace(name) && !path.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) item.AppName = name;
                        }
                        catch { }
                    }
                    string iconKey = item.Key + "|" + item.ClassName;
                    live.Add(iconKey);
                    byte[] icon; bool cached;
                    lock (Icons) cached = Icons.TryGetValue(iconKey, out icon);
                    // A extracao (mensagem a janela) fica fora da trava.
                    if (!cached) { icon = WindowIcon(hwnd, path); lock (Icons) Icons[iconKey] = icon; }
                    item.IconPng = icon; result.Add(item);
                }
                catch { } // Uma janela pode desaparecer durante a coleta.
                return true;
            }, IntPtr.Zero)) throw new InvalidOperationException("Não foi possível consultar as janelas do Windows.");
            lock (Icons) foreach (var key in new List<string>(Icons.Keys)) if (!live.Contains(key)) Icons.Remove(key);
            return result;
        }

        static bool Eligible(IntPtr hwnd, uint excludedProcess)
        { using (var query = new AppWindowQuery()) return EligibleReason(hwnd, excludedProcess, query) == null; }
        // Motivo de uma janela nao contar como app (null quando conta); a mesma regra da lista e dos alternadores.
        static string EligibleReason(IntPtr hwnd, uint excludedProcess)
        { using (var query = new AppWindowQuery()) return EligibleReason(hwnd, excludedProcess, query); }
        internal static string EligibleReason(IntPtr hwnd, uint excludedProcess, AppWindowQuery query)
        {
            if (!IsWindow(hwnd)) return "nao existe";
            if (!IsWindowVisible(hwnd)) return "invisivel";
            if (hwnd == GetShellWindow()) return "shell";
            uint pid; GetWindowThreadProcessId(hwnd, out pid); if (pid == 0 || pid == excludedProcess) return "processo excluido";
            long style = GetWindowLongPtr(hwnd, -20).ToInt64();
            if ((style & 0x80) != 0 || (style & 0x08000000) != 0) return "ferramenta"; // TOOLWINDOW / NOACTIVATE
            if (GetWindow(hwnd, 4) != IntPtr.Zero && (style & 0x40000) == 0) return "com dono"; // owner, exceto APPWINDOW
            string name = WindowClass(hwnd);
            if (name == "Shell_TrayWnd" || name == "Shell_SecondaryTrayWnd" || name == "Progman" || name == "WorkerW") return "barra de tarefas";
            var text = new StringBuilder(2048); GetWindowText(hwnd, text, text.Capacity);
            if (string.IsNullOrWhiteSpace(text.ToString())) return "sem titulo";
            if (InternalShellSurface(name, ProcessPath(pid))) return "componente do shell";
            uint? cloaked = query.Cloak(hwnd);
            if (cloaked.HasValue && !AppWindowQuery.CloakAllowed(cloaked.Value, query.XboxMode,
                cloaked == 2 && query.XboxMode == true ? query.OnCurrentDesktop(hwnd) : null))
                return "camuflada=" + WindowObservation.CloakName(cloaked.Value);
            // A CoreWindow e o conteudo de um frame UWP, nao outra janela do app.
            if (name == "Windows.UI.Core.CoreWindow" && query.HasFrame(WindowAppId(hwnd, pid), excludedProcess)) return "conteudo hospedado";
            return null;
        }
        static string WindowClass(IntPtr hwnd)
        { var text = new StringBuilder(256); GetClassName(hwnd, text, text.Capacity); return text.ToString(); }

        // Por que Available recusou: diagnostico de "Essa janela ja foi fechada".
        public static string Unavailability(DesktopWindow item)
        {
            if (item == null || !IsWindow(item.Handle)) return "janela nao existe";
            uint pid; GetWindowThreadProcessId(item.Handle, out pid);
            if (pid != item.ProcessId) return "HWND de outro processo";
            if (item.ProcessCreated != 0 && item.ProcessCreated != ProcessCreation(pid)) return "processo recriado";
            if (!string.IsNullOrEmpty(item.ExecutablePath) && !string.Equals(item.ExecutablePath, ProcessPath(pid), StringComparison.OrdinalIgnoreCase)) return "executavel diferente";
            if (WindowClass(item.Handle) != item.ClassName) return "classe diferente";
            return EligibleReason(item.Handle, 0) ?? "disponivel";
        }
        public static bool Available(DesktopWindow item)
        { using (var query = new AppWindowQuery()) return Available(item, query); }
        internal static bool Available(DesktopWindow item, AppWindowQuery query)
        {
            return Exists(item) == true && EligibleReason(item.Handle, 0, query) == null;
        }
        // Mesma janela do mesmo processo, ainda que fora da enumeracao (por exemplo, "nao respondendo" ou pausada).
        public static bool Belongs(DesktopWindow item)
        { return Exists(item) == true; }
        // null: nao foi possivel conferir a identidade. Nunca usar falha de leitura como prova de fechamento.
        public static bool? Exists(DesktopWindow item)
        {
            if (item == null || !IsWindow(item.Handle)) return false;
            uint pid; GetWindowThreadProcessId(item.Handle, out pid);
            if (pid == 0) return null;
            if (pid != item.ProcessId || WindowClass(item.Handle) != item.ClassName) return false;
            if (item.ProcessCreated != 0)
            {
                long created = ProcessCreation(pid); if (created == 0) return null;
                if (created != item.ProcessCreated) return false;
            }
            if (!string.IsNullOrEmpty(item.ExecutablePath))
            {
                string path = ProcessPath(pid); if (path == null) return null;
                if (!string.Equals(path, item.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return IsWindow(item.Handle) ? (bool?)true : false;
        }
        // Fechamento normal, o mesmo pedido do botao fechar da janela: o programa pode salvar ou pedir confirmacao.
        public static void Close(DesktopWindow item)
        {
            if (!Belongs(item)) throw new InvalidOperationException("Essa janela já foi fechada.");
            if (!PostMessage(item.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero))
                throw new InvalidOperationException(Marshal.GetLastWin32Error() == 5
                    ? "O Windows negou fechar " + item.AppName + ". Ele pode estar como administrador."
                    : "Não foi possível fechar " + item.AppName + ".");
        }
        public static void Activate(DesktopWindow item)
        { ActivateWithReport(item); }
        public static string ActivateWithReport(DesktopWindow item)
        { using (var query = new AppWindowQuery()) return Activate(item, query); }
        // Jogo escolhido: nao promover um popup auxiliar/overlay como destino da transicao.
        public static string ActivateGameWithReport(DesktopWindow item)
        { using (var query = new AppWindowQuery()) return Activate(item, query, false); }
        internal static string Activate(DesktopWindow item, AppWindowQuery query, bool allowPopup = true)
        {
            if (!Available(item, query)) throw new InvalidOperationException("Essa janela já foi fechada ou não está mais disponível.");
            bool restoring = IsIconic(item.Handle);
            if (restoring) ShowWindowAsync(item.Handle, 9); // SW_RESTORE, preserva janelas maximizadas
            IntPtr popup = GetLastActivePopup(item.Handle);
            uint? popupCloak = popup == IntPtr.Zero ? null : query.Cloak(popup);
            bool popupUsable = allowPopup && popup != IntPtr.Zero && IsWindowVisible(popup) && popupCloak.HasValue &&
                AppWindowQuery.CloakAllowed(popupCloak.Value, query.XboxMode, popupCloak == 2 ? query.OnCurrentDesktop(popup) : null);
            IntPtr target = popupUsable ? popup : item.Handle;
            string report = RequestAppForeground(target);
            // O chamador mantem a barra ate observar foreground visivel; retorno nativo nao prova a troca assincrona.
            return "hwnd=" + item.Handle + "; destino=" + target + "; restauracao=" + restoring + "; " + report;
        }
        public static bool IsForeground(DesktopWindow item)
        { using (var query = new AppWindowQuery()) return IsForeground(item, query); }
        public static bool IsGameForeground(DesktopWindow item)
        { return item != null && GetForegroundWindow() == item.Handle && IsForeground(item); }
        public static bool OwnsForeground(DesktopWindow item)
        {
            if (Exists(item) != true) return false;
            IntPtr foreground = GetForegroundWindow();
            return foreground != IntPtr.Zero && (foreground == item.Handle || GetAncestor(foreground, 3) == GetAncestor(item.Handle, 3));
        }
        internal static bool IsForeground(DesktopWindow item, AppWindowQuery query)
        {
            if (!Available(item, query) || IsIconic(item.Handle)) return false;
            IntPtr foreground = GetForegroundWindow();
            return foreground != IntPtr.Zero && IsWindowVisible(foreground) && query.Cloak(foreground) == 0 &&
                (foreground == item.Handle || GetAncestor(foreground, 3) == GetAncestor(item.Handle, 3));
        }

        // Somente a superficie de espera do modo Xbox, nunca um app desconhecido ou o shell inteiro.
        public static bool GamingFallback(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || WindowClass(hwnd) != "GamingFallbackWindow") return false;
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            return string.Equals(ProcessPath(pid), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase);
        }

        static byte[] WindowIcon(IntPtr hwnd, string path)
        {
            IntPtr icon = IntPtr.Zero, answer;
            foreach (int size in new[] { 1, 2, 0 })
            {
                if (SendMessageTimeout(hwnd, 0x7F, new IntPtr(size), IntPtr.Zero, 2, 40, out answer) != IntPtr.Zero && answer != IntPtr.Zero) { icon = answer; break; }
            }
            if (icon == IntPtr.Zero) icon = GetClassLongPtr(hwnd, -14);
            if (icon == IntPtr.Zero) icon = GetClassLongPtr(hwnd, -34);
            try
            {
                if (icon != IntPtr.Zero)
                {
                    IntPtr copy = CopyIcon(icon);
                    if (copy != IntPtr.Zero) try
                    {
                        using (var owned = Icon.FromHandle(copy)) using (var bitmap = owned.ToBitmap()) using (var stream = new MemoryStream())
                        { bitmap.Save(stream, ImageFormat.Png); return stream.ToArray(); }
                    }
                    finally { DestroyIcon(copy); }
                }
                if (path != null && File.Exists(path)) using (var owned = Icon.ExtractAssociatedIcon(path))
                {
                    if (owned != null) using (var bitmap = owned.ToBitmap()) using (var stream = new MemoryStream())
                    { bitmap.Save(stream, ImageFormat.Png); return stream.ToArray(); }
                }
            }
            catch { }
            return null;
        }
        public static string ProcessPath(uint pid)
        {
            IntPtr process = OpenProcess(0x1000, false, pid); if (process == IntPtr.Zero) return null;
            try { var path = new StringBuilder(32768); int size = path.Capacity; return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : null; }
            finally { CloseHandle(process); }
        }
        static long ProcessCreation(uint pid)
        {
            IntPtr process = OpenProcess(0x1000, false, pid); if (process == IntPtr.Zero) return 0;
            try { long created, exit, kernel, user; return GetProcessTimes(process, out created, out exit, out kernel, out user) ? created : 0; }
            finally { CloseHandle(process); }
        }
        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr process, out long created, out long exit, out long kernel, out long user);
        delegate bool EnumProc(IntPtr hwnd, IntPtr arg);
        [DllImport("user32.dll", SetLastError = true)] static extern bool EnumWindows(EnumProc callback, IntPtr arg);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] static extern IntPtr GetClassLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out uint value, uint size);
        [DllImport("user32.dll")] static extern IntPtr GetLastActivePopup(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr hwnd, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] static extern IntPtr CopyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    }
}
