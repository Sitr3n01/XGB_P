// Servidor efemero restrito a loopback. Certificado temporario, sem instalacao em repositorio de certificados.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed class TvCoreTlsPeer
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly List<Task> clients = new List<Task>();
        readonly RSA key = RSA.Create(2048);
        readonly X509Certificate2 certificate;
        readonly Task loop;
        public int Keys, Pongs, Connections;
        public bool Unauthorized, Intermediate;
        public string Error;
        public string Fingerprint { get { using (var sha = SHA256.Create()) return
            BitConverter.ToString(sha.ComputeHash(certificate.RawData)).Replace("-", ""); }
            }
        public Uri Endpoint { get { return new Uri("wss://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/control"); } }
        public TvCoreTlsPeer()
        {
            var request = new CertificateRequest("CN=XGBP-TV-Loopback", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using (var temp = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1)))
                certificate = new X509Certificate2(temp.Export(X509ContentType.Pfx), "", X509KeyStorageFlags.Exportable);
            listener.Start(); loop = Task.Run((Func<Task>)AcceptAsync);
        }
        async Task AcceptAsync()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    var tcp = await listener.AcceptTcpClientAsync().ConfigureAwait(false); Interlocked.Increment(ref Connections);
                    lock (clients) clients.Add(ServeAsync(tcp));
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
        static async Task<byte[]> ExactAsync(Stream stream, int count, CancellationToken ct)
        {
            var bytes = new byte[count]; int offset = 0;
            while (offset < count) { int n = await stream.ReadAsync(bytes, offset, count - offset, ct).ConfigureAwait(false); if (n == 0) throw new IOException(
                "Fim de stream de teste."); offset += n; }
            return bytes;
        }
        static async Task SendFrameAsync(Stream tls, byte opcode, byte[] payload, CancellationToken ct, bool final = true)
        {
            using (var bytes = new MemoryStream())
            {
                bytes.WriteByte((byte)((final ? 128 : 0) | opcode));
                if (payload.Length < 126) bytes.WriteByte((byte)payload.Length);
                else { bytes.WriteByte(126); bytes.WriteByte((byte)(payload.Length >> 8)); bytes.WriteByte((byte)payload.Length); }
                bytes.Write(payload, 0, payload.Length); byte[] data = bytes.ToArray();
                await tls.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
            }
        }
        async Task ServeAsync(TcpClient tcp)
        {
            using (tcp)
            using (var tls = new SslStream(tcp.GetStream()))
            using (lifetime.Token.Register(tcp.Close))
            {
                try
                {
                    await tls.AuthenticateAsServerAsync(certificate, false, SslProtocols.Tls12, false).ConfigureAwait(false);
                    string header = await TvWebSocket.ReadHeaderAsync(tls, lifetime.Token).ConfigureAwait(false);
                    string nonce = header.Split(new[] { "\r\n" }, StringSplitOptions.None).First(s => s.StartsWith("Sec-WebSocket-Key:",
                        StringComparison.OrdinalIgnoreCase)).Substring(18).Trim();
                    string accept;
using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(nonce + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    byte[] response =
                        Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " +
                        accept + "\r\n\r\n");
                    await tls.WriteAsync(response, 0, response.Length, lifetime.Token).ConfigureAwait(false);
                if (Intermediate) await SendFrameAsync(tls, 1, Encoding.UTF8.GetBytes("{\"event\":\"informational\"}"), lifetime.Token).ConfigureAwait(false);
                    await SendFrameAsync(tls, 9, new byte[] { 1, 2, 3 }, lifetime.Token).ConfigureAwait(false);
                    string hello = Unauthorized ? "{\"event\":\"ms.channel.unauthorized\"}" :
                        "{\"event\":\"ms.channel.connect\",\"data\":{\"token\":\"fake-rotated-token\"}}"
                        ;
                    // Hello fragmentado verifica continuacao com ping intercalado.
                    byte[] text = Encoding.UTF8.GetBytes(hello); int middle = text.Length / 2;
                    await SendFrameAsync(tls, 1, text.Take(middle).ToArray(), lifetime.Token, false).ConfigureAwait(false);
                    await SendFrameAsync(tls, 0, text.Skip(middle).ToArray(), lifetime.Token).ConfigureAwait(false);
                    while (!lifetime.IsCancellationRequested)
                    {
                        var h = await ExactAsync(tls, 2, lifetime.Token).ConfigureAwait(false); int length = h[1] & 127;
                        if ((h[1] & 128) == 0 || (h[0] & 128) == 0 || length == 127) throw new IOException("Frame cliente sem mascara ou invalido.");
                        if (length == 126) { var n = await ExactAsync(tls, 2, lifetime.Token).ConfigureAwait(false); length = n[0] * 256 + n[1]; }
                        if (length > 16384) throw new IOException("Frame cliente grande demais.");
                    byte[] mask = await ExactAsync(tls, 4, lifetime.Token).ConfigureAwait(false), body = await ExactAsync(tls, length,
                        lifetime.Token).ConfigureAwait(false);
                        for (int i = 0; i < body.Length; i++) body[i] ^= mask[i % 4];
                        int opcode = h[0] & 15;
                        if (opcode == 10) Interlocked.Increment(ref Pongs);
                        if (opcode == 8) break;
                        if (opcode == 1 && Encoding.UTF8.GetString(body).Contains("KEY_POWER")) Interlocked.Increment(ref Keys);
                    }
                }
                catch (Exception ex) { Error = ex.GetType().Name + ":" + ex.HResult; }
            }
        }
        public async Task CloseAsync()
        {
            lifetime.Cancel(); listener.Stop(); await loop.ConfigureAwait(false);
            Task[] running; lock (clients) running = clients.ToArray();
            await Task.WhenAll(running).ConfigureAwait(false); certificate.Dispose(); key.Dispose(); lifetime.Dispose();
        }
    }
}
