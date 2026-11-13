// Par TLS local e frames em memoria, sem trafego para a TV ou leitura de credenciais reais.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;

namespace XGBP.Design
{
    static partial class TvCoreSuite
    {
        static async Task WireCases(string scratch, Action<bool, string> check)
        {
            string endpoint = TvChannel.Endpoint(Config().Ip, "fake-token", "SamsungTvOrchestrator").OriginalString;
            check(endpoint.Contains("name=" + Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes("SamsungTvOrchestrator")))) &&
                endpoint.EndsWith(
                "&token=fake-token", StringComparison.Ordinal), "nome do cliente compativel e token escapado no endpoint");
            check(Refuses(delegate { TvChannel.Endpoint("8.8.8.8", null, "test"); }), "canal recusa IP externo antes de conectar");
            string nonce = "dGhlIHNhbXBsZSBub25jZQ==";
            string valid = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: keep-alive, Upgrade\r\nSec-Web" +
                "Socket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n";
            TvWebSocket.ValidateHandshake(valid, nonce); check(true, "handshake confere Accept RFC6455 e token Connection");
            check(Refuses(delegate { TvWebSocket.ValidateHandshake(valid.Replace("s3pPL", "wrong"), nonce); }) &&
                Refuses(delegate { TvWebSocket.ValidateHandshake(valid.Replace("101", "200"), nonce); }), "handshake errado ou sem upgrade recusado");
            foreach (byte[] malformed in new[] { new byte[] { 0x81, 0x80 }, new byte[] { 0x82, 0 }, new byte[] { 0x80, 0 },
                new byte[] { 0x09, 0 }, new byte[] { 0x81, 127 }, new byte[] { 0x81, 126, 0, 1 }, new byte[] { 0x88, 1, 0 } })
            {
                var stream = new MemoryStream(malformed); var ws = new TvWebSocket(stream, stream); bool refused = false;
            try { await ws.ReceiveAsync(None).ConfigureAwait(false); } catch (IOException) { refused = true; } catch (InvalidDataException) { refused = true; }
                finally { ws.Dispose(); }
                check(refused, "parser recusa frame malformado " + BitConverter.ToString(malformed));
            }
            var peer = new TvCoreTlsPeer(); TvChannel channel = null;
            try
            {
                var credentials = new TvCredentials(Path.Combine(scratch, "tls-pair.json"), Config()); peer.Intermediate = true;
                channel = new TvChannel(credentials, token => peer.Endpoint);
                var pair = await channel.OpenAsync(TimeSpan.FromSeconds(5), true, false, None).ConfigureAwait(false);
                check(pair.Authorized && channel.Ready && credentials.Certificate == peer.Fingerprint,
                    "TLS pareamento explicito fixa certificado e aceita hello fragmentado: " + pair.Detail + " peer=" + peer.Error);
                var result = await channel.SendAsync("KEY_POWER", TimeSpan.FromSeconds(3), true, None).ConfigureAwait(false);
                await Until(() => peer.Keys == 1 && peer.Pongs >= 1).ConfigureAwait(false);
                check(result.Delivery == DeliveryStatus.Sent, "TLS frame mascarado e ping respondido com pong");
check(!File.ReadAllText(Path.Combine(scratch, "tls-pair.json")).Contains("fake-rotated-token"), "token recebido por TLS persiste apenas cifrado");
                var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => channel.SendAsync("KEY_POWER", TimeSpan.FromSeconds(5), true,
                    None))).ConfigureAwait(false);
                await Until(() => peer.Keys == 21).ConfigureAwait(false);
                check(results.All(r => r.Delivery == DeliveryStatus.Sent), "20 envios TLS paralelos sao serializados sem corromper frames");
                bool invalidKey = false;
                try { await channel.SendAsync("KEY_POWEROFF", TimeSpan.FromSeconds(1), true, None).ConfigureAwait(false); } catch
                    (ArgumentException) { invalidKey = true;
                    }
                check(invalidKey && peer.Keys == 21, "transporte recusa outra tecla de energia");
                await channel.CloseAsync().ConfigureAwait(false); check(!channel.Ready, "fechamento libera sessao e receptor");
                var after = await channel.SendAsync("KEY_POWER", TimeSpan.FromSeconds(1), false, None).ConfigureAwait(false);
                check(after.Delivery == DeliveryStatus.NotSent, "canal encerrado nao envia");
            }
            finally { if (channel != null) channel.CloseAsync().GetAwaiter().GetResult(); peer.CloseAsync().GetAwaiter().GetResult(); }
            foreach (bool unauthorized in new[] { false, true })
            {
                peer = new TvCoreTlsPeer(); channel = null;
                try
                {
                    peer.Unauthorized = unauthorized;
                    var credentials = new TvCredentials(Path.Combine(scratch, unauthorized ? "tls-unauthorized.json" : "tls-wrong-pin.json"), Config());
                    credentials.Save("fake-token", unauthorized ? peer.Fingerprint : new string('0', 64));
                    channel = new TvChannel(credentials, token => peer.Endpoint);
                    var result = await channel.SendAsync("KEY_POWER", TimeSpan.FromSeconds(3), true, None).ConfigureAwait(false);
                    check(result.Delivery == DeliveryStatus.NotSent && peer.Keys == 0 && !channel.Ready,
                        unauthorized ? "TV recusa autorizacao: nenhum frame de energia" : "TLS certificado diferente: nenhum frame de energia");
                }
                finally { if (channel != null) channel.CloseAsync().GetAwaiter().GetResult(); peer.CloseAsync().GetAwaiter().GetResult(); }
            }
            var untouched = new TvCredentials(Path.Combine(scratch, "no-pair.json"), Config());
            channel = new TvChannel(untouched, token => { throw new InvalidOperationException("Nao deveria conectar."); });
            try
            {
                var result = await channel.SendAsync("KEY_POWER", TimeSpan.FromSeconds(1), false, None).ConfigureAwait(false);
                check(result.Delivery == DeliveryStatus.NotSent, "caminho de suspensao sem sessao pronta nao abre conexao");
                check(!(await channel.OpenAsync(TimeSpan.FromSeconds(1), false, false, None).ConfigureAwait(false)).Authorized,
                    "sem token/pin nao inicia pareamento implicito");
            }
            finally { channel.CloseAsync().GetAwaiter().GetResult(); }
        }
    }
}
