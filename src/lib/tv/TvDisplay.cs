// Tela da TV Samsung pela CCD (ADR 0110, porte do Samsung TV Orchestrator): somente leitura. QueryDisplayConfig e
// DisplayConfigGetDeviceInfo (nome do alvo e HDR); nada e configurado. A Samsung e reconhecida pelo fabricante do EDID e
// pela instancia do conector: o codigo de produto alterna entre 0F13 e 0F14 no mesmo aparelho (medido em 08/09).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace XGBP.Tv
{
    public enum TvTopology { Unknown, PcScreenOnly, Clone, Extend, ExternalOnly, NoActiveDisplay }

    public sealed class TvDisplayTarget
    {
        public string FriendlyName = "", DevicePath = "", OutputTechnology = "", EdidVendor = "";
        public int AdapterHigh; public uint AdapterLow, TargetId, SourceId;
        public ushort EdidProductCode;
        public bool Active, Available;
        public double RefreshHz; public uint Width, Height;
        public string AdapterKey { get { return AdapterHigh + ":" + AdapterLow; } }
        public string ShortId { get { return EdidVendor + EdidProductCode.ToString("X4"); } }
    }

    public sealed class TvDisplayState
    {
        public TvTopology Topology = TvTopology.Unknown;
        public bool SamsungFound, SamsungActive, Ambiguous, MatchedByEdid;
        public TvDisplayTarget Samsung;
        public int ActiveCount;
        public bool? HdrSupported, HdrEnabled;
        public string HdrError, Error;
        public bool Valid { get { return Error == null && !Ambiguous && Topology != TvTopology.Unknown; } }
        // Sem caminho de dispositivo nem nomes: so o que identifica o tipo de tela.
        public string Describe()
        {
            var s = new StringBuilder("topologia=" + Topology + "; ativas=" + ActiveCount);
            if (Ambiguous) s.Append("; samsung=ambigua");
            else if (Samsung == null) s.Append("; samsung=nao encontrada");
            else
            {
                s.Append("; samsung=" + (SamsungActive ? "ativa" : "inativa") + " (" + Samsung.ShortId + ", " + Samsung.OutputTechnology +
                    (MatchedByEdid ? ", pelo EDID" : ", pelo conector configurado"));
                if (SamsungActive && Samsung.Width > 0) s.Append(", " + Samsung.Width + "x" + Samsung.Height + " a " + Math.Round(Samsung.RefreshHz) + " Hz");
                s.Append(")");
            }
            if (HdrSupported.HasValue) s.Append("; hdr=" + (HdrSupported.Value ? (HdrEnabled == true ? "ligado" : "desligado") : "sem suporte"));
            else if (HdrError != null) s.Append("; hdr=" + HdrError);
            if (Error != null) s.Append("; erro=" + Error);
            return s.ToString();
        }
    }

    public static class TvDisplay
    {
        static readonly Regex Stable = new Regex(@"DISPLAY[#\\]([A-Za-z]{3})[0-9A-Fa-f]{4}[#\\]([^#\\]+)", RegexOptions.IgnoreCase);

        // Mesmo criterio do Orchestrator: fabricante e instancia do conector iguais; o codigo de produto e ignorado.
        public static bool Matches(string fragment, string path)
        {
            if (string.IsNullOrEmpty(fragment) || string.IsNullOrEmpty(path)) return false;
            Match a = Stable.Match(fragment), b = Stable.Match(path);
            if (!a.Success || !b.Success) return string.Equals(path.TrimStart('\\', '?'), fragment.TrimStart('\\', '?'), StringComparison.OrdinalIgnoreCase);
            return string.Equals(a.Groups[1].Value, b.Groups[1].Value, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.Groups[2].Value, b.Groups[2].Value, StringComparison.OrdinalIgnoreCase);
        }

        // Sem conector configurado, aceita uma unica Samsung na HDMI (como o discover do Orchestrator); mais de uma e ambigua.
        public static TvDisplayState Classify(IList<TvDisplayTarget> all, string fragment)
        {
            var state = new TvDisplayState();
            var active = new List<TvDisplayTarget>(); var seen = new HashSet<string>();
            foreach (var t in all) if (t.Active && seen.Add(t.AdapterKey + "/" + t.TargetId)) active.Add(t);
            state.ActiveCount = active.Count;
            bool byEdid = string.IsNullOrEmpty(fragment);
            var groups = new Dictionary<string, TvDisplayTarget>();
            foreach (var t in all)
            {
                bool match = byEdid ? t.EdidVendor == "SAM" && t.OutputTechnology == "HDMI" : Matches(fragment, t.DevicePath);
                if (!match) continue;
                string key = t.AdapterKey + "/" + t.TargetId; TvDisplayTarget prior;
                if (!groups.TryGetValue(key, out prior) || t.Active && !prior.Active) groups[key] = t;
            }
            if (groups.Count > 1) { state.Ambiguous = true; return state; }
            foreach (var t in groups.Values) state.Samsung = t;
            state.SamsungFound = state.Samsung != null; state.MatchedByEdid = byEdid && state.SamsungFound;
            state.SamsungActive = state.SamsungFound && state.Samsung.Active;
            if (!state.SamsungActive) { state.Topology = active.Count == 0 ? TvTopology.NoActiveDisplay : TvTopology.PcScreenOnly; return state; }
            var samsung = state.Samsung; bool other = false, clone = false;
            foreach (var t in active)
            {
                if (t.AdapterKey == samsung.AdapterKey && t.TargetId == samsung.TargetId) continue;
                other = true; if (t.AdapterKey == samsung.AdapterKey && t.SourceId == samsung.SourceId) clone = true;
            }
            state.Topology = !other ? TvTopology.ExternalOnly : clone ? TvTopology.Clone : TvTopology.Extend;
            return state;
        }

        public static TvDisplayState Read(string fragment)
        {
            string error;
            var targets = ReadTargets(out error);
            if (error != null) return new TvDisplayState { Error = error };
            var state = Classify(targets, fragment);
            if (state.SamsungActive) ReadHdr(state);
            return state;
        }

        static void ReadHdr(TvDisplayState state)
        {
            var t = state.Samsung;
            var info = new AdvancedColorInfo { header = Header(GetAdvancedColorInfo, typeof(AdvancedColorInfo), t.AdapterLow, t.AdapterHigh, t.TargetId) };
            int rc = DisplayConfigGetDeviceInfo(ref info);
            if (rc != 0) { state.HdrError = "consulta rc=" + rc; return; }
            state.HdrSupported = (info.value & 1) != 0; state.HdrEnabled = (info.value & 2) != 0;
        }

        public static string DecodeEdidVendor(ushort raw)
        {
            if (raw == 0) return "";
            int be = ((raw >> 8) | (raw << 8)) & 0xFFFF;
            var c = new[] { (char)('A' - 1 + ((be >> 10) & 0x1F)), (char)('A' - 1 + ((be >> 5) & 0x1F)), (char)('A' - 1 + (be & 0x1F)) };
            foreach (var ch in c) if (ch < 'A' || ch > 'Z') return "";
            return new string(c);
        }

        public static string OutputName(uint v)
        {
            switch (v)
            {
                case 0: return "VGA"; case 4: return "DVI"; case 5: return "HDMI"; case 6: return "LVDS";
                case 10: return "DisplayPort"; case 11: return "DisplayPort interno"; case 15: return "Miracast";
                case 16: case 17: return "Indireta"; case 0x80000000: return "Interna";
            }
            return "0x" + v.ToString("X");
        }

        static List<TvDisplayTarget> ReadTargets(out string error)
        {
            error = null; var result = new List<TvDisplayTarget>();
            try
            {
                uint pathCount = 0, modeCount = 0; PathInfo[] paths = new PathInfo[0]; ModeInfo[] modes = new ModeInfo[0];
                int rc = 122;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    rc = GetDisplayConfigBufferSizes(QdcAllPaths, out pathCount, out modeCount);
                    if (rc != 0) { error = "CCD tamanhos rc=" + rc; return result; }
                    if (pathCount > 4096 || modeCount > 8192) { error = "CCD devolveu tamanho invalido"; return result; }
                    paths = new PathInfo[pathCount]; modes = new ModeInfo[modeCount];
                    rc = QueryDisplayConfig(QdcAllPaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                    if (rc != 122) break; // ERROR_INSUFFICIENT_BUFFER: a topologia mudou entre as duas chamadas
                }
                if (rc != 0) { error = "CCD consulta rc=" + rc; return result; }
                for (int i = 0; i < pathCount && i < paths.Length; i++)
                {
                    var p = paths[i];
                    bool active = (p.flags & 1) != 0, available = p.targetInfo.targetAvailable != 0;
                    if (!active && !available) continue;
                    var name = new TargetDeviceName { header = Header(GetTargetName, typeof(TargetDeviceName), p.targetInfo.adapterLow,
                        p.targetInfo.adapterHigh,
                        p.targetInfo.id) };
                    int nameRc = DisplayConfigGetDeviceInfo(ref name);
                    if (nameRc != 0 || string.IsNullOrEmpty(name.monitorDevicePath))
                    {
                        if (active) { error = "identificacao de tela ativa falhou rc=" + nameRc; return result; }
                        continue;
                    }
                    var t = new TvDisplayTarget {
                        FriendlyName = name.monitorFriendlyDeviceName ?? "", DevicePath = name.monitorDevicePath,
                        AdapterHigh = p.targetInfo.adapterHigh, AdapterLow = p.targetInfo.adapterLow, TargetId = p.targetInfo.id, SourceId = p.sourceInfo.id,
                    OutputTechnology = OutputName(name.outputTechnology), EdidVendor = DecodeEdidVendor(name.edidManufactureId), EdidProductCode
                        = name.edidProductCodeId,
                        Active = active, Available = available,
                        RefreshHz = p.targetInfo.refreshDenominator == 0 ? 0 : (double)p.targetInfo.refreshNumerator / p.targetInfo.refreshDenominator };
                    uint mode = p.sourceInfo.modeInfoIdx;
                if (active && mode < modeCount && mode < modes.Length && modes[mode].infoType == 1) { t.Width = modes[mode].width; t.Height =
                    modes[mode].height; }
                    result.Add(t);
                }
            }
            catch (Exception ex) { error = "falha CCD: " + ex.GetType().Name; }
            return result;
        }

        static DeviceInfoHeader Header(uint type, Type layout, uint adapterLow, int adapterHigh, uint id)
        { return new DeviceInfoHeader { type = type, size = (uint)Marshal.SizeOf(layout), adapterLow = adapterLow, adapterHigh = adapterHigh, id = id }; }

        const uint QdcAllPaths = 1, GetTargetName = 2, GetAdvancedColorInfo = 9;

    [StructLayout(LayoutKind.Sequential)] struct PathSourceInfo { public uint adapterLow; public int adapterHigh; public uint id, modeInfoIdx, statusFlags; }
        [StructLayout(LayoutKind.Sequential)] struct PathTargetInfo
        {
            public uint adapterLow; public int adapterHigh; public uint id, modeInfoIdx, outputTechnology, rotation, scaling;
            public uint refreshNumerator, refreshDenominator, scanLineOrdering; public int targetAvailable; public uint statusFlags;
        }
        [StructLayout(LayoutKind.Sequential)] struct PathInfo { public PathSourceInfo sourceInfo; public PathTargetInfo targetInfo; public uint flags; }
        // DISPLAYCONFIG_MODE_INFO (64 bytes); so o modo de origem (infoType 1) e lido: largura e altura no inicio da uniao.
        [StructLayout(LayoutKind.Explicit, Size = 64)] struct ModeInfo
        {
            [FieldOffset(0)] public uint infoType; [FieldOffset(4)] public uint id;
            [FieldOffset(8)] public uint adapterLow; [FieldOffset(12)] public int adapterHigh;
            [FieldOffset(16)] public uint width; [FieldOffset(20)] public uint height;
        }
        [StructLayout(LayoutKind.Sequential)] struct DeviceInfoHeader { public uint type, size, adapterLow; public int adapterHigh; public uint id; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct TargetDeviceName
        {
        public DeviceInfoHeader header; public uint flags, outputTechnology; public ushort edidManufactureId, edidProductCodeId; public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }
        [StructLayout(LayoutKind.Sequential)] struct AdvancedColorInfo { public DeviceInfoHeader header; public uint value; public int
            colorEncoding; public uint
            bitsPerColorChannel; }

        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths, ref uint modeCount, [Out]
            ModeInfo[] modes, IntPtr topology);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName request);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref AdvancedColorInfo request);
    }
}
