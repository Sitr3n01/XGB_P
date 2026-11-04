// Associacao por caminho, nome e ancestrais: nao cruza cada processo com todos os jogos.
using System;
using System.Collections.Generic;
using System.IO;

namespace XGBP.Games
{
    public sealed class GameProcessIndex
    {
        readonly Dictionary<string, List<GameProcess>> matches = new Dictionary<string, List<GameProcess>>();
        readonly Dictionary<string, HashSet<string>> paths = NewMap(), names = NewMap(), roots = NewMap();
        public int Lookups { get; private set; }
        static Dictionary<string, HashSet<string>> NewMap()
        { return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase); }
        static void Add(Dictionary<string, HashSet<string>> map, string value, string key)
        {
            if (string.IsNullOrEmpty(value)) return;
            HashSet<string> owners;
            if (!map.TryGetValue(value, out owners)) map[value] = owners = new HashSet<string>(StringComparer.Ordinal);
            owners.Add(key);
        }
        void Include(Dictionary<string, HashSet<string>> map, string value, HashSet<string> candidates)
        {
            Lookups++;
            HashSet<string> owners;
            if (!string.IsNullOrEmpty(value) && map.TryGetValue(value, out owners)) candidates.UnionWith(owners);
        }
        public GameProcessIndex(IList<GameEntry> games, IList<GameProcess> processes)
        {
            foreach (var game in games)
            {
                if (game.Kind != "Game") continue;
                foreach (var path in game.RuntimePaths)
                {
                    Add(paths, path, game.Key);
                    Add(names, Path.GetFileName(path), game.Key);
                }
                Add(roots, game.InstallRoot, game.Key);
            }
            foreach (var process in processes)
            {
                var candidates = new HashSet<string>(StringComparer.Ordinal);
                if (process.Path == null) Include(names, process.Name, candidates);
                else
                {
                    if (string.IsNullOrEmpty(process.Full)) continue;
                    Include(paths, process.Full, candidates);
                    if (!GameFiles.Helper(process.Full))
                    {
                        string parent = Path.GetDirectoryName(process.Full);
                        while (!string.IsNullOrEmpty(parent))
                        {
                            Include(roots, parent.TrimEnd('\\', '/'), candidates);
                            string next = Path.GetDirectoryName(parent);
                            if (next == parent) break;
                            parent = next;
                        }
                    }
                }
                if (candidates.Count != 1) continue; // Ambiguidade conserva a politica anterior.
                foreach (string key in candidates)
                {
                    List<GameProcess> list;
                    if (!matches.TryGetValue(key, out list)) matches[key] = list = new List<GameProcess>();
                    list.Add(process);
                }
            }
        }
        public List<GameProcess> For(string key)
        { List<GameProcess> list; return matches.TryGetValue(key, out list) ? list : new List<GameProcess>(); }
    }
}
