// Processamento serial do coordenador; efeitos reais apenas pelo host normal e por preferencia explicita.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed partial class TvCoordinator
    {
        bool CanAct(bool suspend)
        { lock (gate) return enabled && config.Enabled && (mode == TvMode.Running || suspend && mode == TvMode.Suspending); }
        bool CanManual()
        { lock (gate) return persistenceError == null && (mode == TvMode.Running || mode == TvMode.Disabled); }
        static TvReply Reply(Guid id, EnsureResult result) { return new TvReply(id, result.Outcome, result.Detail, true, result.Delivery); }
        async Task<TvReply> ProcessAsync(Job job, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); var request = job.Request;
            switch (request.Kind)
            {
                case TvRequestKind.Stop: return new TvReply(request.Id, TvOutcome.NoAction, "Encerramento solicitado.");
                case TvRequestKind.CancelSuspend:
                    lock (gate) { state.SleepId = null; state.SuspendedAt = null; state.ResumeConsumed = true; }
                    await SaveAsync(ct).ConfigureAwait(false);
                    lock (gate) if (mode == TvMode.Suspending) mode = enabled ? TvMode.Running : TvMode.Disabled;
                    return new TvReply(request.Id, TvOutcome.NoAction, "Suspensão do PC não concluída; nenhuma ação de energia repetida.");
                case TvRequestKind.Options:
                {
                    var next = request.Options;
                    if (next == null) return new TvReply(request.Id, TvOutcome.InvalidCommand, "Opções ausentes.");
                    next.Validate();
                    lock (gate)
                    {
                        if (next.Mac != config.Mac || next.Duid != config.Duid || next.Ip != config.Ip || next.ClientName != config.ClientName ||
                            next.Enabled != config.Enabled)
                            return new TvReply(request.Id, TvOutcome.InvalidCommand, "Opções não podem trocar alvo, cliente ou interruptor geral.");
                    }
                    await store.SaveConfigAsync(next, ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested();
                    lock (gate) config = next; tv.UpdateConfiguration(next);
                    if (next.KeepHdrOff && CanAct(false) && !display.SetHdr(false))
                        return new TvReply(request.Id, TvOutcome.QueryFailed, "Opções salvas, mas o HDR da Samsung não foi confirmado.");
                    return new TvReply(request.Id, TvOutcome.NoAction, "Opções da TV salvas.");
                }
                case TvRequestKind.Enable:
                {
                    TvCoreConfig next; lock (gate) { next = config.Copy(); next.Enabled = request.Value; }
                    await store.SaveConfigAsync(next, ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested();
                    lock (gate)
                    {
                        config = next;
                    mode = !enabled ? TvMode.Disabled : mode == TvMode.Suspending || mode == TvMode.Stopping || mode == TvMode.Faulted ? mode : TvMode.Running;
                        if (job.Generation == generation) { desired = DesiredTvState.Keep; manualOverride = false; state.Desired = desired; }
                    }
                    tv.UpdateConfiguration(next); await SaveAsync(ct).ConfigureAwait(false);
                    lock (gate) persistenceError = null;
                    return new TvReply(request.Id, TvOutcome.NoAction, "Preferência salva; nenhum comando inicial emitido.");
                }
                case TvRequestKind.HdrPreference:
                {
                    TvCoreConfig next; lock (gate) { next = config.Copy(); next.KeepHdrOff = request.Value; }
                    await store.SaveConfigAsync(next, ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested(); lock (gate) config = next;
                    tv.UpdateConfiguration(next);
                    if (request.Value && CanAct(false)) { ct.ThrowIfCancellationRequested(); display.SetHdr(false); }
                    return new TvReply(request.Id, TvOutcome.NoAction, "Preferência HDR salva.");
                }
                case TvRequestKind.HdrOn: case TvRequestKind.HdrOff:
                    if (!CanManual()) return new TvReply(request.Id, TvOutcome.Disabled, "HDR bloqueado pelo modo atual.");
                    ct.ThrowIfCancellationRequested();
                    return new TvReply(request.Id, display.SetHdr(request.Kind == TvRequestKind.HdrOn) ? TvOutcome.NoAction : TvOutcome.QueryFailed,
                        "Resultado do ajuste de HDR no alvo.");
                case TvRequestKind.ConfirmOn: case TvRequestKind.ConfirmOff:
            return Reply(request.Id, await tv.ConfirmAsync(request.Kind == TvRequestKind.ConfirmOn ? TvState.On : TvState.Off, ct).ConfigureAwait(false));
                case TvRequestKind.Forget:
                    await tv.ForgetAsync(ct).ConfigureAwait(false); return new TvReply(request.Id, TvOutcome.NoAction, "Confirmação descartada.");
                case TvRequestKind.Pair:
                    var pair = await tv.PairAsync(request.Value, ct).ConfigureAwait(false);
                    return new TvReply(request.Id, pair.Authorized ? TvOutcome.NoAction : TvOutcome.AuthRequired, pair.Detail);
                case TvRequestKind.Observe:
                    var observed = await tv.ObserveAsync(ct).ConfigureAwait(false);
                    return new TvReply(request.Id, observed.IdentityVerified ? TvOutcome.NoAction : TvOutcome.QueryFailed, observed.Evidence);
            }
            if (request.Kind == TvRequestKind.Suspend || request.Kind == TvRequestKind.SessionEnding)
            {
                bool sleep = request.Kind == TvRequestKind.Suspend;
                lock (gate)
                {
                    state.Desired = desired; state.SleepId = sleep ? Guid.NewGuid().ToString("N") : null;
                    state.SuspendedAt = sleep ? clock.UtcNow : (DateTimeOffset?)null; state.ResumeConsumed = !sleep;
                }
                await SaveAsync(ct).ConfigureAwait(false);
                if (!CanAct(true) || config.Suspend != TvAction.Off) return new TvReply(request.Id, TvOutcome.NoAction, "Suspensão sem comando de energia.");
                return await PowerAsync(request.Id, DesiredTvState.Off, !request.Value, true, ct).ConfigureAwait(false);
            }
            if (request.Kind == TvRequestKind.Resume)
            {
                DesiredTvState previous;
                lock (gate)
                {
                    bool valid = !state.ResumeConsumed && !string.IsNullOrEmpty(state.SleepId) && state.SuspendedAt.HasValue &&
                        clock.UtcNow >= state.SuspendedAt.Value && clock.UtcNow - state.SuspendedAt.Value <= TimeSpan.FromHours(24);
                    previous = valid ? state.Desired : DesiredTvState.Keep; state.ResumeConsumed = true;
                }
                await SaveAsync(ct).ConfigureAwait(false);
                if (!CanAct(false)) return new TvReply(request.Id, TvOutcome.Disabled, "Retomada consumida sem efeitos.");
                if (job.Generation != Interlocked.Read(ref generation)) return new TvReply(request.Id, TvOutcome.NoAction,
                    "Intenção mais recente tem precedência sobre a retomada.");
                if (previous != DesiredTvState.Keep) lock (gate) manualOverride = true;
                var intent = config.Resume == TvResume.Keep ? DesiredTvState.Keep : config.Resume == TvResume.Off ? DesiredTvState.Off : previous;
                return await PowerAsync(request.Id, intent, false, false, ct).ConfigureAwait(false);
            }
            if (request.Kind == TvRequestKind.PowerOn || request.Kind == TvRequestKind.PowerOff)
                return await PowerAsync(request.Id, request.Kind == TvRequestKind.PowerOn ? DesiredTvState.On : DesiredTvState.Off, false, false, ct,
                    true).ConfigureAwait(false);
            if (!CanAct(false)) return new TvReply(request.Id, TvOutcome.Disabled, "Automação bloqueada pelo modo atual.");
            if (request.Kind == TvRequestKind.Startup)
            {
                if (config.KeepHdrOff) { ct.ThrowIfCancellationRequested(); if (!display.SetHdr(false)) return new TvReply(request.Id, TvOutcome.QueryFailed,
                    "HDR da Samsung indisponível ou não confirmado."); }
                if (display.Read().SamsungActive) await tv.PrepareAsync(ct).ConfigureAwait(false);
                return new TvReply(request.Id, TvOutcome.NoAction, "Inicialização não altera energia.");
            }
            if (request.Kind == TvRequestKind.PowerOn || request.Kind == TvRequestKind.PowerOff || request.Kind == TvRequestKind.DualSenseVerified)
            {
                if (request.Kind == TvRequestKind.DualSenseVerified && !VerifiedPs) return new TvReply(request.Id, TvOutcome.BlockedAmbiguous,
                    "PS fora da janela válida de retomada.");
                if (request.Kind == TvRequestKind.DualSenseVerified && !config.PsWakeEnabled) return new TvReply(request.Id, TvOutcome.NoAction,
                    "Ligar pelo PS está desligado.");
                var intent = request.Kind == TvRequestKind.DualSenseVerified ? TvPolicy.Action(config.DualSense) : request.Kind == TvRequestKind.PowerOff ?
                    DesiredTvState.Off : DesiredTvState.On;
                return await PowerAsync(request.Id, intent, false, false, ct).ConfigureAwait(false);
            }
            if (request.Kind == TvRequestKind.NetworkRecovered)
            {
                DesiredTvState intent; lock (gate) intent = desired;
                return intent == DesiredTvState.On ? await PowerAsync(request.Id, intent, false, false, ct).ConfigureAwait(false) : new TvReply(request.Id,
                    TvOutcome.NoAction, "Rede não cria intenção de ligar.");
            }
            if (request.Kind == TvRequestKind.DisplayChanged)
            {
                await clock.Delay(TimeSpan.FromMilliseconds(config.DebounceMs), ct).ConfigureAwait(false);
                var topology = display.Read(); ct.ThrowIfCancellationRequested(); bool same;
            lock (gate) { same = Key(topology) == topologyKey; if (topology.Valid && topology.Topology != TvTopology.NoActiveDisplay) topologyKey
                = Key(topology); }
                await SaveAsync(ct).ConfigureAwait(false);
                if (topology.Valid && config.KeepHdrOff) { ct.ThrowIfCancellationRequested(); if (!display.SetHdr(false)) return new TvReply(request.Id,
                    TvOutcome.QueryFailed, "Mudança de tela observada; HDR da Samsung não confirmado."); }
                if (!config.TopologyEnabled || manualOverride || job.ResumeGuard || same || !topology.Valid || topology.Topology == TvTopology.NoActiveDisplay)
                    return new TvReply(request.Id, TvOutcome.NoAction, "Topologia informativa; intenção preservada.");
                return await PowerAsync(request.Id, TvPolicy.Topology(config, topology), false, false, ct).ConfigureAwait(false);
            }
            return new TvReply(request.Id, TvOutcome.InvalidCommand, "Comando desconhecido.");
        }
        async Task<TvReply> PowerAsync(Guid id, DesiredTvState intent, bool fast, bool preserve, CancellationToken ct, bool manual = false)
        {
            ct.ThrowIfCancellationRequested();
            if (!(manual ? CanManual() : CanAct(preserve))) return new TvReply(id, TvOutcome.Disabled, "Energia bloqueada pelo modo atual.");
            if (!preserve) { lock (gate) { desired = intent; state.Desired = intent; } await SaveAsync(ct).ConfigureAwait(false); }
            if (intent == DesiredTvState.Keep) return new TvReply(id, TvOutcome.NoAction, "Nenhum comando de energia solicitado.");
        var result = intent == DesiredTvState.On ? await tv.EnsureOnAsync(ct).ConfigureAwait(false) : await tv.EnsureOffAsync(fast, ct).ConfigureAwait(false);
            lock (gate)
            {
                activeDelivery = result.Delivery; lastResult = result;
        if (result.Outcome == TvOutcome.PersistenceFailure) { mode = TvMode.Disabled; enabled = false; persistenceError = "PowerStatePersistenceFailure"; }
            }
            // Conservar resultado/entrega mesmo se a persistencia posterior ao efeito falhar.
            await SaveAsync(CancellationToken.None).ConfigureAwait(false);
            if (intent == DesiredTvState.On && tv.LastObservation.IdentityVerified && tv.LastObservation.Net == Reachability.Reachable)
                await tv.PrepareAsync(ct).ConfigureAwait(false);
            return Reply(id, result);
        }
    }
}
