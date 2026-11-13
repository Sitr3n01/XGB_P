// Verificacao deterministica: fixtures locais em bin, sem iniciar jogos nem gravar o perfil real.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Games;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static void GameAssert(bool condition, string message) { if (!condition) throw new Exception("Jogos: " + message); }
        static void BadValve(Action action)
        { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } catch (EndOfStreamException) { rejected = true; } GameAssert(rejected, "arquivo truncado deveria ser rejeitado"); }
        static void VString(BinaryWriter w, string value) { w.Write(Encoding.UTF8.GetBytes(value)); w.Write((byte)0); }
        static byte[] ShortcutFixture(string exe)
        {
            using (var memory = new MemoryStream()) using (var w = new BinaryWriter(memory))
            {
                w.Write((byte)0); VString(w, "shortcuts"); w.Write((byte)0); VString(w, "0");
                w.Write((byte)2); VString(w, "appid"); w.Write(3899276376U);
                foreach (var pair in new[] { new[] { "AppName", "Jogo externo" }, new[] { "Exe", exe }, new[] { "StartDir", Path.GetDirectoryName(exe) } })
                { w.Write((byte)1); VString(w, pair[0]); VString(w, pair[1]); }
                w.Write((byte)2); VString(w, "LastPlayTime"); w.Write((uint)GameFiles.Epoch(DateTime.UtcNow.AddDays(-2)));
                w.Write((byte)8); w.Write((byte)8); w.Write((byte)8); return memory.ToArray();
            }
        }
        static void AppInfoFixture(string file, bool table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using (var memory = new MemoryStream()) using (var data = new BinaryWriter(memory))
            {
                data.Write((byte)0); if (table) data.Write(0U); else VString(data, "appinfo");
                data.Write((byte)0); if (table) data.Write(1U); else VString(data, "common");
                data.Write((byte)1); if (table) data.Write(2U); else VString(data, "type"); VString(data, "Game");
                data.Write((byte)8); // common
                data.Write((byte)0); if (table) data.Write(3U); else VString(data, "config");
                data.Write((byte)0); if (table) data.Write(4U); else VString(data, "launch");
                data.Write((byte)0); if (table) data.Write(5U); else VString(data, "0");
                data.Write((byte)1); if (table) data.Write(6U); else VString(data, "executable"); VString(data, "game.exe");
                data.Write((byte)8); data.Write((byte)8); data.Write((byte)8); data.Write((byte)8); data.Write((byte)8);
                byte[] body = memory.ToArray();
                using (var f = new BinaryWriter(File.Create(file)))
                {
                    f.Write(table ? 0x07564429U : 0x07564428U); f.Write(1U);
                    if (table) f.Write((long)(16 + 8 + 60 + body.Length + 4));
                    f.Write(3513350U); f.Write((uint)(60 + body.Length)); f.Write(new byte[60]); f.Write(body); f.Write(0U);
                    if (table) { f.Write(7U); foreach (string key in new[] { "appinfo", "common", "type", "config", "launch", "0", "executable" }) VString(f, key); }
                }
            }
        }
        void VerifyGameBackend()
        {
            var node = ValveData.Text("// nota\n \"Root\" { \"path\" \"D:\\\\Jogos\\\\Nome\" \"nome\" \"Jogo \\\"A\\\"\" }");
            GameAssert(node.Get("root").Text("path") == @"D:\Jogos\Nome" && node.Get("Root").Text("nome") == "Jogo \"A\"", "escapes/case do VDF");
            BadValve(delegate { ValveData.Text("\"root\" { \"key\""); });
            BadValve(delegate { ValveData.Binary(new byte[] { 0, 65, 0 }); });
            GameAssert(SteamCatalog.ShortcutUri(3899276376U) == "steam://rungameid/16747264513018953728", "game ID de atalho deve preservar uint32 e bits baixos");
            GameAssert(GameFiles.Normalize("") == "" && GameFiles.ValidTime("0", DateTime.UtcNow) == 0 && GameFiles.ValidTime(long.MaxValue.ToString(), DateTime.UtcNow) == 0, "datas/caminhos invalidos");

            string test = Path.Combine(Output, "games-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(test);
            string steam = Path.Combine(test, "steam"), library = Path.Combine(test, "library"), tools = Path.Combine(steam, "steamapps"), apps = Path.Combine(library, "steamapps");
            string config = Path.Combine(steam, "userdata", "42", "config"), install = Path.Combine(apps, "common", "Game");
            Directory.CreateDirectory(tools); Directory.CreateDirectory(config); Directory.CreateDirectory(install);
            string exe = Path.Combine(install, "game.exe"); File.WriteAllBytes(exe, new byte[0]);
            File.WriteAllBytes(Path.Combine(install, "unrelated.exe"), new byte[0]);
            File.WriteAllText(Path.Combine(tools, "libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" + library.Replace("\\", "\\\\") + "\" } }");
            File.WriteAllText(Path.Combine(apps, "appmanifest_3513350.acf"), "\"AppState\" { \"appid\" \"3513350\" \"name\" \"Jogo Steam\" \"installdir\" \"Game\" }");
            File.WriteAllText(Path.Combine(apps, "appmanifest_431960.acf"), "\"AppState\" { \"appid\" \"431960\" \"name\" \"Wallpaper Engine\" \"installdir\" \"Game\" }");
            File.WriteAllText(Path.Combine(config, "localconfig.vdf"), "\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { \"3513350\" { \"LastPlayed\" \"" + GameFiles.Epoch(DateTime.UtcNow.AddDays(-1)) + "\" } } } } } }");
            File.WriteAllBytes(Path.Combine(config, "shortcuts.vdf"), ShortcutFixture(exe));
            string appInfo = Path.Combine(steam, "appcache", "appinfo.vdf"); AppInfoFixture(appInfo, false);
            GameAssert(ValveData.AppTypes(appInfo, new HashSet<uint> { 3513350 })[3513350] == "Game", "appinfo v40");
            AppInfoFixture(appInfo, true);
            GameAssert(ValveData.AppTypes(appInfo, new HashSet<uint> { 3513350 })[3513350] == "Game", "appinfo v41 com tabela de strings");
            var settings = new GameSettings { SteamProfile = "42" };
            var catalog = SteamCatalog.Read(steam, settings, null);
            GameAssert(catalog.Entries.Count == 3, "catalogo em biblioteca secundaria e atalho");
            GameAssert(catalog.Entries.Find(e => e.Name == "Jogo Steam").Available, "instalacao real da fixture");
            GameAssert(!catalog.Entries.Find(e => e.Name == "Wallpaper Engine").Available, "ferramenta excluida");
            GameAssert(catalog.Entries.Find(e => e.Name == "Jogo externo").Key == "shortcut:42:3899276376", "ID unsigned no VDF binario");
            GameAssert(catalog.Entries.Find(e => e.Name == "Jogo externo").RuntimePaths.Length == 1, "atalho nao herda executaveis de outros jogos na pasta");
            File.WriteAllBytes(Path.Combine(config, "shortcuts.vdf"), ShortcutFixture(Path.Combine(test, "wscript.exe")));
            File.WriteAllBytes(Path.Combine(test, "wscript.exe"), new byte[0]);
            catalog = SteamCatalog.Read(steam, settings, null);
            GameAssert(!catalog.Entries.Find(e => e.Name == "Jogo externo").Available, "interpretador sozinho nao e jogo");
            settings.Entries.Add(new GameMapping { Key = "shortcut:42:3899276376", Kind = "Game", RuntimePaths = new[] { exe }, RequiredFiles = new[] { exe } });
            catalog = SteamCatalog.Read(steam, settings, null);
            GameAssert(catalog.Entries.Find(e => e.Name == "Jogo externo").Available, "wrapper com mapeamento explicito");
            // Manifesto antigo do mesmo jogo noutra biblioteca: o catalogo nao cai e fica o registro instalado (sessao de debug 1, B5).
            string stale = Path.Combine(tools, "appmanifest_3513350.acf"); File.Copy(Path.Combine(apps, "appmanifest_3513350.acf"), stale);
            var repeated = SteamCatalog.Read(steam, settings, null); File.Delete(stale);
            GameAssert(repeated.Entries.FindAll(e => e.Name == "Jogo Steam").Count == 1 && repeated.Entries.Find(e => e.Name == "Jogo Steam").Available &&
                repeated.Repeated.Count == 1 && repeated.Repeated[0] == "steam:42:3513350", "registro repetido nao derruba o catalogo e mantem o instalado");

            string historyPath = Path.Combine(test, "history.json"); var history = new GameHistory(historyPath);
            var tracker = new GameSessions(history); DateTime now = DateTime.UtcNow;
            // Usar somente uma entrada para nao associar o mesmo executavel de fixture a dois jogos.
            catalog.Entries.RemoveAll(e => e.Name != "Jogo Steam"); string key = catalog.Entries[0].Key;
            var process = new GameProcess { Pid = 701, Created = 100, Path = exe };
            var win = new DesktopWindow { ProcessId = 701, Handle = new IntPtr(702), ExecutablePath = exe, Title = "Jogo" };
            var ps = new List<GameProcess> { process }; var ws = new List<DesktopWindow> { win };
            var view = tracker.Update(catalog, ps, ws, 701, now);
            GameAssert(history.Last(key) == 0 && !File.Exists(historyPath), "uma leitura nao confirma partida");
            view = tracker.Update(catalog, ps, ws, 701, now.AddSeconds(2));
            GameAssert(view.Current != null && view.Current.Confirmed && File.Exists(historyPath), "duas leituras confirmam e persistem");
            long observed = history.Last(key); win.Minimized = true;
            view = tracker.Update(catalog, ps, ws, 0, now.AddSeconds(4));
            GameAssert(view.Current != null && history.Last(key) == observed, "minimizar/overlay preserva sessao sem renovar data");
            process.Created = 200;
            view = tracker.Update(catalog, ps, ws, 0, now.AddSeconds(6));
            GameAssert(history.Last(key) == observed, "recriacao curta preserva partida");
            view = tracker.Update(catalog, new List<GameProcess>(), new List<DesktopWindow>(), 0, now.AddSeconds(20));
            GameAssert(view.Current == null && view.Activities.Count == 0, "fim do processo remove retomada");
            GameAssert(new GameHistory(historyPath).Last(key) == observed, "historico sobrevive ao reinicio do observador");
            win.Minimized = false; process.Created = 300;
            view = tracker.Update(catalog, ps, ws, 701, now.AddSeconds(22));
            GameAssert(!view.Current.Confirmed && history.Last(key) == observed, "PID reutilizado depois do encerramento inicia nova sessao");
            view = tracker.Update(catalog, ps, ws, 701, now.AddSeconds(24));
            GameAssert(history.Last(key) > observed, "nova sessao renova ultimo acesso");
            var standalone = new GameSettings(); standalone.Entries.Add(new GameMapping { Key = "local:demo", Name = "Independente", Kind = "Game", LaunchFile = exe, RuntimePaths = new[] { exe } });
            var localCatalog = SteamCatalog.Read(null, standalone, null);
            GameAssert(localCatalog.Entries.Count == 1 && localCatalog.Entries[0].Available, "jogo local funciona sem Steam");
            history.Observe(key, now.AddSeconds(1)); GameAssert(File.Exists(historyPath + ".bak"), "substituicao atomica com backup");
            File.WriteAllText(historyPath, "incompleto"); var broken = new GameHistory(historyPath); broken.Observe(key, now);
            GameAssert(broken.Error != null && File.ReadAllText(historyPath) == "incompleto", "historico ilegivel nao e sobrescrito");
            // Tracking robusto: janela fora da enumeracao, processo sem caminho, pasta de instalacao e destaque pelo foco.
            DateTime t0 = DateTime.UtcNow; bool valid = true; Func<DesktopWindow, bool> check = delegate { return valid; };
            var robust = new GameSessions(new GameHistory(Path.Combine(test, "history-robusto.json")));
            var gameProc = new GameProcess { Pid = 801, Created = 10, Path = exe };
            var gameWin = new DesktopWindow { ProcessId = 801, Handle = new IntPtr(802), ExecutablePath = exe, Title = "Jogo" };
            var alone = new List<GameProcess> { gameProc };
            var seen = robust.Update(catalog, alone, new List<DesktopWindow> { gameWin }, 801, t0, check);
            GameAssert(seen.Current != null && seen.Current.Window == gameWin && seen.Current.Pid == 801 && seen.Current.Pids.Count == 1, "jogo aberto com janela e processo principal");
            seen = robust.Update(catalog, alone, new List<DesktopWindow>(), 0, t0.AddSeconds(2), check);
            GameAssert(seen.Current != null && seen.Current.Window == gameWin, "janela fora da enumeracao (pausada ou nao respondendo) continua do jogo");
            valid = false; seen = robust.Update(catalog, alone, new List<DesktopWindow>(), 0, t0.AddSeconds(4), check);
            GameAssert(seen.Current != null && seen.Current.Window == null && seen.Featured != null && seen.Featured.Key == key, "processo vivo sem janela continua aberto e em destaque");
            valid = true;
            var shielded = new GameSessions(new GameHistory(Path.Combine(test, "history-protegido.json"))).Update(catalog,
                new List<GameProcess> { new GameProcess { Pid = 803, Name = "GAME.EXE" } }, new List<DesktopWindow> { new DesktopWindow { ProcessId = 803, Handle = new IntPtr(804), Title = "Jogo" } }, 0, t0, check);
            GameAssert(shielded.Activity(key) != null && shielded.Activity(key).Window != null, "processo sem caminho legivel (anti-cheat) reconhecido pelo nome");
            var helper = new GameSessions(new GameHistory(Path.Combine(test, "history-auxiliar.json"))).Update(catalog,
                new List<GameProcess> { new GameProcess { Pid = 805, Created = 20, Path = Path.Combine(install, "CrashReporter.exe") } }, new List<DesktopWindow>(), 0, t0, check);
            GameAssert(helper.Activities.Count == 0, "auxiliar de crash na pasta nao abre o jogo");
            var engine = new GameSessions(new GameHistory(Path.Combine(test, "history-pasta.json"))).Update(catalog,
                new List<GameProcess> { new GameProcess { Pid = 806, Created = 21, Path = Path.Combine(install, "bin", "Engine.exe") } }, new List<DesktopWindow>(), 0, t0, check);
            GameAssert(engine.Activity(key) != null, "outro executavel da pasta de instalacao conta como jogo aberto");
            var two = new GameCatalogState(); string exeA = Path.Combine(test, "a", "a.exe"), exeB = Path.Combine(test, "b", "b.exe");
            two.Entries.Add(new GameEntry { Key = "local:a", Name = "A", Kind = "Game", RuntimePaths = new[] { GameFiles.Normalize(exeA) }, Available = true });
            two.Entries.Add(new GameEntry { Key = "local:b", Name = "B", Kind = "Game", RuntimePaths = new[] { GameFiles.Normalize(exeB) }, Available = true });
            var duo = new GameSessions(new GameHistory(Path.Combine(test, "history-dois.json")));
            var both = new List<GameProcess> { new GameProcess { Pid = 901, Created = 100, Path = exeA }, new GameProcess { Pid = 902, Created = 200, Path = exeB } };
            var wins = new List<DesktopWindow> { new DesktopWindow { ProcessId = 901, Handle = new IntPtr(911), ExecutablePath = exeA, Title = "A" },
                new DesktopWindow { ProcessId = 902, Handle = new IntPtr(912), ExecutablePath = exeB, Title = "B" } };
            seen = duo.Update(two, both, wins, 0, t0, check);
            GameAssert(seen.Current.Key == "local:b", "sem foco registrado, o jogo aberto mais recente fica em destaque");
            seen = duo.Update(two, both, wins, 901, t0.AddSeconds(2), check);
            GameAssert(seen.Current.Key == "local:a" && seen.Recent().Count == 1 && seen.Recent()[0].Key == "local:b", "o jogo em foco fica somente no destaque; o outro aberto permanece nos recentes");
            seen = duo.Update(two, both, wins, 902, t0.AddSeconds(4), check);
            GameAssert(seen.Current.Key == "local:b" && seen.Recent().Count == 1 && seen.Recent()[0].Key == "local:a", "trocar o foco troca o destaque excluido da lista");
            File.Delete(exe); catalog = SteamCatalog.Read(steam, settings, null);
            GameAssert(!catalog.Entries.Find(e => e.Name == "Jogo Steam").Available && !catalog.Entries.Find(e => e.Name == "Jogo externo").Available, "executavel removido invalida jogo e wrapper");
        }
        void VerifyRecentExclusion()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) {
                checks++; GameAssert(ok, "recentes sem destaque: " + message);
            };
            var view = new GamesView();
            int time = 500;
            foreach (string key in new[] { "a", "b", "c", "d", "e" })
            {
                view.Entries.Add(new GameEntry { Key = key, Name = "Mesmo nome", Kind = "Game", Available = true, Imported = time });
                time -= 100;
            }
            check(view.Featured.Key == "a", "sem jogo aberto, ultimo executado e o destaque");
            var recent = view.Recent();
            check(recent.Count == 3 && recent[0].Key == "b" && recent[1].Key == "c" && recent[2].Key == "d", "excluir antes de limitar a tres");
            view.Current = new GameActivity { Key = "e", LastFocused = 10 }; view.Activities.Add(view.Current);
            recent = view.Recent();
            check(view.Featured.Key == "e" && recent.Count == 3 && recent[0].Key == "a" && recent[2].Key == "c", "aberto tem prioridade no destaque e libera tres vagas");
            view.Activities.Add(new GameActivity { Key = "d", LastFocused = 20 });
            recent = view.Recent();
            check(recent.Count == 3 && recent[0].Key == "d" && recent[1].Key == "a" && recent[2].Key == "b", "outro aberto preserva prioridade sem repetir destaque");
            view.Entries.Add(new GameEntry { Key = "e", Name = "Outra referencia", Kind = "Game", Available = true, Imported = 600 });
            check(!view.Recent().Exists(e => e.Key == "e"), "exclusao por chave, nao por referencia ou nome");
            view.Current = null; view.Activities.Clear(); recent = view.Recent();
            check(view.Featured.Key == "e" && recent[0].Key == "a" && recent.Count == 3, "ultimo fechado permanece excluido");
            view.Find("a").Available = false; recent = view.Recent();
            check(recent.Count == 3 && recent[0].Key == "b" && recent[2].Key == "d", "indisponivel nao ocupa vaga");
            view = new GamesView();
            check(view.Featured == null && view.Recent().Count == 0, "historico vazio nao inventa jogos");
            view.Entries.Add(new GameEntry { Key = "unico", Name = "Unico", Kind = "Game", Available = true, Imported = 100 });
            check(view.Featured.Key == "unico" && view.Recent().Count == 0, "um jogo aparece so no destaque");
            view.Entries.Add(new GameEntry { Key = "anterior", Name = "Anterior", Kind = "Game", Available = true, Imported = 90 });
            check(view.Recent().Count == 1 && view.Recent()[0].Key == "anterior", "pouco historico nao duplica para preencher");
            view.Entries.Add(new GameEntry { Key = "ferramenta", Kind = "Tool", Available = true, Imported = 1000 });
            check(view.Featured.Key == "unico" && view.Recent().Count == 1, "ferramentas continuam fora da Home de jogos");
            File.WriteAllText(Path.Combine(Output, "recentes-sem-destaque-verificacao.txt"),
                "OK: " + checks + " verificacoes de exclusao, limite, identidade, disponibilidade, historico vazio e outros jogos abertos. " +
                "Somente snapshots em memoria; nenhum jogo ou dado real alterado.\r\n", Encoding.UTF8);
        }
        void VerifyGames()
        {
            VerifyGameBackend();
            VerifyRecentExclusion();
            byte[] imageFixture = VerifyGameImages();
            var original = gamesView; var launch = gameLaunch; string sent = null;
            var available = appAvailable; var activate = switchApp; var foreground = appForeground;
            var foregroundReader = foregroundRead;
            var controlFocus = controllerFocus;
            try
            {
                gamesView = ExampleGames(); gameLaunch = delegate(GameEntry e) { sent = e.Key; };
                appAvailable = delegate { return true; }; switchApp = delegate(DesktopWindow w) { sent = "resume"; }; appForeground = delegate { return true; };
                foregroundRead = delegate { return window.IsVisible ? handle : IntPtr.Zero; };
                controllerFocus = delegate { return true; };
                SetPanel(0); Show(); window.UpdateLayout();
                GameAssert(gamesView.Recent().Count == 3 && gamesView.Recent()[0].Key == "fixture:one" && gamesView.Recent()[2].Key == "fixture:three", "tres recentes seguintes, sem ocupar vaga com o destaque");
                foreach (var c in gameControls)
                {
                    if (((string)c.Tag).Contains("pause-") || ((string)c.Tag).Contains("close-")) continue;
                    var grid = (Grid)((Button)c).Content; var border = (Border)grid.Children[0]; GameAssert(((TextBlock)border.Child).Text == GameGlyph, "icone unico de jogo");
                }
                double resumeHeight = gameResumeRow[0].ActualHeight;
                var illustrated = gamesView.Featured; illustrated.IconPng = imageFixture; illustrated.IconKey = "fixture-image";
                RenderGames(true); window.UpdateLayout();
                var iconSlot = (Border)((Grid)((Button)gameResumeRow[0]).Content).Children[0];
                var imageLayer = iconSlot.Child as Grid;
                var gameImage = imageLayer == null ? null : imageLayer.Children[imageLayer.Children.Count - 1] as Image;
                GameAssert(gameImage != null && gameImage.Width == 32 && gameImage.Height == 32 && Math.Abs(gameResumeRow[0].ActualHeight - resumeHeight) < .1, "imagem nao altera proporcao do cartao");
                illustrated.IconPng = null; illustrated.IconKey = null; RenderGames(true);
                FocusHome("resume"); Activate(); SettleGameTransitionFixture(); GameAssert(sent == "resume" && !window.IsVisible, "retomada pela janela");
                Show(); FocusHome("recent-fixture:one"); Activate();
                GameAssert(gameConfirmation == "fixture:one" && (string)((Control)Keyboard.FocusedElement).Tag == "game-cancel", "confirmacao inicia em cancelar");
                Navigate("CIMA"); GameAssert((string)((Control)Keyboard.FocusedElement).Tag == "game-confirm", "controle navega na confirmacao");
                Back(); GameAssert(gameConfirmation == null && (string)((Control)Keyboard.FocusedElement).Tag == "recent-fixture:one", "cancelar restaura foco estavel");
                FocusHome("recent-fixture:one"); Activate(); FocusHome("game-confirm"); Activate();
                GameAssert(sent == "resume" && launchingGame == null && gameLaunchRoute != null, "confirmar troca nao despacha outro jogo enquanto ha jogos abertos");
                EndGameLaunchRoute(); gamesView.Activities.Clear(); gamesView.Current = null; RequestGameLaunch("fixture:one");
                GameAssert(sent == "fixture:one" && !window.IsVisible && launchingGame == sent, "despacho e pendencia separados de confirmacao");
                Show(); sent = null; RequestGameLaunch("fixture:two"); GameAssert(sent == null && window.IsVisible, "bloquear lancamento duplicado");
                launchDeadline = DateTime.UtcNow.AddSeconds(-1); CheckGameLaunch(); GameAssert(launchingGame == null && window.IsVisible, "timeout permite nova tentativa");
                gameLaunch = delegate { throw new IOException("falha simulada"); }; RequestGameLaunch("fixture:one");
                GameAssert(window.IsVisible && launchingGame == null, "falha de despacho preserva janela");
                // O mesmo cartao permanece quando a janela fecha, sem duplicar o jogo nos recentes.
                gameLaunch = delegate(GameEntry e) { sent = e.Key; };
                gamesView.Current = null; gamesView.Activities.Clear(); RenderGames(true); window.UpdateLayout();
                GameAssert(gamesView.Featured.Key == "fixture:current" && gameResumeRow.Count == 1 && gameResumeRow[0].IsEnabled && gamesView.Recent()[0].Key == "fixture:one" && gamesView.Recent().Count == 3, "ultimo jogo fechado permanece somente em Voltar ao jogo");
                SaveRender("home-ultimo-jogo"); sent = null; FocusHome("resume"); Activate();
                GameAssert(sent == "fixture:current" && !window.IsVisible, "cartao do ultimo jogo fechado solicita novo lancamento");
                launchingGame = null; Show(); gamesView.Find("fixture:current").Available = false; RenderGames(true);
                GameAssert(gamesView.Featured.Key == "fixture:current" && !gameResumeRow[0].IsEnabled, "ultimo jogo indisponivel permanece identificado");
                gamesView.Find("fixture:one").Observed = GameFiles.Epoch(DateTime.UtcNow); RenderGames(true);
                GameAssert(gamesView.Featured.Key == "fixture:one" && gamesView.Recent()[0].Key == "fixture:two" && gamesView.Recent().Count == 2, "novo ultimo uso sai da lista; sem inventar terceira entrada indisponivel");
                gamesView = new GamesView(); RenderGames(true); window.UpdateLayout();
                GameAssert(gameControls.Count == 1 && gameResumeRow.Count == 1 && !gameResumeRow[0].IsEnabled, "cartao permanente sem inventar jogo com historico vazio");
                FocusHome("steam-home"); Navigate("BAIXO");
                GameAssert(((Control)Keyboard.FocusedElement).Tag != null, "navegar com linhas vazias"); SaveRender("home-jogos-vazio");
                gamesView = ExampleGames(); SetPanel(0); Show(); FocusHome("recent-fixture:two");
                gamesView.Entries.Reverse(); RenderGames(false);
                GameAssert((string)((Control)Keyboard.FocusedElement).Tag == "recent-fixture:two", "foco preservado durante tracking");
                var next = new GameActivity { Key = "fixture:two", Pid = 600, Pids = new List<uint> { 600 } };
                gamesView.Current = next; gamesView.Activities.Add(next); RenderGames(false); window.UpdateLayout();
                GameAssert(gamesView.Featured.Key == "fixture:two" && !gameControls.Exists(c => (string)c.Tag == "recent-fixture:two") &&
                    gameControls.Exists(c => (string)c.Tag == "recent-fixture:three") && gameOpenRows.Count == 1 &&
                    (string)gameOpenRows[0][0].Tag == "recent-fixture:current" &&
                    (string)((Control)Keyboard.FocusedElement).Tag == "resume", "tracking com foco move o destaque e mantem abertos no grupo de retomada");
                SaveRender("home-recentes-sem-destaque");
                VerifyOpenGames();
                File.WriteAllText(Path.Combine(Output, "jogos-verificacao.txt"), "OK: parsers, IDs, catalogo, ferramentas, wrappers, caminhos ausentes, datas, persistencia atomica, arquivo corrompido, sessoes, retomada, recentes, icones, foco, confirmacao, erros e timeout.\r\nImagens: prioridade, extracao nativa, cache persistente e invalidacao/corrupcao, arquivo ausente/invalido, bloqueio de icone de interpretador, correspondencia de atalhos, falta de permissao de cache, proporcao/transparencia e tamanho do cartao preservados.\r\nFixtures em games-tests; nenhum jogo ou comando externo executado.\r\n" +
                    "Jogos abertos: tracking sem janela, janela pausada/nao respondendo, processo sem caminho, pasta de instalacao, auxiliares, destaque pelo foco, ate tres outros recentes sem duplicar o destaque inclusive durante tracking com foco; pausar/retomar com minimizacao e descendentes, voltar retomando, fechar com confirmacao, forcar apos 10 s, saida confirmada, erros e sem relancar jogo aberto. Processos reais nao tocados.\r\n", Encoding.UTF8);
            }
            finally
            { gamesView = original; gameLaunch = launch; gameConfirmation = null; launchingGame = null; appAvailable = available; switchApp = activate; appForeground = foreground; foregroundRead = foregroundReader; controllerFocus = controlFocus; SetPanel(0); }
        }
        // Jogos abertos na Home: botoes so com o jogo aberto, pausar/retomar, voltar retomando, fechar com confirmacao e forcar.
        void VerifyOpenGames()
        {
            var calls = new List<string>();
            var suspend = gameSuspend; var resume = gameResume; var kill = gameKill; var tree = gameTree; var close = gameCloseWindow; var minimize = gameMinimize;
            Func<string, IList<uint>, string> record = delegate(string verb, IList<uint> pids) { calls.Add(verb + ":" + string.Join(",", pids)); return null; };
            try
            {
                gameSuspend = delegate(IList<uint> p) { return record("pausar", p); }; gameResume = delegate(IList<uint> p) { return record("retomar", p); };
                gameKill = delegate(IList<uint> p) { return record("encerrar", p); };
                gameCloseWindow = delegate(IntPtr h) { calls.Add("fechar:" + h.ToInt64()); }; gameMinimize = delegate(IntPtr h) { calls.Add("minimizar:" + h.ToInt64()); };
                gameTree = delegate(IEnumerable<uint> roots) { var all = new List<uint>(roots); all.Add(999); return all; };
                string switched = null; switchApp = delegate(DesktopWindow w) { switched = w.Title; }; appAvailable = delegate { return true; };
                gamesView = ExampleGames();
                var second = new GameActivity { Key = "fixture:two", Pid = 600, Pids = new List<uint> { 600 }, Window = new DesktopWindow { Handle = new IntPtr(601), ProcessId = 600, Title = "Dois", AppName = "Dois" } };
                gamesView.Activities.Add(second);
                SetPanel(0); Show(); window.UpdateLayout();
                GameAssert(gameResumeRow.Count == 3 && (string)gameResumeRow[1].Tag == "pause-fixture:current" && (string)gameResumeRow[2].Tag == "close-fixture:current", "destaque aberto com pausar e fechar a direita");
                GameAssert(gameOpenRows.Count == 1 && gameOpenRows[0].Count == 3 && gameRecentRows[0].Count == 1 && gameRecentRows[1].Count == 1 &&
                    (string)gameOpenRows[0][1].Tag == "recent-pause-fixture:two", "outro aberto fica no grupo de retomada; recentes so com fechados");
                GameAssert(Math.Abs(gameResumeRow[1].ActualHeight - gameResumeRow[0].ActualHeight) < .5 && Math.Abs(gameResumeRow[1].ActualWidth - 52) < .5 &&
                    Math.Abs(gameResumeRow[2].TranslatePoint(new Point(52, 0), body).X - gameOpenRows[0][2].TranslatePoint(new Point(52, 0), body).X) < .5, "botoes com a altura do cartao e alinhados a direita");
                SaveRender("home-jogos-abertos");
                FocusHome("utility-programs"); Navigate("CIMA"); // Programas fica abaixo do fechar, do lado direito.
                GameAssert(Keyboard.FocusedElement == gameOpenRows[0][0], "subir do Acesso rapido entra pelo cartao do outro jogo aberto, nao pelo fechar");
                Navigate("DIREITA"); GameAssert(Keyboard.FocusedElement == gameOpenRows[0][1] && Outline(gameOpenRows[0][1]) == Brush("FocusGreen").Color, "direita vai para pausar, com contorno verde");
                Navigate("DIREITA"); GameAssert(Keyboard.FocusedElement == gameOpenRows[0][2] && Outline(gameOpenRows[0][2]) == Brush("CloseRed").Color, "direita vai para fechar, com contorno vermelho");
                GameAssert(gameResumeRow[2].Style == window.FindResource("CloseAction") && gameResumeRow[1].Style != window.FindResource("CloseAction"), "fechar do destaque no estilo vermelho; pausar continua verde");
                window.UpdateLayout(); SaveRender("home-fechar-jogo-foco");
                FocusHome("pause-fixture:current"); Activate();
                GameAssert(calls.Count == 2 && calls[0] == "minimizar:501" && calls[1] == "pausar:500,999" && gamesView.Activity("fixture:current").Paused, "pausar minimiza e suspende processo e descendentes");
                GameAssert((string)((Control)Keyboard.FocusedElement).Tag == "pause-fixture:current" && ((TextBlock)((Button)gameResumeRow[1]).Content).Text == "\uE768", "botao vira retomar e mantem o foco");
                GameAssert(((TextBlock)((StackPanel)((Grid)((Button)gameResumeRow[0]).Content).Children[1]).Children[1]).Text.EndsWith("pausado") && pausedByPreview.ContainsKey("fixture:current"), "destaque mostra pausado e a previa lembra de retomar");
                window.UpdateLayout(); SaveRender("home-jogo-pausado");
                FocusHome("resume"); Activate();
                SettleGameTransitionFixture();
                GameAssert(calls[2] == "retomar:500,999" && !gamesView.Activity("fixture:current").Paused && switched == "Jogo de teste" && !window.IsVisible && !pausedByPreview.ContainsKey("fixture:current"),
                    "voltar a um jogo pausado retoma e traz a janela");
                Show(); FocusHome("recent-pause-fixture:two"); Activate(); Activate();
                GameAssert(calls[calls.Count - 1] == "retomar:600,999" && !gamesView.Activity("fixture:two").Paused, "pausar duas vezes alterna para retomar");
                GameAssert((string)((Control)Keyboard.FocusedElement).Tag == "recent-pause-fixture:two", "pausar na lista mantem o foco na lista");
                FocusHome("recent-close-fixture:two"); Activate();
                GameAssert(gameCloseConfirmation == "fixture:two" && (string)((Control)Keyboard.FocusedElement).Tag == "game-cancel", "fechar pede confirmacao e comeca em cancelar");
                window.UpdateLayout(); SaveRender("home-fechar-jogo");
                Navigate("CIMA"); GameAssert((string)((Control)Keyboard.FocusedElement).Tag == "game-close-confirm", "controle navega na confirmacao de fechar");
                Back(); GameAssert(gameCloseConfirmation == null && (string)((Control)Keyboard.FocusedElement).Tag == "recent-close-fixture:two", "cancelar volta ao botao fechar de origem");
                FocusHome("recent-close-fixture:two"); Activate(); FocusHome("game-close-confirm"); Activate();
                GameAssert(calls[calls.Count - 1] == "fechar:601" && closingGame == "fixture:two" && !window.IsVisible, "confirmar envia o fechamento normal e oculta a barra antes da saida");
                closeDeadline = DateTime.UtcNow.AddSeconds(-1); CheckGameClose();
                GameAssert(gameCloseConfirmation == "fixture:two" && gameCloseForce && !window.IsVisible, "jogo que nao fecha prepara forcar sem reabrir a barra");
                Show(); RenderGames(true); FocusHome("game-cancel");
                FocusHome("game-close-confirm"); Activate();
                GameAssert(calls[calls.Count - 1] == "encerrar:600,999" && closingGame == "fixture:two" && !window.IsVisible, "forcar oculta a barra e aguarda confirmar saida dos processos");
                gamesView.Activities.Remove(second); CheckGameClose(); RenderGames(true); window.UpdateLayout();
                GameAssert(gameRecentRows.Exists(r => r.Count == 1 && (string)r[0].Tag == "recent-fixture:two") && !gameControls.Exists(c => (string)c.Tag == "recent-pause-fixture:two"), "jogo fechado perde os botoes");
                closingGame = "fixture:current"; closingName = "Jogo em execução"; closeDeadline = DateTime.UtcNow.AddSeconds(5);
                gameCloseFeedback = BeginFeedback("Fechando Jogo em execução…", "0");
                gamesView.Activities.Clear(); gamesView.Current = null; CheckGameClose();
                GameAssert(closingGame == null && LatestHistoryText() == "Jogo em execução fechado." && ((TextBlock)window.FindName("Feedback")).Text == "", "saida do processo confirma o fechamento so no historico");
                gamesView = ExampleGames(); Show(); RenderGames(true);
                gameCloseWindow = delegate { throw new InvalidOperationException("Fechamento negado no teste."); };
                AskCloseGame("fixture:current", "close-fixture:current"); ConfirmCloseGame();
                GameAssert(window.IsVisible && closingGame == null && gameCloseFeedback.Kind == FeedbackKind.Error, "pedido de fechamento negado mantem a barra e o erro visiveis");
                gameCloseWindow = close;
                gameSuspend = delegate { throw new InvalidOperationException("O Windows negou pausar o jogo. Ele pode estar como administrador ou protegido por anti-cheat."); };
                FocusHome("pause-fixture:current"); Activate();
                GameAssert(!gamesView.Activity("fixture:current").Paused && ((TextBlock)window.FindName("Feedback")).Text.StartsWith("O Windows negou") && gameBusy == null, "pausa negada informa e nao marca pausado");
                // Estado de pausa: maioria dos threads; a pausa propria vale durante a entrega; retomada externa limpa o registro.
                var probe = ExampleGames(); var act = probe.Activity("fixture:current");
                pausedByPreview["fixture:current"] = new PausedGame { Pid = 500 };
                act.SuspendedShare = .3; act.Paused = false; ApplyPausedState(probe);
                GameAssert(act.Paused && pausedByPreview.ContainsKey("fixture:current"), "pausa propria vale com threads ainda presos no kernel");
                act.SuspendedShare = 0; act.Paused = false; ApplyPausedState(probe);
                GameAssert(!act.Paused && !pausedByPreview.ContainsKey("fixture:current"), "retomada por fora limpa o registro");
                pausedByPreview["fixture:current"] = new PausedGame { Pid = 777 }; ApplyPausedState(probe);
                GameAssert(!pausedByPreview.ContainsKey("fixture:current"), "outro processo nao herda a pausa");
                // O principal saiu durante a pausa: o restante registrado e retomado em vez de ficar congelado.
                pausedByPreview["fixture:current"] = new PausedGame { Pid = 777, Members = new Dictionary<uint, long> { { 777, 0 }, { 778, 0 } } }; ApplyPausedState(probe);
                GameAssert(calls[calls.Count - 1] == "retomar:777,778" && !pausedByPreview.ContainsKey("fixture:current"), "principal que saiu retoma o restante da pausa");
                string launched = null; var launch = gameLaunch; gameLaunch = delegate(GameEntry e) { launched = e.Key; };
                gamesView.Activity("fixture:current").Window = null; RenderGames(true); FocusHome("resume"); Activate();
                GameAssert(launched == null && window.IsVisible && gameConfirmation == null, "jogo aberto sem janela nunca e lancado de novo");
                gameLaunch = launch;
                AskCloseGame("fixture:current", "close-fixture:current"); ConfirmCloseGame();
                GameAssert(window.IsVisible && gameCloseForce && closingGame == null, "sem janela e sem pedido aceito, a barra mantem a confirmacao de forcar visivel");
            }
            finally
            {
                gameSuspend = suspend; gameResume = resume; gameKill = kill; gameTree = tree; gameCloseWindow = close; gameMinimize = minimize;
                pausedByPreview.Clear(); gameCloseConfirmation = null; gameCloseForce = false; closingGame = null; gameBusy = null;
            }
        }
        // Espera curta por um estado do kernel (suspensao entregue ao voltar ao modo usuario), sem dormir a toa.
        static bool Settle(Func<bool> ready)
        {
            for (int i = 0; i < 20; i++) { if (ready()) return true; Thread.Sleep(50); }
            return ready();
        }
        // Backend real so num processo criado aqui: cmd com um ping filho. Nenhum jogo ou processo do usuario e tocado.
        public void VerifyGameControlLive()
        {
            var lines = new List<string>();
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c ping -n 60 127.0.0.1 >nul") { CreateNoWindow = true, UseShellExecute = false };
            using (var shell = Process.Start(start))
            {
                var identities = new List<ProcessIdentity>();
                try
                {
                    Thread.Sleep(800);
                    var tree = GameControl.Tree(new[] { (uint)shell.Id });
                    identities = GameControl.CheckedTree(GameControl.Capture(new[] { (uint)shell.Id }));
                    lines.Add("arvore: " + string.Join(",", tree));
                    GameAssert(tree.Count >= 2 && tree[0] == (uint)shell.Id, "arvore inclui os filhos do processo de teste");
                    var suspended = GameControl.Apply(identities, ProcessAction.Suspend); suspended.RequireRoot();
                    string warning = suspended.Warning;
                    GameAssert(warning == null && Settle(delegate { return tree.TrueForAll(GameControl.Suspended); }), "processo de teste e descendentes pausados");
                    foreach (var pid in tree) lines.Add("pausado " + pid + ": " + Math.Round(GameControl.SuspendedShare(pid) * 100) + "% dos threads suspensos");
                    var resumed = GameControl.Apply(identities, ProcessAction.Resume);
                    GameAssert(resumed.Complete, "retomada verificada por identidade");
                    GameAssert(Settle(delegate { return tree.TrueForAll(p => GameControl.SuspendedShare(p) == 0); }), "processo de teste retomado");
                    lines.Add("retomado: nenhum thread suspenso");
                    GameControl.Apply(identities, ProcessAction.Kill).RequireRoot();
                    GameAssert(shell.WaitForExit(5000), "processo de teste encerrado");
                    lines.Add("encerrado com descendentes");
                }
                finally
                {
                    // Nunca deixar filhos do teste pausados: retomar e encerrar a arvore inteira, inclusive apos falha.
                    try { GameControl.Apply(identities, ProcessAction.Resume); GameControl.Apply(identities, ProcessAction.Kill); } catch (Exception) { }
                    if (!shell.HasExited) shell.Kill();
                }
            }
            File.WriteAllLines(Path.Combine(Output, "jogos-controle-real.txt"), lines, Encoding.UTF8);
            WriteLog("jogos: controle real verificado so no processo de teste");
        }
        byte[] VerifyGameImages()
        {
            string folder = Path.Combine(Output, "games-tests", "images-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string source = Path.Combine(folder, "custom.png"), cache = Path.Combine(folder, "cache");
            using (var bitmap = new System.Drawing.Bitmap(32, 16))
            { using (var g = System.Drawing.Graphics.FromImage(bitmap)) g.Clear(System.Drawing.Color.Red); bitmap.Save(source, System.Drawing.Imaging.ImageFormat.Png); }
            var game = new GameEntry { Key = "local:image", IconLocation = source, LaunchFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") };
            var images = new GameImages(cache); images.Resolve(game);
            GameAssert(game.IconPng != null && game.IconSource == source, "imagem personalizada tem prioridade sobre executavel");
            string key = game.IconKey, cached = Path.Combine(cache, key + ".png");
            GameAssert(File.Exists(cached), "PNG extraido fica no cache");
            using (var stream = new MemoryStream(game.IconPng)) using (var bitmap = new System.Drawing.Bitmap(stream))
                GameAssert(bitmap.Width == 64 && bitmap.Height == 64 && bitmap.GetPixel(32, 0).A == 0, "PNG proporcional com transparencia");
            byte[] fixture = game.IconPng;
            new GameImages(cache).Resolve(game); GameAssert(game.IconKey == key && Convert.ToBase64String(game.IconPng) == Convert.ToBase64String(fixture), "cache reutilizado entre instancias");
            File.WriteAllText(cached, "PNG incompleto"); new GameImages(cache).Resolve(game);
            GameAssert(game.IconPng != null && new FileInfo(cached).Length > 20, "cache corrompido e reconstruido");
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(2)); images.Resolve(game);
            GameAssert(game.IconKey != key, "mudanca na origem invalida cache e UI");
            new GameImages(source).Resolve(game); GameAssert(game.IconPng != null, "falha de escrita do cache nao perde imagem");
            File.WriteAllText(source, "invalida"); images.Resolve(game);
            GameAssert(game.IconPng != null && GameFiles.Same(game.IconSource, game.LaunchFile), "imagem invalida tenta extracao nativa do exe");
            game.IconLocation = Path.Combine(folder, "ausente.png"); images.Resolve(game);
            GameAssert(game.IconPng != null, "origem ausente nao impede reserva pelo exe");
            game.LaunchFile = Path.Combine(Environment.SystemDirectory, "wscript.exe"); images.Resolve(game);
            GameAssert(game.IconPng == null && game.IconKey == "generic", "sem arte valida, usar simbolo de jogo e nao interpretador");
            game.RuntimePaths = new[] { Path.Combine(folder, "Game.exe") };
            GameAssert(GameImages.Matches(game, new GameImageLink { Target = game.RuntimePaths[0] }) && !GameImages.Matches(game, new GameImageLink { Target = Path.Combine(folder, "other", "Game.exe") }), "atalho por caminho exato, nao nome");
            game.LaunchUri = "steam://rungameid/42";
            GameAssert(GameImages.Matches(game, new GameImageLink { Uri = game.LaunchUri }) && !GameImages.Matches(game, new GameImageLink { Target = game.LaunchFile }), "URI e identidade do jogo, interpretador compartilhado nao");
            string path; int index; GameImages.Location("\"" + source + "\",-4", out path, out index);
            GameAssert(path == source && index == -4, "recurso de icone com caminho entre aspas e indice");
            string desktop = Path.Combine(folder, "desktop"); Directory.CreateDirectory(desktop);
            string custom = Path.Combine(folder, "desktop.png"); File.WriteAllBytes(custom, fixture);
            File.WriteAllText(Path.Combine(desktop, "Nome diferente.url"), "[InternetShortcut]\r\nURL=" + game.LaunchUri + "\r\nIconFile=" + custom + "\r\nIconIndex=0\r\n");
            images.RefreshFolders(new[] { desktop }); game.IconLocation = null; images.Resolve(game);
            GameAssert(game.IconPng != null && game.IconSource == custom, "leitor de .url encontra arte pela URI mesmo com nome diferente");
            string lnkFolder = Path.Combine(folder, "links"); Directory.CreateDirectory(lnkFolder);
            object shell = null, shortcut = null;
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell"); shell = Activator.CreateInstance(type);
                shortcut = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(lnkFolder, "Outra descricao.lnk") });
                shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { game.RuntimePaths[0] });
                shortcut.GetType().InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { custom + ",0" });
                shortcut.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            { if (shortcut != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); if (shell != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
            images.RefreshFolders(new[] { lnkFolder }); images.Resolve(game);
            GameAssert(game.IconPng != null && game.IconSource == custom, "leitor de .lnk encontra icone pelo executavel exato");
            return fixture;
        }
    }
}
