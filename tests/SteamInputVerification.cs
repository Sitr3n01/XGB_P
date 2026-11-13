// Regressao do ciclo forcar/liberar do Steam Input. Protocolo, console_log, foco e Steam substituidos.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifySteamInput()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("Steam Input: " + reason); };
            var game = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(100), ProcessId = 10, ProcessCreated = 1, ClassName = "jogo", Title = "Jogo" } };
            var launches = new List<string>(); var console = new StringBuilder(); var context = new StringBuilder();
            bool running = true, focused = true, lateForce = false, failForce = false;
            IntPtr foreground = game.Window.Handle; int returned = 0; Func<SteamInputCycle> held = null;
            var oldFocus = controllerFocus; var oldGames = gamesView;
            Action<Func<SteamInputCycle>> inline = delegate(Func<SteamInputCycle> body) { CompleteSteamInput(body()); };
            Action<Func<SteamInputCycle>> hold = delegate(Func<SteamInputCycle> body) { held = body; };
            try
            {
                ConfigureSteamVerification(); Hide();
                controllerFocus = delegate { return focused; };
                foregroundRead = delegate { return foreground; };
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? game : null; };
                returnWindowFocus = delegate { returned++; return true; };
                steamInputAvailable = delegate { return running; };
                steamBarInputLaunch = delegate(string uri) {
                    if (failForce && uri == SteamInput.ForceDesktop) throw new IOException("protocolo indisponivel");
                    launches.Add(uri);
                    if (uri == SteamInput.ForceDesktop && lateForce) return; // a Steam executa depois da liberacao
                    console.AppendLine(SteamInput.Executed(uri));
                    if (uri == SteamInput.ForceDesktop) context.AppendLine("OnFocusWindowChanged URL Forcing to window type: k_EWindowTypeGame, 413080");
                    if (uri == SteamInput.Release && lateForce) { console.AppendLine(SteamInput.Executed(SteamInput.ForceDesktop)); lateForce = false; }
                };
                steamInputLog = delegate { int start = console.Length; return delegate { return console.ToString().Substring(start); }; };
                steamBarConsoleLog = steamInputLog;
                steamBarContextLog = delegate { int start = context.Length; return delegate { return context.ToString().Substring(start); }; };
                Func<string, int> count = delegate(string uri) { return launches.FindAll(u => u == uri).Count; };
                // Abertura pelo PS que a Steam tambem recebeu (o virtual do DSX); as outras aberturas nao forcam.
                Action open = delegate { steamGuidePending = true; try { Show(); } finally { steamGuidePending = false; } };

                Show(); check(launches.Count == 0 && !steamInputPending, "abertura sem PS visto pela Steam nao forca"); Hide();
                open(); foreground = handle;
                check(launches.Count == 2 && launches[0] == SteamInput.ForceDesktop && launches[1] == SteamInput.Release, "abertura com foco forca e libera em ordem");
                check(!steamInputPending && !File.Exists(steamInputMarker), "ciclo concluido sem marcador");
                Show(); MaybeHoldSteamInput(); check(launches.Count == 2, "um ciclo por abertura");
                Hide(true); check(!window.IsVisible && returned == 1, "ciclo concluido nao atrasa o fechamento");
                // App de tras fora da Steam (Configuracoes): a Steam ja fica no desktop; sem ciclo e sem espera (ADR 0044).
                steamOwned = delegate { return false; }; foreground = game.Window.Handle; open();
                check(launches.Count == 2 && !steamGate && !steamInputPending, "app de tras fora da Steam nao espera nem forca"); Hide();
                steamOwned = delegate { return true; };

                running = false; foreground = game.Window.Handle; open(); check(launches.Count == 2, "sem a Steam aberta nao despacha protocolo"); Hide(); running = true;
                focused = false; open(); check(launches.Count == 2, "sem primeiro plano nao forca");
                focused = true; MaybeHoldSteamInput(); check(launches.Count == 4, "primeiro plano obtido depois inicia o ciclo"); Hide();

                // Fechar no meio do ciclo: a barra fica invisivel e so devolve o foco depois da liberacao.
                steamInputRun = hold; returned = 0; foreground = game.Window.Handle; open(); foreground = handle;
                check(steamInputPending && held != null && File.Exists(steamInputMarker), "ciclo pendente com marcador");
                Hide(true);
                check(window.IsVisible && window.Opacity == 0 && !window.IsHitTestVisible && returned == 0 && motionClosing, "fechar aguarda a liberacao, invisivel e sem foco devolvido");
                Hide(true); check(window.IsVisible && returned == 0, "novo fechamento durante a espera nao duplica");
                CompleteSteamInput(held()); held = null;
                check(!window.IsVisible && returned == 1 && window.Opacity == 1 && window.IsHitTestVisible, "liberacao confirmada fecha e devolve o foco uma vez");

                foreground = game.Window.Handle; open(); foreground = handle; Hide(true);
                Show(); check(window.IsVisible && window.Opacity == 1 && !steamHideDeferred && !motionClosing, "reabrir durante a espera mostra a barra de novo");
                var older = held; CompleteSteamInput(older());
                check(steamGate && steamInputPending && held != older, "reabertura aguarda um ciclo novo; resultado anterior nao libera");
                CompleteSteamInput(held()); held = null;
                check(window.IsVisible && returned == 1, "liberacao depois de reabrir nao fecha a barra");
                Hide();
                foreground = game.Window.Handle; open(); foreground = handle; check(steamInputPending, "ciclo pendente antes de sair do modo");
                Hide(true); Hide(); check(!window.IsVisible && window.Opacity == 1 && !steamHideDeferred, "saida do modo durante a espera oculta sem esperar");
                CompleteSteamInput(held()); held = null; check(!window.IsVisible, "liberacao tardia nao reabre");

                foreground = game.Window.Handle; open(); foreground = handle; older = held;
                Hide(); foreground = game.Window.Handle; open(); foreground = handle;
                check(steamGate && steamInputPending && held == older, "ocultar sem retorno e reabrir conserva bloqueio ate o ciclo anterior sair");
                CompleteSteamInput(older());
                check(steamGate && steamInputPending && held != older, "resultado cancelado inicia nova geracao sem liberar a abertura nova");
                CompleteSteamInput(held()); held = null;
                check(!steamGate && !steamInputPending, "somente a geracao nova confirmada libera a reabertura"); Hide();

                // Enquanto a Steam ainda tem o controle: painel apagado e sem navegar; so o circulo e o PS fecham (ADR 0043).
                var content = MotionPart("PanelContent");
                Action<string, bool> key = delegate(string button, bool down) { OnController(new ButtonEvent { Device = "teste", Button = button, Down = down, Time = controllerNow() }); };
                foreground = game.Window.Handle; open(); foreground = handle;
                check(steamGate && Math.Abs(content.Opacity - SteamGateOpacity) < .01, "abertura com a Steam no controle apaga o painel");
                key("BAIXO", true); check(repeatButton == null, "sem navegar enquanto a Steam tem o controle"); key("BAIXO", false);
                CompleteSteamInput(held()); held = null;
                check(!steamGate && Math.Abs(content.Opacity - 1) < .01 && window.IsVisible, "confirmacao da Steam acende o painel");
                key("BAIXO", true); check(repeatButton == "BAIXO", "depois da confirmacao a barra navega"); key("BAIXO", false); Hide();
                foreground = game.Window.Handle; open(); foreground = handle; key("CIRCULO", true);
                check(steamHideDeferred && steamGate, "circulo fecha durante a espera"); key("CIRCULO", false);
                CompleteSteamInput(held()); held = null;
                check(!window.IsVisible && !steamGate && Math.Abs(content.Opacity - 1) < .01, "liberacao conclui o fechamento com o painel normal");
                var oldNow = controllerNow; DateTime late = DateTime.Now.AddSeconds(10);
                foreground = game.Window.Handle; open(); foreground = handle; controllerNow = delegate { return late; }; TickController();
                check(steamGate && steamGateFailed && steamInputPending, "limite sem resposta conserva bloqueio e informa falha"); controllerNow = oldNow;
                CompleteSteamInput(held()); held = null; Hide();
                Show(); check(!steamGate && Math.Abs(content.Opacity - 1) < .01, "abertura sem PS visto pela Steam nao apaga"); Hide();
                // Entrada normal mesmo esperando a Steam; o bloqueio depende da confirmacao, nao da animacao.
                Func<double> entrance = delegate {
                    var sidebar = MotionPart("Sidebar"); var item = motionClocks.Find(c => c.Target == sidebar && c.Property == UIElement.OpacityProperty);
                    return item == null ? 0 : item.Clock.Timeline.Duration.TimeSpan.TotalMilliseconds;
                };
                // Animacoes do Windows ligadas por substituto: numa maquina com elas desligadas a barra entra sem animacao.
                var systemMotion = motionSystemOverride;
                motionTesting = true; motionSystemOverride = false;
                try
                {
                    foreground = game.Window.Handle; open(); foreground = handle; double slow = entrance(); Hide();
                    CompleteSteamInput(held()); held = null;
                    Show(); double normal = entrance(); Hide();
                    check(slow == MotionEntrance && normal == MotionEntrance, "entrada de 250 ms com e sem espera da Steam");
                }
                finally { motionTesting = false; motionSystemOverride = systemMotion; StopMotion(); }
                steamInputRun = inline;

                // Console da Steam ilegivel: a liberacao sai depois do limite, mesmo sem confirmacao.
                steamInputLog = delegate { return delegate { return null; }; };
                steamBarConsoleLog = steamInputLog;
                int before = launches.Count; foreground = game.Window.Handle; open(); foreground = handle;
                check(launches.Count == before + 2 && launches[launches.Count - 1] == SteamInput.Release && File.Exists(steamInputMarker) && steamGate, "sem confirmacao tenta liberar mas conserva diario e bloqueio");
                Hide();
                steamInputLog = delegate { int start = console.Length; return delegate { return console.ToString().Substring(start); }; };
                steamBarConsoleLog = steamInputLog;
                lateForce = true; before = count(SteamInput.Release); foreground = game.Window.Handle; open(); foreground = handle;
                check(count(SteamInput.Release) == before + 2, "forca executada depois da liberacao repete a liberacao");
                Hide();
                failForce = true; before = launches.Count; foreground = game.Window.Handle; open(); foreground = handle;
                check(launches.Count == before + 1 && launches[launches.Count - 1] == SteamInput.Release && !steamInputPending && File.Exists(steamInputMarker) && steamGate,
                    "falha incerta ao forcar tenta limpeza e conserva diario/bloqueio");
                failForce = false; Hide();

                // Atalho da Steam fora das pastas dela (Genshin pelo VBS): a Steam acompanha o jogo e reage ao PS (ADR 0066).
                steamOwned = delegate { return false; };
                gamesView = ExampleGames();
                var shortcut = TransitionFixture("shortcut:1:3899276376", 10, 100, 1); gamesView.Activities.Add(shortcut);
                before = launches.Count; foreground = game.Window.Handle; open(); foreground = handle;
                check(launches.Count == before + 2 && !steamInputPending, "atalho da Steam fora das pastas dela forca e libera"); Hide();
                shortcut.Key = "local:genshin"; before = launches.Count; foreground = game.Window.Handle; open(); foreground = handle;
                check(launches.Count == before && !steamGate, "jogo fora da Steam continua sem ciclo"); Hide();
                shortcut.Key = "shortcut:1:3899276376"; shortcut.Window = new DesktopWindow { Handle = new IntPtr(101), ProcessId = 10, ProcessCreated = 5, Title = "Genshin" };
                shortcut.Processes.Clear(); shortcut.Processes.Add(new ProcessIdentity(10, 5));
                before = launches.Count; foreground = game.Window.Handle; open(); foreground = handle;
                check(launches.Count == before, "PID reutilizado nao herda o atalho da Steam"); Hide();

                // PS de fechar visto pela Steam com um jogo dela na frente e a barra sem foco (16:21:46): ciclo antes de
                // fechar e foco devolvido a esse jogo, nao ao app da abertura.
                var neva = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(300), ProcessId = 30, ProcessCreated = 1, ClassName = "jogo", Title = "Neva" } };
                var other = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(400), ProcessId = 40, ProcessCreated = 1, ClassName = "app", Title = "App" } };
                gamesView.Activities.Remove(shortcut); gamesView.Activities.Add(TransitionFixture("steam:1:2420660", 30, 300, 2));
                observeWindow = delegate(IntPtr hwnd) { return hwnd == game.Window.Handle ? game : hwnd == neva.Window.Handle ? neva : hwnd == other.Window.Handle ? other : null; };
                var returnedTo = new List<IntPtr>();
                returnWindowFocus = delegate(DesktopWindow target) { returned++; returnedTo.Add(target.Handle); return true; };
                bool grant = true;
                takeForeground = delegate { if (grant) foreground = handle; return grant ? "concedido" : "negado"; };
                Action close = delegate { steamGuidePending = true; try { Toggle(); } finally { steamGuidePending = false; } };

                foreground = game.Window.Handle; open(); before = launches.Count; foreground = neva.Window.Handle; close();
                check(launches.Count == before + 2 && !window.IsVisible && returnedTo.Count == 1 && returnedTo[0] == neva.Window.Handle,
                    "PS de fechar com jogo da Steam na frente forca, libera e devolve o foco a esse jogo");
                steamInputRun = hold; returnedTo.Clear();
                foreground = game.Window.Handle; open(); foreground = neva.Window.Handle; close();
                check(steamHideDeferred && window.IsVisible && window.Opacity == 0 && returnedTo.Count == 0, "fechamento aguarda a liberacao sem devolver o foco antes");
                CompleteSteamInput(held()); held = null;
                check(!window.IsVisible && returnedTo.Count == 1 && returnedTo[0] == neva.Window.Handle, "liberacao devolve o foco ao jogo da frente");
                steamInputRun = inline; returnedTo.Clear();
                foreground = game.Window.Handle; open(); before = launches.Count; close();
                check(launches.Count == before && !window.IsVisible && returnedTo.Count == 1 && returnedTo[0] == game.Window.Handle,
                    "PS de fechar com a barra na frente nao repete o ciclo");
                returnedTo.Clear(); foreground = game.Window.Handle; open(); before = launches.Count; foreground = other.Window.Handle; close();
                check(launches.Count == before && !window.IsVisible && returnedTo.Count == 0, "app comum na frente fecha como antes, sem ciclo nem disputa de foco");
                returnedTo.Clear(); foreground = game.Window.Handle; open(); before = launches.Count;
                grant = false; focused = false; foreground = neva.Window.Handle; close();
                check(launches.Count == before && !window.IsVisible && returnedTo.Count == 0, "sem primeiro plano o fechamento nao forca nem devolve foco");
                grant = true; focused = true;

                Directory.CreateDirectory(Path.GetDirectoryName(steamInputMarker)); File.WriteAllText(steamInputMarker, "teste");
                before = count(SteamInput.Release); RecoverSteamInput();
                check(count(SteamInput.Release) == before + 1 && !File.Exists(steamInputMarker), "marcador de encerramento anterior libera ao iniciar");
                var recoveryLog = steamBarConsoleLog;
                steamBarConsoleLog = delegate { return delegate { return ""; }; };
                File.WriteAllText(steamInputMarker, "teste"); RecoverSteamInput();
                check(File.Exists(steamInputMarker), "recuperacao sem confirmacao preserva marcador");
                steamBarConsoleLog = delegate { throw new IOException("fixture"); };
                before = launches.Count; RecoverSteamInput();
                check(launches.Count == before && File.Exists(steamInputMarker), "recuperacao sem log novo nao anuncia sucesso nem apaga marcador");
                steamBarConsoleLog = recoveryLog;
                string normalMarker = steamInputMarker;
                string markerParent = Path.Combine(Output, "steam-input-marker-parent.fixture"); File.WriteAllText(markerParent, "arquivo");
                steamInputMarker = Path.Combine(markerParent, "diario.txt"); steamOwned = delegate { return true; };
                before = launches.Count; foreground = game.Window.Handle; open(); foreground = handle;
                check(steamGate && steamGateFailed && !steamInputPending && launches.Count == before, "diario indisponivel bloqueia antes de qualquer despacho");
                Hide(); steamInputMarker = normalMarker;
                running = false; File.WriteAllText(steamInputMarker, "teste"); before = launches.Count; RecoverSteamInput();
                check(launches.Count == before && !File.Exists(steamInputMarker), "sem a Steam aberta so descarta o marcador"); running = true;
                steamInputPending = true; before = count(SteamInput.Release); ReleaseSteamInputOnExit();
                check(count(SteamInput.Release) == before + 1 && steamInputCancelled, "encerrar durante o ciclo libera e cancela");
                check(SteamInputCycleBody().ForceSent == false, "ciclo cancelado nao forca");
                VerifySteamInputTransfer(check);
                File.WriteAllText(Path.Combine(Output, "steam-input-verificacao.txt"), "OK: " + checks + " verificacoes. Soltura do PS, geracoes de abertura, desktop e liberacao confirmados por logs novos, entrada de 250 ms, limite de 8 s sem liberar por timeout, reabertura durante ciclo, falhas/rotacao/linhas parciais, transporte restrito, fila compartilhada e recuperacao confirmada.\r\nProtocolo, logs da Steam, foco e relogio substituidos; nenhum protocolo real despachado.\r\n", Encoding.UTF8);
            }
            finally
            {
                controllerFocus = oldFocus; gamesView = oldGames; ConfigureSteamVerification(); Hide();
            }
        }
        // Manual, fora da bateria: despacha os protocolos reais. So no desktop e com um app comum na frente,
        // porque a Steam fica no layout de desktop ate a proxima troca de foco.
        public void VerifySteamInputLive()
        {
            var lines = new List<string>();
            IntPtr front = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(front, out pid);
            string owner = "";
            try { using (var process = System.Diagnostics.Process.GetProcessById((int)pid)) owner = process.ProcessName; } catch (Exception) { }
            bool? xbox = XGBP.Gaming.GamingExperience.Active();
            if (xbox != false) lines.Add("Recusado: modo Xbox ativo ou indisponivel (" + xbox + ").");
            else if (owner.StartsWith("steam", StringComparison.OrdinalIgnoreCase)) lines.Add("Recusado: a Steam esta em primeiro plano (" + owner + ").");
            else if (!SteamInput.Running()) lines.Add("Steam fechada: nada despachado.");
            else
            {
                steamBarInputLaunch = LaunchBarSteamInput; steamInputWait = System.Threading.Thread.Sleep; steamInputCancelled = false;
                steamBarConsoleLog = delegate { return new SteamInputLogCursor(SteamInput.ConsoleLog()).ReadNew; };
                steamBarContextLog = delegate { return new SteamInputLogCursor(SteamInput.ControllerLog()).ReadNew; };
                steamInputLog = delegate { return new SteamConsoleTail(SteamInput.ConsoleLog()).ReadNew; };
                var result = SteamInputCycleBody();
                lines.Add("Primeiro plano: " + owner + " (" + front.ToInt64() + ").");
                lines.Add("Desktop: enviado=" + result.ForceSent + "; confirmado=" + result.Forced + "; " + result.ForceMs + " ms.");
                lines.Add("Liberacao: enviada=" + result.ReleaseSent + "; confirmada=" + result.Released + "; " + result.ReleaseMs + " ms; repetida=" + result.Repeated + ".");
                lines.Add("console_log legivel=" + result.LogReadable + (result.Error != null ? "; erro: " + result.Error : "") + ".");
                if (!result.ForceSent || !result.ReleaseSent || !result.Forced || !result.Released) throw new Exception("Steam Input real: ciclo incompleto. " + string.Join(" ", lines.ToArray()));
            }
            File.WriteAllLines(Path.Combine(Output, "steam-input-real.txt"), lines.ToArray(), Encoding.UTF8);
        }
    }
}
