// Historico atomico com pendencia explicita e retry limitado por backoff; JSON corrompido nunca e sobrescrito.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace XGBP.Games
{
    [DataContract]
    public sealed class GameHistoryRecord
    {
        [DataMember] public string Key;
        [DataMember] public long LastObserved;
    }
    [DataContract]
    public sealed class GameHistoryData
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<GameHistoryRecord> Records = new List<GameHistoryRecord>();
    }
    public sealed class GameHistory
    {
        readonly object gate = new object();
        readonly string path;
        readonly Action<string, GameHistoryData> write;
        readonly Dictionary<string, long> records = new Dictionary<string, long>();
        bool writable = true, dirty;
        int failures;
        DateTime retryAt = DateTime.MinValue;
        string error;
        public string Error { get { lock (gate) return error; } }
        public bool Pending { get { lock (gate) return dirty; } }
        public bool Writable { get { lock (gate) return writable; } }
        public GameHistory(string path) : this(path, (p, data) => GameFiles.Write(p, data)) { }
        internal GameHistory(string path, Action<string, GameHistoryData> write)
        {
            this.path = path; this.write = write;
            try
            {
                var data = GameFiles.Read<GameHistoryData>(path);
                if (data == null || data.Version != 1) throw new InvalidDataException("Versao de historico nao suportada.");
                foreach (var r in data.Records ?? new List<GameHistoryRecord>())
                    if (r != null && !string.IsNullOrEmpty(r.Key) && GameFiles.ValidTime(r.LastObserved.ToString(), DateTime.UtcNow) > 0)
                        records[r.Key] = r.LastObserved;
            }
            catch (Exception ex) { writable = false; error = "Historico local preservado sem escrita: " + ex.Message; }
        }
        public long Last(string key) { lock (gate) { long value; return records.TryGetValue(key, out value) ? value : 0; } }
        public void Observe(string key, DateTime now)
        {
            lock (gate)
            {
                records[key] = GameFiles.Epoch(now);
                dirty = true;
                Flush(now, false);
            }
        }
        public bool Flush(DateTime now, bool force)
        {
            lock (gate)
            {
                if (!dirty || !writable || !force && now < retryAt) return !dirty;
                try
                {
                    var data = new GameHistoryData();
                    foreach (var pair in records) data.Records.Add(new GameHistoryRecord { Key = pair.Key, LastObserved = pair.Value });
                    write(path, data);
                    dirty = false; error = null; failures = 0;
                    return true;
                }
                catch (Exception ex)
                {
                    failures = Math.Min(5, failures + 1);
                    retryAt = now.AddSeconds(Math.Min(30, 1 << failures));
                    error = "Historico pendente de gravacao; nova tentativa automatica: " + ex.Message;
                    return false;
                }
            }
        }
    }
}
