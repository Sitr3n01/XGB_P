// Contrastes extremos, troca em controles existentes, falhas e isolamento do destaque (ADR 0055).
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyAccentTheme()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool value, string label) {
                checks++; if (!value) throw new Exception("Falha no destaque: " + label);
            };
            var savedRead = accentRead;
            var original = accentPalette.Source;
            try
            {
                var surfaces = AccentSurfaces(); var meter = Brush("MeterTrack").Color;
                check(Math.Abs(AccentPalette.Contrast(Colors.Black, Colors.White) - 21) < .000001, "preto/branco = 21:1");
                check(Math.Abs(AccentPalette.Luminance(Color.FromRgb(0x77, 0x77, 0x77)) - .1844749945) < .00000001, "luminancia sRGB conhecida");
                check(AccentPalette.Contrast(Color.FromRgb(0x77, 0x77, 0x77), Colors.White) < 4.5, "4,478 nao e arredondado para aprovar texto");
                byte[] channels = { 0, 1, 32, 80, 128, 200, 254, 255 };
                foreach (byte r in channels) foreach (byte g in channels) foreach (byte b in channels)
                {
                    var raw = Color.FromRgb(r, g, b); var p = AccentPalette.Create(raw, surfaces, meter);
                    check(p.Source == raw && p.Fill.A == 255 && p.Focus.A == 255 && p.Text.A == 255 &&
                        AccentPalette.Minimum(p.Focus, surfaces) >= 3 && AccentPalette.Minimum(p.Text, surfaces) >= 4.5 &&
                        AccentPalette.Minimum(p.Fill, surfaces) >= 3 && AccentPalette.Contrast(p.Fill, meter) >= 3,
                        "grade RGB " + AccentPalette.Hex(raw));
                }
                var yellow = AccentPalette.Create(Colors.Yellow, surfaces, meter);
                check(yellow.Fill == Colors.Yellow && yellow.Focus == Colors.Yellow && yellow.Text == Colors.Yellow, "cor que passa permanece exata");
                check(AccentPalette.OverWhite(Color.FromArgb(0, 0, 0, 0)) == Colors.White, "alfa considera fundo externo claro conservador");

                Show(); SetPanel(0); rail[0].Focus(); window.UpdateLayout();
                var focused = Keyboard.FocusedElement; var firstControl = actions[0];
                int currentPanel = panel, children = body.Children.Count;
                var fill = Brush("XboxGreen"); var focus = Brush("FocusGreen"); var text = Brush("AccentText");
                var directText = Text("destaque", 16, "AccentText");
                Color critical = Brush("Critical").Color, close = Brush("CloseRed").Color, warning = Brush("FeedbackWarning").Color;
                Color surface = Brush("SurfacePanel").Color, keyFocus = ((SolidColorBrush)KbFocusEdge).Color;
                check(!fill.IsFrozen && !focus.IsFrozen && !text.IsFrozen, "pinceis compartilhados aceitam atualizacao apos aplicar estilos");
                var templateFrame = (Border)rail[0].Template.FindName("Frame", rail[0]);
                // WPF pode clonar Freezables no template; a cor visivel deve acompanhar o recurso mesmo assim.
                check(templateFrame != null && templateFrame.BorderBrush is SolidColorBrush && ((SolidColorBrush)templateFrame.BorderBrush).Color == focus.Color,
                    "template existente recebe a cor do foco; focado=" + rail[0].IsKeyboardFocused + "; borda=" + (templateFrame == null ? "ausente" : templateFrame.BorderBrush.ToString()));
                Color next = Colors.Blue;
                accentRead = delegate { return next; }; RefreshAccentTheme(); window.UpdateLayout();
                check(Brush("XboxGreen") == fill && Brush("FocusGreen") == focus && Brush("AccentText") == text &&
                    text.Color == accentPalette.Text && focus.Color == accentPalette.Focus && fill.Color == accentPalette.Fill &&
                    directText.Foreground == text && ((SolidColorBrush)templateFrame.BorderBrush).Color == accentPalette.Focus,
                    "troca alcanca estilos e referencias C# existentes");
                check(Keyboard.FocusedElement == focused && panel == currentPanel && body.Children.Count == children && actions[0] == firstControl,
                    "troca preserva foco, painel e controles");
                check(Brush("Critical").Color == critical && Brush("CloseRed").Color == close && Brush("FeedbackWarning").Color == warning &&
                    Brush("SurfacePanel").Color == surface && ((SolidColorBrush)KbFocusEdge).Color == keyFocus, "superficies, alertas e teclado cinza preservados");
                int revision = accentRevision; RefreshAccentTheme();
                check(revision == accentRevision, "evento repetido da mesma cor nao reaplica recursos");
                accentRead = delegate { return null; }; RefreshAccentTheme();
                check(accentPalette.Source == next && revision == accentRevision, "leitura falha mantem ultima cor valida");
                accentRead = delegate { throw new InvalidOperationException("fixture"); }; RefreshAccentTheme();
                check(accentPalette.Source == next && revision == accentRevision, "excecao nao derruba nem apaga cor valida");
                accentRead = delegate { return Colors.Transparent; }; RefreshAccentTheme();
                check(accentPalette.Source == next && revision == accentRevision, "cor transparente nao e aceita como destaque valido");
                next = Colors.Yellow; accentRead = delegate { return next; };
                WindowsAccentChanged(null, null);
                var frame = new DispatcherFrame();
                app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { frame.Continue = false; }));
                Dispatcher.PushFrame(frame);
                check(accentPalette.Source == next && !accentReadFailed, "callback passa pela Dispatcher e recupera leitura");

                string[] names = { "azul", "amarelo", "vermelho", "preto", "branco" };
                Color[] colors = { Colors.Blue, Colors.Yellow, Colors.Red, Colors.Black, Colors.White };
                for (int n = 0; n < colors.Length; n++)
                { next = colors[n]; RefreshAccentTheme(); window.UpdateLayout(); SaveRender("destaque-" + names[n]); }
                Hide(); next = Colors.Purple; RefreshAccentTheme();
                check(!window.IsVisible && accentPalette.Source == next, "troca oculta nao abre a barra");
                accentPalette = null; accentRead = delegate { return null; }; RefreshAccentTheme();
                check(accentPalette.Source == AccentPalette.Fallback && accentReadFailed, "primeira leitura falha usa verde com contraste");
            }
            finally
            {
                accentRead = delegate { return original; }; RefreshAccentTheme(); accentRead = savedRead;
            }
            File.WriteAllText(Path.Combine(Output, "destaque-verificacao.txt"), "OK: " + checks + " verificacoes do destaque do Windows (ADR 0055).\r\n" +
                "512 cores RGB, ancoras sRGB, 3:1 para preenchimento/foco e 4,5:1 para texto; alfa conservador; cores claras exatas; troca em estilos e controles existentes sem perder foco; " +
                "eventos repetidos, Dispatcher, falha inicial/transitoria, recuperacao e barra oculta. Superficies, cores semanticas e teclado cinza preservados.\r\n" +
                "Renders reais da arvore WPF com cores substituidas. O teste nao altera o destaque do Windows nem comprova uma notificacao real do sistema.\r\n", Encoding.UTF8);
        }
    }
}
