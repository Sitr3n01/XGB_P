// Identidade imutavel de processo, da observacao ate o handle usado pela acao.
using System;
using System.Collections.Generic;

namespace XGBP.Games
{
    public sealed class ProcessIdentity
    {
        public readonly uint Pid;
        public readonly long Created;
        public ProcessIdentity(uint pid, long created) { Pid = pid; Created = created; }
        public bool Known { get { return Pid != 0 && Created > 0; } }
        public override string ToString() { return Pid + ":" + Created; }
    }

    public enum ProcessAction { Suspend, Resume, Kill }
    public enum ProcessActionState { Applied, Gone, IdentityChanged, Failed, NotAttempted }

    public sealed class ProcessActionItem
    {
        public ProcessIdentity Identity;
        public ProcessActionState State;
        public int NativeError;
    }

    public sealed class ProcessActionResult
    {
        internal string OperationWarning;
        public readonly List<ProcessActionItem> Items = new List<ProcessActionItem>();
        public bool RootApplied { get { return Items.Count > 0 && Items[0].State == ProcessActionState.Applied; } }
        public bool Complete
        {
            get { return Items.TrueForAll(i => i.State == ProcessActionState.Applied || i.State == ProcessActionState.Gone || i.State == ProcessActionState.IdentityChanged); }
        }
        public List<ProcessIdentity> Applied
        { get { return Items.FindAll(i => i.State == ProcessActionState.Applied).ConvertAll(i => i.Identity); } }
        public List<ProcessIdentity> Pending
        { get { return Items.FindAll(i => i.State == ProcessActionState.Failed || i.State == ProcessActionState.NotAttempted).ConvertAll(i => i.Identity); } }
        public string Warning
        {
            get
            {
                if (OperationWarning != null) return OperationWarning;
                int failed = Items.FindAll(i => i.State != ProcessActionState.Applied && i.State != ProcessActionState.Gone).Count;
                return failed == 0 ? null : failed + " processo(s) sem acao confirmada; identidades diferentes foram preservadas.";
            }
        }
        public void RequireRoot()
        {
            if (!RootApplied) throw new InvalidOperationException("O processo do jogo saiu, mudou de identidade ou recusou a acao. Atualize a lista e tente novamente.");
        }
    }
}
