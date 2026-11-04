// Fixtures puras de transferencia; arquivos de log sinteticos somente dentro da pasta de teste.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Games;

namespace XGBP.Design
{
    sealed class SteamTransferFixture
    {
        public const string Desktop = "OnFocusWindowChanged URL Forcing to window type: k_EWindowTypeGame, 413080\n";
        public readonly StringBuilder Console = new StringBuilder(), Context = new StringBuilder();
        public readonly List<string> Sent = new List<string>();
        public readonly SteamInputAttempt Attempt = new SteamInputAttempt();
        public Action<string> OnSend;
        public Action<int> OnWait = delegate { };
        public Func<Func<string>> ConsoleFactory, ContextFactory;
        public int Waited;
        public bool Running = true;
        public SteamTransferFixture()
        {
            ConsoleFactory = delegate { int start = Console.Length; return delegate { return Console.ToString().Substring(start); }; };
            ContextFactory = delegate { int start = Context.Length; return delegate { return Context.ToString().Substring(start); }; };
            OnSend = Acknowledge;
        }
        public void Acknowledge(string uri)
        {
            Console.AppendLine(SteamInput.Executed(uri));
            if (uri == SteamInput.ForceDesktop) Context.Append(Desktop);
        }
        public SteamInputCycle Run()
        {
            return SteamInputTransfer.Run(Attempt, new object(), delegate(string uri) { Sent.Add(uri); OnSend(uri); },
                ConsoleFactory, ContextFactory, delegate(int ms) { Waited += ms; OnWait(ms); }, delegate { return Running; });
        }
    }
    sealed partial class Preview
    {
        void VerifySteamInputTransfer(Action<bool, string> check)
        {
            var f = new SteamTransferFixture(); var result = f.Run();
            check(result.Ready && result.CanForget && f.Sent.Count == 2, "transferencia exige desktop e liberacao em ordem");
            f = new SteamTransferFixture(); f.Attempt.GuideHeld = true;
            f.OnWait = delegate { if (f.Waited >= 75) f.Attempt.GuideHeld = false; };
            f.OnSend = delegate(string uri) { check(!f.Attempt.GuideHeld && f.Waited >= 75, "nenhum protocolo antes da ultima soltura"); f.Acknowledge(uri); };
            check(f.Run().Ready, "soltura permite ciclo sem atraso artificial");
            f = new SteamTransferFixture(); f.Attempt.GuideHeld = true;
            f.OnWait = delegate { f.Attempt.Cancelled = true; };
            result = f.Run(); check(!result.Ready && result.CanForget && f.Sent.Count == 0, "dispositivo perdido na espera cancela sem forcar");
            f = new SteamTransferFixture(); f.Attempt.GuideHeld = true;
            result = f.Run(); check(!result.Ready && f.Sent.Count == 0, "PS nunca solto termina bloqueado sem forcar");
            f = new SteamTransferFixture(); f.Attempt.Cancelled = true;
            check(!f.Run().Ready && f.Sent.Count == 0, "geracao cancelada nao despacha");
            f.Attempt.UnresolvedBefore = true;
            check(!f.Run().CanForget, "cancelamento sem despacho preserva diario de uma transferencia anterior incerta");
            f = new SteamTransferFixture(); f.Attempt.UnresolvedBefore = true;
            check(f.Run().CanForget, "novo ciclo confirmado resolve diario anterior");
            f = new SteamTransferFixture(); f.Running = false;
            check(!f.Run().Ready && f.Sent.Count == 0, "Steam encerrada na fila nao e iniciada pelo protocolo");
            f = new SteamTransferFixture(); f.Attempt.GuideHeld = true;
            f.OnWait = delegate { f.Running = false; f.Attempt.GuideHeld = false; };
            check(!f.Run().Ready && f.Sent.Count == 0, "Steam encerrada enquanto PS apertado nao despacha");
            f = new SteamTransferFixture(); f.OnSend = delegate { throw new SteamNavigationBusyException(); };
            f.OnWait = delegate { f.Running = false; };
            result = f.Run(); check(!result.Ready && result.CanForget && f.Sent.Count == 1, "Steam encerrada durante retry ocupado nao repete");
            f = new SteamTransferFixture(); bool focused = true; f.Attempt.Focused = delegate { return focused; };
            f.OnSend = delegate(string uri) { f.Acknowledge(uri); if (uri == SteamInput.ForceDesktop) focused = false; };
            result = f.Run(); check(!result.Ready && result.Released && result.CanForget, "perder foco limpa mas nao libera navegacao");
            f = new SteamTransferFixture(); f.OnSend = delegate(string uri) { f.Acknowledge(uri); if (uri == SteamInput.ForceDesktop) f.Attempt.GuideHeld = true; };
            result = f.Run(); check(!result.Ready && result.Released, "novo PS durante ciclo nao recebe confirmacao antiga");
            f = new SteamTransferFixture(); f.OnSend = delegate(string uri) { f.Console.AppendLine(SteamInput.Executed(uri)); };
            check(!f.Run().Ready, "receber protocolo sem contexto nao libera");
            f = new SteamTransferFixture(); f.Console.AppendLine(SteamInput.Executed(SteamInput.ForceDesktop)); f.Context.Append(SteamTransferFixture.Desktop);
            f.OnSend = delegate(string uri) { if (uri == SteamInput.Release) f.Acknowledge(uri); };
            check(!f.Run().Ready, "console e contexto anteriores ao cursor nao confirmam");
            foreach (string suffix in new[] {
                "Loaded Config for App ID 413080\n",
                SteamTransferFixture.Desktop + "OnFocusWindowChanged to window type: guide, AppID 443510\n",
                SteamTransferFixture.Desktop + "Loaded Config for App ID 769\n",
                SteamTransferFixture.Desktop + "OnFocusWindowChanged to window type: client, AppID 769",
                SteamTransferFixture.Desktop + "OnFocusWindowChanged to game window type: AppID 2420660\n" })
            {
                f = new SteamTransferFixture(); string selected = suffix;
                f.OnSend = delegate(string uri) { f.Console.AppendLine(SteamInput.Executed(uri)); if (uri == SteamInput.ForceDesktop) f.Context.Append(selected); };
                check(!f.Run().Ready, "carga sem foco, Guia, contexto divergente ou linha parcial nao confirmam desktop");
            }
            f = new SteamTransferFixture(); f.OnSend = delegate(string uri) {
                f.Acknowledge(uri); if (uri == SteamInput.Release) f.Context.Append("OnFocusWindowChanged to window type: client, AppID 769\n"); };
            check(!f.Run().Ready, "contexto que muda durante /0 conserva bloqueio");
            f = new SteamTransferFixture(); int busy = 2;
            f.OnSend = delegate(string uri) { if (uri == SteamInput.ForceDesktop && busy-- > 0) throw new SteamNavigationBusyException(); f.Acknowledge(uri); };
            result = f.Run(); check(result.Ready && f.Sent.Count == 4, "canal ocupado antes de escrita aceita retry limitado");
            f = new SteamTransferFixture(); f.OnSend = delegate { throw new SteamNavigationBusyException(); };
            result = f.Run(); check(!result.Ready && result.CanForget && !result.ReleaseSent, "canal sempre ocupado nao cria limpeza nem sucesso");
            f = new SteamTransferFixture(); int forceCount = 0;
            f.OnSend = delegate(string uri) { if (uri == SteamInput.ForceDesktop) { forceCount++; throw new SteamDeliveryUncertainException(new IOException("fixture")); } f.Acknowledge(uri); };
            result = f.Run(); check(forceCount == 1 && !result.Ready && !result.CanForget && result.Released, "entrega incerta nunca repete force e preserva diario");
            f = new SteamTransferFixture(); f.OnSend = delegate(string uri) { if (uri == SteamInput.Release) throw new IOException("fixture"); f.Acknowledge(uri); };
            result = f.Run(); check(!result.Ready && !result.CanForget, "falha de release nao remove diario");
            f = new SteamTransferFixture(); f.ContextFactory = delegate { throw new IOException("fixture"); };
            result = f.Run(); check(!result.Ready && result.CanForget && f.Sent.Count == 0, "log indisponivel antes do despacho nao forca");
            f = new SteamTransferFixture(); f.ContextFactory = delegate { return delegate { throw new IOException("rotacionado"); }; };
            result = f.Run(); check(!result.Ready && result.Released, "rotacao durante ciclo bloqueia e limpa");
            foreach (string uri in new[] { SteamInput.ForceDesktop, SteamInput.Release }) check(SteamFastNavigation.InputSupported(uri), "URI exata do ciclo permitida");
            foreach (string uri in new[] { SteamInput.Force("769"), SteamInput.Force("2420660"), "steam://open/library", null, SteamInput.Release + "\" -exit" })
                check(!SteamFastNavigation.InputSupported(uri), "outros comandos nao entram no transporte da barra");
            VerifySteamInputCursor(check);
            VerifyGuideDesktopRecovery(check);
            Action drain = null; var fifo = new SteamInputWorkQueue(delegate(Action run) { drain = run; });
            var order = new List<string>();
            fifo.Enqueue(delegate { order.Add("teclado-forca"); }); fifo.Enqueue(delegate { order.Add("teclado-libera"); }); fifo.Enqueue(delegate { order.Add("barra-forca-libera"); });
            drain(); check(string.Join(",", order) == "teclado-forca,teclado-libera,barra-forca-libera", "fila compartilha ordem do teclado e barra sem aguardar UI");
            fifo.Enqueue(delegate { throw new IOException("fixture"); }); fifo.Enqueue(delegate { order.Add("limpeza"); }); drain();
            check(order[order.Count - 1] == "limpeza", "erro anterior nao abandona limpeza enfileirada");
            int accepted = 0; for (int i = 0; i < 100; i++) if (fifo.Enqueue(delegate { })) accepted++;
            check(accepted == 16, "fila limitada recusa excesso sem executar fora de ordem"); drain();
        }
        void VerifyGuideDesktopRecovery(Action<bool, string> check)
        {
            const string guide = "Loaded Config for Local Override Path for App ID 443510, Controller 0: empty.vdf\n";
            const string desktop = "Loaded Config for Local Override Path for App ID 413080, Controller 0: empty.vdf\n";
            // Mesmas ordens dos cinco alertas no Genshin em 29/10, 10:21:58 a 10:22:48.
            foreach (bool repeatedFocus in new[] { false, true })
            {
                var f = new SteamTransferFixture();
                f.OnSend = delegate(string uri) {
                    f.Acknowledge(uri);
                    if (uri == SteamInput.Release) f.Context.Append((repeatedFocus ? SteamTransferFixture.Desktop : "") + guide + desktop);
                };
                check(f.Run().Ready && f.Waited == 0, "Guia seguido de desktop no mesmo ciclo confirma sem espera artificial");
            }
            var delayed = new SteamTransferFixture();
            delayed.OnSend = delegate(string uri) { delayed.Acknowledge(uri); if (uri == SteamInput.Release) delayed.Context.Append(guide); };
            delayed.OnWait = delegate { if (delayed.Waited == 75) delayed.Context.Append(desktop); };
            check(delayed.Run().Ready && delayed.Waited == 75, "desktop posterior a /0 e aceito assim que completa a transicao");
            var lost = new SteamTransferFixture();
            lost.OnSend = delegate(string uri) { lost.Acknowledge(uri); if (uri == SteamInput.Release) lost.Context.Append(guide); };
            check(!lost.Run().Ready && lost.Waited == 500, "Guia sem retorno confirmado nao libera no prazo");
            delayed = new SteamTransferFixture();
            delayed.OnSend = delegate(string uri) { delayed.Acknowledge(uri); if (uri == SteamInput.Release) delayed.Context.Append(guide); };
            delayed.OnWait = delegate { delayed.Attempt.Cancelled = true; delayed.Context.Append(desktop); };
            check(!delayed.Run().Ready, "cancelamento durante reconciliacao nao herda desktop posterior");
            foreach (string prefix in new[] { "", "OnFocusWindowChanged to game window type: AppID 3899276376\n",
                SteamTransferFixture.Desktop + "OnFocusWindowChanged to window type: client, AppID 769\n",
                SteamTransferFixture.Desktop + "Loaded Config for App ID 769\n" })
                check(SteamInputObservation.LastContext(prefix + guide + desktop) != SteamInput.DesktopAppId,
                    "carga desktop nao estabelece foco nem apaga mudanca real de contexto");
        }
        void VerifySteamInputCursor(Action<bool, string> check)
        {
            string folder = Path.Combine(Output, "steam-input-cursor-fixture"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "controller.log"); var utf8 = new UTF8Encoding(false);
            File.WriteAllText(path, SteamTransferFixture.Desktop, utf8); var cursor = new SteamInputLogCursor(path);
            check(cursor.ReadNew() == "", "cursor ignora historico");
            File.AppendAllText(path, SteamTransferFixture.Desktop, utf8);
            check(SteamInputObservation.LastContext(cursor.ReadNew()) == SteamInput.DesktopAppId, "cursor aceita linha nova completa");
            File.AppendAllText(path, "OnFocusWindowChanged to window type: client, AppID 7", utf8);
            check(cursor.ReadNew() == "", "linha parcial posterior invalida desktop completo anterior");
            File.AppendAllText(path, "69\n", utf8);
            check(SteamInputObservation.LastContext(cursor.ReadNew()) == "769", "linha completada confirma divergencia");
            File.WriteAllText(path, "reescrito\n", utf8); bool rejected = false;
            try { cursor.ReadNew(); } catch (IOException) { rejected = true; }
            check(rejected, "truncamento invalida cursor");
            File.WriteAllText(path, "parcial antigo", utf8); cursor = new SteamInputLogCursor(path);
            File.AppendAllText(path, " terminou\n" + SteamTransferFixture.Desktop, utf8);
            check(cursor.ReadNew() == SteamTransferFixture.Desktop, "linha antiga parcial nao e atribuida ao ciclo");
            string moved = path + ".old"; if (File.Exists(moved)) File.Delete(moved);
            File.Move(path, moved); File.WriteAllText(path, File.ReadAllText(moved), utf8); rejected = false;
            try { cursor.ReadNew(); } catch (IOException) { rejected = true; }
            check(rejected, "substituicao com mesmo tamanho invalida identidade do arquivo");
        }
    }
}
