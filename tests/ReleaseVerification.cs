// Regressao de pre-lancamento: concorrencia real controlada, falhas e carga sintetica. Sem operar apps do usuario.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using XGBP.Games;
using XGBP.Media;
using XGBP.Runtime;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        public void VerifyReleaseReadiness()
        {
            var checks = new List<string>();
            Action<bool, string> check = (ok, why) => { if (!ok) throw new InvalidOperationException("Pre-lancamento: " + why); checks.Add("OK: " + why); };
            VerifyProcessIdentity(check);
            VerifyNativeReadLifecycle(check);
            VerifyProcessServiceFailures(check);
            VerifyRecoveryRetention(check);
            VerifyOperationShutdown(check);
            VerifyHistoryRetry(check);
            VerifyPauseJournal(check);
            VerifyMediaBounds(check);
            VerifyLogBounds(check);
            VerifyProcessIndex(check, checks);
            VerifyDiscoveryCache(check);
            VerifyServiceStress(check);
            VerifyIndexedEquivalence(check);
            VerifyMutexRejection(check);
            checks.Insert(0, "OK: " + checks.Count + " verificacoes/medicoes de pre-lancamento.");
            File.WriteAllLines(Path.Combine(Output, "pre-lancamento-verificacao.txt"), checks, Encoding.UTF8);
        }
        void VerifyProcessIdentity(Action<bool, string> check)
        {
            int sent = 0, closed = 0;
            var targets = new List<ProcessIdentity> { new ProcessIdentity(10, 100), new ProcessIdentity(20, 200) };
            var changed = GameControl.ApplyChecked(targets, ProcessAction.Kill, p => new IntPtr(p),
                h => 999, h => { sent++; return 0; }, h => closed++, () => 5);
            check(sent == 0 && closed == 1 && changed.Items[0].State == ProcessActionState.IdentityChanged &&
                changed.Items[1].State == ProcessActionState.NotAttempted, "PID reciclado nao recebe acao e bloqueia os descendentes");
            var partial = GameControl.ApplyChecked(targets, ProcessAction.Resume, p => new IntPtr(p),
                h => h.ToInt64() == 10 ? 100 : 200, h => h.ToInt64() == 10 ? 5 : 0, h => { }, () => 5);
            check(partial.Pending.Count == 1 && partial.Pending[0].Pid == 10 && partial.Applied.Count == 1, "retomada parcial identifica exatamente o membro pendente");
            RetainPendingPause("fixture:partial", partial);
            check(pausedByPreview["fixture:partial"].Members.Count == 1 && pausedByPreview["fixture:partial"].Members[10] == 100,
                "falha parcial conserva o registro para recuperacao");
            var all = GameControl.ApplyChecked(targets, ProcessAction.Resume, p => new IntPtr(p),
                h => h.ToInt64() == 10 ? 100 : 200, h => 0, h => { }, () => 0);
            RetainPendingPause("fixture:partial", all);
            check(!pausedByPreview.ContainsKey("fixture:partial"), "retomada completa libera o registro");
            sent = 0;
            var unknown = GameControl.ApplyChecked(new[] { new ProcessIdentity(10, 0) }, ProcessAction.Suspend,
                p => new IntPtr(p), h => 100, h => { sent++; return 0; }, h => { }, () => 5);
            check(sent == 0 && !unknown.RootApplied, "criacao desconhecida falha fechada antes de abrir o processo");
            var gone = GameControl.ApplyChecked(targets, ProcessAction.Resume, p => IntPtr.Zero,
                h => 0, h => { throw new Exception("nao deve chamar"); }, h => { }, () => 87);
            check(gone.Complete && gone.Pending.Count == 0, "processo encerrado nao vira pausa pendente de outro processo");
        }
        static void VerifyNativeReadLifecycle(Action<bool, string> check)
        {
            using (var own = Process.GetCurrentProcess())
            {
                var buffer = new StringBuilder(1);
                string image = GameProcess.ReadImagePath(own.Handle, ref buffer);
                check(GameFiles.Same(image, System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "consulta nativa cresce o buffer apos ERROR_INSUFFICIENT_BUFFER sem truncar o caminho");
                GameProcess.ReadImagePath(own.Handle, ref buffer);
                check(buffer.Capacity <= 512, "coleta seguinte volta ao buffer pequeno apos o fallback");
            }
            var watchers = new List<XGBP.Desktop.TextFocusWatcher>();
            for (int i = 0; i < 6; i++)
            {
                var watcher = new XGBP.Desktop.TextFocusWatcher(path => true); // Excluir tudo antes de consultar UIA.
                watchers.Add(watcher); watcher.Start(); watcher.Dispose();
            }
            var timeout = Stopwatch.StartNew();
            while (!watchers.TrueForAll(w => w.Stopped) && timeout.ElapsedMilliseconds < 3000) Thread.Sleep(10);
            check(watchers.TrueForAll(w => w.Stopped), "seis ciclos reais do observador encerram as threads; sem consulta UIA a apps");
        }
        static void VerifyOperationShutdown(Action<bool, string> check)
        {
            var coordinator = new GameOperationCoordinator();
            var registry = new PausedGameRegistry();
            Exception failure = null;
            using (var applied = new ManualResetEvent(false))
            using (var commit = new ManualResetEvent(false))
            using (var stopping = new ManualResetEvent(false))
            using (var recovered = new ManualResetEvent(false))
            {
                var activity = new GameActivity { Pid = 10 };
                var service = new GameProcessService(registry, a => new List<ProcessIdentity> { new ProcessIdentity(10, 100) },
                    delegate(IList<ProcessIdentity> targets, ProcessAction action) {
                        if (action == ProcessAction.Suspend)
                        {
                            applied.Set();
                            if (!commit.WaitOne(3000)) throw new TimeoutException("commit controlado");
                        }
                        else if (action == ProcessAction.Resume) recovered.Set();
                        var result = new ProcessActionResult();
                        foreach (var target in targets) result.Items.Add(new ProcessActionItem { Identity = target, State = ProcessActionState.Applied });
                        return result;
                    }, () => true);
                var work = new Thread(delegate() {
                    try { coordinator.Execute(() => service.Execute("game", activity, ProcessAction.Suspend)); }
                    catch (Exception ex) { failure = ex; }
                }) { IsBackground = true };
                var stop = new Thread(delegate() {
                    stopping.Set();
                    coordinator.Stop(delegate {
                        PausedGame entry;
                        if (registry.TryGetValue("game", out entry) && entry.Members[10] == 100) service.ResumeRegistered("game", entry);
                    });
                }) { IsBackground = true };
                work.Start();
                check(applied.WaitOne(2000), "worker alcanca o ponto apos a acao nativa");
                stop.Start();
                check(stopping.WaitOne(2000) && !recovered.WaitOne(20), "encerramento espera a transacao antes da recuperacao");
                commit.Set();
                check(work.Join(3000) && stop.Join(3000) && failure == null && recovered.WaitOne(0) && registry.Count == 0,
                    "pausa recuperada sem depender de callback da interface");
                bool denied = false;
                try { coordinator.Execute(() => { throw new Exception("acao depois de encerrar"); }); }
                catch (OperationCanceledException) { denied = true; }
                check(denied, "encerramento impede operacoes posteriores");
            }
            coordinator.Dispose();
        }
        static void VerifyProcessServiceFailures(Action<bool, string> check)
        {
            var registry = new PausedGameRegistry(); int native = 0;
            Func<IList<ProcessIdentity>, ProcessAction, ProcessActionResult> apply = delegate(IList<ProcessIdentity> targets, ProcessAction action) {
                native++;
                var result = new ProcessActionResult();
                foreach (var target in targets) result.Items.Add(new ProcessActionItem { Identity = target, State = ProcessActionState.Applied });
                return result;
            };
            Func<GameActivity, List<ProcessIdentity>> tree = a => new List<ProcessIdentity> { new ProcessIdentity(10, 100) };
            var blocked = new GameProcessService(registry, tree, apply, () => false);
            bool denied = false;
            try { blocked.Execute("game", new GameActivity { Pid = 10 }, ProcessAction.Suspend); } catch (IOException) { denied = true; }
            check(denied && native == 0 && registry.Count == 0, "diario indisponivel impede a suspensao antes de qualquer efeito");
            bool compensated = false;
            var failing = new GameProcessService(registry, tree, delegate(IList<ProcessIdentity> targets, ProcessAction action) {
                if (action == ProcessAction.Suspend) throw new IOException("falha depois do efeito parcial");
                compensated = true; return apply(targets, action);
            }, () => true);
            denied = false;
            try { failing.Execute("game", new GameActivity { Pid = 10 }, ProcessAction.Suspend); } catch (IOException) { denied = true; }
            check(denied && compensated && registry.Count == 0, "excecao apos efeito parcial executa compensacao e preserva o erro original");
            var normal = new GameProcessService(registry, tree, apply, () => true);
            normal.Execute("game", new GameActivity { Pid = 10 }, ProcessAction.Suspend);
            int before = native; denied = false;
            try { normal.Execute("game", new GameActivity { Pid = 10 }, ProcessAction.Suspend); }
            catch (InvalidOperationException) { denied = true; }
            check(denied && native == before && registry["game"].Members[10] == 100,
                "segunda pausa nao sobrescreve uma recuperacao pendente nem incrementa a suspensao");
            normal.ResumeRegistered("game", registry["game"]);
        }
        void VerifyRecoveryRetention(Action<bool, string> check)
        {
            const string key = "fixture:recovery-access";
            var original = gameProcessService; int mode = 0, applied = 0;
            try
            {
                gameProcessService = new GameProcessService(pausedByPreview,
                    a => new List<ProcessIdentity>(),
                    delegate(IList<ProcessIdentity> targets, ProcessAction action) {
                        if (action != ProcessAction.Resume) throw new InvalidOperationException("Somente recuperacao substituida.");
                        return GameControl.ApplyChecked(targets, action, p => mode == 0 ? IntPtr.Zero : new IntPtr(p),
                            h => mode == 1 ? 0 : h.ToInt64() == 900 ? 100 : 200,
                            h => { applied++; return 0; }, h => { }, () => 5);
                    }, () => true);
                pausedByPreview[key] = new PausedGame { Pid = 900, Members = new Dictionary<uint, long> { { 900, 100 }, { 901, 200 } } };
                ApplyPausedState(new GamesView());
                check(pausedByPreview.ContainsKey(key) && pausedByPreview[key].Members.Count == 2 && applied == 0,
                    "raiz ausente e acesso negado conservam todos os membros no adaptador real da UI");
                var activity = new GameActivity { Key = key, Pid = 900, SuspendedShare = 0, Pids = new List<uint> { 900 } };
                var view = new GamesView(); view.Activities.Add(activity); mode = 1;
                ApplyPausedState(view);
                check(pausedByPreview.ContainsKey(key) && activity.Paused && applied == 0,
                    "raiz retomada nao apaga filhos cuja identidade ficou temporariamente ilegivel");
                mode = 0; RecoverPausedRegistry();
                check(pausedByPreview.ContainsKey(key) && pausedByPreview[key].Members.Count == 2,
                    "recuperacao de encerramento conserva recusas sem filtrar pela consulta previa de PID");
                mode = 2; RecoverPausedRegistry();
                check(!pausedByPreview.ContainsKey(key) && applied == 2,
                    "recuperacao posterior confirma cada identidade e libera o registro completo");
            }
            finally { pausedByPreview.Remove(key); gameProcessService = original; }
        }
        void VerifyHistoryRetry(Action<bool, string> check)
        {
            string path = Path.Combine(Output, "release-fixtures", Guid.NewGuid().ToString("N"), "history.json");
            int attempts = 0;
            var history = new GameHistory(path, delegate(string file, GameHistoryData data) {
                if (++attempts == 1) throw new IOException("bloqueio temporario");
                GameFiles.Write(file, data);
            });
            DateTime now = DateTime.UtcNow;
            history.Observe("local:retry", now);
            check(history.Pending && history.Error != null && attempts == 1, "falha de escrita conserva a alteracao pendente");
            history.Flush(now.AddMilliseconds(500), false);
            check(attempts == 1, "backoff evita escrita repetida a cada tick");
            history.Flush(now.AddSeconds(2), false);
            check(!history.Pending && history.Error == null && new GameHistory(path).Last("local:retry") == GameFiles.Epoch(now),
                "retry persiste sem nova sessao ou mudanca de foco");
            history.Flush(now.AddMinutes(1), false);
            check(attempts == 2, "historico limpo nao gera escritas extras");
        }
        static void VerifyMediaBounds(Action<bool, string> check)
        {
            DateTime now = DateTime.UtcNow;
            var jobs = new List<Action>(); int sent = 0, failed = 0;
            using (var queue = new MediaCommandQueue(c => sent++, a => jobs.Add(a), a => a(), 2, TimeSpan.FromSeconds(5), () => now))
            {
                queue.Enqueue(new MediaCommand { Kind = "next" }, e => { if (e != null) failed++; });
                queue.Enqueue(new MediaCommand { Kind = "next" }, e => { if (e != null) failed++; });
                bool accepted = queue.Enqueue(new MediaCommand { Kind = "next" }, e => { if (e != null) failed++; });
                check(!accepted && queue.Count == 2 && failed == 1, "fila de midia recusa excesso com feedback");
                now = now.AddSeconds(6); jobs[0]();
                check(sent == 0 && failed == 3 && !queue.Pending, "comandos antigos expiram antes de chegar ao player");
            }
            int starts = 0, errors = 0; jobs.Clear(); sent = 0;
            using (var queue = new MediaCommandQueue(c => sent++, a => { if (++starts == 1) throw new IOException("scheduler"); jobs.Add(a); }, a => a()))
            {
                check(!queue.Enqueue(new MediaCommand { Kind = "next" }, e => { if (e != null) errors++; }) &&
                    !queue.Pending && errors == 1, "falha de agendamento nao deixa a fila presa");
                var command = new MediaCommand { Kind = "next" };
                queue.Enqueue(command, e => { }); command.Kind = "alterado"; jobs[0]();
                check(sent == 1 && !queue.Pending, "fila volta a aceitar gestos apos falha do scheduler");
            }
            jobs.Clear(); sent = 0;
            using (var queue = new MediaCommandQueue(c => sent++, a => jobs.Add(a), a => { throw new IOException("dispatcher"); }))
            {
                queue.Enqueue(new MediaCommand { Kind = "next" }, e => { });
                queue.Enqueue(new MediaCommand { Kind = "next" }, e => { }); jobs[0]();
                check(sent == 2 && queue.DeliveryFailures == 2 && !queue.Pending, "falha de entrega e contabilizada sem prender a drenagem");
            }
        }
        void VerifyPauseJournal(Action<bool, string> check)
        {
            string path = Path.Combine(Output, "release-fixtures", Guid.NewGuid().ToString("N"), "paused.json");
            var registry = new PausedGameRegistry();
            registry["fixture"] = new PausedGame { Pid = 10, Members = new Dictionary<uint, long> { { 10, 100 }, { 20, 200 } } };
            var store = new PauseRecoveryStore(path); store.Save(registry);
            var restored = new PausedGameRegistry(); new PauseRecoveryStore(path).Load(restored);
            check(restored.Count == 1 && restored["fixture"].Members[10] == 100 && restored["fixture"].Members[20] == 200,
                "diario conserva identidades completas entre execucoes");
            restored.Clear(); store.Save(restored);
            check(GameFiles.Read<PauseRecoveryData>(path).Processes.Count == 0, "recuperacao concluida limpa o diario atomicamente");
            File.WriteAllText(path, "corrompido");
            var broken = new PauseRecoveryStore(path); bool rejected = false;
            try { broken.Load(new PausedGameRegistry()); } catch (Exception) { rejected = true; }
            try { broken.Save(registry); rejected = false; } catch (InvalidDataException) { }
            check(rejected && File.ReadAllText(path) == "corrompido", "diario corrompido bloqueia novas pausas e permanece preservado");
        }
        void VerifyLogBounds(Action<bool, string> check)
        {
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var log = new BoundedLog(delegate { entered.Set(); release.WaitOne(3000); }, 4))
            {
                try
                {
                    log.Write("primeiro");
                    check(entered.WaitOne(2000), "writer de log executa fora do produtor");
                    var time = Stopwatch.StartNew();
                    for (int i = 0; i < 100; i++) log.Write("rajada " + i);
                    check(log.Pending <= 4 && log.Dropped > 0 && time.ElapsedMilliseconds < 500,
                        "I/O bloqueado mantem fila limitada e produtor responsivo");
                }
                finally { release.Set(); }
                check(log.Flush(2000), "log drena apos liberacao do disco");
            }
            string path = Path.Combine(Output, "release-fixtures", Guid.NewGuid().ToString("N"), "rotate.log");
            for (int i = 0; i < 8; i++) BoundedLog.AppendRotating(path, new string('x', 600), 1024, 3);
            check(File.Exists(path + ".3") && !File.Exists(path + ".4") && new FileInfo(path).Length < 1024,
                "rotacao retém tres arquivos e limita o arquivo ativo");
        }
        static void VerifyProcessIndex(Action<bool, string> check, List<string> report)
        {
            var games = new List<GameEntry>(); var processes = new List<GameProcess>();
            for (int i = 0; i < 3000; i++)
            {
                string root = @"C:\Fixture\Game" + i, exe = root + @"\Bin\Game.exe";
                games.Add(new GameEntry { Key = "fixture:" + i, Kind = "Game", InstallRoot = root, RuntimePaths = new[] { exe } });
                if (i % 2 == 0) processes.Add(new GameProcess { Pid = (uint)(i + 100), Created = i + 1, Path = exe });
            }
            var samples = new List<double>();
            for (int n = 0; n < 21; n++)
            {
                var clock = Stopwatch.StartNew();
                var index = new GameProcessIndex(games, processes);
                clock.Stop();
                if (n > 0) samples.Add(clock.Elapsed.TotalMilliseconds);
                if (n == 0)
                {
                    check(index.For("fixture:100").Count == 1 && index.For("fixture:101").Count == 0, "indice encontra somente o jogo da runtime");
                    check(index.Lookups <= processes.Count * 12, "consultas do indice crescem com processos e profundidade, sem produto jogos x processos");
                }
            }
            samples.Sort();
            double p95 = samples[(int)Math.Ceiling(samples.Count * .95) - 1];
            check(p95 < 100, "indice com 3000 jogos e 1500 processos: p95 abaixo de 100 ms");
            report.Add("MEDICAO: indice 3000 jogos / 1500 processos; 20 amostras aquecidas; p95=" + p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms. Carga sintetica; nao mede FPS ou FSE.");
            var ambiguous = games[0].Copy(); ambiguous.Key = "fixture:ambiguous"; games.Add(ambiguous);
            check(new GameProcessIndex(games, processes).For("fixture:0").Count == 0, "runtime ambigua nao e atribuida a dois jogos");
        }
        static void VerifyMutexRejection(Action<bool, string> check)
        {
            string id = Guid.NewGuid().ToString("N");
            var request = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,
                "--verify-activation-live --run-id " + id) { UseShellExecute = false, CreateNoWindow = true };
            using (var child = Process.Start(request))
            {
                try
                {
                    bool ended = child.WaitForExit(5000);
                    check(ended && child.ExitCode == 3, "verificacao concorrente retorna codigo 3, sem dialogo ou falso sucesso");
                    check(!File.Exists(Path.Combine(Output, "execucao-" + id + ".json")), "verificacao bloqueada nao emite recibo de aprovacao");
                }
                finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(); } }
            }
        }
        static void VerifyDiscoveryCache(Action<bool, string> check)
        {
            DateTime now = DateTime.UtcNow; int scans = 0;
            var cache = new RuntimeDiscoveryCache(() => now);
            Func<string[]> scan = () => { scans++; return new[] { "game.exe" }; };
            var first = cache.Get("root", "v1", scan); first[0] = "alterado";
            check(cache.Get("root", "v1", scan)[0] == "game.exe" && scans == 1, "cache evita varredura repetida e nao expoe seu array interno");
            cache.Get("root", "v2", scan);
            check(scans == 2, "mudanca de manifesto invalida a descoberta imediatamente");
            now = now.AddMinutes(6); cache.Get("root", "v2", scan);
            check(scans == 3, "TTL renova descoberta de instalacoes externas");
            check(!GameFiles.Helper("CrashRacing.exe") && GameFiles.Helper("CrashReporter.exe"), "titulo com crash no nome nao vira auxiliar de diagnostico");
        }
        static void VerifyServiceStress(Action<bool, string> check)
        {
            var registry = new PausedGameRegistry();
            var activity = new GameActivity { Pid = 10 };
            int calls = 0;
            var service = new GameProcessService(registry, a => new List<ProcessIdentity> { new ProcessIdentity(10, 100) },
                delegate(IList<ProcessIdentity> targets, ProcessAction action) {
                    calls++;
                    var result = new ProcessActionResult();
                    foreach (var target in targets) result.Items.Add(new ProcessActionItem { Identity = target, State = ProcessActionState.Applied });
                    return result;
                }, () => true);
            long before = GC.GetTotalMemory(true);
            for (int i = 0; i < 10000; i++)
            {
                service.Execute("game", activity, ProcessAction.Suspend);
                service.Execute("game", activity, ProcessAction.Resume);
            }
            long retained = GC.GetTotalMemory(true) - before;
            check(calls == 20000 && registry.Count == 0, "10000 ciclos do mesmo servico de producao sem pausa orfa; backend substituido");
            check(retained < 8 * 1024 * 1024, "carga de servico nao retém 8 MB apos coleta; nao substitui teste residente prolongado");
        }
        static void VerifyIndexedEquivalence(Action<bool, string> check)
        {
            var random = new Random(53056);
            for (int round = 0; round < 100; round++)
            {
                var games = new List<GameEntry>();
                var processes = new List<GameProcess>();
                for (int g = 0; g < 12; g++)
                {
                    string root = @"C:\Fixture\Group" + random.Next(8), file = root + @"\Bin\App" + random.Next(6) + ".exe";
                    games.Add(new GameEntry { Key = "g" + g, Kind = "Game", InstallRoot = root, RuntimePaths = new[] { file } });
                }
                for (int p = 0; p < 20; p++)
                {
                    string file = @"C:\Fixture\Group" + random.Next(10) + @"\Bin\App" + random.Next(6) + ".exe";
                    processes.Add(new GameProcess { Pid = (uint)(p + 1), Created = p + 1, Path = p % 4 == 0 ? null : file, Name = Path.GetFileName(file) });
                }
                var index = new GameProcessIndex(games, processes);
                foreach (var process in processes)
                {
                    var owners = games.FindAll(g => process.Path == null
                        ? Array.Exists(g.RuntimePaths, r => string.Equals(Path.GetFileName(r), process.Name, StringComparison.OrdinalIgnoreCase))
                        : Array.Exists(g.RuntimePaths, r => string.Equals(r, process.Full, StringComparison.OrdinalIgnoreCase)) ||
                            process.Full.StartsWith(g.InstallRoot + "\\", StringComparison.OrdinalIgnoreCase) && !GameFiles.Helper(process.Full));
                    foreach (var game in games)
                        if (index.For(game.Key).Contains(process) != (owners.Count == 1 && owners[0].Key == game.Key))
                            throw new InvalidOperationException("Indice divergiu da regra de associacao na rodada " + round);
                }
            }
            check(true, "indice equivale ao oraculo por varredura em 100 cenarios de caminhos, nomes e ambiguidades");
        }
    }
}
