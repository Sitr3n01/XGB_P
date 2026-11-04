// Dicas suplementares: mouse/foco por 3 s, tipografia legivel e superficie escura na propria cena responsiva.
// Preserva ToolTip/nomes acessiveis dos controles; suprime apenas a apresentacao nativa do WPF.
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
        const int DescriptionDelayMs = 3000;
        readonly DispatcherTimer descriptionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        Func<DateTime> descriptionNow = delegate { return DateTime.UtcNow; };
        FrameworkElement descriptionOwner;
        string descriptionKey, descriptionContent;
        DateTime descriptionSince;
        bool descriptionMouse;
        Point descriptionPointer = new Point(double.NaN, double.NaN);

        Border DescriptionTip { get { return (Border)window.FindName("DescriptionTip"); } }
        TextBlock DescriptionText { get { return (TextBlock)window.FindName("DescriptionText"); } }
        void StartDescriptions()
        {
            EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(delegate(object sender, ToolTipEventArgs e) {
                if (Window.GetWindow(sender as DependencyObject) == window) e.Handled = true;
            }), true);
            window.GotKeyboardFocus += delegate(object sender, KeyboardFocusChangedEventArgs e) {
                // Foco temporario na janela durante reconstrucao nao reinicia a mesma dica.
                if (e.NewFocus != window) SelectDescription(DescriptionSource(e.NewFocus as DependencyObject), false, false);
            };
            window.PreviewMouseMove += delegate(object sender, MouseEventArgs e) {
                Point point = e.GetPosition(window);
                if (!double.IsNaN(descriptionPointer.X) && (point - descriptionPointer).Length < .5) return;
                descriptionPointer = point; SelectDescription(DescriptionSource(e.OriginalSource as DependencyObject), true, false);
            };
            window.PreviewMouseDown += delegate(object sender, MouseButtonEventArgs e) { SelectDescription(DescriptionSource(e.OriginalSource as DependencyObject), true, true); };
            window.PreviewMouseWheel += delegate { StopDescription(); };
            window.MouseLeave += delegate { StopDescription(); };
            window.Deactivated += delegate { StopDescription(); };
            window.IsVisibleChanged += delegate { if (!window.IsVisible) StopDescription(); };
            window.SizeChanged += delegate { StopDescription(); };
            descriptionTimer.Tick += delegate { DescriptionTick(); };
        }
        FrameworkElement DescriptionSource(DependencyObject source)
        {
            while (source != null && source != window)
            {
                var element = source as FrameworkElement;
                if (element != null && !string.IsNullOrWhiteSpace(ToolTipService.GetToolTip(element) as string)) return element;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return null;
        }
        string DescriptionIdentity(FrameworkElement owner)
        {
            if (owner == null) return null;
            string id = owner.Tag as string;
            if (string.IsNullOrEmpty(id)) id = owner.Name;
            if (string.IsNullOrEmpty(id)) id = owner.GetHashCode().ToString();
            return panel + "|" + settingsPage + "|" + id;
        }
        void SelectDescription(FrameworkElement owner, bool mouse, bool restart)
        {
            if (panel == 0) { StopDescription(); return; } // Home inteira, inclusive trilho/status, sem dicas redundantes.
            string text = owner == null ? null : ToolTipService.GetToolTip(owner) as string;
            if (string.IsNullOrWhiteSpace(text)) { StopDescription(); return; }
            string key = DescriptionIdentity(owner);
            if (!restart && descriptionKey == key && descriptionContent == text)
            { descriptionOwner = owner; descriptionMouse = mouse; return; }
            StopDescription();
            descriptionOwner = owner; descriptionMouse = mouse; descriptionKey = key; descriptionContent = text;
            descriptionSince = descriptionNow(); descriptionTimer.Start();
        }
        void RestartDescriptionForInput()
        {
            SelectDescription(DescriptionSource(Keyboard.FocusedElement as DependencyObject), false, true);
        }
        void StopDescription()
        {
            descriptionTimer.Stop(); descriptionOwner = null; descriptionKey = descriptionContent = null;
            DescriptionTip.Visibility = Visibility.Collapsed; DescriptionText.Text = "";
        }
        static Rect DescriptionBounds(Rect owner, Size size, Size scene, double edge)
        {
            double x = owner.X, y = owner.Bottom + 8;
            if (y + size.Height > scene.Height - edge) y = owner.Top - size.Height - 8;
            x = Math.Max(edge, Math.Min(x, scene.Width - edge - size.Width));
            y = Math.Max(edge, Math.Min(y, scene.Height - edge - size.Height));
            return new Rect(new Point(x, y), size);
        }
        void DescriptionTick()
        {
            if (panel == 0 || descriptionOwner == null || !window.IsVisible || motionClosing || disposed || !window.IsActive && !verify) { StopDescription(); return; }
            if (PresentationSource.FromVisual(descriptionOwner) == null || !descriptionOwner.IsVisible)
            {
                // Backend trocou a instancia visual: mesmo tag/texto conserva os 3 s, sem guardar o controle antigo.
                var replacement = descriptionMouse ? null : DescriptionSource(Keyboard.FocusedElement as DependencyObject);
                if (DescriptionIdentity(replacement) != descriptionKey) { StopDescription(); return; }
                SelectDescription(replacement, false, false);
            }
            string text = ToolTipService.GetToolTip(descriptionOwner) as string;
            if (text != descriptionContent) { SelectDescription(descriptionOwner, descriptionMouse, true); return; }
            if ((descriptionNow() - descriptionSince).TotalMilliseconds < DescriptionDelayMs) return;
            var scene = MotionPart("Scene");
            Point point;
            try { point = descriptionOwner.TranslatePoint(new Point(), scene); }
            catch (InvalidOperationException) { StopDescription(); return; }
            var owner = new Rect(point, descriptionOwner.RenderSize);
            if (!new Rect(new Size(scene.ActualWidth, scene.ActualHeight)).IntersectsWith(owner)) { StopDescription(); return; }
            // IsVisible nao considera recorte: uma linha rolada para fora da viewport nao deve manter sua dica.
            for (DependencyObject parent = VisualTreeHelper.GetParent(descriptionOwner); parent != null && parent != scene; parent = VisualTreeHelper.GetParent(parent))
            {
                var viewport = parent as ScrollContentPresenter;
                if (viewport == null) continue;
                var box = descriptionOwner.TransformToAncestor(viewport).TransformBounds(new Rect(descriptionOwner.RenderSize));
                if (!new Rect(viewport.RenderSize).IntersectsWith(box)) { StopDescription(); return; }
            }
            var tip = DescriptionTip; DescriptionText.Text = text;
            tip.MaxWidth = Math.Max(40, Math.Min(360, scene.ActualWidth - 2 * sceneEdge));
            tip.MaxHeight = Math.Max(40, scene.ActualHeight - 2 * sceneEdge);
            tip.Visibility = Visibility.Visible;
            tip.Measure(new Size(tip.MaxWidth, tip.MaxHeight));
            var bounds = DescriptionBounds(owner, tip.DesiredSize, new Size(scene.ActualWidth, scene.ActualHeight), sceneEdge);
            Canvas.SetLeft(tip, bounds.X); Canvas.SetTop(tip, bounds.Y);
        }

#if XGB_VERIFY
        void VerifyDescriptions()
        {
            var clock = descriptionNow; bool mouse = descriptionMouse;
            DateTime now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
            try
            {
                descriptionNow = delegate { return now; }; returnToHome = false; SetPanel(0); Show(); window.UpdateLayout();
                foreach (var item in actions)
                {
                    SelectDescription(item, false, true); now = now.AddSeconds(4); DescriptionTick();
                    SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed && descriptionOwner == null, "Home sem dica por foco: " + item.Tag);
                    SelectDescription(item, true, true); now = now.AddSeconds(4); DescriptionTick();
                    SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed && descriptionOwner == null, "Home sem dica por mouse: " + item.Tag);
                }
                foreach (var item in rail)
                {
                    SelectDescription(item, true, true); now = now.AddSeconds(4); DescriptionTick();
                    SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed && descriptionOwner == null, "trilho sem dica na Home: " + item.Tag);
                }
                SaveRender("home-sem-descricoes");
                SetPanel(StoresPanel); window.UpdateLayout(); FocusHome("store-xbox"); var first = Keyboard.FocusedElement as FrameworkElement;
                SelectDescription(first, false, true);
                DescriptionTick(); SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed, "dica nao aparece imediatamente");
                now = now.AddMilliseconds(2999); DescriptionTick(); SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed, "dica espera 3 segundos completos");
                now = now.AddMilliseconds(1); DescriptionTick(); window.UpdateLayout();
                SoundAssert(DescriptionTip.Visibility == Visibility.Visible && DescriptionText.Text == first.ToolTip as string, "dica de foco aparece aos 3 segundos");
                SoundAssert(DescriptionText.FontSize == 16 && DescriptionTip.CornerRadius.TopLeft == 4 && !DescriptionTip.IsHitTestVisible, "fonte, cantos e dica sem interceptar entrada");
                SaveRender("descricao-foco-3s");
                var second = actions.Find(c => (string)c.Tag == "store-steam"); SelectDescription(second, true, false);
                SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed, "trocar alvo fecha dica anterior");
                now = now.AddMilliseconds(2999); DescriptionTick(); SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed, "mouse tem o mesmo atraso");
                now = now.AddMilliseconds(1); DescriptionTick(); SoundAssert(DescriptionText.Text == (string)second.ToolTip && DescriptionTip.Visibility == Visibility.Visible,
                    "mouse aparece depois de 3 segundos | texto=" + DescriptionText.Text + " | visual=" + DescriptionTip.Visibility + " | alvo=" + descriptionKey + " | carregado=" + second.IsLoaded);
                now = now.AddSeconds(1); SelectDescription(second, true, false); DescriptionTick();
                SoundAssert(DescriptionTip.Visibility == Visibility.Visible, "mover no mesmo alvo nao reinicia");
                FocusHome("store-steam"); RestartDescriptionForInput();
                SoundAssert(DescriptionTip.Visibility == Visibility.Collapsed, "acao reinicia espera");
                now = now.AddSeconds(2); RenderGames(true); RebuildOpenApps(); DescriptionTick();
                now = now.AddSeconds(1); DescriptionTick(); SoundAssert(DescriptionTip.Visibility == Visibility.Visible, "reconstrucoes alheias nao repetem espera");
                FocusHome("store-xbox"); SelectDescription(Keyboard.FocusedElement as FrameworkElement, false, true);
                now = now.AddSeconds(2); RebuildStores(); window.UpdateLayout(); DescriptionTick();
                now = now.AddSeconds(1); DescriptionTick();
                SoundAssert(DescriptionTip.Visibility == Visibility.Visible && descriptionOwner == Keyboard.FocusedElement as FrameworkElement, "reconstruir o proprio alvo conserva prazo e atualiza instancia");
                window.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
                SoundAssert(descriptionOwner == null && DescriptionTip.Visibility == Visibility.Collapsed, "rolagem do mouse cancela dica");
                StopDescription(); SoundAssert(descriptionOwner == null && DescriptionTip.Visibility == Visibility.Collapsed, "cancelamento limpa alvo e visual");
                second = actions.Find(c => (string)c.Tag == "store-steam");
                var text = second.ToolTip; second.ToolTip = "Uma descrição suplementar mais longa, com fonte legível e quebra de linhas para caber na tela sem ultrapassar as margens.";
                try { SelectDescription(second, false, true); now = now.AddSeconds(3); DescriptionTick(); window.UpdateLayout(); SaveRender("descricao-longa"); }
                finally { second.ToolTip = text; }
                foreach (var edge in new[] { new Rect(900, 850, 80, 25), new Rect(0, 0, 40, 40) })
                {
                    var bounds = DescriptionBounds(edge, new Size(360, 80), new Size(1024, 880), 24);
                    SoundAssert(bounds.Left >= 24 && bounds.Top >= 24 && bounds.Right <= 1000 && bounds.Bottom <= 856, "dica permanece nas margens da cena");
                }
                SetPanel(0); DescriptionTick(); SoundAssert(descriptionOwner == null && DescriptionTip.Visibility == Visibility.Collapsed, "voltar a Home elimina dica do painel anterior");
                Hide(); DescriptionTick(); SoundAssert(descriptionOwner == null && DescriptionTip.Visibility == Visibility.Collapsed, "ocultar barra elimina dica");
                File.WriteAllText(Path.Combine(Output, "descricoes-verificacao.txt"), "OK: Home inteira e trilho sem dicas, por foco/mouse e apos 4 s; voltar a Home fecha dica anterior. Nos outros paineis, foco e mouse esperam 3000 ms (2999 ms oculto), troca de alvo/acao reinicia, mesmo alvo e reconstrucoes alheias preservam espera, " +
                    "reconstruir o proprio alvo conserva prazo e troca instancia, cancelar/ocultar limpa, fonte 16 e raio 4, texto com quebra, limites da cena e overlay sem interceptar entrada. Relogio substituido; renders da arvore WPF, sem mouse/controle fisico.\r\n", Encoding.UTF8);
            }
            finally { StopDescription(); descriptionNow = clock; descriptionMouse = mouse; SetPanel(0); }
        }
#endif
    }
}
