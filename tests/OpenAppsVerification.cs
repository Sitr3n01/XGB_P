// Regressao dos apps camuflados: politica com substitutos locais e APIs nativas so em janelas proprias.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyAppSwitchDelivery()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) { checks++; if (!ok) throw new Exception("Entrega de foco: " + message); };
            IntPtr destination = new IntPtr(123), foreground = new IntPtr(456);
            int requests = 0, connections = 0, disconnections = 0, raises = 0;
            Func<bool> request = delegate { requests++; if (requests == 2) foreground = destination; return true; };
            Func<string> connect = delegate { connections++; return null; };
            Action disconnect = delegate { disconnections++; }, raise = delegate { raises++; };
            string report = DesktopWindows.TransferAppForeground(destination, delegate { return foreground; }, request, connect, disconnect, raise);
            check(requests == 2 && connections == 1 && disconnections == 1 && raises == 1 && foreground == destination && report.Contains("comFila=True"),
                "retorno direto positivo sem entrega tenta fila e a desliga");
            requests = connections = disconnections = raises = 0; foreground = destination;
            DesktopWindows.TransferAppForeground(destination, delegate { return foreground; }, delegate { requests++; return true; }, connect, disconnect, raise);
            check(requests == 1 && connections == 0 && disconnections == 0, "entrega direta dispensa fila");
            foreground = new IntPtr(456); requests = 0;
            report = DesktopWindows.TransferAppForeground(destination, delegate { return foreground; }, delegate { requests++; return false; },
                delegate { return "fila recusada (erro 5)"; }, disconnect, raise);
            check(requests == 1 && disconnections == 0 && report.Contains("erro 5"), "fila recusada e registrada sem simular teclado");
            bool failed = false;
            try { DesktopWindows.TransferAppForeground(destination, delegate { return foreground; }, delegate { return false; }, connect, disconnect, delegate { throw new IOException("fixture"); }); }
            catch (IOException) { failed = true; }
            check(failed && disconnections == 1, "fila desligada mesmo quando o pedido falha");

            var oldAvailable = appAvailable; var oldForeground = appForeground; var oldOwns = appOwnsForeground; var oldSwitch = switchApp;
            var oldRead = foregroundRead; var oldObserve = observeWindow; var oldIsApp = previousIsApp; var oldClock = feedbackNow;
            int oldEpoch = activationEpoch;
            try
            {
                DateTime now = new DateTime(2026, 10, 25, 16, 30, 0);
                var target = ExampleOpenApps()[0]; var other = ExampleOpenApps()[1];
                IntPtr current = handle; string confirmed = null; int sent = 0;
                feedbackNow = delegate { return now; }; foregroundRead = delegate { return current; }; observeWindow = delegate { return null; };
                appAvailable = delegate { return true; }; appOwnsForeground = delegate { return false; };
                appForeground = delegate(DesktopWindow item) { return confirmed == item.Key; };
                switchApp = delegate { check(window.IsVisible, "cada pedido ocorre com a barra ainda visivel"); sent++; };
                Show(); SetPanel(0); OpenApp(target);
                check(window.IsVisible && switchingApp == target && sent == 1 && appSwitchFeedback.Kind == FeedbackKind.Pending,
                    "pedido sem entrega nao esconde a barra nem confirma sucesso");
                now = now.AddMilliseconds(150); CheckAppSwitch(); check(sent == 2 && window.IsVisible, "nova tentativa apos restauracao/entrega assincrona");
                now = now.AddMilliseconds(300); CheckAppSwitch(); now = now.AddMilliseconds(300); CheckAppSwitch();
                check(sent == 3, "limite de tres pedidos por escolha");
                confirmed = target.Key; CheckAppSwitch();
                check(!window.IsVisible && switchingApp == null && appSwitchFeedback.Kind == FeedbackKind.Success, "entrega observada permite ocultar e confirma de novo");

                confirmed = null; Show(); OpenApp(target); now = now.AddSeconds(2); CheckAppSwitch();
                check(window.IsVisible && switchingApp == null && appSwitchFeedback.Kind == FeedbackKind.Attention, "recusa mantem a barra aberta sem reabrir/roubar foco");
                OpenApp(target); int before = sent; Hide(); now = now.AddSeconds(1); CheckAppSwitch();
                check(switchingApp == null && sent == before && !window.IsVisible, "fechamento manual cancela e nao manda pedido tardio");

                Show(); OpenApp(target); before = sent; activationEpoch++; CheckAppSwitch();
                check(switchingApp == null && sent == before, "troca de ciclo FSE cancela sem outro pedido"); activationEpoch = oldEpoch;
                OpenApp(target); before = sent; SetPanel(5); CheckAppSwitch();
                check(switchingApp == null && sent == before, "navegacao cancela pedido pendente"); SetPanel(0);

                OpenApp(target); OpenApp(other); confirmed = target.Key; CheckAppSwitch();
                check(switchingApp == other && window.IsVisible, "confirmacao do destino antigo nao conclui nova escolha");
                confirmed = other.Key; CheckAppSwitch(); check(switchingApp == null && !window.IsVisible, "destino novo conclui sua propria operacao");

                confirmed = null; Show(); OpenApp(target); before = sent; current = new IntPtr(789);
                observeWindow = delegate(IntPtr h) { return h == current ? new WindowObservation { Window = other } : null; };
                previousIsApp = delegate { return true; }; CheckAppSwitch();
                check(switchingApp == null && !window.IsVisible && sent == before, "outro app escolhido e respeitado sem disputar foco");
                current = handle; observeWindow = delegate { return null; };
                Show(); appForeground = delegate { return window.IsVisible; }; OpenApp(target);
                check(!window.IsVisible && switchingApp == target && appSwitchFeedback.Kind == FeedbackKind.Pending, "perda da entrega ao ocultar continua pendente");
                before = sent; now = now.AddSeconds(2); CheckAppSwitch();
                check(!window.IsVisible && switchingApp == null && sent == before && appSwitchFeedback.Kind == FeedbackKind.Attention,
                    "falha apos ocultar nao reabre barra nem envia foco de fundo");

                appForeground = delegate { return false; }; Show(); OpenApp(target);
                switchApp = delegate { throw new IOException("fixture"); }; now = now.AddMilliseconds(200); CheckAppSwitch();
                check(window.IsVisible && switchingApp == null && appSwitchFeedback.Kind == FeedbackKind.Error, "excecao na repeticao nao derruba UI nem esconde barra");
                OpenApp(target); check(window.IsVisible && switchingApp == null && appSwitchFeedback.Kind == FeedbackKind.Error, "erro inicial preserva barra e encerra operacao");
            }
            finally
            {
                switchingApp = null; appSwitchTimer.Stop(); activationEpoch = oldEpoch;
                appAvailable = oldAvailable; appForeground = oldForeground; appOwnsForeground = oldOwns; switchApp = oldSwitch;
                foregroundRead = oldRead; observeWindow = oldObserve; previousIsApp = oldIsApp; feedbackNow = oldClock;
                Hide(); ClearFeedback(); SetPanel(0);
            }
            File.WriteAllText(Path.Combine(Output, "apps-foco-verificacao.txt"), "OK: " + checks + " verificacoes. Pedido direto e fila com liberacao em finally, recusa registrada; barra mantida ate foreground observado, confirmacao posterior ao Hide, repeticao limitada somente com foco, timeout sem roubar foco, cancelamento por fechamento/reabertura/navegacao/ciclo FSE/outra escolha, resposta antiga, excecao e falha apos ocultar. Dependencias de foco/filas e relogio substituidas; sem operar apps do usuario ou mudar FSE.\r\n", Encoding.UTF8);
        }

        void VerifyXboxOpenApps()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) { checks++; if (!ok) throw new Exception("Apps/FSE: " + message); };
            foreach (bool? mode in new bool?[] { false, null, true })
            {
                check(AppWindowQuery.CloakAllowed(0, mode, null), "janela comum independe do FSE");
                check(AppWindowQuery.CloakAllowed(2, mode, true) == (mode == true), "shell exige FSE confirmado");
                check(!AppWindowQuery.CloakAllowed(2, mode, false), "outro desktop nao entra");
                check(!AppWindowQuery.CloakAllowed(2, mode, null), "desktop desconhecido nao entra");
                foreach (uint cloak in new uint[] { 1, 3, 4, 5, 6, 7, 8 })
                    check(!AppWindowQuery.CloakAllowed(cloak, mode, true), "camuflagem pelo app, herdada ou combinada nao entra: " + cloak);
            }
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            check(DesktopWindows.InternalShellSurface("Windows.UI.Core.CoreWindow", Path.Combine(windows, @"SystemApps\Client\TextInputHost.exe")), "experiencia de entrada nao vira app");
            check(!DesktopWindows.InternalShellSurface("Windows.UI.Core.CoreWindow", Path.Combine(windows, @"ImmersiveControlPanel\SystemSettings.exe")), "Configuracoes continua sendo app");
            check(!DesktopWindows.InternalShellSurface("Windows.UI.Core.CoreWindow", @"C:\Apps\TextInputHost.exe"), "nome do executavel fora do Windows nao exclui app");
            using (var query = new AppWindowQuery(true, DesktopWindows.ReadCloak, delegate { return true; }, new HashSet<string>(StringComparer.Ordinal) { "pacote!app" }))
            {
                check(query.HasFrame("pacote!app", 0), "conteudo hospedado reconhecido por AppUserModelID");
                check(!query.HasFrame("outro!app", 0) && !query.HasFrame(null, 0), "outro app ou identidade desconhecida nao e agrupado");
            }

            // Os substitutos mudam apenas a leitura destas duas janelas do teste, sem escrever estado de camuflagem.
            using (var form = new Forms.Form { Text = "XGBP apps fixture", StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-30000, -30000), Size = new System.Drawing.Size(400, 200) })
            using (var second = new Forms.Form { Text = "XGBP apps fixture", StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-30000, -30000), Size = new System.Drawing.Size(400, 200) })
            {
                ShowWindow(form.Handle, 4); ShowWindow(second.Handle, 4);
                var own = DesktopWindows.Observe(form.Handle).Window;
                uint cloak = 2; bool? desktop = true;
                Func<IntPtr, uint?> read = delegate(IntPtr h) { return h == form.Handle ? cloak : DesktopWindows.ReadCloak(h); };
                using (var desktopQuery = new AppWindowQuery(false, read, delegate { return desktop; }))
                using (var unknownQuery = new AppWindowQuery(null, read, delegate { return desktop; }))
                using (var xboxQuery = new AppWindowQuery(true, read, delegate { return desktop; }))
                {
                    check(!DesktopWindows.Available(own, desktopQuery) && !DesktopWindows.Available(own, unknownQuery), "validacao rejeita shell fora do FSE");
                    check(DesktopWindows.Available(own, xboxQuery), "validacao aceita shell no FSE");
                    check(DesktopWindows.Read(0, xboxQuery).Exists(w => w.Handle == own.Handle), "enumeracao usa a mesma politica");
                    check(!DesktopWindows.Read(own.ProcessId, xboxQuery).Exists(w => w.Handle == own.Handle), "proprio processo continua excluido");
                    desktop = false; check(!DesktopWindows.Available(own, xboxQuery), "validacao rejeita outro desktop");
                    desktop = null; check(!DesktopWindows.Available(own, xboxQuery), "validacao rejeita desktop ilegivel"); desktop = true;
                    bool refused = false;
                    try { DesktopWindows.Activate(own, desktopQuery); } catch (InvalidOperationException) { refused = true; }
                    check(refused, "ativacao nao contorna a politica");
                    DesktopWindows.Activate(own, xboxQuery);
                    check(!DesktopWindows.IsForeground(own, xboxQuery), "foreground ainda camuflado nao confirma troca");
                    cloak = 0;
                    check(DesktopWindows.IsForeground(own, xboxQuery) == (GetForegroundWindow() == own.Handle), "confirmacao exige foreground nativo e janela sem camuflagem");
                    var sameTitle = DesktopWindows.Read(0, xboxQuery).FindAll(w => w.ProcessId == own.ProcessId && w.Title == form.Text);
                    check(sameTitle.Count == 2, "duas janelas do mesmo processo e titulo permanecem distintas");
                    cloak = 1; check(!DesktopWindows.Available(own, xboxQuery), "app que se camuflou permanece excluido");
                    cloak = 6; check(!DesktopWindows.Available(own, xboxQuery), "camuflagem shell e herdada nao e liberada");
                    cloak = 2; ShowWindow(own.Handle, 6);
                    DesktopWindows.Activate(own, xboxQuery); PumpMotion(150);
                    check(!DesktopWindows.Observe(own.Handle).Window.Minimized, "selecao explicita restaura minimizada");
                    check(DesktopWindows.Exists(own) == true, "camuflagem nao significa fechamento");
                    var forged = new DesktopWindow { Handle = own.Handle, ProcessId = own.ProcessId, ClassName = own.ClassName, ProcessCreated = own.ProcessCreated + 1 };
                    check(DesktopWindows.Exists(forged) == false && !DesktopWindows.Available(forged, xboxQuery), "processo recriado nao recebe acao");
                    form.Dispose();
                    check(DesktopWindows.Exists(own) == false && !DesktopWindows.Available(own, xboxQuery), "janela destruida e rejeitada");
                }
            }

            var oldExists = appExists; var oldApps = openApps; var oldClosing = closingApp; var oldNotice = appCloseFeedback;
            var oldDeadline = appCloseDeadline; var oldVersion = appCloseVersion; var oldClock = feedbackNow;
            try
            {
                DateTime now = new DateTime(2026, 10, 25, 16, 0, 0); bool? alive = true;
                feedbackNow = delegate { return now; }; appExists = delegate { return alive; };
                openApps = new List<DesktopWindow>();
                Func<DesktopWindow> pending = delegate {
                    var item = new DesktopWindow { Handle = new IntPtr(101), ProcessId = 10, AppName = "Fixture" };
                    closingApp = item; appCloseVersion = 42; appCloseDeadline = now.AddSeconds(6);
                    appCloseFeedback = BeginFeedback("Fechando Fixture…", "0"); return item;
                };
                var target = pending();
                CheckAppClose(false, 42); check(closingApp == target, "snapshot falho nao confirma fechamento");
                CheckAppClose(true, 41); check(closingApp == target, "snapshot anterior ao pedido nao confirma fechamento");
                CheckAppClose(true, 42); check(closingApp == target && appCloseFeedback.Kind == FeedbackKind.Pending, "sumir da faixa com identidade viva nao confirma fechamento");
                alive = null; CheckAppClose(true, 42); check(closingApp == target, "identidade ilegivel permanece pendente");
                now = now.AddSeconds(7); CheckAppClose(true, 42);
                check(closingApp == null && appCloseFeedback.Kind == FeedbackKind.Warning && appCloseFeedback.Text.Contains("confirmar"), "identidade incerta termina sem sucesso presumido");
                alive = true; pending(); now = now.AddSeconds(7); CheckAppClose(true, 42);
                check(closingApp == null && appCloseFeedback.Text.Contains("ainda está aberto"), "app vivo fora da faixa continua aberto");
                alive = false; pending(); CheckAppClose(true, 42);
                check(closingApp == null && appCloseFeedback.Kind == FeedbackKind.Success, "identidade destruida depois do pedido confirma fechamento");
                pending(); appExists = delegate { throw new IOException("fixture"); }; now = now.AddSeconds(7); CheckAppClose(true, 42);
                check(closingApp == null && appCloseFeedback.Kind == FeedbackKind.Warning, "excecao na identidade nao vira sucesso");
            }
            finally
            {
                appExists = oldExists; openApps = oldApps; closingApp = oldClosing; appCloseFeedback = oldNotice;
                appCloseDeadline = oldDeadline; appCloseVersion = oldVersion; feedbackNow = oldClock; openAppsTimer.Interval = TimeSpan.FromSeconds(2);
                ClearFeedback(); SetPanel(0);
            }
            File.WriteAllText(Path.Combine(Output, "apps-fse-verificacao.txt"), "OK: " + checks + " verificacoes. FSE/desktop/API incerta; mascara de camuflagem; desktop virtual atual/outro/incerto; shell interno e identidade hospedada; enumeracao, validacao e ativacao pela mesma politica; varias janelas do mesmo app; restauracao e identidade; fechamento sem confundir ocultacao com saida.\r\nModo/camuflagem/desktop substituidos localmente. APIs nativas so operam janelas criadas pelo teste; sem mudar FSE, DWM ou apps do usuario. Nao comprova a troca real de apps camuflados pelo shell no FSE.\r\n", Encoding.UTF8);
        }
    }
}
