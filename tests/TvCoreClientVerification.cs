// Casos de energia portados do Orchestrator, sem rede.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;

namespace XGBP.Design
{
    static partial class TvCoreSuite
    {
        static async Task ClientCases(Action<bool, string> check)
        {
            var clock = new CoreClock(); var t = new CoreTransport(); var c = Client(t, clock);
            try
            {
                var off = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                check(off.Outcome == TvOutcome.BlockedAmbiguous && t.Sends == 0, "desconhecida: desligar bloqueado");
                var on = await c.EnsureOnAsync(None).ConfigureAwait(false);
                check(!on.Success && !c.HasAuthority && t.Sends == 0, "rede acessivel nunca prova tela ligada");
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false);
                off = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                check(off.Delivery == DeliveryStatus.Sent && off.Outcome == TvOutcome.Unconfirmed && !c.HasAuthority,
                    "envio consome autoridade sem anunciar sucesso fisico");
                off = await c.EnsureOffAsync(true, None).ConfigureAwait(false);
                check(off.Outcome == TvOutcome.BlockedAmbiguous && t.Sends == 1, "nem suspensao repete toggle sem nova confirmacao");
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false);
                var many = await Task.WhenAll(Enumerable.Range(0, 50).Select(i => c.EnsureOffAsync(false, None))).ConfigureAwait(false);
                check(t.Sends == 2 && t.MaxEffects == 1 && many.Count(x => x.Delivery == DeliveryStatus.Sent) == 1,
                    "50 desligamentos paralelos: um toggle e efeitos serializados");
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false); clock.Advance(121); clock.Wall(-120);
                off = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                check(off.Outcome == TvOutcome.BlockedAmbiguous && t.Sends == 2, "expiracao monotona resiste a UTC atrasado");
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false); clock.Wall(-1);
                check(!c.HasAuthority, "confirmacao com data futura nao autoriza"); clock.Wall(1);
                using (var cancelled = new CancellationTokenSource())
                { cancelled.Cancel(); off = await c.EnsureOffAsync(false, cancelled.Token).ConfigureAwait(false); check(off.Outcome ==
                    TvOutcome.Cancelled && off.Delivery
                    == DeliveryStatus.NotSent && c.HasAuthority, "cancelamento antes da fila preserva autoridade e nao herda entrega"); }
                await c.ForgetAsync(None).ConfigureAwait(false); check(!c.HasAuthority, "esquecer remove permissao");
                check((await c.ConfirmAsync(TvState.Unknown, None).ConfigureAwait(false)).Outcome == TvOutcome.InvalidCommand, "confirmacao invalida recusada");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            clock = new CoreClock(); t = new CoreTransport(); c = Client(t, clock);
            try
            {
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false); clock.Advance(119.5);
                await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                check(t.Sends == 1 && t.LastBudget <= TimeSpan.FromSeconds(0.5), "handshake limitado a validade restante da confirmacao");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            t = new CoreTransport { SendKey = ct => Task.FromResult(new SendResult(DeliveryStatus.PossiblySent, TvOutcome.PossiblySent, "Entrega incerta.")),
                Wake = ct => Task.FromResult(new WakeResult(0, false, "Sem interface.")) };
            c = Client(t, new CoreClock());
            try
            {
                await c.ConfirmAsync(TvState.Off, None).ConfigureAwait(false);
                var result = await c.EnsureOnAsync(None).ConfigureAwait(false);
                check(result.Delivery == DeliveryStatus.PossiblySent && t.Sends == 1 && t.Wakes == 1 && !c.HasAuthority,
                    "WoL sem pacotes nao apaga entrega possivel do toggle anterior");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            foreach (DeliveryStatus delivery in new[] { DeliveryStatus.NotSent, DeliveryStatus.PossiblySent, DeliveryStatus.Sent })
            {
                var sent = delivery; t = new CoreTransport { SendKey = ct => Task.FromResult(new SendResult(sent, TvOutcome.NotSent,
                    "Substituto.")) }; c = Client(t, new
                    CoreClock());
                try
                {
                    await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false);
                    var result = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                    check(result.Delivery == sent && c.HasAuthority == (sent == DeliveryStatus.NotSent), "autoridade restaurada apenas sem entrega: " + sent);
                    if (sent != DeliveryStatus.NotSent) { await c.EnsureOffAsync(false, None).ConfigureAwait(false); check(t.Sends == 1,
                        "entrega nunca e repetida: " + sent);
                        }
                }
                finally { c.CloseAsync().GetAwaiter().GetResult(); }
            }
            foreach (TvNetwork status in new[] { TvNetwork.IdentityMismatch, TvNetwork.QueryFailed, TvNetwork.Unreachable })
            {
            var observed = status; t = new CoreTransport { Observe = ct => Task.FromResult(new NetworkObservation(observed)) }; c = Client(t, new CoreClock());
                try
                {
                    await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false);
                    var result = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                    check(!result.Success && !c.HasAuthority && t.Sends == 0, "observacao falha nao revive hipotese: " + status);
                    if (status == TvNetwork.IdentityMismatch) { result = await c.EnsureOnAsync(None).ConfigureAwait(false); check(result.Outcome
                        == TvOutcome.IdentityMismatch
                        && t.Wakes == 0, "identidade trocada bloqueia tambem WoL"); }
                }
                finally { c.CloseAsync().GetAwaiter().GetResult(); }
            }
            var store = new CoreStore(); t = new CoreTransport(); c = Client(t, new CoreClock(), store);
            try
            {
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false); store.Fail = true;
                var result = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                check(result.Outcome == TvOutcome.PersistenceFailure && t.Sends == 0 && !c.HasAuthority, "diario falha antes do frame: efeito bloqueado");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            store = new CoreStore(); t = new CoreTransport(); c = Client(t, new CoreClock(), store);
            try
            {
                await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false);
                store.Before = delegate(int n, CancellationToken ct) { if (n == 3) throw new IOException("Falha apos efeito."); return Task.FromResult(0); };
                var result = await c.EnsureOffAsync(false, None).ConfigureAwait(false);
                check(result.Outcome == TvOutcome.PersistenceFailure && result.Delivery == DeliveryStatus.Sent && t.Sends == 1 && !c.HasAuthority,
                    "falha depois do efeito preserva entrega e remove autoridade");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            t = new CoreTransport { Observe = ct => Task.FromResult(new NetworkObservation(TvNetwork.Reachable, TvState.On)) }; c = Client(t, new CoreClock());
            try { check((await c.EnsureOnAsync(None).ConfigureAwait(false)).Success && t.Sends == 0 && t.Wakes == 0,
                "energia reportada pelo alvo permite confirmar sem comando"); }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            t = new CoreTransport { Wake = ct => Task.FromResult(new WakeResult(0, false, "Sem sub-rede.")) }; c = Client(t, new CoreClock());
        try { check((await c.EnsureOnAsync(None).ConfigureAwait(false)).Outcome == TvOutcome.NotSent && t.Sends == 0,
            "sem interface WoL falha explicitamente"); }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            clock = new CoreClock(); DateTimeOffset offAt = clock.UtcNow; clock.Advance(26); bool panel = false; var time = clock;
            t = new CoreTransport();
            t.Observe = delegate(CancellationToken ct) { double age = (time.UtcNow - offAt).TotalSeconds; return Task.FromResult(new
                NetworkObservation(panel || age <
                18 || age >= 45 && age < 150 ? TvNetwork.Reachable : TvNetwork.Unreachable)); };
            t.Wake = delegate(CancellationToken ct) { if ((time.UtcNow - offAt).TotalSeconds >= 150) panel = true; return Task.FromResult(new
                WakeResult(3, true,
                "Modelo temporal.")); };
            var config = Config(); config.WakeSeconds = 240; config.BackoffSeconds = new[] { 2, 4, 7, 11, 16, 25 }; c = Client(t, clock, null, config, offAt);
            try
            {
                var result = await c.EnsureOnAsync(None).ConfigureAwait(false);
                check(panel && (clock.UtcNow - offAt).TotalSeconds >= 150 && t.Wakes == 2, "retomada curta espera 150s e usa so WoL");
                check(!result.Success && !c.HasAuthority && t.Sends == 0, "retorno da rede depois de desligar nao e sucesso fisico");
            }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
            clock = new CoreClock(); var expiringClock = clock; store = new CoreStore();
            store.Before = delegate(int n, CancellationToken ct) { if (n == 2) expiringClock.Advance(121); return Task.FromResult(0); };
            t = new CoreTransport(); c = Client(t, clock, store);
            try { await c.ConfirmAsync(TvState.On, None).ConfigureAwait(false); check((await c.EnsureOffAsync(false, None).ConfigureAwait(false)).Outcome ==
                TvOutcome.BlockedAmbiguous && t.Sends == 0, "autoridade revalidada depois do diario e antes do frame"); }
            finally { c.CloseAsync().GetAwaiter().GetResult(); }
        }
    }
}
