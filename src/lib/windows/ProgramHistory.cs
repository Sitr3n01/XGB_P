// Uso dos programas abertos pela pesquisa da barra (ADR 0096): so a chave, a contagem e a ultima abertura.
// Gravacao atomica com .bak; arquivo ilegivel e ignorado e nunca bloqueia a pesquisa. O texto digitado nunca e gravado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using XGBP.Games;

namespace XGBP.Desktop
{
    [DataContract]
    public sealed class ProgramUse
    {
        [DataMember(IsRequired = true)] public string Key;
        [DataMember(IsRequired = true)] public int Count;
        [DataMember(IsRequired = true)] public long Last;   // segundos desde 1970 (UTC)
    }
    [DataContract]
    public sealed class ProgramHistoryState
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public List<ProgramUse> Entries = new List<ProgramUse>();
    }

    public static class ProgramHistory
    {
        public const int MaxEntries = 100, RecentCount = 5;
        public static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "history",
            "programs.json");

        public static ProgramHistoryState Load(string path, Action<string> log)
        {
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    if (new FileInfo(candidate).Length > 256 * 1024) throw new InvalidDataException("Historico grande demais.");
                    var state = GameFiles.Read<ProgramHistoryState>(candidate);
                    if (state == null || state.Version != 1 || state.Entries == null) throw new InvalidDataException("Versao desconhecida.");
                    state.Entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.Key) || e.Count < 1);
                    return state;
                }
                catch (Exception ex) { if (log != null) log("programas: historico ignorado | " + Path.GetFileName(candidate) + " | " + ex.Message); }
            }
            return new ProgramHistoryState();
        }
        public static void Record(ProgramHistoryState state, string key, DateTime now)
        {
            var use = state.Entries.Find(e => e.Key == key);
            if (use == null) { use = new ProgramUse { Key = key }; state.Entries.Add(use); }
            use.Count = Math.Min(use.Count + 1, 9999); use.Last = GameFiles.Epoch(now);
            state.Entries.Sort((a, b) => b.Last.CompareTo(a.Last));
            if (state.Entries.Count > MaxEntries) state.Entries.RemoveRange(MaxEntries, state.Entries.Count - MaxEntries);
        }
        // Bonus somado a pontuacao da busca: frequencia (ate 80) e recencia (ate 40).
        public static int Boost(ProgramHistoryState state, string key, DateTime now)
        {
            var use = state.Entries.Find(e => e.Key == key);
            if (use == null) return 0;
            double days = (GameFiles.Epoch(now) - use.Last) / 86400.0;
            return Math.Min(use.Count, 10) * 8 + (days <= 7 ? 40 : days <= 30 ? 20 : 0);
        }
        public static int Count(ProgramHistoryState state, string key)
        {
            var use = state.Entries.Find(e => e.Key == key);
            return use == null ? 0 : use.Count;
        }
    }
}
