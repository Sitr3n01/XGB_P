// Acoes verificam a criacao no MESMO handle que recebe a chamada nativa.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace XGBP.Games
{
    public static partial class GameControl
    {
        // Ausencia de acesso nao e saida. Confere criacao/termino no mesmo handle, somente leitura.
        public static bool? Exited(ProcessIdentity identity)
        {
            if (identity == null || !identity.Known) return null;
            IntPtr h = OpenProcess(QueryLimited, false, identity.Pid);
            if (h == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                return error == 87 || error == 1168 ? (bool?)true : null;
            }
            try
            {
                long born, exit, kernel, user;
                if (!GetProcessTimes(h, out born, out exit, out kernel, out user)) return null;
                return born != identity.Created || exit != 0;
            }
            finally { CloseHandle(h); }
        }
        public static List<ProcessIdentity> Capture(IEnumerable<uint> pids)
        {
            var result = new List<ProcessIdentity>();
            foreach (uint pid in pids) result.Add(new ProcessIdentity(pid, Created(pid)));
            return result;
        }
        public static List<ProcessIdentity> CheckedTree(IList<ProcessIdentity> roots)
        {
            var valid = new List<uint>();
            foreach (var root in roots)
                if (root.Known && Created(root.Pid) == root.Created) valid.Add(root.Pid);
            if (valid.Count != roots.Count || valid.Count == 0)
                throw new InvalidOperationException("A identidade do processo do jogo mudou ou nao pode ser confirmada.");
            long snapshotStarted = DateTime.UtcNow.ToFileTimeUtc();
            var tree = Capture(Tree(valid));
            var expected = new Dictionary<uint, ProcessIdentity>();
            foreach (var root in roots) expected[root.Pid] = root;
            // Nascido depois do inicio da coleta: pode ser PID reciclado. A proxima acao coleta de novo.
            tree.RemoveAll(p => !expected.ContainsKey(p.Pid) && (!p.Known || p.Created > snapshotStarted));
            // Preservar a identidade observada das raizes; nunca substituir por um PID reciclado.
            for (int i = 0; i < tree.Count; i++)
            {
                ProcessIdentity original;
                if (expected.TryGetValue(tree[i].Pid, out original)) tree[i] = original;
            }
            return tree;
        }
        public static ProcessActionResult Apply(IList<ProcessIdentity> processes, ProcessAction action)
        {
            uint access = (action == ProcessAction.Kill ? Terminate : SuspendResume) | QueryLimited;
            return ApplyChecked(processes, action, pid => OpenProcess(access, false, pid),
                delegate(IntPtr h) { long born, exit, kernel, user; return GetProcessTimes(h, out born, out exit, out kernel, out user) ? born : 0; },
                delegate(IntPtr h) {
                    if (action == ProcessAction.Suspend) return NtSuspendProcess(h);
                    if (action == ProcessAction.Resume) return NtResumeProcess(h);
                    return TerminateProcess(h, 1) ? 0 : Marshal.GetLastWin32Error();
                }, h => CloseHandle(h), () => Marshal.GetLastWin32Error());
        }
        // Substitutos exercitam troca de PID, recusa parcial e fechamento sem operar processos do usuario.
        internal static ProcessActionResult ApplyChecked(IList<ProcessIdentity> processes, ProcessAction action,
            Func<uint, IntPtr> open, Func<IntPtr, long> created, Func<IntPtr, int> invoke,
            Action<IntPtr> close, Func<int> error)
        {
            var result = new ProcessActionResult();
            bool rootRejected = false;
            for (int i = 0; i < processes.Count; i++)
            {
                var identity = processes[i];
                var item = new ProcessActionItem { Identity = identity, State = ProcessActionState.NotAttempted };
                result.Items.Add(item);
                if (rootRejected) continue;
                if (!identity.Known) item.State = ProcessActionState.IdentityChanged;
                else
                {
                    IntPtr h = open(identity.Pid);
                    if (h == IntPtr.Zero)
                    {
                        item.NativeError = error();
                        item.State = item.NativeError == 87 || item.NativeError == 1168 ? ProcessActionState.Gone : ProcessActionState.Failed;
                    }
                    else try
                    {
                        long actual = created(h);
                        if (actual == 0) item.State = ProcessActionState.Failed;
                        else if (actual != identity.Created) item.State = ProcessActionState.IdentityChanged;
                        else
                        {
                            item.NativeError = invoke(h);
                            item.State = item.NativeError == 0 ? ProcessActionState.Applied : ProcessActionState.Failed;
                        }
                    }
                    finally { close(h); }
                }
                if (i == 0 && action != ProcessAction.Resume && item.State != ProcessActionState.Applied) rootRejected = true;
            }
            return result;
        }
    }
}
