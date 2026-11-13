// Titulo com seta no lugar do trilho (ADR 0103). Desde a ADR 0112 e uma opcao das Configuracoes, aplicada na hora;
// o marcador da pasta so decide quando o arquivo de aparencia ainda nao tem a escolha.
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const string SlidingTitleMarker = "TITULO-DESLIZANTE.txt";
        readonly bool railMarker = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SlidingTitleMarker));
        static readonly int[] SlidingTitleOrder = { 0, 6, AmdPanel, 3, AmdTuningPanel, 5, 4 };
        bool railHidden;
#if XGB_VERIFY
        bool slidingTitleTesting;
#endif
        Grid titleHeader;
        Button titleNext;
        bool SlidingTitleEnabled
        {
            get
            {
#if XGB_VERIFY
                return slidingTitleTesting || railHidden;
#else
                return railHidden;
#endif
            }
        }

        void StartSlidingTitle() { if (SlidingTitleEnabled) SetSlidingTitleLayout(true); }

        void CreateSlidingTitle()
        {
            if (titleHeader != null) return;
            titleHeader = new Grid { Margin = new Thickness(0, 0, 0, 20), Visibility = Visibility.Collapsed };
            titleHeader.ColumnDefinitions.Add(new ColumnDefinition());
            titleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleNext = SlidingTitleButton(Symbol("\uE76C", 20), "Próximo painel (R1)", delegate { ChangePanel(1); });
            titleNext.Width = 44; titleNext.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(titleNext, 1); titleHeader.Children.Add(titleNext);
            ((Grid)MotionPart("PanelContent")).Children.Add(titleHeader);
            DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(
                MotionPart("PanelTitle"), delegate { UpdateSlidingTitle(); });
        }

        Button SlidingTitleButton(object content, string name, Action action)
        {
            var button = new Button { Content = content, Style = (Style)window.FindResource("Action"),
                Background = Brushes.Transparent, Height = 44, Padding = new Thickness(0), Margin = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = name };
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            WireMotion(button);
            button.Click += delegate { InvokeSoundAction(button, delegate { action(); ActionSound("mover"); }); };
            return button;
        }

        void SetSlidingTitleLayout(bool active)
        {
            ReleaseSidebarHeight();
            CreateSlidingTitle();
            var sidebar = (Grid)MotionPart("Sidebar");
            sidebar.ColumnDefinitions[0].Width = new GridLength(active ? 0 : 92);
            sidebar.ColumnDefinitions[1].Width = new GridLength(active ? 0 : 16);
            MotionPart("RailFrame").Visibility = active ? Visibility.Collapsed : Visibility.Visible;
            var title = (TextBlock)MotionPart("PanelTitle");
            var parent = title.Parent as Panel;
            if (parent != null) parent.Children.Remove(title);
            if (active)
            {
                Grid.SetColumn(title, 0); Grid.SetRow(title, 0);
                title.Margin = new Thickness(0); title.FontSize = 33;
                title.TextWrapping = TextWrapping.Wrap; title.TextTrimming = TextTrimming.None;
                title.VerticalAlignment = VerticalAlignment.Top;
                titleHeader.Children.Add(title);
            }
            else
            {
                // Volta exatamente ao titulo do XAML, no mesmo lugar, para o trilho religado igualar o de quem nunca o desligou.
                Grid.SetColumn(title, 0); Grid.SetRow(title, 0);
                title.Margin = new Thickness(0, 0, 0, 20); title.FontSize = 33;
                title.ClearValue(TextBlock.TextWrappingProperty); title.ClearValue(TextBlock.TextTrimmingProperty);
                title.ClearValue(FrameworkElement.VerticalAlignmentProperty);
                ((Grid)MotionPart("PanelContent")).Children.Insert(0, title);
            }
            titleHeader.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            UpdateSlidingTitle();
            window.UpdateLayout(); FitPanelBody();
        }

        void UpdateSlidingTitle()
        {
            if (titleHeader == null) return;
            System.Windows.Automation.AutomationProperties.SetName(titleNext, "Próximo painel (R1)");
        }

        bool NavigationHasFocus()
        {
            var focused = Keyboard.FocusedElement as Button;
            return rail.Contains(focused) || SlidingTitleEnabled && focused == titleNext;
        }

        bool FocusNavigation(int index)
        {
            if (!SlidingTitleEnabled) return rail[index].Focus();
            return titleNext != null && titleNext.Focus();
        }

        bool ChangeSlidingTitle(int direction)
        {
            if (!SlidingTitleEnabled) return false;
            int current = Array.IndexOf(SlidingTitleOrder, RailIndex(panel));
            int next = SlidingTitleOrder[(Math.Max(0, current) + direction + SlidingTitleOrder.Length) % SlidingTitleOrder.Length];
            returnToHome = false; motionPanelAxis = "home"; motionPanelDirection = direction;
            SetPanel(next); FocusNavigation(next);
            return true;
        }

        bool NavigateSlidingTitle(Control focused, string key)
        {
            if (!SlidingTitleEnabled || !NavigationHasFocus()) return false;
            if (key == "BAIXO") FocusFirstAction();
            else if (key == "ESQUERDA") ChangePanel(-1);
            else if (key == "DIREITA") ChangePanel(1);
            return true;
        }

        // Configuracoes > INTERFACE (ADR 0112): troca o layout na hora, salva junto da aparencia e mantem o foco na linha.
        void AddRailSetting()
        {
            var row = ToggleSetting(body, "Trilho lateral", !railHidden, "rail-layout", ToggleRailSetting);
            row.ToolTip = "Desligado, os painéis trocam pela seta ao lado do título e por L1/R1, e a Biblioteca fica no Acesso rápido do Início.";
        }
        void ToggleRailSetting()
        {
            railHidden = !railHidden;
            double height = ReleaseSidebarHeight();
            SetSlidingTitleLayout(SlidingTitleEnabled);
            appearanceDirty = true; FlushAppearancePreferences();
            RebuildSettings("rail-layout");
            MotionHeight(height);
            MotionReveal(MotionPart(railHidden ? "PanelContent" : "RailFrame"), MotionNormal, railHidden ? 24 : -24, 0);
            if (!appearanceDirty) Feedback(railHidden ? "Salvo. Os painéis trocam pela seta do título e por L1/R1." : "Salvo. O trilho lateral voltou.");
            WriteLog("aparencia: trilho lateral " + (railHidden ? "desligado" : "ligado"));
        }

        bool RevealSlidingTitle(int direction)
        {
            if (!SlidingTitleEnabled) return false;
            MotionReveal(MotionPart("BodyScroll"), MotionNormal, direction * 24, 0);
            MotionReveal(MotionPart("PanelTitle"), MotionNormal, direction * 12, 0);
            return true;
        }
    }
}
