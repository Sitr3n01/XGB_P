// Midia em reproducao no Windows (SMTC): leitura e comandos pela API publica, sem hooks ou injecao.
// C# 5 / .NET Framework, x64. Metadados WinRT de %WINDIR%\System32\WinMetadata, sem SDK nem NuGet.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace XGBP.Media
{
    public sealed class MediaSessionState
    {
        public string Key, AppId, AppName, AppPath, Title, Artist, Album, CoverKey, Error;
        // Executaveis do app de origem, para casar com as sessoes do mixer.
        public readonly List<string> AppPaths = new List<string>();
        public string Status; // playing, paused, stopped, changing, opened, closed
        public bool Current, CanPlayPause, CanPrevious, CanNext, CanSeek, CanShuffle, CanRepeat;
        public bool Shuffle;
        public int Repeat; // 0 desligado, 1 faixa, 2 lista
        public TimeSpan Position, Duration;
        public DateTimeOffset UpdatedAt;
        public double Rate = 1;
        public byte[] Cover;
        public bool Playing { get { return Status == "playing"; } }

        // O app informa a posicao de tempos em tempos; entre as leituras ela avanca pelo relogio.
        public TimeSpan PositionAt(DateTimeOffset now)
        {
            var value = Position;
            if (Playing && UpdatedAt != default(DateTimeOffset) && now > UpdatedAt)
                value += TimeSpan.FromTicks((long)((now - UpdatedAt).Ticks * Rate));
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            if (Duration > TimeSpan.Zero && value > Duration) value = Duration;
            return value;
        }
        public string Describe()
        {
            return string.Format("{0} | {1} | {2} | {3} | {4} | {5} | pos={6:mm\\:ss}/{7:mm\\:ss} | aleatorio={8} repetir={9} | controles={10}{11}{12}{13}{14}{15} | capa={16} | atual={17} | caminhos={18}{19}",
                AppName, AppId, Title, Artist, Album, Status, Position, Duration, Shuffle, Repeat,
                CanPrevious ? "A" : "-", CanPlayPause ? "P" : "-", CanNext ? "N" : "-", CanSeek ? "S" : "-", CanShuffle ? "L" : "-", CanRepeat ? "R" : "-",
                Cover == null ? 0 : Cover.Length, Current, string.Join(";", AppPaths.ToArray()), Error == null ? "" : " | erro=" + Error);
        }
    }

    public sealed class MediaCommand
    {
        // toggle, previous, next, shuffle, repeat, seek
        public string Kind, SessionKey;
        public bool Shuffle;
        public int Repeat;
        public TimeSpan Position;
    }

    public static class MediaSessions
    {
        static readonly object Gate = new object();
        static GlobalSystemMediaTransportControlsSessionManager manager;
        static readonly Dictionary<string, byte[]> Covers = new Dictionary<string, byte[]>();
        static readonly Dictionary<string, DateTime> CoverSeen = new Dictionary<string, DateTime>();
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly MediaSessionKeys SessionKeys = new MediaSessionKeys();
        static Dictionary<string, List<string>> windowIds;
        static DateTime windowIdsAt;

        public static List<MediaSessionState> Read()
        {
            lock (Gate)
            {
                var m = Manager();
                var current = m.GetCurrentSession();
                var result = new List<MediaSessionState>();
                var live = new HashSet<long>();
                long currentIdentity = current == null ? 0 : SessionIdentity(current);
                foreach (var session in m.GetSessions())
                {
                    var item = new MediaSessionState { AppId = session.SourceAppUserModelId ?? "" };
                    long identity = SessionIdentity(session); live.Add(identity);
                    item.Key = SessionKeys.Get(identity, item.AppId, session);
                    item.Current = identity == currentIdentity;
                    try { Fill(session, item); }
                    catch (Exception ex) { item.Status = "closed"; item.Title = ""; item.Error = ex.Message; }
                    result.Add(item);
                }
                SessionKeys.Retain(live);
                // As duas tabelas acompanham so as faixas atuais; faixas sem capa tambem saem (CoverSeen crescia sem limite).
                foreach (var key in new List<string>(CoverSeen.Keys)) if (!result.Exists(s => s.CoverKey == key)) { Covers.Remove(key); CoverSeen.Remove(key); }
                foreach (var key in new List<string>(Covers.Keys)) if (!CoverSeen.ContainsKey(key)) Covers.Remove(key);
                return result;
            }
        }

        public static void Send(MediaCommand command)
        {
            lock (Gate)
            {
                var session = Find(command.SessionKey);
                if (session == null) throw new InvalidOperationException("Essa mídia não está mais disponível.");
                bool accepted;
                if (command.Kind == "toggle") accepted = Wait(session.TryTogglePlayPauseAsync());
                else if (command.Kind == "previous") accepted = Wait(session.TrySkipPreviousAsync());
                else if (command.Kind == "next") accepted = Wait(session.TrySkipNextAsync());
                else if (command.Kind == "shuffle") accepted = Wait(session.TryChangeShuffleActiveAsync(command.Shuffle));
                else if (command.Kind == "repeat")
                    accepted = Wait(session.TryChangeAutoRepeatModeAsync(command.Repeat == 1 ? MediaPlaybackAutoRepeatMode.Track :
                        command.Repeat == 2 ? MediaPlaybackAutoRepeatMode.List : MediaPlaybackAutoRepeatMode.None));
                else if (command.Kind == "seek")
                {
                    // A posicao pedida usa a mesma escala da linha do tempo informada pelo app.
                    var start = session.GetTimelineProperties().StartTime;
                    accepted = Wait(session.TryChangePlaybackPositionAsync((start + command.Position).Ticks));
                }
                else throw new ArgumentException("Comando de mídia desconhecido: " + command.Kind);
                if (!accepted) throw new InvalidOperationException("O aplicativo recusou o comando.");
            }
        }

        // O mixer identifica apps pelo executavel; o SMTC, pelo AppUserModelID.
        public static bool Matches(MediaSessionState media, string appIdentity)
        {
            if (media == null || string.IsNullOrEmpty(appIdentity) || appIdentity.StartsWith("pid:", StringComparison.Ordinal)) return false;
            foreach (var path in media.AppPaths) if (string.Equals(path, appIdentity, StringComparison.OrdinalIgnoreCase)) return true;
            if (media.AppPaths.Count > 0) return false; // Caminho conhecido nunca cai no casamento so por nome.
            if (media.AppId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return string.Equals(Path.GetFileName(appIdentity), media.AppId, StringComparison.OrdinalIgnoreCase);
            int bang = media.AppId.IndexOf('!'), split = bang < 0 ? -1 : media.AppId.LastIndexOf('_', bang);
            if (split <= 0) return false;
            // Pacote: <Nome>_<PublisherId>!App corresponde a WindowsApps\<Nome>_<versao>_<arq>__<PublisherId>\.
            string name = media.AppId.Substring(0, split), publisher = media.AppId.Substring(split + 1, bang - split - 1);
            return appIdentity.IndexOf("\\WindowsApps\\" + name + "_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                appIdentity.IndexOf("__" + publisher + "\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static GlobalSystemMediaTransportControlsSessionManager Manager()
        {
            if (manager == null) manager = Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
            return manager;
        }
        static GlobalSystemMediaTransportControlsSession Find(string key)
        {
            var live = new HashSet<long>();
            GlobalSystemMediaTransportControlsSession found = null;
            foreach (var session in Manager().GetSessions())
            {
                string id = session.SourceAppUserModelId ?? "";
                long identity = SessionIdentity(session); live.Add(identity);
                if (SessionKeys.Get(identity, id, session) == key) found = session;
            }
            SessionKeys.Retain(live); return found;
        }
        static long SessionIdentity(object session)
        {
            IntPtr identity = Marshal.GetIUnknownForObject(session);
            try { return identity.ToInt64(); } finally { Marshal.Release(identity); }
        }

        static void Fill(GlobalSystemMediaTransportControlsSession session, MediaSessionState item)
        {
            var playback = session.GetPlaybackInfo();
            item.Status = playback.PlaybackStatus.ToString().ToLowerInvariant();
            var controls = playback.Controls;
            item.CanPlayPause = controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled;
            item.CanPrevious = controls.IsPreviousEnabled; item.CanNext = controls.IsNextEnabled;
            item.CanSeek = controls.IsPlaybackPositionEnabled;
            item.CanShuffle = controls.IsShuffleEnabled && playback.IsShuffleActive.HasValue;
            item.CanRepeat = controls.IsRepeatEnabled && playback.AutoRepeatMode.HasValue;
            item.Shuffle = playback.IsShuffleActive ?? false;
            var repeat = playback.AutoRepeatMode ?? MediaPlaybackAutoRepeatMode.None;
            item.Repeat = repeat == MediaPlaybackAutoRepeatMode.Track ? 1 : repeat == MediaPlaybackAutoRepeatMode.List ? 2 : 0;
            if (playback.PlaybackRate.HasValue && playback.PlaybackRate.Value > 0) item.Rate = playback.PlaybackRate.Value;

            var timeline = session.GetTimelineProperties();
            item.Duration = timeline.EndTime - timeline.StartTime;
            item.Position = timeline.Position - timeline.StartTime;
            item.UpdatedAt = timeline.LastUpdatedTime;
            if (item.Duration <= TimeSpan.Zero) { item.Duration = TimeSpan.Zero; item.CanSeek = false; }

            var media = Wait(session.TryGetMediaPropertiesAsync());
            item.Title = media.Title ?? ""; item.Artist = media.Artist ?? ""; item.Album = media.AlbumTitle ?? "";
            item.CoverKey = item.Key + "|" + item.Title + "|" + item.Artist + "|" + item.Album;
            item.Cover = Cover(item.CoverKey, media.Thumbnail);

            ResolveApp(item);
        }

        static byte[] Cover(string key, IRandomAccessStreamReference thumbnail)
        {
            byte[] cached; DateTime first;
            if (!CoverSeen.TryGetValue(key, out first)) { first = DateTime.UtcNow; CoverSeen[key] = first; }
            // Alguns apps trocam o titulo antes da capa; reler por alguns segundos evita capa da faixa anterior.
            if (Covers.TryGetValue(key, out cached) && (DateTime.UtcNow - first).TotalSeconds > 4) return cached;
            if (thumbnail == null) return cached;
            try
            {
                using (var stream = Wait(thumbnail.OpenReadAsync()))
                {
                    if (stream.Size == 0 || stream.Size > 8 * 1024 * 1024) return cached;
                    using (var input = stream.GetInputStreamAt(0)) using (var reader = new DataReader(input))
                    {
                        uint size = (uint)stream.Size;
                        uint loaded = Wait(reader.LoadAsync(size));
                        var bytes = new byte[loaded]; reader.ReadBytes(bytes);
                        if (SameCover(cached, bytes)) return cached;
                        Covers[key] = bytes; return bytes;
                    }
                }
            }
            catch { return cached; }
        }
        internal static bool SameCover(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        static void ResolveApp(MediaSessionState item)
        {
            string id = item.AppId;
            if (id.IndexOf('!') < 0 && !id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // Apps desktop com ID explicito (Firefox, Chrome) marcam as janelas com PKEY_AppUserModel_ID.
                if (windowIds == null || (DateTime.UtcNow - windowIdsAt).TotalSeconds > 10) { windowIds = WindowAppIds(); windowIdsAt = DateTime.UtcNow; }
                List<string> paths;
                if (windowIds.TryGetValue(id, out paths)) item.AppPaths.AddRange(paths);
            }
            item.AppPath = item.AppPaths.Count > 0 ? item.AppPaths[0] : null;
            string name;
            if (!Names.TryGetValue(id, out name))
            {
                try { name = Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(id).DisplayInfo.DisplayName; } catch { name = null; }
                if (string.IsNullOrWhiteSpace(name) && item.AppPath != null)
                {
                    try { var info = FileVersionInfo.GetVersionInfo(item.AppPath); name = string.IsNullOrWhiteSpace(info.FileDescription) ? info.ProductName : info.FileDescription; }
                    catch { name = null; }
                }
                if (string.IsNullOrWhiteSpace(name)) name = id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? id.Substring(0, id.Length - 4) : id;
                Names[id] = name;
            }
            item.AppName = name;
        }

        static Dictionary<string, List<string>> WindowAppIds()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            EnumWindows(delegate(IntPtr hwnd, IntPtr arg) {
                try
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    string id = WindowAppId(hwnd); if (string.IsNullOrEmpty(id)) return true;
                    uint pid; GetWindowThreadProcessId(hwnd, out pid);
                    string path = ProcessPath(pid); if (path == null) return true;
                    List<string> list;
                    if (!result.TryGetValue(id, out list)) { list = new List<string>(); result[id] = list; }
                    if (!list.Exists(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase))) list.Add(path);
                }
                catch { } // A janela pode fechar durante a coleta.
                return true;
            }, IntPtr.Zero);
            return result;
        }
        static string WindowAppId(IntPtr hwnd)
        {
            Guid iid = typeof(IPropertyStore).GUID; IPropertyStore store;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store == null) return null;
            try
            {
                var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
                PropVariant value; store.GetValue(ref key, out value);
                try { return value.Type == 31 && value.Pointer != IntPtr.Zero ? Marshal.PtrToStringUni(value.Pointer) : null; }
                finally { PropVariantClear(ref value); }
            }
            finally { Marshal.ReleaseComObject(store); }
        }
        static string ProcessPath(uint pid)
        {
            IntPtr process = OpenProcess(0x1000, false, pid); if (process == IntPtr.Zero) return null;
            try { var path = new StringBuilder(32768); int size = path.Capacity; return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : null; }
            finally { CloseHandle(process); }
        }

        // Espera uma operacao WinRT sem System.Runtime.WindowsRuntime, que exige o Windows.winmd do SDK.
        static T Wait<T>(IAsyncOperation<T> operation)
        {
            var done = new ManualResetEvent(false);
            operation.Completed = delegate { try { done.Set(); } catch (ObjectDisposedException) { } };
            try
            {
                if (!done.WaitOne(3000)) { operation.Cancel(); throw new TimeoutException("O Windows não respondeu à consulta de mídia."); }
                if (operation.Status != AsyncStatus.Completed)
                    throw new InvalidOperationException("Consulta de mídia: " + operation.Status + (operation.ErrorCode == null ? "" : " · " + operation.ErrorCode.Message));
                return operation.GetResults();
            }
            finally { done.Close(); }
        }

        delegate bool EnumProc(IntPtr hwnd, IntPtr arg);
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public int Id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)] struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPropertyStore
        { void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out PropVariant value); void SetValue(ref PropertyKey key, ref PropVariant value); void Commit(); }
        [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr arg);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    }
}
