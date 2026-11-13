// Alternador de janelas da Home. Leitura em worker; ativacao somente por escolha do usuario.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Desktop;
namespace XGBP.Design
{
    sealed partial class Preview
    {
        readonly DispatcherTimer openAppsTimer = new DispatcherTimer(), appSwitchTimer = new DispatcherTimer();
        readonly List<Control> openAppsRow = new List<Control>();
        List<DesktopWindow> openApps;
        StackPanel openAppsButtons;
        ScrollViewer openAppsScroll;
        const double OpenAppSlot = 60, OpenAppEdge = 56;
        Grid openAppsPreviousEdge, openAppsNextEdge;
        TextBlock openAppsCount;
        bool openAppsBusy;
        DesktopWindow switchingApp;
        DateTime switchDeadline;
        DateTime appSwitchRetryAt;
        int appSwitchAttempts, appSwitchEpoch, appSwitchPanel;
        bool appSwitchTransferred;
        Action appSwitchConfirmed;
        Func<List<DesktopWindow>> readOpenApps = delegate { using (var process = Process.GetCurrentProcess()) return DesktopWindows.Read((uint)process.Id); };
        Func<DesktopWindow, bool> appAvailable = DesktopWindows.Available, appForeground = DesktopWindows.IsForeground;
        Func<DesktopWindow, bool> appOwnsForeground = DesktopWindows.OwnsForeground;
        Func<DesktopWindow, bool?> appExists = DesktopWindows.Exists;
        Action<DesktopWindow> switchApp = RequestAppSwitch;
        Action<DesktopWindow> appClose = DesktopWindows.Close;
        static void RequestAppSwitch(DesktopWindow item)
        { WriteLog("janelas: pedido de foco | " + item.Key + " " + item.AppName + " | " + DesktopWindows.ActivateWithReport(item)); }
        // Segurar um app por 2 s mostra o X de fechar ao lado do icone; soltar antes disso abre o app como sempre.
        const int AppHoldMs = 2000, AppHoldFeedbackDelayMs = 250;
        readonly DispatcherTimer holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AppHoldMs) };
        Control holdTarget;
        Border holdBar;
        bool holdFired;
        string holdSource, appCloseTag;
        DesktopWindow closingApp;
        FeedbackNotice appCloseFeedback, appSwitchFeedback;
        long appCommandVersion, appCloseVersion;
        DateTime appCloseDeadline;
        void StartOpenApps()
        {
            holdTimer.Tick += delegate { HoldElapsed(); };
            window.PreviewMouseLeftButtonUp += delegate { if (holdSource == "mouse") CancelHold(); };
#if XGB_VERIFY
            if (verify) { openApps = ExampleOpenApps(); appClose = delegate { }; return; }
#endif
            openAppsTimer.Interval = TimeSpan.FromSeconds(2);
            openAppsTimer.Tick += delegate { RefreshOpenApps(false); }; openAppsTimer.Start();
            appSwitchTimer.Interval = TimeSpan.FromMilliseconds(150);
            appSwitchTimer.Tick += delegate { CheckAppSwitch(); };
        }
        void RefreshOpenApps(bool reorder)
        {
            if (verify || disposed || openAppsBusy || panel != 0 || !window.IsVisible) return;
            openAppsBusy = true;
            long readVersion = appCommandVersion;
            int readEpoch = activationEpoch;
            Task.Factory.StartNew(delegate {
                List<DesktopWindow> found = null; string error = null;
                try { found = readOpenApps(); } catch (Exception ex) { error = ex.Message; }
                Dispatch(delegate {
                    openAppsBusy = false;
                    if (readEpoch != activationEpoch) return; // Nao aplicar inventario de outro ciclo FSE.
                    if (found != null)
                    {
                        // Manter posicoes durante a navegacao; uma nova abertura pode ordenar por Z-order.
                        if (!reorder && openApps != null)
                        {
                            var ordered = new List<DesktopWindow>();
                            foreach (var old in openApps) { var current = found.Find(w => w.Key == old.Key); if (current != null) ordered.Add(current); }
                            foreach (var item in found) if (!ordered.Exists(w => w.Key == item.Key)) ordered.Add(item);
                            found = ordered;
                        }
                        openApps = found;
                        if (panel == 0 && window.IsVisible) RebuildOpenApps();
                        CheckAppClose(true, readVersion);
                    }
                    else if (panel == 0 && window.IsVisible) Feedback("Não foi possível atualizar os aplicativos abertos.", FeedbackKind.Warning);
                    if (error != null) WriteLog("janelas: leitura falhou | " + error);
                });
            });
        }
        void BuildOpenApps()
        {
            var heading = new DockPanel { Margin = new Thickness(0, 10, 0, 7) };
            openAppsCount = Text("", 12, "Muted"); openAppsCount.TextAlignment = TextAlignment.Right;
            openAppsCount.TextWrapping = TextWrapping.NoWrap; openAppsCount.TextTrimming = TextTrimming.CharacterEllipsis; openAppsCount.MaxWidth = 250;
            openAppsCount.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(openAppsCount, Dock.Right);
            heading.Children.Add(openAppsCount); heading.Children.Add(Text("APLICATIVOS ABERTOS", 14, "Muted")); body.Children.Add(heading);
            // A viewport ocupa a largura inteira; as setas nao criam colunas ou espacos vazios.
            var layout = new Grid { Height = 60, Margin = new Thickness(0, 0, 0, 4), ClipToBounds = true,
                SnapsToDevicePixels = true, UseLayoutRounding = true };
            openAppsButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            openAppsScroll = new ScrollViewer { Content = openAppsButtons, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, CanContentScroll = false };
            layout.Children.Add(openAppsScroll);
            openAppsPreviousEdge = OpenAppsEdge(false); openAppsNextEdge = OpenAppsEdge(true);
            layout.Children.Add(openAppsPreviousEdge); layout.Children.Add(openAppsNextEdge);
            openAppsScroll.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) {
                ScrollOpenApps(openAppsScroll.HorizontalOffset + (e.Delta < 0 ? OpenAppSlot : -OpenAppSlot), true); e.Handled = true;
                window.UpdateLayout(); KeepOpenAppsFocusVisible();
            };
            openAppsScroll.ScrollChanged += delegate { UpdateOpenAppsEdges(); };
            openAppsScroll.SizeChanged += delegate { UpdateOpenAppsEdges(); };
            body.Children.Add(layout); openAppsRow.Clear(); homeRows.Add(openAppsRow); appCloseTag = null;
            RebuildOpenApps();
        }
        Grid OpenAppsEdge(bool right)
        {
            var edge = new Grid { Width = OpenAppEdge, HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed };
            var color = Brush("SurfacePanel").Color; color.A = 255;
            var clear = Color.FromArgb(0, color.R, color.G, color.B);
            var gradient = new LinearGradientBrush { StartPoint = new Point(right ? 0 : 1, 0), EndPoint = new Point(right ? 1 : 0, 0) };
            gradient.GradientStops.Add(new GradientStop(clear, 0));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(190, color.R, color.G, color.B), .55));
            gradient.GradientStops.Add(new GradientStop(color, 1));
            edge.Children.Add(new Border { Background = gradient, IsHitTestVisible = false });
            var chevron = new System.Windows.Shapes.Path {
                Data = Geometry.Parse(right ? "M 3,2 L 9,8 L 3,14" : "M 9,2 L 3,8 L 9,14"),
                Stroke = Brush("TextPrimary"), StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 10, Height = 16, Stretch = Stretch.Uniform };
            var arrow = new Button { Content = chevron, Width = 44, Height = 48, Padding = new Thickness(0), Margin = new Thickness(0),
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)window.FindResource("AppStripArrow"), Focusable = false, IsTabStop = false,
                ToolTip = right ? "Mais aplicativos à direita" : "Mais aplicativos à esquerda" };
            System.Windows.Automation.AutomationProperties.SetName(arrow, (string)arrow.ToolTip);
            WireMotion(arrow);
            arrow.Click += delegate { InvokeSoundAction(arrow, delegate {
                double page = Math.Max(OpenAppSlot, Math.Floor((openAppsScroll.ViewportWidth - 2 * OpenAppEdge) / OpenAppSlot) * OpenAppSlot);
                ScrollOpenApps(openAppsScroll.HorizontalOffset + (right ? page : -page), true);
                window.UpdateLayout(); KeepOpenAppsFocusVisible();
            }); };
            edge.Children.Add(arrow); return edge;
        }
        void ScrollOpenApps(double offset, bool smooth = false)
        {
            SmoothScroll(openAppsScroll, offset, true, smooth);
        }
        void UpdateOpenAppsEdges()
        {
            if (openAppsScroll == null || openAppsPreviousEdge == null || openAppsNextEdge == null) return;
            bool left = openAppsScroll.HorizontalOffset > .5;
            bool right = openAppsScroll.HorizontalOffset + .5 < openAppsScroll.ScrollableWidth;
            MotionEdge(openAppsPreviousEdge, left); MotionEdge(openAppsNextEdge, right);
            // Mascara somente as bordas com mais conteudo; o centro e o alvo em foco ficam nitidos.
            double fade = Math.Min(.25, OpenAppEdge / Math.Max(1, openAppsScroll.ViewportWidth));
            var existing = openAppsScroll.OpacityMask as LinearGradientBrush;
            if (left || right)
            {
                if (existing != null && existing.GradientStops.Count == 4 &&
                    existing.GradientStops[0].Color == (left ? Colors.Transparent : Colors.White) &&
                    existing.GradientStops[3].Color == (right ? Colors.Transparent : Colors.White) &&
                    Math.Abs(existing.GradientStops[1].Offset - fade) < .000001) return;
            }
            else { openAppsScroll.OpacityMask = null; return; }
            var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            mask.GradientStops.Add(new GradientStop(left ? Colors.Transparent : Colors.White, 0));
            mask.GradientStops.Add(new GradientStop(Colors.White, fade));
            mask.GradientStops.Add(new GradientStop(Colors.White, 1 - fade));
            mask.GradientStops.Add(new GradientStop(right ? Colors.Transparent : Colors.White, 1));
            mask.Freeze(); openAppsScroll.OpacityMask = left || right ? mask : null;
        }
        void EnsureOpenAppVisible(Control control)
        {
            if (openAppsScroll.ViewportWidth <= 0 || control.ActualWidth <= 0) return;
            double current = openAppsScroll.HorizontalOffset, max = openAppsScroll.ScrollableWidth;
            double x = control.TranslatePoint(new Point(0, 0), openAppsScroll).X + current;
            double target = current;
            // Reavaliar as margens quando a rolagem alcanca uma extremidade e a seta desaparece.
            for (int i = 0; i < 3; i++)
            {
                double left = target > .5 ? OpenAppEdge : 2;
                double right = target + .5 < max ? OpenAppEdge : 2;
                if (x - target < left) target = x - left;
                else if (x + control.ActualWidth - target > openAppsScroll.ViewportWidth - right)
                    target = x + control.ActualWidth - openAppsScroll.ViewportWidth + right;
                target = Math.Max(0, Math.Min(max, target));
            }
            ScrollOpenApps(target);
        }
        void KeepOpenAppsFocusVisible()
        {
            int focused = openAppsRow.IndexOf(Keyboard.FocusedElement as Control);
            if (focused < 0) return;
            double left = openAppsPreviousEdge.Visibility == Visibility.Visible ? OpenAppEdge : 0;
            double right = openAppsScroll.ViewportWidth - (openAppsNextEdge.Visibility == Visibility.Visible ? OpenAppEdge : 0);
            int closest = -1;
            for (int i = 0; i < openAppsRow.Count; i++)
            {
                double x = openAppsRow[i].TranslatePoint(new Point(0, 0), openAppsScroll).X;
                if (x >= left - .5 && x + openAppsRow[i].ActualWidth <= right + .5 &&
                    (closest < 0 || Math.Abs(i - focused) < Math.Abs(closest - focused))) closest = i;
            }
            if (closest >= 0 && closest != focused) openAppsRow[closest].Focus();
        }
        void RebuildOpenApps()
        {
            if (panel != 0 || openAppsButtons == null) return;
            if (appCloseTag != null && (openApps == null || !openApps.Exists(w => "window-" + w.Key == appCloseTag))) appCloseTag = null;
            if (openAppsRow.Count > 0 && DeferOpeningVisual(1)) return;
            var focused = Keyboard.FocusedElement as Control;
            bool hadFocus = openAppsRow.Contains(focused);
            string focusedTag = focused == null ? null : focused.Tag as string;
            int oldPosition = openAppsRow.IndexOf(focused);
            double offset = openAppsScroll.HorizontalOffset;
            int anchorIndex = Math.Min(openAppsRow.Count - 1, (int)(offset / OpenAppSlot));
            string anchor = anchorIndex < 0 ? null : (string)openAppsRow[anchorIndex].Tag;
            double anchorRemainder = offset - Math.Max(0, anchorIndex) * OpenAppSlot;
            // O X de fechar fica logo depois do icone segurado e entra na sequencia de etiquetas da faixa.
            string oldKeys = string.Join("|", openAppsRow.ConvertAll(c => (string)c.Tag).ToArray());
            string newKeys = openApps == null ? "loading" : string.Join("|", openApps.ConvertAll(w => "window-" + w.Key + ("window-" + w.Key == appCloseTag ? "|close-window-" + w.Key : "")).ToArray());
            if (oldKeys != newKeys || openAppsRow.Count == 0)
            {
                var oldTags = new HashSet<string>();
                foreach (var c in openAppsRow) {
                    oldTags.Add((string)c.Tag);
                    if (openApps == null || !openApps.Exists(w => "window-" + w.Key == (string)c.Tag)) MotionExit(c);
                }
                CancelElementMotion(openAppsButtons);
                foreach (var control in openAppsRow) actions.Remove(control);
                openAppsButtons.Children.Clear(); openAppsRow.Clear();
                if (openApps == null || openApps.Count == 0)
                {
                    var empty = Text(openApps == null ? "Buscando janelas…" : "Nenhum outro aplicativo aberto", 15, "Muted");
                    empty.VerticalAlignment = VerticalAlignment.Center; openAppsButtons.Children.Add(empty);
                }
                else foreach (var entry in openApps)
                {
                    var item = entry;
                    // Icone com a barra de progresso do segurar, presa ao pe do conteudo.
                    var face = new Grid { Width = 36, Height = 36 }; face.Children.Add(OpenAppIcon(item));
                    face.Children.Add(new Border { Height = 3, Width = 0, CornerRadius = new CornerRadius(1.5), Background = Brush("FocusGreen"),
                        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false });
                    Button button = null;
                    button = HomeButton(face, delegate {
                        var current = openApps.Find(w => w.Key == item.Key);
                        if (current != null) OpenApp(current);
                    });
                    button.Tag = "window-" + item.Key;
                    button.PreviewMouseLeftButtonDown += delegate { BeginHold("mouse", button); };
                    button.MouseLeave += delegate { if (holdSource == "mouse" && holdTarget == button) CancelHold(); };
                    AddOpenAppButton(button);
                    button.HorizontalContentAlignment = HorizontalAlignment.Center; button.Padding = new Thickness(6);
                    if ((string)button.Tag == appCloseTag) AddAppClose(item);
                }
                for (int i = 0; i < openAppsRow.Count; i++)
                {
                    openAppsRow[i].Margin = new Thickness(0, 0, i == openAppsRow.Count - 1 ? 0 : 8, 0);
                    if (!motionBuilding && !oldTags.Contains((string)openAppsRow[i].Tag)) MotionReveal((FrameworkElement)openAppsRow[i]);
                }
                // Icones centralizados na secao enquanto cabem; com mais apps, a faixa rola desde a esquerda. O aviso vazio fica a esquerda.
                openAppsButtons.HorizontalAlignment = openAppsRow.Count > 0 ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                // Os indices podem mudar quando uma janela fecha; os retornos usam tags estaveis.
                actions.Clear(); foreach (var row in homeRows) actions.AddRange(row);
                window.UpdateLayout();
                int newAnchor = openAppsRow.FindIndex(c => (string)c.Tag == anchor);
                ScrollOpenApps(newAnchor < 0 ? offset : newAnchor * OpenAppSlot + anchorRemainder);
                window.UpdateLayout();
                if (hadFocus && openAppsRow.Count > 0)
                {
                    var target = openAppsRow.Find(c => (string)c.Tag == focusedTag) ?? openAppsRow[Math.Min(oldPosition, openAppsRow.Count - 1)]; target.Focus();
                }
                else if (hadFocus) FocusHome("utility-library");
                window.UpdateLayout();
                if (hadFocus && Keyboard.FocusedElement is Control) EnsureOpenAppVisible((Control)Keyboard.FocusedElement);
            }
            UpdateOpenAppsEdges();
            foreach (var control in openAppsRow)
            {
                string tag = (string)control.Tag; bool closer = tag.StartsWith("close-");
                var entry = openApps.Find(w => "window-" + w.Key == (closer ? tag.Substring(6) : tag));
                string name = closer ? "Fechar " + (string.IsNullOrWhiteSpace(entry.AppName) ? entry.Title : entry.AppName) : entry.Title + (entry.Minimized ? " · minimizado" : "");
                control.ToolTip = name;
                System.Windows.Automation.AutomationProperties.SetName(control, name);
            }
            UpdateOpenAppsHint(Keyboard.FocusedElement as Control);
        }
        // Botoes da faixa ocupam o mesmo passo de 60 (52 + 8): a rolagem por indice continua exata com o X.
        void AddOpenAppButton(Button button)
        {
            button.Width = 52; button.Height = 52;
            button.RequestBringIntoView += delegate(object sender, RequestBringIntoViewEventArgs e) {
                e.Handled = true; EnsureOpenAppVisible(button); openAppsScroll.BringIntoView();
            };
            openAppsButtons.Children.Add(button); openAppsRow.Add(button);
        }
        void AddAppClose(DesktopWindow item)
        {
            string key = item.Key;
            var close = HomeButton(Symbol("\uE711", 18), delegate {
                var current = openApps == null ? null : openApps.Find(w => w.Key == key);
                if (current != null) CloseOpenApp(current);
            });
            close.Tag = "close-window-" + key; close.Style = (Style)window.FindResource("CloseAction");
            close.HorizontalContentAlignment = HorizontalAlignment.Center; close.Padding = new Thickness(0);
            AddOpenAppButton(close);
        }
        // Inicio do segurar: so nos icones de apps. Controle, teclado e mouse; a barra de progresso respeita o movimento reduzido.
        bool BeginHold(string source, Control target = null)
        {
            target = target ?? Keyboard.FocusedElement as Control;
            if (panel != 0 || target == null || !openAppsRow.Contains(target) || !((string)target.Tag).StartsWith("window-")) return false;
            CancelHold(); holdTarget = target; holdSource = source; holdTimer.Start();
            var face = ((Button)target).Content as Grid;
            holdBar = face == null ? null : face.Children[face.Children.Count - 1] as Border;
            // Toque curto nao pisca progresso: aguardar a intencao de segurar sem mudar os 2 s do gesto.
            if (holdBar != null && MotionEnabled) holdBar.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(0, face.Width, TimeSpan.FromMilliseconds(AppHoldMs - AppHoldFeedbackDelayMs))
                { BeginTime = TimeSpan.FromMilliseconds(AppHoldFeedbackDelayMs) });
            return true;
        }
        // Soltar antes de 2 s e o toque normal (abre o app); depois do X, soltar nao faz nada.
        void ReleaseHold()
        {
            var target = holdTarget; bool fired = holdFired; CancelHold();
            if (target != null && !fired && target.IsKeyboardFocused) Activate();
        }
        void CancelHold()
        {
            holdTimer.Stop();
            if (holdBar != null) { holdBar.BeginAnimation(FrameworkElement.WidthProperty, null); holdBar.Width = 0; holdBar = null; }
            holdTarget = null; holdFired = false; holdSource = null;
        }
        void HoldElapsed()
        {
            holdTimer.Stop(); var target = holdTarget; if (target == null) return;
            if (holdBar != null) { holdBar.BeginAnimation(FrameworkElement.WidthProperty, null); holdBar.Width = 0; holdBar = null; }
            // O mouse nao entrega o soltar ao botao reconstruido; teclado e controle esperam soltar sem abrir o app.
            if (holdSource == "mouse") CancelHold(); else holdFired = true;
            ShowAppClose((string)target.Tag);
        }
        void ShowAppClose(string tag)
        {
            if (openApps == null || !openApps.Exists(w => "window-" + w.Key == tag)) return;
            appCloseTag = tag; RebuildOpenApps(); FocusHome("close-" + tag); Sound("lista");
            WriteLog("janelas: X de fechar exibido | " + tag);
        }
        void HideAppClose()
        {
            if (appCloseTag == null) return;
            appCloseTag = null; RebuildOpenApps();
        }
        // Focar outro controle fora do par icone/X recolhe o X; Esc/circulo tambem, devolvendo o foco ao icone.
        void AppCloseFocus(Control focused)
        {
            string tag = focused == null ? null : focused.Tag as string;
            if (appCloseTag != null && tag != appCloseTag && tag != "close-" + appCloseTag) HideAppClose();
        }
        bool AppCloseBack()
        {
            if (appCloseTag == null) return false;
            string tag = appCloseTag; HideAppClose(); FocusHome(tag); return true;
        }
        void CloseOpenApp(DesktopWindow item)
        {
            string tag = "window-" + item.Key; appCloseTag = null;
            try
            {
                if (closingApp != null) { FinishFeedback(appCloseFeedback, "Aguarde a confirmação do fechamento de " + closingApp.AppName + ".", FeedbackKind.Pending); return; }
                appCloseFeedback = BeginFeedback("Fechando " + item.AppName + "…", "0"); appCloseVersion = ++appCommandVersion;
                appClose(item); closingApp = item; appCloseDeadline = feedbackNow().AddSeconds(6);
                openAppsTimer.Interval = TimeSpan.FromMilliseconds(500); WriteLog("janelas: fechar solicitado | " + item.Key);
            }
            catch (Exception ex) { FinishFeedback(appCloseFeedback, ex.Message, FeedbackKind.Error); WriteLog("janelas: fechar falhou | " + item.Key + " | " + ex.Message); }
            RebuildOpenApps(); FocusHome(tag);
            if (verify) return;
            var soon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            soon.Tick += delegate { soon.Stop(); if (!disposed) RefreshOpenApps(false); }; soon.Start();
        }
        // A leitura posterior ao pedido e a identidade nativa confirmam a saida; sumir da faixa nao basta.
        void CheckAppClose(bool fresh = true, long readVersion = long.MaxValue)
        {
            if (closingApp == null) return;
            if (!fresh || readVersion < appCloseVersion || openApps == null)
            {
                if (feedbackNow() >= appCloseDeadline.AddSeconds(2))
                { FinishFeedback(appCloseFeedback, "Não foi possível confirmar o fechamento da janela de " + closingApp.AppName + ". Confira o aplicativo.", FeedbackKind.Warning); closingApp = null; openAppsTimer.Interval = TimeSpan.FromSeconds(2); }
                return;
            }
            bool? exists;
            try { exists = appExists(closingApp); } catch (Exception) { exists = null; }
            if (exists == false) { FinishFeedback(appCloseFeedback, "Janela de " + closingApp.AppName + " fechada.", FeedbackKind.Success); WriteLog("janelas: fechado | " + closingApp.Key); closingApp = null; openAppsTimer.Interval = TimeSpan.FromSeconds(2); return; }
            if (feedbackNow() < appCloseDeadline) return;
            FinishFeedback(appCloseFeedback, exists == true ? closingApp.AppName + " ainda está aberto. Ele pode estar pedindo confirmação." :
                "Não foi possível confirmar o fechamento da janela de " + closingApp.AppName + ". Confira o aplicativo.", exists == true ? FeedbackKind.Attention : FeedbackKind.Warning);
            closingApp = null; openAppsTimer.Interval = TimeSpan.FromSeconds(2);
        }
        void UpdateOpenAppsHint(Control focused)
        {
            // Com uma janela em foco, o titulo dela substitui a contagem ao lado do nome da secao.
            if (openAppsCount == null) return;
            bool named = focused != null && openAppsRow.Contains(focused);
            MotionText(openAppsCount, named ? focused.ToolTip as string ?? "" : openApps == null ? "…" : openApps.Count.ToString());
            openAppsCount.Foreground = Brush(named ? "TextPrimary" : "Muted"); openAppsCount.FontSize = named ? 13 : 12;
        }
        // Icone PNG (janelas e lojas); null quando nao ha icone ou ele nao decodifica.
        Image PngImage(byte[] png, double size)
        {
            if (png != null) try
            {
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(png)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
                return new Image { Source = bitmap, Width = size, Height = size };
            }
            catch { }
            return null;
        }
        FrameworkElement OpenAppIcon(DesktopWindow item)
        {
            var image = PngImage(item.IconPng, 28);
            if (image != null) return image;
            return new System.Windows.Shapes.Path { Data = Geometry.Parse("M 1,1 L 9,1 9,9 1,9 Z M 13,1 L 21,1 21,9 13,9 Z M 1,13 L 9,13 9,21 1,21 Z M 13,13 L 21,13 21,21 13,21 Z"), Fill = Brush("Muted"), Width = 24, Height = 24, Stretch = Stretch.Uniform };
        }
        void OpenApp(DesktopWindow item, Action confirmed = null)
        {
            var game = GameForWindow(item);
            if (game != null) { CancelHold(); ResumeOpenGame(game.Key, game); return; }
            CancelHold(); // Nenhum progresso, inclusive clock ainda no atraso, acompanha a transferencia.
            CancelAppSwitch("Troca de janela substituída.");
            try
            {
                if (!appAvailable(item))
                {
                    string reason; try { reason = DesktopWindows.Unavailability(item); } catch (Exception ex) { reason = ex.Message; }
                    WriteLog("janelas: indisponivel | " + item.Key + " " + item.AppName + " | " + reason);
                    Feedback(appExists(item) == false ? "Essa janela já foi fechada. Atualizando aplicativos…" :
                        "Essa janela não está disponível para alternância agora. Atualizando aplicativos…"); RefreshOpenApps(false); return;
                }
                // O pedido assincrono pode ser recusado ou levar tempo para o shell mostrar o destino.
                // Manter a barra ate observar a entrega, preservando o contexto que permite pedir o foco.
                appSwitchFeedback = BeginFeedback("Alternando para " + item.AppName + "…");
                switchingApp = item; appSwitchTransferred = false; appSwitchAttempts = 1;
                appSwitchConfirmed = confirmed;
                appSwitchEpoch = activationEpoch; appSwitchPanel = panel;
                switchDeadline = feedbackNow().AddMilliseconds(1800); appSwitchRetryAt = feedbackNow().AddMilliseconds(150);
                switchApp(item); TraceAppSwitch("pedido", item);
                appSwitchTimer.Start(); CheckAppSwitch();
            }
            catch (Exception ex)
            {
                switchingApp = null; appSwitchTimer.Stop();
                appSwitchConfirmed = null; RestoreSteamClosingVisual();
                FinishFeedback(appSwitchFeedback, "Não foi possível alternar para " + item.AppName + ".", FeedbackKind.Error); WriteLog("janelas: ativacao falhou | " + ex.Message);
            }
        }
        void CheckAppSwitch()
        {
            if (switchingApp == null) { appSwitchTimer.Stop(); return; }
            try
            {
                if (appSwitchEpoch != activationEpoch || panel != appSwitchPanel || !window.IsVisible && !appSwitchTransferred)
                { CancelAppSwitch("Troca de janela cancelada."); return; }
                var item = switchingApp;
                if (appSwitchConfirmed != null && guideDownDevices.Count > 0)
                { CancelAppSwitch("Retorno Steam cancelado: novo PS pressionado."); return; }
                if (appForeground(item))
                {
                    var confirmed = appSwitchConfirmed;
                    if (!appSwitchTransferred)
                    {
                        // Nao devolver o foco ao app de tras. Conferir de novo depois de ocultar a propria janela.
                        switchingApp = null; Hide(); switchingApp = item; appSwitchTransferred = true; appSwitchConfirmed = confirmed;
                        if (!appForeground(item)) return;
                    }
                    TraceAppSwitch("confirmado", item);
                    WriteLog("janelas: foreground confirmado | " + item.Key);
                    FinishFeedback(appSwitchFeedback, "Janela de " + item.AppName + " ativada.", FeedbackKind.Success);
                    switchingApp = null; appSwitchConfirmed = null; appSwitchTimer.Stop();
                    if (confirmed != null) confirmed();
                    return;
                }
                if (feedbackNow() >= switchDeadline)
                {
                    TraceAppSwitch("limite sem confirmacao", item);
                    switchingApp = null; appSwitchTimer.Stop();
                    appSwitchConfirmed = null; RestoreSteamClosingVisual();
                    FinishFeedback(appSwitchFeedback, "O Windows não confirmou a troca de janela. Tente novamente.", FeedbackKind.Attention);
                    return; // Nao reabrir/ativar a barra sobre um app que o usuario tenha escolhido.
                }
                IntPtr foreground = foregroundRead();
                if (foreground != IntPtr.Zero && foreground != handle && !appOwnsForeground(item))
                {
                    var current = observeWindow(foreground);
                    if (current != null && previousIsApp(current.Window))
                    { CancelAppSwitch("Outra janela foi escolhida. Troca cancelada."); Hide(); return; }
                }
                // Repeticao curta e limitada so enquanto a barra ainda tem o foco. Nao disputar foco com outro app.
                if (!appSwitchTransferred && window.IsVisible && (!motionClosing || steamClosingVisual) && foreground == handle &&
                    appSwitchAttempts < 3 && feedbackNow() >= appSwitchRetryAt)
                {
                    appSwitchAttempts++; appSwitchRetryAt = feedbackNow().AddMilliseconds(300);
                    switchApp(item); TraceAppSwitch("nova tentativa " + appSwitchAttempts, item);
                }
            }
            catch (Exception ex)
            {
                switchingApp = null; appSwitchTimer.Stop();
                appSwitchConfirmed = null; RestoreSteamClosingVisual();
                FinishFeedback(appSwitchFeedback, "Não foi possível alternar para o aplicativo.", FeedbackKind.Error);
                WriteLog("janelas: confirmacao falhou | " + ex.Message);
            }
        }
        void CancelAppSwitch(string message)
        {
            CancelGameNavigation(message);
            CancelSteamNavigation();
            appSwitchConfirmed = null;
            if (switchingApp == null) return;
            switchingApp = null; appSwitchTimer.Stop();
            FinishFeedback(appSwitchFeedback, message, FeedbackKind.Info);
            WriteLog("janelas: troca cancelada | " + message);
        }
        void TraceAppSwitch(string reason, DesktopWindow item)
        {
            try
            {
                var target = observeWindow(item.Handle);
                IntPtr foreground = foregroundRead(); var current = observeWindow(foreground);
                WriteLog("janelas: troca | " + reason + " | " + item.Key + " " + item.AppName + "; tentativas=" + appSwitchAttempts +
                    "; alvo=" + (target == null ? "ausente" : target.Describe()) + "; foreground=" + foreground +
                    "; frente=" + (current == null ? "ausente" : current.Describe()));
            }
            catch (Exception ex) { WriteLog("janelas: diagnostico da troca falhou | " + ex.Message); }
        }
    }
}
