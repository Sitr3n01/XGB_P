// Fila prioritaria limitada, barreira fora da fila comum, geracoes e deduplicacao (porte ADR 0110).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed partial class TvCoordinator
    {
        sealed class Job
        {
            public readonly TvRequest Request;
            public readonly long Generation, Submitted;
            public readonly bool ResumeGuard;
            public long? Finished;
            public readonly TaskCompletionSource<TvReply> Done = new TaskCompletionSource<TvReply>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Job(TvRequest request, long generation, long submitted, bool guard)
            { Request = request; Generation = generation; Submitted = submitted; ResumeGuard = guard; }
        }
        readonly object gate = new object();
        readonly List<Job> pending = new List<Job>();
        readonly Dictionary<Guid, Job> history = new Dictionary<Guid, Job>();
        readonly Dictionary<TvRequestKind, Job> emergency = new Dictionary<TvRequestKind, Job>();
        readonly Queue<Guid> finished = new Queue<Guid>();
        readonly SemaphoreSlim available = new SemaphoreSlim(0);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly ITvClock clock;
        readonly ITvStore store;
        readonly ITvCoreDisplay display;
        readonly TvClient tv;
        readonly Task worker;
        TvCoreConfig config;
        TvStoredState state;
        TvMode mode;
        Job critical;
        CancellationTokenSource active;
        TvRequestKind? activeKind;
        DeliveryStatus activeDelivery;
        long generation, resumeAt = long.MinValue, psAt = long.MinValue, suspendAt = long.MinValue;
        bool enabled, manualOverride, disposed;
        DesiredTvState desired;
        string topologyKey, persistenceError;
        EnsureResult lastResult;
        public event Action<TvReply> AutomaticCompleted;
        public TvCoordinator(TvCoreConfig config, TvStoredState state, ITvTransport transport,
            ITvCoreDisplay display, ITvStore store, ITvClock clock)
        {
            config.Validate(); this.config = config.Copy(); this.state = state.Copy(); this.state.Power = PowerSnapshot.Unknown;
            this.display = display; this.store = store; this.clock = clock;
            enabled = config.Enabled; mode = enabled ? TvMode.Running : TvMode.Disabled;
            topologyKey = Key(display.Read());
            tv = new TvClient(config, transport, clock, PersistPowerAsync, state.OffAttempt);
            worker = Task.Run((Func<Task>)LoopAsync);
        }
        static string Key(TvDisplayState d) { return d.Topology + ":" + d.SamsungActive + ":" + d.Valid; }
    bool ResumeGuard() { return resumeAt != long.MinValue && clock.Elapsed(resumeAt) >= TimeSpan.Zero && clock.Elapsed(resumeAt) < TimeSpan.FromSeconds(2); }
        public bool VerifiedPs
        {
            get { lock (gate) return psAt != long.MinValue && resumeAt != long.MinValue && psAt > suspendAt &&
                clock.Elapsed(psAt) >= TimeSpan.Zero && clock.Elapsed(psAt) < TimeSpan.FromSeconds(10) &&
                clock.Elapsed(resumeAt) >= TimeSpan.Zero && clock.Elapsed(resumeAt) < TimeSpan.FromSeconds(20); }
        }
        static Guid StableId(string identity)
        { string hash = TvJson.Hash(identity); var bytes = new byte[16]; for (int i = 0; i < 16; i++) bytes[i] = Convert.ToByte(hash.Substring(i * 2, 2), 16);
            return new Guid(bytes); }
        // Recebe apenas gesto ja validado pelo leitor compartilhado; nao abre nem escreve em HID.
        public void NoteVerifiedPs()
        {
            Guid id;
            lock (gate)
            {
                psAt = clock.Timestamp;
                if (!VerifiedPs || mode != TvMode.Running) return;
                id = StableId("PS:" + (state.SleepId ?? resumeAt.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            Submit(new TvRequest(id, TvRequestKind.DualSenseVerified, false, "HID/verified-PS"));
        }
        public TvTicket SystemResume()
        {
            string identity; lock (gate) identity = "resume:" + (state.SleepId ?? "no-sleep-session");
            var ticket = Submit(new TvRequest(StableId(identity), TvRequestKind.Resume, false, "Windows"));
            if (VerifiedPs) NoteVerifiedPs(); return ticket;
        }
        void CancelActive() { try { if (active != null) active.Cancel(); } catch (ObjectDisposedException) { } }
        public TvTicket Submit(TvRequest request)
        {
            if (request == null || request.Id == Guid.Empty || request.Source == null || request.Source.Length > 80 || !Enum.IsDefined(typeof(TvRequestKind),
                request.Kind))
                return Immediate(request == null ? Guid.Empty : request.Id, TvOutcome.InvalidCommand, "Solicitação inválida.");
            var observed = request.Kind == TvRequestKind.DisplayChanged ? display.Read() : null;
            lock (gate)
            {
                Job existing; history.TryGetValue(request.Id, out existing);
                existing = existing ?? emergency.Values.FirstOrDefault(j => j.Request.Id == request.Id);
                if (existing != null) return existing.Request.Same(request) ? Ticket(existing) : Immediate(request.Id, TvOutcome.InvalidCommand,
                    "ID já vinculado a outro conteúdo.");
                if (disposed || (mode == TvMode.Stopping || mode == TvMode.Faulted) && request.Kind != TvRequestKind.Stop)
                    return Immediate(request.Id, TvOutcome.Cancelled, "Núcleo encerrado ou em falha.");
                if (request.Kind == TvRequestKind.DualSenseVerified)
                {
                    if (!VerifiedPs) return Immediate(request.Id, TvOutcome.BlockedAmbiguous, "PS fora da janela de retomada.");
                    if (!config.PsWakeEnabled || config.DualSense == TvAction.Keep || !enabled || !config.Enabled)
                        return Immediate(request.Id, TvOutcome.NoAction, "PS reconhecido; nenhuma intenção de energia criada.");
                }
                Prune(); bool barrier = TvPolicy.Critical(request);
            if (!barrier && (history.Count >= 992 || pending.Count >= 64)) return Immediate(request.Id, TvOutcome.Busy, "Fila ou histórico limitado ocupado.");
                if (barrier)
                {
                    generation++; CancelActive();
                    if (request.Kind == TvRequestKind.Enable) { enabled = false; mode = TvMode.Disabled; }
                    if (request.Kind == TvRequestKind.Suspend || request.Kind == TvRequestKind.SessionEnding)
                    { mode = TvMode.Suspending; suspendAt = clock.Timestamp; psAt = long.MinValue; }
                    if (request.Kind == TvRequestKind.Stop) mode = TvMode.Stopping;
                }
                bool guard = ResumeGuard();
                bool advance = !barrier && (TvPolicy.Advances(request.Kind) || request.Kind == TvRequestKind.DisplayChanged && !manualOverride && !guard &&
                    observed != null && observed.Valid && observed.Topology != TvTopology.NoActiveDisplay && Key(observed) != topologyKey);
                long next = advance ? generation + 1 : generation;
                var job = new Job(request, next, clock.Timestamp, guard);
                if (barrier && history.Count >= 1024) emergency[request.Kind] = job; else history.Add(request.Id, job);
                if (barrier)
                {
                if (critical != null) Finish(critical, new TvReply(critical.Request.Id, TvOutcome.StaleIntent, "Barreira substituída por outra mais recente."));
                    critical = job;
                }
                else pending.Add(job);
                if (advance) { generation = next; CancelActive(); }
                if (request.Kind == TvRequestKind.Enable) enabled = request.Value;
                if ((request.Kind == TvRequestKind.ConfirmOn || request.Kind == TvRequestKind.ConfirmOff || request.Kind == TvRequestKind.Forget
                    || request.Kind ==
                    TvRequestKind.Pair) &&
                    (activeKind == TvRequestKind.PowerOn || activeKind == TvRequestKind.DisplayChanged || activeKind ==
                        TvRequestKind.NetworkRecovered || activeKind ==
                        TvRequestKind.Resume)) CancelActive();
                if (request.Kind == TvRequestKind.PowerOn || request.Kind == TvRequestKind.PowerOff || request.Kind == TvRequestKind.DualSenseVerified)
                { manualOverride = true; desired = request.Kind == TvRequestKind.DualSenseVerified ? TvPolicy.Action(config.DualSense) : request.Kind ==
                    TvRequestKind.PowerOff ? DesiredTvState.Off : DesiredTvState.On; state.Desired = desired; }
                if (request.Kind == TvRequestKind.Resume) { mode = enabled ? TvMode.Running : TvMode.Disabled; resumeAt = clock.Timestamp; }
                available.Release(); return Ticket(job);
            }
        }
        static TvTicket Immediate(Guid id, TvOutcome outcome, string detail)
        { var reply = new TvReply(id, outcome, detail); return new TvTicket(reply, Task.FromResult(reply)); }
        static TvTicket Ticket(Job job)
        { return new TvTicket(job.Done.Task.Status == TaskStatus.RanToCompletion ? job.Done.Task.Result : new TvReply(job.Request.Id, TvOutcome.Accepted,
            "Solicitação aceita e pendente.", false), job.Done.Task); }
        public TvRuntime Snapshot()
        { lock (gate) return new TvRuntime(mode, enabled, manualOverride, tv.HasAuthority, VerifiedPs, desired, tv.Power, lastResult, persistenceError); }
        public TvCoreConfig Configuration { get { lock (gate) return config.Copy(); } }
        async Task PersistPowerAsync(PowerSnapshot power, CancellationToken ct)
        {
            TvStoredState snapshot;
            lock (gate)
            {
                state.Power = power;
                if (power.PendingDesired == DesiredTvState.Off) state.OffAttempt = power.At;
                snapshot = state.Copy();
            }
            try { await store.SaveStateAsync(snapshot, ct).ConfigureAwait(false); }
            catch (IOException) { lock (gate) persistenceError = "IOException"; throw; }
            catch (UnauthorizedAccessException) { lock (gate) persistenceError = "UnauthorizedAccessException"; throw; }
        }
        Task SaveAsync(CancellationToken ct) { TvStoredState copy; lock (gate) copy = state.Copy(); return store.SaveStateAsync(copy, ct); }
        void Prune()
        {
            while (finished.Count > 0)
            {
                Guid id = finished.Peek(); Job job;
                if (!history.TryGetValue(id, out job)) { finished.Dequeue(); continue; }
                if (!job.Finished.HasValue || clock.Elapsed(job.Finished.Value) < TimeSpan.FromMinutes(5)) break;
                finished.Dequeue(); history.Remove(id);
            }
        }
        void Finish(Job job, TvReply result)
        { job.Finished = clock.Timestamp; if (history.ContainsKey(job.Request.Id)) finished.Enqueue(job.Request.Id); job.Done.TrySetResult(result); Prune(); }
        async Task LoopAsync()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    await available.WaitAsync(lifetime.Token).ConfigureAwait(false);
                    Job job;
                    lock (gate)
                    {
                        job = critical; critical = null;
                        if (job == null) { job = pending.OrderByDescending(j => TvPolicy.Priority(j.Request.Kind)).ThenBy(j =>
                            j.Submitted).FirstOrDefault(); if (job != null)
                            pending.Remove(job); }
                    }
                    if (job == null) continue;
                    using (var work = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                    {
                        lock (gate)
                        {
                            if (TvPolicy.EpochBound(job.Request.Kind) && job.Generation != generation)
                            { Finish(job, new TvReply(job.Request.Id, TvOutcome.StaleIntent, "Substituída por intenção mais recente.")); continue; }
                            active = work; activeKind = job.Request.Kind; activeDelivery = DeliveryStatus.NotSent;
                        }
                        if (job.Request.Kind == TvRequestKind.Suspend && !job.Request.Value || job.Request.Kind == TvRequestKind.SessionEnding)
                    { var left = TimeSpan.FromSeconds(2.8) - clock.Elapsed(job.Submitted); if (left <= TimeSpan.Zero) work.Cancel(); else
                        work.CancelAfter(left); }
                        else work.CancelAfter(TimeSpan.FromSeconds(job.Request.Kind == TvRequestKind.Pair ? 75 : config.WakeSeconds + 15));
                        TvReply reply;
                        try { reply = await ProcessAsync(job, work.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { reply = new TvReply(job.Request.Id, TvOutcome.Cancelled,
                        "Cancelada por barreira ou prazo.", true, activeDelivery); }
                        catch (Exception ex)
                        {
                            bool persistence = ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException;
lock (gate) { mode = persistence ? TvMode.Disabled : TvMode.Faulted; enabled = false; if (persistence) persistenceError = ex.GetType().Name; }
                            reply = new TvReply(job.Request.Id, persistence ? TvOutcome.PersistenceFailure : TvOutcome.QueryFailed,
                                "Falha no núcleo; ações bloqueadas.", true,
                                activeDelivery);
                        }
                        lock (gate) { active = null; activeKind = null; Finish(job, reply); }
                        if (job.Request.Source != "user") { try { var done = AutomaticCompleted; if (done != null) done(reply); } catch (Exception) { } }
                        if (job.Request.Kind == TvRequestKind.Stop) { lifetime.Cancel(); break; }
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (gate) foreach (var job in history.Values.Concat(emergency.Values))
                    job.Done.TrySetResult(new TvReply(job.Request.Id, TvOutcome.Cancelled, "Núcleo encerrado."));
            }
        }
        public async Task CloseAsync()
        {
            lock (gate) { if (disposed) return; disposed = true; mode = TvMode.Stopping; CancelActive(); }
            lifetime.Cancel(); await worker.ConfigureAwait(false); await tv.CloseAsync().ConfigureAwait(false);
            lifetime.Dispose(); available.Dispose();
        }
    }
}
