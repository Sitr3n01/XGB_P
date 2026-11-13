// Configuracao da TV importada do Samsung TV Orchestrator (ADR 0110): somente leitura de
// %LOCALAPPDATA%\SamsungTvOrchestrator\config.json e do formato de tizen-token.json. O token nunca e decifrado nem guardado:
// so o formato (v2 com DPAPI ou antigo) e se a credencial pertence a mesma TV. Nada e escrito na pasta do Orchestrator.
// Processos iniciados por um app empacotado (agente) leem a copia virtualizada dessa pasta, nao a real.
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace XGBP.Tv
{
    public enum TvCredential { Missing, Protected, Legacy, OtherTv, Invalid }

    public sealed class TvSetupState
    {
        public bool Found;
        public string Error;
        public int Schema;              // 0 = arquivo sem SchemaVersion (formato de 08/09)
        public bool? Enabled;           // interruptor geral do Orchestrator
        public string Model = "", Mac = "", Duid = "", Ip = "", Fragment = "";
        public bool KeepHdrOff;
        public int Extend = -1, External = -1, Clone = -1, PcOnly = -1, Suspend = -1, Resume = -1, DualSense = -1;
        public TvCredential Credential = TvCredential.Missing;
        public bool HasTarget { get { return TvProbe.ParseMac(Mac) != null && Duid.Length > 0; } }
        // Sem MAC, DUID ou IP: so se estao definidos.
        public string Describe()
        {
            if (!Found) return "configuracao=" + (Error ?? "ausente");
            return "configuracao=v" + Schema + "; modelo=" + (Model.Length > 0 ? Model : "?") + "; alvo=" + (HasTarget ? "definido" : "incompleto") +
                "; ip=" + (TvProbe.IsPrivateIPv4(Ip) ? "definido" : "ausente") + "; conector=" + (Fragment.Length > 0 ? "definido" : "ausente") +
                "; automacao=" + (Enabled.HasValue ? (Enabled.Value ? "ligada" : "desligada") : "?") + "; hdr desligado=" + KeepHdrOff +
                "; credencial=" + Credential;
        }
    }

    [DataContract] public sealed class OrchestratorConfig
    {
        [DataMember(Name = "SchemaVersion")] public int? SchemaVersion;
        [DataMember(Name = "Enabled")] public bool? Enabled;
        [DataMember(Name = "Tv")] public OrchestratorTv Tv;
        [DataMember(Name = "Behavior")] public OrchestratorBehavior Behavior;
        [DataMember(Name = "Display")] public OrchestratorDisplay Display;
    }
    [DataContract] public sealed class OrchestratorTv
    {
        [DataMember(Name = "Model")] public string Model;
        [DataMember(Name = "Mac")] public string Mac;
        [DataMember(Name = "Duid")] public string Duid;
        [DataMember(Name = "LastKnownIp")] public string LastKnownIp;
        [DataMember(Name = "MonitorDevicePathFragment")] public string MonitorDevicePathFragment;
    }
    // Enumeracoes gravadas como numero (System.Text.Json sem conversor): Keep 0, On 1, Off 2; Resume: RestoreIntent 0, Keep 1, Off 2.
    [DataContract] public sealed class OrchestratorBehavior
    {
        [DataMember(Name = "Extend")] public int? Extend;
        [DataMember(Name = "External")] public int? External;
        [DataMember(Name = "Clone")] public int? Clone;
        [DataMember(Name = "PcOnly")] public int? PcOnly;
        [DataMember(Name = "Suspend")] public int? Suspend;
        [DataMember(Name = "Resume")] public int? Resume;
        [DataMember(Name = "DualSense")] public int? DualSense;
    }
    [DataContract] public sealed class OrchestratorDisplay { [DataMember(Name = "KeepSamsungHdrOff")] public bool? KeepSamsungHdrOff; }
    // Credencial: so os campos que nao sao segredo. Ciphertext/Token nao sao declarados.
    [DataContract] public sealed class OrchestratorCredential
    {
        [DataMember(Name = "SchemaVersion")] public int? SchemaVersion;
        [DataMember(Name = "CertificateSha256")] public string Certificate;
        [DataMember(Name = "Mac")] public string Mac;
        [DataMember(Name = "Duid")] public string Duid;
    }

    public static class TvSetup
    {
        const int MaxBytes = 1000000;
        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SamsungTvOrchestrator"); }
        }

        public static TvSetupState Read() { return Read(Folder); }
        public static TvSetupState Read(string folder)
        {
            string config = ReadText(Path.Combine(folder, "config.json")), token = ReadText(Path.Combine(folder, "tizen-token.json"));
            return Parse(config, token);
        }

        // null = arquivo ausente. Arquivo grande demais ou ilegivel vira texto vazio (invalido), sem excecao.
        static string ReadText(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length > MaxBytes) return "";
                    using (var reader = new StreamReader(fs, Encoding.UTF8)) return reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        public static TvSetupState Parse(string configJson, string credentialJson)
        {
            var state = new TvSetupState();
            if (configJson == null) { state.Error = "ausente"; return state; }
            var cfg = Deserialize<OrchestratorConfig>(configJson);
            if (cfg == null || cfg.Tv == null) { state.Error = "ilegivel"; return state; }
            state.Found = true;
            state.Schema = cfg.SchemaVersion ?? 0; state.Enabled = cfg.Enabled;
            state.Model = cfg.Tv.Model ?? ""; state.Mac = cfg.Tv.Mac ?? ""; state.Duid = cfg.Tv.Duid ?? "";
            state.Ip = cfg.Tv.LastKnownIp ?? ""; state.Fragment = cfg.Tv.MonitorDevicePathFragment ?? "";
            state.KeepHdrOff = cfg.Display != null && cfg.Display.KeepSamsungHdrOff == true;
            var b = cfg.Behavior;
            if (b != null)
            {
                state.Extend = b.Extend ?? -1; state.External = b.External ?? -1; state.Clone = b.Clone ?? -1; state.PcOnly = b.PcOnly ?? -1;
                state.Suspend = b.Suspend ?? -1; state.Resume = b.Resume ?? -1; state.DualSense = b.DualSense ?? -1;
            }
            state.Credential = Classify(credentialJson, state);
            return state;
        }

        static TvCredential Classify(string json, TvSetupState target)
        {
            if (json == null) return TvCredential.Missing;
            var c = Deserialize<OrchestratorCredential>(json);
            if (c == null) return TvCredential.Invalid;
            if (c.SchemaVersion != 2) return TvCredential.Legacy; // token em texto, sem certificado fixado: exige novo pareamento
            if (string.IsNullOrEmpty(c.Certificate) || c.Certificate.Length != 64) return TvCredential.Invalid;
            bool same = string.Equals(c.Mac, target.Mac, StringComparison.OrdinalIgnoreCase) && string.Equals(c.Duid, target.Duid,
                StringComparison.OrdinalIgnoreCase);
            return same ? TvCredential.Protected : TvCredential.OtherTv;
        }

        static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { using (var s = new MemoryStream(Encoding.UTF8.GetBytes(json))) return new DataContractJsonSerializer(typeof(T)).ReadObject(s) as T; }
            catch (Exception) { return null; }
        }
    }
}
