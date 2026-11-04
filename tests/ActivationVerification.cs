// Matriz FSE/desktop/API incerta e ciclo de vida do observador. Nenhum input/modo real e alterado.
using System;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class ActivationObserverFixture : IDisposable
        {
            public bool Disposed;
            public void Dispose() { Disposed = true; }
        }
        void VerifyActivation()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("Ativacao: " + reason); };
            var oldRead = activationRead; var oldFactory = activationObserver; var oldKeys = activationSubscription;
            var oldFocus = controllerFocus; var oldClock = controllerNow;
            // Relogio controlado: cada evento chega 100 ms depois do anterior, acima do filtro de repique do PS.
            DateTime clock = DateTime.Now.AddMinutes(1);
            bool? mode = false; int installs = 0; bool readFails = false; Action keyboardEvent = null;
            ActivationObserverFixture keyboard = null;
            try
            {
                Hide(); activationStarted = false; activationSubscription = null;
                activationRead = delegate { if (readFails) throw new IOException("API substituida indisponivel"); return mode; };
                activationObserver = delegate(Action trigger) { installs++; keyboardEvent = trigger; keyboard = new ActivationObserverFixture(); return keyboard; };
                controllerFocus = delegate { return true; }; controllerNow = delegate { return clock; };
                Action<string, string, bool> input = delegate(string button, string gesture, bool down) {
                    clock = clock.AddMilliseconds(100); OnController(new ButtonEvent { Button = button, Gesture = gesture, Down = down, Time = controllerNow() });
                };
                // PS visto pela Steam: a decisao vem 40 ms depois do aperto, no tick do controle.
                Action settle = delegate { clock = clock.AddMilliseconds(50); TickGuide(); };
                check(!ActivationAllowed() && installs == 0, "desktop nao instala observador");
                Show(); RequestActivation("Win+G"); RequestActivation("bandeja"); input("PS", null, true); input("PS", null, false);
                input("MUTE", "SEGURADO", true);
                check(!window.IsVisible, "desktop ignora PS, Win+G, bandeja e microfone");
                mode = null; check(!ActivationAllowed() && installs == 0, "API incerta nao autoriza");
                readFails = true; RequestActivation("Win+G"); check(!window.IsVisible, "excecao de leitura nao autoriza"); readFails = false;
                mode = true; check(ActivationAllowed() && installs == 1 && !window.IsVisible, "entrar no FSE instala sem abrir");
                ActivationAllowed(); check(installs == 1, "consulta repetida nao reinstala");
                input("MUTE", "SEGURADO", true); check(!window.IsVisible, "microfone longo nao abre no FSE");
                input("PS", null, true); check(!window.IsVisible, "PS visto pela Steam espera a decisao"); settle(); check(window.IsVisible, "PS abre com FSE confirmado");
                input("PS", "SEGURADO", true); input("PS", "LONGO", false);
                check(window.IsVisible, "segurar e soltar PS nao duplicam");
                input("PS", "DUPLO", true); input("PS", "CURTO", false); check(!window.IsVisible, "novo aperto PS fecha");
                RequestActivation("Win+G"); check(window.IsVisible, "Win+G abre no FSE");
                RequestActivation("Win+G"); check(!window.IsVisible, "Win+G fecha no FSE");
                RequestActivation("Win+G"); input("LS_BAIXO", null, true);
                check(repeatButton != null, "navegacao ativa antes de sair do FSE");
                var removed = keyboard; var staleEvent = keyboardEvent;
                mode = false; check(!ActivationAllowed() && removed.Disposed && !window.IsVisible && repeatButton == null, "sair do FSE libera teclado, oculta e cancela input");
                mode = true; ActivationAllowed(); check(installs == 2, "reentrar no FSE instala uma vez");
                staleEvent(); PumpMotion(20); check(!window.IsVisible, "evento de sessao antiga descartado");
                keyboardEvent(); mode = false; ActivationAllowed(); PumpMotion(20);
                check(!window.IsVisible && keyboard.Disposed, "Win+G enfileirado nao abre apos sair");
                mode = true; ActivationAllowed(); RequestActivation("PS"); removed = keyboard;
                mode = null; ActivationAllowed(); check(!window.IsVisible && removed.Disposed, "perder API depois de ativo fecha e libera");
                mode = true; ActivationAllowed(); RequestActivation("PS"); readFails = true; ActivationAllowed();
                check(!window.IsVisible && keyboard.Disposed, "falha de leitura depois de ativo fecha e libera"); readFails = false;

                mode = true; ActivationAllowed(); controllerFocus = delegate { return false; }; Show();
                RequestActivation("PS"); check(!window.IsVisible, "PS fecha mesmo depois de perder foco");
                Show(); RequestActivation("Win+G"); check(!window.IsVisible, "Win+G fecha mesmo depois de perder foco");
                ClearGuide();
                Action<string, bool> guide = delegate(string device, bool down) {
                    clock = clock.AddMilliseconds(100); OnController(new ButtonEvent { Device = device, Button = "PS", Down = down, Time = controllerNow() });
                };
                guide("HID-A", true); settle(); check(window.IsVisible && steamGuideOpening, "PS do virtual abre 40 ms depois do aperto e pede o ciclo da Steam");
                guide("HID-A", true); check(window.IsVisible, "down duplicado depois de Show nao fecha");
                ClearHeld(); guide("HID-A", true); check(window.IsVisible, "perda de foco nao solta o PS");
                guide("HID-B", true); check(window.IsVisible, "dois dispositivos com PS apertado nao duplicam");
                guide("HID-A", false); guide("HID-B", false); guide("HID-A", true); settle();
                check(!window.IsVisible, "novo aperto depois de ambas solturas fecha");
                guide("HID-A", true); check(!window.IsVisible, "down duplicado depois de Hide nao reabre");
                OnController(new ButtonEvent { Device = "HID-A", Button = "PS", Down = false, Time = controllerInputSince.AddSeconds(-1) });
                guide("HID-A", true); settle(); check(window.IsVisible, "soltura anterior ao corte de navegacao libera o PS");
                guide("HID-A", false); Hide();

                // So o controle fisico (Bluetooth) recebeu o PS: a Steam nao o viu, e a barra abre ao soltar um toque curto (ADR 0042).
                controllerFocus = delegate { return true; }; ClearGuide();
                const string physical = "DualSense#0-BT", virtualPad = "DualSense#1-USB";
                Action<int, string, string, bool> at = delegate(int after, string device, string button, bool down) {
                    clock = clock.AddMilliseconds(after); OnController(new ButtonEvent { Device = device, Button = button, Down = down, Time = clock });
                };
                at(500, physical, "PS", true); check(!window.IsVisible, "fisico: o aperto ainda nao abre");
                at(120, physical, "PS", false); check(window.IsVisible && !steamGuideOpening, "fisico: soltar o toque curto abre sem ciclo da Steam");
                at(500, physical, "PS", true); at(120, physical, "PS", false); check(!window.IsVisible, "fisico: toque curto fecha ao soltar");
                at(500, physical, "PS", true); at(50, physical, "X", true); at(50, physical, "X", false); at(50, physical, "PS", false);
                check(!window.IsVisible, "PS com outro botao e atalho do DSX, nao abre");
                at(500, physical, "PS", true); at(40, physical, "LS_BAIXO", true); at(40, physical, "LS_BAIXO", false); at(40, physical, "PS", false);
                check(window.IsVisible, "analogico durante o toque nao vira atalho"); at(500, physical, "PS", true); at(120, physical, "PS", false);
                at(500, physical, "PS", true); at(700, physical, "PS", false); check(!window.IsVisible, "PS longo nao abre");
                at(500, physical, "PS", true); at(120, physical, "PS", false); check(window.IsVisible, "toque abre antes do repique");
                at(2, physical, "PS", true); at(4, physical, "PS", false); check(window.IsVisible, "repique de 4 ms logo depois da soltura nao fecha");
                at(300, physical, "PS", true); at(120, physical, "PS", false); check(!window.IsVisible, "toque real depois do repique fecha");
                at(500, physical, "PS", true); check(!window.IsVisible, "fisico apertado espera");
                at(2, virtualPad, "PS", true); settle(); check(window.IsVisible && steamGuideOpening && guideVirtualWarned, "PS tambem no virtual abre 40 ms depois do aperto, pede o ciclo e avisa");
                at(120, physical, "PS", false); at(2, virtualPad, "PS", false); check(window.IsVisible, "solturas dos dois dispositivos nao fecham");
                at(500, physical, "PS", true); at(2, virtualPad, "PS", true); at(120, physical, "PS", false); at(2, virtualPad, "PS", false);
                check(!window.IsVisible, "novo aperto nos dois fecha uma vez");
                at(500, physical, "PS", true); GuideDeviceLost(physical); at(120, physical, "PS", false);
                check(!window.IsVisible, "dispositivo perdido no meio do toque nao abre");
                at(500, physical, "PS", true); mode = false; ActivationAllowed(); mode = true; ActivationAllowed(); at(120, physical, "PS", false);
                check(!window.IsVisible, "troca de modo no meio do toque nao abre");
                at(500, physical, "PS", true); at(120, physical, "PS", false); check(window.IsVisible, "toque seguinte volta a abrir"); Hide();
                // Atalhos do DSX com o PS como modificador (teste das 14:35:23–14:35:36).
                at(500, physical, "QUADRADO", true); at(7, physical, "PS", true); at(180, physical, "QUADRADO", false); at(30, physical, "PS", false);
                check(!window.IsVisible, "botao apertado antes do PS e atalho");
                at(500, physical, "PS", true); at(1, virtualPad, "PS", true); at(5, physical, "QUADRADO", true); settle();
                check(!window.IsVisible, "atalho que chega logo depois do PS visto pela Steam nao abre");
                at(170, physical, "QUADRADO", false); at(10, physical, "PS", false); at(1, virtualPad, "PS", false); settle();
                check(!window.IsVisible, "soltar o atalho nao abre");
                at(500, physical, "PS", true); at(0, virtualPad, "PS", true); at(5, virtualPad, "PS", false); settle();
                check(!window.IsVisible, "PS virtual cancelado pelo DSX com o fisico apertado e atalho");
                at(150, physical, "PS", false); check(!window.IsVisible, "fim do atalho cancelado nao abre");
                at(500, virtualPad, "PS", true); at(1, virtualPad, "PS", false); settle(); check(!window.IsVisible, "toque de 1 ms do DSX nao abre");
                at(500, virtualPad, "PS", true); at(35, virtualPad, "PS", false); check(window.IsVisible, "toque rapido de 35 ms abre ao soltar");
                at(500, virtualPad, "PS", true); settle(); check(!window.IsVisible, "PS visto pela Steam fecha 40 ms depois do aperto");
                at(60, virtualPad, "PS", false);
                // Emulacao Xbox do DSX: o Guia do Xbox 360 virtual (XInput) e o PS que a Steam ve (ADR 0080).
                const string xboxPad = "XInput#0";
                at(500, physical, "PS", true); at(3, xboxPad, "PS", true); settle();
                check(window.IsVisible && steamGuideOpening, "Guia do XInput junto do fisico abre 40 ms depois e pede o ciclo da Steam");
                at(120, physical, "PS", false); at(2, xboxPad, "PS", false); check(window.IsVisible, "solturas do fisico e do XInput nao fecham");
                at(500, physical, "PS", true); at(3, xboxPad, "PS", true); settle(); check(!window.IsVisible, "novo Guia visto pela Steam fecha uma vez");
                at(120, physical, "PS", false); at(2, xboxPad, "PS", false);
                at(500, physical, "PS", true); at(3, xboxPad, "PS", true); GuideDeviceLost(xboxPad); at(120, physical, "PS", false);
                check(guideDownDevices.Count == 0, "XInput desconectado no meio do toque nao deixa o PS preso"); Hide();
                RequestActivation("Win+G"); check(window.IsVisible && !steamGuideOpening, "Win+G abre sem ciclo da Steam"); Hide();
                VerifyProbeTail(check);
                File.WriteAllText(Path.Combine(Output, "ativacao-verificacao.txt"), "OK: " + checks + " verificacoes. Desktop/FSE/API incerta e excecao, PS curto/segurado/novo aperto, PS do fisico ao soltar (atalho, longo, repique, virtual junto, dispositivo perdido, troca de modo), PS visto pela Steam decidido em 40 ms, atalhos do DSX (botao antes do PS, botao logo depois, PS virtual cancelado, toques de 1 ms), Guia do XInput (emulacao Xbox) como PS visto pela Steam, Win+G, perda de foco, ponte temporaria, saida/reentrada, callback antigo, parser, histórico, duplicatas, arquivo parcial/truncado e limites.\r\nModo, log, foco e comandos substituidos. Sem captura de teclado, hook, alteracao do modo ou pacote.\r\n", Encoding.UTF8);
            }
            finally
            {
                StopActivationObserver(); activationRead = oldRead; activationObserver = oldFactory; activationSubscription = oldKeys;
                activationStarted = false; controllerFocus = oldFocus; controllerNow = oldClock; ClearGuide(); ActivationAllowed(); SetPanel(0);
            }
        }
        void VerifyProbeTail(Action<bool, string> check)
        {
            DateTime now = DateTime.Now;
            Func<int, string, string> record = delegate(int pid, string uri) {
                return now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " | pid=" + pid + " | pai=explorer(1) | frente=Steam | args(1)=" + uri + " | cmd=sonda";
            };
            const string wing = "ms-gamebar://hotkey/?ihkid=IHKID_GAME_GUIDE_OVERLAY&source=GbEm";
            DateTime time; string id;
            check(ProbeActivationTail.TryParse(record(1, wing), out time, out id), "Win+G real da sonda reconhecido");
            check(!ProbeActivationTail.TryParse(record(1, "ms-gamebar://hotkey/?ihkid=IHKID_CAPTURE_GAME_SCREENSHOT"), out time, out id), "captura nao e abertura");
            check(!ProbeActivationTail.TryParse(record(1, "ms-gamebar://hotkey/?gamepadCommand=GPCMD_NEXUS_SHORTPRESS"), out time, out id), "PS do shell nao duplica HID");
            check(!ProbeActivationTail.TryParse(record(1, "ms-gamingoverlay://startuptips/?ihkid=IHKID_GAME_GUIDE_OVERLAY"), out time, out id), "dica nao e abertura");
            check(!ProbeActivationTail.TryParse(record(1, wing + "&ihkid=IHKID_GAME_GUIDE_OVERLAY"), out time, out id), "parametro duplicado rejeitado");
            check(!ProbeActivationTail.TryParse(record(1, wing).Replace("args(1)", "args(2)"), out time, out id), "multiplos argumentos rejeitados");
            check(!ProbeActivationTail.TryParse("texto qualquer " + wing, out time, out id), "substring fora do evento rejeitada");
            string folder = Path.Combine(Output, "activation-fixture"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "probe.log"); var utf8 = new UTF8Encoding(false);
            File.WriteAllText(path, record(2, wing) + "\n", utf8);
            var tail = new ProbeActivationTail(path, delegate { return now; });
            check(tail.ReadAvailable() == 0, "historico ao iniciar ignorado");
            File.AppendAllText(path, record(3, wing) + "\n", utf8); check(tail.ReadAvailable() == 1, "evento novo entregue uma vez");
            check(tail.ReadAvailable() == 0, "mesmos bytes nao repetidos");
            File.AppendAllText(path, record(3, wing) + "\n", utf8); check(tail.ReadAvailable() == 0, "mesmo evento duplicado rejeitado");
            string partial = record(4, wing); File.AppendAllText(path, partial.Substring(0, 40), utf8);
            check(tail.ReadAvailable() == 0, "linha parcial aguarda"); File.AppendAllText(path, partial.Substring(40) + "\n", utf8);
            check(tail.ReadAvailable() == 1, "restante da linha completa evento");
            now = now.AddSeconds(10); File.AppendAllText(path, record(5, wing).Replace(now.ToString("yyyy-MM-dd HH:mm:ss.fff"), now.AddSeconds(-10).ToString("yyyy-MM-dd HH:mm:ss.fff")) + "\n", utf8);
            check(tail.ReadAvailable() == 0, "evento antigo rejeitado");
            File.WriteAllText(path, record(6, wing) + "\n", utf8); check(tail.ReadAvailable() == 1, "truncamento retoma do inicio");
            File.AppendAllText(path, new string('x', 70000) + "\n" + record(7, wing) + "\n", utf8);
            int count = tail.ReadAvailable() + tail.ReadAvailable(); check(count == 1, "linha excessiva descartada sem perder proxima");
            string missing = Path.Combine(folder, "ausente.log"); if (File.Exists(missing)) File.Delete(missing);
            var late = new ProbeActivationTail(missing, delegate { return now; }); check(late.ReadAvailable() == 0, "arquivo ausente nao abre");
            File.WriteAllText(missing, record(8, wing) + "\n", utf8); check(late.ReadAvailable() == 1, "arquivo criado depois reconhecido");
        }
    }
}
