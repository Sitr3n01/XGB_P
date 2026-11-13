// Automacao e adaptadores em fixtures, incluindo HDR e energia sem chamadas nativas reais.
using System;
using System.Threading.Tasks;
using XGBP.Tv;
using XGBP.Power;

namespace XGBP.Design
{
    static partial class TvCoreSuite
    {
        static async Task ServiceCases(Action<bool, string> check)
        {
        var cfg = Config(); cfg.Enabled = false; var t = new CoreTransport(); var store = new CoreStore(); var d = new CoreDisplay(); var clock =
            new CoreClock();
            var s = new TvService(cfg, new TvStoredState(), t, d, store, clock);
            try
            {
                await s.SendAsync(TvRequestKind.ConfirmOn).ConfigureAwait(false);
                var off = await s.SendAsync(TvRequestKind.PowerOff).ConfigureAwait(false);
                check(off.Delivery == DeliveryStatus.Sent && t.Sends == 1 && !s.Configuration.Enabled, "comando manual independe da automacao geral");
                var next = s.Configuration; next.KeepHdrOff = true; next.PsWakeEnabled = false;
                await s.SaveOptionsAsync(next).ConfigureAwait(false);
                check(store.Configuration.KeepHdrOff && !s.Configuration.PsWakeEnabled && d.Writes == 0,
                    "opcoes persistem com automacao desligada sem escrita automatica de HDR");
                await s.SendAsync(TvRequestKind.Enable, true).ConfigureAwait(false);
                next = s.Configuration; next.TopologyEnabled = false; await s.SaveOptionsAsync(next).ConfigureAwait(false);
                d.Value = new TvDisplayState { Topology = TvTopology.Extend, SamsungActive = true };
                await s.SendAsync(TvRequestKind.DisplayChanged).ConfigureAwait(false);
                check(t.Wakes == 0, "topologia desligada nao cria intencao de ligar");
                next = s.Configuration; next.Duid = "uuid:other";
                check((await s.SaveOptionsAsync(next).ConfigureAwait(false)).Outcome == TvOutcome.InvalidCommand && s.Configuration.Duid == Config().Duid,
                    "opcoes nao trocam identidade do alvo");
            }
            finally { s.CloseAsync().GetAwaiter().GetResult(); }
            foreach (bool keep in new[] { false, true })
            {
                cfg = Config(); cfg.PsWakeEnabled = keep; cfg.DualSense = keep ? TvAction.Keep : TvAction.On; cfg.WakeRetry = false;
                clock = new CoreClock(); t = new CoreTransport(); s = new TvService(cfg, new TvStoredState(), t, new CoreDisplay(), new CoreStore(), clock);
                try
                {
                    await s.BeforeSuspendAsync(false, false).ConfigureAwait(false); clock.Advance(1); s.Resume();
                    await s.SendAsync(TvRequestKind.Observe).ConfigureAwait(false); clock.Advance(1); s.VerifiedPs();
                    await s.SendAsync(TvRequestKind.NetworkRecovered).ConfigureAwait(false);
            check(t.Wakes == 0 && s.Runtime.Desired == DesiredTvState.Keep,
                "PS desativado/manter nao deixa intencao latente para recuperacao de rede: " + keep);
                }
                finally { s.CloseAsync().GetAwaiter().GetResult(); }
            }
            foreach (bool implicitConfirmation in new[] { false, true })
            {
                cfg = Config(); cfg.ImplicitSuspendConfirmation = implicitConfirmation; t = new CoreTransport(); d = new CoreDisplay();
                d.Value.ActiveCount = 1; s = new TvService(cfg, new TvStoredState(), t, d, new CoreStore(), new CoreClock());
                try
                {
                    var result = await s.BeforeSuspendAsync(true, true).ConfigureAwait(false);
                    check(t.Sends == (implicitConfirmation ? 1 : 0) && (implicitConfirmation ? t.LastHandshake : result.Outcome == TvOutcome.BlockedAmbiguous),
                        "suspender pela barra respeita confirmacao implicita: " + implicitConfirmation);
                    await s.SendAsync(TvRequestKind.CancelSuspend).ConfigureAwait(false);
                    check(s.Runtime.Mode == TvMode.Running && t.Wakes == 0, "suspensao recusada pelo PC libera modo sem religar TV");
                }
                finally { s.CloseAsync().GetAwaiter().GetResult(); }
            }
            cfg = Config(); cfg.ImplicitSuspendConfirmation = true;
            var tvOnly = new TvDisplayState { Topology = TvTopology.ExternalOnly, SamsungActive = true, ActiveCount = 1 };
            check(TvService.CanImplicitlyConfirm(cfg, tvOnly, true) && !TvService.CanImplicitlyConfirm(cfg, tvOnly, false),
                "confirmacao implicita requer resposta pelo controle");
            tvOnly.ActiveCount = 2; check(!TvService.CanImplicitlyConfirm(cfg, tvOnly, true), "mais de uma tela impede confirmacao implicita");
            t = new CoreTransport(); s = new TvService(Config(), new TvStoredState(), t, new CoreDisplay(), new CoreStore(), new CoreClock());
            try
            {
                await s.SendAsync(TvRequestKind.ConfirmOn).ConfigureAwait(false); await s.BeforeSuspendAsync(false, false).ConfigureAwait(false);
                check(t.Sends == 1 && !t.LastHandshake, "suspensao externa exige canal pronto e usa caminho rapido");
            }
            finally { s.CloseAsync().GetAwaiter().GetResult(); }
            clock = new CoreClock(); var gestureClock = clock;
            var gesture = new ResumeGesture(() => gestureClock.UtcNow, () => gestureClock.Timestamp, gestureClock.Elapsed); bool first;
            check(!gesture.Handle("pad", true, false, clock.UtcNow, out first), "PS normal antes de qualquer suspensao nao e consumido");
            gesture.Suspend(); clock.Advance(1);
            check(gesture.Handle("physical", true, false, clock.UtcNow, out first) && first, "PS valido pode chegar antes do aviso de retomada");
            check(gesture.Resume() && !gesture.Resume() && gesture.Snapshot.PsConsumed, "avisos de retomada duplicados preservam um unico gesto");
            check(gesture.Handle("virtual", true, false, clock.UtcNow, out first) && !first, "duplicata virtual do mesmo PS tambem e consumida");
        gesture.Handle("physical", false, false, clock.UtcNow, out first); gesture.Handle("virtual", false, false, clock.UtcNow, out first); clock.Advance(1);
            check(!gesture.Handle("physical", true, false, clock.UtcNow, out first), "segundo toque apos soltar volta a navegacao normal");
            gesture.Suspend(); gesture.Resume(); clock.Advance(21);
            check(!gesture.Handle("pad", true, false, clock.UtcNow, out first), "PS depois de 20s nao e atribuido ao despertar");
            gesture.Suspend(); gesture.Resume();
            check(!gesture.Handle("pad", true, true, clock.UtcNow, out first) && !gesture.Handle("pad", true, false, clock.UtcNow.AddSeconds(-2), out first),
                "gesto segurado e evento atrasado nao contam como wake");
            gesture.CancelSuspend(); check(!gesture.Handle("pad", true, false, clock.UtcNow, out first), "suspensao cancelada nao arma consumo futuro");
            // CCD substituida: conferir duas vezes o alvo e observar o valor depois da escrita.
            var target = new TvDisplayTarget { EdidVendor = "SAM", OutputTechnology = "HDMI", AdapterLow = 1, TargetId = 2, DevicePath = "fixture-samsung" };
        var state = new TvDisplayState { Topology = TvTopology.ExternalOnly, SamsungActive = true, Samsung = target, HdrSupported = true, HdrEnabled = true };
            int writes = 0;
            var display = new TvWindowsDisplay("fixture", fragment => state, delegate(TvDisplayTarget a, bool value) { writes++; state.HdrEnabled
                = value; return
                true; });
            check(display.SetHdr(false) && writes == 1 && state.HdrEnabled == false, "HDR escreve apenas no alvo conferido e confirma leitura posterior");
            state.MatchedByEdid = true; check(!display.SetHdr(true) && writes == 1, "identificacao apenas por fabricante nao autoriza escrita de HDR");
            state.MatchedByEdid = false; int reads = 0;
            display = new TvWindowsDisplay("fixture", delegate(string fragment) { reads++; return reads == 1 ? state : new TvDisplayState { Topology =
                TvTopology.ExternalOnly, SamsungActive = true, Samsung = new TvDisplayTarget { EdidVendor = "SAM", OutputTechnology = "HDMI", AdapterLow =
                3, TargetId = 4, DevicePath = "other" }, HdrEnabled = false, HdrSupported = true }; },
                delegate(TvDisplayTarget a, bool value) { writes++; return true; });
            check(!display.SetHdr(true) && writes == 1, "troca do alvo entre consultas bloqueia HDR");
        }
    }
}
