// Regressao da reutilizacao da Steam e retorno de foco. Nenhum app real e operado.
using System;
using System.Drawing;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Input;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyWindowFocus()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("Foco/Steam: " + reason); };
            var game = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(100), ProcessId = 10, ProcessCreated = 1, ClassName = "jogo", Title = "Jogo" },
                Width = 1920, Height = 1080, HasMonitor = true, MonitorWidth = 1920, MonitorHeight = 1080 };
            var steam = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(200), ProcessId = 20, ProcessCreated = 2, ClassName = "steam", Title = "Steam — Modo Big Picture",
                ExecutablePath = @"C:\Steam\bin\cef\cef.win64\steamwebhelper.exe" }, Width = 1920, Height = 1080 };
            IntPtr foreground = game.Window.Handle; int returned = 0, activated = 0, launched = 0;
            bool exists = true, available = true, activateFails = false, returnAccepted = true;
            var oldAvailable = appAvailable; var oldSwitch = switchApp; var oldForeground = appForeground;
            var oldClock = controllerNow; var oldFocus = controllerFocus;
            try
            {
                ConfigureSteamVerification(); Hide();
                foregroundRead = delegate { return foreground; };
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? game : null; };
                findBigPicture = delegate { return exists ? steam : null; };
                existingWindowAvailable = delegate { return available; };
                returnWindowFocus = delegate(DesktopWindow item) { check(item == game.Window, "retorna a identidade capturada"); returned++; return returnAccepted; };
                appAvailable = delegate { return available; };
                appForeground = delegate(DesktopWindow item) { return foreground == item.Handle; };
                switchApp = delegate(DesktopWindow item) { if (activateFails) throw new IOException("ativacao negada"); check(item == steam.Window, "ativa a janela encontrada"); activated++; foreground = item.Handle; };
                steamLaunch = delegate { launched++; };
                controllerFocus = delegate { return true; };

                Show(); check(previousWindow == game && previousForegroundPid == 10, "captura jogo na abertura");
                foreground = handle; Show(); check(previousWindow == game, "Show repetido nao substitui origem");
                Hide(true); check(returned == 1 && !window.IsVisible, "fechar devolve foco uma vez");
                Hide(true); check(returned == 1, "ocultar duas vezes nao repete retorno");

                foreground = game.Window.Handle; Show(); foreground = new IntPtr(300); Hide(true);
                check(returned == 1, "outra janela em foco nao e substituida");
                foreground = game.Window.Handle; Show(); foreground = handle; Hide();
                check(returned == 1 && previousWindow == null, "despacho externo limpa origem sem roubar foco");
                foreground = game.Window.Handle; Show(); foreground = handle; returnAccepted = false; Hide(true);
                check(returned == 2 && !window.IsVisible, "foco negado nao impede fechar");
                foreground = game.Window.Handle; Show(); foreground = handle;
                returnWindowFocus = delegate { throw new IOException("janela desapareceu"); }; Hide(true);
                check(!window.IsVisible, "janela encerrada nao impede fechar");

                foreground = game.Window.Handle; Show(); foreground = handle; OpenSteamHome();
                check(activated == 1 && launched == 1 && !window.IsVisible && returned == 2, "Big Picture existente recebe navegacao Home e foco, sem retorno ao jogo");
                steam.Window.Minimized = true; Show(); OpenSteamHome();
                check(activated == 2 && launched == 2, "Big Picture minimizado recebe Home e e reutilizado"); steam.Window.Minimized = false;
                exists = false; Show(); OpenSteamHome(); check(launched == 3 && window.IsVisible, "sem Big Picture abre modo e aguarda janela");
                exists = true; available = false; Show(); OpenSteamHome(); check(launched == 4 && window.IsVisible, "janela encerrada antes da acao aguarda nova janela"); available = true;
                activateFails = true; Show(); OpenSteamHome();
                check(window.IsVisible && launched == 5, "erro ao ativar apos navegar nao dispara abertura concorrente"); activateFails = false;
                findBigPicture = delegate { throw new IOException("enumeracao indisponivel"); }; OpenSteamHome();
                check(window.IsVisible && launched == 5, "enumeracao incerta nao e tratada como Steam ausente");

                check(DesktopWindows.IsSteamBigPicture(steam.Window, @"C:\Steam"), "reconhece Steam e titulo pt-BR");
                steam.Window.Title = "Steam Big Picture Mode"; check(DesktopWindows.IsSteamBigPicture(steam.Window, @"C:\Steam"), "reconhece titulo ingles");
                steam.Window.Title = "Steam"; check(!DesktopWindows.IsSteamBigPicture(steam.Window, @"C:\Steam"), "desktop Steam nao e Big Picture");
                steam.Window.Title = "Steam — Big Picture"; steam.Window.ExecutablePath = @"C:\Outro\steamwebhelper.exe";
                check(!DesktopWindows.IsSteamBigPicture(steam.Window, @"C:\Steam"), "titulo nao substitui proveniencia do processo");
                steam.Window.ExecutablePath = @"C:\Steam-extra\bin\cef\steamwebhelper.exe";
                check(!DesktopWindows.IsSteamBigPicture(steam.Window, @"C:\Steam"), "prefixo parecido nao e raiz Steam");
                check(game.CoversMonitor, "ocupacao do monitor e aproximacao geometrica"); game.Width = 1280;
                check(!game.CoversMonitor && game.Describe().Contains("1280x1080"), "reducao de janela aparece no diagnostico");
                game.HasCloak = true; game.Cloaked = 2;
                check(game.Describe().Contains("camuflada=shell") && WindowObservation.CloakName(0) == "nao" && WindowObservation.CloakName(1) == "app" &&
                    WindowObservation.CloakName(4) == "herdada" && WindowObservation.CloakName(3) == "0x3", "camuflagem do DWM aparece no diagnostico");
                game.HasCloak = false; check(game.Describe().Contains("camuflada=indisponivel"), "camuflagem ilegivel nao vira falso");

                // Primeiro plano da barra: a abertura pede uma vez; negado, um aperto pede de novo (500 ms) ate a primeira ativacao.
                int takes = 0; bool grant = false; DateTime now = new DateTime(2026, 10, 25, 13, 0, 0);
                controllerNow = delegate { return now; }; controllerFocus = delegate { return foreground == handle; };
                takeForeground = delegate(IntPtr own) { check(own == handle, "pede foco para a propria barra"); takes++; if (grant) foreground = handle; return grant ? "concedido" : "negado"; };
                Action<string, bool> press = delegate(string button, bool down) { OnController(new ButtonEvent { Device = "HID", Button = button, Down = down, Time = now }); };
                Hide(); foreground = game.Window.Handle; Show(); foregroundSinceOpen = false;
                check(takes == 1 && foreground == game.Window.Handle && previousWindow == game, "abertura pede o foco uma vez e preserva a origem");
                press("BAIXO", true); check(takes == 2 && repeatButton == null, "aperto sem foco pede de novo e nao navega");
                press("BAIXO", false); check(takes == 2, "soltura nao pede foco");
                press("BAIXO", true); check(takes == 2, "novo pedido espera 500 ms");
                press("BAIXO", false); now = now.AddMilliseconds(600); grant = true;
                press("BAIXO", true); check(takes == 3 && foreground == handle && repeatButton == "BAIXO", "foco concedido no aperto ja navega");
                press("BAIXO", false); foregroundSinceOpen = true; foreground = new IntPtr(300); now = now.AddSeconds(1);
                press("BAIXO", true); check(takes == 3 && repeatButton == null, "depois do primeiro foco, outra janela escolhida e respeitada");
                press("BAIXO", false); Hide();
                takeForeground = delegate { throw new InvalidOperationException("negado"); };
                foreground = game.Window.Handle; Show(); check(window.IsVisible, "falha ao pedir foco nao impede abrir"); Hide();
                // App elevado na frente (Steam/DSX como administrador): um aviso por abertura, so com a barra sem elevacao.
                Func<bool> hint = delegate { FeedbackNotice n; return feedbackNotices.TryGetValue(FeedbackScope, out n) && n.Text.Contains("administrador"); };
                takeForeground = delegate { return "negado; fila nao anexada (erro 5, app da frente com integridade maior)"; };
                ClearFeedback(); Show(); check(hint(), "app elevado na frente explica como a barra recebe o controle");
                ClearFeedback(); foregroundSinceOpen = false; now = now.AddSeconds(1); press("BAIXO", true); press("BAIXO", false);
                check(!hint(), "aviso de elevacao uma vez por abertura"); Hide();
                barElevated = delegate { return true; }; Show(); check(!hint(), "barra elevada nao mostra o aviso"); Hide();
                takeForeground = delegate { return "negado; fila nao anexada (erro 87)"; }; barElevated = delegate { return false; };
                Show(); check(!hint(), "outro erro nao culpa a elevacao"); Hide(); ClearFeedback();
                check(DesktopWindows.TakeForeground(IntPtr.Zero, false) == "janela invalida", "foco nativo rejeita janela invalida");

                // A frente nao e um app (Visao de Tarefas, janela vazia do shell): o retorno vai para o app de cima (ADR 0044).
                var taskView = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(400), ProcessId = 40, ProcessCreated = 4, ClassName = "visao", Title = "Visão de Tarefas" } };
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? game : hwnd == taskView.Window.Handle ? taskView : null; };
                previousIsApp = delegate(DesktopWindow item) { return item != taskView.Window; };
                frontApp = delegate { return game; };
                takeForeground = delegate { return "substituido"; };
                int toApp = 0; returnWindowFocus = delegate(DesktopWindow item) { check(item == game.Window, "retorna ao app de cima"); toApp++; return true; };
                Hide(); foreground = taskView.Window.Handle; Show(); check(previousWindow == game, "Visao de Tarefas na frente: destino e o app de cima");
                foreground = handle; Hide(true); check(toApp == 1, "fechar devolve o foco ao app de cima");
                foreground = handle; Show(); check(previousWindow == game, "barra oculta ainda na frente: destino e o app de cima"); Hide();
                // Minimizou com a barra na frente (jogo que minimiza ao perder o foco): restaura. Minimizada antes da barra: nao.
                int restored = 0; restoreWindowFocus = delegate { restored++; return true; };
                returnWindowFocus = delegate { toApp++; return false; };
                var minimized = new WindowObservation { Window = new DesktopWindow { Handle = game.Window.Handle, ProcessId = 10, ProcessCreated = 1, ClassName = "jogo", Title = "Jogo", Minimized = true } };
                game.Window.Minimized = false; foreground = game.Window.Handle; Show(); foreground = handle;
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? minimized : null; };
                Hide(true); check(restored == 1 && toApp == 1, "jogo que minimizou com a barra na frente e restaurado");
                foreground = game.Window.Handle; Show(); foreground = handle; Hide(true);
                check(restored == 1 && toApp == 2, "janela minimizada antes da barra nao e restaurada");
                var other = new WindowObservation { Window = new DesktopWindow { Handle = game.Window.Handle, ProcessId = 11, ProcessCreated = 9, ClassName = "jogo", Title = "Outro", Minimized = true } };
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? game : null; }; foreground = game.Window.Handle; Show(); foreground = handle;
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? other : null; }; Hide(true);
                check(restored == 1, "HWND reciclado por outro processo nao e restaurado");
                observeWindow = delegate { return null; }; previousIsApp = delegate { return true; }; frontApp = delegate { return null; };

                // Uma janela WinForms criada pelo teste, fora da tela; sem ativar Steam ou outro app.
                using (var form = new Forms.Form { Text = "XGBP foco fixture", ShowInTaskbar = false, StartPosition = Forms.FormStartPosition.Manual,
                    Location = new Point(-30000, -30000), Size = new Size(640, 480) })
                {
                    IntPtr ownHandle = form.Handle; ShowWindow(ownHandle, 4);
                    var own = DesktopWindows.Observe(ownHandle);
                    check(own != null && own.Window.Handle == ownHandle && own.Width > 0 && own.HasPlacement && own.HasMonitor, "APIs nativas leem janela propria");
                    check(own.HasCloak && own.Cloaked == 0, "janela propria visivel nao esta camuflada");
                    DesktopWindows.MarkToolWindow(ownHandle); DesktopWindows.MarkToolWindow(ownHandle);
                    check((GetWindowLongPtr(ownHandle, -20).ToInt64() & 0x80) != 0, "estilo de ferramenta aplicado sem repetir");
                    DesktopWindows.FocusOnly(own.Window);
                    var afterFocus = DesktopWindows.Observe(ownHandle);
                    check(afterFocus.Left == own.Left && afterFocus.Top == own.Top && afterFocus.Width == own.Width && afterFocus.Height == own.Height &&
                        afterFocus.NormalWidth == own.NormalWidth && afterFocus.NormalHeight == own.NormalHeight, "foco nativo conserva limites e posicao normal da janela propria");
                    ShowWindow(ownHandle, 6);
                    check(!DesktopWindows.FocusOnly(own.Window), "retorno nao restaura janela minimizada pelo usuario");
                    check(DesktopWindows.Observe(ownHandle).Window.Minimized, "janela propria permanece minimizada");
                    ShowWindow(ownHandle, 4);
                    var forged = new DesktopWindow { Handle = ownHandle, ProcessId = own.Window.ProcessId + 1, ProcessCreated = own.Window.ProcessCreated, ClassName = own.Window.ClassName };
                    check(!DesktopWindows.FocusOnly(forged), "HWND com outra identidade nao recebe foco");
                    form.Dispose();
                    check(DesktopWindows.Observe(ownHandle) == null && !DesktopWindows.FocusOnly(own.Window), "janela fechada e rejeitada");
                }
                File.WriteAllText(Path.Combine(Output, "foco-steam-verificacao.txt"), "OK: " + checks + " verificacoes. Janela Steam existente/ausente/minimizada/encerrada, erro de ativacao/enumeracao sem protocolo concorrente, identidade por caminho, retorno de foco e escolha de outro app; primeiro plano da barra na abertura, novo pedido pelo controle a cada 500 ms ate a primeira ativacao, falha sem impedir abrir, aviso unico quando o app da frente e elevado; destino do retorno quando a frente nao e app (Visao de Tarefas, barra oculta na frente) e restauracao so do que minimizou com a barra na frente; camuflagem do DWM e estilo de ferramenta.\r\nSteam, foco e protocolos substituidos. API nativa: somente janela propria do teste, sem pedir foco com fila anexada; sem mudar modo Xbox, resolucao ou escala.\r\n", Encoding.UTF8);
            }
            finally
            {
                switchingApp = null; appSwitchTimer.Stop(); appAvailable = oldAvailable; switchApp = oldSwitch; appForeground = oldForeground;
                controllerNow = oldClock; controllerFocus = oldFocus;
                Hide(); ConfigureSteamVerification();
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    }
}
