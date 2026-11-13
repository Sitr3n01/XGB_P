// Acesso rapido: quatro retangulos e seis quadrados, somente icones (ADR 0099, refino final).
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int HomeUtilityColumns = 6;
        readonly List<Control> homeUtilityTopRow = new List<Control>();
        readonly List<Control> homeUtilityMiddleRow = new List<Control>();
        readonly List<Control> homeUtilityBottomRow = new List<Control>();

        void BuildHomeUtilities()
        {
            homeUtilities.Clear(); homeUtilityTopRow.Clear(); homeUtilityMiddleRow.Clear(); homeUtilityBottomRow.Clear();
            utilitiesHint = HomeCaption("ACESSO RÁPIDO");
            // Logo abaixo dos jogos, o titulo fica mais junto (como Voltar ao jogo abaixo dos quadrados): a Home cabe em 1080p sem rolar.
            ((FrameworkElement)utilitiesHint.Parent).Margin = new Thickness(0, 0, 0, 3);
            var utilities = new Grid();
            for (int i = 0; i < HomeUtilityColumns * 2 - 1; i++)
                utilities.ColumnDefinitions.Add(new ColumnDefinition { Width = i % 2 == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(8) });
            utilities.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            utilities.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            utilities.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] labels = { "Biblioteca", "Programas", "Som do sistema", "Gravação", "Lojas", "Downloads", "Teclado", "Desempenho", "AMD", "Energia" };
            string[] glyphs = { "", "", "", "", "", "", "", "", "", "" };
            string[] tags = { "utility-library", "utility-programs", "utility-audio", "utility-recording", "utility-stores",
                "utility-downloads", "utility-keyboard", "utility-performance", "utility-amd", "utility-power" };
            for (int i = 0; i < tags.Length; i++)
            {
                bool wide = i < 4;
                int row = wide ? i / 2 : 2;
                int col = wide ? i % 2 * HomeUtilityColumns : (i - 4) * 2;
                string tag = tags[i];
                var button = HomeButton(HomeUtilityIcon(tag, glyphs[i]), delegate { UseHomeUtility(tag); });
                button.Tag = tag; button.ToolTip = labels[i];
                System.Windows.Automation.AutomationProperties.SetName(button, labels[i]);
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
                button.Padding = new Thickness(5);
                button.Margin = new Thickness(0, 0, 0, 8);
                if (wide) button.Height = 52;
                else
                {
                    // Os seis quadrados ocupam toda a largura, com a mesma proporcao e os mesmos espacos de 8 DIP.
                    button.SetBinding(FrameworkElement.HeightProperty, new Binding("ActualWidth") { Source = button });
                }
                Grid.SetRow(button, row); Grid.SetColumn(button, col);
                if (wide) Grid.SetColumnSpan(button, HomeUtilityColumns - 1);
                utilities.Children.Add(button); homeUtilities.Add(button);
                (row == 0 ? homeUtilityTopRow : row == 1 ? homeUtilityMiddleRow : homeUtilityBottomRow).Add(button);
            }
            body.Children.Add(utilities);
            homeRows.Add(homeUtilityTopRow); homeRows.Add(homeUtilityMiddleRow); homeRows.Add(homeUtilityBottomRow);
        }

        FrameworkElement HomeUtilityIcon(string tag, string glyph)
        {
            if (tag == "utility-recording") return RecordingIcon("video", 24);
            if (tag == "utility-amd") return AmdLogoIcon(24, Brush("TextPrimary"));
            return Symbol(glyph, 24);
        }

        // Entre as fileiras do Acesso rapido e a dos aplicativos abertos, logo abaixo, o destino segue a posicao na tela,
        // mesmo com a fileira de apps centralizada ou rolada. Nas demais secoes vale a coluna lembrada.
        int HomeUtilityColumn(List<Control> from, List<Control> to, Control focused, int fallback)
        {
            if (!HomeGridRow(from) || !HomeGridRow(to))
                return fallback;
            double center = focused.TranslatePoint(new Point(focused.ActualWidth / 2, 0), body).X;
            int best = fallback; double distance = double.PositiveInfinity;
            for (int i = 0; i < to.Count; i++)
            {
                if (!to[i].IsEnabled) continue;
                double x = to[i].TranslatePoint(new Point(to[i].ActualWidth / 2, 0), body).X;
                // No empate, manter a coluna da esquerda; arredondamentos nao trocam o destino.
                if (Math.Abs(x - center) < distance - .01) { best = i; distance = Math.Abs(x - center); }
            }
            homeColumn = best; return best;
        }

        bool HomeUtilityRow(List<Control> row)
        { return row == homeUtilityTopRow || row == homeUtilityMiddleRow || row == homeUtilityBottomRow; }
        bool HomeGridRow(List<Control> row) { return HomeUtilityRow(row) || row == openAppsRow; }

        // A lista de acoes (L2/R2, primeiro alvo) acompanha a ordem das secoes, como ja fazem os apps e o player.
        void SyncHomeActions() { actions.Clear(); foreach (var row in homeRows) actions.AddRange(row); }
    }
}
