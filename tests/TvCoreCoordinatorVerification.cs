// Barreiras, geracoes, limites e retomada com transporte/relogio/tela substituidos.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;

namespace XGBP.Design
{
    static partial class TvCoreSuite
    {
        static async Task CoordinatorCases(Action<bool, string> check)
        {
            var t = new CoreTransport(); var clock = new CoreClock(); var store = new CoreStore(); var d = new CoreDisplay();
            var cfg = Config(); cfg.Enabled = false; cfg.KeepHdrOff = true;
            var c = new TvCoordinator(cfg, new TvStoredState(), t, d, store, clock);
            try
            {
                await Limit(Send(c, TvRequestKind.Startup).Completion).ConfigureAwait(false);
                await Limit(Send(c, TvRequestKind.DisplayChanged).Completion).ConfigureAwait(false);
                await Limit(Send(c, TvRequestKind.HdrPreference, true).Completion).ConfigureAwait(false);
                check(t.Wakes == 0 && t.Sends == 0 && d.Writes == 0, "desativado bloqueia energia e HDR na inicializacao");
                await Limit(Send(c, TvRequestKind.Enable, true).Completion).ConfigureAwait(false);
                check(store.Configuration.Enabled && c.Snapshot().Enabled && t.Wakes == 0, "ativar persiste sem ligar imediatamente");
                var request = new TvRequest(Guid.NewGuid(), TvRequestKind.Pair);
                await Limit(c.Submit(request).Completion).ConfigureAwait(false); await Limit(c.Submit(request).Completion).ConfigureAwait(false);
                check(t.Pairs == 1, "mesmo ID nunca reexecuta pedido retido");
            check(c.Submit(new TvRequest(request.Id, TvRequestKind.ConfirmOn)).Receipt.Outcome == TvOutcome.InvalidCommand,
                "replay com outro conteudo recusado");
                check(c.Submit(new TvRequest(Guid.Empty, TvRequestKind.PowerOn)).Receipt.Outcome == TvOutcome.InvalidCommand, "ID vazio recusado");
                await Limit(Send(c, TvRequestKind.ConfirmOn).Completion).ConfigureAwait(false);
                var on = await Limit(Send(c, TvRequestKind.PowerOn).Completion).ConfigureAwait(false);
                check(on.Outcome == TvOutcome.Confirmed && t.Prepares == 1, "ligar confirmado prepara canal para suspensao");
                var sleep = await Limit(Send(c, TvRequestKind.Suspend).Completion).ConfigureAwait(false);
                check(sleep.Delivery == DeliveryStatus.Sent && !t.LastHandshake && t.Sends == 1, "suspender usa caminho rapido sem handshake");
                check(store.State.SleepId != null && !store.State.ResumeConsumed && store.State.Desired == DesiredTvState.On,
                    "suspensao preserva intencao e cria sessao"
                    );
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            foreach (double age in new[] { 60.0, 86401.0, -1.0 })
            {
                clock = new CoreClock(); store = new CoreStore(); t = new CoreTransport(); cfg = Config(); cfg.WakeRetry = false;
                var state = new TvStoredState { Desired = DesiredTvState.On, SleepId = "sleep-fixture", ResumeConsumed = false, SuspendedAt =
                    clock.UtcNow.AddSeconds(-age) };
                c = new TvCoordinator(cfg, state, t, new CoreDisplay(), store, clock);
                try
                {
var a = c.SystemResume(); var b = c.SystemResume(); await Limit(a.Completion).ConfigureAwait(false); await Limit(b.Completion).ConfigureAwait(false);
                check(a.Receipt.Id == b.Receipt.Id && store.State.ResumeConsumed && t.Wakes == (age == 60 ? 1 : 0),
                    "retomada deduplicada e limitada a 24h: " + age);
                    await Limit(Send(c, TvRequestKind.Resume).Completion).ConfigureAwait(false);
                    check(t.Wakes == (age == 60 ? 1 : 0), "sessao de sono consumida antes dos efeitos: " + age);
                }
                finally { c.CloseAsync().GetAwaiter().GetResult(); }
            }
            clock = new CoreClock(); store = new CoreStore(); t = new CoreTransport(); cfg = Config(); cfg.Enabled = false;
            c = new TvCoordinator(cfg, new TvStoredState { Desired = DesiredTvState.On, SleepId = "sleep-disabled", ResumeConsumed = false, SuspendedAt =
                clock.UtcNow.AddMinutes(-1) }, t, new CoreDisplay(), store, clock);
            try { await Limit(c.SystemResume().Completion).ConfigureAwait(false); check(store.State.ResumeConsumed && t.Wakes == 0,
                "retomada desativada consome sessao sem efeitos"); }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            store = new CoreStore(); c = new TvCoordinator(Config(), new TvStoredState(), new CoreTransport(), new CoreDisplay(), store, new CoreClock());
            try { await Limit(Send(c, TvRequestKind.SessionEnding).Completion).ConfigureAwait(false); check(store.State.ResumeConsumed &&
                store.State.SleepId == null
                && store.State.SuspendedAt == null, "fim de sessao invalida retomada anterior"); }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            foreach (int failAt in new[] { 3, 4, 5 })
            {
                int failedWrite = failAt; store = new CoreStore(); store.Before = delegate(int n, CancellationToken ct) { if (n == failedWrite)
                    throw new IOException(
                    "Falha de diario simulada."); return Task.FromResult(0); };
                t = new CoreTransport(); c = new TvCoordinator(Config(), new TvStoredState(), t, new CoreDisplay(), store, new CoreClock());
                try
                {
                    await Limit(Send(c, TvRequestKind.ConfirmOn).Completion).ConfigureAwait(false);
                    var result = await Limit(Send(c, TvRequestKind.PowerOff).Completion).ConfigureAwait(false);
                    check(result.Outcome == TvOutcome.PersistenceFailure && !c.Snapshot().Enabled && t.Sends == (failAt == 3 ? 0 : 1),
                        "falha de diario bloqueia automacao: escrita " + failAt);
                    check(failAt == 3 || result.Delivery == DeliveryStatus.Sent, "entrega preservada na falha posterior: escrita " + failAt);
                    await Limit(Send(c, TvRequestKind.NetworkRecovered).Completion).ConfigureAwait(false); check(t.Wakes == 0,
                        "recuperacao de rede nao contorna bloqueio: escrita " + failAt);
                    await Limit(Send(c, TvRequestKind.Enable, true).Completion).ConfigureAwait(false);
                    check(c.Snapshot().Enabled && c.Snapshot().PersistenceError == null, "recuperacao explicita exige nova persistencia: escrita " + failAt);
                }
                finally { c.CloseAsync().GetAwaiter().GetResult(); }
            }
            var entered = Signal(); var release = Signal(); store = new CoreStore();
        store.Before = async delegate(int n, CancellationToken ct) { if (n == 1) { entered.TrySetResult(true); await release.Task.ConfigureAwait(false); } };
            t = new CoreTransport(); d = new CoreDisplay { Value = new TvDisplayState { Topology = TvTopology.PcScreenOnly } };
            c = new TvCoordinator(Config(), new TvStoredState(), t, d, store, new CoreClock());
            try
            {
                var confirm = Send(c, TvRequestKind.ConfirmOn); await Limit(entered.Task).ConfigureAwait(false);
                var off = Send(c, TvRequestKind.PowerOff); d.Value = new TvDisplayState { Topology = TvTopology.Extend, SamsungActive = true };
                var change = Send(c, TvRequestKind.DisplayChanged); release.TrySetResult(true);
                await Limit(confirm.Completion).ConfigureAwait(false); var result = await Limit(off.Completion).ConfigureAwait(false); await
                    Limit(change.Completion).ConfigureAwait(false);
                check(result.Outcome != TvOutcome.StaleIntent && c.Snapshot().Desired == DesiredTvState.Off && t.Wakes == 0,
                    "pedido manual tem precedencia sobre mudanca de topologia");
            }
            finally { release.TrySetResult(true); c.CloseAsync().GetAwaiter().GetResult(); }
            // A barreira continua disponivel com a fila comum cheia e cancela a operacao anterior.
            entered = Signal(); var capturedEntered = entered; t = new CoreTransport();
            t.Observe = async delegate(CancellationToken ct) { capturedEntered.TrySetResult(true); await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new NetworkObservation(TvNetwork.Reachable); };
            store = new CoreStore(); c = new TvCoordinator(Config(), new TvStoredState(), t, new CoreDisplay(), store, new CoreClock());
            try
            {
                var on = Send(c, TvRequestKind.PowerOn); await Limit(entered.Task).ConfigureAwait(false); int busy = 0;
                for (int i = 0; i < 70; i++) if (Send(c, TvRequestKind.HdrPreference).Receipt.Outcome == TvOutcome.Busy) busy++;
                var disable = Send(c, TvRequestKind.Enable, false);
                check(!c.Snapshot().Enabled && busy == 6, "fila limitada a 64 reserva barreira de desativacao");
                await Limit(disable.Completion).ConfigureAwait(false); var cancelled = await Limit(on.Completion).ConfigureAwait(false);
                check(cancelled.Outcome == TvOutcome.Cancelled && t.Wakes == 0 && t.Sends == 0 && !store.Configuration.Enabled,
                    "barreira cancela wake e persiste desativacao");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            // Encher o historico nao bloqueia desativar ou parar.
        store = new CoreStore(); t = new CoreTransport(); c = new TvCoordinator(Config(), new TvStoredState(), t, new CoreDisplay(), store, new CoreClock());
            try
            {
                for (int i = 0; i < 992; i++) await Limit(Send(c, TvRequestKind.Observe).Completion).ConfigureAwait(false);
                check(Send(c, TvRequestKind.Observe).Receipt.Outcome == TvOutcome.Busy, "historico comum limitado a 992");
                for (int i = 0; i < 32; i++) await Limit(Send(c, TvRequestKind.Enable, false).Completion).ConfigureAwait(false);
                check((await Limit(Send(c, TvRequestKind.Enable, false).Completion).ConfigureAwait(false)).Outcome == TvOutcome.NoAction &&
                    !store.Configuration.Enabled,
                    "historico 1024 preserva desativacao de emergencia");
                check((await Limit(Send(c, TvRequestKind.Stop).Completion).ConfigureAwait(false)).Outcome == TvOutcome.NoAction && t.Sends == 0,
                    "historico cheio nao bloqueia parar");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            clock = new CoreClock(); cfg = Config(); cfg.WakeRetry = false; t = new CoreTransport();
            c = new TvCoordinator(cfg, new TvStoredState(), t, new CoreDisplay(), new CoreStore(), clock);
            try
            {
                c.NoteVerifiedPs(); clock.Advance(1); await Limit(Send(c, TvRequestKind.Suspend).Completion).ConfigureAwait(false);
                clock.Advance(1); await Limit(c.SystemResume().Completion).ConfigureAwait(false);
                check(!c.VerifiedPs && t.Wakes == 0, "PS anterior a suspensao nao autoriza despertar");
                clock.Advance(1); c.NoteVerifiedPs(); await Until(() => t.Wakes == 1).ConfigureAwait(false);
                c.NoteVerifiedPs(); await Limit(Send(c, TvRequestKind.Observe).Completion).ConfigureAwait(false);
                check(c.VerifiedPs && t.Wakes == 1, "PS valido apos retomada cria uma unica intencao");
                clock.Advance(21); c.NoteVerifiedPs(); check(!c.VerifiedPs && t.Wakes == 1, "PS fora dos 20s nao liga a TV");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            // Tempo gasto antes de selecionar a barreira integra o prazo de 2,8s.
clock = new CoreClock(); var barrierClock = clock; entered = Signal(); release = Signal(); var barrierEntered = entered; var barrierRelease = release;
            store = new CoreStore(); store.Before = async delegate(int n, CancellationToken ct) { if (n == 1) { barrierEntered.TrySetResult(true); await
                barrierRelease.Task.ConfigureAwait(false); ct.ThrowIfCancellationRequested(); } };
            t = new CoreTransport(); c = new TvCoordinator(Config(), new TvStoredState(), t, new CoreDisplay(), store, clock);
            try
            {
                var confirm = Send(c, TvRequestKind.ConfirmOn); await Limit(entered.Task).ConfigureAwait(false);
                var sleep = Send(c, TvRequestKind.Suspend); barrierClock.Advance(3); release.TrySetResult(true);
                await Limit(confirm.Completion).ConfigureAwait(false);
                check((await Limit(sleep.Completion).ConfigureAwait(false)).Outcome == TvOutcome.Cancelled && t.Sends == 0,
                    "barreira expirada em 2,8s nao inicia efeitos"
                    );
            }
            finally { release.TrySetResult(true); c.CloseAsync().GetAwaiter().GetResult(); }
        }
    }
}
