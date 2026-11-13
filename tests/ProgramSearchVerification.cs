// Pesquisa de programas (ADR 0096): pontuacao, classificacao, historico, painel, teclado embutido, controle, teclado
// fisico, abertura e rotas de elevacao, tudo com substitutos. Nada real e aberto. O diagnostico real so le o indice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static KeyValuePair<string, string> ProgramPair(string name, string path) { return new KeyValuePair<string, string>(name, path); }
        static List<ProgramEntry> ExamplePrograms()
        {
            return ProgramIndex.Classify(new[] {
                ProgramPair("OBS Studio", @"{6D809377-6AF0-444B-8957-A3773F02200E}\obs-studio\bin\64bit\obs64.exe"),
                ProgramPair("OBS Studio", @"{6D809377-6AF0-444B-8957-A3773F02200E}\obs-studio\bin\64bit\obs64.exe"),
                ProgramPair("Discord", "com.squirrel.Discord.Discord"),
                ProgramPair("Calculadora", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
                ProgramPair("Visual Studio Code", @"C:\Exemplo\Code\Code.exe"),
                ProgramPair("Bloco de Notas", @"C:\Exemplo\Windows\notepad.exe"),
                ProgramPair("Éter Editor", @"C:\Exemplo\Eter\eter.exe"),
                ProgramPair("Neva", "steam://rungameid/1735040"),
                ProgramPair("Desinstalar OBS Studio", @"C:\Exemplo\obs\uninstall.exe"),
                ProgramPair("Site do OBS", "https://obsproject.com/"),
                ProgramPair("Leia-me", @"C:\Exemplo\obs\readme.txt"),
                ProgramPair("XGB_P", @"C:\Exemplo\XGB_P\XGB_P.exe"),
                ProgramPair("Ferramentas do Windows", "::{D20EA4E1-3957-11D2-A40B-0C5020524153}") });
        }
        static void ProgramAssert(bool condition, string message) { if (!condition) throw new Exception("Programas: " + message); }
        string ProgramFocus() { var c = Keyboard.FocusedElement as Control; return c == null ? null : c.Tag as string; }
        void ProgramPress(string tag) { var c = actions.Find(a => (string)a.Tag == tag); ProgramAssert(c != null, "controle ausente: " + tag); c.Focus();
            Activate(); }
        bool ProgramHas(string text) { return ProgramHasIn(body, text); }
        static bool ProgramHasIn(object node, string text)
        {
            var t = node as TextBlock; if (t != null && t.Text == text) return true;
            var d = node as DependencyObject; if (d == null) return false;
            foreach (object child in LogicalTreeHelper.GetChildren(d)) if (ProgramHasIn(child, text)) return true;
            return false;
        }
        string FirstProgramTag() { var c = actions.Find(a => (a.Tag as string ?? "").StartsWith("program-result-", StringComparison.Ordinal));
            return c == null ?
            null : (string)c.Tag; }

        void VerifyProgramMatching()
        {
            var all = ExamplePrograms();
            Func<string, string> first = delegate(string q) { var r = ProgramMatch.Search(all, q, null, 12); return r.Count == 0 ? null : r[0].Name; };
            ProgramAssert(all.Count == 7,
                "classificacao: 7 programas, sem repetidos, desinstalador, site, leia-me, a propria barra e pasta do shell (" + all.Count +
                ")");
            ProgramAssert(all.Find(p => p.Name == "Calculadora").Kind == "app" && all.Find(p => p.Name == "Discord").Kind == "program" &&
                all.Find(p => p.Name == "Neva").Kind == "steam" && all.Find(p => p.Name == "Neva").SteamAppId == "1735040",
                    "tipos: pacote, app Win32 com AUMID e jogo da Steam");
            var obs = all.Find(p => p.Name == "OBS Studio");
            ProgramAssert(obs.ExecutablePath != null && !obs.ExecutablePath.StartsWith("{") &&
                obs.ExecutablePath.EndsWith(@"\obs-studio\bin\64bit\obs64.exe") &&
                obs.Alias == "obs64" && obs.Target.StartsWith(@"shell:AppsFolder\{"), "pasta conhecida resolvida e alvo pelo Iniciar");
            ProgramAssert(all.Find(p => p.Name == "Calculadora").Alias == "WindowsCalculator" && all.Find(p => p.Name == "Neva").Target ==
                "steam://rungameid/1735040"
                , "apelido do pacote e alvo do jogo");
            ProgramAssert(ProgramMatch.Fold("Configurações").Length == "Configurações".Length && ProgramMatch.Fold("Configurações") == "configuracoes",
                "sem acentos, mesmo comprimento");
            ProgramAssert(first("calc") == "Calculadora" && first("eter") == "Éter Editor" && first("ÉTER") == "Éter Editor",
                "comeco do nome, sem acentos e sem caixa");
            ProgramAssert(first("vsc") == "Visual Studio Code" && first("notepad") == "Bloco de Notas" && first("calculator") == "Calculadora",
                "iniciais e apelidos"
                );
            ProgramAssert(first("cord") == "Discord" && first("dscrd") == "Discord" && first("xyz") == null && first("   ") == null,
                "trecho, letras em ordem, nada e vazio");
            ProgramAssert(first("studio") == "OBS Studio" && first("vis cod") == "Visual Studio Code", "comeco de palavra e varias palavras");
            ProgramAssert(ProgramMatch.MatchStart("stu", "OBS Studio") == 4 && ProgramMatch.MatchStart("vsc", "Visual Studio Code") == -1, "trecho destacado");
            ProgramAssert(ProgramMatch.Score("obs", "OBS Studio", "") > ProgramMatch.Score("obs", "Orbitas", "") && ProgramMatch.Score("obs",
                "Orbitas", "") > 0 &&
                ProgramMatch.Score("ob", "Orbitas", "") == 0,
                "comeco do nome vence letras soltas, que exigem tres letras em ordem");
            // Uso: frequencia e recencia desempatam a mesma pontuacao, sem passar na frente de um comeco de nome.
            var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc); var state = new ProgramHistoryState();
            var code = all.Find(p => p.Name == "Visual Studio Code");
            for (int i = 0; i < 3; i++) ProgramHistory.Record(state, code.Key, now);
ProgramAssert(ProgramMatch.Search(all, "studio", e => ProgramHistory.Boost(state, e.Key, now), 5)[0] == code, "uso sobe o programa na mesma pontuacao");
            ProgramAssert(ProgramMatch.Search(all, "v", e => ProgramHistory.Boost(state, e.Key, now), 5)[0] == code &&
                ProgramHistory.Count(state, code.Key) == 3,
                "contagem do uso");
            ProgramAssert(ProgramHistory.Boost(state, code.Key, now.AddDays(40)) == 24 && ProgramHistory.Boost(state, "nada", now) == 0, "recencia expira");
            for (int i = 0; i < 105; i++) ProgramHistory.Record(state, "k" + i, now.AddSeconds(i));
            ProgramAssert(state.Entries.Count == ProgramHistory.MaxEntries && state.Entries[0].Key == "k104", "historico limitado e mais recente primeiro");
            // Arquivo ilegivel: ignorado (com o .bak como reserva), nunca bloqueia.
            string path = Path.Combine(Output, "programas-historico-ilegivel.json"); var logged = new List<string>();
            File.WriteAllText(path, "{"); if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            ProgramAssert(ProgramHistory.Load(path, logged.Add).Entries.Count == 0 && logged.Count == 1, "historico ilegivel ignorado");
            GameFiles.Write(path + ".bak", state);
            ProgramAssert(ProgramHistory.Load(path, logged.Add).Entries.Count == ProgramHistory.MaxEntries, "reserva .bak usada");
            File.Delete(path); File.Delete(path + ".bak");
        }

        void VerifyPrograms()
        {
            VerifyProgramMatching();
        var read = programsRead; var windows = programWindowsRead; var shell = programShellOpen; var direct = programDirectOpen; var elevated = programElevated;
            var available = appAvailable; var switcher = switchApp; var now = programNow; string history = programHistoryPath;
            var opened = new List<string>(); var switched = new List<string>(); bool admin = true, shellWorks = true;
            var code = ExamplePrograms().Find(p => p.Name == "Visual Studio Code");
            GameEntry steamGame = null;
            try
            {
                programs = null; programHistory = null; programHistoryPath = Path.Combine(Output, "programas-historico-teste.json");
                if (File.Exists(programHistoryPath)) File.Delete(programHistoryPath);
                programsRead = ExamplePrograms;
                programWindowsRead = delegate { return new List<DesktopWindow> { new DesktopWindow { Handle = new IntPtr(77), ProcessId = 77,
                    AppName = "Code", Title =
                    "Code", ExecutablePath = code.ExecutablePath } }; };
                programShellOpen = delegate(string target, bool max) { opened.Add("explorer|" + target + "|" + max); return shellWorks; };
                programDirectOpen = delegate(string target, bool max) { if (target == "falha") throw new IOException("falha simulada");
                    opened.Add("direto|" + target +
                    "|" + max); };
                programElevated = delegate { return admin; };
                appAvailable = delegate { return true; }; switchApp = delegate(DesktopWindow w) { switched.Add(w.ExecutablePath); };
                int tick = 0; programNow = delegate { return new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc).AddMinutes(tick++); };

                // Home: Programas ao lado da Biblioteca; abre o painel sob o Inicio com o foco no teclado (ADR 0097).
                returnToHome = false; SetPanel(0); Show(); window.UpdateLayout();
                ProgramAssert(homeUtilities.Count == 10 && (string)homeUtilities[1].Tag == "utility-programs" && (string)homeUtilities[1].ToolTip
                    == "Programas",
                    "atalho Programas ao lado da Biblioteca");
                FocusHome("utility-programs"); ProgramAssert(utilitiesHint.Text == "Programas", "nome do atalho no titulo da secao");
                Activate();
                ProgramAssert(panel == ProgramsPanel && ((TextBlock)window.FindName("PanelTitle")).Text == "Programas" && returnToHome &&
                    rail[0].Background == Brush("SurfaceSelected") && ProgramFocus() == ProgramStartKey, "abrir pela Home com Inicio destacado e foco no Q");
                ProgramAssert(ProgramHas("TODOS · 7   ·   L2/R2 trocam de letra") && ProgramHas("Digite o nome do programa") && !ProgramHas("RECENTES"),
                    "lista completa sem recentes");
                ProgramAssert(ProgramHas("Programa · em execução"), "programa aberto marcado");
                window.UpdateLayout(); SaveRender("programas");

                // Teclado embutido: navegacao pela posicao, X digita, quadrado apaga, triangulo e espaco sem repetir.
                Navigate("BAIXO"); ProgramAssert(ProgramFocus() == "program-key-a", "Q para A");
                Navigate("CIMA"); Navigate("CIMA"); ProgramAssert(ProgramFocus() == "program-key-1", "Q para 1");
                Navigate("CIMA"); ProgramAssert(ProgramFocus() == "program-field", "1 para o campo");
actions.Find(c => (string)c.Tag == "program-key-m").Focus(); Navigate("BAIXO"); ProgramAssert(ProgramFocus() == "program-key-space", "M para Espaco");
            actions.Find(c => (string)c.Tag == "program-key-p").Focus(); Navigate("DIREITA"); ProgramAssert(ProgramFocus() == "program-key-p", "fim da linha");
                actions.Find(c => (string)c.Tag == ProgramStartKey).Focus(); Navigate("ESQUERDA");
                ProgramAssert(Keyboard.FocusedElement == rail[0] && panel == ProgramsPanel, "esquerda vai ao Inicio no trilho sem sair do painel");
                Navigate("DIREITA"); ProgramAssert(ProgramFocus() == ProgramStartKey, "do trilho de volta ao teclado");
                ProgramPress("program-key-o"); ProgramPress("program-key-b"); ProgramPress("program-key-s");
                ProgramAssert(programQuery == "obs" && programQueryText.Text == "obs" && ProgramHas("RESULTADOS · 1") && ProgramFocus() == "program-key-s",
                    "digitar pelo teclado embutido");
                ProgramAssert(FirstProgramTag() == "program-result-result-" + ExamplePrograms()[ExamplePrograms().FindIndex(p => p.Name == "OBS Studio")].Key,
                    "resultado OBS");
                window.UpdateLayout(); SaveRender("programas-busca");
                ProgramAssert(ProgramsButton("QUADRADO") && programQuery == "ob", "quadrado apaga");
                ProgramAssert(ProgramsButton("TRIANGULO") && ProgramsButton("TRIANGULO") && programQuery == "ob ", "triangulo e espaco, sem espaco duplo");
                ProgramsButton("QUADRADO"); ProgramPress("program-key-s"); ProgramAssert(programQuery == "obs", "voltar a obs");
                actions.Find(c => (string)c.Tag == "program-key-clear").Focus(); Navigate("BAIXO");
                ProgramAssert(ProgramFocus() == FirstProgramTag(), "do teclado para a lista pelo resultado, nao pelas opcoes");
                Navigate("DIREITA"); ProgramAssert(ProgramFocus().StartsWith("program-more-"), "opcoes ao lado do resultado");
                Navigate("CIMA"); ProgramAssert(ProgramFocus().StartsWith("program-key-"), "da lista de volta ao teclado");

                // Teclado fisico: letras pelo TextInput, apagar e espaco pelo OnKey; Q nao troca de painel aqui.
                var source = PresentationSource.FromVisual(window);
                Func<Key, KeyEventArgs> key = delegate(Key k) { return new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, k) { RoutedEvent =
                    Keyboard.PreviewKeyDownEvent
                    }; };
                var q = key(Key.Q); ProgramAssert(ProgramsKey(q) && !q.Handled && panel == ProgramsPanel, "letra fica para o texto, sem trocar de painel");
                var back = key(Key.Back); ProgramAssert(ProgramsKey(back) && back.Handled && programQuery == "ob", "Backspace apaga");
                window.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, window, "é")) { RoutedEvent =
                    TextCompositionManager.PreviewTextInputEvent });
                ProgramAssert(programQuery == "obé", "texto composto do teclado fisico");
                ProgramsKey(key(Key.Back)); ProgramsKey(key(Key.Down)); ProgramAssert(programQuery == "ob", "setas seguem para a navegacao");
                ProgramsKey(key(Key.Back)); ProgramsKey(key(Key.Back)); TypeProgram("obs");

                // Options abre o primeiro resultado. Barra elevada: pelo Explorer, sem herdar o administrador.
                ProgramAssert(ProgramsButton("OPTIONS") && opened.Count == 1 && opened[0] == "explorer|" + ExamplePrograms().Find(p => p.Name ==
                    "OBS Studio").Target +
                    "|False" &&
                    !window.IsVisible && FeedbackText() == "Abertura de OBS Studio solicitada.", "Options abre pelo Explorer e oculta a barra | "
                        + string.Join(";",
                        opened.ToArray()) + " | visivel=" + window.IsVisible + " | " + FeedbackText());
                ProgramAssert(File.Exists(programHistoryPath) && ProgramHistory.Load(programHistoryPath, null).Entries.Count == 1 &&
                    !File.ReadAllText(programHistoryPath).Contains("\"obs\""),
                    "uso gravado sem o texto digitado");
                Show(); ProgramAssert(panel == ProgramsPanel && programQuery == "obs", "reabrir mantem a pesquisa");
                shellWorks = false; OpenFirstProgram();
                ProgramAssert(opened.Count == 3 && opened[2].StartsWith("direto|") &&
                    FeedbackText().Contains("como administrador, porque o Explorer não respondeu"),
                    "sem Explorer: abre direto e avisa");
                shellWorks = true; admin = false; Show(); OpenFirstProgram();
                ProgramAssert(opened.Count == 4 && opened[3].StartsWith("direto|"), "barra sem elevacao abre direto");
                admin = true; Show();
                var obs = ExamplePrograms().Find(p => p.Name == "OBS Studio");
                programShellOpen = delegate { throw new IOException("falha simulada"); };
                OpenProgram(obs, false, false);
                ProgramAssert(window.IsVisible && FeedbackText() == "Não foi possível abrir OBS Studio." && ProgramHistory.Count(ProgramUses(), obs.Key) == 3,
                    "erro avisa, mantem a barra e nao conta uso");
                programShellOpen = delegate(string target, bool max) { opened.Add("explorer|" + target + "|" + max); return shellWorks; };

                // Opcoes: maximizado sempre; administrador so com a barra elevada e para programas Win32.
                ProgramPress(FirstProgramTag().Replace("program-result-", "program-more-"));
                ProgramAssert(ProgramFocus().StartsWith("program-option-max-") && ProgramHas("Abrir maximizado") &&
                    ProgramHas("Como administrador"), "opcoes do programa"
                    );
                ClearFeedback(); window.UpdateLayout(); SaveRender("programas-opcoes");
            Back(); ProgramAssert(programOptions == null && ProgramFocus().StartsWith("program-more-") && panel == ProgramsPanel, "circulo fecha as opcoes");
                ProgramPress(ProgramFocus()); ProgramPress(ProgramFocus());
                ProgramAssert(opened[opened.Count - 1].EndsWith("|True") && !window.IsVisible, "abrir maximizado");
                Show(); ProgramClear(); TypeProgram("calc");
            ProgramPress(FirstProgramTag().Replace("program-result-", "program-more-")); ProgramAssert(!ProgramHas("Como administrador"),
                "pacote sem administrador");
                Back(); admin = false; ProgramClear(); TypeProgram("obs"); ProgramPress(FirstProgramTag().Replace("program-result-", "program-more-"));
                ProgramAssert(!ProgramHas("Como administrador"), "barra sem elevacao sem administrador"); Back(); admin = true;
                ProgramPress(FirstProgramTag().Replace("program-result-", "program-more-"));
                ProgramPress(actions.Find(c => (c.Tag as string ?? "").StartsWith("program-option-admin-")).Tag as string);
                ProgramAssert(opened[opened.Count - 1].StartsWith("direto|") && !window.IsVisible, "administrador abre direto pela barra elevada, sem UAC");

                // Programa ja aberto: vai para a janela, sem abrir outra instancia.
                Show(); ProgramClear(); TypeProgram("visual"); int before = opened.Count;
                ProgramPress(FirstProgramTag()); CancelAppSwitch("verificacao");
                ProgramAssert(switched.Count == 1 && switched[0] == code.ExecutablePath && opened.Count == before, "programa aberto volta para a janela");

                // Jogo da Steam do catalogo: fluxo dos jogos pela Home; fora do catalogo, o protocolo pelo Explorer.
                Show(); ProgramClear(); TypeProgram("neva"); ProgramPress(FirstProgramTag());
                ProgramAssert(opened[opened.Count - 1] == "explorer|steam://rungameid/1735040|False", "jogo fora do catalogo pelo protocolo");
                if (gamesView != null)
                {
                    steamGame = new GameEntry { Key = "steam:verificacao:1735040", Name = "Neva", Source = "Steam", LaunchUri =
                        "steam://rungameid/1735040", Available = false
                        };
                    gamesView.Entries.Add(steamGame); before = opened.Count;
                    Show(); ProgramClear(); TypeProgram("neva"); ProgramPress(FirstProgramTag());
                    ProgramAssert(panel == 0 && opened.Count == before && (gameConfirmation == steamGame.Key ||
                        FeedbackText().StartsWith("O jogo está indisponível")),
                        "jogo do catalogo segue o fluxo dos jogos, com a confirmacao da Home");
                    if (gameConfirmation != null) CancelGame();
                }

                // Sem texto: recentes primeiro; circulo no teclado recolhe o teclado e vai aos resultados.
                Show(); SetPanel(0); FocusHome("utility-programs"); Activate(); ProgramAssert(programQuery == "" && programKeyboard,
                    "nova entrada pela Home comeca vazia e com teclado");
                ProgramAssert(ProgramHas("RECENTES") && FirstProgramTag() == "program-result-recent-steam:1735040" &&
                    actions.Exists(c => (string)c.Tag == "program-result-recent-" + obs.Key), "recentes no topo, o mais recente primeiro");
                actions.Find(c => (string)c.Tag == ProgramStartKey).Focus(); Back();
                ProgramAssert(!programKeyboard && !actions.Exists(c => (c.Tag as string ?? "").StartsWith("program-key-")) && ProgramFocus() ==
                    FirstProgramTag(),
                    "circulo recolhe o teclado");
                window.UpdateLayout(); SaveRender("programas-sem-teclado");
                ProgramAssert(!ProgramsButton("QUADRADO") && !ProgramsButton("TRIANGULO"), "sem teclado, quadrado e triangulo voltam ao normal");
                // L2/R2: primeiro programa da letra seguinte ou anterior na lista completa.
                actions.Find(c => (c.Tag as string ?? "").StartsWith("program-result-all-")).Focus();
                string firstLetter = ProgramLetter((Control)Keyboard.FocusedElement);
                ProgramAssert(ProgramsPage(1) && ProgramLetter((Control)Keyboard.FocusedElement) != firstLetter, "R2 vai a proxima letra");
                ProgramAssert(ProgramsPage(-1) && ProgramLetter((Control)Keyboard.FocusedElement) == firstLetter, "L2 volta a letra anterior");
                ProgramPress("program-field"); ProgramAssert(programKeyboard && ProgramFocus() == ProgramStartKey, "X no campo mostra o teclado");
                TypeProgram("o"); ProgramAssert(!ProgramsPage(1), "com texto, L2/R2 paginam normalmente");
                // Circulo fora do teclado: volta a Home no atalho.
                actions.Find(c => (string)c.Tag == ProgramStartKey).Focus(); Back(); Back();
                ProgramAssert(panel == 0 && !returnToHome && ProgramFocus() == "utility-programs", "circulo volta a Home no atalho Programas");

                // Leitura que falha mantem a lista; sem lista, mostra que nada foi encontrado.
                programsRead = delegate { throw new InvalidOperationException("leitura simulada"); };
                SetPanel(ProgramsPanel); ProgramAssert(ProgramHas("TODOS · 7   ·   L2/R2 trocam de letra"), "leitura falha mantem a lista anterior");
                programs = null; SetPanel(0); SetPanel(ProgramsPanel); ProgramAssert(ProgramHas("TODOS · 0   ·   L2/R2 trocam de letra") && ProgramHas(
                    "Nenhum programa encontrado no Menu Iniciar."), "sem lista");
                TypeProgram("x"); ProgramAssert(ProgramHas("NENHUM PROGRAMA ENCONTRADO") && ProgramsButton("OPTIONS") && FeedbackText() ==
                    "Nenhum programa para abrir.",
                    "nada para abrir");
                File.WriteAllText(Path.Combine(Output, "programas-verificacao.txt"),
                    "OK: pontuacao (comeco do nome, palavras, iniciais, trecho, letras em ordem, apelido do executavel/pa" +
                        "cote, sem acentos), uso por frequencia/recencia, " +
"historico limitado, sem o texto digitado e tolerante a arquivo ilegivel; classificacao do Iniciar (p" +"acote, AUMID, pasta conhecida, jogo Steam; " +
"sem desinstaladores, sites, leia-me, a propria barra e pastas do shell); atalho Programas depois de " +"Lojas, painel sob o Inicio, teclado embutido " +
"(X, quadrado, triangulo, Options, navegacao pela posicao, trilho), teclado fisico (TextInput, Backsp" +"ace, Q sem trocar de painel), resultados, " +
                    "opcoes (maximizado, administrador so elevado e Win32), abertura pelo Explorer com a barra elevada, d" +
                        "ireta sem elevacao, sem Explorer com aviso, erro, " +
                    "programa aberto volta a janela, jogo Steam pelo fluxo dos jogos, recentes, recolher teclado, L2/R2 p" +
                        "or letra, volta a Home, leitura falha e lista vazia. " +
        "Shell, janelas e aberturas substituidos; nada real aberto. Nao confirma o Explorer no modo Xbox nem " +"a janela aberta na frente.\r\n",
            Encoding.UTF8);
            }
            finally
            {
                if (steamGame != null && gamesView != null) gamesView.Entries.Remove(steamGame);
                programsRead = read; programWindowsRead = windows; programShellOpen = shell; programDirectOpen = direct; programElevated = elevated;
                appAvailable = available; switchApp = switcher; programNow = now;
                if (File.Exists(programHistoryPath)) File.Delete(programHistoryPath);
                if (File.Exists(programHistoryPath + ".bak")) File.Delete(programHistoryPath + ".bak");
                programHistoryPath = history; programHistory = null; programs = null; programWindows = new List<DesktopWindow>();
                returnToHome = false; ClearFeedback(); SetPanel(0);
            }
        }

        // Diagnostico real so de leitura: indice do Iniciar, tempos, tipos, icones e a area de trabalho do shell (sem executar
        // nada). Nomes e a tela ficam so em bin.
        public void VerifyProgramsLive()
        {
            var watch = Stopwatch.StartNew();
            var found = ProgramIndex.Read(); long indexMs = watch.ElapsedMilliseconds;
            watch.Restart(); int icons = 0, sample = Math.Min(found.Count, 20);
            for (int i = 0; i < sample; i++) { found[i].IconPng = GameStores.ShellIcon(found[i].IconSource); found[i].IconRead = true; if
                (found[i].IconPng != null)
                icons++; }
            long iconMs = watch.ElapsedMilliseconds;
            watch.Restart(); bool shell = ProgramLauncher.ShellAvailable(); long shellMs = watch.ElapsedMilliseconds;
            var lines = new List<string> {
                "programas no indice: " + found.Count + " | " + indexMs + " ms",
                "  Win32: " + found.FindAll(p => p.Kind == "program").Count + " | pacotes: " + found.FindAll(p => p.Kind == "app").Count + " | jogos Steam: " +
                    found.FindAll(p => p.Kind == "steam").Count,
                "icones: " + icons + " de " + sample + " | " + iconMs + " ms",
                "area de trabalho do shell para abrir sem elevacao: " + (shell ? "disponivel" : "indisponivel") + " | " + shellMs + " ms (nada executado)",
                "barra elevada: " + LaunchSettings.Elevated + " | modo Xbox: " + (xboxMode() == true ? "sim" : xboxMode() == false ? "nao" : "indefinido"),
                "Nenhum programa aberto." };
            foreach (var p in found) lines.Add("  " + p.Kind + " | " + p.Name + " | " + p.ParsingName);
            programsRead = delegate { return found; };
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000; window.Show();
            programs = found; returnToHome = false; SetPanel(ProgramsPanel); window.UpdateLayout(); SaveRender("programas-reais");
            programs = null; SetPanel(0); window.Hide();
            File.WriteAllLines(Path.Combine(Output, "programas-reais.txt"), lines.ToArray(), Encoding.UTF8);
            WriteLog("programas: leitura real concluida, nada aberto | " + found.Count + " | " + indexMs + " ms | shell " + shell);
        }
    }
}
