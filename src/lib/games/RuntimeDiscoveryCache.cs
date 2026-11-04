// Cache limitado de descoberta; manifesto atualizado invalida imediatamente, TTL cobre atalhos externos.
using System;
using System.Collections.Generic;

namespace XGBP.Games
{
    public sealed class RuntimeDiscoveryCache
    {
        sealed class Entry { public string Revision; public string[] Paths; public DateTime Expires; }
        readonly object gate = new object();
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly Func<DateTime> now;
        public RuntimeDiscoveryCache() : this(() => DateTime.UtcNow) { }
        internal RuntimeDiscoveryCache(Func<DateTime> now) { this.now = now; }
        public string[] Get(string key, string revision, Func<string[]> discover)
        {
            lock (gate)
            {
                Entry cached;
                if (entries.TryGetValue(key, out cached) && cached.Revision == revision && now() < cached.Expires)
                    return (string[])cached.Paths.Clone();
            }
            string[] found = discover();
            lock (gate)
            {
                if (entries.Count >= 128) entries.Clear();
                entries[key] = new Entry { Revision = revision, Paths = (string[])found.Clone(), Expires = now().AddMinutes(found.Length == 0 ? .5 : 5) };
            }
            return found;
        }
    }
}
