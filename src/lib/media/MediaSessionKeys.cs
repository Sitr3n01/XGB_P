// Identidade de objeto COM retida durante sua presenca, sem depender da posicao na enumeracao.
using System;
using System.Collections.Generic;

namespace XGBP.Media
{
    internal sealed class MediaSessionKeys
    {
        sealed class Entry { public string AppId, Key; public object Owner; }
        readonly Dictionary<long, Entry> entries = new Dictionary<long, Entry>();
        long sequence;
        internal string Get(long identity, string appId, object owner)
        {
            if (identity == 0) throw new InvalidOperationException("A sessão de mídia não pôde ser identificada.");
            Entry entry;
            if (!entries.TryGetValue(identity, out entry) || entry.AppId != appId)
            {
                entry = new Entry { AppId = appId, Key = appId + "#s" + (++sequence), Owner = owner };
                entries[identity] = entry;
            }
            return entry.Key;
        }
        internal void Retain(HashSet<long> live)
        { foreach (var key in new List<long>(entries.Keys)) if (!live.Contains(key)) entries.Remove(key); }
    }
}
