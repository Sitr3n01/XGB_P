// Leitor do Xbox (ADR 0117) com pacotes de teste em pasta temporaria; nada e aberto nem instalado.
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
        static XboxPackage ExampleXboxPackage(string folder, string name, string config, string manifestId)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "MicrosoftGame.config"), config, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "AppxManifest.xml"),
                "<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Applications>" +
                "<Application Id=\"" + manifestId + "\" Executable=\"GameLaunchHelper.exe\" /></Applications></Package>", Encoding.UTF8);
            return new XboxPackage { FullName = name + "_1.0.0.0_x64__abcdefghjkmnp", Root = folder, DisplayName = "Nome do pacote" };
        }
        void VerifyXboxGames()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) { checks++; if (!ok) throw new Exception("Jogos do Xbox: " + message); };
            string test = Path.Combine(Output, "xbox-verificacao"); if (Directory.Exists(test)) Directory.Delete(test, true);
            try
            {
                string root = Path.Combine(test, "a"), real = Path.Combine(test, "real");
                var package = ExampleXboxPackage(root, "Estudio.Jogo",
                    "<Game configVersion=\"1\"><ShellVisuals DefaultDisplayName=\"Jogo A\" Square480x480Logo=\"Logo.png\" StoreLogo=\"Loja.png\"/>" +
                    "<ExecutableList><Executable Name=\"bin\\jogo.exe\" TargetDeviceFamily=\"PC\" Id=\"Principal\"/><Executable " +
                        "Name=\"console.exe\" TargetDeviceFamily=\"Scarlett\" Id=\"Principal\"/>" +
                    "<Executable Name=\"..\\fora.exe\" Id=\"Principal\"/><Executable Name=\"dev.exe\" IsDevOnly=\"true\" " +
                        "Id=\"Principal\"/><Executable Name=\"outro.exe\" Id=\"SemManifesto\"/></ExecutableList></Game>", "Principal");
                package.Aliases = new[] { real, root + "\\" };
                Directory.CreateDirectory(Path.Combine(root, "bin")); string exe = Path.Combine(root, "bin", "jogo.exe"); File.WriteAllText(exe, "x");
                File.WriteAllText(Path.Combine(root, "Loja.scale-200.png"), "x");
                var games = XboxCatalog.Read(package);
                check(games.Count == 1, "um jogo por identificador: " + games.Count);
                var game = games[0];
                check(game.Key == "xbox:Estudio.Jogo_abcdefghjkmnp!Principal" && game.LaunchApp == "Estudio.Jogo_abcdefghjkmnp!Principal" &&
                    game.Name == "Jogo A" &&
                    game.Kind == "Game" && game.Source == "Xbox", "chave, nome e identificador de abertura");
                check(game.RuntimePaths.Length == 2 && GameFiles.Same(game.RuntimePaths[0], exe) && GameFiles.Same(game.RuntimePaths[1],
                    Path.Combine(real, "bin", "jogo.exe")),
                    "caminho do pacote e o real, sem repetir nem aceitar console, fora da pasta ou desenvolvimento");
                check(GameFiles.Same(game.IconLocation, Path.Combine(root, "Loja.scale-200.png")), "icone em escala quando o nome exato nao existe");
                check(SteamGameContext.AppId(game.Key) == null, "jogo do Xbox nao entra no contexto da Steam");
                check(XboxCatalog.Family("a_b") == null && XboxCatalog.Family(null) == null && XboxCatalog.Family("N_1_x64__curto") == null,
                    "nome de pacote invalido recusado");

                var state = new GameCatalogState(); var settings = new GameSettings();
                check(XboxCatalog.Append(state, settings, new[] { package, package }).Length == 0 && state.Entries.Count == 1 &&
                    state.Entries[0].Available, "jogo disponivel e sem duplicar");
                File.Delete(exe); state = new GameCatalogState(); XboxCatalog.Append(state, settings, new[] { package });
                check(!state.Entries[0].Available, "sem executavel fica indisponivel"); File.WriteAllText(exe, "x");
                state = new GameCatalogState(); state.Entries.Add(new GameEntry { Key = "shortcut:1:2", Kind = "Game", RuntimePaths = new[] { exe } });
                XboxCatalog.Append(state, settings, new[] { package }); check(state.Entries.Count == 1, "atalho existente do mesmo executavel prevalece");
                state = new GameCatalogState(); settings.Entries.Add(new GameMapping { Key = game.Key, Hidden = true });
                XboxCatalog.Append(state, settings, new[] { package }); check(state.Entries.Count == 0, "jogo oculto pelo cadastro");
                settings.Entries[0].Hidden = false; settings.Entries[0].Name = "Meu nome"; state = new GameCatalogState();
                XboxCatalog.Append(state, settings, new[] { package }); check(state.Entries[0].Name == "Meu nome", "nome do cadastro");

                var broken = ExampleXboxPackage(Path.Combine(test, "b"), "Estudio.Ruim", "<Game><Executable", "Game");
                var dtd = ExampleXboxPackage(Path.Combine(test, "c"), "Estudio.Dtd", "<!DOCTYPE Game [<!ENTITY a \"b\">]><Game/>", "Game");
                var extra = ExampleXboxPackage(Path.Combine(test, "d"), "Estudio.Extra", "<Game configVersion=\"1\"/>", "Game");
                state = new GameCatalogState(); string problem = XboxCatalog.Append(state, new GameSettings(), new[] { broken, dtd, extra, package });
                check(state.Entries.Count == 1 && problem.Contains("Estudio.Ruim") && problem.Contains("Estudio.Dtd") &&
                    !problem.Contains("Estudio.Extra"), "pacote ilegivel nao derruba os demais");
                var resource = ExampleXboxPackage(Path.Combine(test, "e"), "Estudio.Recurso",
                    "<Game><ShellVisuals DefaultDisplayName=\"ms-resource:Nome\"/><ExecutableList><Executable " +
                    "Name=\"r.exe\"/></ExecutableList></Game>", "Game");
                check(XboxCatalog.Read(resource)[0].Name == "Nome do pacote" && XboxCatalog.Read(resource)[0].LaunchApp.EndsWith("!Game",
                    StringComparison.Ordinal), "nome por recurso e identificador padrao");

                // Sessao: caminho conhecido, caminho desconhecido com a identidade do pacote e auxiliares.
                state = new GameCatalogState(); XboxCatalog.Append(state, new GameSettings(), new[] { package });
                Func<string, string, GameProcess> process = delegate(string path, string full) {
                    return new GameProcess { Pid = 77, Created = 1000, Path = path, Name = Path.GetFileName(path), PackageFullName = full }; };
                Func<GameProcess, List<GameProcess>> match = delegate(GameProcess p) { return new GameProcessIndex(state.Entries, new
                    List<GameProcess> { p }).For(game.Key); };
                check(match(process(Path.Combine(real, "bin", "jogo.exe"), null)).Count == 1, "processo pelo caminho real");
                check(match(process(@"Z:\volume\jogo.exe", package.FullName)).Count == 1, "processo pela identidade do pacote");
                check(match(process(@"C:\Windows\navegador.exe", package.FullName)).Count == 0, "filho com a identidade herdada nao conta");
                check(match(process(@"Z:\volume\jogo.exe", "Outro_1.0.0.0_x64__abcdefghjkmnp")).Count == 0, "outro pacote nao conta");
                check(match(process(Path.Combine(root, "gamelaunchhelper.exe"), package.FullName)).Count == 0, "auxiliar de abertura nao conta como jogo");
                var history = new GameHistory(Path.Combine(test, "history.json")); var sessions = new GameSessions(history);
                var running = new List<GameProcess> { process(@"Z:\volume\jogo.exe", package.FullName) }; var now = DateTime.UtcNow;
                Func<DesktopWindow, bool> valid = delegate { return true; };
                sessions.Update(state, running, new List<DesktopWindow>(), 0, now, valid);
                var view = sessions.Update(state, running, new List<DesktopWindow>(), 0, now.AddSeconds(2), valid);
                check(view.Activity(game.Key) != null && view.Current.Key == game.Key && history.Last(game.Key) > 0,
                    "jogo aberto aparece e entra no historico");

                string opened = null; bool viaShell = true;
                Func<string, bool, bool> shell = delegate(string t, bool m) { opened = "shell " + t; return viaShell; }; Action<string, bool>
                    direct = delegate(string t, bool m) { opened = "direto " + t; };
                LaunchXboxGame(game.LaunchApp, true, shell, direct); check(opened == @"shell shell:AppsFolder\" + game.LaunchApp,
                    "barra elevada abre pelo explorer");
                viaShell = false; LaunchXboxGame(game.LaunchApp, true, shell, direct); check(opened.StartsWith("direto",
                    StringComparison.Ordinal), "sem explorer abre direto");
                LaunchXboxGame(game.LaunchApp, false, shell, direct); check(opened.StartsWith("direto", StringComparison.Ordinal), "barra comum abre direto");
                int refused = 0; foreach (string bad in new[] { null, "", "semexclamacao", "a!", "!b", "a!b c", @"a!..\b", "a!b\"" })
                    try { LaunchXboxGame(bad, false, shell, direct); } catch (InvalidOperationException) { refused++; }
                check(refused == 8, "identificador invalido recusado: " + refused);
                bool failed = false; try { LaunchXboxGame(game.LaunchApp, false, shell, delegate { throw new IOException("recusado"); }); } catch
                    (InvalidOperationException) { failed = true; }
                check(failed, "recusa do Windows vira erro na Home");
            }
            finally { try { Directory.Delete(test, true); } catch (IOException) { } }
            File.WriteAllText(Path.Combine(Output, "jogos-xbox-verificacao.txt"), "Jogos do Xbox: " + checks + " verificacoes aprovadas.\r\n" +
                "Pacotes de teste em pasta temporaria; nenhum jogo aberto, nenhum pacote instalado.\r\n", Encoding.UTF8);
        }
    }
}
