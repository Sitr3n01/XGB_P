// Pesquisa de programas (ADR 0096): painel aberto pelo Acesso rapido da Home e mostrado sob o Inicio no trilho, como Lojas.
// Campo, teclado embutido e resultados. O teclado virtual (ADR 0052) digita no app da frente e fica suspenso com a barra
// aberta, entao o painel tem o proprio teclado, com os mesmos botoes: X digita, quadrado apaga, triangulo e espaco,
// Options abre o primeiro resultado e circulo leva aos resultados. O indice e lido numa thread STA a cada entrada no
// painel; abrir so por escolha do usuario. O log nunca registra o texto digitado.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int ProgramsPanel = 10, ProgramMaxResults = 12, ProgramQueryMax = 40;
        const string ProgramStartKey = "program-key-q";
        static readonly string[] ProgramKeyRows = { "1234567890", "qwertyuiop", "asdfghjklç", "zxcvbnm-.'" };
        List<ProgramEntry> programs;
        List<DesktopWindow> programWindows = new List<DesktopWindow>();
        ProgramHistoryState programHistory;
        string programQuery = "", programOptions, programSignature;
        bool programKeyboard = true, programsBusy, programIconsBusy, programLaunchBusy;
        StackPanel programResults;
        TextBlock programQueryText;
        readonly List<List<Control>> programRows = new List<List<Control>>();
        readonly Dictionary<string, ProgramEntry> programByTag = new Dictionary<string, ProgramEntry>();
        // Substituiveis: a verificacao nunca le o shell real nem abre programas.
        Func<List<ProgramEntry>> programsRead = ProgramIndex.Read;
        Func<List<DesktopWindow>> programWindowsRead;
        Func<string, bool, bool> programShellOpen = ProgramLauncher.OpenThroughShell;
        Action<string, bool> programDirectOpen = ProgramLauncher.OpenDirect;
        Func<bool> programElevated = delegate { return LaunchSettings.Elevated; };
        Func<ProgramEntry, byte[]> programIcon = delegate(ProgramEntry e) { return GameStores.ShellIcon(e.IconSource); };
        Func<DateTime> programNow = delegate { return DateTime.UtcNow; };
        string programHistoryPath = ProgramHistory.FilePath;

        void StartPrograms()
        {
            programWindowsRead = delegate { return readOpenApps(); };
            if (verify)
            {
                programsRead = delegate { return new List<ProgramEntry>(); }; programWindowsRead = delegate { return new List<DesktopWindow>(); };
                programShellOpen = delegate { return true; }; programDirectOpen = delegate { }; programIcon = delegate { return null; };
                programHistoryPath = System.IO.Path.Combine(Output, "programas-historico-teste.json");
            }
            // Teclado fisico: letras e simbolos chegam compostos (acentos do ABNT2); espaco, apagar e Enter vem do OnKey.
            window.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e) {
                if (!ProgramsTyping()) return;
                if (e.Text != " ") TypeProgram(e.Text);
                e.Handled = true;
            };
        }
        bool ProgramsTyping() { return panel == ProgramsPanel && window.IsVisible && !motionClosing && !NavigationHasFocus(); }
        ProgramHistoryState ProgramUses() { if (programHistory == null) programHistory = ProgramHistory.Load(programHistoryPath, WriteLog);
            return programHistory;
            }
        static bool ProgramResultTag(string tag)
        {
            return tag != null && (tag.StartsWith("program-result-", StringComparison.Ordinal) || tag.StartsWith("program-more-", StringComparison.Ordinal) ||
                tag.StartsWith("program-option-", StringComparison.Ordinal));
        }
        // Entrar pela Home sempre comeca uma pesquisa nova; reabrir a barra no painel mantem a anterior.
        void ResetProgramSearch() { programQuery = ""; programOptions = null; programKeyboard = true; }

        Button ProgramButton(object content, string tag, Action action)
        {
            var button = new Button { Content = content, Tag = tag, Style = (Style)window.FindResource("Action") };
            WireMotion(button);
            button.Click += delegate { InvokeSoundAction(button, action); };
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            actions.Add(button);
            return button;
        }
        void BuildPrograms()
        {
            programRows.Clear();
            var field = new Grid();
            field.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); field.ColumnDefinitions.Add(new ColumnDefinition());
            var glyph = Symbol("", 18); glyph.Foreground = Brush("Muted"); field.Children.Add(glyph);
            programQueryText = Text("", 20, "TextPrimary"); programQueryText.TextWrapping = TextWrapping.NoWrap;
            programQueryText.TextTrimming = TextTrimming.CharacterEllipsis; programQueryText.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(programQueryText, 1); field.Children.Add(programQueryText);
            var fieldButton = ProgramButton(field, "program-field", ShowProgramKeyboard);
            fieldButton.Padding = new Thickness(14, 10, 14, 10); fieldButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            System.Windows.Automation.AutomationProperties.SetName(fieldButton, "Pesquisar programas");
            body.Children.Add(fieldButton); programRows.Add(new List<Control> { fieldButton });
            UpdateProgramField();
            if (programKeyboard) BuildProgramKeyboard();
            programResults = new StackPanel();
            body.Children.Add(programResults);
            BuildProgramResults();
            programSignature = ProgramsSignature();
        }
        void UpdateProgramField()
        {
            if (programQueryText == null) return;
            programQueryText.Text = programQuery.Length == 0 ? "Digite o nome do programa" : programQuery;
            programQueryText.Foreground = Brush(programQuery.Length == 0 ? "Muted" : "TextPrimary");
        }

        // Teclas de 1/10 da largura do painel, no estilo dos botoes da barra; letras maiusculas na tela, minusculas no texto.
        void BuildProgramKeyboard()
        {
            var keys = new Grid { Margin = new Thickness(0, 2, 0, 12) };
            for (int c = 0; c < 10; c++) keys.ColumnDefinitions.Add(new ColumnDefinition());
            for (int r = 0; r < ProgramKeyRows.Length; r++)
            {
                keys.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var row = new List<Control>();
                for (int c = 0; c < ProgramKeyRows[r].Length; c++)
                {
                    string key = ProgramKeyRows[r][c].ToString();
                    var button = ProgramKey(keys, r, c, 1, key.ToUpperInvariant(), "program-key-" + key, delegate { TypeProgram(key); ActionSound("mover"); });
                    System.Windows.Automation.AutomationProperties.SetName(button, key);
                    row.Add(button);
                }
                programRows.Add(row);
            }
            keys.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int last = ProgramKeyRows.Length;
            programRows.Add(new List<Control> {
                ProgramKey(keys, last, 0, 3, "Apagar", "program-key-backspace", delegate { ProgramBackspace(); ActionSound("mover"); }, "square"),
                ProgramKey(keys, last, 3, 4, "Espaço", "program-key-space", delegate { TypeProgram(" "); ActionSound("mover"); }, "triangle"),
                ProgramKey(keys, last, 7, 3, "Limpar", "program-key-clear", delegate { ProgramClear(); ActionSound("mover"); }) });
            body.Children.Add(keys);
        }
        Button ProgramKey(Grid keys, int row, int column, int span, string label, string tag, Action action, string icon = null)
        {
            var text = Text(label, span == 1 ? 18 : 14, "TextPrimary"); text.TextWrapping = TextWrapping.NoWrap; text.HorizontalAlignment =
                HorizontalAlignment.Center;
            FrameworkElement content = text;
            if (icon != null)
            {
                // Mesmos vetores do teclado virtual; separados do texto para manter tamanho e alinhamento.
                var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment =
                    VerticalAlignment.Center };
                var badge = KbIcon(icon, 20); badge.Margin = new Thickness(0, 0, 8, 0); badge.VerticalAlignment = VerticalAlignment.Center;
                group.Children.Add(badge); group.Children.Add(text); content = group;
            }
            var button = ProgramButton(content, tag, action);
            button.Padding = new Thickness(0); button.Margin = new Thickness(2); button.Height = 42;
            button.HorizontalContentAlignment = HorizontalAlignment.Center; button.ToolTip = label;
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            Grid.SetRow(button, row); Grid.SetColumn(button, column); Grid.SetColumnSpan(button, span); keys.Children.Add(button);
            return button;
        }

        // Com texto: ate 12 resultados pela pontuacao e pelo uso. Sem texto: recentes e todos de A a Z.
        void BuildProgramResults()
        {
            programResults.Children.Clear(); programByTag.Clear();
            actions.RemoveAll(c => ProgramResultTag(c.Tag as string));
            programRows.RemoveAll(r => r.Count > 0 && ProgramResultTag(r[0].Tag as string));
            // Com o teclado aberto, a altura nao acompanha cada letra.
            programResults.MinHeight = programKeyboard ? 330 : 0;
            if (programs == null) { programResults.Children.Add(ProgramNote("Procurando programas…")); return; }
            string query = programQuery.Trim();
            if (query.Length > 0)
            {
                DateTime now = programNow();
                var found = ProgramMatch.Search(programs, query, e => ProgramHistory.Boost(ProgramUses(), e.Key, now), ProgramMaxResults);
                ProgramCaption(found.Count == 0 ? "NENHUM PROGRAMA ENCONTRADO" : "RESULTADOS · " + found.Count);
                if (found.Count == 0) programResults.Children.Add(ProgramNote("Confira o nome ou apague algumas letras."));
                foreach (var entry in found) AddProgramRow(entry, "result");
                return;
            }
            var recent = RecentPrograms();
            if (recent.Count > 0) { ProgramCaption("RECENTES"); foreach (var entry in recent) AddProgramRow(entry, "recent"); }
            ProgramCaption("TODOS · " + programs.Count + "   ·   L2/R2 trocam de letra");
            if (programs.Count == 0) programResults.Children.Add(ProgramNote("Nenhum programa encontrado no Menu Iniciar."));
            foreach (var entry in programs) AddProgramRow(entry, "all");
        }
        List<ProgramEntry> RecentPrograms()
        {
            var recent = new List<ProgramEntry>();
            foreach (var use in ProgramUses().Entries)
            {
                if (recent.Count >= ProgramHistory.RecentCount) break;
                var entry = programs.Find(p => p.Key == use.Key);
                if (entry != null) recent.Add(entry);
            }
            return recent;
        }
        void ProgramCaption(string text) { var t = Text(text, 14, "Muted"); t.Margin = new Thickness(0, 10, 0, 8); programResults.Children.Add(t); }
        TextBlock ProgramNote(string text) { var t = Text(text, 14, "Muted"); t.Margin = new Thickness(2, 0, 0, 8); return t; }
        void AddProgramRow(ProgramEntry entry, string section)
        {
            string id = section + "-" + entry.Key;
            var running = ProgramWindow(entry);
            string detail = (entry.Kind == "app" ? "App" : entry.Kind == "steam" ? "Jogo da Steam" : "Programa") + (running != null ? " · em execução" : "");
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); content.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement icon = PngImage(entry.IconPng, 28) ?? (FrameworkElement)Symbol(entry.Kind == "steam" ? "" : "", 18);
            content.Children.Add(new Border { Background = Brush("SurfaceHover"), CornerRadius = new CornerRadius(6), Height = 40, Child = icon });
            var labels = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(ProgramName(entry.Name));
            var note = Text(detail, 13, "Muted"); note.TextWrapping = TextWrapping.NoWrap; note.TextTrimming = TextTrimming.CharacterEllipsis;
            labels.Children.Add(note); Grid.SetColumn(labels, 1); content.Children.Add(labels);
            var open = ProgramButton(content, "program-result-" + id, delegate { OpenProgram(entry, false, false); });
            open.Padding = new Thickness(8, 6, 8, 6); open.Margin = new Thickness(0, 0, 0, 6); open.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            open.ToolTip = entry.Name + " · " + detail; System.Windows.Automation.AutomationProperties.SetName(open, entry.Name + " · " + detail);
            var more = ProgramButton(Symbol("", 18), "program-more-" + id, delegate { ToggleProgramOptions(id); });
        more.Width = 52; more.Padding = new Thickness(0); more.Margin = new Thickness(6, 0, 0, 6); more.HorizontalContentAlignment = HorizontalAlignment.Center;
            more.ToolTip = "Mais opções de " + entry.Name; System.Windows.Automation.AutomationProperties.SetName(more, "Mais opções de " + entry.Name);
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition()); line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(more, 1); line.Children.Add(open); line.Children.Add(more);
            programResults.Children.Add(line); programRows.Add(new List<Control> { open, more });
            programByTag["program-result-" + id] = entry;
            if (programOptions == id) AddProgramOptions(entry, id);
        }
        // Trecho que combinou em negrito; iniciais e letras soltas ficam sem destaque.
        TextBlock ProgramName(string name)
        {
            var text = Text("", 18, "TextPrimary"); text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
            string query = programQuery.Trim();
            int start = query.Length == 0 ? -1 : ProgramMatch.MatchStart(query, name);
            if (start < 0 || start + query.Length > name.Length) { text.Text = name; return text; }
            text.Inlines.Add(new Run(name.Substring(0, start)));
            text.Inlines.Add(new Run(name.Substring(start, query.Length)) { FontWeight = FontWeights.Bold });
            text.Inlines.Add(new Run(name.Substring(start + query.Length)));
            return text;
        }
        // Administrador so com a barra elevada: abre direto, sem UAC (a tela segura nao responde ao controle).
        void AddProgramOptions(ProgramEntry entry, string id)
        {
            var grid = new Grid { Margin = new Thickness(16, 0, 0, 6) };
            var row = new List<Control>();
            var options = new List<KeyValuePair<string, Action>> { new KeyValuePair<string, Action>("Abrir maximizado", delegate {
                OpenProgram(entry, true, false); })
                };
            if (programElevated() && entry.Kind == "program")
                options.Add(new KeyValuePair<string, Action>("Como administrador", delegate { OpenProgram(entry, false, true); }));
            for (int i = 0; i < options.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var button = PanelButton(grid, options[i].Key, "program-option-" + (i == 0 ? "max-" : "admin-") + id, options[i].Value);
                button.Margin = new Thickness(i == 0 ? 0 : 3, 0, i == options.Count - 1 ? 0 : 3, 0); Grid.SetColumn(button, i); row.Add(button);
            }
            programResults.Children.Add(grid); programRows.Add(row);
        }
        DesktopWindow ProgramWindow(ProgramEntry entry)
        {
            return programWindows.Find(w => entry.ExecutablePath != null && GameFiles.Same(w.ExecutablePath, entry.ExecutablePath) ||
                !string.IsNullOrEmpty(w.AppUserModelId) && string.Equals(w.AppUserModelId, entry.ParsingName, StringComparison.OrdinalIgnoreCase));
        }

        void TypeProgram(string text)
        {
            bool changed = false;
            foreach (char c in text ?? "")
            {
            if (char.IsControl(c) || programQuery.Length >= ProgramQueryMax || c == ' ' && (programQuery.Length == 0 || programQuery.EndsWith(" "))) continue;
                programQuery += c; changed = true;
            }
            if (changed) ProgramQueryChanged();
        }
        void ProgramBackspace() { if (programQuery.Length == 0) return; programQuery = programQuery.Substring(0, programQuery.Length - 1);
            ProgramQueryChanged();
            }
        void ProgramClear() { if (programQuery.Length == 0) return; programQuery = ""; ProgramQueryChanged(); }
        void ProgramQueryChanged() { programOptions = null; UpdateProgramField(); RebuildProgramResults(); }

        void ShowProgramKeyboard()
        {
            ActionSound("mover");
            if (!programKeyboard) { programKeyboard = true; RebuildPrograms(ProgramStartKey); return; }
            var start = actions.Find(c => (string)c.Tag == ProgramStartKey); if (start != null) start.Focus();
        }
        // Circulo: fecha as opcoes de um resultado; no teclado ou no campo, recolhe o teclado e vai aos resultados.
        bool ProgramsBack()
        {
            if (panel != ProgramsPanel) return false;
            if (programOptions != null)
            {
                string id = programOptions; programOptions = null; RebuildProgramResults();
                var more = actions.Find(c => (string)c.Tag == "program-more-" + id); if (more != null) more.Focus();
                return true;
            }
            var focused = Keyboard.FocusedElement as Control; string tag = focused == null ? null : focused.Tag as string;
            if (!programKeyboard || tag == null || tag != "program-field" && !tag.StartsWith("program-key-", StringComparison.Ordinal)) return false;
            programKeyboard = false; RebuildPrograms(null);
            return true;
        }
        Control ProgramsEntryFocus()
        {
            return (programKeyboard ? actions.Find(c => (string)c.Tag == ProgramStartKey) : null) ??
    actions.Find(c => (c.Tag as string ?? "").StartsWith("program-result-", StringComparison.Ordinal)) ?? actions.Find(c => (string)c.Tag == "program-field");
        }
        void RebuildPrograms(string focusTag)
        {
            double height = ReleaseSidebarHeight();
            CancelElementMotion(body); body.Children.Clear(); actions.Clear(); flyout = null; flyoutSelector = null; panelFill = null;
            BuildPrograms(); window.UpdateLayout();
            var target = (focusTag == null ? null : actions.Find(c => (string)c.Tag == focusTag && c.IsEnabled)) ?? ProgramsEntryFocus();
            if (target != null) target.Focus();
            MotionHeight(height);
        }
        // So os resultados mudam: teclado e campo continuam com o foco onde estavam.
        void RebuildProgramResults()
        {
            if (programResults == null || panel != ProgramsPanel) return;
            var focused = Keyboard.FocusedElement as Control;
            string tag = focused == null ? null : focused.Tag as string;
            double height = ReleaseSidebarHeight();
            BuildProgramResults(); window.UpdateLayout();
            if (ProgramResultTag(tag))
            {
                var target = actions.Find(c => (string)c.Tag == tag && c.IsEnabled) ?? ProgramsEntryFocus();
                if (target != null) target.Focus();
            }
            MotionHeight(height);
            programSignature = ProgramsSignature();
        }
        string ProgramsSignature()
        {
            if (programs == null) return "lendo";
            var text = new System.Text.StringBuilder();
            foreach (var p in programs) text.Append(p.Key).Append(p.IconPng == null ? "|" : "|i").Append(ProgramWindow(p) == null ? ";" : "+;");
            return text.ToString();
        }

        bool NavigatePrograms(Control focused, string key)
        {
            if (panel != ProgramsPanel) return false;
            int row = programRows.FindIndex(r => r.Contains(focused));
            if (row < 0) return false;
            var items = programRows[row]; int col = items.IndexOf(focused);
            if (key == "ESQUERDA" || key == "DIREITA")
            {
                int next = col + (key == "DIREITA" ? 1 : -1);
                if (next >= 0 && next < items.Count) items[next].Focus(); else if (key == "ESQUERDA") FocusNavigation(0);
                return true;
            }
            int step = key == "CIMA" ? -1 : 1;
            double x = ProgramCenter(focused);
            for (int target = row + step; target >= 0 && target < programRows.Count; target += step)
            {
                // Do teclado para a lista, entra sempre pelo resultado, nunca pelo botao de opcoes.
                var candidates = programRows[target];
                if (!ProgramResultTag(focused.Tag as string) && ProgramResultTag(candidates[0].Tag as string) && candidates[0].IsEnabled) {
                    candidates[0].Focus(); return
                    true; }
                Control best = null; double distance = double.MaxValue;
                foreach (var c in candidates)
                    if (c.IsEnabled && c.IsVisible && Math.Abs(ProgramCenter(c) - x) < distance) { distance = Math.Abs(ProgramCenter(c) - x); best = c; }
                if (best != null) { best.Focus(); return true; }
            }
            return true;
        }
        double ProgramCenter(Control c)
        {
            try { return c.TranslatePoint(new Point(c.ActualWidth / 2, 0), body).X; }
            catch (InvalidOperationException) { return 0; }
        }
        // Quadrado, triangulo e Options valem como no teclado virtual enquanto o teclado do painel estiver aberto.
        bool ProgramsButton(string button)
        {
            if (panel != ProgramsPanel || !programKeyboard || flyout != null || NavigationHasFocus()) return false;
            if (button == "QUADRADO") { if (programQuery.Length > 0) { ProgramBackspace(); Sound("mover"); } return true; }
            if (button == "TRIANGULO") { string before = programQuery; TypeProgram(" "); if (programQuery != before) Sound("mover"); return true; }
            if (button == "OPTIONS") { OpenFirstProgram(); return true; }
            return false;
        }
        bool ProgramsKey(KeyEventArgs e)
        {
            if (!ProgramsTyping()) return false;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape || key == Key.Up || key == Key.Down || key == Key.Left || key == Key.Right || key == Key.PageUp || key ==
                Key.PageDown || key ==
                Key.Tab) return false;
            if (key == Key.Back) ProgramBackspace();
            else if (key == Key.Space) TypeProgram(" ");
            else if (key == Key.Enter)
            {
                var focused = Keyboard.FocusedElement as Control;
                if (focused != null && ProgramResultTag(focused.Tag as string)) return false; // Enter num resultado: acao normal
                if (!e.IsRepeat) OpenFirstProgram();
            }
            else return true; // letras e simbolos seguem para o TextInput
            e.Handled = true;
            return true;
        }
        // L2/R2 na lista completa: primeiro programa da letra seguinte ou anterior.
        bool ProgramsPage(int direction)
        {
            if (panel != ProgramsPanel || programQuery.Trim().Length > 0) return false;
            var all = actions.FindAll(c => (c.Tag as string ?? "").StartsWith("program-result-all-", StringComparison.Ordinal));
            if (all.Count == 0) return false;
            int at = all.IndexOf(Keyboard.FocusedElement as Control);
            Control target = null;
            if (at < 0) target = direction > 0 ? all[0] : all[all.Count - 1];
            else
            {
                string letter = ProgramLetter(all[at]);
                int i = at;
                if (direction > 0) { while (i < all.Count && ProgramLetter(all[i]) == letter) i++; if (i < all.Count) target = all[i]; }
                else
                {
                    while (i > 0 && ProgramLetter(all[i - 1]) == letter) i--;  // comeco da letra atual
                    if (i != at) target = all[i];
            else if (i > 0) { string previous = ProgramLetter(all[i - 1]); while (i > 0 && ProgramLetter(all[i - 1]) == previous) i--; target = all[i]; }
                }
            }
            if (target != null && target != Keyboard.FocusedElement) { CancelHold(); target.Focus(); target.BringIntoView(); Sound("mover"); }
            return true;
        }
        string ProgramLetter(Control c)
        {
            ProgramEntry entry; if (!programByTag.TryGetValue(c.Tag as string ?? "", out entry) || entry.Name.Length == 0) return "#";
            char first = ProgramMatch.Fold(entry.Name)[0];
            return char.IsLetter(first) ? first.ToString() : "#";
        }

        void ToggleProgramOptions(string id)
        {
            programOptions = programOptions == id ? null : id;
            RebuildProgramResults();
            var target = actions.Find(c => (string)c.Tag == (programOptions == null ? "program-more-" : "program-option-max-") + id);
            if (target != null) target.Focus();
            ActionSound(programOptions == null ? "voltar" : "lista");
        }
        void OpenFirstProgram()
        {
            var first = actions.Find(c => (c.Tag as string ?? "").StartsWith("program-result-", StringComparison.Ordinal));
            ProgramEntry entry;
        if (first == null || !programByTag.TryGetValue((string)first.Tag, out entry)) { Feedback("Nenhum programa para abrir.", FeedbackKind.Info); return; }
            Sound("selecionar"); OpenProgram(entry, false, false);
        }
    }
}
