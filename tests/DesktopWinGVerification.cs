// Excecao opt-in do desktop: somente Win+G; nenhum teclado, FSE ou preferencia real e alterado.
using System;
using System.IO;
using System.Text;
using XGBP.Input;
using XGBP.Games;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyDesktopWinG()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("Win+G desktop: " + why); };
            var oldRead = activationRead; var oldObserver = activationObserver; var oldSubscription = activationSubscription;
            var oldPreferences = launchPreferences; var oldWrite = launchWrite; var oldTray = desktopWinGTray;
            var oldClock = controllerNow; var oldFocus = controllerFocus;
            string folder = Path.Combine(Output, "desktop-wing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); string path = Path.Combine(folder, "launch.json");
            bool? mode = false; bool failRead = false; int installs = 0, writes = 0; Action trigger = null;
            ActivationObserverFixture observer = null;
            try
            {
                Hide(); activationStarted = false; activationSubscription = null;
                activationRead = delegate { if (failRead) throw new IOException("fixture"); return mode; };
                activationObserver = delegate(Action action) { installs++; trigger = action; observer = new ActivationObserverFixture(); return observer; };
                launchPreferences = new LaunchPreferenceState { AlwaysAdmin = true };
                launchWrite = delegate(LaunchPreferenceState value) { writes++; GameFiles.Write(path, value); };
                desktopWinGTray = new Forms.ToolStripMenuItem();
                DateTime now = DateTime.Now.AddMinutes(1); controllerNow = delegate { return now; }; controllerFocus = delegate { return true; };
                Action<string, bool> input = delegate(string button, bool down) {
                    now = now.AddMilliseconds(100); OnController(new ButtonEvent { Device = "DualSense#0-BT", Button = button, Down = down, Time = now });
                };
                check(!ActivationAllowed() && installs == 0, "desktop padrao continua inativo sem ponte");
                RequestActivation("Win+G"); Show(); check(!window.IsVisible && writes == 0, "padrao nao abre nem grava ao iniciar");
                ToggleDesktopWinG();
                check(writes == 1 && launchPreferences.DesktopWinG && launchPreferences.AlwaysAdmin && desktopWinGTray.Checked, "bandeja habilita preservando administrador");
                check(installs == 1 && !window.IsVisible && ActivationAllowed(), "habilitar instala ponte sem abrir sozinho");
                check(LaunchSettings.Read(path).DesktopWinG && LaunchSettings.Read(path).AlwaysAdmin, "preferencias persistem juntas");
                Show(); RequestActivation("bandeja"); RequestActivation("PS"); RequestActivation("outro");
                check(!window.IsVisible, "Show direto, bandeja, PS e origem desconhecida nao abrem desktop");
                input("PS", true); now = now.AddMilliseconds(100); TickGuide(); input("PS", false);
                input("MUTE", true); input("MUTE", false); input("CREATE", true); input("CREATE", false);
                check(!window.IsVisible && !guideCycleActive, "HID fisico, microfone e Create nao abrem desktop");
                RequestActivation("Win+G"); check(window.IsVisible, "Win+G abre desktop habilitado");
                RequestActivation("PS"); RequestActivation("bandeja");
                input("PS", true); input("PS", false);
                check(window.IsVisible, "PS/bandeja tambem nao fecham desktop");
                RequestActivation("Win+G"); check(!window.IsVisible, "Win+G fecha desktop");
                trigger(); PumpMotion(35); check(window.IsVisible, "ponte nova entrega Win+G no desktop");
                RequestActivation("Win+G"); Action stale = trigger; var removed = observer;
                ToggleDesktopWinG();
                check(!ActivationAllowed() && removed.Disposed && !desktopWinGTray.Checked && !LaunchSettings.Read(path).DesktopWinG, "desligar persiste e libera ponte");
                ToggleDesktopWinG(); stale(); PumpMotion(35);
                check(!window.IsVisible, "callback anterior a desligar/religar nao abre");
                trigger(); PumpMotion(35); check(window.IsVisible, "callback da geracao atual abre");
                removed = observer; ToggleDesktopWinG();
                check(!window.IsVisible && removed.Disposed, "desligar enquanto aberta oculta a barra");
                ToggleDesktopWinG(); mode = null; ActivationAllowed();
                RequestActivation("Win+G"); check(!window.IsVisible && observer.Disposed, "API incerta bloqueia mesmo com opcao ligada");
                mode = false; failRead = true; RequestActivation("Win+G");
                check(!window.IsVisible, "excecao da API bloqueia desktop"); failRead = false;
                mode = true; ActivationAllowed(); RequestActivation("PS"); check(window.IsVisible, "PS continua abrindo no FSE");
                mode = false; ActivationAllowed();
                check(!window.IsVisible && launchPreferences.DesktopWinG, "sair do FSE oculta mesmo com excecao desktop");
                input("PS", true); input("PS", false); check(!window.IsVisible, "PS apos sair do FSE nao reabre");
                RequestActivation("Win+G"); check(window.IsVisible, "Win+G pode abrir de novo no desktop apos transicao"); Hide();

                mode = true; ActivationAllowed(); SetPanel(5); Show();
                check(ActionLabel("launch-desktop-wing") == "Permitir Win+G no desktop · Ativado", "opcao aparece nas Configuracoes");
                FocusHome("launch-desktop-wing");
                actions.Find(c => (string)c.Tag == "launch-desktop-wing").BringIntoView(); window.UpdateLayout(); PumpMotion(100);
                ClearFeedback(); SaveRender("configuracoes-wing-desktop");
                bool preferred = launchPreferences.DesktopWinG;
                ToggleAlwaysAdmin(); check(launchPreferences.DesktopWinG == preferred && !launchPreferences.AlwaysAdmin, "opcao administrador preserva Win+G desktop");
                launchWrite = delegate { throw new IOException("fixture"); }; int before = installs;
                ToggleDesktopWinG();
                check(launchPreferences.DesktopWinG == preferred && installs == before, "falha ao salvar nao muda politica nem ponte");
                File.WriteAllText(path, "{\"Version\":1,\"AlwaysAdmin\":true}");
                check(!LaunchSettings.Read(path).DesktopWinG && LaunchSettings.Read(path).AlwaysAdmin, "arquivo antigo migra com desktop desligado");
            }
            finally
            {
                StopActivationObserver(); Hide();
                launchPreferences = oldPreferences; launchWrite = oldWrite;
                if (desktopWinGTray != oldTray) desktopWinGTray.Dispose(); desktopWinGTray = oldTray;
                activationRead = oldRead; activationObserver = oldObserver; activationSubscription = oldSubscription;
                activationStarted = false; controllerNow = oldClock; controllerFocus = oldFocus; ClearGuide(); ActivationAllowed(); SetPanel(0);
                foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder);
            }
            File.WriteAllText(Path.Combine(Output, "desktop-wing-verificacao.txt"), "OK: " + checks + " verificacoes. Opt-in desligado, persistencia/compatibilidade e preservacao de administrador; somente Win+G abre/fecha desktop, PS/HID/bandeja/Show direto ignorados; sem abrir ao habilitar/iniciar/transicionar, geracoes/callback antigo, fechamento ao desligar/sair do FSE, API incerta/excecao, FSE com PS preservado, opcoes de Settings/bandeja e falha ao salvar. Modo/ponte/HID e escrita real de preferencia substituidos; somente JSON temporario da verificacao. Sem hook, pacote, Win+G fisico ou entrada no FSE reais.\r\n", Encoding.UTF8);
        }
    }
}
