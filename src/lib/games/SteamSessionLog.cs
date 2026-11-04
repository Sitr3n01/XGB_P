// Leitura incremental de eventos de sessao. Nenhuma escrita ou protocolo da Steam.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XGBP.Games
{
    public sealed class SteamSessionEvent
    {
        public string AppId, Command, Action;
        public uint Pid;
        public DateTime Utc;
        public static SteamSessionEvent Parse(string line)
        {
            const string prefix = "] Game process ";
            if (line.Length < 23 || line[0] != '[' || line.IndexOf(prefix, StringComparison.Ordinal) != 20) return null;
            DateTime time;
            if (!DateTime.TryParseExact(line.Substring(1, 19), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out time)) return null;
            int actionAt = 20 + prefix.Length, colon = line.IndexOf(':', actionAt);
            if (colon < 0) return null;
            string action = line.Substring(actionAt, colon - actionAt).Trim();
            if (action != "added" && action != "updated" && action != "removed") return null;
            int appAt = colon + 1;
            while (appAt < line.Length && line[appAt] == ' ') appAt++;
            if (!line.Substring(appAt).StartsWith("AppID ", StringComparison.Ordinal)) return null;
            appAt += 6;
            int end = line.IndexOf(' ', appAt), pidAt = line.LastIndexOf(", ProcID ", StringComparison.Ordinal);
            long id;
            if (end < 0 || pidAt <= end || !long.TryParse(line.Substring(appAt, end - appAt), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out id) || id < int.MinValue || id > uint.MaxValue || id == 0) return null;
            string pidText = line.Substring(pidAt + 9).Trim(); int digits = 0;
            while (digits < pidText.Length && char.IsDigit(pidText[digits])) digits++;
            uint pid;
            if (digits == 0 || !uint.TryParse(pidText.Substring(0, digits), out pid) || pid == 0) return null;
            if (digits < pidText.Length && pidText[digits] != ',' && pidText[digits] != ' ') return null;
            string command = line.Substring(end, pidAt - end).Trim();
            if (command.Length < 2 || command[0] != '"' || command[command.Length - 1] != '"') return null;
            return new SteamSessionEvent { AppId = unchecked((uint)id).ToString(CultureInfo.InvariantCulture), Pid = pid,
                Command = command, Action = action, Utc = time.ToUniversalTime() };
        }
    }
    public sealed class SteamSessionBatch
    {
        public readonly List<SteamSessionEvent> Events = new List<SteamSessionEvent>();
        public bool Readable, CaughtUp, Reset;
        public string Error;
    }
    public sealed class SteamSessionLog
    {
        const int BatchLimit = 256 * 1024, LineLimit = 32768;
        readonly string path;
        long position;
        string identity;
        byte[] partial = new byte[0];
        public SteamSessionLog(string path) { this.path = path; }
        public SteamSessionBatch Read()
        {
            var batch = new SteamSessionBatch();
            if (string.IsNullOrEmpty(path)) { batch.Error = "Caminho do log da Steam indisponível."; return batch; }
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    FileInformation info;
                    if (!GetFileInformationByHandle(file.SafeFileHandle, out info)) throw new IOException("Identidade do log indisponível.");
                    string current = info.Volume + ":" + info.IndexHigh + ":" + info.IndexLow + ":" + info.CreationHigh + ":" + info.CreationLow;
                    if (identity != null && (identity != current || file.Length < position))
                    { batch.Reset = true; position = 0; partial = new byte[0]; }
                    identity = current;
                    int count = (int)Math.Min(BatchLimit, file.Length - position);
                    var bytes = new byte[partial.Length + count]; Array.Copy(partial, bytes, partial.Length);
                    file.Position = position; int read = 0;
                    while (read < count) { int n = file.Read(bytes, partial.Length + read, count - read); if (n == 0) break; read += n; }
                    int length = partial.Length + read, start = 0;
                    if (position - partial.Length == 0 && length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191) start = 3;
                    for (int i = 0; i < length; i++) if (bytes[i] == 10)
                    {
                        if (i - start > LineLimit) throw new IOException("Linha do log excedeu o limite de leitura.");
                        string line = Encoding.UTF8.GetString(bytes, start, i - start).TrimEnd('\r');
                        var item = SteamSessionEvent.Parse(line);
                        if (item != null) batch.Events.Add(item);
                        start = i + 1;
                    }
                    if (length - start > LineLimit) throw new IOException("Linha incompleta do log excedeu o limite.");
                    var remainder = new byte[length - start]; Array.Copy(bytes, start, remainder, 0, remainder.Length);
                    position += read; partial = remainder;
                    batch.Readable = true; batch.CaughtUp = position == file.Length && partial.Length == 0;
                }
            }
            catch (IOException ex) { batch.Events.Clear(); batch.Error = ex.Message; }
            catch (UnauthorizedAccessException ex) { batch.Events.Clear(); batch.Error = ex.Message; }
            return batch;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct FileInformation
        {
            public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    }
}
