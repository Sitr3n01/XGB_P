// Transicoes por desenho/recorte: o conteudo mede uma vez e nunca e esticado.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XGBP.Design
{
    public sealed class MotionHeightSurface : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(MotionHeightSurface),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender,
                delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) {
                    var surface = (MotionHeightSurface)d;
                    if (surface.Changed != null) surface.Changed((double)e.NewValue);
                }));
        public Action<double> Changed;
        public Brush Rail, Panel;
        public Pen Outline;
        public double Value { get { return (double)GetValue(ValueProperty); } }
        protected override Size MeasureOverride(Size availableSize) { return new Size(); }
        protected override void OnRender(DrawingContext drawing)
        {
            double height = Math.Max(1, Value), top = (RenderSize.Height - height) / 2;
            drawing.DrawRoundedRectangle(Rail, Outline, new Rect(.5, top + .5, 91, Math.Max(0, height - 1)), 21.5, 21.5);
            drawing.DrawRoundedRectangle(Panel, Outline, new Rect(108.5, top + .5, 479, Math.Max(0, height - 1)), 21.5, 21.5);
        }
    }

    // Copia os comandos retidos de desenho; nao rasteriza pixels e nao retem controles da UI.
    static class MotionDrawing
    {
        const int MaxVisuals = 2048;
        public static ImageSource Capture(FrameworkElement element)
        {
            int count = 0;
            var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            DrawingVisual copy = Copy(element, true, ref count);
            if (copy == null) return null;
            var brush = new VisualBrush(copy) { AutoLayoutContent = false, ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = bounds, Stretch = Stretch.Fill };
            return new DrawingImage(new GeometryDrawing(brush, null, new RectangleGeometry(brush.Viewbox)));
        }
        static DrawingVisual Copy(Visual source, bool root, ref int count)
        {
            if (++count > MaxVisuals) throw new InvalidOperationException("Transicao excedeu o limite de elementos visuais.");
            var element = source as UIElement;
            if (element != null && element.Visibility != Visibility.Visible) return null;
            var copy = new DrawingVisual();
            var scaling = RenderOptions.GetBitmapScalingMode(source);
            if (scaling != BitmapScalingMode.Unspecified) RenderOptions.SetBitmapScalingMode(copy, scaling);
            var edge = RenderOptions.GetEdgeMode(source);
            if (edge != EdgeMode.Unspecified) RenderOptions.SetEdgeMode(copy, edge);
            var text = TextOptions.GetTextRenderingMode(source);
            if (text != TextRenderingMode.Auto) TextOptions.SetTextRenderingMode(copy, text);
            if (!root)
            {
                copy.Offset = VisualTreeHelper.GetOffset(source);
                var transform = VisualTreeHelper.GetTransform(source);
                if (transform != null) copy.Transform = transform.CloneCurrentValue();
            }
            copy.Opacity = VisualTreeHelper.GetOpacity(source);
            var clip = VisualTreeHelper.GetClip(source);
            if (clip != null) copy.Clip = clip.CloneCurrentValue();
            var mask = VisualTreeHelper.GetOpacityMask(source);
            if (mask != null) copy.OpacityMask = mask.CloneCurrentValue();
            var effect = VisualTreeHelper.GetEffect(source);
            if (effect != null) copy.Effect = effect.CloneCurrentValue();
            var retained = VisualTreeHelper.GetDrawing(source);
            if (retained != null)
            {
                var drawing = retained.IsFrozen ? retained : retained.CloneCurrentValue();
                if (drawing.CanFreeze) drawing.Freeze();
                using (var context = copy.RenderOpen()) context.DrawDrawing(drawing);
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(source); i++)
            {
                var child = VisualTreeHelper.GetChild(source, i) as Visual;
                if (child == null) continue;
                var result = Copy(child, false, ref count);
                if (result != null) copy.Children.Add(result);
            }
            return copy;
        }
    }

    sealed partial class Preview
    {
        bool heightPresenting;
        double heightTarget, heightBody;
        Brush heightRailBackground, heightRailBorder, heightPanelBackground, heightPanelBorder;
        Geometry heightRailClip, heightPanelClip, heightBodyClip;
        Transform heightPanelTransform, heightTopTransform, heightBottomTransform, heightFooterTransform;
        readonly TranslateTransform heightPanelShift = new TranslateTransform(), heightTopShift = new TranslateTransform(),
            heightBottomShift = new TranslateTransform(), heightFooterShift = new TranslateTransform();
        readonly RectangleGeometry heightRailCrop = new RectangleGeometry(), heightPanelCrop = new RectangleGeometry(), heightBodyCrop = new RectangleGeometry();
        MotionHeightSurface HeightSurface { get { return (MotionHeightSurface)window.FindName("HeightSurface"); } }
        double PresentedSidebarHeight { get { return heightPresenting ? HeightSurface.Value : MotionPart("Sidebar").ActualHeight; } }

        double ReleaseSidebarHeight()
        {
            double current = PresentedSidebarHeight;
            CancelElementMotion(HeightSurface);
            ResetHeightPresentation();
            return current;
        }
        void MotionHeight(double before)
        {
            if (!MotionEnabled || before <= 0) return;
            window.UpdateLayout();
            var sidebar = MotionPart("Sidebar"); double after = sidebar.ActualHeight;
            if (Math.Abs(after - before) < 1) return;
            var railFrame = (Border)MotionPart("RailFrame"); var panelFrame = (Border)MotionPart("PanelFrame");
            heightTarget = after; heightBody = MotionPart("BodyScroll").ActualHeight;
            heightRailBackground = railFrame.Background; heightRailBorder = railFrame.BorderBrush;
            heightPanelBackground = panelFrame.Background; heightPanelBorder = panelFrame.BorderBrush;
            heightRailClip = railFrame.Clip; heightPanelClip = panelFrame.Clip; heightBodyClip = MotionPart("BodyScroll").Clip;
            heightPanelTransform = panelFrame.RenderTransform; heightTopTransform = MotionPart("RailTop").RenderTransform;
            heightBottomTransform = MotionPart("RailBottom").RenderTransform; heightFooterTransform = MotionPart("FeedbackFrame").RenderTransform;
            railFrame.Background = railFrame.BorderBrush = panelFrame.Background = panelFrame.BorderBrush = Brushes.Transparent;
            railFrame.Clip = heightRailCrop; panelFrame.Clip = heightPanelCrop; MotionPart("BodyScroll").Clip = heightBodyCrop;
            panelFrame.RenderTransform = HeightTransform(heightPanelTransform, heightPanelShift);
            MotionPart("RailTop").RenderTransform = HeightTransform(heightTopTransform, heightTopShift);
            MotionPart("RailBottom").RenderTransform = HeightTransform(heightBottomTransform, heightBottomShift);
            MotionPart("FeedbackFrame").RenderTransform = HeightTransform(heightFooterTransform, heightFooterShift);
            var surface = HeightSurface;
            surface.Rail = heightRailBackground; surface.Panel = heightPanelBackground; surface.Outline = new Pen(heightPanelBorder.CloneCurrentValue(), 1);
            if (surface.Outline.CanFreeze) surface.Outline.Freeze();
            surface.Changed = PresentHeight; heightPresenting = true; surface.Visibility = Visibility.Visible;
            MotionTo(surface, MotionHeightSurface.ValueProperty, after, MotionNormal, before, 0, ResetHeightPresentation);
        }
        static Transform HeightTransform(Transform original, TranslateTransform shift)
        {
            var group = new TransformGroup();
            if (original != null) group.Children.Add(original);
            group.Children.Add(shift); return group;
        }
        void PresentHeight(double height)
        {
            if (!heightPresenting) return;
            double delta = (heightTarget - height) / 2;
            heightPanelShift.Y = heightTopShift.Y = delta; heightBottomShift.Y = -delta;
            heightFooterShift.Y = height - heightTarget;
            heightRailCrop.Rect = new Rect(0, delta, MotionPart("RailFrame").ActualWidth, Math.Max(0, height));
            heightPanelCrop.Rect = new Rect(0, 0, MotionPart("PanelFrame").ActualWidth, Math.Max(0, height));
            heightBodyCrop.Rect = new Rect(0, 0, MotionPart("BodyScroll").ActualWidth, Math.Max(0, heightBody + height - heightTarget));
        }
        void ResetHeightPresentation()
        {
            if (!heightPresenting) return;
            heightPresenting = false; HeightSurface.Changed = null; HeightSurface.Visibility = Visibility.Collapsed;
            var railFrame = (Border)MotionPart("RailFrame"); var panelFrame = (Border)MotionPart("PanelFrame");
            railFrame.Background = heightRailBackground; railFrame.BorderBrush = heightRailBorder;
            panelFrame.Background = heightPanelBackground; panelFrame.BorderBrush = heightPanelBorder;
            railFrame.Clip = heightRailClip; panelFrame.Clip = heightPanelClip; MotionPart("BodyScroll").Clip = heightBodyClip;
            panelFrame.RenderTransform = heightPanelTransform; MotionPart("RailTop").RenderTransform = heightTopTransform;
            MotionPart("RailBottom").RenderTransform = heightBottomTransform; MotionPart("FeedbackFrame").RenderTransform = heightFooterTransform;
        }
    }
}
