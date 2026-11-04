// Interruptor comum: estado confirmado, visual Windows 10 e uma unica area de foco/acionamento.
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XGBP.Design
{
    sealed class SettingToggle : Button
    {
        public bool? State { get; internal set; }
        protected override AutomationPeer OnCreateAutomationPeer() { return new SettingTogglePeer(this); }
    }
    sealed class SettingTogglePeer : ButtonAutomationPeer, IToggleProvider
    {
        readonly SettingToggle toggle;
        public SettingTogglePeer(SettingToggle owner) : base(owner) { toggle = owner; }
        public override object GetPattern(PatternInterface pattern) { return pattern == PatternInterface.Toggle ? this : base.GetPattern(pattern); }
        public ToggleState ToggleState { get { return toggle.State == true ? ToggleState.On : toggle.State == false ? ToggleState.Off : ToggleState.Indeterminate; } }
        public void Toggle()
        {
            if (!toggle.IsEnabled) throw new ElementNotEnabledException();
            // O callback normal aplica/confirma a mudanca. Nunca antecipar o estado ao clicar.
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
    sealed partial class Preview
    {
        Button ToggleSetting(Panel parent, string name, bool? state, string tag, Action callback, string detail = null)
        {
            var toggle = new SettingToggle();
            var button = ConnectionButton(parent, name, "", tag, callback, toggle);
            button.IsEnabledChanged += delegate { button.Opacity = button.IsEnabled ? 1 : .45; };
            UpdateToggleSetting(button, name, state, detail);
            return button;
        }
        void UpdateToggleSetting(Button button, string name, bool? state, string detail = null)
        {
            var toggle = (SettingToggle)button; bool? previous = toggle.State; toggle.State = state;
            var grid = (Grid)button.Content;
            var label = (TextBlock)grid.Children[1];
            label.Text = detail ?? (state == true ? "Ativado" : state == false ? "Desativado" : "Indisponível");
            label.Foreground = Brush("Muted");
            if (grid.Children.Count > 2) grid.Children.RemoveAt(2);
            else grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var track = new Grid { Width = 40, Height = 20, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false, Opacity = state.HasValue ? 1 : .4 };
            track.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = state == true ? Brush("ToggleTrackOn") : Brushes.Transparent,
                BorderBrush = state == true ? Brush("ToggleTrackOn") : Brush("Muted"), BorderThickness = new Thickness(2) });
            track.Children.Add(new Ellipse { Width = 12, Height = 12, Margin = new Thickness(4, 0, 4, 0),
                HorizontalAlignment = state == true ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Fill = state == true ? Brush("ToggleThumbOn") : Brush("TextPrimary") });
            Grid.SetColumn(track, 2); grid.Children.Add(track);
            string accessible = name + " · " + label.Text;
            button.ToolTip = accessible; AutomationProperties.SetName(button, accessible);
            var peer = UIElementAutomationPeer.FromElement(toggle);
            if (peer != null && previous != state)
                peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                    previous == true ? ToggleState.On : previous == false ? ToggleState.Off : ToggleState.Indeterminate,
                    state == true ? ToggleState.On : state == false ? ToggleState.Off : ToggleState.Indeterminate);
        }
    }
}
