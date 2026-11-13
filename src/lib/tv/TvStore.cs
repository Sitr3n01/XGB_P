// Persistencia atomica propria. Nao escreve na origem SamsungTvOrchestrator (ADR 0110).
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    [DataContract] public sealed class TvStoredState
    {
        [DataMember] public int SchemaVersion = 1;
        [DataMember] public DesiredTvState Desired;
        [DataMember] public PowerSnapshot Power = PowerSnapshot.Unknown;
        [DataMember] public DateTimeOffset? OffAttempt, SuspendedAt;
        [DataMember] public string SleepId;
        [DataMember] public bool ResumeConsumed = true;
        public TvStoredState Copy() { return (TvStoredState)MemberwiseClone(); }
    }
    public interface ITvStore
    {
        Task SaveStateAsync(TvStoredState state, CancellationToken ct);
        Task SaveConfigAsync(TvCoreConfig config, CancellationToken ct);
    }
    public sealed class TvStore : ITvStore
    {
        readonly string root;
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "tv"); } }
        public TvStore(string root) { this.root = Path.GetFullPath(root); }
        public Task SaveStateAsync(TvStoredState state, CancellationToken ct)
        { var copy = state.Copy(); return Task.Run(delegate { TvJson.Write(Path.Combine(root, "state.json"), copy, ct); }, ct); }
        public Task SaveConfigAsync(TvCoreConfig config, CancellationToken ct)
        { var copy = config.Copy(); copy.Validate(); return Task.Run(delegate { TvJson.Write(Path.Combine(root, "config.json"), copy, ct); }, ct); }
        public TvStoredState ReadState()
        {
            var value = TvJson.Read<TvStoredState>(Path.Combine(root, "state.json"));
            if (value == null) return new TvStoredState();
            if (value.SchemaVersion != 1 || !Enum.IsDefined(typeof(DesiredTvState), value.Desired)) throw new IOException("Estado da TV inválido.");
            value.Power = PowerSnapshot.Unknown; // A autoridade e local a sessao, nunca restaurada do disco.
            return value;
        }
        public TvCoreConfig ReadConfig()
        {
            var value = TvJson.Read<TvCoreConfig>(Path.Combine(root, "config.json"));
            if (value == null) throw new IOException("Configuração da TV ausente.");
            value.Validate(); return value;
        }
    }
    public static class TvJson
    {
        const int MaxBytes = 1000000;
        public static string Serialize<T>(T value)
        { using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return
            Encoding.UTF8.GetString(stream.ToArray()); } }
        public static T Parse<T>(string text) where T : class
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new IOException("JSON da TV excede o limite.");
            try { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
            catch (SerializationException) { throw new IOException("JSON da TV inválido."); }
        }
        public static T Read<T>(string path) where T : class
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length < 1 || stream.Length > MaxBytes) throw new IOException("JSON da TV vazio ou grande demais.");
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) return Parse<T>(reader.ReadToEnd());
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
        public static string Hash(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
        public static void Write<T>(string path, T value, CancellationToken ct, bool backup = true)
        {
            ct.ThrowIfCancellationRequested();
            byte[] bytes;
            using (var memory = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(memory, value); bytes = memory.ToArray(); }
            if (bytes.Length > MaxBytes) throw new IOException("JSON da TV excede o limite.");
            string full = Path.GetFullPath(path), temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var mutex = new Mutex(false, @"Local\XGBP.TV.File." + Hash(full.ToUpperInvariant())))
            {
                bool owned = false;
                try
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    while (!owned && watch.Elapsed < TimeSpan.FromSeconds(5))
                    { ct.ThrowIfCancellationRequested(); try { owned = mutex.WaitOne(25); } catch (AbandonedMutexException) { owned = true; } }
                    if (!owned) throw new IOException("Diário da TV ocupado.");
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                    ct.ThrowIfCancellationRequested();
                    if (File.Exists(full)) File.Replace(temp, full, backup ? full + ".previous.json" : null, false);
                    else File.Move(temp, full);
                }
                finally
                {
                    if (owned) mutex.ReleaseMutex();
                    try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    Array.Clear(bytes, 0, bytes.Length);
                }
            }
        }
    }
}
