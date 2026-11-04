// Confirmacao nova do Steam Input: identidade do arquivo, prefixo preservado e linhas completas.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XGBP.Games
{
    public static class SteamInputObservation
    {
        // Foco e carga de perfil sao estados distintos. Guia -> desktop pode concluir a carga
        // sem outro evento de foco. Uma carga isolada nunca estabelece desktop, nem desfaz
        // uma mudanca real para jogo/ClientUI; isso exige novo evento de foco desktop.
        public static string LastContext(string text)
        { return LastContext(text, SteamInput.DesktopAppId); }
        public static string LastContext(string text, string expected)
        {
            if (text == null) return null;
            int complete = text.LastIndexOf('\n'); if (complete < 0 || complete != text.Length - 1) return null;
            string selected = null, loaded = null; bool divergentLoad = false;
            foreach (string raw in text.Substring(0, complete).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                bool focus = line.IndexOf("OnFocusWindowChanged ", StringComparison.Ordinal) >= 0;
                bool loadEvent = line.IndexOf("Loaded Config", StringComparison.Ordinal) >= 0;
                if (!focus && !loadEvent) continue;
                int at = line.LastIndexOf("AppID ", StringComparison.Ordinal), skip = 6;
                if (at < 0) { at = line.LastIndexOf("App ID ", StringComparison.Ordinal); skip = 7; }
                if (at < 0 && focus && line.IndexOf("URL Forcing to window type:", StringComparison.Ordinal) >= 0)
                { at = line.LastIndexOf(','); skip = 1; }
                if (at < 0) continue;
                string value = line.Substring(at + skip).Trim();
                int end = value.StartsWith("-", StringComparison.Ordinal) ? 1 : 0;
                while (end < value.Length && char.IsDigit(value[end])) end++;
                long id;
                if (!long.TryParse(value.Substring(0, end), out id) || id < int.MinValue || id > uint.MaxValue) continue;
                string normalized = unchecked((uint)id).ToString();
                if (focus) { selected = normalized; loaded = null; divergentLoad = false; }
                else
                {
                    loaded = normalized;
                    if (normalized != expected && normalized != SteamInput.GuideAppId) divergentLoad = true;
                }
            }
            if (selected != expected) return selected;
            if (divergentLoad) return null;
            return loaded == null || loaded == expected ? selected : loaded;
        }
        public static bool Executed(string text, string uri)
        {
            if (text == null) return false;
            int complete = text.LastIndexOf('\n');
            return complete >= 0 && text.Substring(0, complete).Contains(SteamInput.Executed(uri));
        }
    }

    public sealed class SteamInputLogCursor
    {
        const int Limit = 256 * 1024;
        readonly string path;
        readonly long start;
        readonly byte[] anchor;
        readonly FileIdentity identity;
        readonly bool skipPartial;
        bool invalid;
        public SteamInputLogCursor(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new IOException("Log da Steam indisponivel.");
            this.path = path;
            using (var file = Open())
            {
                identity = Identify(file); start = file.Length;
                anchor = new byte[Math.Min(128L, start)];
                file.Position = start - anchor.Length; ReadAll(file, anchor);
                skipPartial = anchor.Length > 0 && anchor[anchor.Length - 1] != 10;
            }
        }
        FileStream Open() { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        public string ReadNew()
        {
            if (invalid) throw new IOException("O log da Steam mudou durante a confirmacao.");
            try
            {
                using (var file = Open())
                {
                    var current = Identify(file);
                    if (!identity.Same(current) || file.Length < start || file.Length - start > Limit)
                        throw new IOException("Log da Steam rotacionado, truncado ou fora do limite.");
                    file.Position = start - anchor.Length; var check = new byte[anchor.Length]; ReadAll(file, check);
                    for (int i = 0; i < anchor.Length; i++) if (check[i] != anchor[i]) throw new IOException("Log da Steam reescrito.");
                    var buffer = new byte[file.Length - start]; file.Position = start; ReadAll(file, buffer);
                    string text = Encoding.UTF8.GetString(buffer); int end = text.LastIndexOf('\n');
                    if (end < 0 || end != text.Length - 1) return "";
                    int first = skipPartial ? text.IndexOf('\n') + 1 : 0;
                    return first > end ? "" : text.Substring(first, end + 1 - first);
                }
            }
            catch { invalid = true; throw; }
        }
        static void ReadAll(FileStream file, byte[] bytes)
        {
            int read = 0;
            while (read < bytes.Length) { int count = file.Read(bytes, read, bytes.Length - read); if (count == 0) throw new IOException("Log da Steam mudou durante a leitura."); read += count; }
        }
        [StructLayout(LayoutKind.Sequential)]
        struct FileIdentity
        {
            public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
            public bool Same(FileIdentity other) { return Volume == other.Volume && IndexHigh == other.IndexHigh && IndexLow == other.IndexLow; }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileIdentity info);
        static FileIdentity Identify(FileStream file)
        {
            FileIdentity value; if (!GetFileInformationByHandle(file.SafeFileHandle, out value)) throw new IOException("Identidade do log indisponivel."); return value;
        }
    }
}
