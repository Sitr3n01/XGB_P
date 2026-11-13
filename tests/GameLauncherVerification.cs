// ADR 0089: atalho externo cujo alvo e um lancador (unlocker de FPS, wrapper proprio) que abre o jogo como filho.
// Processos, janelas e Steam sinteticos em bin; nenhum jogo real iniciado e nenhum historico real alterado.
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
        void VerifyLauncherChildren()
        {
            string folder = Path.Combine(Output, "games-launcher-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool value, string text) { GameAssert(value, "lancador: " + text); checks.Add("OK: " + text); };
            Func<string, GameSessions> sessions = name => new GameSessions(new GameHistory(Path.Combine(folder, name + ".json")));
            string launcher = GameFiles.Normalize(Path.Combine(folder, "Downloads", "unlockfps_nc_signed.exe"));
            string game = GameFiles.Normalize(Path.Combine(folder, "Game", "GenshinImpact.exe"));
            string key = "shortcut:42:3166698800";
            var entry = new GameEntry { Key = key, Name = "Genshin Impact", Kind = "Game", Available = true, RuntimePaths = new[] { launcher },
                AdoptChildren = true
                };
        Func<GameEntry[], GameCatalogState> catalogOf = delegate(GameEntry[] items) { var c = new GameCatalogState(); c.Entries.AddRange(items); return c; };
            var catalog = catalogOf(new[] { entry });
            Func<uint, uint, long, string, GameProcess> proc = (pid, parent, created, path) => new GameProcess { Pid = pid, ParentPid = parent,
                Created = created,
                Path = path };
            Func<uint, long, string, DesktopWindow> win = (pid, hwnd, path) => new DesktopWindow { ProcessId = pid, Handle = new IntPtr(hwnd),
                ExecutablePath = path,
                Title = Path.GetFileName(path) };
            var none = new List<DesktopWindow>();
            DateTime t0 = DateTime.UtcNow.AddMinutes(-20);

            var tracker = sessions("fluxo");
            var gameWindow = win(11, 1101, game);
            var view = tracker.Update(catalog, new List<GameProcess> { proc(10, 2, 100, launcher) }, none, 0, t0, w => true);
            check(view.Activity(key) != null && view.Activity(key).Window == null, "lancador sozinho continua aberto e sem janela");
            var both = new List<GameProcess> { proc(10, 2, 100, launcher), proc(11, 10, 150, game) };
            view = tracker.Update(catalog, both, new List<DesktopWindow> { gameWindow }, 0, t0.AddSeconds(2), w => true);
            var a = view.Activity(key);
            check(a != null && a.Window == gameWindow && a.Pid == 11, "janela do filho vale quando o alvo nao tem janela, e o filho vira o processo principal");
            check(a.Pids.Contains(10) && a.Pids.Contains(11) && a.Processes.Count == 2, "pausar e fechar alcancam lancador e jogo");
            var focusStore = new GameHistory(Path.Combine(folder, "foco.json")); var focus = new GameSessions(focusStore);
            focus.Update(catalog, both, new List<DesktopWindow> { gameWindow }, 11, t0.AddSeconds(4), w => true);
            view = focus.Update(catalog, both, new List<DesktopWindow> { gameWindow }, 11, t0.AddSeconds(6), w => true);
            check(view.Activity(key).LastFocused > 0 && focusStore.Last(key) > 0, "foco no jogo filho conta como foco do atalho");
            view = tracker.Update(catalog, new List<GameProcess> { proc(11, 10, 150, game) }, new List<DesktopWindow> { gameWindow }, 11, t0.AddSeconds(8), w =>
                true);
            check(view.Activity(key) != null && view.Activity(key).Window == gameWindow, "filho adotado continua do jogo depois que o lancador sai");
            view = tracker.Update(catalog, new List<GameProcess> { proc(11, 99, 450, game) }, new List<DesktopWindow> { gameWindow }, 0, t0.AddSeconds(10), w =>
                true);
            check(view.Activity(key) == null, "adocao antiga nao vale para outro processo com o mesmo PID");
            view = tracker.Update(catalog, new List<GameProcess>(), none, 0, t0.AddSeconds(30), w => true);
            check(view.Activity(key) == null, "jogo e lancador encerrados encerram a sessao");

            view = sessions("pid").Update(catalog, new List<GameProcess> { proc(10, 2, 100, launcher), proc(11, 10, 50, game) }, new
                List<DesktopWindow> { gameWindow
                }, 0, t0, w => true);
            check(view.Activity(key).Window == null && view.Activity(key).Pids.Count == 1, "filho mais antigo que o pai (PID reaproveitado) nao e adotado");
            view = sessions("sem-pai").Update(catalog, new List<GameProcess> { proc(10, 2, 100, launcher), proc(11, 0, 150, game) }, new List<DesktopWindow> {
                gameWindow }, 0, t0, w => true);
            check(view.Activity(key).Window == null, "sem pai conhecido nao ha adocao");
            var launcherWindow = win(10, 1001, launcher);
            view = sessions("alvo-com-janela").Update(catalog, both, new List<DesktopWindow> { launcherWindow, gameWindow }, 0, t0, w => true);
            check(view.Activity(key).Window == launcherWindow && view.Activity(key).Pid == 10 && view.Activity(key).Pids.Contains(11),
                "janela do proprio alvo tem prioridade; o filho segue na arvore");
            var owner = new GameEntry { Key = "local:genshin", Name = "Genshin direto", Kind = "Game", Available = true, RuntimePaths = new[] { game } };
            view = sessions("dono").Update(catalogOf(new[] { entry, owner }), both, new List<DesktopWindow> { gameWindow }, 0, t0, w => true);
            check(view.Activity(key).Window == null && view.Activity(key).Pids.Count == 1 && view.Activity("local:genshin").Window == gameWindow,
                "filho com jogo proprio no catalogo fica com ele");
            var mapped = entry.Copy(); mapped.AdoptChildren = false;
            view = sessions("mapeado").Update(catalogOf(new[] { mapped }), both, new List<DesktopWindow> { gameWindow }, 0, t0, w => true);
            check(view.Activity(key).Window == null && view.Activity(key).Pids.Count == 1, "sem adocao (mapeamento explicito), so o alvo conta");
            string crash = GameFiles.Normalize(Path.Combine(folder, "Game", "UnityCrashHandler64.exe"));
            view = sessions("auxiliar").Update(catalog, new List<GameProcess> { proc(10, 2, 100, launcher), proc(12, 10, 160, crash) }, new
                List<DesktopWindow> {
                win(12, 1201, crash) }, 0, t0, w => true);
            check(view.Activity(key).Window == null && view.Activity(key).Pids.Count == 1, "auxiliar filho do lancador nao e adotado");
            string cmd = GameFiles.Normalize(Path.Combine(folder, "Tools", "runner.exe"));
            var chain = new List<GameProcess> { proc(10, 2, 100, launcher), proc(20, 10, 110, cmd), proc(21, 20, 120, cmd), proc(22, 21, 130,
                game), proc(23, 22, 140,
                game) };
            view = sessions("profundo").Update(catalog, chain, new List<DesktopWindow> { win(22, 2201, game) }, 0, t0, w => true);
            check(view.Activity(key).Window != null && view.Activity(key).Window.ProcessId == 22, "bisneto do alvo (profundidade " +
                GameProcessIndex.MaxChildDepth +
                ") e adotado");
            view = sessions("fundo").Update(catalog, chain, new List<DesktopWindow> { win(23, 2301, game) }, 0, t0, w => true);
            check(view.Activity(key).Window == null && !view.Activity(key).Pids.Contains(23), "descendente alem da profundidade maxima nao e adotado");

            // Catalogo: so atalho externo com alvo proprio adota; wrapper e mapeamento explicito nao.
            string steam = Path.Combine(folder, "steam"), config = Path.Combine(steam, "userdata", "42", "config");
            Directory.CreateDirectory(Path.Combine(steam, "steamapps")); Directory.CreateDirectory(config);
                Directory.CreateDirectory(Path.GetDirectoryName(launcher));
            File.WriteAllBytes(launcher, new byte[0]);
            File.WriteAllBytes(Path.Combine(config, "shortcuts.vdf"), ShortcutFixture(launcher));
            var settings = new GameSettings { SteamProfile = "42" };
            var shortcut = SteamCatalog.Read(steam, settings, null).Entries.Find(e => e.Key == "shortcut:42:3899276376");
            check(shortcut != null && shortcut.AdoptChildren && shortcut.Available, "atalho externo com alvo existente adota filhos");
            settings.Entries.Add(new GameMapping { Key = "shortcut:42:3899276376", Name = "Renomeado" });
        check(SteamCatalog.Read(steam, settings, null).Entries.Find(e => e.Name == "Renomeado").AdoptChildren, "mapeamento sem executavel preserva a adocao");
            settings.Entries[0].RuntimePaths = new[] { game };
            check(!SteamCatalog.Read(steam, settings, null).Entries.Find(e => e.Name == "Renomeado").AdoptChildren,
                "mapeamento com executavel do jogo desliga a adocao");
            string wscript = Path.Combine(folder, "wscript.exe"); File.WriteAllBytes(wscript, new byte[0]);
            File.WriteAllBytes(Path.Combine(config, "shortcuts.vdf"), ShortcutFixture(wscript));
            check(!SteamCatalog.Read(steam, new GameSettings { SteamProfile = "42" }, null).Entries.Find(e => e.Key == "shortcut:42:3899276376").AdoptChildren,
                "wrapper (wscript/cmd) continua sem adocao");

            checks.Insert(0, "OK: " + checks.Count + " verificacoes do lancador de atalho (ADR 0089). Processos, janelas e Steam sinteticos em bin; nenhum" +
                " jogo real iniciado ou historico real alterado.");
            File.WriteAllLines(Path.Combine(Output, "jogos-lancador-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}
