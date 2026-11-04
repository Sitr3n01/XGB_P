// Regressao do PS devolvido antes da soltura e da saida omitida no retorno sem jogo.
using System;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifySteamReturn()
        {
            var oldFind = findBigPicture; var oldAvailable = existingWindowAvailable; var oldAppAvailable = appAvailable;
            var oldSwitch = switchApp; var oldForeground = appForeground; var oldRead = foregroundRead; var oldFocus = controllerFocus;
            var oldNow = feedbackNow; var oldClock = controllerNow; var oldMode = activationMode; var oldActivation = activationRead;
            var oldMotion = motionTesting; var oldSystem = motionSystemOverride; var oldReduced = motionReduced;
            var oldGames = gamesView; var oldFresh = gamesSnapshotFresh; long oldSnapshot = gamesSnapshotVersion;
            int checks = 0, sent = 0, focused = 0; bool owns = true, delivered = false;
            DateTime now = DateTime.UtcNow;
            const string client = "OnFocusWindowChanged to window type: client, AppID 769\n";
            const string desktop = "OnFocusWindowChanged to window type: desktop, AppID 413080\n";
            string log = client;
            var target = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(720), ProcessId = 72,
                ProcessCreated = 720, ClassName = "steam", Title = "Steam - Big Picture" } };
            Action<bool, string> check = delegate(bool value, string name) { if (!value) throw new Exception("Retorno Steam: " + name); checks++; };
            Action reset = delegate {
                Hide(); ClearGuide(); steamInputPending = false; CancelAppSwitch("fixture");
                now = now.AddSeconds(10); owns = true; delivered = false; log = client;
                motionTesting = false; motionReduced = false; activationMode = true; activationRead = delegate { return true; };
                gamesView = new GamesView(); gamesSnapshotFresh = true; gamesSnapshotVersion = gameCommandVersion;
                launchingGame = unconfirmedLaunch = gameBusy = closingGame = null;
                Show(); SetPanel(0); sent = focused = 0;
            };
            Action<string> release = delegate(string device) { ReleaseGuide(new ButtonEvent { Button = "PS", Device = device, Down = false, Time = now, DurationMs = 100 }); };
            Action hold = delegate { guideDownDevices.Add("fisico-BT"); guideDownDevices.Add("virtual-USB"); guideCycleActive = guideCycleToggled = true; guideCycleStart = now; };
            Action<int> tick = delegate(int ms) { now = now.AddMilliseconds(ms); TickSteamReturn(); };
            try
            {
                ConfigureSteamVerification();
                feedbackNow = controllerNow = delegate { return now; };
                findBigPicture = delegate { return target; }; existingWindowAvailable = delegate { return true; };
                appAvailable = delegate { return true; }; controllerFocus = delegate { return owns; };
                foregroundRead = delegate { return delivered ? target.Window.Handle : owns ? handle : new IntPtr(800); };
                appForeground = delegate(DesktopWindow w) { return delivered && w.Handle == target.Window.Handle && w.ProcessCreated == 720; };
                switchApp = delegate { focused++; delivered = true; owns = false; };
                steamLaunch = delegate { sent++; };
                steamReturnContextLog = delegate { return delegate { return log; }; };
                reset(); hold(); Dismiss();
                check(sent == 0 && motionClosing, "PS ainda pressionado entregou foco");
                now = now.AddMilliseconds(700); TrySteamNavigation();
                check(sent == 0, "limite antigo de 500 ms liberou PS mantido");
                release("fisico-BT"); check(sent == 0, "soltura fisica ignorou PS virtual ainda apertado");
                release("virtual-USB"); check(sent == 1 && focused == 1 && !window.IsVisible && steamReturn != null, "ultima soltura nao entregou na hora");
                tick(150); check(steamReturn != null, "uma leitura confirmou estabilidade"); tick(150);
                check(steamReturn == null && !steamNavigationTimer.IsEnabled, "769 estavel nao confirmou ou deixou timer");

                reset(); hold(); Dismiss(); Show(); release("fisico-BT"); release("virtual-USB"); TrySteamNavigation();
                check(sent == 0 && window.IsVisible && !motionClosing, "soltura antiga fechou reabertura");
                reset(); hold(); Dismiss(); GuideDeviceLost("virtual-USB"); release("fisico-BT"); TrySteamNavigation();
                check(sent == 0 && !motionClosing, "perda do dispositivo foi tratada como soltura");
                reset(); hold(); Dismiss(); owns = false; TrySteamNavigation(); release("fisico-BT"); release("virtual-USB");
                check(sent == 0 && steamNavigationUri == null, "outra janela recebeu retorno tardio");
                reset(); hold(); Dismiss(); now = now.AddSeconds(9); TrySteamNavigation();
                check(sent == 0 && !motionClosing && window.IsVisible, "timeout entregou foco ou deixou barra invisivel");
                reset(); steamLaunch = delegate { sent++; hold(); }; Dismiss();
                check(focused == 0 && steamNavigationUri == null && !motionClosing, "PS novo durante despacho vazou para entrega");
                steamLaunch = delegate { sent++; };

                reset(); motionTesting = true; motionSystemOverride = false; Show(); SetPanel(0); PumpMotion(350);
                hold(); Dismiss(); PumpMotion(45);
                check(sent == 0 && motionClosing && MotionTranslation(MotionPart("Sidebar")).X < 0 && MotionPart("Sidebar").Opacity > 0,
                    "retorno sem jogo pulou animacao de saida");
                release("fisico-BT"); release("virtual-USB"); check(sent == 0, "soltura pulou restante da animacao");
                PumpMotion(200); check(sent == 1 && !window.IsVisible, "fim da animacao nao concluiu retorno");
                reset(); motionTesting = true; Show(); SetPanel(0); PumpMotion(350); hold(); Dismiss(); PumpMotion(45);
                Show(); release("fisico-BT"); release("virtual-USB"); PumpMotion(250);
                check(sent == 0 && window.IsVisible && !motionClosing, "callback de saida ocultou nova abertura");
                reset(); motionTesting = true; Show(); SetPanel(0); PumpMotion(350); hold(); Dismiss();
                motionReduced = true; SettleMotion(); check(sent == 0, "movimento reduzido ignorou PS");
                release("fisico-BT"); release("virtual-USB"); check(sent == 1 && !window.IsVisible, "movimento reduzido perdeu destino Home");

                reset(); steamInputPending = true; Dismiss(); check(sent == 0, "saida ultrapassou ciclo Steam pendente");
                steamInputPending = false; TrySteamNavigation(); check(sent == 1, "liberacao nao concluiu saida");
                reset(); switchApp = delegate { focused++; }; Dismiss(); check(switchingApp != null, "foco negado nao e acompanhado");
                now = now.AddSeconds(2); CheckAppSwitch();
                check(window.IsVisible && !motionClosing && !steamClosingVisual && MotionPart("Sidebar").Opacity == 1, "foco negado deixou barra apagada");
                reset(); Dismiss(); hold(); CheckAppSwitch();
                check(switchingApp == null && !motionClosing, "novo PS permitiu repeticao de foco pendente");
                switchApp = delegate { focused++; delivered = true; owns = false; };

                reset(); log = client + desktop; Dismiss(); tick(300);
                check(steamReturn != null, "769 seguido de desktop foi aceito"); tick(1600);
                check(steamReturn == null && !window.IsVisible, "contexto errado nao encerrou com aviso");
                reset(); log = client + "Loaded Config for App ID 413080\n"; Dismiss(); tick(300);
                check(steamReturn != null, "carga desktop sem foco novo foi aceita como Big Picture");
                reset(); log = client + "Loaded Config for App ID 443510\nLoaded Config for App ID 769\n"; Dismiss(); tick(300);
                check(steamReturn == null, "Guia retornando a 769 valido nao foi aceito");
                reset(); log = "Loaded Config for App ID 769\n"; Dismiss(); tick(300);
                check(steamReturn != null, "carga isolada comprovou foco sem evento novo");
                reset(); log = client + "OnFocusWindowChanged to window type: desktop, AppID 413"; Dismiss(); tick(300);
                check(steamReturn != null, "linha parcial usou contexto anterior");
                reset(); Dismiss(); now = now.AddMilliseconds(700); TickSteamReturn();
                check(steamReturn != null, "leitura atrasada confirmou estabilidade");
                delivered = false; TickSteamReturn(); check(steamReturn == null && focused == 1, "perda de foco roubou janela ou manteve observacao");
                reset(); steamReturnContextLog = delegate { return delegate { throw new IOException("rotacao fixture"); }; }; Dismiss(); tick(150);
                check(steamReturn == null && !steamNavigationTimer.IsEnabled, "falha de log nao encerrou observacao");
                File.WriteAllText(Path.Combine(Output, "retorno-steam-verificacao.txt"), "OK: " + checks +
                    " verificacoes. PS fisico/virtual, espera sem liberacao aos 500 ms, animacao de saida, reabertura, desconexao, foco negado, movimento reduzido, contexto 769 novo/estavel e falhas.\r\n" +
                    "Fixtures; nenhum protocolo, jogo, FSE ou controle fisico operado. Nao comprova ausencia de input duplicado no hardware.\r\n", Encoding.UTF8);
            }
            finally
            {
                steamInputPending = false; Hide(); ClearGuide(); CancelAppSwitch("fim fixture");
                findBigPicture = oldFind; existingWindowAvailable = oldAvailable; appAvailable = oldAppAvailable;
                switchApp = oldSwitch; appForeground = oldForeground; foregroundRead = oldRead; controllerFocus = oldFocus;
                feedbackNow = oldNow; controllerNow = oldClock; activationMode = oldMode; activationRead = oldActivation;
                motionTesting = oldMotion; motionSystemOverride = oldSystem; motionReduced = oldReduced;
                gamesView = oldGames; gamesSnapshotFresh = oldFresh; gamesSnapshotVersion = oldSnapshot;
                ConfigureSteamVerification();
            }
        }
    }
}
