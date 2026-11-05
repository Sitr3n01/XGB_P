// Associacao por caminho, nome e ancestrais: nao cruza cada processo com todos os jogos.
using System;
using System.Collections.Generic;
using System.IO;

namespace XGBP.Games
{
    public sealed class GameProcessIndex
    {
        // Profundidade maxima a partir do alvo do atalho: lancador -> wrapper -> jogo, com folga de um nivel.
        public const int MaxChildDepth = 3;
        readonly Dictionary<string, List<GameProcess>> matches = new Dictionary<string, List<GameProcess>>();
        readonly Dictionary<string, HashSet<string>> paths = NewMap(), names = NewMap(), roots = NewMap();
        // Processos sem nenhum jogo candidato, com caminho e criacao legiveis e fora da lista de auxiliares, por PID do pai.
        readonly Dictionary<uint, List<GameProcess>> orphans = new Dictionary<uint, List<GameProcess>>();
        readonly Dictionary<string, GameProcess> unclaimed = new Dictionary<string, GameProcess>(StringComparer.Ordinal);
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
                if (candidates.Count == 0 && process.Path != null && process.Created > 0 && !GameFiles.Helper(process.Full))
                {
                    unclaimed[process.Instance] = process;
                    if (process.ParentPid != 0 && process.ParentPid != process.Pid)
                    {
                        List<GameProcess> children;
                        if (!orphans.TryGetValue(process.ParentPid, out children)) orphans[process.ParentPid] = children = new List<GameProcess>();
                        children.Add(process);
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
        // Descendentes sem dono dos processos do jogo (ADR 0089). Um filho so entra se nasceu depois do pai vivo com aquele PID,
        // o que barra PID reaproveitado. "kept" sao instancias ja adotadas que continuam valendo depois que o lancador sai.
        public List<GameProcess> Descendants(string key, ICollection<string> kept)
        {
            var result = new List<GameProcess>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            var level = new List<GameProcess>(For(key));
            foreach (var p in level) seen.Add(p.Instance);
            if (kept != null) foreach (string instance in kept)
            {
                GameProcess survivor;
                if (unclaimed.TryGetValue(instance, out survivor) && seen.Add(instance)) { result.Add(survivor); level.Add(survivor); }
            }
            for (int depth = 0; depth < MaxChildDepth && level.Count > 0; depth++)
            {
                var next = new List<GameProcess>();
                foreach (var parent in level)
                {
                    List<GameProcess> children;
                    if (parent.Created <= 0 || !orphans.TryGetValue(parent.Pid, out children)) continue;
                    foreach (var child in children)
                        if (child.Created >= parent.Created && seen.Add(child.Instance)) { result.Add(child); next.Add(child); }
                }
                level = next;
            }
            return result;
        }
    }
}
