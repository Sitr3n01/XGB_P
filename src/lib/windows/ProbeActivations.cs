// Ponte temporaria da sonda ja instalada: acompanha somente novas ativacoes Win+G no log local.
// Nao altera pacote, captura teclado, bloqueia teclas nem envia input. O consumidor aplica o filtro FSE.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace XGBP.Desktop
{
    public sealed class ProbeActivationTail
    {
        readonly string path;
        readonly Func<DateTime> now;
        readonly MemoryStream line = new MemoryStream();
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Queue<string> recent = new Queue<string>();
        long position;
        DateTime created;
        bool discard;
        public ProbeActivationTail(string path, Func<DateTime> now)
        {
            this.path = path; this.now = now;
            var file = new FileInfo(path);
            if (file.Exists) { position = file.Length; created = file.CreationTimeUtc; }
        }
        public static bool TryParse(string text, out DateTime time, out string identity)
        {
            time = DateTime.MinValue; identity = null;
            if (string.IsNullOrEmpty(text) || text.Length < 30 || text.Length > 65536 ||
                !DateTime.TryParseExact(text.Substring(0, 23), "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return false;
            int pidStart = text.IndexOf(" | pid=", StringComparison.Ordinal), pidEnd = pidStart < 0 ? -1 : text.IndexOf(" |", pidStart + 7, StringComparison.Ordinal);
            int pid;
            if (pidStart != 23 || pidEnd < 0 || !int.TryParse(text.Substring(pidStart + 7, pidEnd - pidStart - 7), out pid) || pid <= 0) return false;
            const string marker = " | args(1)=";
            int begin = text.IndexOf(marker, pidEnd, StringComparison.Ordinal), end = begin < 0 ? -1 : text.IndexOf(" | cmd=", begin, StringComparison.Ordinal);
            if (begin < 0 || end < 0) return false;
            Uri uri;
            if (!Uri.TryCreate(text.Substring(begin + marker.Length, end - begin - marker.Length), UriKind.Absolute, out uri) ||
                uri.Scheme != "ms-gamebar" || uri.Host != "hotkey" || uri.Fragment != "" || (uri.AbsolutePath != "" && uri.AbsolutePath != "/")) return false;
            bool guide = false;
            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(parts[0]);
                if (key == "gamepadCommand") return false;
                if (key != "ihkid") continue;
                if (guide || parts.Length != 2 || Uri.UnescapeDataString(parts[1]) != "IHKID_GAME_GUIDE_OVERLAY") return false;
                guide = true;
            }
            if (!guide) return false;
            identity = text.Substring(0, pidEnd); return true;
        }
        public int ReadAvailable()
        {
            var file = new FileInfo(path);
            if (!file.Exists) return 0;
            if (file.Length < position || created != file.CreationTimeUtc) { position = 0; line.SetLength(0); discard = false; }
            created = file.CreationTimeUtc;
            int count = 0;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Position = position;
                var buffer = new byte[8192]; int budget = 65536, n;
                while (budget > 0 && (n = stream.Read(buffer, 0, Math.Min(buffer.Length, budget))) > 0)
                {
                    position += n; budget -= n;
                    for (int i = 0; i < n; i++)
                    {
                        byte value = buffer[i];
                        if (value != 10)
                        {
                            if (!discard) { if (line.Length >= 65536) { line.SetLength(0); discard = true; } else line.WriteByte(value); }
                            continue;
                        }
                        if (!discard)
                        {
                            DateTime time; string identity;
                            string text = Encoding.UTF8.GetString(line.ToArray()).TrimEnd('\r').TrimStart('\uFEFF');
                            if (TryParse(text, out time, out identity) && (now() - time).TotalSeconds >= -1 && (now() - time).TotalSeconds <= 5 && seen.Add(identity))
                            {
                                recent.Enqueue(identity); if (recent.Count > 64) seen.Remove(recent.Dequeue()); count++;
                            }
                        }
                        line.SetLength(0); discard = false;
                    }
                }
            }
            return count;
        }
    }
    public sealed class ProbeActivationBridge : IDisposable
    {
        readonly object gate = new object();
        readonly ProbeActivationTail tail;
        readonly Action activate;
        readonly Action<string> log;
        Timer timer;
        bool stopped;
        string error;
        public ProbeActivationBridge(string path, Action activate, Action<string> log)
        {
            tail = new ProbeActivationTail(path, delegate { return DateTime.Now; }); this.activate = activate; this.log = log;
            timer = new Timer(delegate { Poll(); }, null, 100, 100);
        }
        void Poll()
        {
            lock (gate)
            {
                if (stopped) return;
                try
                {
                    int count = tail.ReadAvailable(); error = null;
                    for (int i = 0; i < count; i++) activate();
                }
                catch (Exception ex) { if (error != ex.Message) { error = ex.Message; log("ponte da sonda: " + error); } }
            }
        }
        public void Dispose()
        {
            lock (gate) { stopped = true; if (timer != null) { timer.Dispose(); timer = null; } }
        }
    }
}
