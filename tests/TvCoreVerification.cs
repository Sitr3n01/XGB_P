// Fase 3: fixtures, arquivos temporarios e TLS loopback. Nunca consulta a configuracao/TV real.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        public void VerifyTvCore() { Task.Run(delegate { return TvCoreSuite.RunAsync(Output); }).GetAwaiter().GetResult(); }
    }
    static partial class TvCoreSuite
    {
        static readonly CancellationToken None = CancellationToken.None;
        internal static TvCoreConfig Config()
        { return new TvCoreConfig { Enabled = true, Mac = "02:00:00:00:00:01", Duid = "uuid:test-target", Ip = "192.168.55.10", Model =
            "TV de teste", WakeSeconds
            = 30, BackoffSeconds = new[] { 1, 2, 3 }, DebounceMs = 50 }; }
        static TaskCompletionSource<bool> Signal() { return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }
        static async Task Until(Func<bool> predicate)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!predicate()) { if (watch.ElapsedMilliseconds > 5000) throw new TimeoutException("Condição de teste da TV não atendida."); await
                Task.Delay(5).ConfigureAwait(false); }
        }
        static async Task<T> Limit<T>(Task<T> task)
        { if (await Task.WhenAny(task, Task.Delay(10000)).ConfigureAwait(false) != task) throw new TimeoutException("Operação de teste da TV excedeu o prazo.");
            return await task.ConfigureAwait(false); }
        static TvTicket Send(TvCoordinator c, TvRequestKind kind, bool value = false)
        { return c.Submit(new TvRequest(Guid.NewGuid(), kind, value)); }
        static TvClient Client(CoreTransport t, CoreClock clock, CoreStore store = null, TvCoreConfig config = null, DateTimeOffset? off = null)
        {
            return new TvClient(config ?? Config(), t, clock, delegate(PowerSnapshot power, CancellationToken ct)
                { return store == null ? Task.FromResult(0) : store.SaveStateAsync(new TvStoredState { Power = power }, ct); }, off);
        }
        public static async Task RunAsync(string output)
        {
            string scratch = Path.Combine(output, "tv-nucleo-testes", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
            var passed = new List<string>();
            Action<bool, string> check = delegate(bool ok, string label)
            { if (!ok) throw new InvalidOperationException("Núcleo TV: " + label); passed.Add(label); };
            await ClientCases(check).ConfigureAwait(false);
            await CoordinatorCases(check).ConfigureAwait(false);
            await StorageCases(scratch, check).ConfigureAwait(false);
            await ServiceCases(check).ConfigureAwait(false);
            await WireCases(scratch, check).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(output, "tv-nucleo-verificacao.txt"), "OK: " + passed.Count +
                " verificacoes do nucleo TV (ADR 0110, fases 3-5). Transporte/relogio/tela substituidos; DPAPI com segredo ficticio; TLS so em 127.0.0.1.\r\n" +
                string.Join("\r\n", passed) + "\r\n", Encoding.UTF8);
        }
        sealed class CoreClock : ITvClock
        {
            readonly object gate = new object();
            long ticks = TimeSpan.FromDays(1).Ticks;
            DateTimeOffset utc = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
            public DateTimeOffset UtcNow { get { lock (gate) return utc; } }
            public long Timestamp { get { return Interlocked.Read(ref ticks); } }
            public TimeSpan Elapsed(long start) { return TimeSpan.FromTicks(Timestamp - start); }
            public void Advance(double seconds) { lock (gate) { utc += TimeSpan.FromSeconds(seconds); Interlocked.Add(ref ticks,
                TimeSpan.FromSeconds(seconds).Ticks);
                } }
            public void Wall(double seconds) { lock (gate) utc += TimeSpan.FromSeconds(seconds); }
            public Task Delay(TimeSpan duration, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Advance(duration.TotalSeconds);
                return Task.FromResult(0);
                }
        }
        sealed class CoreTransport : ITvTransport
        {
            public bool ChannelReady { get; set; }
            public int Sends, Wakes, Pairs, Prepares, Observes, Effects, MaxEffects;
            public bool LastHandshake;
            public TimeSpan LastBudget;
            public Func<CancellationToken, Task<NetworkObservation>> Observe = ct => Task.FromResult(new NetworkObservation(TvNetwork.Reachable));
            public Func<CancellationToken, Task<SendResult>> SendKey = ct => Task.FromResult(new SendResult(DeliveryStatus.Sent, TvOutcome.Unconfirmed,
                "Frame fictício."));
            public Func<CancellationToken, Task<WakeResult>> Wake = ct => Task.FromResult(new WakeResult(3, true, "WoL fictício."));
            public Task<NetworkObservation> ObserveAsync(TvCoreConfig c, TimeSpan b, bool r, CancellationToken ct)
            { ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref Observes); return Observe(ct); }
            public async Task<SendResult> SendKeyAsync(TvCoreConfig c, string key, TimeSpan b, bool handshake, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested(); if (key != "KEY_POWER") throw new InvalidOperationException("Tecla inesperada.");
                Interlocked.Increment(ref Sends); LastHandshake = handshake; LastBudget = b; int n = Interlocked.Increment(ref Effects);
                    MaxEffects = Math.Max(MaxEffects,
                    n);
                try { return await SendKey(ct).ConfigureAwait(false); } finally { Interlocked.Decrement(ref Effects); }
            }
            public async Task<WakeResult> WakeAsync(TvCoreConfig c, CancellationToken ct)
            {
            ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref Wakes); int n = Interlocked.Increment(ref Effects); MaxEffects =
                Math.Max(MaxEffects, n);
                try { return await Wake(ct).ConfigureAwait(false); } finally { Interlocked.Decrement(ref Effects); }
            }
            public Task<PairResult> PairAsync(TvCoreConfig c, bool replace, CancellationToken ct)
            { ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref Pairs); return Task.FromResult(new PairResult(true, "Pareamento fictício.")); }
            public Task PrepareAsync(TvCoreConfig c, CancellationToken ct)
            { ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref Prepares); ChannelReady = true; return Task.FromResult(0); }
            public Task CloseAsync() { ChannelReady = false; return Task.FromResult(0); }
        }
        sealed class CoreStore : ITvStore
        {
            public TvStoredState State;
            public TvCoreConfig Configuration;
            public bool Fail;
            public int Writes;
            public Func<int, CancellationToken, Task> Before = (n, ct) => Task.FromResult(0);
            public async Task SaveStateAsync(TvStoredState state, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested(); int n = Interlocked.Increment(ref Writes);
                await Before(n, ct).ConfigureAwait(false); if (Fail) throw new IOException("Disco fictício indisponível."); State = state.Copy();
            }
            public Task SaveConfigAsync(TvCoreConfig config, CancellationToken ct)
            { ct.ThrowIfCancellationRequested(); if (Fail) throw new IOException("Disco fictício indisponível."); Configuration = config.Copy(); return
                Task.FromResult(0); }
        }
        sealed class CoreDisplay : ITvCoreDisplay
        {
            public TvDisplayState Value = new TvDisplayState { Topology = TvTopology.ExternalOnly, SamsungActive = true };
            public int Writes;
            public TvDisplayState Read() { return Value; }
            public bool SetHdr(bool enabled) { Interlocked.Increment(ref Writes); return true; }
        }
    }
}
