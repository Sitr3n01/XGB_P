// Home da previa: jogos recentes reais, aplicativos, midia e utilidades.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        readonly List<List<Control>> homeRows = new List<List<Control>>();
        readonly List<Control> homeUtilities = new List<Control>();
        TextBlock utilitiesHint;
        bool returnToHome;
        string homeReturnTag;
        int homeColumn;

        TextBlock Symbol(string glyph, double size)
        {
            var text = Text(glyph, size, "TextPrimary");
            text.FontFamily = new FontFamily("Segoe MDL2 Assets");
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            return text;
        }

        Button HomeButton(object content, Action action)
        {
            var button = new Button { Content = content, Style = (Style)window.FindResource("Action") };
            WireMotion(button);
            button.Click += delegate { InvokeSoundAction(button, action); };
            button.GotKeyboardFocus += delegate { button.BringIntoView(); ShowHomeHint(button); };
            actions.Add(button);
            return button;
        }

        void BuildHome()
        {
            homeColumn = 0;
            var steamHome = new StackPanel { Orientation = Orientation.Horizontal };
            steamHome.Children.Add(Symbol("\uE80F", 22));
            var steamHomeLabel = Text("Início", 21, "TextPrimary");
            steamHomeLabel.FontWeight = FontWeights.SemiBold;
            steamHomeLabel.Margin = new Thickness(14, 0, 0, 0);
            steamHome.Children.Add(steamHomeLabel);
            var steamHomeButton = HomeButton(steamHome, OpenSteamHome);
            steamHomeButton.Tag = "steam-home";
            // O texto visivel e so "Início"; dica e acessibilidade dizem para onde vai.
            steamHomeButton.ToolTip = "Início do Big Picture";
            System.Windows.Automation.AutomationProperties.SetName(steamHomeButton, "Início do Big Picture");
            steamHomeButton.Padding = new Thickness(14, 10, 14, 10);
            body.Children.Add(steamHomeButton); homeRows.Add(new List<Control> { steamHomeButton });

            // Inicio, Voltar ao jogo, Acesso rapido, Aplicativos abertos, Jogos recentes e o player por ultimo.
            BuildGameResume();

            BuildHomeUtilities();

            BuildOpenApps();

            BuildGameRecents();

            BuildMediaPlayer();
            SyncHomeActions();
            ((TextBlock)window.FindName("Feedback")).TextWrapping = TextWrapping.Wrap;
            ((TextBlock)window.FindName("Feedback")).TextTrimming = TextTrimming.None;
        }

        TextBlock HomeCaption(string text)
        {
            // O lado direito do titulo da secao identifica o icone em foco, sem rodape que mude o layout.
            var heading = new DockPanel { Margin = new Thickness(0, 10, 0, 7) };
            var hint = Text("", 13, "TextPrimary"); hint.TextWrapping = TextWrapping.NoWrap; hint.TextTrimming = TextTrimming.CharacterEllipsis;
            hint.MaxWidth = 250; hint.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(hint, Dock.Right);
            heading.Children.Add(hint); heading.Children.Add(Text(text, 14, "Muted")); body.Children.Add(heading);
            return hint;
        }
        void ShowHomeHint(Control focused)
        {
            if (panel != 0) return;
            if (utilitiesHint != null) MotionText(utilitiesHint, focused != null && homeUtilities.Contains(focused) ? focused.ToolTip as string ?? "" : "");
            AppCloseFocus(focused);
            UpdateOpenAppsHint(focused);
        }

        void FocusHome(string tag)
        {
            var control = actions.Find(c => (string)c.Tag == tag);
            if (control != null) control.Focus(); else if (actions.Count > 0) actions[0].Focus();
        }

        // Biblioteca e Downloads abrem a Steam; Teclado oculta a barra e abre o teclado virtual sobre o app de tras (ADR 0052);
        // Lojas, Programas, Audio, Gravacao, Desempenho, AMD e Energia abrem paineis com volta para a Home.
        void UseHomeUtility(string tag)
        {
            if (tag == "utility-library") { OpenSteamLibrary(); return; }
            if (tag == "utility-downloads") { OpenSteamDownloads(); return; }
            if (tag == "utility-keyboard") { OpenKeyboardFromBar(); return; }
            int target = tag == "utility-stores" ? StoresPanel : tag == "utility-programs" ? ProgramsPanel : tag == "utility-audio" ? 6 : tag == "utility-performance" ? 3 : 4;
            if (tag == "utility-recording") target = AmdPanel;
            else if (tag == "utility-amd") target = AmdTuningPanel;
            motionPanelAxis = "home"; motionPanelDirection = 1; SetPanel(target);
            homeReturnTag = tag; returnToHome = true;
            FocusFirstAction();
            ActionSound("mover"); // Clique no acesso rapido: navegacao curta, sem efeito de abertura.
        }

        bool GameRow(List<Control> row) { return row == gameResumeRow || gameOpenRows.Contains(row) || gameRecentRows.Contains(row); }
        bool NavigateHome(Control focused, string key)
        {
            if (GameConfirmationNavigate(key)) return true;
            for (int row = 0; row < homeRows.Count; row++)
            {
                var items = homeRows[row];
                int col = items.IndexOf(focused);
                if (col < 0) continue;
                // Sliders do player usam esquerda/direita para ajustar; cima/baixo saem deles.
                var slider = focused as Slider;
                if (slider != null && (key == "ESQUERDA" || key == "DIREITA") && NudgeMedia(slider, key == "DIREITA" ? 1 : -1)) return true;
                if (key == "ESQUERDA" || key == "DIREITA")
                {
                    int step = key == "DIREITA" ? 1 : -1, next = col + step;
                    while (next >= 0 && next < items.Count && !items[next].IsEnabled) next += step;
                    if (next >= 0 && next < items.Count) { homeColumn = next; items[next].Focus(); }
                    else if (key == "ESQUERDA") FocusNavigation(0);
                }
                else if (key == "CIMA" || key == "BAIXO")
                {
                    // Controles inativos do player nao recebem foco; a linha toda e pulada se nada estiver ativo.
                    int target = row + (key == "CIMA" ? -1 : 1);
                    while (target >= 0 && target < homeRows.Count && FirstEnabled(homeRows[target]) == null) target += key == "CIMA" ? -1 : 1;
                    if (target >= 0 && target < homeRows.Count)
                    {
                        // O cartao de um jogo nao redefine a coluna lembrada; pausar e fechar redefinem.
                        if (items.Count > 1 && !(GameRow(items) && col == 0)) homeColumn = col;
                        var candidates = homeRows[target]; int wanted = Math.Min(homeColumn, candidates.Count - 1);
                        wanted = HomeUtilityColumn(items, candidates, focused, wanted);
                        // Linha de jogo: vindo de outra secao, entra pelo cartao; entre jogos, mantem a coluna. Nunca cai direto em fechar.
                        if (GameRow(candidates)) wanted = GameRow(items) ? Math.Min(col, candidates.Count - 1) : 0;
                        Control best = null;
                        for (int i = 0; i < candidates.Count; i++)
                            if (candidates[i].IsEnabled && (best == null || Math.Abs(i - wanted) < Math.Abs(candidates.IndexOf(best) - wanted))) best = candidates[i];
                        best.Focus();
                    }
                }
                return true;
            }
            return false;
        }

