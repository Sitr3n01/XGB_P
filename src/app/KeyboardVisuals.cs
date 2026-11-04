// Desenho do teclado: vetores de 24 unidades, sem depender de glifos de fontes de icones.
// A geometria das linhas e a rota de entrada continuam em KeyboardLayout/VirtualKeyboard.
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class KbKeyChrome
        {
            public Border Face, FocusRing, Indicator;
            public FrameworkElement CapsMark;
            public bool Hovered;
        }
        readonly Dictionary<string, KbKeyChrome> kbChrome = new Dictionary<string, KbKeyChrome>();
        Border kbHeaderGrip;
        static readonly Brush KbInk = KbSolid("#F3F3F3"), KbSecondary = KbSolid("#AEAEAE");
        static readonly Brush KbPanel = KbSolid("#1D1D1D"), KbFocusEdge = KbSolid("#777777");
        static readonly Brush KbLetterFace = KbSolid("#363636"), KbCommandFace = KbSolid("#303030");
        static readonly Brush KbKeyEdge = KbSolid("#151515"), KbHoverFace = KbSolid("#444444");
        static readonly Brush KbFocusFace = KbSolid("#3B3B3B"), KbActiveFace = KbSolid("#545454");

        static Brush KbSolid(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze(); return brush;
        }

        // Tracos arredondados e espessura uniforme nas teclas especiais e nos botoes do controle.
        static FrameworkElement KbIcon(string id, double size)
        {
            string data;
            bool filled = false;
            switch (id)
            {
                case "undo": data = "M9,5 L4,10 9,15 M4,10 H14 A5,5 0 0 1 14,20 H12"; break;
                case "cut": data = "M9,15 L19,4 M9,9 L19,20 M9,7 A3,3 0 1 1 3,7 A3,3 0 1 1 9,7 M9,17 A3,3 0 1 1 3,17 A3,3 0 1 1 9,17"; break;
                case "copy": data = "M8,8 H20 V21 H8 Z M15,4 V2 H3 V16 H5"; break;
                case "paste": data = "M8,5 H5 V21 H19 V5 H16 M9,3 H15 V7 H9 Z M9,12 H15 M9,16 H15"; break;
                case "selectall": data = "M8,3 H3 V8 M16,3 H21 V8 M21,16 V21 H16 M8,21 H3 V16 M8,8 H16 V16 H8 Z"; break;
                case "clipboard": data = "M8,5 H5 V21 H19 V5 H16 M9,3 H15 V7 H9 Z M12,11 V15 L15,17"; break;
                case "emoji": data = "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M8,9 V10 M16,9 V10 M8,15 Q12,19 16,15"; break;
                case "layer-full": case "keyboard": data = "M4,5 H20 Q22,5 22,7 V17 Q22,19 20,19 H4 Q2,19 2,17 V7 Q2,5 4,5 Z M6,9 H6.5 M10,9 H10.5 M14,9 H14.5 M18,9 H18.5 M6,12 H6.5 M10,12 H10.5 M14,12 H14.5 M18,12 H18.5 M7,16 H17"; break;
                case "close": data = "M6,6 L18,18 M18,6 L6,18"; break;
                case "backspace": data = "M9,5 H20 Q22,5 22,7 V17 Q22,19 20,19 H9 L2,12 Z M12,9 L18,15 M18,9 L12,15"; break;
                case "enter": data = "M20,5 V12 Q20,15 17,15 H4 M9,10 L4,15 9,20"; break;
                case "shift-left": case "shift-right": data = "M12,3 L3,12 H8 V20 H16 V12 H21 Z"; break;
                case "keyboard-settings": data = "M9,3 H15 L15.5,6 18,7.5 21,6.5 23,11 20.5,13 20,16 21.5,18.5 18,22 15.5,20.5 12,21 10,23 6,21 7,18 5.5,15.5 2.5,15 V9 L5.5,8.5 7,6 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12"; break;
                case "left": data = "M15,5 L8,12 15,19"; break;
                case "right": data = "M9,5 L16,12 9,19"; break;
                case "up": data = "M12,20 V4 M6,10 L12,4 18,10"; break;
                case "down": data = "M12,4 V20 M6,14 L12,20 18,14"; break;
                case "win": data = "M3,3 H11 V11 H3 Z M13,3 H21 V11 H13 Z M3,13 H11 V21 H3 Z M13,13 H21 V21 H13 Z"; filled = true; break;
                case "dictation": case "mic": data = "M9,6 A3,3 0 0 1 15,6 V12 A3,3 0 0 1 9,12 Z M6,11 V12 A6,6 0 0 0 18,12 V11 M12,18 V22 M9,22 H15"; break;
                case "square": data = "M5,5 H19 V19 H5 Z"; break;
                case "triangle": data = "M12,4 L21,20 H3 Z"; break;
                case "circle": data = "M20,12 A8,8 0 1 1 4,12 A8,8 0 1 1 20,12"; break;
                case "cross": data = "M6,6 L18,18 M18,6 L6,18"; break;
                case "options": data = "M5,6 H19 M5,12 H19 M5,18 H19"; break;
                default: return null;
            }
            var canvas = new Canvas { Width = 24, Height = 24 };
            canvas.Children.Add(new Path {
                Data = Geometry.Parse(data), Stroke = filled ? null : KbInk, Fill = filled ? KbInk : null,
                StrokeThickness = 1.65, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
            return new Viewbox { Width = size, Height = size, Child = canvas, Stretch = Stretch.Uniform };
        }

        static FrameworkElement KbControllerBadge(string hint)
        {
            string icon = hint == "□" ? "square" : hint == "△" ? "triangle" : hint == "○" ? "circle" : hint == "✕" ? "cross" : hint == "≡" ? "options" : hint == "mic" ? "mic" : null;
            FrameworkElement content = icon == null ? (FrameworkElement)new TextBlock {
                Text = hint, FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = KbInk, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : KbIcon(icon, 12);
            return content;
        }

        Border KbKeyView(KeyDef key, double width, double height, out TextBlock label)
        {
            var chrome = new KbKeyChrome();
            var grid = new Grid();
            var icon = KbIcon(key.Id, key.Id == "keyboard-settings" || key.Id == "close" ? 16 : key.Strip ? 20 : 24);
            bool iconOnly = icon != null && key.Id != "layer-full";
            label = new TextBlock {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Foreground = KbInk, TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Normal,
                FontSize = key.Strip ? 13 : key.Kind == KeyKind.Char ? 22 : 14,
                Visibility = iconOnly ? Visibility.Collapsed : Visibility.Visible };
            var content = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0) };
            if (icon != null)
            {
                icon.VerticalAlignment = VerticalAlignment.Center;
                if (!iconOnly) icon.Margin = new Thickness(0, 0, 7, 0);
                content.Children.Add(icon);
            }
            content.Children.Add(label);
            grid.Children.Add(content);
            if (key.Corner != null)
                grid.Children.Add(new TextBlock { Text = key.Corner, FontSize = 11, Foreground = KbInk,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(5, 2, 0, 0) });
            else if (key.Shifted != null)
                grid.Children.Add(new TextBlock { Text = key.Shifted, FontSize = 10, Foreground = KbSecondary,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(5, 2, 0, 0) });
            if (key.Hint != null)
            {
                var badge = KbControllerBadge(ButtonLabel(key.Hint));
                badge.HorizontalAlignment = HorizontalAlignment.Right; badge.VerticalAlignment = VerticalAlignment.Top; badge.Margin = new Thickness(0, 4, 5, 0);
                grid.Children.Add(badge);
            }
            if (key.Kind == KeyKind.Modifier)
            {
                chrome.Indicator = new Border { Width = 14, Height = 2, Background = KbInk,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Hidden };
                grid.Children.Add(chrome.Indicator);
                if (key.Modifiers == KeyModifiers.Shift)
                {
                    chrome.CapsMark = new Border { Width = 11, Height = 1.5, Background = KbInk, Visibility = Visibility.Hidden,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 27, 0, 0) };
                    grid.Children.Add(chrome.CapsMark);
                }
            }
            chrome.Face = new Border { CornerRadius = new CornerRadius(2), BorderThickness = new Thickness(key.Strip ? 0 : 1), Child = grid };
            chrome.FocusRing = new Border { CornerRadius = new CornerRadius(key.Strip ? 5 : 8), BorderThickness = new Thickness(key.Strip ? 2 : 3),
                BorderBrush = KbFocusEdge, Margin = new Thickness(key.Strip ? -1 : -5), IsHitTestVisible = false, Visibility = Visibility.Hidden };
            var body = new Grid(); body.Children.Add(chrome.Face); body.Children.Add(chrome.FocusRing);
            var view = new Border { Width = width, Height = height, Background = Brushes.Transparent,
                Child = body, Cursor = Cursors.Hand, SnapsToDevicePixels = true, Tag = key.Id };
            kbChrome[key.Id] = chrome;
            System.Windows.Automation.AutomationProperties.SetName(view, KbAccessibleName(key));
            view.ToolTip = KbAccessibleName(key);
            var captured = key;
            view.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; KbPointer(captured); };
            view.MouseEnter += delegate { chrome.Hovered = true; UpdateKeyVisuals(); };
            view.MouseLeave += delegate { chrome.Hovered = false; UpdateKeyVisuals(); };
            return view;
        }
        void KbPaintKey(KeyDef key, Border view, bool focused, bool active)
        {
            KbKeyChrome chrome;
            if (!kbChrome.TryGetValue(key.Id, out chrome)) return;
            chrome.Face.Background = active ? KbActiveFace : chrome.Hovered ? KbHoverFace : key.Strip ? Brushes.Transparent : focused ? KbFocusFace : key.Kind == KeyKind.Char ? KbLetterFace : KbCommandFace;
            chrome.Face.BorderBrush = KbKeyEdge;
            chrome.FocusRing.Visibility = focused ? Visibility.Visible : Visibility.Hidden;
            Panel.SetZIndex(view, focused ? 2 : 0);
            if (chrome.Indicator != null) chrome.Indicator.Visibility = active ? Visibility.Visible : Visibility.Hidden;
            if (chrome.CapsMark != null) chrome.CapsMark.Visibility = kbCaps ? Visibility.Visible : Visibility.Hidden;
        }

        void KbRenderHeader()
        {
            var divider = new Border { Width = kbKeys.Width + 12, Height = 1, Background = KbSolid("#333333"), IsHitTestVisible = false };
            Canvas.SetLeft(divider, -6); Canvas.SetTop(divider, 31); kbKeys.Children.Add(divider);
            kbHeaderGrip = new Border { Width = 70, Height = 28, Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Mover teclado (" + ButtonLabel("R3") + ")",
                Child = new Border { Width = 44, Height = 2, Background = KbSolid("#8E8E8E"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            kbHeaderGrip.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; KbSwapDock(); };
            System.Windows.Automation.AutomationProperties.SetName(kbHeaderGrip, "Mover teclado (" + ButtonLabel("R3") + ")");
            Canvas.SetLeft(kbHeaderGrip, (kbKeys.Width - 70) / 2); kbKeys.Children.Add(kbHeaderGrip);
            Canvas.SetLeft(kbStatus, (kbKeys.Width - kbStatus.Width) / 2); kbStatus.IsHitTestVisible = false;
            kbKeys.Children.Add(kbStatus);
        }
    }
}
