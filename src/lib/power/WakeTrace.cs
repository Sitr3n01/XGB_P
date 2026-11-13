// Correlacao de retomadas: somente leitura do log Windows; nunca arma dispositivos ou timers de wake.
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XGBP.Tv;

namespace XGBP.Power
{
    [DataContract] public sealed class WindowsWakeEvent
    {
        [DataMember] public long RecordId;
        [DataMember] public int SourceType;
        [DataMember] public DateTimeOffset SleepAt, WakeAt;
        [DataMember] public string SourceText, TimerOwner, TimerContext, RawXml;
    }
    [DataContract] public sealed class WakeTraceRecord
    {
        [DataMember] public int SchemaVersion = 1;
        [DataMember] public ResumeSession Session;
        [DataMember] public WindowsWakeEvent Windows;
        [DataMember] public DateTimeOffset UpdatedAt;
        [DataMember] public string Classification, EventReadError;
        [DataMember] public bool ObservationComplete;
        [DataMember] public int EventReadAttempts;
        // Mesmo um PS valido depois da retomada nao comprova qual sinal fisico acordou o PC.
        [DataMember] public bool PhysicalCauseConfirmed = false;
        public WakeTraceRecord Copy()
        { var r = (WakeTraceRecord)MemberwiseClone(); r.Session = Session.Copy(); r.Classification = Classify(r); return r; }
        public static string Classify(WakeTraceRecord r)
        {
            if (r.Windows != null && r.Windows.SourceType != 0) return "WindowsReportedSource";
            string source = r.Windows == null ? "WindowsEventUnavailable" : "WindowsUnknown";
            return source + (r.Session.PsConsumed ? "PsObserved" : "NoPsObserved");
        }
        public string Summary()
        {
            string source = Windows == null ? "Origem do Windows ainda indisponível" : Windows.SourceType == 0 ? "Windows: Unknown" :
                Windows.SourceType == 1 ? "Windows: botão de energia" : !string.IsNullOrEmpty(Windows.SourceText) ? "Windows: " +
                    Windows.SourceText : "Windows: tipo " +
                    Windows.SourceType;
            string ps = Session.PsConsumed ? "PS observado" + (Session.ResumeToPsMs.HasValue ? " (" + Session.ResumeToPsMs.Value.ToString("0",
                CultureInfo.InvariantCulture) + " ms)" : "") : "sem PS observado";
            return source + " · " + ps + " · registro " + Session.CycleId.Substring(0, Math.Min(8, Session.CycleId.Length));
        }
    }
    public static class WindowsWakeReader
    {
        public static bool Matches(WindowsWakeEvent e, ResumeSession session)
        {
            return e != null && session.ResumedAt.HasValue && e.RecordId > 0 &&
                Math.Abs((e.WakeAt - session.ResumedAt.Value).TotalSeconds) <= 30 &&
                (!session.SuspendedAt.HasValue || Math.Abs((e.SleepAt - session.SuspendedAt.Value).TotalSeconds) <= 10);
        }
        public static WindowsWakeEvent Parse(string xml, long recordId)
        {
            var document = new XmlDocument { XmlResolver = null }; document.LoadXml(xml);
            var values = new Dictionary<string, string>();
            foreach (XmlNode n in document.SelectNodes("/*[local-name()='Event']/*[local-name()='EventData']/*[local-name()='Data']"))
            { var name = n.Attributes["Name"]; if (name != null) values[name.Value] = n.InnerText; }
            Func<string, string> get = key => values.ContainsKey(key) ? values[key] : "";
            int source;
            if (!int.TryParse(get("WakeSourceType"), NumberStyles.Integer, CultureInfo.InvariantCulture, out source)) throw new IOException(
                "Evento de retomada sem tipo de origem válido.");
            return new WindowsWakeEvent { RecordId = recordId, SourceType = source,
                SleepAt = DateTimeOffset.Parse(get("SleepTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                WakeAt = DateTimeOffset.Parse(get("WakeTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                SourceText = get("WakeSourceText"), TimerOwner = get("WakeTimerOwner"), TimerContext = get("WakeTimerContext"), RawXml = xml };
        }
        public static WindowsWakeEvent Read(ResumeSession session)
        {
            var query = new EventLogQuery("System", PathType.LogName, "*[System[Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1]]") {
                ReverseDirection = true };
            WindowsWakeEvent closest = null; double distance = double.MaxValue;
            using (var reader = new EventLogReader(query))
                for (int i = 0; i < 16; i++)
                    using (var entry = reader.ReadEvent(TimeSpan.FromSeconds(2)))
                    {
                        if (entry == null) break;
                        var e = Parse(entry.ToXml(), entry.RecordId ?? 0);
                        if (!Matches(e, session)) continue;
                        double d = Math.Abs((e.WakeAt - session.ResumedAt.Value).TotalMilliseconds) +
                            (session.SuspendedAt.HasValue ? Math.Abs((e.SleepAt - session.SuspendedAt.Value).TotalMilliseconds) : 0);
                        if (d < distance) { closest = e; distance = d; }
                    }
            return closest;
        }
    }
    public sealed class WakeTraceJournal
    {
        readonly object gate = new object();
        readonly Dictionary<string, WakeTraceRecord> records = new Dictionary<string, WakeTraceRecord>();
        readonly Dictionary<string, string> eventOwners = new Dictionary<string, string>();
        readonly HashSet<string> started = new HashSet<string>();
        readonly Func<ResumeSession, WindowsWakeEvent> read;
        readonly Action<WakeTraceRecord> write, changed;
        readonly Action<string> log;
        readonly Func<int, Task> delay;
        WakeTraceRecord latest;
        public WakeTraceJournal(Func<ResumeSession, WindowsWakeEvent> read, Action<WakeTraceRecord> write,
            Action<WakeTraceRecord> changed, Action<string> log, Func<int, Task> delay)
        { this.read = read; this.write = write; this.changed = changed; this.log = log; this.delay = delay; }
        public WakeTraceRecord Latest { get { lock (gate) return latest == null ? null : latest.Copy(); } }
        // Chamado pelo worker do diario, nunca por uma API de energia ou pelo driver.
        public void Observe(ResumeSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.CycleId)) return;
            bool begin;
            lock (gate)
            {
                WakeTraceRecord r;
                if (!records.TryGetValue(session.CycleId, out r)) records.Add(session.CycleId, r = new WakeTraceRecord());
                var merged = session.Copy();
                if (r.Session != null)
                {
                    if (!merged.ResumedAt.HasValue) merged.ResumedAt = r.Session.ResumedAt;
                    if (r.Session.PsConsumed && !merged.PsConsumed)
            { merged.PsConsumed = true; merged.PsAt = r.Session.PsAt; merged.PsDevice = r.Session.PsDevice; merged.ResumeToPsMs = r.Session.ResumeToPsMs; }
                }
                r.Session = merged; Persist(r);
                begin = merged.ResumedAt.HasValue && started.Add(session.CycleId);
            }
            if (begin) ReadLater(session.CycleId);
        }
        async void ReadLater(string id)
        {
            try
            {
                int waited = 0;
                foreach (int wait in new[] { 0, 2000, 8000, 20000 })
                {
                    if (wait > 0) { await delay(wait).ConfigureAwait(false); waited += wait; }
                    ResumeSession session; lock (gate) session = records[id].Session.Copy();
                    WindowsWakeEvent e = null; string error = null;
                    try { e = read(session); } catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
                    bool accepted;
                    lock (gate)
                    {
                        var r = records[id]; r.EventReadAttempts++; r.EventReadError = error;
                        string owner;
                        string eventKey = e == null ? "" : e.RecordId + ":" + e.WakeAt.UtcTicks;
                        accepted = WindowsWakeReader.Matches(e, r.Session) && (!eventOwners.TryGetValue(eventKey, out owner) || owner == id);
                        if (accepted) { r.Windows = e; eventOwners[eventKey] = id; }
                        Persist(r);
                    }
                    if (accepted) break;
                }
                // Preserva o fim da janela de PS mesmo quando o evento Windows chega imediatamente.
                if (waited < 20000) await delay(20000 - waited).ConfigureAwait(false);
                lock (gate) { records[id].ObservationComplete = true; Persist(records[id]); }
            }
            catch (Exception ex) { if (log != null) log("despertar: falha no registro " + id + " | " + ex.GetType().Name); }
        }
        void Persist(WakeTraceRecord record)
        {
            record.UpdatedAt = DateTimeOffset.UtcNow;
            var copy = record.Copy();
            // Cada ciclo tem seu arquivo: uma tarefa atrasada nao substitui a retomada mais nova.
            write(copy);
            if (copy.Session.ResumedAt.HasValue && (latest == null || copy.Session.ResumedAt.Value >= latest.Session.ResumedAt.Value))
            { latest = copy; if (changed != null) changed(copy.Copy()); }
        }
        public static WakeTraceJournal Open(string folder, Action<WakeTraceRecord> changed, Action<string> log)
        {
            string full = Path.GetFullPath(folder);
            var journal = new WakeTraceJournal(WindowsWakeReader.Read, delegate(WakeTraceRecord record) {
                Guid id; if (!Guid.TryParseExact(record.Session.CycleId, "N", out id)) throw new IOException("Identificador de retomada inválido.");
                TvJson.Write(Path.Combine(full, id.ToString("N") + ".json"), record, CancellationToken.None, false);
            }, changed, log, ms => Task.Delay(ms));
            try
            {
                if (Directory.Exists(full))
                {
                    var files = new List<FileInfo>(new DirectoryInfo(full).GetFiles("*.json"));
                    files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                    foreach (var file in files.GetRange(0, Math.Min(128, files.Count)))
                    {
                        Guid id; if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out id)) continue;
                        var record = TvJson.Read<WakeTraceRecord>(file.FullName);
                        if (record == null || record.SchemaVersion != 1 || record.Session == null || record.Session.CycleId != id.ToString("N") ||
                            !record.Session.ResumedAt.HasValue) continue;
                        if (journal.latest == null || record.Session.ResumedAt > journal.latest.Session.ResumedAt) journal.latest = record;
                    }
                }
            }
            catch (Exception ex) { if (log != null) log("despertar: historico anterior indisponivel | " + ex.GetType().Name); }
            return journal;
        }
    }
}
