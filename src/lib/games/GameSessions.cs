// Sessoes observadas: apenas consulta de processos/janelas, sem memoria de jogos, hooks ou injecao.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using XGBP.Desktop;

namespace XGBP.Games
{
    public sealed class GameProcess
    {
        public uint Pid;
        public long Created;
        public string Path, Name;
        string full;
        public string Instance { get { return Pid + ":" + Created; } }
        public string Full { get { return full ?? (full = GameFiles.Normalize(Path)); } }
        public static List<GameProcess> Read()
        {
            var result = new List<GameProcess>();
            var path = new System.Text.StringBuilder(512); // Caminhos usuais nao exigem um buffer de 64 KB por processo.
            foreach (var process in Process.GetProcesses()) using (process)
            {
                uint pid = (uint)process.Id; if (pid == 0) continue;
                var item = new GameProcess { Pid = pid };
                // Sem acesso de consulta (protecao de anti-cheat, por exemplo), o processo continua listado so pelo nome.
                IntPtr h = OpenProcess(0x1000, false, pid);
                if (h != IntPtr.Zero) try
                {
                    long created, exit, kernel, user;
                    string image = ReadImagePath(h, ref path);
                    if (image != null && GetProcessTimes(h, out created, out exit, out kernel, out user))
                    { item.Path = image; item.Created = created; }
                }
                finally { CloseHandle(h); }
                if (item.Path != null) item.Name = System.IO.Path.GetFileName(item.Path);
                else try { item.Name = process.ProcessName + ".exe"; }
                    catch (InvalidOperationException) { continue; } catch (System.ComponentModel.Win32Exception) { continue; }
                result.Add(item);
            }
            return result;
        }
        internal static string ReadImagePath(IntPtr handle, ref System.Text.StringBuilder path)
        {
            if (path.Capacity > 512) path = new System.Text.StringBuilder(512);
            path.Length = 0; int size = path.Capacity;
            bool read = QueryFullProcessImageName(handle, 0, path, ref size);
            if (!read && Marshal.GetLastWin32Error() == 122)
            {
                path.Capacity = 32768; size = path.Capacity;
                read = QueryFullProcessImageName(handle, 0, path, ref size);
            }
            return read ? path.ToString() : null;
        }
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr handle, uint flags, System.Text.StringBuilder path, ref int size);
        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr handle, out long created, out long exit, out long kernel, out long user);
    }
    public sealed class GameActivity
    {
        public string Key;
        public DesktopWindow Window;
        public bool Confirmed, Paused;
        public long LastFocused, Started;
        // Fracao dos threads suspensos lida pelo worker (-1 sem leitura); a interface combina com as pausas da propria previa.
        public double SuspendedShare = -1;
        // Processo principal (janela ou runtime) e todos os processos do jogo, para pausar, retomar e fechar.
        public uint Pid;
        public List<uint> Pids = new List<uint>();
        public List<ProcessIdentity> Processes = new List<ProcessIdentity>();
    }
    public sealed class GamesView
    {
        public List<GameEntry> Entries = new List<GameEntry>();
        public List<GameActivity> Activities = new List<GameActivity>();
        public GameActivity Current;
        public string Status;
        public GameEntry Find(string key) { return Entries.Find(e => e.Key == key); }
        public GameActivity Activity(string key) { return Activities.Find(a => a.Key == key); }
        public GameEntry Featured
        {
            get
            {
                var running = Current == null ? null : Find(Current.Key);
                if (running != null && running.Kind == "Game") return running;
                GameEntry last = null;
                foreach (var e in Entries)
                    if (e.Kind == "Game" && e.LastPlayed > 0 && (last == null || e.LastPlayed > last.LastPlayed || e.LastPlayed == last.LastPlayed && string.CompareOrdinal(e.Key, last.Key) < 0)) last = e;
                return last;
            }
        }
        // Ate tres outros jogos: o destaque pertence somente a Voltar ao jogo (ADR 0060).
        // Preservar a prioridade dos demais abertos, seguida do historico dos fechados.
        public List<GameEntry> Recent()
        {
            var featured = Featured;
            var result = new List<GameEntry>();
            var open = new List<GameActivity>(Activities);
            open.Sort(delegate(GameActivity a, GameActivity b)
            {
                int c = b.LastFocused.CompareTo(a.LastFocused); if (c != 0) return c;
                c = b.Started.CompareTo(a.Started); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
            });
            foreach (var a in open)
            {
                var e = Find(a.Key);
                if (e != null && e.Kind == "Game" && (featured == null || e.Key != featured.Key) && !result.Contains(e)) result.Add(e);
            }
            var closed = Entries.FindAll(e => e.Available && e.Kind == "Game" && e.LastPlayed > 0 &&
                (featured == null || e.Key != featured.Key) && !result.Contains(e));
            closed.Sort(delegate(GameEntry a, GameEntry b) { int c = b.LastPlayed.CompareTo(a.LastPlayed); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key); });
            result.AddRange(closed);
            if (result.Count > 3) result.RemoveRange(3, result.Count - 3); return result;
        }
        // Agrupamento de apresentacao da Home. Nao muda sessao, historico ou a politica de foco.
        public List<GameEntry> OpenGames()
        {
            var open = new List<GameActivity>(Activities);
            string current = Current == null ? null : Current.Key;
            open.Sort(delegate(GameActivity a, GameActivity b) {
                int order = (b.Key == current).CompareTo(a.Key == current);
                if (order == 0) order = b.LastFocused.CompareTo(a.LastFocused);
                if (order == 0) order = b.Started.CompareTo(a.Started);
                return order == 0 ? string.CompareOrdinal(a.Key, b.Key) : order;
            });
            var result = new List<GameEntry>(); var seen = new HashSet<string>();
            foreach (var activity in open)
            {
                var entry = Find(activity.Key);
                if (entry != null && entry.Kind == "Game" && seen.Add(entry.Key)) result.Add(entry);
            }
            return result;
        }
        public List<GameEntry> ClosedRecent()
        {
            var featured = OpenGames().Count == 0 ? Featured : null; var seen = new HashSet<string>();
            var closed = Entries.FindAll(e => e.Kind == "Game" && e.Available && e.LastPlayed > 0 && Activity(e.Key) == null &&
                (featured == null || featured.Key != e.Key) && seen.Add(e.Key));
            closed.Sort(delegate(GameEntry a, GameEntry b) {
                int order = b.LastPlayed.CompareTo(a.LastPlayed);
                return order == 0 ? string.CompareOrdinal(a.Key, b.Key) : order;
            });
            if (closed.Count > 3) closed.RemoveRange(3, closed.Count - 3);
            return closed;
        }
    }
    public sealed class GameSessions
    {
        sealed class Session
        {
            public string Instance, RuntimeInstance;
            public bool Confirmed;
            public int FocusReads, RuntimeReads;
            public DateTime LastSeen, LastRead;
            public long LastFocused;
            public DesktopWindow Window;
        }
        readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();
        readonly GameHistory history;
        string lastFocusedGame;
        public GameSessions(GameHistory history) { this.history = history; }
        // Historico ilegivel fica preservado sem escrita por design: nao ha o que salvar, entao nao conta como falha.
        public bool FlushHistory() { return history.Flush(DateTime.UtcNow, true) || !history.Writable; }
        // Persistir sem foco exige a runtime exata, com criacao conhecida. Nome/pasta apenas nao bastam.
        static bool HistoryRuntime(GameEntry e, GameProcess p)
        {
            if (p.Created <= 0 || string.IsNullOrEmpty(p.Path) || GameFiles.Helper(p.Path)) return false;
            string name = System.IO.Path.GetFileName(p.Path).ToLowerInvariant();
            if (name == "wscript.exe" || name == "cscript.exe" || name == "cmd.exe" || name == "powershell.exe" || name == "javaw.exe") return false;
            return Array.Exists(e.RuntimePaths, r => string.Equals(r, p.Full, StringComparison.OrdinalIgnoreCase));
        }
        public GamesView Update(GameCatalogState catalog, List<GameProcess> processes, List<DesktopWindow> windows, uint foregroundPid, DateTime now, Func<DesktopWindow, bool> stillValid = null)
        {
            if (stillValid == null) stillValid = DesktopWindows.Belongs;
            history.Flush(now, false);
            var processIndex = new GameProcessIndex(catalog.Entries, processes);
            var view = new GamesView { Status = string.IsNullOrEmpty(history.Error) ? catalog.Status : history.Error };
            var live = new HashSet<string>();
            foreach (var original in catalog.Entries)
            {
                var e = original.Copy(); e.Observed = history.Last(e.Key); view.Entries.Add(e);
                if (e.Kind != "Game") continue;
                var matches = processIndex.For(e.Key);
                Session s; sessions.TryGetValue(e.Key, out s);
                if (matches.Count == 0)
                {
                    if (s != null)
                    {
                        s.FocusReads = s.RuntimeReads = 0; s.RuntimeInstance = null;
                        if (now - s.LastSeen < TimeSpan.FromSeconds(10)) live.Add(e.Key);
                    }
                    continue;
                }
                // Janela: a do foreground, depois uma nao minimizada, depois qualquer uma dos processos do jogo.
                var owned = windows.FindAll(w => matches.Exists(p => p.Pid == w.ProcessId && (p.Path == null || w.ExecutablePath == null || GameFiles.Same(w.ExecutablePath, p.Path))));
                var window = owned.Find(w => w.ProcessId == foregroundPid && !w.Minimized) ?? owned.Find(w => !w.Minimized) ?? (owned.Count > 0 ? owned[0] : null);
                // Janela fora da enumeracao (pausada, "nao respondendo" ou oculta por instantes) continua do jogo enquanto o handle existir.
                if (window == null && s != null && s.Window != null && matches.Exists(p => p.Pid == s.Window.ProcessId) && stillValid(s.Window)) window = s.Window;
                // Identidade estavel: manter a instancia da sessao; senao a da janela, a do foreground ou a do runtime.
                var process = (s == null ? null : matches.Find(p => p.Instance == s.Instance))
                    ?? (window == null ? null : matches.Find(p => p.Pid == window.ProcessId))
                    ?? matches.Find(p => p.Pid == foregroundPid)
                    ?? matches.Find(p => p.Path != null && Array.Exists(e.RuntimePaths, r => GameFiles.Same(r, p.Path)))
                    ?? matches[0];
                if (s == null || s.Instance != process.Instance && now - s.LastSeen >= TimeSpan.FromSeconds(10))
                    sessions[e.Key] = s = new Session { Instance = process.Instance };
                // Um salto de tempo (suspensao) nao confirma continuidade de atividade por duas leituras.
                if (now - s.LastRead > TimeSpan.FromSeconds(8))
                { s.FocusReads = s.RuntimeReads = 0; s.RuntimeInstance = null; }
                if (s.LastRead != default(DateTime) && now - s.LastRead > TimeSpan.FromMinutes(1)) s.Confirmed = false;
                var runtime = matches.Find(p => p.Instance == s.RuntimeInstance && HistoryRuntime(e, p)) ?? matches.Find(p => HistoryRuntime(e, p));
                s.RuntimeReads = runtime == null ? 0 : runtime.Instance == s.RuntimeInstance ? s.RuntimeReads + 1 : 1;
                s.RuntimeInstance = runtime == null ? null : runtime.Instance;
                bool foreground = foregroundPid != 0 && matches.Exists(p => p.Pid == foregroundPid);
                s.FocusReads = foreground ? s.FocusReads + 1 : 0;
                if (foreground) s.LastFocused = GameFiles.Epoch(now);
                bool focused = s.FocusReads >= 2;
                if (!s.Confirmed && (s.RuntimeReads >= 2 || focused) || focused && lastFocusedGame != e.Key)
                {
                    s.Confirmed = true; if (focused) lastFocusedGame = e.Key;
                    history.Observe(e.Key, now); e.Observed = history.Last(e.Key);
                }
                s.Instance = process.Instance; s.LastSeen = now; s.LastRead = now; s.Window = window; live.Add(e.Key);
                long started = long.MaxValue; foreach (var p in matches) if (p.Created > 0 && p.Created < started) started = p.Created;
                var activity = new GameActivity { Key = e.Key, Window = window, Confirmed = s.Confirmed, LastFocused = s.LastFocused,
                    Started = started == long.MaxValue ? 0 : started, Pid = process.Pid, Pids = matches.ConvertAll(p => p.Pid),
                    Processes = matches.ConvertAll(p => new ProcessIdentity(p.Pid, p.Created)) };
                view.Activities.Add(activity);
                // O jogo aberto com foco mais recente e o destaque; sem foco registrado, o ultimo a abrir. Nao depende de janela.
                if (view.Current == null || activity.LastFocused > view.Current.LastFocused ||
                    activity.LastFocused == view.Current.LastFocused && activity.Started > view.Current.Started) view.Current = activity;
            }
            foreach (var key in new List<string>(sessions.Keys)) if (!live.Contains(key)) sessions.Remove(key);
            return view;
        }
    }
}
