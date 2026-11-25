// Mouse nos sliders (ADR 0116): area clicavel do modelo real, conversao do ponto e roda. Nenhum audio real muda.
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static T SliderPart<T>(DependencyObject hit) where T : class
        {
            for (var node = hit; node != null; node = VisualTreeHelper.GetParent(node)) { var found = node as T; if (found != null) return found; }
            return null;
        }
        void VerifySliderMouse()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) { checks++; if (!ok) throw new Exception("Mouse nos sliders: " + message); };
            // Comando substituido pela fila: a verificacao nunca aplica audio (audioTimer parado em verify).
            Show(); SetPanel(6); window.UpdateLayout();
            var binding = audioBindings.Find(b => b.Key == "master");
            check(binding != null && binding.Slider.IsEnabled, "volume do PC disponivel no exemplo");
            var slider = binding.Slider; var track = SliderTrack(slider);
            check(track != null && track.ActualWidth > 100, "trilho do slider montado");
            foreach (var any in audioBindings)
            {
                int answered = 0, thumbs = 0; double height = any.Slider.ActualHeight, width = any.Slider.ActualWidth;
                for (double y = .5; y < height; y++)
                {
                    var left = VisualTreeHelper.HitTest(any.Slider, new Point(width * .25, y)); var right = VisualTreeHelper.HitTest(any.Slider,
                        new Point(width * .75, y));
                    if (left != null && right != null) answered++;
                    var middle = VisualTreeHelper.HitTest(any.Slider, any.Slider.TranslatePoint(new Point(0, 0), any.Slider) + new Vector(
                        SliderTrack(any.Slider).TranslatePoint(new Point(SliderThumbCenter(any.Slider, SliderTrack(any.Slider).ActualWidth,
                            ThumbWidth(SliderTrack(any.Slider))), 0), any.Slider).X, y));
                    if (middle != null && SliderPart<Thumb>(middle.VisualHit) != null) thumbs++;
                }
                check(answered >= (int)height, "toda a altura responde ao mouse: " + any.Key + " " + answered + " de " + height);
                check(thumbs >= 18, "marcador continua alcancavel: " + any.Key + " " + thumbs);
            }
            double w = track.ActualWidth, t = ThumbWidth(track);
            check(SliderValueAt(slider, t / 2, w, t) == slider.Minimum && SliderValueAt(slider, w - t / 2, w, t) == slider.Maximum, "extremos");
            check(SliderValueAt(slider, w / 2, w, t) == 50, "centro");
            check(SliderValueAt(slider, -500, w, t) == 0 && SliderValueAt(slider, w + 500, w, t) == 100, "arrastar para fora fica no limite");
            check(SliderValueAt(slider, w * .333, w, t) == Math.Round(SliderValueAt(slider, w * .333, w, t)), "encaixe de 1 em 1");
            var step = new Slider { Minimum = 30, Maximum = 130, TickFrequency = 20, IsSnapToTickEnabled = true, Value = 30 };
            check(SliderValueAt(step, 120, 200, 0) == 90, "passo do driver contado a partir do minimo");
            var fixedRange = new Slider { Minimum = 5, Maximum = 5, Value = 5 };
            check(SliderValueAt(fixedRange, 50, 100, 6) == 5, "faixa vazia nao divide por zero");

            double before = slider.Value; audioPending.Clear();
            BeginSliderDrag(slider, w * .25, w, t, false);
            check(Math.Abs(slider.Value - 25) <= 1 && audioPending.Count == 1, "clique leva ao ponto e enfileira um ajuste: " + slider.Value);
            foreach (double x in new[] { .4, .6, .8 }) MoveSlider(slider, w * x, w, t);
            check(Math.Abs(slider.Value - 80) <= 1 && audioPending.Count == 1, "arraste acompanha e mantem so o ultimo valor: " + slider.Value);
            double value = slider.Value, center = SliderThumbCenter(slider, w, t);
            BeginSliderDrag(slider, center + 2, w, t, true);
            check(slider.Value == value, "pegar o marcador nao salta");
            MoveSlider(slider, center + 2, w, t); check(slider.Value == value, "marcador parado nao muda");
            MoveSlider(slider, center + 2 - w * .3, w, t); check(Math.Abs(slider.Value - (value - 30)) <= 1.5, "marcador acompanha o ponteiro");
            mouseSlider = null; mouseSliderGrab = 0;

            // Ligacao dos eventos: aperto e soltura chegam pela classe, com captura so entre eles.
            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent };
            track.RaiseEvent(down);
            check(mouseSlider == slider && slider.IsKeyboardFocused, "aperto do mouse chega ao slider e da foco");
            track.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent =
                Mouse.PreviewMouseUpEvent });
            check(mouseSlider == null && !slider.IsMouseCaptured, "soltar encerra o arraste e a captura");
            slider.Value = 40; value = slider.Value;
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
            track.RaiseEvent(wheel); check(wheel.Handled && slider.Value == 42, "roda chega ao slider pela janela");

            value = slider.Value; check(WheelSlider(track, 120) && slider.Value == value + 2, "roda para cima aumenta");
            check(WheelSlider(track, -120) && slider.Value == value, "roda para baixo diminui");
            slider.Value = 100; check(WheelSlider(track, 120) && slider.Value == 100, "roda para no limite");
            slider.IsEnabled = false; check(!WheelSlider(track, -120) && slider.Value == 100, "slider inativo ignora a roda"); slider.IsEnabled = true;
            check(!WheelSlider(binding.Mute, 120), "fora do slider a roda continua da lista");
            var rolling = new Slider { Maximum = 100, Value = 40, Height = 34, Style = (Style)window.FindResource("VolumeSlider") };
            var list = new StackPanel(); list.Children.Add(rolling); list.Children.Add(new Border { Height = 4000 });
            var viewer = new ScrollViewer { Content = list, Height = 120 }; body.Children.Add(viewer); window.UpdateLayout();
            check(viewer.ScrollableHeight > 0 && !WheelSlider(SliderTrack(rolling), 120) && rolling.Value == 40, "lista que rola nao muda volume sem clique");
            check(rolling.Focus() && WheelSlider(SliderTrack(rolling), 120) && rolling.Value == 42, "slider clicado aceita a roda na lista");
            rolling.SmallChange = 5; check(WheelSlider(SliderTrack(rolling), 120) && rolling.Value == 47, "passo proprio do ajuste");
            check(mediaProgress == null || !WheelSlider(mediaProgress, 120), "roda nao avanca a midia");
            slider.Value = before; audioPending.Clear(); SetPanel(0); Hide();
            File.WriteAllText(Path.Combine(Output, "sliders-mouse-verificacao.txt"), "Mouse nos sliders: " + checks + " verificacoes aprovadas.\r\n" +
                "Area clicavel medida no modelo real; arraste pela logica comum, sem mover o ponteiro do usuario. Nenhum volume real " +
                    "alterado.\r\n", Encoding.UTF8);
        }
    }
}
