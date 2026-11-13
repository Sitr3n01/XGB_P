// RFC 6455 sobre TLS por conexao: nenhum callback global de certificado (ADR 0110).
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed class TvWebSocket : IDisposable
    {
        readonly Stream stream;
        readonly IDisposable owner;
        readonly SemaphoreSlim writes = new SemaphoreSlim(1, 1);
        int closed;
        public string ObservedCertificate { get; private set; }
        public bool Alive { get { return Volatile.Read(ref closed) == 0; } }
        internal TvWebSocket(Stream stream, IDisposable owner) { this.stream = stream; this.owner = owner; }
        public static async Task<TvWebSocket> ConnectAsync(Uri endpoint, string expectedPin, bool explicitPair, CancellationToken ct)
        {
            if (endpoint.Scheme != "wss" || !string.IsNullOrEmpty(endpoint.UserInfo) ||
                !(TvProbe.IsPrivateIPv4(endpoint.Host) || endpoint.Host == "127.0.0.1")) throw new ArgumentException("Endpoint TLS local inválido.");
            var tcp = new TcpClient(AddressFamily.InterNetwork); TvWebSocket socket = null;
            try
            {
                using (ct.Register(tcp.Close))
                {
                    await tcp.ConnectAsync(endpoint.Host, endpoint.Port).ConfigureAwait(false);
                    string observed = null;
                    var tls = new SslStream(tcp.GetStream(), false, delegate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                    {
                        if (cert == null) return false;
                        byte[] raw = cert.GetRawCertData();
                        using (var sha = SHA256.Create()) observed = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "");
                        return explicitPair && expectedPin == null || TvCredentials.PinMatches(raw, expectedPin);
                    });
                    socket = new TvWebSocket(tls, tcp);
                    await tls.AuthenticateAsClientAsync(endpoint.Host, new X509CertificateCollection(), SslProtocols.Tls12, false).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested(); socket.ObservedCertificate = observed;
                    var nonce = new byte[16]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(nonce);
                    string key = Convert.ToBase64String(nonce);
                    string request = "GET " + endpoint.PathAndQuery + " HTTP/1.1\r\nHost: " + endpoint.Authority +
                        "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: " + key + "\r\nSec-WebSocket-Version: 13\r\n\r\n";
                    byte[] bytes = Encoding.ASCII.GetBytes(request);
                    await tls.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                    string header = await ReadHeaderAsync(tls, ct).ConfigureAwait(false);
                    ValidateHandshake(header, key); return socket;
                }
            }
            catch { if (socket != null) socket.Dispose(); else tcp.Close(); throw; }
        }
        internal static async Task<string> ReadHeaderAsync(Stream input, CancellationToken ct)
        {
            using (var header = new MemoryStream())
            {
                byte[] one = new byte[1]; int suffix = 0;
                while (header.Length < 8192)
                {
                    if (await input.ReadAsync(one, 0, 1, ct).ConfigureAwait(false) == 0) throw new IOException("Handshake truncado.");
                    header.WriteByte(one[0]);
                    byte expected = (byte)(suffix == 0 || suffix == 2 ? '\r' : '\n');
                    suffix = one[0] == expected ? suffix + 1 : one[0] == '\r' ? 1 : 0;
                    if (suffix == 4) return Encoding.ASCII.GetString(header.ToArray());
                }
                throw new InvalidDataException("Cabeçalho WebSocket excede o limite.");
            }
        }
        internal static void ValidateHandshake(string header, string key)
        {
            string accept = null, upgrade = null, connection = null;
            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length < 2 || !lines[0].StartsWith("HTTP/1.1 101 ", StringComparison.Ordinal)) throw new InvalidDataException("Upgrade WebSocket recusado.");
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':'); if (colon < 0) continue;
                string name = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                if (name.Equals("Sec-WebSocket-Accept", StringComparison.OrdinalIgnoreCase))
                { if (accept != null) throw new InvalidDataException("Accept duplicado."); accept = value; }
                if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) upgrade = value;
                if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase)) connection = value;
                if (name.Equals("Sec-WebSocket-Extensions", StringComparison.OrdinalIgnoreCase) || name.Equals("Sec-WebSocket-Protocol",
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Extensão ou subprotocolo não solicitado.");
            }
            string expected;
        using (var sha = SHA1.Create()) expected = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key +
            "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            bool hasUpgrade = false;
            if (connection != null) foreach (string part in connection.Split(',')) if (part.Trim().Equals("Upgrade",
                StringComparison.OrdinalIgnoreCase)) hasUpgrade =
                true;
            if (accept != expected || !string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase) || !hasUpgrade)
                throw new InvalidDataException("Handshake WebSocket inválido.");
        }
        async Task<byte[]> ExactAsync(int count, CancellationToken ct)
        {
            var bytes = new byte[count]; int offset = 0;
            while (offset < count)
            {
                int n = await stream.ReadAsync(bytes, offset, count - offset, ct).ConfigureAwait(false);
                if (n == 0) throw new IOException("Frame WebSocket truncado."); offset += n;
            }
            return bytes;
        }
        public Task SendTextAsync(string text, CancellationToken ct)
        { return SendFrameAsync(1, new UTF8Encoding(false, true).GetBytes(text), ct); }
        async Task SendFrameAsync(byte opcode, byte[] payload, CancellationToken ct)
        {
            if (payload.Length > 16384) throw new InvalidDataException("Frame excede o limite.");
            await writes.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!Alive) throw new IOException("Canal fechado.");
                ct.ThrowIfCancellationRequested();
                byte[] mask = new byte[4]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(mask);
                using (var frame = new MemoryStream())
                {
                    frame.WriteByte((byte)(0x80 | opcode));
                    if (payload.Length < 126) frame.WriteByte((byte)(0x80 | payload.Length));
                    else { frame.WriteByte(0xFE); frame.WriteByte((byte)(payload.Length >> 8)); frame.WriteByte((byte)payload.Length); }
                    frame.Write(mask, 0, mask.Length);
                    for (int i = 0; i < payload.Length; i++) frame.WriteByte((byte)(payload[i] ^ mask[i % 4]));
                    var bytes = frame.ToArray();
                    using (ct.Register(Dispose)) await stream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                }
            }
            finally { writes.Release(); }
        }
        public async Task<string> ReceiveAsync(CancellationToken ct)
        {
            using (ct.Register(Dispose))
            using (var message = new MemoryStream())
            {
                bool fragmented = false;
                while (Alive)
                {
                    var header = await ExactAsync(2, ct).ConfigureAwait(false);
                    bool final = (header[0] & 128) != 0; int opcode = header[0] & 15, length = header[1] & 127;
                    if ((header[0] & 0x70) != 0 || (header[1] & 128) != 0) throw new InvalidDataException("Flags de frame do servidor inválidas.");
                    if (length == 127) throw new InvalidDataException("Frame excede o limite.");
                    if (length == 126)
                    { var more = await ExactAsync(2, ct).ConfigureAwait(false); length = more[0] * 256 + more[1]; if (length < 126) throw new
                        InvalidDataException(
                        "Tamanho não canônico."); }
                    if (length > 16384 || opcode >= 8 && (!final || length > 125)) throw new InvalidDataException("Frame inválido.");
                    byte[] payload = await ExactAsync(length, ct).ConfigureAwait(false);
                    if (opcode == 8)
                    {
                        if (length == 1) throw new InvalidDataException("Close inválido.");
                        if (length >= 2)
                        {
                            int status = payload[0] * 256 + payload[1];
                            if (!(status >= 1000 && status <= 1014 && status != 1004 && status != 1005 && status != 1006 || status >= 3000 && status <= 4999))
                                throw new InvalidDataException("Código close inválido.");
                            new UTF8Encoding(false, true).GetString(payload, 2, payload.Length - 2);
                        }
                        await SendFrameAsync(8, payload, ct).ConfigureAwait(false); Dispose(); return null;
                    }
                    if (opcode == 9) { await SendFrameAsync(10, payload, ct).ConfigureAwait(false); continue; }
                    if (opcode == 10) continue;
                    if (opcode != 1 && opcode != 0 || opcode == 0 && !fragmented || opcode == 1 && fragmented)
                        throw new InvalidDataException("Esperado texto ou continuação WebSocket.");
                    if (message.Length + length > 16384) throw new InvalidDataException("Mensagem Tizen excede o limite.");
                    message.Write(payload, 0, payload.Length); fragmented = !final;
                    if (final) return new UTF8Encoding(false, true).GetString(message.ToArray());
                }
                return null;
            }
        }
        public void Dispose()
        { if (Interlocked.Exchange(ref closed, 1) == 0) { try { stream.Dispose(); } finally { owner.Dispose(); } } }
    }
}
