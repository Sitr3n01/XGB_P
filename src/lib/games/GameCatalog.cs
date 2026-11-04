// Catalogo independente de loja. Steam e o primeiro provedor; configuracao local permite outras origens.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;

namespace XGBP.Games
{
    public sealed class GameEntry
    {
        public string Key, Profile, Name, Source, InstallRoot, LaunchUri, LaunchFile, Arguments, WorkingDirectory, Kind = "Unknown";
        public string[] RuntimePaths = new string[0], RequiredFiles = new string[0];
        public string IconLocation, IconKey, IconSource;
        public byte[] IconPng;
        public bool Available;
        public long Imported, Observed;
        public string UnavailableReason;
        public long LastPlayed { get { return Math.Max(Imported, Observed); } }
        public GameEntry Copy() { return (GameEntry)MemberwiseClone(); }
    }
    [DataContract]
    public sealed class GameMapping
    {
        [DataMember] public string Key;
        [DataMember] public string Name;
        [DataMember] public string Kind;
        [DataMember] public string LaunchFile;
        [DataMember] public string Arguments;
        [DataMember] public string WorkingDirectory;
        [DataMember] public string[] RuntimePaths;
        [DataMember] public string[] RequiredFiles;
        [DataMember] public string IconLocation;
        [DataMember] public bool Hidden;
    }
    [DataContract]
    public sealed class GameSettings
    {
        [DataMember] public int Version = 1;
        [DataMember] public string SteamProfile;
        [DataMember] public List<GameMapping> Entries = new List<GameMapping>();
    }
    public sealed class GameCatalogState
    {
        public List<GameEntry> Entries = new List<GameEntry>();
        public string Profile, Status = "", SteamRoot;
        // Chaves repetidas ignoradas (mesmo jogo em duas bibliotecas, atalho adicionado duas vezes): ficam para o log.
        public List<string> Repeated = new List<string>();
    }
    public static class GameFiles
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "games");
        public static T Read<T>(string path) where T : new()
        { if (!File.Exists(path)) return new T(); using (var f = File.OpenRead(path)) { if (f.Length > 4 * 1024 * 1024) throw new InvalidDataException("JSON muito grande."); return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(f); } }
        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + ".tmp";
            using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { new DataContractJsonSerializer(typeof(T)).WriteObject(f, value); f.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
        }
        public static string Normalize(string value)
        { if (string.IsNullOrWhiteSpace(value)) return ""; try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'))).TrimEnd('\\', '/'); } catch { return ""; } }
        public static bool Same(string a, string b) { return !string.IsNullOrEmpty(a) && string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase); }
        // Executaveis auxiliares (crash, instaladores, navegador embutido, anti-cheat, redistribuiveis) nao representam o jogo aberto.
        public static bool Helper(string path)
        {
            string n = Path.GetFileName(path ?? "").ToLowerInvariant();
            bool crashHelper = n.StartsWith("crashreport", StringComparison.Ordinal) || n.StartsWith("crashpad", StringComparison.Ordinal) ||
                n.StartsWith("crashhandler", StringComparison.Ordinal) || n.StartsWith("unitycrashhandler", StringComparison.Ordinal);
            return crashHelper || n.Contains("unins") || n.Contains("setup") || n.Contains("cef") || n.Contains("anticheat") || n.Contains("thirdparty") || n.Contains("redist") || n.Contains("webview");
        }
        public static long Epoch(DateTime utc) { return (long)(utc.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
        public static long ValidTime(string text, DateTime now)
        { long value; return long.TryParse(text, out value) && value > 0 && value <= Epoch(now.AddMinutes(5)) ? value : 0; }
        public static bool Ready(GameEntry entry)
        {
            if (entry.Kind != "Game") return false;
            if (string.IsNullOrEmpty(entry.LaunchUri) && string.IsNullOrEmpty(entry.LaunchFile)) return false;
            if (!string.IsNullOrEmpty(entry.LaunchFile) && !File.Exists(entry.LaunchFile)) return false;
            if (!string.IsNullOrEmpty(entry.InstallRoot) && !Directory.Exists(entry.InstallRoot)) return false;
            foreach (var f in entry.RequiredFiles) if (!File.Exists(f)) return false;
            foreach (var f in entry.RuntimePaths) if (File.Exists(f)) return true;
            return false;
        }
    }
    public static class SteamCatalog
    {
        static readonly RuntimeDiscoveryCache RuntimeCache = new RuntimeDiscoveryCache();
        static readonly HashSet<string> Tools = new HashSet<string> { "1812620", "431960", "228980", "250820", "1070560" };
        public static string ShortcutUri(uint appId) { return "steam://rungameid/" + (((ulong)appId << 32) | 0x02000000UL).ToString(CultureInfo.InvariantCulture); }
        public static GameCatalogState Read(GameSettings settings)
        {
            string root = null, activeProfile = null; int steamPid = 0;
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) if (k != null) root = k.GetValue("SteamPath") as string;
            if (string.IsNullOrEmpty(root)) return Read(null, settings, null);
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess")) if (k != null)
            {
                steamPid = Convert.ToInt32(k.GetValue("pid", 0));
                uint user = unchecked((uint)Convert.ToInt32(k.GetValue("ActiveUser", 0)));
                activeProfile = user == 0 ? null : user.ToString(CultureInfo.InvariantCulture);
            }
            // O registro pode sobreviver ao reboot. Validar PID e imagem antes de aceitar a conta como ativa.
            try { using (var p = Process.GetProcessById(steamPid)) if (!GameFiles.Same(XGBP.Desktop.DesktopWindows.ProcessPath((uint)p.Id), Path.Combine(root, "steam.exe"))) activeProfile = null; }
            catch { activeProfile = null; }
            return Read(root, settings, activeProfile);
        }
        public static GameCatalogState Read(string root, GameSettings settings, string activeProfile)
        {
            if (settings == null || settings.Version != 1) throw new InvalidDataException("Versao de catalog.json nao suportada.");
            var state = new GameCatalogState { SteamRoot = root };
            string profile = settings.SteamProfile;
            if (string.IsNullOrEmpty(profile)) profile = activeProfile;
            if (!string.IsNullOrEmpty(root) && string.IsNullOrEmpty(profile))
            {
                string login = Path.Combine(root, "config", "loginusers.vdf");
                if (File.Exists(login)) foreach (var pair in ValveData.Read(login, false).Get("users").Children)
                {
                    ulong id;
                    if (pair.Value.Text("MostRecent") == "1" && ulong.TryParse(pair.Key, out id) && id >= 76561197960265728UL)
                        profile = (id - 76561197960265728UL).ToString(CultureInfo.InvariantCulture);
                }
                if (string.IsNullOrEmpty(profile) && Directory.Exists(Path.Combine(root, "userdata")))
                {
                    var candidates = new List<string>();
                    foreach (var d in Directory.GetDirectories(Path.Combine(root, "userdata")))
                        if (File.Exists(Path.Combine(d, "config", "localconfig.vdf"))) candidates.Add(Path.GetFileName(d));
                    if (candidates.Count == 1) profile = candidates[0];
                    else if (candidates.Count > 1) state.Status = "Selecione SteamProfile em catalog.json para não misturar contas.";
                }
            }
            uint profileId;
            if (!string.IsNullOrEmpty(profile) && (!uint.TryParse(profile, out profileId) || profileId == 0)) throw new InvalidDataException("SteamProfile invalido.");
            state.Profile = profile;
            if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(profile)) ReadSteam(state, settings);
            else if (state.Status.Length == 0) state.Status = "Histórico Steam indisponível; jogos locais continuam disponíveis.";
            foreach (var m in settings.Entries ?? new List<GameMapping>())
            {
                if (m.Key == null || !m.Key.StartsWith("local:", StringComparison.Ordinal) || m.Hidden) continue;
                var e = new GameEntry { Key = m.Key, Profile = "local", Source = "Local", Name = m.Name ?? m.Key, Kind = m.Kind ?? "Game" };
                Apply(e, m); state.Entries.Add(e);
            }
            // Uma chave repetida nao derruba o catalogo inteiro: fica um registro so, de preferencia o instalado
            // (manifesto antigo noutra biblioteca, atalho externo adicionado duas vezes).
            var unique = new Dictionary<string, int>(); var entries = new List<GameEntry>();
            foreach (var e in state.Entries)
            {
                e.Available = GameFiles.Ready(e); e.UnavailableReason = e.Kind != "Game" ? "Ferramenta ou classificação pendente" : e.Available ? "" : "Caminho ou executável indisponível";
                int at;
                if (!unique.TryGetValue(e.Key, out at)) { unique[e.Key] = entries.Count; entries.Add(e); continue; }
                state.Repeated.Add(e.Key);
                if (e.Available && !entries[at].Available) entries[at] = e;
            }
            state.Entries = entries;
            return state;
        }
        static void ReadSteam(GameCatalogState state, GameSettings settings)
        {
            string root = state.SteamRoot, profile = state.Profile;
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { GameFiles.Normalize(root) };
            string libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFile)) foreach (var n in ValveData.Read(libraryFile, false).Get("libraryfolders").Children.Values)
                if (n.Text("path").Length > 0) libraries.Add(GameFiles.Normalize(n.Text("path")));
            string config = Path.Combine(root, "userdata", profile, "config");
            var appHistory = new ValveNode(); string historyFile = Path.Combine(config, "localconfig.vdf");
            if (File.Exists(historyFile)) appHistory = ValveData.Read(historyFile, false).Get("UserLocalConfigStore").Get("Software").Get("Valve").Get("Steam").Get("apps");
            var ids = new HashSet<uint>();
            foreach (var library in libraries)
            {
                string apps = Path.Combine(library, "steamapps"); if (!Directory.Exists(apps)) continue;
                foreach (var file in Directory.GetFiles(apps, "appmanifest_*.acf"))
                { uint id; if (uint.TryParse(Path.GetFileNameWithoutExtension(file).Substring(12), out id)) ids.Add(id); }
            }
            Dictionary<uint, ValveNode> metadata;
            try { metadata = ValveData.AppInfo(Path.Combine(root, "appcache", "appinfo.vdf"), ids); }
            catch (Exception ex) { metadata = new Dictionary<uint, ValveNode>(); state.Status = "Classificação Steam indisponível: " + ex.Message; }
            foreach (var library in libraries)
            {
                string apps = Path.Combine(library, "steamapps");
                if (!Directory.Exists(apps)) { state.Status = "Uma biblioteca está indisponível; confira a unidade."; continue; }
                foreach (var file in Directory.GetFiles(apps, "appmanifest_*.acf"))
                {
                    var n = ValveData.Read(file, false).Get("AppState"); string id = n.Text("appid"); uint numeric;
                    if (!uint.TryParse(id, out numeric) || numeric == 0) continue;
                    string common = GameFiles.Normalize(Path.Combine(apps, "common")), install = GameFiles.Normalize(Path.Combine(common, n.Text("installdir")));
                    if (!install.StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                    ValveNode info; if (!metadata.TryGetValue(numeric, out info)) info = new ValveNode();
                    string appType = info.Get("common").Text("type");
                    bool isGame = string.Equals(appType, "game", StringComparison.OrdinalIgnoreCase) || string.Equals(appType, "demo", StringComparison.OrdinalIgnoreCase) || string.Equals(appType, "mod", StringComparison.OrdinalIgnoreCase);
                    // Sem tipo conhecido, nao promover automaticamente um aplicativo a jogo.
                    var e = new GameEntry { Key = "steam:" + profile + ":" + id, Profile = profile, Name = n.Text("name"), Source = "Steam", InstallRoot = install,
                        Kind = Tools.Contains(id) ? "Tool" : isGame ? "Game" : string.IsNullOrEmpty(appType) ? "Unknown" : "Tool", LaunchUri = "steam://rungameid/" + id,
                        Imported = GameFiles.ValidTime(appHistory.Get(id).Text("LastPlayed"), DateTime.UtcNow) };
                    if (e.Imported == 0) e.Imported = GameFiles.ValidTime(n.Text("LastPlayed"), DateTime.UtcNow);
                    var mapping = Find(settings, e.Key); if (mapping != null && mapping.Hidden) continue;
                    if (e.Kind == "Game" || mapping != null && mapping.Kind == "Game")
                    {
                        var launches = new List<string>();
                        foreach (var launch in info.Get("config").Get("launch").Children.Values)
                        {
                            string os = launch.Get("config").Text("oslist"), relative = launch.Text("executable");
                            if (os.Length > 0 && os.IndexOf("windows", StringComparison.OrdinalIgnoreCase) < 0 || !string.Equals(Path.GetExtension(relative), ".exe", StringComparison.OrdinalIgnoreCase)) continue;
                            string target = GameFiles.Normalize(Path.Combine(install, relative));
                            if (target.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) launches.Add(target);
                        }
                        e.LaunchFile = launches.Find(File.Exists) ?? (launches.Count > 0 ? launches[0] : null);
                        var discovered = CachedRuntime(install, null, file);
                        var shipping = Array.FindAll(discovered, p => p.EndsWith("-Shipping.exe", StringComparison.OrdinalIgnoreCase));
                        e.RuntimePaths = shipping.Length > 0 ? shipping : launches.ToArray();
                    }
                    if (mapping != null) Apply(e, mapping); state.Entries.Add(e);
                }
            }
            string shortcuts = Path.Combine(config, "shortcuts.vdf");
            if (File.Exists(shortcuts)) foreach (var n in ValveData.Read(shortcuts, true).Get("shortcuts").Children.Values)
            {
                uint id; if (!uint.TryParse(n.Text("appid"), out id)) continue;
                string exe = GameFiles.Normalize(n.Text("exe")), dir = GameFiles.Normalize(n.Text("StartDir"));
                string basename = Path.GetFileName(exe).ToLowerInvariant();
                bool wrapper = basename == "wscript.exe" || basename == "cscript.exe" || basename == "cmd.exe" || basename == "powershell.exe" || basename == "javaw.exe";
                var e = new GameEntry { Key = "shortcut:" + profile + ":" + id, Profile = profile, Source = "Steam · externo", Name = n.Text("appname"),
                    LaunchFile = exe, WorkingDirectory = dir, LaunchUri = ShortcutUri(id), IconLocation = n.Text("icon"), Kind = wrapper ? "Unknown" : "Game",
                    Imported = GameFiles.ValidTime(n.Text("LastPlayTime"), DateTime.UtcNow) };
                if (!wrapper && File.Exists(exe)) e.RuntimePaths = CachedRuntime(Path.GetDirectoryName(exe), exe, shortcuts);
                var mapping = Find(settings, e.Key); if (mapping != null && mapping.Hidden) continue;
                if (mapping != null) Apply(e, mapping); state.Entries.Add(e);
            }
        }
        static GameMapping Find(GameSettings settings, string key) { return (settings.Entries ?? new List<GameMapping>()).Find(m => m.Key == key); }
        static void Apply(GameEntry e, GameMapping m)
        {
            if (!string.IsNullOrEmpty(m.Name)) e.Name = m.Name;
            if (!string.IsNullOrEmpty(m.Kind)) e.Kind = m.Kind;
            if (!string.IsNullOrEmpty(m.LaunchFile)) e.LaunchFile = GameFiles.Normalize(m.LaunchFile);
            if (m.Arguments != null) e.Arguments = m.Arguments;
            if (!string.IsNullOrEmpty(m.WorkingDirectory)) e.WorkingDirectory = GameFiles.Normalize(m.WorkingDirectory);
            if (m.RuntimePaths != null) e.RuntimePaths = Array.ConvertAll(m.RuntimePaths, GameFiles.Normalize);
            if (m.RequiredFiles != null) e.RequiredFiles = Array.ConvertAll(m.RequiredFiles, GameFiles.Normalize);
            if (m.IconLocation != null) e.IconLocation = m.IconLocation;
        }
        static string[] CachedRuntime(string root, string launcher, string manifest)
        {
            var info = new FileInfo(manifest);
            string revision = info.Length + ":" + info.LastWriteTimeUtc.Ticks + ":" + Directory.GetLastWriteTimeUtc(root).Ticks;
            return RuntimeCache.Get(root + "|" + launcher, revision, () => DiscoverRuntime(root, launcher));
        }
        static string[] DiscoverRuntime(string root, string launcher = null)
        {
            var found = new List<string>(); var shipping = new List<string>(); var todo = new Queue<string>(); todo.Enqueue(root); int visited = 0;
            while (todo.Count > 0 && visited++ < 1500 && found.Count < 2048)
            {
                string dir = todo.Dequeue(); if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.exe"))
                    {
                        string n = Path.GetFileName(file).ToLowerInvariant();
                        if (GameFiles.Helper(file)) continue;
                        // Atalho externo pode estar numa pasta compartilhada por varios jogos.
                        // Aceitar somente o alvo ou uma variante com a mesma base; outros casos pedem alias explicito.
                        if (launcher != null)
                        {
                            string stem = Path.GetFileNameWithoutExtension(launcher).ToLowerInvariant();
                            if (!GameFiles.Same(file, launcher) && Path.GetFileNameWithoutExtension(file).ToLowerInvariant() != stem && !n.StartsWith(stem + "-", StringComparison.Ordinal)) continue;
                        }
                        found.Add(GameFiles.Normalize(file)); if (n.EndsWith("-shipping.exe")) shipping.Add(GameFiles.Normalize(file));
                        if (found.Count >= 2048) break;
                    }
                    foreach (var sub in Directory.GetDirectories(dir)) if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) todo.Enqueue(sub);
                }
                catch (UnauthorizedAccessException) { } catch (IOException) { }
            }
            return (shipping.Count > 0 ? shipping : found).ToArray();
        }
    }
}
