// Operacoes serializadas; diario antes do efeito; nenhuma repeticao de toggle apos entrega possivel (ADR 0110).
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed class TvClient
    {
        TvCoreConfig config;
        readonly ITvTransport transport;
        readonly ITvClock clock;
        readonly Func<PowerSnapshot, CancellationToken, Task> persist;
        readonly SemaphoreSlim operations = new SemaphoreSlim(1, 1);
        PowerSnapshot power = PowerSnapshot.Unknown;
        TvObservation last = new TvObservation(Reachability.Unknown, "Sem observação.");
        long authorityAt;
        DateTimeOffset? offAttempt;
        DeliveryStatus delivery;
        bool disposed;
        public TvClient(TvCoreConfig config, ITvTransport transport, ITvClock clock,
            Func<PowerSnapshot, CancellationToken, Task> persist, DateTimeOffset? recentOff = null)
        {
            config.Validate(); this.config = config.Copy(); this.transport = transport; this.clock = clock;
            this.persist = persist; offAttempt = recentOff;
        }
        public PowerSnapshot Power { get { return Volatile.Read(ref power); } }
        public TvObservation LastObservation { get { return Volatile.Read(ref last); } }
        public bool ChannelReady { get { return transport.ChannelReady; } }
        public bool HasAuthority { get { return Authority(Power, authorityAt); } }
        bool Authority(PowerSnapshot p, long timestamp)
        {
            var age = TimeSpan.FromSeconds(config.ConfirmedSeconds);
            var elapsed = clock.Elapsed(timestamp);
            return p.Authoritative(clock.UtcNow, age) && elapsed >= TimeSpan.Zero && elapsed <= age;
        }
        public void UpdateConfiguration(TvCoreConfig value) { value.Validate(); config = value.Copy(); }
        async Task ChangeAsync(PowerSnapshot value, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try { await persist(value, ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested(); }
            catch { Volatile.Write(ref power, PowerSnapshot.Unknown); throw; }
            if (value.AuthorityId != null) authorityAt = clock.Timestamp;
            Volatile.Write(ref power, value);
        }
        public async Task<EnsureResult> ConfirmAsync(TvState state, CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (disposed) return Result(TvOutcome.Cancelled, "Cliente encerrado.");
                if (state != TvState.On && state != TvState.Off) return Result(TvOutcome.InvalidCommand, "Confirmação exige ligada ou apagada.");
                await ChangeAsync(new PowerSnapshot(state, EvidenceSource.UserConfirmed, clock.UtcNow,
                    "Estado confirmado visualmente pelo usuário.", Guid.NewGuid().ToString("N")), ct).ConfigureAwait(false);
                return new EnsureResult(TvOutcome.Confirmed, state, "Confirmação temporária e consumível registrada.");
            }
            finally { operations.Release(); }
        }
        public async Task ForgetAsync(CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try { await ChangeAsync(PowerSnapshot.Unknown, ct).ConfigureAwait(false); }
            finally { operations.Release(); }
        }
        EnsureResult Result(TvOutcome outcome, string detail, DeliveryStatus sent = DeliveryStatus.NotSent)
        { return new EnsureResult(outcome, Power.State, detail, sent); }
        static DeliveryStatus Merge(DeliveryStatus prior, DeliveryStatus next)
        { return prior == DeliveryStatus.PossiblySent || next == DeliveryStatus.PossiblySent ? DeliveryStatus.PossiblySent :
            prior == DeliveryStatus.Sent || next == DeliveryStatus.Sent ? DeliveryStatus.Sent : DeliveryStatus.NotSent; }
        public Task<EnsureResult> EnsureOnAsync(CancellationToken ct) { return ExecuteAsync(true, false, ct); }
        public Task<EnsureResult> EnsureOffAsync(bool fast, CancellationToken ct) { return ExecuteAsync(false, fast, ct); }
        async Task<EnsureResult> ExecuteAsync(bool on, bool fast, CancellationToken ct)
        {
            bool entered = false;
            try
            {
                await operations.WaitAsync(ct).ConfigureAwait(false); entered = true; delivery = DeliveryStatus.NotSent;
                if (disposed) return Result(TvOutcome.Cancelled, "Cliente encerrado.");
                var deadline = new TvDeadline(clock, TimeSpan.FromSeconds(fast ? 2.7 : on ? config.WakeSeconds : 12));
                var observed = await ObserveLockedAsync(!fast, deadline.UpTo(fast ? 1.1 : 10), ct).ConfigureAwait(false);
                if (observed.IdentityVerified && HasAuthority && Power.State == (on ? TvState.On : TvState.Off))
                    return Result(TvOutcome.Confirmed, "Estado desejado já confirmado; nenhum comando enviado.");
                if (observed.Evidence == "IDENTITY_MISMATCH") return Result(TvOutcome.IdentityMismatch, "Identidade diferente do alvo.");
                if (on)
                {
                    if (observed.Net == Reachability.Reachable && HasAuthority && Power.State == TvState.Off)
                    {
                        var toggle = await ToggleAsync(TvState.On, deadline, false, ct).ConfigureAwait(false);
                        if (toggle.Delivery == DeliveryStatus.NotSent && toggle.Outcome != TvOutcome.AuthRequired) return toggle;
                    }
                    return await WakeAsync(deadline, observed.Net, ct).ConfigureAwait(false);
                }
                if (observed.Net != Reachability.Reachable) return Result(TvOutcome.Unreachable, "Sem rede verificada; desligamento não confirmado.");
            if (!HasAuthority || Power.State != TvState.On) return Result(TvOutcome.BlockedAmbiguous,
                "Confirme visualmente a TV ligada; nenhum toggle enviado.");
                return await ToggleAsync(TvState.Off, deadline, fast, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return Result(TvOutcome.Cancelled, "Operação cancelada; nenhum toggle será repetido.", entered ? delivery :
                DeliveryStatus.NotSent); }
            catch (IOException) { Volatile.Write(ref power, PowerSnapshot.Unknown); return Result(TvOutcome.PersistenceFailure,
                "Falha no diário; energia bloqueada.",
                delivery); }
            catch (UnauthorizedAccessException) { Volatile.Write(ref power, PowerSnapshot.Unknown); return Result(TvOutcome.PersistenceFailure,
                "Diário sem acesso; energia bloqueada.", delivery); }
            finally { if (entered) operations.Release(); }
        }
        async Task<TvObservation> ObserveLockedAsync(bool rediscover, TimeSpan budget, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (budget <= TimeSpan.Zero) throw new OperationCanceledException(ct);
            var observed = await transport.ObserveAsync(config, budget, rediscover, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            TvObservation value;
            if (observed.Status != TvNetwork.Reachable)
            {
                await ChangeAsync(PowerSnapshot.Unknown, ct).ConfigureAwait(false);
                value = new TvObservation(observed.Status == TvNetwork.Unreachable ? Reachability.Unreachable : Reachability.Unknown,
                    observed.Status == TvNetwork.IdentityMismatch ? "IDENTITY_MISMATCH" : observed.Detail);
            }
            else
            {
                if (observed.ReportedPower == TvState.On || observed.ReportedPower == TvState.Off)
                    await ChangeAsync(new PowerSnapshot(observed.ReportedPower.Value, EvidenceSource.DeviceReported, clock.UtcNow,
                        "Energia reportada pelo alvo identificado.", Guid.NewGuid().ToString("N")), ct).ConfigureAwait(false);
                else if (!HasAuthority && Power.AuthorityId != null) await ChangeAsync(PowerSnapshot.Unknown, ct).ConfigureAwait(false);
                value = new TvObservation(Reachability.Reachable, observed.Detail, true);
            }
            Volatile.Write(ref last, value); return value;
        }
        async Task<EnsureResult> ToggleAsync(TvState desired, TvDeadline deadline, bool fast, CancellationToken ct)
        {
            var prior = Power; var stamp = authorityAt;
            if (!Authority(prior, stamp)) return Result(TvOutcome.BlockedAmbiguous, "Confirmação expirada.");
            await ChangeAsync(new PowerSnapshot(TvState.Unknown, EvidenceSource.CommandUnconfirmed, clock.UtcNow,
                "Permissão consumida antes do toggle.", null, desired == TvState.On ? DesiredTvState.On : DesiredTvState.Off), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (!Authority(prior, stamp) || deadline.Expired) return Result(TvOutcome.BlockedAmbiguous, "Confirmação expirou antes do frame.");
            // O handshake tambem precisa caber na validade restante da autorizacao consumida.
            var ttl = TimeSpan.FromSeconds(config.ConfirmedSeconds);
            var remaining = ttl - clock.Elapsed(stamp);
            var wallRemaining = ttl - (clock.UtcNow - prior.At);
            if (wallRemaining < remaining) remaining = wallRemaining;
            if (deadline.Remaining < remaining) remaining = deadline.Remaining;
            if (remaining <= TimeSpan.Zero) return Result(TvOutcome.BlockedAmbiguous, "Autorização expirou antes do transporte.");
            delivery = DeliveryStatus.PossiblySent;
            var sent = await transport.SendKeyAsync(config, "KEY_POWER", remaining, !fast, ct).ConfigureAwait(false);
            delivery = sent.Delivery;
            if (desired == TvState.Off && sent.Delivery != DeliveryStatus.NotSent) offAttempt = clock.UtcNow;
            if (sent.Delivery == DeliveryStatus.NotSent)
            {
                if (!ct.IsCancellationRequested && Authority(prior, stamp))
                { await ChangeAsync(prior, ct).ConfigureAwait(false); authorityAt = stamp; }
                return Result(sent.Outcome, sent.Detail);
            }
            await ChangeAsync(new PowerSnapshot(TvState.Unknown, EvidenceSource.CommandUnconfirmed, clock.UtcNow,
                "Comando entregue ou possivelmente entregue; confirme a tela.", null,
                desired == TvState.On ? DesiredTvState.On : DesiredTvState.Off), CancellationToken.None).ConfigureAwait(false);
            return Result(sent.Delivery == DeliveryStatus.PossiblySent ? TvOutcome.PossiblySent : TvOutcome.Unconfirmed, sent.Detail, sent.Delivery);
        }
        async Task<EnsureResult> WakeAsync(TvDeadline deadline, Reachability initialNetwork, CancellationToken ct)
        {
            if (deadline.Expired) return Result(TvOutcome.NotSent, "Prazo de wake expirou.", delivery);
            await ChangeAsync(new PowerSnapshot(TvState.Unknown, EvidenceSource.CommandUnconfirmed, clock.UtcNow,
                "WoL solicitado; energia não confirmada.", null), ct).ConfigureAwait(false);
            var priorDelivery = delivery;
            delivery = Merge(priorDelivery, DeliveryStatus.PossiblySent);
            var burst = await transport.WakeAsync(config, ct).ConfigureAwait(false);
            delivery = Merge(priorDelivery, burst.Packets > 0 ? DeliveryStatus.Sent : DeliveryStatus.NotSent);
            ct.ThrowIfCancellationRequested();
            if (!burst.HasRoute || burst.Packets == 0) return Result(TvOutcome.NotSent, burst.Detail, delivery);
            if (!config.WakeRetry) return Result(TvOutcome.Unconfirmed, "WoL enviado; verificação automática desativada.", delivery);
            int silent = 0, attempt = 0; bool afterSleep = false;
            while (!deadline.Expired)
            {
                await clock.Delay(deadline.UpTo(config.BackoffSeconds[Math.Min(attempt++, config.BackoffSeconds.Length - 1)]), ct).ConfigureAwait(false);
                if (deadline.Expired) break;
                var observation = await ObserveLockedAsync(attempt >= 3, deadline.UpTo(5), ct).ConfigureAwait(false);
            if (observation.Evidence == "IDENTITY_MISMATCH") return Result(TvOutcome.IdentityMismatch,
                "Identidade trocada durante o wake; interrompido.", delivery);
                if (HasAuthority && Power.State == TvState.On) return Result(TvOutcome.Confirmed, "Alvo identificado reportou ligado.", delivery);
                if (observation.Net == Reachability.Reachable)
                {
                    if (afterSleep) return Result(TvOutcome.Unconfirmed, "Rede retornou após WoL; confirme a tela.", delivery);
                    silent = 0;
                    bool recent = offAttempt.HasValue && clock.UtcNow >= offAttempt.Value && clock.UtcNow - offAttempt.Value < TimeSpan.FromSeconds(210);
                    if (initialNetwork == Reachability.Unreachable && !recent && attempt == 1)
                        return Result(TvOutcome.Unconfirmed, "Rede voltou; energia ainda não confirmada.", delivery);
                }
                else if (observation.Net == Reachability.Unreachable && ++silent >= 2 && !afterSleep &&
                    (!offAttempt.HasValue || clock.UtcNow < offAttempt.Value || clock.UtcNow - offAttempt.Value >= TimeSpan.FromSeconds(150)))
                {
                    afterSleep = true;
                    var retry = await transport.WakeAsync(config, ct).ConfigureAwait(false);
                    if (!retry.HasRoute || retry.Packets == 0) return Result(TvOutcome.NotSent, retry.Detail, delivery);
                }
            }
            return Result(TvOutcome.Unconfirmed, "Prazo terminou sem confirmação física; nenhum toggle repetido.", delivery);
        }
        public async Task<TvObservation> ObserveAsync(CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try { return await ObserveLockedAsync(true, TimeSpan.FromSeconds(10), ct).ConfigureAwait(false); }
            finally { operations.Release(); }
        }
        public async Task PrepareAsync(CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try { if (!disposed) await transport.PrepareAsync(config, ct).ConfigureAwait(false); }
            finally { operations.Release(); }
        }
        public async Task<PairResult> PairAsync(bool replace, CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try { return disposed ? new PairResult(false, "Cliente encerrado.") : await transport.PairAsync(config, replace, ct).ConfigureAwait(false); }
            finally { operations.Release(); }
        }
        public async Task CloseAsync()
        {
            await operations.WaitAsync().ConfigureAwait(false);
            try { if (!disposed) { disposed = true; await transport.CloseAsync().ConfigureAwait(false); } }
            finally { operations.Release(); }
        }
    }
}
