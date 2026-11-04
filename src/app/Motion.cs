// Movimento da previa: clocks substituiveis, sem alterar layout ou executar comandos.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XGBP.Design
{
    sealed class MotionScroll : DependencyObject
    {
        public static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached("Owner", typeof(FrameworkElement), typeof(MotionScroll));
        public static readonly DependencyProperty WiredProperty = DependencyProperty.RegisterAttached("Wired", typeof(bool), typeof(MotionScroll), new PropertyMetadata(false));
        public static readonly DependencyProperty XProperty = DependencyProperty.RegisterAttached("X", typeof(double), typeof(MotionScroll),
            new PropertyMetadata(0.0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ScrollViewer)d).ScrollToHorizontalOffset((double)e.NewValue); }));
        public static readonly DependencyProperty YProperty = DependencyProperty.RegisterAttached("Y", typeof(double), typeof(MotionScroll),
            new PropertyMetadata(0.0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue); }));
    }
    sealed partial class Preview
    {
        const int MotionFast = 83, MotionNormal = 167, MotionEntrance = 250;
        sealed class MotionClock
        {
            public DependencyObject Target;
            public DependencyProperty Property;
            public AnimationClock Clock;
            public FrameworkElement Owner;
            public double End;
        }
        readonly List<MotionClock> motionClocks = new List<MotionClock>();
        readonly HashSet<Control> motionButtons = new HashSet<Control>();
        readonly HashSet<string> motionImages = new HashSet<string>();
#pragma warning disable 0649 // ganchos de teste: atribuidos somente na build XGB_VERIFY
        bool motionTesting, motionReduced, motionClosing, motionBuilding;
        bool? motionSystemOverride;
#pragma warning restore 0649
        int motionCloseVersion, motionPanelDirection;
        string motionPanelAxis;
        long motionStarted;
        readonly DispatcherTimer motionTicker = new DispatcherTimer(DispatcherPriority.Render);
        bool SystemMotionOff { get { return motionSystemOverride ?? !SystemParameters.ClientAreaAnimation; } }
        bool MotionEnabled { get { return (!verify || motionTesting) && !motionReduced && !SystemMotionOff && window.IsVisible && !disposed; } }
        FrameworkElement MotionPart(string name) { return (FrameworkElement)window.FindName(name); }

        void StartMotion()
        {
            SystemParameters.StaticPropertyChanged += MotionSystemChanged;
            EventManager.RegisterClassHandler(typeof(Thumb), FrameworkElement.LoadedEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs e) {
                var thumb = (Thumb)sender;
                if (Window.GetWindow(thumb) != window || (bool)thumb.GetValue(MotionScroll.WiredProperty)) return;
                DependencyObject node = thumb;
                while (node != null && !(node is ScrollBar)) node = VisualTreeHelper.GetParent(node);
                var bar = node as ScrollBar; if (bar == null) return;
                thumb.SetValue(MotionScroll.WiredProperty, true);
                Action update = delegate { MotionTo(thumb, bar.Orientation == Orientation.Vertical ? FrameworkElement.WidthProperty : FrameworkElement.HeightProperty,
                    thumb.IsMouseOver || thumb.IsDragging ? 6 : 3, MotionFast); };
                thumb.MouseEnter += delegate { update(); }; thumb.MouseLeave += delegate { update(); };
                thumb.DragStarted += delegate { update(); }; thumb.DragCompleted += delegate { update(); };
                thumb.Unloaded += delegate { CancelElementMotion(thumb); };
            }));
            motionTicker.Interval = TimeSpan.FromMilliseconds(33);
            motionTicker.Tick += delegate {
                if (!window.IsVisible || motionClosing || motionReduced || SystemMotionOff) { motionTicker.Stop(); return; }
                // Apenas apresentacao: o setter fica sob mediaSync e nunca gera seek.
                UpdateMediaClock();
                var selected = MediaSelected();
                if (!MediaPanel() || selected == null || !selected.Playing) motionTicker.Stop();
            };
            window.IsVisibleChanged += delegate { if (!window.IsVisible) StopMotion(); };
            StartRailFocus();
            window.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) {
                DependencyObject node = e.OriginalSource as DependencyObject;
                while (node != null && !(node is ScrollViewer)) node = node is Visual ? VisualTreeHelper.GetParent(node) : null;
                var scroll = node as ScrollViewer;
                if (scroll == null || scroll.ScrollableHeight <= 0 || !MotionEnabled) return;
                SmoothScroll(scroll, scroll.VerticalOffset - Math.Sign(e.Delta) * 48, false, true); e.Handled = true;
            };
        }
        void MotionSystemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "ClientAreaAnimation") return;
            Dispatch(delegate { SettleMotion(); UpdateMotionSetting(); });
        }
        static void ApplyClock(DependencyObject target, DependencyProperty property, AnimationClock clock)
        {
            var element = target as UIElement;
            if (element != null) element.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
            else ((Animatable)target).ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
        }
        void MotionTo(DependencyObject target, DependencyProperty property, double end, int ms, double? start = null, int delay = 0, Action completed = null, bool exit = false)
        {
            double current = start ?? (double)target.GetValue(property);
            for (int i = motionClocks.Count - 1; i >= 0; i--)
                if (motionClocks[i].Target == target && motionClocks[i].Property == property)
                { current = (double)target.GetValue(property); ApplyClock(target, property, null); motionClocks[i].Clock.Controller.Remove(); motionClocks.RemoveAt(i); }
            target.SetCurrentValue(property, end);
            if (!MotionEnabled || ms <= 0 || Math.Abs(current - end) < .0001 && delay == 0) { if (completed != null) completed(); return; }
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms + delay), FillBehavior = FillBehavior.HoldEnd };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(current, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (delay > 0) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(current, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(end, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms + delay)),
                new KeySpline(exit ? 1 : 0, 0, exit ? 1 : 0, 1)));
            var item = new MotionClock { Target = target, Property = property, Owner = target as FrameworkElement ?? target.GetValue(MotionScroll.OwnerProperty) as FrameworkElement,
                End = end, Clock = (AnimationClock)animation.CreateClock(true) };
            motionClocks.Add(item); motionStarted++;
            item.Clock.Completed += delegate {
                if (!motionClocks.Remove(item)) return;
                ApplyClock(target, property, null); item.Clock.Controller.Remove(); target.SetCurrentValue(property, end);
                if (completed != null) completed();
            };
            ApplyClock(target, property, item.Clock);
            item.Clock.Controller.Begin(); item.Clock.Controller.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
        }
        void StopMotion()
        {
            EndOpeningPresentation("interrompida", false);
            motionTicker.Stop();
            foreach (var item in motionClocks) { ApplyClock(item.Target, item.Property, null); item.Clock.Controller.Remove(); item.Target.SetCurrentValue(item.Property, item.End); }
            motionClocks.Clear();
            ((Canvas)window.FindName("MotionOverlay")).Children.Clear();
            ResetHeightPresentation();
        }
        void SettleMotion()
        {
            bool closing = motionClosing; StopMotion();
            foreach (var button in motionButtons) {
                var scale = MotionScale(button);
                if (scale != null) scale.ScaleX = scale.ScaleY = 1;
            }
            if (closing && steamNavigationClosing) { steamNavigationExitComplete = true; TrySteamNavigation(); }
            else if (closing && !steamClosingVisual) Hide(true);
            else QueueOpeningVisuals();
        }
        // Botao com a escala de foco (WireMotion): a translacao entra num grupo com a escala, sem substitui-la.
        // Substituir derrubava a previa no clique seguinte (InvalidCastException, 2026-10-24 09:32).
        TranslateTransform MotionTranslation(FrameworkElement element)
        {
            var transform = element.RenderTransform as TranslateTransform;
            var group = element.RenderTransform as TransformGroup;
            if (transform == null && group != null) foreach (var child in group.Children) if (child is TranslateTransform) transform = (TranslateTransform)child;
            if (transform == null)
            {
                transform = new TranslateTransform();
                var scale = element.RenderTransform as ScaleTransform;
                if (scale != null) { group = new TransformGroup(); group.Children.Add(scale); element.RenderTransform = group; }
                if (group != null) group.Children.Add(transform); else element.RenderTransform = transform;
            }
            transform.SetValue(MotionScroll.OwnerProperty, element);
            return transform;
        }
        // Escala de foco do botao, direta ou dentro do grupo criado pela translacao.
        static ScaleTransform MotionScale(UIElement element)
        {
            var scale = element.RenderTransform as ScaleTransform; if (scale != null) return scale;
            var group = element.RenderTransform as TransformGroup;
            if (group != null) foreach (var child in group.Children) if (child is ScaleTransform) return (ScaleTransform)child;
            return null;
        }
        void MotionReveal(FrameworkElement element, int ms = MotionNormal, double x = 0, double y = 0, int delay = 0)
        {
            if (element == null || !MotionEnabled) return;
            if (x != 0 || y != 0) {
                var transform = MotionTranslation(element);
                MotionTo(transform, TranslateTransform.XProperty, 0, ms, x, delay);
                MotionTo(transform, TranslateTransform.YProperty, 0, ms, y, delay);
            }
            MotionTo(element, UIElement.OpacityProperty, 1, ms, .25, delay);
        }
        void MotionOpen(bool fresh)
        {
            var sidebar = MotionPart("Sidebar"); var transform = MotionTranslation(sidebar);
            sidebar.IsHitTestVisible = true;
            motionClosing = false; motionCloseVersion++;
            BeginOpeningPresentation();
            // A entrada dura 250 ms; a confirmacao Steam controla somente disponibilidade e atenuacao.
            int entrance = MotionEntrance;
            MotionTo(transform, TranslateTransform.XProperty, 0, entrance, fresh ? (double?)-(sceneEdge + sidebar.ActualWidth) : null);
            MotionTo(sidebar, UIElement.OpacityProperty, 1, entrance, fresh ? (double?).25 : null, 0,
                delegate { EndOpeningPresentation("concluida", true); });
            MotionReveal(MotionPart("StatusArea"));
            if (fresh) {
                int group = 0;
                foreach (FrameworkElement child in body.Children) {
                    if (child.ActualHeight <= 0) continue;
                    var point = child.TranslatePoint(new Point(), MotionPart("BodyScroll"));
                    if (point.Y + child.ActualHeight < 0 || point.Y > MotionPart("BodyScroll").ActualHeight) continue;
                    MotionReveal(child, MotionNormal, 0, 12, Math.Min(group++, 3) * 20);
                }
            }
            MotionResumeMedia();
        }
        void Dismiss()
        {
            if (motionClosing) return;
            if (ReturnHomeWithoutGame()) return;
            AnimateDismiss(delegate { Hide(true); });
        }
        void AnimateDismiss(Action completed)
        {
            StopDescription();
            Sound("fechar");
            ClearHeld(); motionTicker.Stop();
            motionClosing = true; int version = ++motionCloseVersion;
            if (!MotionEnabled) { completed(); return; }
            MotionPart("Sidebar").IsHitTestVisible = false;
            // Nenhum clock de midia ou de conteudo sobrevive ao fechamento.
            StopMotion();
            MotionTo(MotionTranslation(MotionPart("Sidebar")), TranslateTransform.XProperty, -48, MotionNormal, null, 0, null, true);
            MotionTo(MotionPart("StatusArea"), UIElement.OpacityProperty, 0, MotionNormal);
            MotionTo(MotionPart("Sidebar"), UIElement.OpacityProperty, 0, MotionNormal, null, 0, delegate { if (motionClosing && version == motionCloseVersion) completed(); }, true);
        }
        void MotionPanel(int before, int after)
        {
            if (!motionBuilding || before == after) { motionPanelDirection = 0; motionPanelAxis = null; return; }
            int[] order = RailOrder;
            int direction = motionPanelDirection != 0 ? motionPanelDirection : Math.Sign(Array.IndexOf(order, after) - Array.IndexOf(order, before));
            bool horizontal = motionPanelAxis == "home";
            motionPanelDirection = 0; motionPanelAxis = null;
            MotionReveal(MotionPart("PanelContent"), MotionNormal, horizontal ? direction * 24 : 0, horizontal ? 0 : direction * 24);
        }
        void WireMotion(Button button)
        {
            if (!motionButtons.Add(button)) return;
            button.RenderTransformOrigin = new Point(.5, .5); button.RenderTransform = new ScaleTransform(1, 1);
            button.RenderTransform.SetValue(MotionScroll.OwnerProperty, button);
            button.GotKeyboardFocus += delegate {
                MotionFocus(button);
                // Brilho local breve, na cor do contorno (vermelho nos botoes de fechar); o contorno do template continua imediato.
                if (MotionEnabled) {
                    string tone = button.Style == window.TryFindResource("CloseAction") ? "CloseRed" : "FocusGreen";
                    var glow = new System.Windows.Media.Effects.DropShadowEffect { Color = Brush(tone).Color, ShadowDepth = 0, BlurRadius = 8, Opacity = .55 };
                    System.Windows.Data.BindingOperations.SetBinding(glow, System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
                        new System.Windows.Data.Binding("Color") { Source = Brush(tone) });
                    glow.SetValue(MotionScroll.OwnerProperty, button);
                    button.Effect = glow;
                    MotionTo(glow, System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, 0, MotionFast, .55, 0, delegate { if (button.Effect == glow) button.Effect = null; });
                }
            };
            button.LostKeyboardFocus += delegate { MotionFocus(button); };
            button.MouseEnter += delegate { MotionHover(button, true); };
            button.MouseLeave += delegate { MotionHover(button, false); };
            button.Click += delegate {
                var scale = MotionScale(button);
                if (!button.IsEnabled || !window.IsVisible || motionClosing || scale == null) return;
                MotionTo(scale, ScaleTransform.ScaleXProperty, .98, MotionFast, null, 0, delegate { MotionFocus(button); });
                MotionTo(scale, ScaleTransform.ScaleYProperty, .98, MotionFast);
            };
            button.Unloaded += delegate { motionButtons.Remove(button); CancelElementMotion(button); };
        }
        // Trilho em descanso: com o foco no painel, os icones ficam um pouco menores e apagados, como a navegacao do Xbox
        // ao entrar no conteudo; voltar o foco ao trilho, ou passar o mouse nele, o desperta. Cada icone encolhe no proprio
        // lugar (escala propria, depois da escala de foco), sem deslocar o trilho. Com movimento reduzido, muda sem animar.
        const double RailRestOpacity = .55, RailRestScale = .9;
        readonly List<ScaleTransform> railRest = new List<ScaleTransform>();
        bool railResting;
        void StartRailFocus()
        {
            foreach (var button in rail)
            {
                var group = new TransformGroup(); group.Children.Add(MotionScale(button) ?? new ScaleTransform(1, 1));
                var rest = new ScaleTransform(1, 1); rest.SetValue(MotionScroll.OwnerProperty, button);
                group.Children.Add(rest); button.RenderTransform = group; railRest.Add(rest);
            }
            window.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(delegate(object sender, KeyboardFocusChangedEventArgs e) {
                RailFocus(e.NewFocus as DependencyObject);
            }), true);
            var frame = MotionPart("RailFrame");
            frame.MouseEnter += delegate { RailFocus(null); }; frame.MouseLeave += delegate { RailFocus(null); };
        }
        // Focos de passagem (a janela durante uma reconstrucao, por exemplo) nao mudam o estado.
        void RailFocus(DependencyObject focused)
        {
            focused = focused ?? Keyboard.FocusedElement as DependencyObject;
            bool inRail = focused is Button && rail.Contains((Button)focused);
            bool inPanel = !inRail && focused is Visual && MotionPart("PanelFrame").IsAncestorOf(focused);
            if (!inRail && !inPanel) return;
            bool resting = inPanel && !MotionPart("RailFrame").IsMouseOver;
            if (resting == railResting) return;
            railResting = resting;
            MotionTo(MotionPart("RailItems"), UIElement.OpacityProperty, resting ? RailRestOpacity : 1, MotionNormal);
            foreach (var rest in railRest)
            {
                MotionTo(rest, ScaleTransform.ScaleXProperty, resting ? RailRestScale : 1, MotionNormal);
                MotionTo(rest, ScaleTransform.ScaleYProperty, resting ? RailRestScale : 1, MotionNormal);
            }
        }
