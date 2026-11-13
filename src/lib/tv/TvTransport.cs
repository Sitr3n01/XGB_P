// Adaptador real preparado para a fase 4. Nenhuma instancia e criada pelo residente na fase 3.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed class TvTransport : ITvTransport
    {
        readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        readonly TvCredentials credentials;
        readonly TvLease lease;
        readonly ITvClock clock;
        readonly Func<string, string, string, CancellationToken, Task<TvNetworkState>> probe;
        readonly Func<string, CancellationToken, Task<List<string>>> discover;
        string verifiedIp, verifiedMac, verifiedDuid, channelIp, channelName;
        DateTimeOffset verifiedAt;
        long verifiedStamp;
        TvChannel channel;
        bool closed;
        public TvTransport(TvCredentials credentials, TvLease lease, ITvClock clock)
            : this(credentials, lease, clock, TvDiscovery.CheckAsync, TvDiscovery.CandidatesAsync) { }
        internal TvTransport(TvCredentials credentials, TvLease lease, ITvClock clock,
            Func<string, string, string, CancellationToken, Task<TvNetworkState>> probe,
            Func<string, CancellationToken, Task<List<string>>> discover)
        { this.credentials = credentials; this.lease = lease; this.clock = clock; this.probe = probe; this.discover = discover; }
        public bool ChannelReady { get { return !closed && channel != null && channel.Ready; } }
        bool OwnsTarget(TvCoreConfig config)
        { return !closed && lease != null && lease.Owned && lease.TargetName == TvLease.Name(config.Mac) && credentials.Matches(config); }
        public bool CanSend(TvCoreConfig config)
        {
            return OwnsTarget(config) && TvProbe.IsPrivateIPv4(verifiedIp) &&
                string.Equals(config.Mac, verifiedMac, StringComparison.OrdinalIgnoreCase) && string.Equals(config.Duid, verifiedDuid,
                    StringComparison.OrdinalIgnoreCase)
                    &&
                clock.UtcNow >= verifiedAt && clock.UtcNow - verifiedAt <= TimeSpan.FromSeconds(120) &&
                clock.Elapsed(verifiedStamp) >= TimeSpan.Zero && clock.Elapsed(verifiedStamp) <= TimeSpan.FromSeconds(120);
        }
        void Invalidate() { verifiedIp = null; verifiedMac = null; verifiedDuid = null; }
        async Task<NetworkObservation> CheckAsync(string ip, TvCoreConfig config, CancellationToken ct)
        {
            if (!TvProbe.IsPrivateIPv4(ip)) return new NetworkObservation(TvNetwork.NotConfigured);
            var state = await probe(ip, config.Mac, config.Duid, ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested();
            if (state.Status == TvNetwork.Reachable)
            {
                verifiedIp = ip; verifiedMac = config.Mac; verifiedDuid = config.Duid;
                verifiedAt = clock.UtcNow; verifiedStamp = clock.Timestamp;
            }
            else Invalidate();
            return new NetworkObservation(state.Status, state.ReportedPower == "on" ? TvState.On : state.ReportedPower == "off" ? TvState.Off : (TvState?)null,
                state.Status == TvNetwork.Reachable ? "Alvo identificado; rede não comprova energia." : "Observação sem identidade válida.");
        }
        public async Task<NetworkObservation> ObserveAsync(TvCoreConfig config, TimeSpan budget, bool rediscover, CancellationToken ct)
        {
            config.Validate();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(budget); bool entered = false;
                try
                {
                    await gate.WaitAsync(timeout.Token).ConfigureAwait(false); entered = true;
                    if (closed) return new NetworkObservation(TvNetwork.QueryFailed);
                    string ip = verifiedIp ?? config.Ip;
                    var current = await CheckAsync(ip, config, timeout.Token).ConfigureAwait(false);
                    if (current.Status == TvNetwork.Reachable || current.Status == TvNetwork.IdentityMismatch || !rediscover) return current;
                    foreach (string candidate in (await discover(config.Mac, timeout.Token).ConfigureAwait(false)).Distinct().Take(32))
                    {
                        timeout.Token.ThrowIfCancellationRequested();
                        if (!TvProbe.IsPrivateIPv4(candidate)) continue;
                        var found = await CheckAsync(candidate, config, timeout.Token).ConfigureAwait(false);
                        if (found.Status == TvNetwork.Reachable) return found;
                    }
                    return current;
                }
                catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); return new NetworkObservation(TvNetwork.QueryFailed, null,
                    "Prazo de identidade expirou."); }
                finally { if (entered) gate.Release(); }
            }
        }
        async Task SelectAsync(TvCoreConfig config)
        {
            if (channel != null && (channelIp != verifiedIp || channelName != config.ClientName))
            { await channel.CloseAsync().ConfigureAwait(false); channel = null; }
            if (channel == null) { channelIp = verifiedIp; channelName = config.ClientName; channel = new TvChannel(channelIp, credentials, channelName); }
        }
        public async Task<SendResult> SendKeyAsync(TvCoreConfig config, string key, TimeSpan budget, bool handshake, CancellationToken ct)
        {
            if (key != "KEY_POWER") throw new ArgumentException("Somente KEY_POWER é permitido.");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(budget); bool entered = false;
                try
                {
                    await gate.WaitAsync(timeout.Token).ConfigureAwait(false); entered = true;
                if (!CanSend(config)) return new SendResult(DeliveryStatus.NotSent, TvOutcome.IdentityMismatch,
                    "Posse ou identidade do alvo não está atualizada.");
                    await SelectAsync(config).ConfigureAwait(false);
                    var proofLeft = TimeSpan.FromSeconds(120) - clock.Elapsed(verifiedStamp);
                    var utcLeft = TimeSpan.FromSeconds(120) - (clock.UtcNow - verifiedAt);
                    if (utcLeft < proofLeft) proofLeft = utcLeft;
                if (proofLeft <= TimeSpan.Zero) return new SendResult(DeliveryStatus.NotSent, TvOutcome.IdentityMismatch,
                    "Prova de identidade expirou antes do canal.");
                    if (proofLeft < budget) timeout.CancelAfter(proofLeft);
                    return await channel.SendAsync(key, budget, handshake, timeout.Token).ConfigureAwait(false);
                }
            catch (OperationCanceledException) { return new SendResult(DeliveryStatus.NotSent, TvOutcome.Cancelled, "Transporte cancelado antes do frame."); }
                finally { if (entered) gate.Release(); }
            }
        }
        public async Task<WakeResult> WakeAsync(TvCoreConfig config, CancellationToken ct)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!OwnsTarget(config)) return new WakeResult(0, false, "Outro residente detém o alvo; nenhum WoL enviado.");
                var target = config.Copy(); if (verifiedIp != null) target.Ip = verifiedIp;
                return await TvWake.BurstAsync(target, clock, ct).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        public async Task<PairResult> PairAsync(TvCoreConfig config, bool replaceCertificate, CancellationToken ct)
        {
            var observed = await ObserveAsync(config, TimeSpan.FromSeconds(10), true, ct).ConfigureAwait(false);
            if (observed.Status != TvNetwork.Reachable) return new PairResult(false, "Alvo não identificado para autorização.");
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!CanSend(config)) return new PairResult(false, "Sem posse ou identidade recente do alvo.");
                await SelectAsync(config).ConfigureAwait(false);
                return await channel.OpenAsync(TimeSpan.FromSeconds(60), true, replaceCertificate, ct).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        public async Task PrepareAsync(TvCoreConfig config, CancellationToken ct)
        {
            if (credentials.Certificate == null || credentials.Token == null || !OwnsTarget(config)) return;
            var observed = await ObserveAsync(config, TimeSpan.FromSeconds(5), false, ct).ConfigureAwait(false);
            if (observed.Status != TvNetwork.Reachable) return;
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!CanSend(config)) return;
                await SelectAsync(config).ConfigureAwait(false);
                await channel.OpenAsync(TimeSpan.FromSeconds(5), false, false, ct).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        public async Task CloseAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try { closed = true; Invalidate(); if (channel != null) await channel.CloseAsync().ConfigureAwait(false); }
            finally { gate.Release(); }
        }
    }
}
