// Configuracao propria do nucleo. Importacao e ativacao sao explicitas, sem uso no residente na fase 3.
using System;
using System.Linq;
using System.Runtime.Serialization;

namespace XGBP.Tv
{
    public enum TvAction { Keep, On, Off }
    public enum TvResume { RestoreIntent, Keep, Off }
    [DataContract] public sealed class TvCoreConfig
    {
        [DataMember] public int SchemaVersion = 1;
        [DataMember] public bool Enabled;
        [DataMember] public string Mac = "", Duid = "", Ip = "", Model = "", Fragment = "";
        [DataMember] public string ClientName = "SamsungTvOrchestrator";
        [DataMember] public bool KeepHdrOff;
        [DataMember] public bool PsWakeEnabled = true, TopologyEnabled = true;
        [DataMember] public bool ImplicitSuspendConfirmation, WakeSoundEnabled;
        [DataMember] public TvAction Extend = TvAction.On, External = TvAction.On, Clone = TvAction.On, PcOnly = TvAction.Keep;
        [DataMember] public TvAction Suspend = TvAction.Off, DualSense = TvAction.On;
        [DataMember] public TvResume Resume = TvResume.RestoreIntent;
        [DataMember] public int ConfirmedSeconds = 120, WakeSeconds = 240, DebounceMs = 500;
        [DataMember] public bool WakeRetry = true;
        [DataMember] public int[] BackoffSeconds = { 2, 4, 7, 11, 16, 25 }, BurstDelaysMs = { 0, 300, 600 };
        public TvCoreConfig Copy()
        {
            var copy = (TvCoreConfig)MemberwiseClone();
            copy.BackoffSeconds = BackoffSeconds == null ? null : (int[])BackoffSeconds.Clone();
            copy.BurstDelaysMs = BurstDelaysMs == null ? null : (int[])BurstDelaysMs.Clone();
            return copy;
        }
        public void Validate()
        {
            if (SchemaVersion != 1 || TvWake.ParseMac(Mac) == null || string.IsNullOrWhiteSpace(Duid) || Duid.Length > 200 ||
                Ip == null || Ip.Length > 0 && !TvProbe.IsPrivateIPv4(Ip) || Model == null || Model.Length > 120 ||
                Fragment == null || Fragment.Length > 260 || string.IsNullOrEmpty(ClientName) || ClientName.Length > 80)
                throw new ArgumentException("Configuração ou identidade da TV inválida.");
            foreach (var a in new[] { Extend, External, Clone, PcOnly, Suspend, DualSense })
                if (!Enum.IsDefined(typeof(TvAction), a)) throw new ArgumentException("Política de energia inválida.");
            if (Suspend == TvAction.On || !Enum.IsDefined(typeof(TvResume), Resume) || ConfirmedSeconds < 5 || ConfirmedSeconds > 120 ||
                WakeSeconds < 10 || WakeSeconds > 300 || DebounceMs < 50 || DebounceMs > 3000 ||
                BackoffSeconds == null || BackoffSeconds.Length < 1 || BackoffSeconds.Length > 10 || BackoffSeconds.Any(x => x < 1 || x > 30) ||
                BurstDelaysMs == null || BurstDelaysMs.Length < 1 || BurstDelaysMs.Length > 5 || BurstDelaysMs.Any(x => x < 0 || x > 2000))
                throw new ArgumentException("Limites de confiabilidade da TV inválidos.");
        }
        // Preferencias existentes sao importadas, mas a automacao exige ativacao posterior e explicita.
        public static TvCoreConfig Import(TvSetupState setup)
        {
            if (setup == null || !setup.Found) throw new ArgumentException("Configuração de origem ausente.");
            var c = new TvCoreConfig { Mac = setup.Mac, Duid = setup.Duid, Ip = setup.Ip, Model = setup.Model,
                Fragment = setup.Fragment, KeepHdrOff = setup.KeepHdrOff };
            if (setup.Extend >= 0) c.Extend = (TvAction)setup.Extend;
            if (setup.External >= 0) c.External = (TvAction)setup.External;
            if (setup.Clone >= 0) c.Clone = (TvAction)setup.Clone;
            if (setup.PcOnly >= 0) c.PcOnly = (TvAction)setup.PcOnly;
            if (setup.Suspend >= 0) c.Suspend = (TvAction)setup.Suspend;
            if (setup.DualSense >= 0) c.DualSense = (TvAction)setup.DualSense;
            if (setup.Resume >= 0) c.Resume = (TvResume)setup.Resume;
            c.Validate(); return c;
        }
        public static TvCoreConfig ImportJson(string json)
        {
            var c = Import(TvSetup.Parse(json, null));
            var document = TvJson.Parse<TvImportDocument>(json);
            if (document == null || document.DualSenseDisplayBehavior.GetValueOrDefault() != 0 ||
                document.Tv != null && document.Tv.PowerOffKey != null && document.Tv.PowerOffKey != "KEY_POWER")
                throw new ArgumentException("Perfil de origem não suportado pelo núcleo da TV.");
            var r = document.Reliability;
            if (r != null)
            {
                if (r.ContinuousEnforcement == true) throw new ArgumentException("Imposição contínua não é suportada.");
                c.ConfirmedSeconds = r.ConfirmedStateMaxAgeSeconds ?? c.ConfirmedSeconds;
                c.WakeSeconds = r.WakeDeadlineSeconds ?? c.WakeSeconds; c.DebounceMs = r.DebounceMs ?? c.DebounceMs;
                c.WakeRetry = r.WakeRetryEnabled ?? c.WakeRetry;
                c.BackoffSeconds = r.ProbeBackoffSeconds ?? c.BackoffSeconds; c.BurstDelaysMs = r.WakeBurstDelaysMs ?? c.BurstDelaysMs;
                if (r.WakeBurstCount.HasValue && r.WakeBurstCount != c.BurstDelaysMs.Length) throw new ArgumentException("Quantidade de rajadas incompatível.");
            }
            c.Validate(); return c;
        }
    }
    [DataContract] public sealed class TvImportDocument
    {
        [DataMember] public TvImportReliability Reliability;
        [DataMember] public TvImportProfile Tv;
        [DataMember] public int? DualSenseDisplayBehavior;
    }
    [DataContract] public sealed class TvImportProfile { [DataMember] public string PowerOffKey; }
    [DataContract] public sealed class TvImportReliability
    {
        [DataMember] public bool? WakeRetryEnabled, ContinuousEnforcement;
        [DataMember] public int? ConfirmedStateMaxAgeSeconds, WakeDeadlineSeconds, DebounceMs, WakeBurstCount;
        [DataMember] public int[] ProbeBackoffSeconds, WakeBurstDelaysMs;
    }
}