#if XGB_VERIFY
        void VerifyRailFocus()
        {
            Func<double, bool> at = delegate(double scale) {
                return Math.Abs(MotionPart("RailItems").Opacity - (scale < 1 ? RailRestOpacity : 1)) < .001 && railRest.TrueForAll(s => Math.Abs(s.ScaleX - scale) < .001 && Math.Abs(s.ScaleY - scale) < .001);
            };
            returnToHome = false; SetPanel(0); Show(); window.UpdateLayout();
            rail[0].Focus(); MotionAssert(at(1), "trilho em foco fica inteiro");
            FocusHome("steam-home"); MotionAssert(at(RailRestScale), "foco no painel deixa o trilho em descanso");
            MotionAssert(MotionScale(rail[0]) != null && MotionScale(rail[0]).ScaleX == 1, "escala de foco do icone continua a primeira do grupo");
            Keyboard.Focus(window); MotionAssert(at(RailRestScale), "foco de passagem nao desperta o trilho");
            FocusHome("utility-library"); Navigate("ESQUERDA"); MotionAssert(Keyboard.FocusedElement == rail[0] && at(1), "voltar ao trilho o desperta");
            Navigate("DIREITA"); MotionAssert(at(RailRestScale), "entrar no painel pelo trilho o poe em descanso");
            window.UpdateLayout(); SaveRender("trilho-em-descanso");
            rail[0].Focus();
        }
