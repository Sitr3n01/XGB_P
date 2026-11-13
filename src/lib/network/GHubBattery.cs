// Bateria de fones Logitech pelo agente local do G HUB (ws://127.0.0.1:9010), o mesmo canal que a janela do G HUB usa (ADR 0106).
// Somente leitura: GET da lista de dispositivos e do estado da bateria. Nenhuma assinatura, nenhum SET e nada enviado ao fone.
// A API nao e documentada: G HUB fechado, resposta lenta ou formato diferente resultam em lista vazia com nota, nunca num nivel inventado.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Network
{
    [DataContract] public sealed class GHubMessage
    {
        [DataMember(Name = "verb")] public string Verb;
        [DataMember(Name = "path")] public string Path;
        [DataMember(Name = "result")] public GHubResult Result;
        [DataMember(Name = "payload")] public GHubPayload Payload;
    }
    [DataContract] public sealed class GHubResult { [DataMember(Name = "code")] public string Code; }
    // Uniao dos campos usados das duas respostas (lista e bateria); os demais campos do G HUB sao ignorados.
    [DataContract] public sealed class GHubPayload
    {
        [DataMember(Name = "deviceInfos")] public List<GHubDevice> Devices;
        [DataMember(Name = "deviceId")] public string DeviceId;
        [DataMember(Name = "percentage")] public double? Percentage;
        [DataMember(Name = "charging")] public bool? Charging;
        [DataMember(Name = "mileage")] public double? Mileage;   // horas restantes estimadas pelo G HUB (60% de 40 h = 24)
        [DataMember(Name = "batteryStateOfChargeSupport")] public string ChargeSupport;
        [DataMember(Name = "batteryMileageSupport")] public string MileageSupport;
        [DataMember(Name = "isBatteryRemoved")] public bool? Removed;
    }
    [DataContract] public sealed class GHubDevice
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "state")] public string State;
        [DataMember(Name = "deviceType")] public string DeviceType;
        [DataMember(Name = "extendedDisplayName")] public string ExtendedName;
        [DataMember(Name = "displayName")] public string DisplayName;
        [DataMember(Name = "displayConnectionType")] public string Link;
        [DataMember(Name = "capabilities")] public GHubCapabilities Capabilities;
        [DataMember(Name = "activeInterfaces")] public List<GHubInterface> Interfaces;
    }
    [DataContract] public sealed class GHubCapabilities { [DataMember(Name = "hasBatteryStatus")] public bool? HasBattery; }
    [DataContract] public sealed class GHubInterface { [DataMember(Name = "containerId")] public string Container; }

    public sealed class GHubReading
    {
        public List<HeadsetBattery> Headsets = new List<HeadsetBattery>();
        public int Devices = -1;   // dispositivos que o G HUB listou; -1 sem lista
        public string Note;        // motivo de uma leitura vazia ou parcial; null quando tudo respondeu
    }

    // Canal de mensagens de texto; a verificacao usa um substituto, sem rede.
    public interface IGHubChannel : IDisposable
    {
        void Send(string json);
        string Receive(int timeoutMs);   // null quando nada chegou no prazo
    }
    public sealed class GHubUnavailableException : Exception
    {
        public GHubUnavailableException() : base("G HUB fechado ou sem o servico local") { }
        public GHubUnavailableException(string reason) : base(reason) { }
    }

    public static class GHubBattery
    {
        public const int BudgetMs = 2500, ConnectMs = 1000;

        public static GHubReading Read() { return Read(WebSocketChannel.Open, BudgetMs); }

        public static GHubReading Read(Func<int, IGHubChannel> open, int budgetMs)
        {
            var reading = new GHubReading();
            var clock = Stopwatch.StartNew();
            IGHubChannel channel;
            try { channel = open(Math.Min(ConnectMs, budgetMs)); }
            catch (GHubUnavailableException ex) { reading.Note = ex.Message; return reading; }
            using (channel)
            {
                channel.Send(Request("/devices/list"));
                GHubMessage list = null;
                while (list == null)
                {
                    int left = Left(budgetMs, clock);
                    string text = left > 0 ? channel.Receive(left) : null;
                    if (text == null) { reading.Note = "sem resposta a lista de dispositivos"; return reading; }
                    var m = Parse(text);
                    if (m != null && m.Verb == "GET" && m.Path == "/devices/list") list = m;
                }
                if (!Success(list) || list.Payload == null || list.Payload.Devices == null) { reading.Note =
                    "lista de dispositivos em formato inesperado"; return
                    reading; }
                reading.Devices = list.Payload.Devices.Count;
                var wanted = new Dictionary<string, GHubDevice>();
                foreach (var d in list.Payload.Devices) if (IsHeadsetWithBattery(d) && !wanted.ContainsKey(d.Id)) wanted[d.Id] = d;
                foreach (var id in wanted.Keys) channel.Send(Request(BatteryPath(id)));
                var answered = new HashSet<string>();
                while (answered.Count < wanted.Count)
                {
                    int left = Left(budgetMs, clock); if (left <= 0) break;
                    string text = channel.Receive(left); if (text == null) break;
                    var m = Parse(text); if (m == null || m.Verb != "GET") continue;
                    foreach (var pair in wanted)
                    {
                        if (m.Path != BatteryPath(pair.Key) || !answered.Add(pair.Key)) continue;
                        var headset = FromBattery(pair.Value, m);
                        if (headset != null) reading.Headsets.Add(headset);
                    }
                }
                if (answered.Count < wanted.Count) reading.Note = (wanted.Count - answered.Count) + " fone(s) sem resposta da bateria";
            }
            return reading;
        }

        static int Left(int budgetMs, Stopwatch clock) { return Math.Max(0, budgetMs - (int)clock.ElapsedMilliseconds); }
        // O id vai dentro do caminho do pedido: so letras e digitos, no formato que o G HUB usa (dev00000001).
        public static bool IsDeviceId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 40 || !id.StartsWith("dev", StringComparison.Ordinal)) return false;
            foreach (char c in id) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')) return false;
            return true;
        }
        public static string Request(string path) { return "{\"msgId\":\"\",\"verb\":\"GET\",\"path\":\"" + path + "\"}"; }
        static string BatteryPath(string id) { return "/battery/" + id + "/state"; }
        static bool Success(GHubMessage m) { return m.Result != null && m.Result.Code == "SUCCESS"; }

        // So fones ativos com bateria entram; fone desligado ou ausente nao e consultado.
        public static bool IsHeadsetWithBattery(GHubDevice d)
        {
            return d != null && IsDeviceId(d.Id) && d.Capabilities != null && d.Capabilities.HasBattery == true &&
                d.DeviceType != null && d.DeviceType.IndexOf("HEAD", StringComparison.OrdinalIgnoreCase) >= 0 &&
                string.Equals(d.State, "ACTIVE", StringComparison.OrdinalIgnoreCase);
        }

        // Nivel valido so com sucesso, o mesmo id, 0-100, carga informada pelo fone e bateria presente.
        public static HeadsetBattery FromBattery(GHubDevice d, GHubMessage m)
        {
            var p = m == null ? null : m.Payload;
            if (p == null || !Success(m) || p.DeviceId != d.Id || !p.Percentage.HasValue || p.Removed == true) return null;
            if (p.ChargeSupport != null && p.ChargeSupport != "STATE_OF_CHARGE_SUPPORTED") return null;
            double level = p.Percentage.Value;
            if (double.IsNaN(level) || level < 0 || level > 100) return null;
            var headset = new HeadsetBattery { Name = NameOf(d), Percent = (int)Math.Round(level), Connected = true, Charging = p.Charging == true,
                Link = string.IsNullOrWhiteSpace(d.Link) ? "LIGHTSPEED" : d.Link.Trim(), FromGHub = true, Container = ContainerOf(d) };
            if (p.MileageSupport == "MILEAGE_SUPPORTED" && p.Mileage.HasValue && p.Mileage.Value >= 0 && p.Mileage.Value <= 1000)
                headset.HoursLeft = (int)Math.Round(p.Mileage.Value);
            return headset;
        }
        static string NameOf(GHubDevice d)
        {
        string name = !string.IsNullOrWhiteSpace(d.ExtendedName) ? d.ExtendedName : !string.IsNullOrWhiteSpace(d.DisplayName) ? d.DisplayName : "Fone Logitech";
            name = name.Trim();
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }
        static Guid ContainerOf(GHubDevice d)
        {
            if (d.Interfaces != null) foreach (var i in d.Interfaces) { Guid g; if (i != null && Guid.TryParse(i.Container, out g)) return g; }
            return Guid.Empty;
        }

        public static GHubMessage Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { using (var s = new MemoryStream(Encoding.UTF8.GetBytes(text))) return new DataContractJsonSerializer(typeof(GHubMessage)).ReadObject(s) as
                GHubMessage; }
            catch (Exception) { return null; }   // mensagem fora do formato e descartada, sem derrubar a leitura
        }
    }

    // Conexao real com o agente do G HUB. Origem e subprotocolo iguais aos da interface do G HUB; sem proxy.
    // O agente escuta so em 127.0.0.1: "localhost" tenta o ::1 antes e gasta cerca de 2 s na recusa (medido em 07/11).
    sealed class WebSocketChannel : IGHubChannel
    {
        static readonly Uri Address = new Uri("ws://127.0.0.1:9010");
        const int MaxMessage = 4 * 1024 * 1024;
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly byte[] buffer = new byte[64 * 1024];
        readonly MemoryStream partial = new MemoryStream();
        Task<WebSocketReceiveResult> pending;

        public static IGHubChannel Open(int timeoutMs)
        {
            var channel = new WebSocketChannel();
            try
            {
                channel.socket.Options.AddSubProtocol("json");
                channel.socket.Options.SetRequestHeader("Origin", "file://");
                channel.socket.Options.Proxy = null;
                using (var cancel = new CancellationTokenSource(timeoutMs))
                {
                    var connect = channel.socket.ConnectAsync(Address, cancel.Token);
                    bool done = Finished(connect, timeoutMs + 250);
                    if (done && channel.socket.State == WebSocketState.Open) return channel;
                    channel.Dispose();
            throw done || connect.IsFaulted && !connect.IsCanceled ? new GHubUnavailableException() : new
                GHubUnavailableException("o G HUB nao respondeu a tempo");
                }
            }
            catch (GHubUnavailableException) { throw; }
            catch (Exception) { channel.Dispose(); throw new GHubUnavailableException(); }
        }
        static bool Finished(Task task, int timeoutMs)
        {
            try { return task.Wait(Math.Max(0, timeoutMs)); }
            catch (AggregateException) { return false; }
        }

        public void Send(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            if (!Finished(socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None), 1000))
                throw new IOException("o G HUB nao aceitou o pedido");
        }

        public string Receive(int timeoutMs)
        {
            int deadline = Environment.TickCount + Math.Max(0, timeoutMs);
            while (true)
            {
                if (pending == null) pending = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                bool done;
                try { done = pending.Wait(Math.Max(0, deadline - Environment.TickCount)); }
                catch (AggregateException ex) { pending = null; throw new IOException("a conexao com o G HUB caiu", ex.InnerException); }
                if (!done) return null;   // o pedido pendente continua para a proxima chamada
                var result = pending.Result; pending = null;
                if (result.MessageType == WebSocketMessageType.Close) throw new IOException("o G HUB encerrou a conexao");
                partial.Write(buffer, 0, result.Count);
                if (partial.Length > MaxMessage) throw new IOException("resposta do G HUB grande demais");
                if (!result.EndOfMessage) continue;
                string text = Encoding.UTF8.GetString(partial.GetBuffer(), 0, (int)partial.Length);
                partial.SetLength(0);
                return text;
            }
        }

        public void Dispose()
        {
        try { if (socket.State == WebSocketState.Open) Finished(socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None), 250); }
            catch (Exception) { }
            socket.Dispose();   // aborta o que ainda estiver pendente
            // O recebimento abandonado termina com erro depois do Dispose: observado aqui, sem excecao solta.
            var left = pending; pending = null;
            if (left != null) left.ContinueWith(delegate(Task<WebSocketReceiveResult> t) { GC.KeepAlive(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
            partial.Dispose();
        }
    }
}
