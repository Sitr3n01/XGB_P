// Regressao da bateria: fontes, formatos HID, validade e atualizacao da UI; sem tocar controles reais.
using System;
using System.IO;
using System.Text;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // Fixture com CRC calculado por tabela. Vetor fixo conferido independentemente com zlib.crc32.
        static void SealBluetoothFixture(byte[] report, int start)
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i; for (int bit = 0; bit < 8; bit++) c = (c & 1) == 0 ? c >> 1 : 0xEDB88320 ^ (c >> 1);
                table[i] = c;
            }
            uint crc = table[(0xFFFFFFFF ^ 0xA1) & 255] ^ (0xFFFFFFFF >> 8);
            for (int i = 0; i < 74; i++) crc = table[(crc ^ report[start + i]) & 255] ^ (crc >> 8);
            crc = ~crc;
            for (int i = 0; i < 4; i++) report[start + 74 + i] = (byte)(crc >> (i * 8));
        }
        void VerifyBattery()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("Bateria: " + reason); };
            var savedClock = batteryNow; DateTime now = new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);
            try
            {
                batteryNow = delegate { return now; }; batteries.Clear(); batteryDevice = null;
                byte[] usb = new byte[69]; usb[3] = 1; usb[56] = 5;
                Battery b;
                check(DualSenseReport.DecodeBattery(usb, 3, 64, out b) && b.Percent == 55 && b.ChargeState == 0, "USB com deslocamento e faixa de 10 por cento");
                usb[56] = 0x16; check(DualSenseReport.DecodeBattery(usb, 3, 64, out b) && b.Percent == 65 && b.ChargeState == 1, "carregando");
                usb[56] = 0x2A; check(DualSenseReport.DecodeBattery(usb, 3, 64, out b) && b.Percent == 100 && b.ChargeState == 2, "completa separada de carregando");
                usb[56] = 0x0F; check(!DualSenseReport.DecodeBattery(usb, 3, 64, out b), "nivel reservado nao vira 100 por cento");
                usb[56] = 0xB0; check(DualSenseReport.DecodeBattery(usb, 3, 64, out b) && b.Percent == -1, "erro de temperatura preservado");
                check(!DualSenseReport.DecodeBattery(usb, 3, 63, out b), "USB truncado rejeitado");
                byte[] bt = new byte[83]; bt[4] = 0x31; bt[58] = 7; SealBluetoothFixture(bt, 4);
                check(DualSenseReport.DecodeBattery(bt, 4, 78, out b) && b.Percent == 75, "Bluetooth com CRC e deslocamento");
                bt[58] ^= 1; check(!DualSenseReport.DecodeBattery(bt, 4, 78, out b), "byte alterado invalida CRC");
                check(!DualSenseReport.DecodeBattery(bt, 4, 56, out b) && !DualSenseReport.DecodeBattery(bt, 4, 77, out b), "Bluetooth truncado rejeitado");
                byte[] vector = new byte[78]; vector[0] = 0x31; vector[74] = 0x9D; vector[75] = 0x51; vector[76] = 0x9C; vector[77] = 0x1C;
                check(DualSenseReport.ValidBluetoothReport(vector, 0, 78), "vetor fixo CRC 1C9C519D conferido com zlib");
                byte[] simple = new byte[78]; simple[0] = 1; simple[5] = 0x28; simple[8] = 42; simple[9] = 84;
                Buttons buttons;
                check(!DualSenseReport.DecodeBattery(simple, 0, 78, out b) && !DualSenseReport.DecodeBattery(simple, 0, 10, out b), "simples curto e preenchido nao inventam bateria");
                check(DualSenseReport.Decode(simple, 0, 78, out buttons) && buttons.Cross && buttons.Hat == 8 && buttons.TriggerLeft == 42 && buttons.TriggerRight == 84, "simples preenchido conserva navegacao");

                check(Hid.ClassifyBatterySource("USB", new[] { "HID\\VID_054C&PID_0CE6", "USB\\VID_054C&PID_0CE6", "ROOT\\VIGEMBUS\\0" }) == BatterySource.Virtual, "virtual ViGEm reconhecido na ancestralidade");
                check(Hid.ClassifyBatterySource("USB", new[] { "USB\\ROOT_HUB30\\0", "ROOT\\ACPI_HAL\\0000" }) == BatterySource.PhysicalUsb, "USB fisico nao confundido com raiz ACPI");
                check(Hid.ClassifyBatterySource("BT", new[] { "BTHENUM\\DEV_0011", "USB\\ROOT_HUB30\\0", "ROOT\\ACPI_HAL\\0000" }) == BatterySource.PhysicalBluetooth, "Bluetooth fisico");
                check(Hid.ClassifyBatterySource("USB", new[] { "USB\\VID_054C&PID_0CE6" }) == BatterySource.Unknown, "caminho USB sozinho nao prova origem fisica");
                check(Hid.ClassifyBatterySource("USB", new[] { "USB\\ROOT_HUB30\\0", "ROOT\\OUTROBUS\\0" }) == BatterySource.Unknown, "barramento desconhecido nao recebe confianca por padrao");

                SetPanel(0); Show();
                SetBattery("simples", StatusSample(0, -1, -1, "relatorio sem bateria", BatterySource.PhysicalBluetooth));
                check(batteryDevice == null && batteryText == BatteryUnavailableText && batterySlot.Children.Count == 0, "controle simples conectado sem bateria nao parece aguardando controle");
                ControllerLost("simples");
                SetBattery("virtual", StatusSample(0x2A, 100, 2, "completa", BatterySource.Virtual));
                check(batteryDevice == null && batteryText == BatteryUnavailableText && batterySlot.Children.Count == 0, "virtual sozinho nao representa bateria");
                SetBattery("fisico-bt", StatusSample(5, 55, 0, "descarregando", BatterySource.PhysicalBluetooth));
                SetBattery("virtual", StatusSample(0x2A, 100, 2, "completa", BatterySource.Virtual));
                check(batteryDevice == "fisico-bt" && batteryText.Contains("55%"), "virtual nao sobrescreve fisico de 55 por cento");
                SetBattery("outro-bt", StatusSample(8, 85, 0, "descarregando", BatterySource.PhysicalBluetooth));
                check(batteryDevice == "fisico-bt", "fontes da mesma prioridade nao alternam a cada amostra");
                SetBattery("fisico-usb", StatusSample(0x16, 65, 1, "carregando"));
                check(batteryDevice == "fisico-usb" && batteryBolt != null, "USB fisico tem prioridade e mostra novo selo");
                SetPanel(4); check(batteryPanelText != null && batteryPanelText.Text.Contains("65%"), "painel recebe estado atual");
                SetBattery("fisico-usb", StatusSample(0x17, 75, 1, "carregando"));
                check(batteryPanelText.Text.Contains("75%"), "painel aberto atualiza sem reconstruir");
                ControllerLost("outro-bt"); check(batteryDevice == "fisico-usb", "perder fonte nao selecionada preserva selecao");
                ControllerLost("fisico-usb"); check(batteryDevice == "fisico-bt" && batteryPanelText.Text.Contains("55%"), "perda do USB retorna ao fisico Bluetooth");
                now = now.AddSeconds(4);
                SetBattery("fisico-bt", StatusSample(5, 55, 0, "descarregando", BatterySource.PhysicalBluetooth));
                now = now.AddSeconds(2); UpdateStatus();
                check(batteryDevice == "fisico-bt", "mesmo byte renova validade pela chegada de novos relatorios");
                var old = StatusSample(1, 15, 0, "descarregando", BatterySource.PhysicalBluetooth); old.ObservedUtc = now.AddSeconds(-3);
                SetBattery("fisico-bt", old); check(batteryText.Contains("55%"), "callback antigo nao substitui leitura recente");
                now = now.AddSeconds(4); UpdateStatus();
                check(batteryDevice == null && batterySlot.Children.Count == 0 && batteryPanelText.Text == WaitingText, "silencio expira leitura no painel e na faixa");

                SetBattery("fisico-usb", StatusSample(0x16, 65, 1, "carregando"));
                SetPanel(0); window.UpdateLayout(); SaveRender("bateria-carregando");
                check(batteryBolt != null && batteryFill.Fill == Brush("AccentText"), "carregamento com selo compacto e carga verde");
                SetPanel(4); window.UpdateLayout(); SaveRender("bateria-painel-atualizado");
                SetBattery("fisico-usb", StatusSample(0x2A, 100, 2, "completa")); SetPanel(0);
                check(batteryBolt == null && batteryFill.Fill == Brush("TextPrimary"), "completa sem raio de carregamento");
                window.UpdateLayout(); SaveRender("bateria-completa");
                File.WriteAllText(Path.Combine(Output, "bateria-verificacao.txt"), "OK: " + checks + " verificacoes de formatos/CRC, origem fisica/virtual, prioridade, renovacao, expiracao, painel e icones. Relogio/relatorios substituidos; nenhum HID real operado.\r\n", Encoding.UTF8);
            }
            finally
            {
                batteryNow = savedClock; batteries.Clear(); batteryDevice = null; SetPanel(0); UpdateStatus();
            }
        }
    }
}
