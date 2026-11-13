// PS validado pelo leitor existente: consumido uma vez por ciclo de suspensao; nao infere a origem pelo Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace XGBP.Power
{
    [DataContract] public sealed class ResumeSession
    {
        [DataMember] public DateTimeOffset? SuspendedAt, ResumedAt, PsAt;
        [DataMember] public double? ResumeToPsMs;
        [DataMember] public bool PsConsumed;
        [DataMember] public string CycleId, PsDevice;
        public ResumeSession Copy() { return (ResumeSession)MemberwiseClone(); }
    }
    public sealed class ResumeGesture
    {
        readonly object gate = new object();
        readonly Func<DateTimeOffset> utc;
        readonly Func<long> stamp;
        readonly Func<long, TimeSpan> elapsed;
        readonly HashSet<string> suppress = new HashSet<string>();
        ResumeSession state = new ResumeSession();
        long resumeStamp, quietStamp;
        bool armed, resumed;
        public ResumeGesture() : this(() => DateTimeOffset.UtcNow, Stopwatch.GetTimestamp,
            start => TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency)) { }
        internal ResumeGesture(Func<DateTimeOffset> utc, Func<long> stamp, Func<long, TimeSpan> elapsed)
        { this.utc = utc; this.stamp = stamp; this.elapsed = elapsed; }
        public ResumeSession Snapshot { get { lock (gate) return state.Copy(); } }
        public void Suspend()
        { lock (gate) { state = new ResumeSession { CycleId = Guid.NewGuid().ToString("N"), SuspendedAt = utc() }; armed = true; resumed = false;
            suppress.Clear(); } }
        public void CancelSuspend() { lock (gate) { armed = false; resumed = false; suppress.Clear(); state = new ResumeSession(); } }
        public bool Resume()
        {
            lock (gate)
            {
                if (resumed) return false;
                if (string.IsNullOrEmpty(state.CycleId)) state.CycleId = Guid.NewGuid().ToString("N");
                resumed = true; armed = true; state.ResumedAt = utc(); resumeStamp = stamp();
                if (state.PsAt.HasValue) state.ResumeToPsMs = (state.PsAt.Value - state.ResumedAt.Value).TotalMilliseconds;
                return true;
            }
        }
        public bool Handle(string device, bool down, bool held, DateTimeOffset eventAt, out bool first)
        {
            lock (gate)
            {
                first = false; device = device ?? "";
                if (!down) { if (!suppress.Remove(device)) return false; quietStamp = stamp(); return true; }
                if (suppress.Contains(device)) return true;
                if (held) return false;
                if (state.PsConsumed && (suppress.Count > 0 || elapsed(quietStamp) < TimeSpan.FromMilliseconds(250)))
                { suppress.Add(device); return true; }
                var age = utc() - eventAt;
                if (!armed || state.PsConsumed || age < TimeSpan.Zero || age > TimeSpan.FromSeconds(1) ||
                    state.SuspendedAt.HasValue && eventAt < state.SuspendedAt.Value ||
                    resumed && (elapsed(resumeStamp) < TimeSpan.Zero || elapsed(resumeStamp) > TimeSpan.FromSeconds(20))) return false;
                state.PsAt = eventAt; state.PsConsumed = true; state.PsDevice = device; quietStamp = stamp(); first = true; suppress.Add(device);
                if (resumed) state.ResumeToPsMs = elapsed(resumeStamp).TotalMilliseconds;
                return true;
            }
        }
    }
}
