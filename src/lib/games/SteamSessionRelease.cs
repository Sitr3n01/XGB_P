// Estado da sessao Steam independente da presenca do processo nativo no catalogo.
using System;
using System.Collections.Generic;

namespace XGBP.Games
{
    public sealed class SteamReleaseSnapshot
    {
        public bool Readable, CaughtUp;
        public DateTime Utc;
        public string Error;
        public readonly List<string> Pending = new List<string>();
        public readonly List<string> Confirmed = new List<string>();
        public bool Ready { get { return Readable && CaughtUp && Error == null && Pending.Count == 0; } }
    }
    public sealed class SteamSessionRelease
    {
        sealed class Session
        {
            public string Command, Name;
            public DateTime Started;
            public bool Active, HasStart, Ambiguous;
            public readonly HashSet<uint> Pids = new HashSet<uint>(), OldPids = new HashSet<uint>();
            public readonly HashSet<string> Instances = new HashSet<string>(), Released = new HashSet<string>();
        }
        readonly string path;
        readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();
        readonly HashSet<string> monitored = new HashSet<string>();
        SteamSessionLog reader;
        string client;
        bool lost, bootstrapped;
        public SteamSessionRelease(string path) { this.path = path; }
        public SteamReleaseSnapshot Update(IList<GameProcess> processes, GamesView view, DateTime utc)
        {
            var steam = new List<GameProcess>();
            foreach (var process in processes) if (string.Equals(process.Name, "steam.exe", StringComparison.OrdinalIgnoreCase)) steam.Add(process);
            var result = new SteamReleaseSnapshot { Utc = utc };
            if (steam.Count == 0)
            {
                sessions.Clear(); monitored.Clear(); client = null; reader = null; lost = bootstrapped = false;
                result.Readable = result.CaughtUp = true; return result;
            }
            steam.Sort(delegate(GameProcess a, GameProcess b) { return a.Pid.CompareTo(b.Pid); });
            string generation = ""; DateTime since = utc;
            foreach (var process in steam)
            {
                if (process.Created <= 0) { result.Error = "Não foi possível confirmar a instância da Steam."; return result; }
                generation += process.Instance + ";";
                var started = DateTime.FromFileTimeUtc(process.Created); if (started < since) since = started;
            }
            // O cliente novo nao herda eventos ou autorizacoes do anterior.
            if (client != generation)
            { client = generation; reader = new SteamSessionLog(path); sessions.Clear(); monitored.Clear(); lost = bootstrapped = false; }
            foreach (var entry in view.Entries)
            {
                string app = SteamGameContext.AppId(entry.Key);
                if (entry.Kind == "Game" && app != null) monitored.Add(app);
            }
            var batch = reader.Read();
            if (batch.Reset)
            {
                bootstrapped = false;
                foreach (var session in sessions.Values) if (session.Active) lost = true;
                if (!lost) sessions.Clear();
            }
            // O horario do log tem precisao de segundos; eventos de outro cliente sao descartados.
            since = new DateTime(since.Ticks - since.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
            foreach (var e in batch.Events)
            {
                if (lost) break;
                if (e.Utc < since || !monitored.Contains(e.AppId)) continue;
                Session session;
                if (!sessions.TryGetValue(e.AppId, out session))
                {
                    if (sessions.Count >= 256) { lost = true; break; }
                    sessions[e.AppId] = session = new Session { Name = e.AppId };
                }
                if (e.Action == "added")
                {
                    if (session.Active && session.HasStart) session.Ambiguous = true;
                    if (!session.Active) session.OldPids.Clear();
                    foreach (uint pid in session.Pids) session.OldPids.Add(pid);
                    if (!session.Active) { session.Pids.Clear(); session.Instances.Clear(); session.Released.Clear(); }
                    session.Command = e.Command; session.Started = e.Utc; session.HasStart = session.Active = true;
                    session.Pids.Add(e.Pid);
                }
                else if (e.Action == "updated" && session.Active && session.Command == e.Command && e.Utc >= session.Started)
                    session.Pids.Add(e.Pid);
                else if (e.Action == "removed" && session.Active && !session.Ambiguous && e.Utc >= session.Started &&
                    (session.Command == null || session.Command == e.Command) &&
                    (session.Pids.Contains(e.Pid) || session.Instances.Count == 0 && !session.OldPids.Contains(e.Pid)))
                {
                    session.Active = false;
                    foreach (var token in session.Instances) session.Released.Add(token);
                    session.Pids.Add(e.Pid);
                    if (bootstrapped) result.Confirmed.Add(e.AppId + " | PID=" + e.Pid + " | eventoUtc=" + e.Utc.ToString("o"));
                }
                if (session.Pids.Count + session.OldPids.Count + session.Instances.Count + session.Released.Count > 256) lost = true;
            }
            // Nao misturar o snapshot atual com eventos historicos ainda em carregamento.
            if (batch.CaughtUp) foreach (var activity in view.Activities)
            {
                if (lost) break;
                string app = SteamGameContext.AppId(activity.Key); if (app == null || !monitored.Contains(app)) continue;
                var root = activity.Processes.Find(p => p.Pid == activity.Pid && p.Known);
                if (root == null) { result.Error = "Identidade do jogo da Steam ainda não confirmada."; continue; }
                Session session;
                if (!sessions.TryGetValue(app, out session))
                {
                    if (sessions.Count >= 256) { lost = true; break; }
                    sessions[app] = session = new Session { Name = app };
                }
                string token = root.Pid + ":" + root.Created;
                if (session.Released.Contains(token)) continue;
                if (!session.Active)
                {
                    session.Active = true; session.HasStart = false; session.Command = null;
                    var started = DateTime.FromFileTimeUtc(root.Created);
                    session.Started = new DateTime(started.Ticks - started.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc); session.Pids.Clear();
                }
                // Remocao so com PID do jogo observado ou comando da unica sessao reconstruida no bootstrap.
                session.Instances.Add(token); session.Pids.Add(root.Pid);
                if (session.Pids.Count + session.OldPids.Count + session.Instances.Count + session.Released.Count > 256) lost = true;
                var entry = view.Find(activity.Key); if (entry != null) session.Name = entry.Name;
            }
            foreach (var item in sessions)
                if (item.Value.Active) result.Pending.Add(item.Value.Name);
            result.Readable = batch.Readable; result.CaughtUp = batch.CaughtUp;
            if (batch.Readable && batch.CaughtUp) bootstrapped = true;
            if (lost) result.Error = "O log da Steam mudou durante uma sessão; não é possível confirmar o encerramento.";
            else if (batch.Error != null) result.Error = batch.Error;
            return result;
        }
    }
}
