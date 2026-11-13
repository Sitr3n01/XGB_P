// Persistencia em diretorio de teste; credenciais e identificadores inteiramente ficticios.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;

namespace XGBP.Design
{
    static partial class TvCoreSuite
    {
        static bool Refuses(Action action)
        { try { action(); return false; } catch (ArgumentException) { return true; } catch (IOException) { return true; } catch (InvalidDataException) { return
            true; } }
        static async Task StorageCases(string scratch, Action<bool, string> check)
        {
            foreach (string ip in new[] { "8.8.8.8", "127.0.0.1", "::1", "192.168.55.10/x" })
            { var cfg = Config(); cfg.Ip = ip; check(Refuses(cfg.Validate), "configuracao recusa endereco externo/invalido: " + ip); }
            foreach (string mac in new[] { "00:00:00:00:00:00", "FF:FF:FF:FF:FF:FF", "01:00:00:00:00:01", "invalid" })
            { var cfg = Config(); cfg.Mac = mac; check(Refuses(cfg.Validate), "alvo WoL recusa MAC nao individual: " + mac); }
            var config = Config(); config.ConfirmedSeconds = 121; check(Refuses(config.Validate), "configuracao nunca amplia autoridade alem de 120s");
            config = Config(); config.Suspend = TvAction.On; check(Refuses(config.Validate), "politica de suspensao nunca liga");
            config = Config(); config.BackoffSeconds = new[] { 0 }; check(Refuses(config.Validate), "backoff invalido recusado");
            var clone = Config(); var copy = clone.Copy(); clone.BackoffSeconds[0] = 27;
            check(copy.BackoffSeconds[0] == 1, "configuracao copia arrays sem alias mutavel");
            var imported = TvCoreConfig.Import(new TvSetupState { Found = true, Mac = Config().Mac, Duid = Config().Duid, Ip = Config().Ip,
                KeepHdrOff = true, Extend = 0, External = 1, Clone = 1, PcOnly = 0, Suspend = 2, Resume = 0, DualSense = 1 });
            check(!imported.Enabled && imported.KeepHdrOff && imported.Extend == TvAction.Keep && imported.Suspend == TvAction.Off,
                "importar preserva preferencias sem ativar automacao");
            string importJson = "{\"Enabled\":true,\"Tv\":{\"Mac\":\"02:00:00:00:00:01\",\"Duid\":\"uuid:test-target\",\"PowerOffKey" +
                "\":\"KEY_POWER\"},\"Reliability\":{\"WakeRetryEnabled\":false,\"ConfirmedStateMaxAgeSeconds\":30,\"P" +
                "robeBackoffSeconds\":[3],\"WakeBurstCount\":1,\"WakeBurstDelaysMs\":[0]}}";
            imported = TvCoreConfig.ImportJson(importJson);
            check(!imported.Enabled && !imported.WakeRetry && imported.ConfirmedSeconds == 30 && imported.BackoffSeconds[0] == 3 &&
                imported.BurstDelaysMs.Length ==
                1, "importacao integral preserva limites e politicas de confiabilidade");
            check(Refuses(delegate { TvCoreConfig.ImportJson(importJson.Replace("KEY_POWER", "KEY_POWEROFF")); }),
                "perfil com outra tecla nao e convertido silenciosamente em toggle");
            var packet = TvWake.Magic(TvWake.ParseMac(Config().Mac));
            check(packet.Length == 102 && packet.Take(6).All(b => b == 255) && Enumerable.Range(0, 16).All(i => packet.Skip(6 + i *
                6).Take(6).SequenceEqual(TvWake.ParseMac(Config().Mac))), "pacote WoL: seis FF e dezesseis MACs");
            var local = IPAddress.Parse("192.168.55.2"); var target = IPAddress.Parse(Config().Ip); var mask = IPAddress.Parse("255.255.255.0");
            check(TvWake.SameSubnet(local, target, mask) && !TvWake.SameSubnet(IPAddress.Parse("192.168.56.2"), target, mask) &&
                !TvWake.SameSubnet(IPAddress.IPv6Loopback, target, mask), "interface selecionada por sub-rede IPv4");
            int chooseCalls = 0, udpCalls = 0; var clock = new CoreClock();
            var wake = await TvWake.BurstAsync(Config(), clock, None, delegate(IPAddress ip) { chooseCalls++; return null; },
                delegate(TvEgress e, IPEndPoint p, byte[] bytes, CancellationToken ct) { udpCalls++; return Task.FromResult(0); }).ConfigureAwait(false);
            check(!wake.HasRoute && wake.Packets == 0 && udpCalls == 0 && chooseCalls >= 20 && clock.Elapsed(TimeSpan.FromDays(1).Ticks) ==
                TimeSpan.FromSeconds(10),
                "WoL aguarda interface ate 10s e nao envia sem ela");
            var destinations = new HashSet<string>();
            wake = await TvWake.BurstAsync(Config(), new CoreClock(), None, ip => new TvEgress(local, IPAddress.Parse("192.168.55.255")),
                delegate(TvEgress e, IPEndPoint p, byte[] bytes, CancellationToken ct) { udpCalls++; destinations.Add(p.ToString()); return Task.FromResult(0);
                    }).ConfigureAwait(false);
            check(wake.Packets == 18 && destinations.Count == 6 && udpCalls == 18, "WoL simulado: tres rajadas, duas portas e tres destinos");
            var neighbors =
                TvDiscovery.NeighborCandidates("192.168.55.10 02-00-00-00-00-01 dynamic\n192.168.55.11 02-00-00-00-00-02 dynamic\n8.8.8.8 02-00-00-0" +
                "0-00-01 dynamic", Config().Mac);
            check(neighbors.SequenceEqual(new[] { Config().Ip }), "vizinhos filtrados por MAC e endereco privado");
            var store = new TvStore(Path.Combine(scratch, "store"));
            await store.SaveConfigAsync(Config(), None).ConfigureAwait(false);
            check(store.ReadConfig().Mac == Config().Mac && store.ReadConfig().BackoffSeconds.SequenceEqual(Config().BackoffSeconds),
                "configuracao propria persiste e valida");
            var state = new TvStoredState { Power = new PowerSnapshot(TvState.On, EvidenceSource.UserConfirmed, clock.UtcNow, "Fixture.", "permit"),
                SleepId = "sleep", SuspendedAt = clock.UtcNow, ResumeConsumed = false, Desired = DesiredTvState.On };
            await store.SaveStateAsync(state, None).ConfigureAwait(false); var loaded = store.ReadState();
            check(loaded.Power.State == TvState.Unknown && loaded.SleepId == "sleep" && !loaded.ResumeConsumed,
                "reinicio preserva sessao mas nunca restaura autoridade de toggle");
            string concurrent = Path.Combine(scratch, "concurrent.json");
            await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(delegate { TvJson.Write(concurrent, new TvStoredState { Desired =
                (DesiredTvState)(i % 3)
                }, None); }))).ConfigureAwait(false);
            check(TvJson.Read<TvStoredState>(concurrent) != null && TvJson.Read<TvStoredState>(concurrent + ".previous.json") != null &&
                Directory.GetFiles(scratch,
                "*.tmp", SearchOption.AllDirectories).Length == 0, "32 escritores atomicos: atual e backup completos, sem temporarios");
            string bad = Path.Combine(scratch, "bad.json"); File.WriteAllText(bad, "{bad");
            check(Refuses(delegate { TvJson.Read<TvStoredState>(bad); }), "JSON corrompido falha fechado");
            check(TvJson.Read<TvStoredState>(Path.Combine(scratch, "missing.json")) == null, "arquivo ausente distinto de arquivo corrompido");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel(); bool stopped = false;
                try { TvJson.Write(Path.Combine(scratch, "cancelled.json"), state, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
                check(stopped && !File.Exists(Path.Combine(scratch, "cancelled.json")), "cancelamento antes da escrita nao cria estado");
            }
            byte[] clear = Encoding.UTF8.GetBytes("fake-token-unit-test"), encrypted = TvProtection.Transform(clear, true);
            check(!encrypted.SequenceEqual(clear) && TvProtection.Transform(encrypted, false).SequenceEqual(clear),
                "DPAPI do usuario faz ida e volta com segredo ficticio");
            string source = Path.Combine(scratch, "credential-source.json"), output = Path.Combine(scratch, "credential-copy.json"), pin = new string('A', 64);
            var credentials = new TvCredentials(source, Config()); credentials.Save("fake-token-unit-test", pin);
            byte[] before = File.ReadAllBytes(source);
            var next = new TvCredentials(output, Config());
            check(next.Import(source) && next.Token == "fake-token-unit-test" && next.Certificate == pin && File.ReadAllBytes(source).SequenceEqual(before),
                "importacao v2 e DPAPI compativeis sem mudar origem");
            check(!File.ReadAllText(output).Contains("fake-token-unit-test") && !File.Exists(output + ".previous.json"),
                "token ficticio cifrado e sem backup legado em texto");
            check(Refuses(delegate { next.Import(output); }), "importacao nao aceita origem igual ao destino");
            var reloaded = new TvCredentials(output, Config()); byte[] saved = File.ReadAllBytes(output);
            check(reloaded.Load() && reloaded.Token == next.Token && File.ReadAllBytes(output).SequenceEqual(saved),
                "recarga da credencial propria e somente leitura"
                );
            var other = Config(); other.Duid = "uuid:other";
            check(!new TvCredentials(Path.Combine(scratch, "other.json"), other).Import(source), "credencial de outra TV recusada");
            File.WriteAllText(bad, "{\"Token\":\"fake-legacy-token\"}");
            check(!new TvCredentials(Path.Combine(scratch, "legacy.json"), Config()).Import(bad), "token legado sem certificado nao autoriza sessao");
            check(!TvCredentials.ValidPin(new string('Z', 64)) && !TvCredentials.ValidToken("bad&token\r\n"), "pin hexadecimal e token de protocolo validados");
            using (var a = new TvLease(Config().Mac))
            using (var b = new TvLease(Config().Mac))
            {
                check(a.Owned && !b.Owned && a.TargetName == @"Global\SamsungTvOrchestrator." + TvJson.Hash("target:020000000001"),
                    "mutex do alvo coincide com o original e impede segundo dono");
                int probes = 0;
                var transport = new TvTransport(next, a, clock, delegate(string ip, string mac, string duid, CancellationToken ct)
            { probes++; return Task.FromResult(new TvNetworkState { Status = TvNetwork.Reachable }); }, (mac, ct) => Task.FromResult(new List<string>()));
                try
                {
                    await transport.ObserveAsync(Config(), TimeSpan.FromSeconds(1), false, None).ConfigureAwait(false);
                    check(probes == 1 && transport.CanSend(Config()), "transporte so libera envio com identidade e posse recentes");
                    clock.Advance(121); clock.Wall(-120);
                    check(!transport.CanSend(Config()), "prova de identidade expira pelo relogio monotono");
                    await transport.ObserveAsync(Config(), TimeSpan.FromSeconds(1), false, None).ConfigureAwait(false);
                    check(!transport.CanSend(other), "DUID trocado nao reutiliza prova anterior");
                }
                finally { transport.CloseAsync().GetAwaiter().GetResult(); }
            }
            using (var acquired = new TvLease(Config().Mac)) check(acquired.Owned, "posse pode ser adquirida depois de liberar");
            foreach (var topology in new[] { TvTopology.ExternalOnly, TvTopology.Extend, TvTopology.Clone })
                check(TvPolicy.Topology(Config(), new TvDisplayState { Topology = topology, SamsungActive = true }) == DesiredTvState.On,
                    "politica de topologia: " +
                    topology);
            check(TvPolicy.Topology(Config(), new TvDisplayState { Topology = TvTopology.PcScreenOnly }) == DesiredTvState.Keep &&
TvPolicy.Topology(Config(), new TvDisplayState { Topology = TvTopology.Extend, SamsungActive = true, Ambiguous = true }) == DesiredTvState.Keep &&
                TvPolicy.Topology(Config(), new TvDisplayState { Topology = TvTopology.NoActiveDisplay }) == DesiredTvState.Keep,
                    "topologia PC/transitoria/ambigua nao cria energia");
        }
    }
}
