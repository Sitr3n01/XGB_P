// Transferencia limitada da entrada Steam. Efeitos e observacoes sao substituiveis nos testes.
using System;
using System.Diagnostics;
using System.IO;
using XGBP.Games;

namespace XGBP.Design
{
    sealed class SteamInputAttempt
    {
        public long Epoch;
        public bool UnresolvedBefore;
        public volatile bool GuideHeld, Cancelled, DispatchStarted;
        public Func<bool> Focused;
        public bool Allowed() { return !Cancelled && !GuideHeld && (Focused == null || Focused()); }
    }
    static class SteamInputTransfer
    {
        const int TimeoutMs = 2500, PollMs = 25;
        internal static SteamInputCycle Run(SteamInputAttempt attempt, object dispatchLock, Action<string> send,
            Func<Func<string>> consoleFactory, Func<Func<string>> contextFactory, Action<int> wait, Func<bool> available)
        {
            var result = new SteamInputCycle { Attempt = attempt };
            Func<string> console = null, context = null;
            bool mayHaveForced = false;
            try
            {
                int elapsed = 0;
                while (attempt.GuideHeld)
                {
                    if (attempt.Cancelled || attempt.Focused != null && !attempt.Focused()) throw new OperationCanceledException("Abertura cancelada antes da soltura do PS.");
                    if (elapsed >= TimeoutMs) throw new TimeoutException("O PS nao foi solto a tempo.");
                    wait(PollMs); elapsed += PollMs;
                }
                if (!attempt.Allowed()) throw new OperationCanceledException("Abertura sem primeiro plano.");
                console = consoleFactory(); context = contextFactory();
                var clock = Stopwatch.StartNew();
                Send(attempt, dispatchLock, send, SteamInput.ForceDesktop, wait, available, true, ref mayHaveForced);
                result.ForceSent = true;
                for (elapsed = 0; ; elapsed += PollMs)
                {
                    if (!attempt.Allowed()) throw new OperationCanceledException("Abertura cancelada durante a confirmacao.");
                    string received = console(), selected = context();
                    if (received == null || selected == null) throw new IOException("Logs da Steam indisponiveis.");
                    result.LogReadable = true;
                    result.Forced = SteamInputObservation.Executed(received, SteamInput.ForceDesktop);
                    result.DesktopConfirmed = SteamInputObservation.LastContext(selected) == SteamInput.DesktopAppId;
                    if (result.Forced && result.DesktopConfirmed) break;
                    if (elapsed >= TimeoutMs) throw new TimeoutException("A Steam nao confirmou o contexto de desktop.");
                    wait(PollMs);
                }
                result.ForceMs = (int)clock.ElapsedMilliseconds;
            }
            catch (Exception ex) { result.Error = ex.Message; result.Cancelled = ex is OperationCanceledException; }
            // Mesmo uma entrega incerta pode ter forçado o desktop. Sempre tentar desfazer,
            // mas preservar o diario se a ordem nao puder ser confirmada.
            if (mayHaveForced)
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    if (!available()) throw new IOException("A Steam encerrou antes da liberacao.");
                    Func<string> releaseLog = null;
                    try { releaseLog = consoleFactory(); } catch (Exception ex) { result.Error = ex.Message; }
                    bool ignored = false;
                    Send(attempt, dispatchLock, send, SteamInput.Release, wait, available, false, ref ignored); result.ReleaseSent = true;
                    if (releaseLog == null) throw new IOException("A liberacao nao pode ser confirmada sem um log novo.");
                    for (int elapsed = 0; ; elapsed += PollMs)
                    {
                        string received = releaseLog();
                        if (received == null) throw new IOException("Log da liberacao indisponivel.");
                        if (SteamInputObservation.Executed(received, SteamInput.Release)) { result.Released = true; break; }
                        if (elapsed >= TimeoutMs) throw new TimeoutException("A Steam nao confirmou a liberacao.");
                        wait(PollMs);
                    }
                    string final = console == null ? null : console();
                    result.Forced = result.Forced || SteamInputObservation.Executed(final, SteamInput.ForceDesktop);
                    // Com execucao atrasada apos /0, repetir somente a limpeza, nunca a forçada.
                    if (final != null && final.LastIndexOf(SteamInput.Executed(SteamInput.ForceDesktop), StringComparison.Ordinal) >
                        final.LastIndexOf(SteamInput.Executed(SteamInput.Release), StringComparison.Ordinal))
                    {
                        releaseLog = consoleFactory(); result.Released = false; result.Repeated = true;
                        Send(attempt, dispatchLock, send, SteamInput.Release, wait, available, false, ref ignored);
                        for (int elapsed = 0; ; elapsed += PollMs)
                        {
                            if (SteamInputObservation.Executed(releaseLog(), SteamInput.Release)) { result.Released = true; break; }
                            if (elapsed >= TimeoutMs) throw new TimeoutException("A Steam nao confirmou a limpeza repetida.");
                            wait(PollMs);
                        }
                    }
                    // /0 pode ser confirmado enquanto o log ainda conclui Guia -> desktop.
                    // Sem espera fixa: sair na primeira confirmacao atual; nunca liberar pelo prazo.
                    if (result.Error == null && result.Forced && result.DesktopConfirmed && result.Released)
                        for (int elapsed = 0; ; elapsed += PollMs)
                        {
                            if (!attempt.Allowed() || !available()) break;
                            string selected = SteamInputObservation.LastContext(context());
                            if (selected == SteamInput.DesktopAppId) { result.Ready = true; break; }
                            if (selected != null && selected != SteamInput.GuideAppId || elapsed >= 500) break;
                            wait(PollMs);
                        }
                    if (!result.Ready && result.Error == null) result.Error = "O contexto mudou antes de liberar a navegacao.";
                }
                catch (Exception ex) { result.Error = ex.Message; }
                result.ReleaseMs = (int)clock.ElapsedMilliseconds;
            }
            result.CanForget = !mayHaveForced && !attempt.UnresolvedBefore || result.Forced && result.Released;
            return result;
        }
        static void Send(SteamInputAttempt attempt, object sync, Action<string> send, string uri, Action<int> wait, Func<bool> available,
            bool force, ref bool mayHaveForced)
        {
            for (int elapsed = 0; ; elapsed += PollMs)
            {
                try
                {
                    lock (sync)
                    {
                        if (force && !attempt.Allowed()) throw new OperationCanceledException("Abertura cancelada antes do despacho.");
                        if (!available()) throw new IOException("A Steam encerrou antes do despacho.");
                        if (force) { attempt.DispatchStarted = true; mayHaveForced = true; }
                        send(uri); return;
                    }
                }
                catch (SteamNavigationBusyException)
                {
                    // Busy garante que nenhuma escrita foi tentada. Outros erros nunca repetem a forçada.
                    if (force) mayHaveForced = false;
                    if (elapsed >= TimeoutMs) throw;
                    wait(PollMs);
                }
            }
        }
    }
}
