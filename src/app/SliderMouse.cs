// Mouse nos sliders da barra: clicar leva ao ponto, arrastar acompanha e a roda ajusta (ADR 0116).
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        Slider mouseSlider;
        double mouseSliderGrab; // distancia entre o ponteiro e o centro do marcador, ao pegar o proprio marcador

        void StartSliderMouse()
        {
            // Registro na classe: vale para os sliders de todos os paineis, inclusive os reconstruidos.
            EventManager.RegisterClassHandler(typeof(Slider), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnSliderMouseDown));
            EventManager.RegisterClassHandler(typeof(Slider), UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnSliderMouseUp));
            EventManager.RegisterClassHandler(typeof(Slider), UIElement.PreviewMouseMoveEvent, new MouseEventHandler(OnSliderMouseMove));
            EventManager.RegisterClassHandler(typeof(Slider), UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnSliderCaptureLost));
        }
        static Track SliderTrack(Slider slider)
        {
            slider.ApplyTemplate();
            return slider.Template == null ? null : slider.Template.FindName("PART_Track", slider) as Track;
        }
        static double ThumbWidth(Track track) { return track.Thumb == null ? 0 : track.Thumb.ActualWidth; }
        // Calculado pela largura, sem depender do layout anterior: movimentos seguidos nao acumulam erro.
        internal static double SliderValueAt(Slider slider, double x, double width, double thumb)
        {
            double range = slider.Maximum - slider.Minimum, room = width - thumb;
            if (range <= 0 || room <= 0) return slider.Value;
            double value = slider.Minimum + Math.Max(0, Math.Min(1, (x - thumb / 2) / room)) * range;
            if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
                value = slider.Minimum + Math.Round((value - slider.Minimum) / slider.TickFrequency) * slider.TickFrequency;
            return Math.Max(slider.Minimum, Math.Min(slider.Maximum, value));
        }
        internal static double SliderThumbCenter(Slider slider, double width, double thumb)
        {
            double range = slider.Maximum - slider.Minimum;
            return thumb / 2 + (range <= 0 ? 0 : (slider.Value - slider.Minimum) / range) * Math.Max(0, width - thumb);
        }
        bool OwnSlider(object sender, out Slider slider)
        {
            slider = sender as Slider;
            return slider != null && !disposed && Window.GetWindow(slider) == window;
        }
        void OnSliderMouseDown(object sender, MouseButtonEventArgs e)
        {
            Slider slider; if (!OwnSlider(sender, out slider) || !slider.IsEnabled) return;
            var track = SliderTrack(slider); if (track == null) return;
            e.Handled = true;
            slider.Focus();
            BeginSliderDrag(slider, e.GetPosition(track).X, track.ActualWidth, ThumbWidth(track), track.Thumb != null && track.Thumb.IsMouseOver);
            slider.CaptureMouse();
        }
        void BeginSliderDrag(Slider slider, double x, double width, double thumb, bool onThumb)
        {
            mouseSlider = slider;
            mouseSliderGrab = onThumb ? x - SliderThumbCenter(slider, width, thumb) : 0;
            if (!onThumb) MoveSlider(slider, x, width, thumb);
        }
        void MoveSlider(Slider slider, double x, double width, double thumb)
        {
            double value = SliderValueAt(slider, x - mouseSliderGrab, width, thumb);
            if (Math.Abs(value - slider.Value) > .0001) slider.Value = value;
        }
        void OnSliderMouseMove(object sender, MouseEventArgs e)
        {
            Slider slider; if (!OwnSlider(sender, out slider) || slider != mouseSlider || !slider.IsMouseCaptured) return;
            var track = SliderTrack(slider); if (track == null) return;
            MoveSlider(slider, e.GetPosition(track).X, track.ActualWidth, ThumbWidth(track));
            e.Handled = true;
        }
        void OnSliderMouseUp(object sender, MouseButtonEventArgs e)
        {
            Slider slider; if (!OwnSlider(sender, out slider) || slider != mouseSlider) return;
            mouseSlider = null; e.Handled = true;
            if (slider.IsMouseCaptured) slider.ReleaseMouseCapture();
        }
        void OnSliderCaptureLost(object sender, MouseEventArgs e) { if (sender == mouseSlider) mouseSlider = null; }
        bool SliderDragging { get { return mouseSlider != null && mouseSlider.IsMouseCaptured; } }

        // Sobre uma lista que rola, a roda so ajusta o slider ja clicado: passar por cima nao muda o volume de ninguem.
        bool WheelSlider(DependencyObject source, int delta)
        {
            Slider slider = null; ScrollViewer scroll = null;
            for (var node = source; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            {
                if (slider == null) slider = node as Slider;
                else if ((scroll = node as ScrollViewer) != null) break;
            }
            if (slider == null || !slider.IsEnabled || delta == 0 || slider == mediaProgress) return false; // a roda nao avanca a musica
            if (!slider.IsKeyboardFocused && scroll != null && scroll.ScrollableHeight > 0) return false;
            double step = slider.SmallChange >= 1 ? slider.SmallChange : 2; // ajustes da AMD usam o passo do driver
            double value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value + Math.Sign(delta) * step));
            if (Math.Abs(value - slider.Value) > .0001) slider.Value = value;
            return true;
        }
    }
}
