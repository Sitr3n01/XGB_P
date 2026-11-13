// Fone na faixa de status: icone de fone e o nivel, so com o fone conectado. Duas fontes, so de leitura e nada enviado ao fone:
// o nivel que o fone Bluetooth informou ao Windows (ADR 0094) e o que o G HUB conhece dos fones Logitech sem fio (ADR 0106).
// Leitura a cada 30 s com a barra visivel e a cada abertura; uma fonte que falha nao apaga a outra.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using XGBP.Network;
using Shapes = System.Windows.Shapes;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyHeadsetStatus()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) { if (!ok) throw new Exception("Fone Bluetooth: " + why); checks.Add("OK: " + why); };
            var saved = headsets;
            try
            {
                check(BluetoothBattery.IsAudioService(@"BTHENUM\{0000111E-0000-1000-8000-00805F9B34FB}_LOCALMFG&005D\8&1") &&
                    BluetoothBattery.IsAudioService(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_X\1"), "Hands-Free e A2DP do fone contam como audio");
                check(!BluetoothBattery.IsAudioService(@"BTHENUM\{0000111F-0000-1000-8000-00805F9B34FB}_VID&0001\1") && !BluetoothBattery.IsAudioService(
                    @"BTHLE\DEV_020000000001\1") &&
                    !BluetoothBattery.IsAudioService(null), "gateway do celular, BLE generico e id vazio nao sao fone");
                var c = Guid.NewGuid();
                var merged = BluetoothBattery.ByContainer(new List<BluetoothBatteryNode> {
                    new BluetoothBatteryNode { Container = c, Percent = 40, InstanceId = "BTHLE\\x", Audio = false },
                    new BluetoothBatteryNode { Container = c, Percent = 70, InstanceId = "BTHENUM\\{0000111E-}", Audio = true } });
                check(merged.Count == 1 && merged[c].Percent == 70, "no de audio do mesmo aparelho tem prioridade");
                var order = new List<HeadsetBattery> { new HeadsetBattery { Name = "B", Connected = false }, new HeadsetBattery { Name = "C",
                    Connected = true }, new
                    HeadsetBattery { Name = "A", Connected = true } };
BluetoothBattery.Sort(order); check(order[0].Name == "A" && order[1].Name == "C" && order[2].Name == "B", "conectados primeiro, depois pelo nome");

                SetPanel(0); Show(); window.UpdateLayout();
            SetHeadsets(new List<HeadsetBattery>()); check(headsetSlot.Children.Count == 0 && headsetSlot.ToolTip == null, "sem fone a faixa fica como antes");
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "Space Travel 2", Percent = 100, Connected = false } });
                check(headsetSlot.Children.Count == 0, "nivel guardado de fone desconectado nao aparece");
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "G435", Percent = 80, Connected = true, Container = Guid.NewGuid() } });
                check(headsetSlot.Children.Count == 2 && headsetFill != null && Math.Abs(headsetFill.Width - 16.6 * .8) < .01 && headsetFill.Fill
                    == Brush("TextPrimary")
                    &&
                    (string)headsetSlot.ToolTip == "G435 · 80%", "fone conectado mostra icone, nivel e nome");
                check(AutomationProperties.GetName(headsetSlot) == "Fone Bluetooth: G435 · 80%", "nome acessivel do fone");
                window.UpdateLayout();
                var scene = (UIElement)window.FindName("Scene");
                double networkBottom = networkIcon.TranslatePoint(new Point(0, networkIcon.ActualHeight), scene).Y;
                foreach (FrameworkElement part in new[] { (FrameworkElement)headsetSlot.Children[0], (FrameworkElement)headsetSlot.Children[1] })
                    check(Math.Abs(part.TranslatePoint(new Point(0, part.ActualHeight), scene).Y - networkBottom) <= 1, "pe do " + (part is
                        Canvas ? "icone do fone" :
                        "nivel do fone") + " na mesma linha da rede");
                SaveRender("faixa-status-fone");
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "G435", Percent = 10, Connected = true } });
                check(headsetFill != null && headsetFill.Fill == Brush("Critical"), "fone com bateria baixa em destaque");
                SetHeadsets(new List<HeadsetBattery> { new HeadsetBattery { Name = "A", Percent = 50, Connected = false }, new HeadsetBattery {
                    Name = "B", Percent = 30,
                    Connected = true } });
                check((string)headsetSlot.ToolTip == "B · 30%", "entre varios, aparece o conectado");
                SetHeadsets(null); check(headsetSlot.Children.Count == 0, "leitura vazia remove o fone");

                var thing = new BluetoothThing { Name = "G435", Kind = "Áudio", Connected = true, Battery = 70 };
                check(BluetoothDetail(thing) == "Conectado · 70% · Áudio", "Conexoes mostram o nivel do fone conectado");
                thing.Connected = false; check(BluetoothDetail(thing) == "Desconectado · Áudio", "Conexoes nao mostram nivel guardado de desconectado");
            thing.Connected = true; thing.Battery = -1; check(BluetoothDetail(thing) == "Conectado · Áudio", "aparelho sem bateria informada fica como antes");
                int before = headsetSlot.Children.Count; headsetNextRead = DateTime.MinValue; HeadsetTick();
                check(!headsetBusy && headsetSlot.Children.Count == before, "verificacao nao le fones reais");
            }
            finally { SetHeadsets(saved); headsetLogged = null; Hide(); SetPanel(0); }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do fone Bluetooth na faixa de status e nas Conexoes.");
            checks.Add("Fones, niveis e conexoes substituidos; nenhum dispositivo Bluetooth lido ou operado.");
            File.WriteAllLines(Path.Combine(Output, "fone-bluetooth-verificacao.txt"), checks, Encoding.UTF8);
            VerifyGHubHeadset();
        }
    }
}
