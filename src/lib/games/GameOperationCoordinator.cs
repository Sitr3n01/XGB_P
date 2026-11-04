// Ciclo de vida das acoes nativas: encerrar cancela a espera e sincroniza a recuperacao.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace XGBP.Games
{
    public sealed class PausedGame
    {
        public uint Pid;
        public Dictionary<uint, long> Members = new Dictionary<uint, long>();
    }

    // Enumeracao sempre por copia; callbacks de erro e workers nao enumeram o Dictionary da UI.
    public sealed class PausedGameRegistry : IEnumerable<KeyValuePair<string, PausedGame>>
    {
        readonly object gate = new object();
        readonly Dictionary<string, PausedGame> entries = new Dictionary<string, PausedGame>();
        public PausedGame this[string key]
        {
            get { lock (gate) return entries[key]; }
            set { lock (gate) entries[key] = value; }
        }
        public ICollection<string> Keys { get { lock (gate) return new List<string>(entries.Keys); } }
        public int Count { get { lock (gate) return entries.Count; } }
        public bool ContainsKey(string key) { lock (gate) return entries.ContainsKey(key); }
        public bool TryGetValue(string key, out PausedGame value) { lock (gate) return entries.TryGetValue(key, out value); }
        public bool Remove(string key) { lock (gate) return entries.Remove(key); }
        public void Clear() { lock (gate) entries.Clear(); }
        public IEnumerator<KeyValuePair<string, PausedGame>> GetEnumerator()
        { lock (gate) return new List<KeyValuePair<string, PausedGame>>(entries).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public sealed class GameOperationCoordinator : IDisposable
    {
        readonly object gate = new object();
        readonly object cancelGate = new object();
        readonly ManualResetEvent cancel = new ManualResetEvent(false);
        volatile bool stopped;
        bool released;

        public bool Stopped { get { return stopped; } }
        public void Delay(int milliseconds)
        {
            if (cancel.WaitOne(milliseconds)) throw new OperationCanceledException("A barra esta encerrando.");
        }
        public string Execute(Func<string> action)
        {
            lock (gate)
            {
                if (stopped) throw new OperationCanceledException("A barra esta encerrando.");
                // O worker registra a pausa dentro desta transacao, antes de publicar qualquer callback.
                return action();
            }
        }
        public void Stop(Action recover)
        {
            lock (cancelGate) { stopped = true; if (!released) cancel.Set(); }
            lock (gate) recover();
        }
        public void Dispose()
        {
            Stop(delegate { });
            lock (cancelGate) { if (!released) { released = true; cancel.Dispose(); } }
        }
    }
}
