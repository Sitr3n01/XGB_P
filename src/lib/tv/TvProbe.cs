// Rede da TV Samsung (ADR 0110): somente leitura. Uma conexao TCP na porta 8001 e um GET de http://<ip>:8001/api/v2/, o
// mesmo endpoint de identidade do Samsung TV Orchestrator, no IP ja conhecido. Sem WSS, WoL, SSDP ou comando de energia.
// Nenhum sinal de rede distingue a tela ligada do standby (medido em 08/09): o resultado nunca afirma que a TV esta acesa.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace XGBP.Tv
{
    public enum TvNetwork { NotConfigured, Reachable, Unreachable, IdentityMismatch, QueryFailed }

    public sealed class TvIdentity
    {
        public string Mac = "", Duid = "", ModelName = "", Model = "", NetworkType = "", PowerState;
    }

    public sealed class TvNetworkState
    {
        public TvNetwork Status = TvNetwork.NotConfigured;
        public long PortMs = -1, HttpMs = -1;
        public string ModelName, NetworkType, ReportedPower, Detail;
        public string Describe()
        {
            return "rede=" + Status + (PortMs >= 0 ? "; porta=" + PortMs + " ms" : "") + (HttpMs >= 0 ? "; identidade=" + HttpMs + " ms" : "") +
                (ModelName != null ? "; modelo=" + ModelName : "") + (NetworkType != null ? "; tipo=" + NetworkType : "") +
                "; energia informada=" + (ReportedPower ?? "nao") + (Detail != null ? "; " + Detail : "");
        }
    }

    [DataContract] public sealed class ApiRoot { [DataMember(Name = "device")] public ApiDevice Device; }
    [DataContract] public sealed class ApiDevice
    {
        [DataMember(Name = "wifiMac")] public string WifiMac;
        [DataMember(Name = "duid")] public string Duid;
        [DataMember(Name = "modelName")] public string ModelName;
        [DataMember(Name = "model")] public string Model;
        [DataMember(Name = "networkType")] public string NetworkType;
        [DataMember(Name = "PowerState")] public string PowerState;
    }

    public static class TvProbe
    {
        public const int Port = 8001;
        const int MaxBody = 16384;

        public static bool IsPrivateIPv4(string text)
        {
            IPAddress ip;
            if (string.IsNullOrEmpty(text) || !IPAddress.TryParse(text, out ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            var b = ip.GetAddressBytes();
            return b[0] == 10 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 || b[0] == 192 && b[1] == 168;
        }

        public static byte[] ParseMac(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string hex = text.Replace(":", "").Replace("-", "").Trim();
            if (hex.Length != 12) return null;
            var bytes = new byte[6];
            for (int i = 0; i < 6; i++)
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i])) return null;
            return bytes;
        }

        public static TvIdentity ParseIdentity(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            ApiRoot root;
        try { using (var s = new MemoryStream(Encoding.UTF8.GetBytes(json))) root = new DataContractJsonSerializer(typeof(ApiRoot)).ReadObject(s) as ApiRoot; }
            catch (Exception) { return null; }
            if (root == null || root.Device == null) return null;
            var d = root.Device;
            if (string.IsNullOrEmpty(d.ModelName) || ParseMac(d.WifiMac) == null || string.IsNullOrEmpty(d.Duid) || d.Duid.Length > 200) return null;
            string power = (d.PowerState ?? "").Trim().ToLowerInvariant();
            return new TvIdentity { Mac = d.WifiMac, Duid = d.Duid, ModelName = d.ModelName, Model = d.Model ?? "", NetworkType = d.NetworkType ?? "",
                PowerState = power == "on" || power == "off" ? power : null };
        }

        // MAC e DUID iguais aos do alvo; sem DUID configurado, nunca corresponde (mesma regra do Orchestrator).
        public static bool Matches(TvIdentity id, string mac, string duid)
        {
            if (id == null || string.IsNullOrEmpty(duid)) return false;
            byte[] a = ParseMac(id.Mac), b = ParseMac(mac);
            if (a == null || b == null) return false;
            for (int i = 0; i < 6; i++) if (a[i] != b[i]) return false;
            return string.Equals(id.Duid, duid, StringComparison.OrdinalIgnoreCase);
        }

        // Decisao pura, separada da rede para as verificacoes: porta, corpo da identidade e alvo.
        public static TvNetworkState Evaluate(bool portOpen, string body, string mac, string duid)
        {
            var state = new TvNetworkState();
            if (!portOpen) { state.Status = TvNetwork.Unreachable; state.Detail = "porta sem resposta"; return state; }
            var id = ParseIdentity(body);
            if (id == null) { state.Status = TvNetwork.QueryFailed; state.Detail = "identidade ilegivel"; return state; }
            state.ModelName = id.ModelName; state.NetworkType = id.NetworkType;
            if (!Matches(id, mac, duid)) { state.Status = TvNetwork.IdentityMismatch; state.Detail = "MAC/DUID diferentes do alvo"; return state; }
            state.Status = TvNetwork.Reachable; state.ReportedPower = id.PowerState;
            return state;
        }

        public static TvNetworkState Probe(string ip, string mac, string duid, int portTimeoutMs, int httpTimeoutMs)
        {
            if (!IsPrivateIPv4(ip)) return new TvNetworkState { Status = TvNetwork.NotConfigured, Detail = "sem IP privado configurado" };
            var watch = Stopwatch.StartNew();
            bool open = TcpOpen(ip, portTimeoutMs);
            long portMs = watch.ElapsedMilliseconds;
            string body = null; long httpMs = -1;
            if (open)
            {
                watch.Restart();
                try { body = Get("http://" + ip + ":" + Port + "/api/v2/", httpTimeoutMs); }
                catch (Exception) { body = null; }
                httpMs = watch.ElapsedMilliseconds;
            }
            var state = Evaluate(open, body, mac, duid);
            state.PortMs = portMs; state.HttpMs = httpMs;
            return state;
        }

        static bool TcpOpen(string ip, int timeoutMs)
        {
            using (var client = new TcpClient(AddressFamily.InterNetwork))
            {
                try
                {
                    var pending = client.BeginConnect(IPAddress.Parse(ip), Port, null, null);
                    if (!pending.AsyncWaitHandle.WaitOne(Math.Max(1, timeoutMs))) return false;
                    client.EndConnect(pending);
                    return client.Connected;
                }
                catch (SocketException) { return false; }
                catch (ObjectDisposedException) { return false; }
            }
        }

        static string Get(string url, int timeoutMs)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Proxy = null; request.Timeout = timeoutMs; request.ReadWriteTimeout = timeoutMs;
            request.AllowAutoRedirect = false; request.KeepAlive = false; request.Method = "GET";
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaxBody) return null;
                using (var stream = response.GetResponseStream())
                {
                    var buffer = new byte[MaxBody + 1]; int count = 0, n;
                    while (count < buffer.Length && (n = stream.Read(buffer, count, buffer.Length - count)) > 0) count += n;
                    return count > MaxBody ? null : Encoding.UTF8.GetString(buffer, 0, count);
                }
            }
        }
    }
}
