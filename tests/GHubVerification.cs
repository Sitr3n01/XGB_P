// Verificacao do fone pelo G HUB (ADR 0106): canal substituto, sem rede e sem o G HUB real.
// Cobre os pedidos (so GET), a selecao dos fones, a validacao da bateria, falhas e prazos, a faixa de status e as Conexoes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using XGBP.Network;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class FakeGHub : IGHubChannel
        {
            public readonly List<string> Sent = new List<string>();
            public readonly List<string> Inbox = new List<string>();
            public readonly Dictionary<string, string> Replies = new Dictionary<string, string>();
            public bool Lifo, Disposed;
            public void Send(string json)
            {
                Sent.Add(json);
                int at = json.IndexOf("\"path\":\"", StringComparison.Ordinal) + 8;
                string path = json.Substring(at, json.IndexOf('"', at) - at), reply;
                if (Replies.TryGetValue(path, out reply)) Inbox.Add(reply);
            }
            public string Receive(int timeoutMs)
            {
                if (Inbox.Count == 0) return null;
                int i = Lifo ? Inbox.Count - 1 : 0; string text = Inbox[i]; Inbox.RemoveAt(i); return text;
            }
            public void Dispose() { Disposed = true; }
        }

        const string GHubOptions = "{\"msgId\":\"\",\"verb\":\"OPTIONS\",\"path\":\"/\",\"origin\":\"backend\"}";
        static string GHubList(params string[] devices)
        {
            return "{\"msgId\":\"a\",\"verb\":\"GET\",\"path\":\"/devices/list\",\"origin\":\"backend\",\"result\":{\"co" +
                "de\":\"SUCCESS\",\"what\":\"\"},\"payload\":{" +
                "\"@type\":\"type.googleapis.com/logi.protocol.devices.Device.Info.List\",\"deviceInfos\":[" + string.Join(",", devices) + "]}}";
        }
        // Campos e aninhamento como na resposta real de 07/11 (inclui escapes & e objetos ignorados), sem numeros de serie.
        static string GHubDeviceJson(string id, string type, bool battery, string state = "ACTIVE", string name = "G522 Gaming Headset")
        {
            return "{\"id\":\"" + id + "\",\"pid\":2840,\"state\":\"" + state + "\",\"connectionType\":\"USB\",\"displayConnectionType\":\"LIGHTSPEED\"," +
"\"deviceType\":\"" + type + "\",\"displayName\":\"G522\",\"extendedDisplayName\":\"" + name + "\",\"capabilities\":{\"audio\":{\"systemTray\":" +
                "{\"hasSidetoneControl\":true},\"unifiedEqualizer\":{\"instanceNames\":{\"0\":\"HEADPHONES_LABEL\"}}}" +",\"hasBatteryStatus\":"
                    + (battery ? "true" :
                    "false") + "}," +
                "\"activeInterfaces\":[{\"type\":\"DEVIO\",\"path\":\"\\\\\\\\?\\\\hid#vid_046d\\u0026pid_0b18\",\"co" +
                    "ntainerId\":\"6621230e-3572-585e-829d-583e825d0748\"}]}";
        }
        static string GHubBatteryReply(string id, string fields, string code = "SUCCESS")
        {
            return "{\"msgId\":\"b\",\"verb\":\"GET\",\"path\":\"/battery/" + id + "/state\",\"origin\":\"backend\",\"result\":{\"code\":\"" + code +
                "\",\"what\":\"\"}" +
                (fields == null ? "" : ",\"payload\":{\"@type\":\"type.googleapis.com/logi.protocol.wireless.Battery\",\"deviceId\":\"" + id +
                    "\"," + fields + "}") + "}"
                    ;
        }
        const string GHubLevel = "\"percentage\":60,\"mileage\":24,\"maxLifeSpan\":40,\"charging\":false,\"batteryStateOfChargeSupport" +
            "\":\"STATE_OF_CHARGE_SUPPORTED\"," +
            "\"criticalLevel\":false,\"isBatteryRemoved\":false,\"batteryMileageSupport\":\"MILEAGE_SUPPORTED\"";

        void VerifyGHubHeadset()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) { if (!ok) throw new Exception("Fone pelo G HUB: " + why); checks.Add("OK: " + why); };
            FakeGHub fake = null;
            Func<int, IGHubChannel> open = delegate { return fake; };
            Func<FakeGHub> standard = delegate {
                var f = new FakeGHub(); f.Inbox.Add(GHubOptions);
                f.Replies["/devices/list"] = GHubList(GHubDeviceJson("dev00000000", "SPEAKER", false, name: "G560 Gaming Speaker"),
                    GHubDeviceJson("dev00000001", "HEADSET", true), GHubDeviceJson("dev00000002", "KEYBOARD", false, name: "G512 Carbon"),
                        GHubDeviceJson("dev00000003",
                        "HID_DEVICE", false, name: "G435"));
                f.Replies["/battery/dev00000001/state"] = GHubBatteryReply("dev00000001", GHubLevel);
                return f;
            };
            var saved = headsets;
            try
            {
                check(GHubBattery.IsDeviceId("dev00000001") && !GHubBattery.IsDeviceId("dev0\"},{\"verb") && !GHubBattery.IsDeviceId("") &&
                    !GHubBattery.IsDeviceId(null) && !GHubBattery.IsDeviceId("abc1"), "id do dispositivo so com letras e digitos antes de entrar no pedido");
                check(GHubBattery.Request("/devices/list") == "{\"msgId\":\"\",\"verb\":\"GET\",\"path\":\"/devices/list\"}", "pedido em JSON com o verbo GET");

                fake = standard(); var r = GHubBattery.Read(open, 2500);
                check(r.Note == null && r.Devices == 4 && r.Headsets.Count == 1, "lista real: so o fone com bateria entre caixa de som, teclado e G435");
                var h = r.Headsets[0];
                check(h.Name == "G522 Gaming Headset" && h.Percent == 60 && h.HoursLeft == 24 && !h.Charging && h.Connected && h.FromGHub &&
                    h.Link == "LIGHTSPEED" &&
                    h.Container == new Guid("6621230e-3572-585e-829d-583e825d0748"), "nivel, tempo restante, conexao e container do fone");
                check(fake.Sent.Count == 2 && fake.Sent.TrueForAll(s => s.Contains("\"verb\":\"GET\"")) && fake.Sent[1].Contains("/battery/dev00000001/state"),
                    "so dois pedidos GET: a lista e a bateria do fone; nenhuma assinatura ou SET");
                check(fake.Disposed, "conexao fechada ao fim da leitura");

                fake = standard(); fake.Replies["/battery/dev00000001/state"] = GHubBatteryReply("dev00000001", GHubLevel.Replace("\"charging\":false",
                    "\"charging\":true"));
                r = GHubBattery.Read(open, 2500);
                check(r.Headsets.Count == 1 && r.Headsets[0].Charging && HeadsetLabel(r.Headsets[0]) == "G522 Gaming Headset · 60% · carregando",
                    "carregando informado pelo G HUB");
                fake = standard(); fake.Replies["/battery/dev00000001/state"] = GHubBatteryReply("dev00000001", GHubLevel.Replace("\"MILEAGE_SUPPORTED\"",
                    "\"MILEAGE_NOT_SUPPORTED\""));
                r = GHubBattery.Read(open, 2500);
                check(r.Headsets.Count == 1 && r.Headsets[0].HoursLeft == -1 && HeadsetLabel(r.Headsets[0]) == "G522 Gaming Headset · 60%",
                    "sem estimativa suportada nao mostra horas");

            var device = new GHubDevice { Id = "dev00000001", State = "ACTIVE", DeviceType = "HEADSET", Capabilities = new GHubCapabilities {
                HasBattery = true } };
                Func<string, string, bool> rejected = delegate(string fields, string code) {
                    return GHubBattery.FromBattery(device, GHubBattery.Parse(GHubBatteryReply("dev00000001", fields, code))) == null; };
                check(rejected(GHubLevel.Replace("\"percentage\":60", "\"percentage\":120"), "SUCCESS") &&
                    rejected(GHubLevel.Replace("\"percentage\":60,", ""), "SUCCESS"
                    ) &&
                    rejected(GHubLevel.Replace("\"percentage\":60", "\"percentage\":-1"), "SUCCESS"), "nivel ausente ou fora de 0-100 descartado");
                check(rejected(GHubLevel.Replace("\"STATE_OF_CHARGE_SUPPORTED\"", "\"STATE_OF_CHARGE_NOT_SUPPORTED\""), "SUCCESS") &&
            rejected(GHubLevel.Replace("\"isBatteryRemoved\":false", "\"isBatteryRemoved\":true"), "SUCCESS"),
                "carga sem suporte ou bateria removida descartadas");
                check(rejected(null, "NO_SUCH_PATH") && rejected(GHubLevel, "ERROR") &&
GHubBattery.FromBattery(device, GHubBattery.Parse(GHubBatteryReply("dev00000009", GHubLevel))) == null, "erro do G HUB ou outro dispositivo descartados");
                check(GHubBattery.Parse("{nao e json") == null && GHubBattery.Parse("") == null && GHubBattery.Parse(null) == null,
                    "mensagem fora do formato e ignorada"
                    );
                check(!GHubBattery.IsHeadsetWithBattery(new GHubDevice { Id = "dev1", State = "NOT_CONNECTED", DeviceType = "HEADSET",
                    Capabilities = new GHubCapabilities { HasBattery = true } }) && !GHubBattery.IsHeadsetWithBattery(null), "fone inativo nao e consultado");

                fake = standard(); fake.Replies["/devices/list"] = GHubList(GHubDeviceJson("dev00000001", "HEADSET", true, "NOT_CONNECTED"));
                r = GHubBattery.Read(open, 2500); check(r.Headsets.Count == 0 && fake.Sent.Count == 1 && r.Note == null,
                    "fone desligado na lista: nenhum pedido de bateria");
                fake = standard(); fake.Inbox.Insert(0, "{lixo"); fake.Replies["/devices/list"] = GHubList(GHubDeviceJson("dev00000001",
                    "HEADSET", true), GHubDeviceJson(
                    "dev00000001", "HEADSET", true));
            r = GHubBattery.Read(open, 2500); check(r.Headsets.Count == 1 && fake.Sent.Count == 2,
                "mensagem invalida ignorada e id repetido consultado uma vez");
                fake = standard(); fake.Lifo = true;
            fake.Replies["/devices/list"] = GHubList(GHubDeviceJson("dev00000001", "HEADSET", true), GHubDeviceJson("dev00000004", "HEADSET",
                true, name: "PRO X 2"));
                fake.Replies["/battery/dev00000004/state"] = GHubBatteryReply("dev00000004", GHubLevel.Replace("\"percentage\":60", "\"percentage\":35"));
                r = GHubBattery.Read(open, 2500);
                check(r.Headsets.Count == 2 && r.Headsets.Exists(x => x.Name == "PRO X 2" && x.Percent == 35) && r.Headsets.Exists(x => x.Name ==
                    "G522 Gaming Headset" &&
                    x.Percent == 60),
                    "respostas fora de ordem ligadas pelo caminho");

                Func<int, IGHubChannel> closed = delegate { throw new GHubUnavailableException(); };
                r = GHubBattery.Read(closed, 2500);
                check(r.Headsets.Count == 0 && r.Devices == -1 && r.Note == "G HUB fechado ou sem o servico local", "G HUB fechado: nenhum fone e uma nota");
                fake = standard(); fake.Replies.Remove("/devices/list"); r = GHubBattery.Read(open, 2500);
            check(r.Headsets.Count == 0 && r.Note == "sem resposta a lista de dispositivos" && fake.Disposed,
                "sem resposta a lista: vazio, nota e conexao fechada");
                fake = standard(); fake.Replies.Remove("/battery/dev00000001/state"); r = GHubBattery.Read(open, 2500);
                check(r.Headsets.Count == 0 && r.Note == "1 fone(s) sem resposta da bateria", "sem resposta da bateria: vazio e nota");
                fake = standard(); r = GHubBattery.Read(open, 0);
                check(r.Headsets.Count == 0 && r.Note != null, "prazo esgotado nao espera nem inventa nivel");
                fake = standard(); fake.Replies["/devices/list"] = GHubList().Replace("\"SUCCESS\"", "\"ERROR\""); r = GHubBattery.Read(open, 2500);
                check(r.Headsets.Count == 0 && r.Note == "lista de dispositivos em formato inesperado", "lista com erro do G HUB vira nota");

                var order = new List<HeadsetBattery> { new HeadsetBattery { Name = "A", Connected = true }, new HeadsetBattery { Name = "Z",
                    Connected = true, FromGHub =
                    true, Link = "LIGHTSPEED" },
                    new HeadsetBattery { Name = "B", Connected = false, FromGHub = true } };
                OrderHeadsets(order); check(order[0].Name == "Z" && order[1].Name == "A" && order[2].Name == "B",
                    "conectado do G HUB antes do Bluetooth; desconectados por ultimo");

                SetPanel(0); Show(); window.UpdateLayout();
                var g522 = new HeadsetBattery { Name = "G522 Gaming Headset", Percent = 60, HoursLeft = 24, Connected = true, FromGHub = true,
                    Link = "LIGHTSPEED",
                    Container = Guid.NewGuid() };
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "G435", Percent = 80, Connected = true }, g522 });
                check((string)headsetSlot.ToolTip == "G522 Gaming Headset · 60% · cerca de 24 h" && headsetBolt == null && headsetFill != null &&
Math.Abs(headsetFill.Width - 16.6 * .6) < .01 && headsetFill.Fill == Brush("TextPrimary"), "faixa mostra o fone do G HUB com o tempo restante");
            check(AutomationProperties.GetName(headsetSlot) == "Fone LIGHTSPEED: G522 Gaming Headset · 60% · cerca de 24 h",
                "nome acessivel com a conexao do fone");
                window.UpdateLayout(); SaveRender("faixa-status-fone-ghub");
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "G522 Gaming Headset", Percent = 45, Charging = true,
                    HoursLeft = 18, Connected = true,
                    FromGHub = true, Link = "LIGHTSPEED" } });
                check(headsetBolt != null && headsetFill.Fill == Brush("AccentText") && (string)headsetSlot.ToolTip == "G522 Gaming Headset · 45% · carregando",
                    "carregando mostra o raio e o nivel em destaque");
                window.UpdateLayout(); SaveRender("faixa-status-fone-ghub-carregando");

                SetHeadsets(new List<HeadsetBattery> { g522, new HeadsetBattery { Name = "G435", Percent = 80, Connected = true } });
                SetPanel(5); window.UpdateLayout();
                Func<Border> row = delegate {
            foreach (UIElement child in body.Children) { var b = child as Border; if (b != null && (string)b.Tag == "fone-ghub") return b; } return null; };
                var line = row();
                check(line != null && AutomationProperties.GetName(line) == "G522 Gaming Headset · LIGHTSPEED · 60% · cerca de 24 h" && !line.Focusable,
                    "Conexoes mostram o fone do G HUB numa linha informativa");
                int bt = body.Children.IndexOf(actions.Find(c => (string)c.Tag == "page-bluetooth"));
                check(bt >= 0 && body.Children.IndexOf(line) == bt + 1, "linha do fone logo abaixo dos dispositivos Bluetooth");
                int count = 0; foreach (UIElement child in body.Children) { var b = child as Border; if (b != null && (string)b.Tag == "fone-ghub") count++; }
                check(count == 1, "fone Bluetooth nao ganha linha do G HUB");
                SaveRender("conexoes-fone-ghub");
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "G522 Gaming Headset", Percent = 12, HoursLeft = 5, Connected
                    = true, FromGHub = true,
                    Link = "LIGHTSPEED" } });
                line = row();
                check(line != null && AutomationProperties.GetName(line) == "G522 Gaming Headset · LIGHTSPEED · 12% · cerca de 5 h" &&
                    ((TextBlock)((Grid)line.Child).Children[1]).Foreground == Brush("Critical"), "nova leitura atualiza a linha; nivel baixo em destaque");
                SetHeadsets(new List<HeadsetBattery>()); check(row() == null, "sem leitura a linha some");
            }
            finally { SetHeadsets(saved); headsetLogged = null; headsetNoted = null; Hide(); SetPanel(0); }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do fone pelo G HUB (pedidos, selecao, validacao, falhas, faixa de status e Conexoes).");
            checks.Add("Canal substituto, sem rede; nenhum pedido ao G HUB real nem ao fone.");
            File.WriteAllLines(Path.Combine(Output, "fone-ghub-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}
