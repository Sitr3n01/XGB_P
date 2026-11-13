// Pausa, retomada e fechamento de jogos abertos, so por escolha do usuario.
// Mesma tecnica do Nyrna (GPL-3.0; codigo proprio, sem copia): minimizar, NtSuspendProcess/NtResumeProcess
// no processo e nos descendentes, e estado pausado observado pelos threads. Sem injecao, hooks ou escrita de memoria.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XGBP.Games
{
    public static partial class GameControl
    {
        const uint Terminate = 0x0001, SuspendResume = 0x0800, QueryLimited = 0x1000;

        // Fracao dos threads em espera por suspensao (0 a 1; -1 se ilegivel). Observado em 2026-10-24: um thread preso
        // numa espera do kernel (UserRequest) so suspende ao voltar ao modo usuario, entao "todos" falha logo apos pausar.
        public static double SuspendedShare(uint pid)
        {
            try
            {
                using (var process = Process.GetProcessById((int)pid))
                {
                    var threads = process.Threads; if (threads.Count == 0) return -1;
                    int suspended = 0;
                    foreach (ProcessThread thread in threads)
                        if (thread.ThreadState == System.Diagnostics.ThreadState.Wait && thread.WaitReason == ThreadWaitReason.Suspended) suspended++;
                    return (double)suspended / threads.Count;
                }
            }
            catch (ArgumentException) { return -1; } catch (InvalidOperationException) { return -1; } catch (Win32Exception) { return -1; }
        }
        // Pausado pela maioria dos threads: vale para pausas do XGB_P e de outras ferramentas.
        public static bool Suspended(uint pid) { return SuspendedShare(pid) > .5; }

        // Raizes e descendentes, como no Nyrna. Um filho so entra se nasceu depois do pai: PID reaproveitado nao herda a acao.
        public static List<uint> Tree(IEnumerable<uint> roots)
        {
            var parents = new Dictionary<uint, List<uint>>();
            foreach (var pair in Parents())
            {
                List<uint> children; if (!parents.TryGetValue(pair.Value, out children)) parents[pair.Value] = children = new List<uint>();
                if (pair.Key != pair.Value) children.Add(pair.Key);
            }
            var result = new List<uint>(); var seen = new HashSet<uint>(); var pending = new Queue<uint>();
            foreach (var pid in roots) if (seen.Add(pid)) { result.Add(pid); pending.Enqueue(pid); }
            while (pending.Count > 0)
            {
                uint parent = pending.Dequeue(); List<uint> children;
                if (!parents.TryGetValue(parent, out children)) continue;
                long born = Created(parent);
                foreach (var child in children)
                    if (born > 0 && Created(child) >= born && seen.Add(child)) { result.Add(child); pending.Enqueue(child); }
            }
            return result;
        }

        // PID -> PID do pai no instante da leitura (Toolhelp). O pai pode ja ter saido e o PID ser reaproveitado:
        // quem usa confere a criacao. Vazio se o snapshot falhar.
        public static Dictionary<uint, uint> Parents()
        {
            var result = new Dictionary<uint, uint>();
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) return result;
            try
            {
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                for (bool ok = Process32First(snapshot, ref entry); ok; ok = Process32Next(snapshot, ref entry)) result[entry.Pid] = entry.Parent;
            }
            finally { CloseHandle(snapshot); }
            return result;
        }

        // A raiz precisa pausar; descendentes negados nao desfazem a pausa e voltam como aviso.
        public static string Suspend(IList<uint> pids) { return Each(pids, SuspendResume, delegate(IntPtr h) { return NtSuspendProcess(h); }, "pausar", true); }
        // Retomar tenta todos: com a raiz ja encerrada, os demais nao podem ficar congelados. So falha se nenhum retomar.
        public static string Resume(IList<uint> pids) { return Each(pids, SuspendResume, delegate(IntPtr h) { return NtResumeProcess(h); }, "retomar", false); }
        public static string Kill(IList<uint> pids) { return Each(pids, Terminate, delegate(IntPtr h) { return TerminateProcess(h, 1) ? 0 : Marshal.GetLastWin32Error(); }, "encerrar", true); }

        // Fechamento normal: o mesmo pedido do botao fechar da janela. O jogo pode salvar ou pedir confirmacao.
        public static void Close(IntPtr window)
        {
            if (!PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero)) throw Denied(Marshal.GetLastWin32Error(), "fechar");
        }
        // Minimizar antes de pausar, como o Nyrna: a janela congelada nao fica na tela nem segura a tela cheia.
        public static void Minimize(IntPtr window) { if (window != IntPtr.Zero) ShowWindowAsync(window, 6); }

        static string Each(IList<uint> pids, uint access, Func<IntPtr, int> action, string verb, bool rootFirst)
        {
            int failed = 0, first = 0;
            for (int i = 0; i < pids.Count; i++)
            {
                IntPtr h = OpenProcess(access, false, pids[i]); int status;
                if (h == IntPtr.Zero) status = Marshal.GetLastWin32Error();
                else try { status = action(h); } finally { CloseHandle(h); }
                if (status == 0) continue;
                if (i == 0 && rootFirst) throw Denied(status, verb);
                if (failed++ == 0) first = status;
            }
            if (pids.Count > 0 && failed == pids.Count) throw Denied(first, verb);
            return failed == 0 ? null : failed + " processo(s) do jogo não puderam " + verb + ".";
        }
        static InvalidOperationException Denied(int status, string verb)
        {
            bool denied = status == 5 || status == unchecked((int)0xC0000022);
            return new InvalidOperationException(denied
                ? "O Windows negou " + verb + " o jogo. Ele pode estar como administrador ou protegido por anti-cheat."
                : "Não foi possível " + verb + " o jogo (código 0x" + status.ToString("X") + ").");
        }
        public static long ProcessCreated(uint pid) { return Created(pid); }
        static long Created(uint pid)
        {
            IntPtr h = OpenProcess(QueryLimited, false, pid); if (h == IntPtr.Zero) return 0;
            try { long created, exit, kernel, user; return GetProcessTimes(h, out created, out exit, out kernel, out user) ? created : 0; }
            finally { CloseHandle(h); }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessEntry
        {
            public uint Size, Usage, Pid; public IntPtr HeapId; public uint ModuleId, Threads, Parent; public int Priority; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string File;
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr handle, out long created, out long exit, out long kernel, out long user);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr handle, uint code);
        [DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr handle);
        [DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr handle);
        [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr hwnd, int command);
    }
}
