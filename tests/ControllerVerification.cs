// Regressao do caminho relatorio HID -> gesto -> interface. Sem HID, teclado ou comandos externos reais.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        int controllerAssertions;
        void ControllerAssert(bool value, string reason)
        {
            controllerAssertions++;
            if (!value) throw new Exception("Controle: " + reason);
        }
        static Buttons NeutralPad()
        {
            return new Buttons { Hat = 8, HasAnalog = true, LeftX = 128, LeftY = 128, RightX = 128, RightY = 128 };
        }
        void VerifyControllerReports()
        {
            Buttons b;
            foreach (int layout in new[] { 64, 78, 10 })
            {
                int start = 3, axes = layout == 78 ? 2 : 1, buttons = layout == 78 ? 9 : layout == 64 ? 8 : 5;
                int triggers = layout == 78 ? 6 : layout == 64 ? 5 : 8;
                var report = new byte[layout + start + 2]; report[start] = (byte)(layout == 78 ? 0x31 : 0x01);
                report[start + axes] = 0; report[start + axes + 1] = 255; report[start + axes + 2] = 128; report[start + axes + 3] = 127;
                report[start + triggers] = 123; report[start + triggers + 1] = 234;
                report[start + buttons] = 0xF8; report[start + buttons + 1] = 0xFF; report[start + buttons + 2] = 7;
                if (layout == 78) SealBluetoothFixture(report, start);
                ControllerAssert(DualSenseReport.Decode(report, start, layout, out b), "layout " + layout);
                ControllerAssert(b.HasAnalog && b.LeftX == 0 && b.LeftY == 255 && b.RightX == 128 && b.RightY == 127, "quatro eixos e offset " + layout);
                ControllerAssert(b.TriggerLeft == 123 && b.TriggerRight == 234, "gatilhos analogicos " + layout);
                int count = 0; foreach (var item in b.ToMap()) if (item.Value) count++;
                ControllerAssert(count == 15 && b.Hat == 8, "todos os botoes e repouso " + layout);
            }
            ControllerAssert(!DualSenseReport.Decode(null, 0, 64, out b), "null rejeitado");
            ControllerAssert(!DualSenseReport.Decode(new byte[64], -1, 64, out b), "offset negativo rejeitado");
            ControllerAssert(!DualSenseReport.Decode(new byte[64], 1, 64, out b), "limite do buffer respeitado");
            ControllerAssert(!DualSenseReport.Decode(new byte[64], 0, 0, out b), "relatorio vazio rejeitado");
            ControllerAssert(!DualSenseReport.Decode(new byte[] { 0x31, 0, 0 }, 0, 3, out b), "Bluetooth truncado rejeitado");
            ControllerAssert(!DualSenseReport.Decode(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, 0, 8, out b), "simples sem gatilhos rejeitado");
            Battery bat; ControllerAssert(!DualSenseReport.DecodeBattery(new byte[54], 2, 54, out bat), "bateria fora do buffer rejeitada");
            var stick = new StickDirectionTracker();
            ControllerAssert(StickDirectionTracker.Normalize(0) == -1 && StickDirectionTracker.Normalize(128) == 0 && StickDirectionTracker.Normalize(255) == 1, "normalizacao dos extremos");
            for (byte raw = 115; raw < 142; raw++) ControllerAssert(stick.Update(raw, raw, true) == null, "zona morta " + raw);
            ControllerAssert(stick.Update(180, 128, true) == "DIREITA", "entrada no limite");
            ControllerAssert(stick.Update(169, 128, true) == "DIREITA", "histerese mantem direcao");
            ControllerAssert(stick.Update(159, 128, true) == null, "saida antes do centro");
            ControllerAssert(stick.Update(200, 200, true) == "BAIXO", "diagonal tem apenas um eixo");
            ControllerAssert(stick.Update(202, 200, true) == "BAIXO", "diagonal nao oscila");
            ControllerAssert(stick.Update(255, 170, true) == "DIREITA", "troca pelo eixo dominante");
            ControllerAssert(stick.Update(0, 128, true) == "ESQUERDA", "inversao direta");
            ControllerAssert(stick.Update(0, 0, false) == null, "ausencia solta eixo");
            DateTime now = new DateTime(2026, 10, 25, 12, 0, 0);
            var events = new List<ButtonEvent>(); var tracker = new ButtonTracker("teste"); tracker.Event += events.Add;
            b = NeutralPad(); tracker.Update(b, now); ControllerAssert(events.Count == 0, "repouso sem entrada falsa");
            b.LeftX = 255; tracker.Update(b, now);
            for (int i = 0; i < 1000; i++) tracker.Update(b, now.AddMilliseconds(i));
            ControllerAssert(events.Count == 1 && events[0].Button == "LS_DIREITA" && events[0].Down, "mil relatorios geram um evento");
            b.LeftX = 0; tracker.Update(b, now.AddSeconds(2));
            ControllerAssert(events.Count == 3 && !events[1].Down && events[1].Button == "LS_DIREITA" && events[2].Button == "LS_ESQUERDA", "soltura antes da inversao");
            b = NeutralPad(); tracker.Update(b, now.AddSeconds(3)); events.Clear();
            b.TriggerLeft = 32; tracker.Update(b, now); b.TriggerLeft = 25; tracker.Update(b, now); b.TriggerLeft = 19; tracker.Update(b, now);
            ControllerAssert(events.Count == 2 && events[0].Button == "L2" && events[0].Down && !events[1].Down, "gatilho com histerese");
            events.Clear(); b = NeutralPad(); b.Mute = true; tracker.Update(b, now);
            tracker.Update(b, now.AddMilliseconds(599)); ControllerAssert(events.Count == 1, "microfone antes de 600 ms");
            tracker.Update(b, now.AddMilliseconds(600)); tracker.Update(b, now.AddSeconds(1));
            ControllerAssert(events.Count == 2 && events[1].Gesture == "SEGURADO", "um gesto aos 600 ms");
            b.Mute = false; tracker.Update(b, now.AddMilliseconds(1100));
            ControllerAssert(events.Count == 3 && events[2].Gesture == "LONGO", "soltura longa preservada");
        }
        void VerifyController()
        {
            var oldClock = controllerNow; var oldFocus = controllerFocus; var oldAudio = audioSnapshot;
            DateTime now = DateTime.Now.AddMinutes(1); bool focused = true;
            controllerAssertions = 0;
            try
            {
                VerifyControllerReports();
                controllerNow = delegate { return now; }; controllerFocus = delegate { return focused; };
                Action<string, bool> send = delegate(string button, bool down) {
                    now = now.AddMilliseconds(10); OnController(new ButtonEvent { Button = button, Down = down, Time = now, Device = "teste" });
                };
                Action<string> press = delegate(string button) { send(button, true); send(button, false); };
                // PS de 100 ms: toques abaixo de 30 ms sao descartados (repique e toques do DSX, ADR 0044).
                Action guideTap = delegate { send("PS", true); now = now.AddMilliseconds(100); send("PS", false); };
                Hide(); press("CREATE"); press("MUTE");
                ControllerAssert(!window.IsVisible, "Create e microfone curto nao abrem");
                now = now.AddSeconds(1);
                send("PS", true); now = now.AddMilliseconds(50); TickGuide(); ControllerAssert(window.IsVisible, "PS abre oculta no FSE substituido");
                OnController(new ButtonEvent { Device = "teste", Button = "PS", Down = true, Gesture = "SEGURADO", Time = now });
                ControllerAssert(window.IsVisible, "PS segurado nao fecha novamente"); send("PS", false);
                // A soltura do gesto nao abre/fecha: somente um novo aperto.
                now = now.AddSeconds(1); guideTap();
                ControllerAssert(!window.IsVisible, "PS fecha no novo aperto");
                Show(); SetPanel(0); rail[0].Focus(); ClearHeld();
                send("LS_BAIXO", true); ControllerAssert(Keyboard.FocusedElement == rail[1], "analogico move o foco");
                var before = Keyboard.FocusedElement; now = now.AddMilliseconds(419); TickController();
                ControllerAssert(Keyboard.FocusedElement == before, "sem repeticao antes de 420 ms");
                now = now.AddMilliseconds(1); TickController(); ControllerAssert(Keyboard.FocusedElement == rail[6], "repeticao aos 420 ms");
                send("LS_BAIXO", false); before = Keyboard.FocusedElement; now = now.AddSeconds(1); TickController();
                ControllerAssert(Keyboard.FocusedElement == before && repeatButton == null, "centro interrompe repeticao");
                rail[0].Focus(); send("LS_BAIXO", true); before = Keyboard.FocusedElement; send("BAIXO", true);
                ControllerAssert(Keyboard.FocusedElement == before, "direcional e analogico juntos nao duplicam");
                send("BAIXO", false); ControllerAssert(repeatButton == "BAIXO", "soltar direcional preserva analogico");
                send("LS_BAIXO", false); ControllerAssert(repeatButton == null, "soltar ambas as fontes encerra");
                send("LS_BAIXO", true); ClearHeld(); before = Keyboard.FocusedElement; now = now.AddSeconds(1); TickController();
                ControllerAssert(Keyboard.FocusedElement == before && repeatButton == null && holdSource == null, "perda de foco/desconexao limpa repeticao e segurar");
                focused = false; press("R1"); int unchanged = panel; press("OPTIONS"); ControllerAssert(panel == unchanged, "atalhos ignorados sem foco");
                now = now.AddSeconds(1); guideTap();
                ControllerAssert(!window.IsVisible, "PS fecha barra visivel mesmo sem foco"); focused = true; Show();
                SetPanel(0); rail[0].Focus(); press("R1"); ControllerAssert(panel == 0 && Keyboard.FocusedElement == rail[1], "R1 ancora na Biblioteca sem abrir");
                press("R1"); ControllerAssert(panel == 6, "R1 segue da Biblioteca para Som");
                press("L1"); ControllerAssert(panel == 6 && Keyboard.FocusedElement == rail[1], "L1 ancora na Biblioteca sem trocar painel");
                press("L1"); ControllerAssert(panel == 0, "L1 painel anterior");
                SetPanel(6); rail[6].Focus(); send("LS_CIMA", true); send("LS_CIMA", false);
                now = now.AddSeconds(1); TickController(); UpdateStatus(); SettleMotion();
                ControllerAssert(panel == 6 && Keyboard.FocusedElement == rail[1], "analogico fica ancorado na Biblioteca apos centro e atualizacao");
                window.UpdateLayout(); SaveRender("biblioteca-foco");
                press("OPTIONS"); ControllerAssert(panel == 5, "Options abre Configuracoes"); press("TRIANGULO"); ControllerAssert(panel == 0, "Triangulo volta a Home");
                press("QUADRADO"); ControllerAssert(Keyboard.FocusedElement == rail[0], "Quadrado foca trilho");
                press("L3"); ControllerAssert(!rail.Contains(Keyboard.FocusedElement as Button), "L3 entra no painel");
                press("R3"); ControllerAssert(Keyboard.FocusedElement == rail[0], "R3 foca trilho");
                press("TOUCHPAD"); ControllerAssert(!rail.Contains(Keyboard.FocusedElement as Button), "touchpad entra no painel");
                press("TOUCHPAD"); ControllerAssert(Keyboard.FocusedElement == rail[0], "touchpad volta ao trilho");
                press("CIRCULO"); ControllerAssert(!window.IsVisible, "Circulo fecha pelo trilho"); Show();
                // Alvos locais: selecionar e rolar nao operam apps, volume ou preferencias do usuario.
                SetPanel(5); body.Children.Clear(); actions.Clear();
                int clicks = 0; var stack = new StackPanel { Width = 900 };
                var scroll = new ScrollViewer { Height = 110, Width = 400, Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                body.Children.Add(scroll);
                for (int i = 0; i < 15; i++) { var button = new Button { Content = "Teste " + i, Height = 48, Width = 850 }; button.Click += delegate { clicks++; }; stack.Children.Add(button); actions.Add(button); }
                window.UpdateLayout(); actions[0].Focus(); press("X"); ControllerAssert(clicks == 1, "X seleciona uma vez");
                press("R2"); ControllerAssert(Keyboard.FocusedElement == actions[5], "R2 avanca cinco alvos");
                press("L2"); ControllerAssert(Keyboard.FocusedElement == actions[0], "L2 recua cinco alvos");
                press("L2"); ControllerAssert(Keyboard.FocusedElement == actions[0], "pagina nao circula no limite");
                window.UpdateLayout(); scroll.ScrollToHome(); window.UpdateLayout();
                send("RS_BAIXO", true); window.UpdateLayout(); ControllerAssert(scroll.VerticalOffset > 0, "analogico direito rola verticalmente");
                send("RS_BAIXO", false); double offset = scroll.VerticalOffset; now = now.AddSeconds(1); TickController(); window.UpdateLayout();
                ControllerAssert(scroll.VerticalOffset == offset, "soltar analogico direito para rolagem");
                send("RS_DIREITA", true); window.UpdateLayout(); ControllerAssert(scroll.HorizontalOffset > 0, "analogico direito rola horizontalmente"); send("RS_DIREITA", false);
                ClearHeld(); OnController(new ButtonEvent { Button = "OPTIONS", Down = true, Time = now.AddSeconds(-2) });
                ControllerAssert(body.Children.Contains(scroll), "evento antigo descartado");
                audioSnapshot = ExampleAudio(); SetPanel(6); FocusAudioTag("output"); ToggleAudioDevices(0); window.UpdateLayout();
                ControllerAssert(flyout != null, "lista flutuante de teste aberta");
                press("R2"); ControllerAssert(InsideControllerScope(Keyboard.FocusedElement as DependencyObject, flyout), "pagina mantem foco na lista");
                press("TOUCHPAD"); ControllerAssert(flyout == null, "touchpad fecha lista antes de mudar foco");
                var binding = audioBindings.Find(item => item.Key == "master"); binding.Slider.Focus();
                double volume = binding.Slider.Value;
                var packet = new byte[64]; packet[0] = 1; packet[1] = packet[2] = packet[3] = packet[4] = 128; packet[8] = 8;
                var pipeline = new ButtonTracker("pipeline"); pipeline.Event += OnController;
                Buttons decoded; DualSenseReport.Decode(packet, 0, packet.Length, out decoded); pipeline.Update(decoded, now);
                packet[1] = 255; now = now.AddMilliseconds(10);
                DualSenseReport.Decode(packet, 0, packet.Length, out decoded); pipeline.Update(decoded, now);
                ControllerAssert(binding.Slider.Value == Math.Min(100, volume + 5), "relatorio USB passa pelo tracker e ajusta slider substituido");
                packet[1] = 128; now = now.AddMilliseconds(10); DualSenseReport.Decode(packet, 0, packet.Length, out decoded); pipeline.Update(decoded, now);
                ControllerAssert(repeatButton == null, "relatorio neutro solta navegacao da interface");
                send("LS_DIREITA", true); DateTime releaseAt = now.AddMilliseconds(10); now = now.AddSeconds(2);
                OnController(new ButtonEvent { Button = "LS_DIREITA", Down = false, Time = releaseAt });
                ControllerAssert(repeatButton == null, "soltura atrasada cancela repeticao");
                File.WriteAllText(Path.Combine(Output, "controle-verificacao.txt"), "OK: " + controllerAssertions + " verificacoes. USB, BT completo/simples, limites, quatro eixos, gatilhos, zona morta/histerese, diagonais, eventos sem inundacao, gestos, abertura/fechamento, foco/repeticao, fontes simultaneas, selecao, paineis, atalhos, rolagem vertical/horizontal e listas.\r\nRelatorios/relogio/foco e comandos substituidos; nao afirma teste fisico. Nenhuma saida HID.\r\n", Encoding.UTF8);
            }
            finally
            {
                audioPending.Clear(); audioSnapshot = oldAudio; ClearHeld(); controllerNow = oldClock; controllerFocus = oldFocus; controllerInputSince = DateTime.MinValue;
                SetPanel(0);
            }
        }
    }
}
