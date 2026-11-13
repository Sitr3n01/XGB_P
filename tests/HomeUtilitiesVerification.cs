// Layout e navegacao dos atalhos: arvore WPF e comandos substituidos, sem operar o PC.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyHomeUtilities()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) {
                if (!ok) throw new Exception("Acesso rapido: " + why); checks.Add("OK: " + why);
            };
            SetPanel(0); Show(); SettleMotion(); window.UpdateLayout();
            check(homeUtilityTopRow.Count == 2 && homeUtilityMiddleRow.Count == 2 && homeUtilityBottomRow.Count == 6 && homeUtilities.Count == 10,
                "dez atalhos: dois retangulos, mais dois retangulos e seis quadrados");
            int top = homeRows.IndexOf(homeUtilityTopRow), firstUtility = 1 + gameResumeRow.Count;
            foreach (var row in gameOpenRows) firstUtility += row.Count;
            check(homeRows[1] == gameResumeRow && top == 2 + gameOpenRows.Count && homeRows[top + 1] == homeUtilityMiddleRow &&
                homeRows[top + 2] == homeUtilityBottomRow && homeRows[top + 3] == openAppsRow && homeRows[top + 4] == gameRecentRows[0] &&
                homeRows.IndexOf(mediaTransportRow) > homeRows.IndexOf(gameRecentRows[2]),
                "Inicio, Voltar ao jogo, Acesso rapido, Aplicativos abertos, Jogos recentes e player, nesta ordem");
            Func<FrameworkElement, double> topOf = e => e.TranslatePoint(new Point(), body).Y;
            Func<FrameworkElement, double> bottomOf = e => e.TranslatePoint(new Point(0, e.ActualHeight), body).Y;
            Func<FrameworkElement, double> leftOf = e => e.TranslatePoint(new Point(), body).X;
            Func<FrameworkElement, double> rightOf = e => e.TranslatePoint(new Point(e.ActualWidth, 0), body).X;
            FrameworkElement[] stack = { actions.Find(c => (string)c.Tag == "steam-home"), gameResumeSlot, homeUtilityTopRow[0],
                homeUtilityBottomRow[0], openAppsScroll, gameRecentsSlot, mediaHost };
            var edges = new List<string>(); bool stacked = true;
            for (int i = 0; i < stack.Length; i++)
            {
                edges.Add(Math.Round(topOf(stack[i])) + "-" + Math.Round(bottomOf(stack[i])));
                if (i > 0 && bottomOf(stack[i - 1]) > topOf(stack[i]) + .5) stacked = false;
            }
            check(stacked, "secoes empilhadas na mesma ordem na tela, sem sobreposicao: " + string.Join(" | ", edges.ToArray()));
            var homeScroll = (ScrollViewer)window.FindName("BodyScroll");
        check(homeScroll.ScrollableHeight < .5, "Home de referencia (um jogo, tres apps, tres recentes e player) cabe sem rolar: " +
            homeScroll.ScrollableHeight);
            check(openAppsRow.Count >= 3 && openAppsScroll.ScrollableWidth < .5, "fixture com aplicativos abertos que cabem na largura");
        double appsLeft = leftOf(openAppsRow[0]) - leftOf(openAppsScroll), appsRight = rightOf(openAppsScroll) - rightOf(openAppsRow[openAppsRow.Count - 1]);
            check(appsLeft > 8 && Math.Abs(appsLeft - appsRight) < 1, "icones dos aplicativos abertos centralizados na secao: " +
                Math.Round(appsLeft, 1) + " DIP a esquerda e " + Math.Round(appsRight, 1) + " a direita");
            string[] expected = { "utility-library", "utility-programs", "utility-audio", "utility-recording", "utility-stores",
                "utility-downloads", "utility-keyboard", "utility-performance", "utility-amd", "utility-power" };
            for (int i = 0; i < expected.Length; i++)
            {
                var c = homeUtilities[i];
                check((string)c.Tag == expected[i] && actions.IndexOf(c) == firstUtility + i, "ordem visual e de Tab: " + expected[i]);
                check(!string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(c)), "nome acessivel: " + expected[i]);
                check(leftOf(c) >= -.5 && rightOf(c) <= body.ActualWidth + .5, "atalho dentro da largura do painel: " + expected[i]);
            }
            var rectangles = new List<Control>(homeUtilityTopRow); rectangles.AddRange(homeUtilityMiddleRow);
            foreach (Button c in rectangles)
            {
                bool iconOnly = (string)c.Tag == "utility-recording" ? c.Content is Viewbox :
                    c.Content is TextBlock && ((TextBlock)c.Content).FontFamily.Source == "Segoe MDL2 Assets";
                check(iconOnly && c.ActualWidth > c.ActualHeight * 2 && c.ActualHeight == 52 &&
                    Math.Abs(c.ActualWidth - homeUtilityTopRow[0].ActualWidth) < .5,
                    "retangulo somente com icone e dimensoes preservadas: " + c.ToolTip);
            }
            foreach (Button c in homeUtilityBottomRow)
            {
                bool vector = (string)c.Tag == "utility-amd";
                check((vector ? c.Content is Viewbox : c.Content is TextBlock && ((TextBlock)c.Content).FontFamily.Source == "Segoe MDL2 Assets") &&
                    Math.Abs(c.ActualWidth - c.ActualHeight) < .5 && Math.Abs(c.ActualWidth - homeUtilityBottomRow[0].ActualWidth) < .5,
                    "quadrado uniforme com icone: " + c.ToolTip);
                check(c.ActualWidth >= 44, "alvo de pelo menos 44 DIP: " + c.ToolTip);
            }
            check((string)homeUtilityMiddleRow[0].ToolTip == "Som do sistema" && (string)homeUtilityMiddleRow[1].ToolTip == "Gravação",
                "Som do sistema antes de Gravacao na segunda fileira");
            foreach (var row in new[] { homeUtilityTopRow, homeUtilityMiddleRow, homeUtilityBottomRow })
                for (int i = 1; i < row.Count; i++)
                    check(Math.Abs(leftOf(row[i]) - rightOf(row[i - 1]) - 8) < .5, "espaco horizontal de 8 DIP antes de " + row[i].ToolTip);
            check(Math.Abs(topOf(homeUtilityMiddleRow[0]) - bottomOf(homeUtilityTopRow[0]) - 8) < .5 &&
                Math.Abs(topOf(homeUtilityBottomRow[0]) - bottomOf(homeUtilityMiddleRow[0]) - 8) < .5,
                "espaco vertical de 8 DIP entre as tres fileiras");
            check(Math.Abs(leftOf(homeUtilityBottomRow[0]) - leftOf(homeUtilityTopRow[0])) < .5 &&
                Math.Abs(rightOf(homeUtilityBottomRow[5]) - rightOf(homeUtilityTopRow[1])) < .5,
                "seis quadrados redistribuidos entre as mesmas bordas dos retangulos");
            check(topOf(homeUtilityBottomRow[0]) > bottomOf(homeUtilityMiddleRow[0]) && topOf(homeUtilityTopRow[0]) > bottomOf(gameResumeSlot) &&
                bottomOf(homeUtilityBottomRow[0]) < topOf(openAppsScroll), "fileiras sem sobreposicao, abaixo do jogo e acima dos aplicativos");
            FocusHome("steam-home"); Navigate("BAIXO");
            check(Keyboard.FocusedElement == gameResumeRow[0], "Inicio desce para o cartao de Voltar ao jogo");
            Navigate("BAIXO"); check(FocusedTag() == "utility-library", "cartao do jogo desce para Biblioteca");
            Navigate("CIMA"); check(Keyboard.FocusedElement == gameResumeRow[0], "Biblioteca sobe para o cartao do jogo");
            Navigate("DIREITA"); Navigate("DIREITA"); Navigate("BAIXO");
            check(FocusedTag() == "utility-programs", "fechar do jogo desce para Programas, do mesmo lado");
            Navigate("CIMA"); check(Keyboard.FocusedElement == gameResumeRow[0], "Programas sobe pelo cartao, nunca por pausar ou fechar");
            FocusHome("utility-library");
            Navigate("DIREITA"); check(FocusedTag() == "utility-programs", "Biblioteca vai para Programas");
            Navigate("DIREITA"); check(FocusedTag() == "utility-programs", "fim dos retangulos nao pula de fileira");
            Navigate("BAIXO"); check(FocusedTag() == "utility-recording", "Programas desce para Gravacao");
            Navigate("BAIXO"); check(FocusedTag() == "utility-amd", "Gravacao desce pela posicao para AMD");
            Navigate("CIMA"); check(FocusedTag() == "utility-recording", "AMD sobe para Gravacao");
            Navigate("CIMA"); check(FocusedTag() == "utility-programs", "Gravacao sobe para Programas");
            Navigate("ESQUERDA"); Navigate("BAIXO");
            check(FocusedTag() == "utility-audio", "Biblioteca desce para Som do sistema");
            Navigate("BAIXO"); check(FocusedTag() == "utility-downloads", "Som desce pela posicao para Downloads");
            Navigate("ESQUERDA"); check(FocusedTag() == "utility-stores", "quadrados vao para Lojas");
            Navigate("ESQUERDA"); check(Keyboard.FocusedElement == rail[0], "esquerda da primeira coluna volta ao trilho");
            Func<Control, double> centerOf = c => c.TranslatePoint(new Point(c.ActualWidth / 2, 0), body).X;
            Func<Control, List<Control>, bool> nearest = delegate(Control origin, List<Control> row) {
                var to = Keyboard.FocusedElement as Control; if (to == null || !row.Contains(to)) return false;
            foreach (var c in row) if (c.IsEnabled && Math.Abs(centerOf(c) - centerOf(origin)) < Math.Abs(centerOf(to) - centerOf(origin)) - .01) return false;
                return true;
            };
            foreach (string tag in new[] { "utility-stores", "utility-power" })
            {
                var square = actions.Find(c => (string)c.Tag == tag); FocusHome(tag); Navigate("BAIXO");
                check(nearest(square, openAppsRow), (string)square.ToolTip + " desce para o aplicativo mais proximo na tela");
            }
            for (int i = 0; i < openAppsRow.Count; i++)
            {
                openAppsRow[i].Focus(); Navigate("CIMA");
                check(nearest(openAppsRow[i], homeUtilityBottomRow), "aplicativo " + (i + 1) + " sobe para o quadrado acima dele: " + FocusedTag());
            }
        openAppsRow[0].Focus(); Navigate("BAIXO"); check(Keyboard.FocusedElement == gameRecentRows[0][0], "aplicativos descem para o primeiro jogo recente");
            FocusHome("utility-audio"); Navigate("DIREITA");
            check(FocusedTag() == "utility-recording", "Som vai para Gravacao");
            Navigate("DIREITA"); check(FocusedTag() == "utility-recording", "fim da segunda fileira nao pula para os quadrados");
            FocusHome("utility-performance");
            Navigate("DIREITA"); check(FocusedTag() == "utility-amd", "Desempenho vai para AMD");
            Navigate("DIREITA"); check(FocusedTag() == "utility-power", "AMD vai para Energia");
            FocusHome("utility-audio"); Activate();
            check(panel == 6 && returnToHome && ((TextBlock)window.FindName("PanelTitle")).Text == "Som",
                "retangulo Som do sistema abre o painel de audio existente");
            Back(); check(panel == 0 && !returnToHome && FocusedTag() == "utility-audio", "voltar do Som restaura o retangulo");
            FocusHome("utility-recording"); Activate();
            check(panel == AmdPanel && returnToHome && ((TextBlock)window.FindName("PanelTitle")).Text == "Gravação",
                "atalho Gravacao abre o painel de captura existente");
            Back(); check(panel == 0 && !returnToHome && FocusedTag() == "utility-recording", "voltar de Gravacao restaura o atalho");
            FocusHome("utility-amd"); Activate();
            check(panel == AmdTuningPanel && returnToHome && ((TextBlock)window.FindName("PanelTitle")).Text == "AMD",
                "atalho AMD abre os ajustes graficos existentes");
            Back(); check(panel == 0 && !returnToHome && FocusedTag() == "utility-amd", "voltar de AMD restaura o atalho");
            FocusHome("utility-programs");
            check(utilitiesHint.Text == "Programas", "nome do foco continua no titulo da secao");
            RenderGames(true); RebuildMedia(MediaSelected()); RenderMedia(); window.UpdateLayout();
            check(FocusedTag() == "utility-programs" && homeRows[top] == homeUtilityTopRow && homeRows[top + 1] == homeUtilityMiddleRow && homeRows[top + 2] ==
                homeUtilityBottomRow,
                "atualizacoes dos jogos e do player mantem posicao e foco dos atalhos");
            var order = new List<Control>(); foreach (var row in homeRows) order.AddRange(row);
            check(actions.Count == order.Count && actions.TrueForAll(c => actions.IndexOf(c) == order.IndexOf(c)) &&
                actions[firstUtility] == homeUtilities[0] && actions[firstUtility + 1] == homeUtilities[1],
                "reconstruir jogos e player mantem a lista de acoes na ordem das secoes");
            FocusHome("steam-home"); SettleMotion(); window.UpdateLayout(); SaveRender("home-ordem-secoes");
            FocusHome("utility-programs"); SettleMotion(); window.UpdateLayout(); SaveRender("home-acesso-rapido");
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do Acesso rapido abaixo de Voltar ao jogo e dos apps centralizados.");
            checks.Add("Fixtures e arvore WPF; sem Steam, programas reais, HID, audio ou preferencias operados.");
            File.WriteAllLines(Path.Combine(Output, "home-acesso-rapido-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}
