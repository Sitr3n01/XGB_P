// Ciclo de vida unico da TV no XGB_P. Abertura normal importa para pasta propria; verificacoes usam fixtures.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed class TvService
    {
        readonly TvCoordinator coordinator;
        readonly TvLease lease;
        readonly ITvCoreDisplay display;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly object gate = new object();
        bool closed;
        public event Action<TvReply> AutomaticResult;
        public bool Imported { get; private set; }
        public string CredentialNote { get; private set; }
        public bool AuthorizationKnown { get; private set; }
        internal TvService(TvCoreConfig config, TvStoredState state, ITvTransport transport, ITvCoreDisplay display, ITvStore store, ITvClock
            clock, TvLease lease
            = null)
        { this.lease = lease; this.display = display; coordinator = new TvCoordinator(config, state, transport, display, store, clock);
            coordinator.AutomaticCompleted += Report; }
        public TvCoreConfig Configuration { get { return coordinator.Configuration; } }
        public TvRuntime Runtime { get { return coordinator.Snapshot(); } }
        public static TvService Open(string root, string source)
        {
            var store = new TvStore(root); bool imported = !File.Exists(Path.Combine(root, "config.json"));
            TvCoreConfig config;
            if (imported)
            {
                string path = Path.Combine(source, "config.json");
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length > 1000000) throw new IOException("Configuração de origem grande demais.");
                    using (var reader = new StreamReader(stream)) config = TvCoreConfig.ImportJson(reader.ReadToEnd());
                }
            }
            else config = store.ReadConfig();
            var lease = new TvLease(config.Mac);
            if (!lease.Owned) { lease.Dispose(); throw new IOException(
                "Outro programa controla esta TV ou a posse está indisponível. Encerre o Orchestrator para usar a integração."); }
            try
            {
                var credentials = new TvCredentials(Path.Combine(root, "credential.json"), config);
            bool credential = File.Exists(Path.Combine(root, "credential.json")) ? credentials.Load() : credentials.Import(Path.Combine(source,
                "tizen-token.json"));
                if (imported) store.SaveConfigAsync(config, CancellationToken.None).GetAwaiter().GetResult();
                var clock = new TvClock(); var display = new TvWindowsDisplay(config.Fragment);
                return new TvService(config, store.ReadState(), new TvTransport(credentials, lease, clock), display, store, clock, lease)
                    { Imported = imported, AuthorizationKnown = credential, CredentialNote = credential ? "Autorização protegida carregada." :
                        "Autorize a conexão na TV para usar os comandos." };
            }
            catch { lease.Dispose(); throw; }
        }
        public async Task<TvReply> SendAsync(TvRequestKind kind, bool value = false)
        {
            Task<TvReply> pending;
            lock (gate)
            {
                if (closed) return new TvReply(Guid.Empty, TvOutcome.Cancelled, "Integração encerrada.");
                pending = coordinator.Submit(new TvRequest(Guid.NewGuid(), kind, value)).Completion;
            }
            var reply = await pending.ConfigureAwait(false);
            if (kind == TvRequestKind.Pair && reply.Outcome == TvOutcome.NoAction) { AuthorizationKnown = true; CredentialNote = "Conexão autorizada na TV."; }
            return reply;
        }
        public Task<TvReply> SaveOptionsAsync(TvCoreConfig config)
        {
            lock (gate)
            {
                if (closed) return Task.FromResult(new TvReply(Guid.Empty, TvOutcome.Cancelled, "Integração encerrada."));
                return coordinator.Submit(new TvRequest(Guid.NewGuid(), TvRequestKind.Options, false, "user", config)).Completion;
            }
        }
        public static bool CanImplicitlyConfirm(TvCoreConfig c, TvDisplayState d, bool pad)
        { return c.Enabled && c.ImplicitSuspendConfirmation && pad && d != null && d.Valid && d.SamsungActive && d.ActiveCount == 1 && d.Topology ==
            TvTopology.ExternalOnly; }
        public async Task<TvReply> BeforeSuspendAsync(bool fromBar, bool byPad)
        {
            if (fromBar && CanImplicitlyConfirm(Configuration, display.Read(), byPad))
            {
                var confirmation = await SendAsync(TvRequestKind.ConfirmOn).ConfigureAwait(false);
                if (confirmation.Outcome != TvOutcome.Confirmed) { Report(confirmation); return confirmation; }
            }
            var result = await SendAsync(TvRequestKind.Suspend, fromBar).ConfigureAwait(false); Report(result); return result;
        }
        public void Startup() { Auto(TvRequestKind.Startup); }
        public void DisplayChanged() { if (Configuration.Enabled) Auto(TvRequestKind.DisplayChanged); }
        public void NetworkRecovered() { if (Configuration.Enabled) Auto(TvRequestKind.NetworkRecovered); }
        public Task<TvReply> SessionEnding()
        { lock (gate) return closed ? Task.FromResult(new TvReply(Guid.Empty, TvOutcome.Cancelled, "Integração encerrada.")) : coordinator.Submit(new
            TvRequest(Guid.NewGuid(), TvRequestKind.SessionEnding, false, "Windows")).Completion; }
        public void Resume()
        {
            lock (gate)
            {
                if (closed) return;
                coordinator.SystemResume();
            }
        }
        public void VerifiedPs() { lock (gate) if (!closed) coordinator.NoteVerifiedPs(); }
        void Auto(TvRequestKind kind)
        {
            lock (gate)
            {
                if (closed) return;
                coordinator.Submit(new TvRequest(Guid.NewGuid(), kind, false, "Windows"));
            }
        }
        async void CompleteAutomatic(Task<TvReply> completion)
        {
            try { Report(await completion.ConfigureAwait(false)); }
            catch (Exception) { Report(new TvReply(Guid.Empty, TvOutcome.QueryFailed, "Automação da TV sem resultado confirmado.")); }
        }
        void Report(TvReply reply) { try { var handler = AutomaticResult; if (handler != null) handler(reply); } catch (Exception) { } }
        public async Task CloseAsync()
        {
            lock (gate) { if (closed) return; closed = true; lifetime.Cancel(); }
            try { await coordinator.CloseAsync().ConfigureAwait(false); }
            finally { if (lease != null) lease.Dispose(); lifetime.Dispose(); }
        }
    }
}
