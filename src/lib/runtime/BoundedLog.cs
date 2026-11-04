// Log com fila limitada e I/O numa thread propria; produtores nunca aguardam disco.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace XGBP.Runtime
{
    public sealed class BoundedLog : IDisposable
    {
        readonly object gate = new object();
        readonly Queue<string> queue = new Queue<string>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ManualResetEvent idle = new ManualResetEvent(true);
        readonly Action<string> append;
        readonly int capacity;
        readonly Thread worker;
        bool stopped;
        int dropped, failures;
        public int Dropped { get { lock (gate) return dropped; } }
        public int Failures { get { lock (gate) return failures; } }
        public int Pending { get { lock (gate) return queue.Count; } }
        public BoundedLog(string path) : this(batch => AppendRotating(path, batch, 1024 * 1024, 3), 2048) { }
        internal BoundedLog(Action<string> append, int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            this.append = append; this.capacity = capacity;
            worker = new Thread(Drain) { IsBackground = true, Name = "XGBP log" };
            worker.Start();
        }
        public void Write(string text)
        {
            lock (gate)
            {
                if (stopped) return;
                if (queue.Count >= capacity) { dropped++; return; }
                if (text == null) text = "";
                if (text.Length > 8192) text = text.Substring(0, 8192) + " [truncado]";
                queue.Enqueue(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + text + Environment.NewLine);
                idle.Reset(); wake.Set();
            }
        }
        void Drain()
        {
            while (true)
            {
                string batch;
                lock (gate)
                {
                    if (queue.Count == 0)
                    {
                        idle.Set();
                        if (stopped) return;
                        batch = null;
                    }
                    else
                    {
                        var text = new StringBuilder();
                        for (int n = 0; n < 64 && queue.Count > 0 && text.Length < 32768; n++) text.Append(queue.Dequeue());
                        batch = text.ToString();
                    }
                }
                if (batch == null) { wake.WaitOne(); continue; }
                try { append(batch); }
                catch (Exception) { lock (gate) failures++; }
            }
        }
        public bool Flush(int timeoutMs) { return idle.WaitOne(timeoutMs); }
        public void Dispose()
        {
            lock (gate) { if (stopped) return; stopped = true; wake.Set(); }
            if (worker.Join(2000)) { wake.Dispose(); idle.Dispose(); }
        }
        internal static void AppendRotating(string path, string batch, long maxBytes, int retained)
        {
            string identity;
            using (var sha = SHA256.Create())
                identity = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-", "");
            using (var mutex = new Mutex(false, "Local\\XGBPLog-" + identity))
            {
                bool owned = false;
                try
                {
                    try { owned = mutex.WaitOne(1000); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) throw new IOException("Log ocupado por outra instancia.");
                    AppendCore(path, batch, maxBytes, retained);
                }
                finally { if (owned) mutex.ReleaseMutex(); }
            }
        }
        static void AppendCore(string path, string batch, long maxBytes, int retained)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(batch) > maxBytes)
            {
                string oldest = path + "." + retained;
                if (File.Exists(oldest)) File.Delete(oldest);
                for (int n = retained - 1; n >= 1; n--)
                    if (File.Exists(path + "." + n)) File.Move(path + "." + n, path + "." + (n + 1));
                File.Move(path, path + ".1");
            }
            File.AppendAllText(path, batch, Encoding.UTF8);
        }
    }
}
