// Sessoes sinteticas e JSON isolado em bin; sem iniciar/consultar jogos ou alterar historico real.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyGamePersistence()
        {
            string folder = Path.Combine(Output, "games-persistence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool value, string text) { GameAssert(value, text); checks.Add("OK: " + text); };
            Func<string, GameHistory> history = name => new GameHistory(Path.Combine(folder, name + ".json"));
            string exe = GameFiles.Normalize(Path.Combine(folder, "GenshinImpact.exe")), key = "local:runtime";
            var entry = new GameEntry { Key = key, Name = "Jogo de teste", Kind = "Game", Available = true, RuntimePaths = new[] { exe }, InstallRoot = folder };
            var catalog = new GameCatalogState(); catalog.Entries.Add(entry);
            var windows = new List<DesktopWindow>(); var empty = new List<GameProcess>();
            Func<long, List<GameProcess>> processes = created => new List<GameProcess> { new GameProcess { Pid = 701, Created = created, Path = exe } };
            DateTime now = DateTime.UtcNow.AddMinutes(-30);
            var store = history("background"); var tracker = new GameSessions(store);
            var view = tracker.Update(catalog, empty, windows, 0, now);
            check(store.Last(key) == 0 && view.Recent().Count == 0, "cadastro sozinho nao inventa ultimo uso");
            view = tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(2));
            check(!view.Current.Confirmed && !File.Exists(Path.Combine(folder, "background.json")), "uma leitura da runtime nao persiste");
            view = tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(4));
            long observed = store.Last(key);
            check(view.Current.Confirmed && observed == GameFiles.Epoch(now.AddSeconds(4)) && view.Current.LastFocused == 0,
                "duas leituras da mesma runtime persistem sem janela ou foco");
            check(new GameHistory(Path.Combine(folder, "background.json")).Last(key) == observed, "confirmacao sem foco chega ao disco");
            view = tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(6));
            check(store.Last(key) == observed && !File.Exists(Path.Combine(folder, "background.json.bak")), "polling continuo nao regrava o historico");
            tracker.Update(catalog, processes(100), windows, 701, now.AddSeconds(8));
            check(store.Last(key) == observed, "um foco curto nao renova a data");
            tracker.Update(catalog, processes(100), windows, 701, now.AddSeconds(10));
            check(store.Last(key) > observed, "foco confirmado depois da deteccao em segundo plano renova o uso");
            observed = store.Last(key);
            tracker.Update(catalog, processes(100), windows, 701, now.AddSeconds(12));
            check(store.Last(key) == observed, "manter foco nao regrava a cada leitura");
            view = tracker.Update(catalog, empty, windows, 0, now.AddSeconds(14));
            check(view.Activities.Count == 0 && view.Featured != null && view.Featured.Key == key && view.Recent().Count == 0, "fechar conserva ultimo jogo no destaque sem duplicar nos recentes");
            view = new GameSessions(history("background")).Update(catalog, empty, windows, 0, now.AddSeconds(16));
            check(view.Featured != null && view.Featured.Key == key && view.Featured.LastPlayed == observed && view.Recent().Count == 0, "reiniciar conserva ultimo uso no destaque sem duplicacao");

            store = history("short"); tracker = new GameSessions(store);
            tracker.Update(catalog, processes(100), windows, 0, now);
            tracker.Update(catalog, empty, windows, 0, now.AddSeconds(2));
            check(store.Last(key) == 0, "processo que some apos uma leitura nao gera historico");
            view = tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(4));
            check(!view.Current.Confirmed && store.Last(key) == 0, "ausencia quebra sequencia de confirmacao");
            tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(6));
            check(store.Last(key) > 0, "duas novas leituras depois da ausencia confirmam");

            store = history("gap"); tracker = new GameSessions(store);
            tracker.Update(catalog, processes(100), windows, 0, now);
            tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(20));
            check(store.Last(key) == 0, "salto de tempo nao conta como duas leituras consecutivas");
            tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(22));
            check(store.Last(key) > 0, "retomada do polling confirma com duas leituras novas");

            store = history("identity"); tracker = new GameSessions(store);
            tracker.Update(catalog, processes(100), windows, 0, now);
            tracker.Update(catalog, processes(200), windows, 0, now.AddSeconds(2));
            check(store.Last(key) == 0, "PID reutilizado com outra criacao nao soma leituras");
            tracker.Update(catalog, processes(200), windows, 0, now.AddSeconds(4));
            check(store.Last(key) > 0, "nova instancia estavel confirma em separado");

            var named = new List<GameProcess> { new GameProcess { Pid = 702, Name = "GenshinImpact.exe" } };
            store = history("name"); tracker = new GameSessions(store);
            tracker.Update(catalog, named, windows, 0, now); view = tracker.Update(catalog, named, windows, 0, now.AddSeconds(2));
            check(view.Current != null && !view.Current.Confirmed && store.Last(key) == 0, "nome sozinho detecta aberto mas nao persiste sem foco");
            tracker.Update(catalog, named, windows, 702, now.AddSeconds(4)); tracker.Update(catalog, named, windows, 702, now.AddSeconds(6));
            check(store.Last(key) > 0, "fallback por nome conserva confirmacao por foco");

            store = history("unknown-creation"); tracker = new GameSessions(store);
            tracker.Update(catalog, processes(0), windows, 0, now); tracker.Update(catalog, processes(0), windows, 0, now.AddSeconds(2));
            check(store.Last(key) == 0, "caminho sem criacao conhecida nao confirma em segundo plano");

            store = history("folder"); tracker = new GameSessions(store);
            var engine = new List<GameProcess> { new GameProcess { Pid = 703, Created = 100, Path = Path.Combine(folder, "Engine.exe") } };
            tracker.Update(catalog, engine, windows, 0, now); view = tracker.Update(catalog, engine, windows, 0, now.AddSeconds(2));
            check(view.Current != null && store.Last(key) == 0, "outro executavel da pasta nao basta para persistir sem foco");

            foreach (string name in new[] { "wscript.exe", "cmd.exe", "CrashReporter.exe" })
            {
                var blocked = new GameCatalogState(); var copy = entry.Copy(); copy.RuntimePaths = new[] { GameFiles.Normalize(Path.Combine(folder, name)) }; blocked.Entries.Add(copy);
                var auxiliary = new List<GameProcess> { new GameProcess { Pid = 704, Created = 100, Path = copy.RuntimePaths[0] } };
                store = history(name); tracker = new GameSessions(store);
                tracker.Update(blocked, auxiliary, windows, 0, now); tracker.Update(blocked, auxiliary, windows, 0, now.AddSeconds(2));
                check(store.Last(key) == 0, name + " nao cria historico em segundo plano mesmo se mapeado");
            }
            var ambiguous = new GameCatalogState(); ambiguous.Entries.Add(entry.Copy()); var other = entry.Copy(); other.Key = "local:other"; ambiguous.Entries.Add(other);
            store = history("ambiguous"); tracker = new GameSessions(store);
            tracker.Update(ambiguous, processes(100), windows, 0, now); view = tracker.Update(ambiguous, processes(100), windows, 0, now.AddSeconds(2));
            check(view.Activities.Count == 0 && store.Last(key) == 0 && store.Last(other.Key) == 0, "runtime de dois jogos nao grava identidade ambigua");

            string corrupt = Path.Combine(folder, "corrupt.json"); File.WriteAllText(corrupt, "incompleto");
            store = new GameHistory(corrupt); tracker = new GameSessions(store);
            tracker.Update(catalog, processes(100), windows, 0, now); tracker.Update(catalog, processes(100), windows, 0, now.AddSeconds(2));
            check(store.Error != null && File.ReadAllText(corrupt) == "incompleto", "novo fluxo preserva historico corrompido sem sobrescrever");
            checks.Insert(0, "OK: " + checks.Count + " verificacoes de persistencia. Processos sinteticos, datas de teste e JSON isolado em bin; nenhum jogo real iniciado ou historico real alterado.");
            File.WriteAllLines(Path.Combine(Output, "jogos-persistencia-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}
