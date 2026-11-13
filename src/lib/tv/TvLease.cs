// Mesmo nome de mutex do Orchestrator. Thread propria preserva a afinidade de posse entre awaits.
using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace XGBP.Tv
{
    public sealed class TvLease : IDisposable
    {
        readonly ManualResetEvent ready = new ManualResetEvent(false), stop = new ManualResetEvent(false);
        readonly Thread holder;
        int disposed;
        public string TargetName { get; private set; }
        public bool Owned { get; private set; }
        public string Error { get; private set; }
        public static string Name(string mac)
        {
            if (TvWake.ParseMac(mac) == null) throw new ArgumentException("Alvo inválido para exclusão.");
            return @"Global\SamsungTvOrchestrator." + TvJson.Hash("target:" + mac.Replace(":", "").Replace("-", "").Replace(".", "").ToUpperInvariant());
        }
        public TvLease(string mac)
        {
            string name = Name(mac);
            TargetName = name;
            holder = new Thread(delegate()
            {
                bool owned = false;
                try
                {
                    var security = new MutexSecurity();
                    security.AddAccessRule(new MutexAccessRule(WindowsIdentity.GetCurrent().User, MutexRights.FullControl, AccessControlType.Allow));
                    bool created;
                    using (var mutex = new Mutex(false, name, out created, security))
                    {
                        try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                        Owned = owned; ready.Set();
                        if (owned) { stop.WaitOne(); mutex.ReleaseMutex(); }
                    }
                }
                catch (Exception ex) { Error = ex.GetType().Name; ready.Set(); }
            });
            holder.IsBackground = true; holder.Name = "XGBP TV lease"; holder.Start(); ready.WaitOne();
        }
        public void Dispose()
        { if (Interlocked.Exchange(ref disposed, 1) == 0) { stop.Set(); holder.Join(); Owned = false; ready.Dispose(); stop.Dispose(); } }
    }
}