#if XGB_VERIFY
        void VerifyHome()
        {
            returnToHome = false; SetPanel(0); window.UpdateLayout();
            int rowControls = 0; foreach (var r in homeRows) rowControls += r.Count;
            if (homeRows.Count != 12 || openAppsRow.Count != openApps.Count || actions.Count != rowControls || actions.Count != 10 + openApps.Count + 9 + 6 + 1) // Tres fileiras, dez atalhos (ADR 0099).
                throw new Exception("Falha: conteudo da Home.");
            FocusHome("utility-downloads");
            if (utilitiesHint.Text != "Downloads" || ((TextBlock)window.FindName("Feedback")).Text != "") throw new Exception("Falha: nome do icone no titulo da secao.");
            FocusHome("steam-home"); if (utilitiesHint.Text != "") throw new Exception("Falha: limpar nome do icone.");
            FocusHome("utility-library"); Navigate("DIREITA");
            if ((string)((Control)Keyboard.FocusedElement).Tag != "utility-programs") throw new Exception("Falha: navegar nos retangulos de utilidades.");
            foreach (var c in homeUtilities)
                if (string.IsNullOrEmpty((string)c.ToolTip) || string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(c)))
                    throw new Exception("Falha: utilidades devem manter nome acessivel.");
            FocusHome("utility-performance");
            Activate(); if (panel != 3) throw new Exception("Falha: atalho de desempenho.");
            Back(); if (panel != 0 || (string)((Control)Keyboard.FocusedElement).Tag != "utility-performance") throw new Exception("Falha: voltar a Home com foco restaurado.");
            // Apps -> tres recentes -> tres linhas do player, sempre na coluna 2.
            openAppsRow[2].Focus(); for (int i = 0; i < 7; i++) Navigate("BAIXO");
            if (!MediaControl(Keyboard.FocusedElement as Control)) throw new Exception("Falha: navegar dos apps ate o player ao fim da Home.");
            FocusHome("recent-fixture:one"); Activate();
            if (gameConfirmation != "fixture:one") throw new Exception("Falha: confirmar outro jogo aberto.");
            Back();
            SelectRail(1); if (panel != 0) throw new Exception("Falha: Biblioteca deve continuar sendo uma acao.");
            Show();
            rail[0].Focus(); Navigate("DIREITA");
            if (Keyboard.FocusedElement != actions[0]) throw new Exception("Falha: trilho para Home.");
            ChangePanel(-1); if (panel != 4) throw new Exception("Falha: ciclo da Home para Energia.");
            ChangePanel(1); if (panel != 0) throw new Exception("Falha: retorno ao inicio do ciclo.");
            VerifyHomeUtilities();
        }
#endif
    }
}
