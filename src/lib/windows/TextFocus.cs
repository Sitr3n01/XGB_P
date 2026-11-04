// Caixa de texto em foco no app da frente, para o teclado virtual abrir sozinho no modo Xbox.
// Gatilho: WinEvents de primeiro plano e de foco, fora do processo (WINEVENT_OUTOFCONTEXT, sem injecao de codigo).
// Leitura: UI Automation numa thread MTA propria, so do elemento em foco. Nunca le o conteudo, o nome nem o valor do campo.
// Processos excluidos (Steam, DSX, a propria barra) nao recebem consulta: o Chromium/CEF liga a acessibilidade quando
// um cliente UIA o consulta, e o Big Picture nao deve pagar esse custo.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace XGBP.Desktop
{
    public sealed class TextFocusInfo
    {
        public IntPtr Window;                    // janela em primeiro plano no momento da leitura
        public uint WindowProcess, ElementProcess;
        public string ExecutablePath;            // do processo da janela da frente
        public string ControlType, ClassName, Framework;
        public bool Editable, Password, ReadOnly, Excluded;
        // Janela + RuntimeId do elemento: identifica o campo (para nao reabrir onde o usuario fechou), sem ler conteudo.
        public string Key;
        public bool HasBounds;
        public double Left, Top, Right, Bottom;  // pixels de tela
        public string Note;

        public string Describe()
        {
            string exe = string.IsNullOrEmpty(ExecutablePath) ? "pid " + WindowProcess : Path.GetFileName(ExecutablePath);
            if (Note != null) return Note + " | " + exe + " | hwnd=" + Window.ToInt64();
            return (ControlType ?? "?") + (Editable ? " editavel" : ReadOnly ? " somente leitura" : " sem texto") + (Password ? " senha" : "") +
                " | " + exe + (ElementProcess != 0 && ElementProcess != WindowProcess ? " (elemento pid " + ElementProcess + ")" : "") +
                " | " + (Framework ?? "") + "/" + (ClassName ?? "") + " | hwnd=" + Window.ToInt64();
        }
    }

    public sealed class TextFocusWatcher : IDisposable
    {
        const uint EventSystemForeground = 0x0003, EventObjectFocus = 0x8005, OutOfContext = 0x0000, SkipOwnProcess = 0x0002;
        const int QuietMs = 120, MaxCoalesceMs = 600;
        public event Action<TextFocusInfo> Changed;
        public event Action<string> Log;
        readonly Func<string, bool> excluded;
        readonly AutoResetEvent signal = new AutoResetEvent(false);
        Thread hookThread, queryThread;
        uint hookThreadId;
        volatile bool stopping;
        int generation;
        int threadsRemaining = 2;
        internal bool Stopped { get { return Interlocked.CompareExchange(ref threadsRemaining, 0, 0) == 0; } }
        static int queryBusy;
        WinEventProc callback;   // referencia viva enquanto o gancho existir

        public TextFocusWatcher(Func<string, bool> excluded) { this.excluded = excluded; }

        public void Start()
        {
            hookThread = new Thread(HookLoop) { IsBackground = true, Name = "XGBP foco de texto (eventos)" };
            hookThread.Start();
            queryThread = new Thread(QueryLoop) { IsBackground = true, Name = "XGBP foco de texto (UIA)" };
            queryThread.SetApartmentState(ApartmentState.MTA);
            queryThread.Start();
            signal.Set(); // le o estado atual uma vez
        }

        void HookLoop()
        {
            Message initial;
            PeekMessage(out initial, IntPtr.Zero, 0, 0, 0); // Cria a fila antes de publicar o ID para Dispose.
            hookThreadId = GetCurrentThreadId();
            callback = OnWinEvent;
            IntPtr foreground = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, callback, 0, 0, OutOfContext | SkipOwnProcess);
            IntPtr focus = SetWinEventHook(EventObjectFocus, EventObjectFocus, IntPtr.Zero, callback, 0, 0, OutOfContext | SkipOwnProcess);
            if (foreground == IntPtr.Zero || focus == IntPtr.Zero) Report("ganchos de foco indisponiveis (erro " + Marshal.GetLastWin32Error() + ")");
            try
            {
                Message message;
                while (!stopping && GetMessage(out message, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessage(ref message); }
            }
            finally
            {
                if (foreground != IntPtr.Zero) UnhookWinEvent(foreground);
                if (focus != IntPtr.Zero) UnhookWinEvent(focus);
                ThreadExited();
            }
        }
        void OnWinEvent(IntPtr hook, uint type, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            Interlocked.Increment(ref generation);
            try { signal.Set(); } catch (ObjectDisposedException) { }
        }

        // Rajadas de foco (Tab, abrir janela) viram uma leitura so: espera 120 ms sem eventos novos, no maximo 600 ms.
        void QueryLoop()
        {
            try
            {
            while (!stopping)
            {
                signal.WaitOne();
                if (stopping) break;
                int waited = 0, seen;
                do { seen = Thread.VolatileRead(ref generation); Thread.Sleep(QuietMs); waited += QuietMs; }
                while (!stopping && seen != Thread.VolatileRead(ref generation) && waited < MaxCoalesceMs);
                if (stopping) break;
                TextFocusInfo info;
                try { info = Query(excluded); }
                catch (Exception ex) { info = new TextFocusInfo { Note = "leitura falhou: " + ex.GetType().Name }; }
                var handler = Changed;
                if (handler != null) try { handler(info); } catch (Exception ex) { Report("callback falhou: " + ex.Message); }
            }
            }
            finally { ThreadExited(); }
        }

        // Uma leitura do campo em foco no app da frente. Publica para o diagnostico somente leitura.
        public static TextFocusInfo Query(Func<string, bool> excluded)
        {
            // Um provider UIA travado ocupa no maximo uma thread, mesmo apos reentradas no FSE.
            if (Interlocked.CompareExchange(ref queryBusy, 1, 0) != 0)
                return new TextFocusInfo { Note = "UIA ocupada por outra consulta" };
            try { return QueryCore(excluded); }
            finally { Interlocked.Exchange(ref queryBusy, 0); }
        }
        static TextFocusInfo QueryCore(Func<string, bool> excluded)
        {
            IntPtr window = GetForegroundWindow();
            var info = new TextFocusInfo { Window = window };
            if (window == IntPtr.Zero) { info.Note = "sem janela na frente"; info.Excluded = true; return info; }
            uint pid; GetWindowThreadProcessId(window, out pid);
            info.WindowProcess = pid;
            using (var own = Process.GetCurrentProcess()) if (pid == (uint)own.Id) { info.Note = "propria barra"; info.Excluded = true; return info; }
            info.ExecutablePath = DesktopWindows.ProcessPath(pid);
            // Caminho ilegivel: processo protegido ou acima desta integridade, que o UIA tambem nao alcancaria.
            if (info.ExecutablePath == null || excluded != null && excluded(info.ExecutablePath)) { info.Note = "app excluido"; info.Excluded = true; return info; }
            AutomationElement element;
            try { element = AutomationElement.FocusedElement; }
            catch (Exception ex) { info.Note = "UIA indisponivel: " + ex.GetType().Name; return info; }
            if (element == null) { info.Note = "sem elemento em foco"; return info; }
            try
            {
                var current = element.Current;
                info.ElementProcess = (uint)current.ProcessId;
                if (info.ElementProcess != pid && excluded != null)
                {
                    string elementPath = DesktopWindows.ProcessPath(info.ElementProcess);
                    if (elementPath == null || excluded(elementPath)) { info.Note = "elemento de app excluido"; info.Excluded = true; return info; }
                }
                ControlType type = current.ControlType;
                info.ControlType = type == null ? "?" : type.ProgrammaticName.Replace("ControlType.", "");
                info.ClassName = current.ClassName; info.Framework = current.FrameworkId;
                info.Password = current.IsPassword;
                bool enabled = current.IsEnabled;
                object pattern;
                bool hasValue = element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern);
                bool valueReadOnly = hasValue && ((ValuePattern)pattern).Current.IsReadOnly;
                bool? textReadOnly = null;
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                {
                    // So a selecao/cursor: o atributo sobre o documento inteiro pode percorrer uma pagina enorme.
                    try
                    {
                        var selection = ((TextPattern)pattern).GetSelection();
                        if (selection != null && selection.Length > 0)
                        {
                            object value = selection[0].GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                            if (value is bool) textReadOnly = (bool)value;
                        }
                    }
                    catch (Exception) { }
                }
                // Edit: editavel salvo indicacao de somente leitura. Document (corpo de pagina, Bloco de Notas): so com
                // valor editavel ou cursor num trecho editavel. ComboBox editavel sem filho Edit.
                if (type == ControlType.Edit) info.Editable = enabled && !(hasValue ? valueReadOnly : textReadOnly == true);
                else if (type == ControlType.Document) info.Editable = enabled && (hasValue ? !valueReadOnly && textReadOnly != true : textReadOnly == false);
                else if (type == ControlType.ComboBox) info.Editable = enabled && hasValue && !valueReadOnly;
                info.ReadOnly = !info.Editable && (valueReadOnly || textReadOnly == true);
                var bounds = current.BoundingRectangle;
                if (!bounds.IsEmpty && !double.IsInfinity(bounds.Width) && bounds.Width > 0 && bounds.Height > 0)
                { info.HasBounds = true; info.Left = bounds.Left; info.Top = bounds.Top; info.Right = bounds.Right; info.Bottom = bounds.Bottom; }
                int[] runtime = element.GetRuntimeId();
                var key = new StringBuilder(window.ToInt64().ToString());
                if (runtime != null) foreach (int part in runtime) key.Append('.').Append(part);
                info.Key = key.ToString();
            }
            catch (ElementNotAvailableException) { info.Note = "elemento sumiu"; info.Editable = false; }
            catch (Exception ex) { info.Note = "UIA falhou: " + ex.GetType().Name; info.Editable = false; }
            return info;
        }

        void Report(string text) { var log = Log; if (log != null) try { log(text); } catch (Exception) { } }

        public void Dispose()
        {
            stopping = true;
            try { signal.Set(); } catch (ObjectDisposedException) { }
            Changed = null; Log = null;
            if (hookThreadId != 0) PostThreadMessage(hookThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        }
        void ThreadExited() { if (Interlocked.Decrement(ref threadsRemaining) == 0) signal.Dispose(); }

        delegate void WinEventProc(IntPtr hook, uint type, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        [StructLayout(LayoutKind.Sequential)] struct Message { public IntPtr Hwnd; public uint Msg; public IntPtr WParam, LParam; public uint Time; public int X, Y; }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] static extern int GetMessage(out Message message, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] static extern bool PeekMessage(out Message message, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref Message message);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    }

    // Nivel de integridade (RID do rotulo obrigatorio): o SendInput a um app acima deste processo e descartado em silencio.
    public static class ProcessIntegrity
    {
        public const int Medium = 0x2000, High = 0x3000, System = 0x4000;

        // null: o token nao pode ser lido (processo protegido ou acima desta integridade).
        public static int? Level(uint pid)
        {
            IntPtr process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (process == IntPtr.Zero) return null;
            try
            {
                IntPtr token;
                if (!OpenProcessToken(process, 0x0008, out token)) return null; // TOKEN_QUERY
                try
                {
                    int size;
                    GetTokenInformation(token, 25, IntPtr.Zero, 0, out size); // TokenIntegrityLevel
                    if (size <= 0) return null;
                    IntPtr buffer = Marshal.AllocHGlobal(size);
                    try
                    {
                        if (!GetTokenInformation(token, 25, buffer, size, out size)) return null;
                        IntPtr sid = Marshal.ReadIntPtr(buffer);
                        int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                        if (count < 1) return null;
                        return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
                    }
                    finally { Marshal.FreeHGlobal(buffer); }
                }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(process); }
        }
        public static int Current()
        {
            using (var own = Process.GetCurrentProcess()) { int? level = Level((uint)own.Id); return level ?? Medium; }
        }
        public static string Name(int? level)
        {
            if (level == null) return "desconhecida";
            int value = level.Value;
            return value >= System ? "sistema" : value >= High ? "alta" : value >= Medium ? "media" : "baixa";
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returned);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    }
}
