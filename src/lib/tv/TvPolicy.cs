// Politica e contratos do coordenador. Eventos reais de energia/HID so serao ligados na fase 5.
using System;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public enum TvMode { Running, Disabled, Suspending, Stopping, Faulted }
    public enum TvRequestKind { PowerOn, PowerOff, DisplayChanged, NetworkRecovered, Resume, Suspend, Enable,
        HdrPreference, HdrOn, HdrOff, ConfirmOn, ConfirmOff, Forget, Pair, Observe, Startup, Stop, DualSenseVerified, SessionEnding, Options, CancelSuspend }
    public sealed class TvRequest
    {
        public readonly Guid Id;
        public readonly TvRequestKind Kind;
        public readonly bool Value;
        public readonly string Source;
        readonly TvCoreConfig options;
        readonly string optionsKey;
        public TvCoreConfig Options { get { return options == null ? null : options.Copy(); } }
        public TvRequest(Guid id, TvRequestKind kind, bool value = false, string source = "user", TvCoreConfig options = null)
        { Id = id; Kind = kind; Value = value; Source = source; this.options = options == null ? null : options.Copy();
            optionsKey = options == null ? null : TvJson.Serialize(this.options); }
        public bool Same(TvRequest other) { return Id == other.Id && Kind == other.Kind && Value == other.Value && Source == other.Source && optionsKey ==
            other.optionsKey; }
    }
    public sealed class TvReply
    {
        public readonly Guid Id;
        public readonly TvOutcome Outcome;
        public readonly string Detail;
        public readonly bool Completed;
        public readonly DeliveryStatus Delivery;
        public TvReply(Guid id, TvOutcome outcome, string detail, bool completed = true, DeliveryStatus delivery = DeliveryStatus.NotSent)
        { Id = id; Outcome = outcome; Detail = detail; Completed = completed; Delivery = delivery; }
    }
    public sealed class TvTicket
    {
        public readonly TvReply Receipt;
        public readonly Task<TvReply> Completion;
        public TvTicket(TvReply receipt, Task<TvReply> completion) { Receipt = receipt; Completion = completion; }
    }
    public sealed class TvRuntime
    {
        public readonly TvMode Mode;
        public readonly bool Enabled, ManualOverride, HasAuthority, VerifiedPs;
        public readonly DesiredTvState Desired;
        public readonly PowerSnapshot Power;
        public readonly EnsureResult LastResult;
        public readonly string PersistenceError;
        public TvRuntime(TvMode mode, bool enabled, bool manualOverride, bool authority, bool ps,
            DesiredTvState desired, PowerSnapshot power, EnsureResult result, string error)
        { Mode = mode; Enabled = enabled; ManualOverride = manualOverride; HasAuthority = authority; VerifiedPs = ps;
            Desired = desired; Power = power; LastResult = result; PersistenceError = error; }
    }
    public interface ITvCoreDisplay
    {
        TvDisplayState Read();
        bool SetHdr(bool enabled);
    }
    public static class TvPolicy
    {
        public static DesiredTvState Action(TvAction action)
        { return action == TvAction.On ? DesiredTvState.On : action == TvAction.Off ? DesiredTvState.Off : DesiredTvState.Keep; }
        public static DesiredTvState Topology(TvCoreConfig config, TvDisplayState topology)
        {
            if (topology == null || !topology.Valid || topology.Topology == TvTopology.NoActiveDisplay) return DesiredTvState.Keep;
            return Action(!topology.SamsungActive ? config.PcOnly : topology.Topology == TvTopology.Extend ? config.Extend :
                topology.Topology == TvTopology.ExternalOnly ? config.External : topology.Topology == TvTopology.Clone ? config.Clone : TvAction.Keep);
        }
        public static int Priority(TvRequestKind k)
        {
            switch (k)
            {
                case TvRequestKind.Stop: return 1000;
                case TvRequestKind.SessionEnding: return 950;
                case TvRequestKind.Suspend: return 900;
                case TvRequestKind.Enable: return 850;
                case TvRequestKind.ConfirmOn: case TvRequestKind.ConfirmOff: case TvRequestKind.Forget: return 800;
                case TvRequestKind.Pair: return 700;
                case TvRequestKind.Resume: return 600;
                case TvRequestKind.PowerOn: case TvRequestKind.PowerOff: case TvRequestKind.DualSenseVerified: return 500;
                case TvRequestKind.HdrPreference: case TvRequestKind.HdrOn: case TvRequestKind.HdrOff: return 400;
                default: return 100;
            }
        }
        public static bool Critical(TvRequest r)
        { return r.Kind == TvRequestKind.Stop || r.Kind == TvRequestKind.SessionEnding || r.Kind == TvRequestKind.Suspend || r.Kind == TvRequestKind.Enable &&
            !r.Value; }
        public static bool EpochBound(TvRequestKind k)
        { return k == TvRequestKind.PowerOn || k == TvRequestKind.PowerOff || k == TvRequestKind.DualSenseVerified || k == TvRequestKind.DisplayChanged || k ==
            TvRequestKind.NetworkRecovered; }
        public static bool Advances(TvRequestKind k)
        { return EpochBound(k) && k != TvRequestKind.DisplayChanged && k != TvRequestKind.NetworkRecovered || k == TvRequestKind.Enable || k ==
            TvRequestKind.Resume; }
    }
}
