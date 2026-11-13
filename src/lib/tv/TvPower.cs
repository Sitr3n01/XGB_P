// Porte do modelo de energia do Orchestrator 32640a6 (ADR 0110). Autoridade nunca vem da rede.
using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public enum TvState { Unknown, Off, Waking, On, Sleeping, Unreachable, AuthRequired }
    public enum Reachability { Unknown, Reachable, Unreachable }
    public enum DesiredTvState { Keep, On, Off }
    public enum TvReason { Startup, Display, SystemSuspend, SystemResume, DualSense, Manual, Recovery }
    public enum EvidenceSource { None, UserConfirmed, DeviceReported, NetworkInference, CommandUnconfirmed }
    public enum DeliveryStatus { NotSent, PossiblySent, Sent }
    public enum TvOutcome { Confirmed, Unconfirmed, NoAction, BlockedAmbiguous, Cancelled, Disabled, StaleIntent,
        Unreachable, AuthRequired, IdentityMismatch, PersistenceFailure, NotSent, PossiblySent, Busy, InvalidCommand, Accepted, QueryFailed }

    [DataContract] public sealed class PowerSnapshot
    {
        [DataMember] public TvState State { get; private set; }
        [DataMember] public EvidenceSource Source { get; private set; }
        [DataMember] public DateTimeOffset At { get; private set; }
        [DataMember] public string Evidence { get; private set; }
        [DataMember] public string AuthorityId { get; private set; }
        [DataMember] public DesiredTvState PendingDesired { get; private set; }
        public PowerSnapshot(TvState state, EvidenceSource source, DateTimeOffset at, string evidence, string id,
            DesiredTvState pending = DesiredTvState.Keep)
        { State = state; Source = source; At = at; Evidence = evidence; AuthorityId = id; PendingDesired = pending; }
        public static PowerSnapshot Unknown { get { return new PowerSnapshot(TvState.Unknown, EvidenceSource.None,
            DateTimeOffset.MinValue, "Estado físico não confirmado.", null); } }
        public bool Authoritative(DateTimeOffset now, TimeSpan age)
        {
            return (State == TvState.On || State == TvState.Off) &&
                (Source == EvidenceSource.UserConfirmed || Source == EvidenceSource.DeviceReported) &&
                !string.IsNullOrEmpty(AuthorityId) && now >= At && now - At <= age;
        }
    }
    public sealed class TvObservation
    {
        public readonly Reachability Net;
        public readonly string Evidence;
        public readonly bool IdentityVerified;
        public TvObservation(Reachability net, string evidence, bool verified = false)
        { Net = net; Evidence = evidence; IdentityVerified = verified; }
    }
    public sealed class EnsureResult
    {
        public readonly TvOutcome Outcome;
        public readonly TvState Final;
        public readonly string Detail;
        public readonly DeliveryStatus Delivery;
        public bool Success { get { return Outcome == TvOutcome.Confirmed; } }
        public EnsureResult(TvOutcome outcome, TvState final, string detail, DeliveryStatus delivery = DeliveryStatus.NotSent)
        { Outcome = outcome; Final = final; Detail = detail; Delivery = delivery; }
    }
    public sealed class NetworkObservation
    {
        public readonly TvNetwork Status;
        public readonly TvState? ReportedPower;
        public readonly string Detail;
        public NetworkObservation(TvNetwork status, TvState? power = null, string detail = "Identidade conferida; energia desconhecida.")
        { Status = status; ReportedPower = power; Detail = detail; }
    }
    public sealed class SendResult
    {
        public readonly DeliveryStatus Delivery;
        public readonly TvOutcome Outcome;
        public readonly string Detail;
        public SendResult(DeliveryStatus delivery, TvOutcome outcome, string detail)
        { Delivery = delivery; Outcome = outcome; Detail = detail; }
    }
    public sealed class WakeResult
    {
        public readonly int Packets;
        public readonly bool HasRoute;
        public readonly string Detail;
        public WakeResult(int packets, bool route, string detail) { Packets = packets; HasRoute = route; Detail = detail; }
    }
    public sealed class PairResult
    {
        public readonly bool Authorized;
        public readonly string Detail;
        public PairResult(bool authorized, string detail) { Authorized = authorized; Detail = detail; }
    }
    public interface ITvClock
    {
        DateTimeOffset UtcNow { get; }
        long Timestamp { get; }
        TimeSpan Elapsed(long start);
        Task Delay(TimeSpan duration, CancellationToken ct);
    }
    public sealed class TvClock : ITvClock
    {
        public DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }
        public long Timestamp { get { return Stopwatch.GetTimestamp(); } }
        public TimeSpan Elapsed(long start) { return TimeSpan.FromSeconds((double)(Timestamp - start) / Stopwatch.Frequency); }
        public Task Delay(TimeSpan duration, CancellationToken ct) { return Task.Delay(duration, ct); }
    }
    public sealed class TvDeadline
    {
        readonly ITvClock clock;
        readonly long start;
        readonly TimeSpan budget;
        public TvDeadline(ITvClock clock, TimeSpan budget) { this.clock = clock; start = clock.Timestamp; this.budget = budget; }
        public TimeSpan Remaining { get { return TimeSpan.FromTicks(Math.Max(0, (budget - clock.Elapsed(start)).Ticks)); } }
        public bool Expired { get { return Remaining == TimeSpan.Zero; } }
        public TimeSpan UpTo(double seconds) { return Remaining < TimeSpan.FromSeconds(seconds) ? Remaining : TimeSpan.FromSeconds(seconds); }
    }
    public interface ITvTransport
    {
        bool ChannelReady { get; }
        Task<NetworkObservation> ObserveAsync(TvCoreConfig config, TimeSpan budget, bool rediscover, CancellationToken ct);
        Task<SendResult> SendKeyAsync(TvCoreConfig config, string key, TimeSpan budget, bool handshake, CancellationToken ct);
        Task<WakeResult> WakeAsync(TvCoreConfig config, CancellationToken ct);
        Task<PairResult> PairAsync(TvCoreConfig config, bool replaceCertificate, CancellationToken ct);
        Task PrepareAsync(TvCoreConfig config, CancellationToken ct);
        Task CloseAsync();
    }
}