#endif
        void MotionHover(Button button, bool over)
        {
            var shade = button.Template == null ? null : button.Template.FindName("HoverShade", button) as FrameworkElement;
            if (shade != null) MotionTo(shade, UIElement.OpacityProperty, over ? 1 : 0, MotionFast);
        }
        void SmoothScroll(ScrollViewer scroll, double offset, bool horizontal, bool smooth)
        {
            double current = horizontal ? scroll.HorizontalOffset : scroll.VerticalOffset;
            offset = Math.Max(0, Math.Min(horizontal ? scroll.ScrollableWidth : scroll.ScrollableHeight, offset));
            var focused = Keyboard.FocusedElement as FrameworkElement;
            bool contains = focused != null && scroll.IsAncestorOf(focused);
            if (contains) {
                var at = focused.TranslatePoint(new Point(), scroll);
                double p = (horizontal ? at.X : at.Y) + current, size = horizontal ? focused.ActualWidth : focused.ActualHeight;
                double viewport = horizontal ? scroll.ViewportWidth : scroll.ViewportHeight;
                // Deslocar imediatamente quando revelar o foco exigir sair da viewport atual.
                if (p - current < 2 || p + size - current > viewport - 2) smooth = false;
                if (smooth) offset = Math.Max(Math.Max(0, p + size + 2 - viewport), Math.Min(offset, Math.Max(0, p - 2)));
            }
            MotionTo(scroll, horizontal ? MotionScroll.XProperty : MotionScroll.YProperty, offset, smooth ? MotionNormal : 0, current);
            if (!smooth) { if (horizontal) scroll.ScrollToHorizontalOffset(offset); else scroll.ScrollToVerticalOffset(offset); }
        }
        void MotionFocus(Button button)
        {
            var scale = MotionScale(button); if (scale == null) return;
            bool icon = rail.Contains(button) || button.Width > 0 && button.Width <= 80 || homeUtilities.Contains(button);
            if (openAppsRow.Contains(button)) button.RenderTransformOrigin = new Point(openAppsRow.IndexOf(button) == 0 ? 0 : openAppsRow.IndexOf(button) == openAppsRow.Count - 1 ? 1 : .5, .5);
            double target = !MotionEnabled || !button.IsKeyboardFocused ? 1 : icon ? 1.04 : 1.02;
            MotionTo(scale, ScaleTransform.ScaleXProperty, target, MotionNormal);
            MotionTo(scale, ScaleTransform.ScaleYProperty, target, MotionNormal);
        }
        void CancelElementMotion(FrameworkElement element)
        {
            for (int i = motionClocks.Count - 1; i >= 0; i--) {
                var item = motionClocks[i];
                if (item.Target == element || item.Target == element.RenderTransform || MotionInside(item.Owner, element)) {
                    ApplyClock(item.Target, item.Property, null); item.Clock.Controller.Remove(); item.Target.SetCurrentValue(item.Property, item.End); motionClocks.RemoveAt(i);
                }
            }
        }
        static bool MotionInside(DependencyObject child, DependencyObject ancestor)
        {
            // Conteudo novo pode ter apenas pai logico, antes de Measure/ApplyTemplate.
            var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>();
            if (child != null) pending.Push(child);
            while (pending.Count > 0) {
                var node = pending.Pop(); if (node == ancestor) return true; if (!seen.Add(node)) continue;
                var parent = LogicalTreeHelper.GetParent(node); if (parent != null) pending.Push(parent);
                var visual = node as Visual; if (visual != null) { parent = VisualTreeHelper.GetParent(visual); if (parent != null) pending.Push(parent); }
                var element = node as FrameworkElement; if (element != null && element.TemplatedParent != null) pending.Push(element.TemplatedParent);
            }
            return false;
        }
        void MotionText(TextBlock element, string text, int ms = MotionFast)
        {
            if (element.Text == text) return;
            element.Text = text; MotionReveal(element, ms);
        }
        void MotionResumeMedia()
        {
            var selected = MediaSelected();
            if (MotionEnabled && !motionClosing && MediaPanel() && selected != null && selected.Playing) motionTicker.Start();
        }
        void MotionVolume(Slider slider, double value)
        {
            double before = slider.Value; slider.Value = value;
            if (Math.Abs(before - value) < .001 || slider.IsMouseCaptureWithin || slider.IsKeyboardFocused || !MotionEnabled || value <= 0) return;
            slider.ApplyTemplate();
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track == null || track.DecreaseRepeatButton == null) return;
            var fill = track.DecreaseRepeatButton;
            var scale = fill.RenderTransform as ScaleTransform;
            if (scale == null) { scale = new ScaleTransform(1, 1); fill.RenderTransform = scale; fill.RenderTransformOrigin = new Point(0, .5); }
            scale.SetValue(MotionScroll.OwnerProperty, fill);
            MotionTo(scale, ScaleTransform.ScaleXProperty, 1, MotionFast, Math.Max(0, before) / value);
        }
        void MotionCancelVolume(Slider slider)
        {
            slider.ApplyTemplate(); var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track != null && track.DecreaseRepeatButton != null) {
                CancelElementMotion(track.DecreaseRepeatButton);
                var scale = track.DecreaseRepeatButton.RenderTransform as ScaleTransform;
                if (scale != null) scale.ScaleX = scale.ScaleY = 1;
            }
        }
        // Captura local de saida: camada visual sem foco/hit test, removida ao concluir.
        void MotionExit(FrameworkElement element, int duration = MotionNormal)
        {
            if (!MotionEnabled || element == null || element.ActualWidth < 1 || element.ActualHeight < 1 || !element.IsVisible) return;
            var overlay = (Canvas)window.FindName("MotionOverlay");
            if (overlay.Children.Count >= 8) return; // copia decorativa limitada mesmo sob rajadas de alteracoes
            ImageSource drawing;
            try { drawing = MotionDrawing.Capture(element); }
            catch (InvalidOperationException) { return; } // arvore excepcionalmente grande: dispensa apenas a copia decorativa
            if (drawing == null) return;
            var map = element.TransformToAncestor(MotionPart("Scene"));
            Point at = map.Transform(new Point()), x = map.Transform(new Point(1, 0)), y = map.Transform(new Point(0, 1));
            var ghost = new Image { Source = drawing, Width = element.ActualWidth, Height = element.ActualHeight, IsHitTestVisible = false,
                RenderTransform = new MatrixTransform(new Matrix(x.X - at.X, x.Y - at.Y, y.X - at.X, y.Y - at.Y, at.X, at.Y)) };
            overlay.Children.Add(ghost);
            MotionTo(ghost, UIElement.OpacityProperty, 0, duration, 1, 0, delegate { overlay.Children.Remove(ghost); });
        }
        void MotionEdge(FrameworkElement edge, bool shown)
        {
            if ((edge.Visibility == Visibility.Visible) == shown) return;
            edge.IsHitTestVisible = shown;
            if (shown) { edge.Visibility = Visibility.Visible; MotionReveal(edge, MotionFast); }
            else { MotionExit(edge, MotionFast); edge.Visibility = Visibility.Collapsed; }
        }
        void MotionCloseFlyout()
        {
            if (flyout != null) { flyout.IsHitTestVisible = false; MotionExit(flyout); }
        }
        Button motionSetting;
        void AddMotionSetting()
        {
            Caption("ACESSIBILIDADE");
            motionSetting = ToggleSetting(body, "Reduzir movimento", SystemMotionOff || motionReduced, "reduce-motion", ToggleReducedMotion);
            UpdateMotionSetting();
        }
        void UpdateMotionSetting()
        {
            if (motionSetting == null) return;
            UpdateToggleSetting(motionSetting, "Reduzir movimento", SystemMotionOff || motionReduced, SystemMotionOff ? "Ativado pelo Windows" : null);
            motionSetting.ToolTip = "Preferência salva neste PC. Respeita as animações desativadas no Windows.";
        }
    }
}
