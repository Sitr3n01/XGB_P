// Uma sessao Tizen autorizada e um receptor por canal; falha apos iniciar frame e sempre entrega possivel.
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    [DataContract] public sealed class TvHello
    {
        [DataMember(Name = "event")] public string Event;
        [DataMember(Name = "data")] public TvHelloData Data;
    }
    [DataContract] public sealed class TvHelloData { [DataMember(Name = "token")] public string Token; }
    public sealed class TvChannel
    {
        sealed class Session
        {
            public readonly TvWebSocket Socket;
            public readonly CancellationTokenSource Lifetime = new CancellationTokenSource();
            public Task Pump = Task.FromResult(0);
            public Session(TvWebSocket socket) { Socket = socket; }
        }
        readonly SemaphoreSlim operations = new SemaphoreSlim(1, 1);
        readonly TvCredentials credentials;
        readonly Func<string, Uri> endpoint;
        Session session;
        bool disposed;
        public TvChannel(string ip, TvCredentials credentials, string clientName)
            : this(credentials, token => Endpoint(ip, token, clientName)) { }
        internal TvChannel(TvCredentials credentials, Func<string, Uri> endpoint) { this.credentials = credentials; this.endpoint = endpoint; }
        public bool Ready { get { var s = Volatile.Read(ref session); return !disposed && s != null && s.Socket.Alive; } }
        public static Uri Endpoint(string ip, string token, string name)
        {
            if (!TvProbe.IsPrivateIPv4(ip) || string.IsNullOrEmpty(name) || name.Length > 80 || token != null && !TvCredentials.ValidToken(token))
                throw new ArgumentException("Endpoint Tizen inválido.");
            return new Uri("wss://" + ip + ":8002/api/v2/channels/samsung.remote.control?name=" +
                Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(name))) + (token == null ? "" : "&token=" + Uri.EscapeDataString(token)));
        }
        public async Task<PairResult> OpenAsync(TimeSpan budget, bool pair, bool replace, CancellationToken ct)
        {
            using (var limited = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                limited.CancelAfter(budget);
                try
                {
                    await operations.WaitAsync(limited.Token).ConfigureAwait(false);
                    try { return await OpenLockedAsync(pair, replace, limited.Token).ConfigureAwait(false); }
                    finally { operations.Release(); }
                }
                catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); return new PairResult(false, "Prazo de autorização expirou."); }
            }
        }
        async Task<PairResult> OpenLockedAsync(bool pair, bool replace, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (disposed) return new PairResult(false, "Canal encerrado.");
            if (Ready && !replace) return new PairResult(true, "Sessão já autorizada.");
            if (!pair && (credentials.Certificate == null || credentials.Token == null)) return new PairResult(false, "Autorização explícita necessária.");
            CloseSession(); TvWebSocket socket = null;
            try
            {
                socket = await TvWebSocket.ConnectAsync(endpoint(replace ? null : credentials.Token), replace ? null : credentials.Certificate, pair,
                    ct).ConfigureAwait(false);
                for (int i = 0; i < 8; i++)
                {
                    string text = await socket.ReceiveAsync(ct).ConfigureAwait(false);
                    if (text == null) break;
                    var hello = TvJson.Parse<TvHello>(text);
                    if (hello == null || hello.Event == "ms.channel.unauthorized") break;
                    if (hello.Event != "ms.channel.connect") continue;
                    string token = hello.Data != null && TvCredentials.ValidToken(hello.Data.Token) ? hello.Data.Token : replace ? null : credentials.Token;
                    if (!TvCredentials.ValidToken(token)) break;
                    credentials.Save(token, socket.ObservedCertificate); // A sessao so e publicada depois da persistencia.
                    var next = new Session(socket); Volatile.Write(ref session, next); socket = null;
                    next.Pump = PumpAsync(next); return new PairResult(true, "Sessão autorizada e certificado vinculado ao alvo.");
                }
                return new PairResult(false, "TV não autorizou; confirme Permitir na TV.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                // Nunca incluir URI, corpo JSON, token ou mensagem da excecao em log/feedback.
                return new PairResult(false, "Canal/credencial recusado: " + ex.GetType().Name + ".");
            }
            finally { if (socket != null) socket.Dispose(); }
        }
        public async Task<SendResult> SendAsync(string key, TimeSpan budget, bool handshake, CancellationToken ct)
        {
            if (key != "KEY_POWER") throw new ArgumentException("Somente KEY_POWER é permitido.");
            bool entered = false, started = false;
            using (var limited = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                limited.CancelAfter(budget);
                try
                {
                    await operations.WaitAsync(limited.Token).ConfigureAwait(false); entered = true;
                    if (!Ready)
                    {
                        if (!handshake) return new SendResult(DeliveryStatus.NotSent, TvOutcome.NotSent, "Suspensão não inicia handshake.");
                        var opened = await OpenLockedAsync(false, false, limited.Token).ConfigureAwait(false);
                        if (!opened.Authorized) return new SendResult(DeliveryStatus.NotSent, TvOutcome.AuthRequired, opened.Detail);
                    }
                    var s = Volatile.Read(ref session); limited.Token.ThrowIfCancellationRequested();
                    if (s == null || !s.Socket.Alive) return new SendResult(DeliveryStatus.NotSent, TvOutcome.NotSent, "Canal caiu antes do envio.");
                    started = true;
                    await s.Socket.SendTextAsync("{\"method\":\"ms.remote.control\",\"params\":{\"Cmd\":\"Click\",\"DataOfCmd\":\"KEY_POWER\",\"Option" +
                        "\":\"false\",\"TypeOfRemote\":\"SendRemoteKey\"}}", limited.Token).ConfigureAwait(false);
                    return new SendResult(DeliveryStatus.Sent, TvOutcome.Unconfirmed, "Frame enviado; energia não confirmada.");
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException || ex is IOException || ex is ObjectDisposedException)) throw;
                    return new SendResult(started ? DeliveryStatus.PossiblySent : DeliveryStatus.NotSent,
                        started ? TvOutcome.PossiblySent : ct.IsCancellationRequested ? TvOutcome.Cancelled : TvOutcome.NotSent,
                        started ? "Entrega indeterminada; não repetir toggle." : "Nenhum frame iniciado.");
                }
                finally { if (entered) operations.Release(); }
            }
        }
        async Task PumpAsync(Session owned)
        {
            try
            {
                while (!owned.Lifetime.IsCancellationRequested)
                {
                    string text = await owned.Socket.ReceiveAsync(owned.Lifetime.Token).ConfigureAwait(false);
                    if (text == null) break;
                    var hello = TvJson.Parse<TvHello>(text);
                    if (hello != null && hello.Event == "ms.channel.unauthorized") break;
                }
            }
            catch (Exception) { }
            finally { owned.Socket.Dispose(); } // Uma bomba antiga nunca fecha uma sessao substituta.
        }
        void CloseSession()
        {
            var old = Interlocked.Exchange(ref session, null);
            if (old == null) return;
            old.Lifetime.Cancel(); old.Socket.Dispose();
            old.Pump.ContinueWith(delegate { old.Lifetime.Dispose(); }, TaskScheduler.Default);
        }
        public async Task CloseAsync()
        {
            await operations.WaitAsync().ConfigureAwait(false);
            try { disposed = true; CloseSession(); }
            finally { operations.Release(); }
        }
    }
}
