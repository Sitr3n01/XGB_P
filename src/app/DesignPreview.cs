// XGB_P: interface residente WPF, acoes do usuario e verificacoes isoladas. HID somente leitura.
// C# 5 / WPF do .NET Framework, sem instalacao automatica de pacotes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Input;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{

    sealed partial class Preview : IDisposable
    {
        readonly Application app;
        readonly bool verify;
        readonly bool startHidden;
        readonly Window window;
        readonly StackPanel body;
        readonly List<Button> rail = new List<Button>();
        static readonly int[] RailOrder = { 0, 1, 6, AmdPanel, 3, AmdTuningPanel, 5, 4 };
        readonly List<Control> actions = new List<Control>();
        readonly DispatcherTimer clock = new DispatcherTimer();
        static readonly bool DiagnosticMode = Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("--verify", StringComparison.Ordinal) || a == "--inspect-motion");
        static readonly string Output = OutputFolder(AppDomain.CurrentDomain.BaseDirectory);
        static readonly XGBP.Runtime.BoundedLog Log = new XGBP.Runtime.BoundedLog(Path.Combine(Output, DiagnosticMode ? "preview.log" : "xgb.log"));
        internal static string ReportFolder { get { return Output; } }
        internal static bool FlushLog() { return Log.Flush(2000); }
        IntPtr handle;
        Forms.NotifyIcon tray;
        DualSenseMonitor controller;
        bool disposed;
        int panel = 0;
        string batteryText = WaitingText;
        readonly Dictionary<string, bool> held = new Dictionary<string, bool>();
        DateTime nextRepeat = DateTime.MaxValue;
        string repeatButton;
        readonly DispatcherTimer repeat = new DispatcherTimer();

        public Preview(Application app, bool verify, bool startHidden = false)
        {
            this.app = app; this.verify = verify; this.startHidden = startHidden;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesignPreview.xaml"))
            using (var reader = new StreamReader(stream))
                window = (Window)XamlReader.Parse(reader.ReadToEnd().Replace("assembly=XGB_P", "assembly=" + Assembly.GetExecutingAssembly().GetName().Name));
            body = (StackPanel)window.FindName("PanelBody");
            // O indice 2 era o Jogo atual, que saiu do trilho; fica um botao solto para manter os IDs dos paineis.
            foreach (string name in new[] { "Home", "Library", null, "Performance", "Power", "Settings", "Sound", null, "Amd", "AmdTuning" })
            {
                var button = name == null ? new Button() : (Button)window.FindName(name);
                int index = rail.Count;
                rail.Add(button);
                WireMotion(button);
                button.GotKeyboardFocus += delegate {
                    if (systemPowerBusy) return;
                    // Lojas fica sob o Inicio: focar o Inicio no trilho nao troca o painel.
                    if (index != 1 && panel != index && !(index == 0 && panel == StoresPanel)) { returnToHome = false; SetPanel(index); }
                    else ShowHomeHint(null);
                };
                button.Click += delegate { InvokeSoundAction(button, delegate { SelectRail(index); }); };
            }
            rail[AmdPanel].Content = RecordingIcon("video", 26);
            // Clique fora de uma lista flutuante a fecha, sem acionar o controle que estava por baixo.
            window.PreviewMouseDown += delegate(object sender, MouseButtonEventArgs e) { if (!ActivationAllowed()) e.Handled = true; };
            window.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) { if (!ActivationAllowed()) e.Handled = true; };
            window.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (flyout == null || flyout.IsMouseOver || flyoutSelector.IsMouseOver) return;
                e.Handled = true; Back();
            };
        }

        // Instalada em pasta protegida (variante uiAccess, ADR 0040), sem escrita ali: logs e relatorios vao para o perfil.
        static string OutputFolder(string baseDirectory)
        {
            string leaf = DiagnosticMode ? "design-preview" : "logs";
            foreach (var special in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string root = Environment.GetFolderPath(special).TrimEnd('\\') + "\\";
                if (root.Length > 1 && baseDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", leaf);
            }
            return Path.Combine(baseDirectory, leaf);
        }
        // Nunca lanca: um log bloqueado (antivirus, editor) nao pode derrubar a interface nem as threads do controle.
        public static void WriteLog(string text)
        {
            Log.Write(text);
        }
        // Erro inesperado na interface: registra e segue aberta, com os jogos pausados sob controle da barra.
        // Uma rajada (3 em 30 s) indica estado quebrado: encerra pelo caminho normal, que retoma os jogos.
        int uiErrors;
        DateTime uiErrorsSince;
        void OnUiError(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteLog("ERRO na interface: " + e.Exception);
            if (DateTime.UtcNow - uiErrorsSince > TimeSpan.FromSeconds(30)) { uiErrorsSince = DateTime.UtcNow; uiErrors = 0; }
            if (++uiErrors >= 3) { WriteLog("erros repetidos na interface; encerrando"); return; }
            e.Handled = true;
            try { Feedback("Algo falhou nesta ação. A barra continua aberta.", FeedbackKind.Error); } catch (Exception) { }
        }
        // Erro numa thread propria encerra o processo: antes disso, nenhum jogo pausado pela previa pode ficar congelado.
        void OnFatalError(object sender, UnhandledExceptionEventArgs e)
        {
            WriteLog("ERRO fatal fora da interface: " + e.ExceptionObject);
            try { gameOperations.Stop(ResumePausedGames); } catch (Exception) { }
        }

        public void Start()
        {
            Directory.CreateDirectory(Output);
            StartAppearancePreferences();
            StartAccentTheme();
            StartMotion();
            StartDescriptions();
            StartFeedback();
            StartWindowFocus();
            StartSteamInput();
            if (verify) ConfigureSteamVerification();
            StartScene();
            handle = new WindowInteropHelper(window).EnsureHandle();
            MarkOverlayWindow();
            StartLaunchSettings(); StartTaskbar(); StartAppVolume(); StartSteamStatus(); // antes da ponte Win+G e do modo (ADRs 0091-0093)
            StartActivation();
            if (!verify)
            {
                app.DispatcherUnhandledException += OnUiError;
                AppDomain.CurrentDomain.UnhandledException += OnFatalError;
                tray = new Forms.NotifyIcon();
                tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                tray.Text = "XGB_P · PS no Xbox / Win+G conforme preferência";
                var menu = new Forms.ContextMenuStrip();
                menu.Items.Add("Mostrar / ocultar (modo Xbox)", null, delegate { RequestActivation("bandeja"); });
                desktopWinGTray = (Forms.ToolStripMenuItem)menu.Items.Add("Permitir Win+G no desktop", null, delegate { ToggleDesktopWinG(); });
                desktopWinGTray.Checked = launchPreferences.DesktopWinG;
                menu.Items.Add("Teclado virtual", null, delegate { OpenKeyboardFromTray(); });
                menu.Items.Add("Encerrar XGB_P", null, delegate { app.Shutdown(); });
                tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { RequestActivation("bandeja"); }; tray.Visible = true;
                controller = new DualSenseMonitor();
                controller.DeduplicateGuide = false; // ver se o PS tambem passou pelo virtual do DSX (ADR 0042)
                controller.Log += delegate(string message) {
                    WriteLog(message);
                };
                controller.DeviceLost += delegate(string device) { Dispatch(delegate { GuideDeviceLost(device); ClearHeld(); ControllerLost(device); }); };
                controller.ButtonChanged += delegate(ButtonEvent e) { Dispatch(delegate { OnController(e); }); };
                controller.BatteryChanged += delegate(string device, Battery battery) { Dispatch(delegate { SetBattery(device, battery); }); };
                controller.Start();
                StartXInputGuide(); // Guia do Xbox 360 virtual do DSX em emulacao Xbox (ADR 0080)
            }
            window.PreviewKeyDown += OnKey;
            window.PreviewKeyUp += delegate(object sender, KeyEventArgs e) {
                if ((e.Key == Key.Enter || e.Key == Key.Space) && holdSource == "key") { ReleaseHold(); e.Handled = true; }
            };
            window.Deactivated += delegate { ClearHeld(); };
            window.Closing += delegate { app.Shutdown(); };
            ((System.Windows.FrameworkElement)window.FindName("DismissArea")).MouseLeftButtonDown += delegate { Dismiss(); };
            clock.Interval = TimeSpan.FromSeconds(1);
            clock.Tick += delegate { UpdateStatus(); }; clock.Start();
            repeat.Interval = TimeSpan.FromMilliseconds(40);
            repeat.Tick += delegate { TickController(); };
            repeat.Start();
            StartStatus(); StartAudio(); StartOpenApps(); StartMedia(); StartPerf(); StartGames(); StartConnections(); StartStores(); StartSounds(); StartKeyboard(); StartSystemPower(); StartAmd(); StartAmdTuning(); SetPanel(0); UpdateStatus();
            StartPresentationScheduling();
            WriteLog(verify ? "verificacao iniciada" : "iniciado; PS so no FSE; Win+G desktop=" + launchPreferences.DesktopWinG + "; na bandeja; " + (startHidden ? "inicializacao do Windows" : "abertura manual") + "; administrador=" + LaunchSettings.Elevated +
                "; uiAccess=" + LaunchSettings.UiAccess + "; logs=" + Output);
        }

        void Dispatch(Action action)
        {
            QueueUi(action, DispatcherPriority.Normal);
        }

        void Show()
        {
            openingRequested = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!ActivationAllowed()) return;
            RefreshAccentTheme();
            if (activationMode == false && !window.IsVisible && !desktopShowByWinG) return;
            CancelCapturePhoto();
            CancelAppSwitch("Troca de janela cancelada pela reabertura da barra.");
            ReopenDeferredWindow();
            bool fresh = !window.IsVisible;
            // A barra tem prioridade sobre o teclado virtual: ele some e volta quando ela devolver o foco (ADR 0052).
            if (fresh) SuspendKeyboard();
            // O app em primeiro plano antes de abrir (normalmente o jogo) tem prioridade no FPS.
            if (fresh) { CapturePreviousWindow(); ReduceAppVolumeOnOpen(); UpdateStatusPlacement(); } // ADRs 0092 e 0093
            if (fresh && !verify) PlaceWindow();
            if (fresh) { CheckSounds(); Sound("abrir"); foregroundSinceOpen = elevationHintShown = steamInputDoneThisOpening = false; foregroundRetryAt = DateTime.MinValue; steamGuideOpening = steamGuidePending; }
            ClearHeld(); window.Show(); window.Activate();
            TakeBarForeground(fresh ? "abertura" : "reabertura");
            MaybeHoldSteamInput();
            EnableExitGamingModeButtons();
            if (panel == 0 && actions.Count > 0) actions[0].Focus();
            else rail[RailIndex(panel)].Focus();
            window.UpdateLayout(); MotionOpen(fresh);
            RenderFeedback();
            RefreshOpenApps(true); mediaNextRead = DateTime.MinValue; perfNextRead = DateTime.MinValue; networkNextRead = DateTime.MinValue; amdNextRead = DateTime.MinValue;
            if (panel == AmdTuningPanel && !verify) ReadAmdTuning();
            GamesTick(); // a abertura atualiza sessoes; o catalogo mantem seu TTL e o backoff de erro
            connectionsNextRead = DateTime.MinValue; ConnectionsTick();
            UpdateStatus();
            RefreshPresentationTimers();
        }
        void Hide(bool restorePrevious = false)
        {
            mediaEditing = null; if (window.IsVisible) RestoreAppVolumeOnClose("barra fechada"); // o som volta antes de adiar pela Steam
            CancelAppSwitch("Troca de janela cancelada pelo fechamento da barra.");
            if (DeferHideForSteamInput(restorePrevious)) return;
            if (window.IsVisible) ReleasePreviousWindow(restorePrevious);
            StopDescription(); ClearHeld(); motionClosing = false; motionCloseVersion++; StopMotion(); window.Hide();
            RestoreDeferredWindow(); EndSteamGate();
            RefreshPresentationTimers();
        }
        void Toggle()
        {
            if (!ActivationAllowed()) return;
            if (motionClosing || !window.IsVisible) Show();
            else { HoldSteamInputForClose(); Dismiss(); }
        }
        void ClearHeld() { held.Clear(); repeatButton = scrollButton = null; controllerInputSince = controllerNow(); CancelHold(); }

        void SelectRail(int index)
        {
            if (index == 1) { OpenSteamLibrary(); return; }
            returnToHome = false;
            SetPanel(index);
            FocusFirstAction();
            ActionSound("mover"); // Clique no painel: navegacao curta, sem efeito de abertura/transicao.
        }
        // Entrada no painel: o Som comeca pela saida de som, mesmo com o player acima dela.
        void FocusFirstAction()
        {
            Control target = panel == 6 ? actions.Find(c => (string)c.Tag == "output" && c.IsEnabled) : null;
            if (target == null) target = actions.Find(c => c.IsEnabled);
            if (target != null) target.Focus();
        }

        TextBlock Text(string value, double size, string color)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap };
        }
        // A paleta inteira fica nos recursos XAML; paineis dinamicos usam as mesmas cores.
        SolidColorBrush Brush(string key) { return (SolidColorBrush)window.FindResource(key); }
        void Caption(string text) { var t = Text(text, 14, "Muted"); t.Margin = new Thickness(0, 14, 0, 9); body.Children.Add(t); }
        Button Action(string label, Action action)
        {
            var button = new Button { Content = label, Style = (Style)window.FindResource("Action") };
            WireMotion(button);
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            button.Click += delegate { InvokeSoundAction(button, action); }; actions.Add(button); body.Children.Add(button); return button;
        }
        // Botao compacto de uma linha, usado nos seletores e listas do Som e do Desempenho.
        Button PanelButton(Panel parent, string label, string tag, Action callback)
        {
            var button = new Button { Tag = tag, Style = (Style)window.FindResource("Action"), FontSize = 16,
                Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 5), MinHeight = 40 };
            var text = Text(label, 16, "TextPrimary"); text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
            button.Content = text; button.ToolTip = label;
            WireMotion(button);
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += delegate { InvokeSoundAction(button, callback); };
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            parent.Children.Add(button); actions.Add(button);
            return button;
        }

        // Lista flutuante, como um menu: ancorada sob o seletor e por cima dos controles seguintes, sem empurrar o layout.
        Border flyout;
        Button flyoutSelector;
        void Flyout(Panel parent, Panel choices, Button selector)
        {
            var list = new ScrollViewer { Content = choices, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Padding = new Thickness(0, 0, 8, 0) };
            var frame = new Border { Background = Brush("SurfaceFlyout"), BorderBrush = Brush("Divider"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = list,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = .55, Color = Colors.Black } };
            frame.SetBinding(FrameworkElement.WidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = parent });
            var anchor = new Canvas { Height = 0 };
            anchor.Children.Add(frame); Panel.SetZIndex(anchor, 10); parent.Children.Add(anchor);
            flyout = frame; flyoutSelector = selector;
            MotionReveal(frame, MotionNormal, 0, 8);
        }
        static void FlyoutChoice(Button b) { b.FontSize = 14; ((TextBlock)b.Content).FontSize = 14; b.Background = Brushes.Transparent; }
        void SetPanel(int index)
        {
            mediaEditing = null;
            CancelAppSwitch("Troca de janela cancelada pela navegação.");
            powerConfirmation = null;
            if (index == 0) StopDescription();
            int before = panel; motionBuilding = true;
            CancelElementMotion(MotionPart("PanelContent"));
            CancelElementMotion(MotionPart("MotionOverlay"));
            ((Canvas)window.FindName("MotionOverlay")).Children.Clear();
            if (index != 0) { gameConfirmation = null; gameCloseConfirmation = null; gameCloseForce = false; }
            double height = ReleaseSidebarHeight();
            panel = index; body.Children.Clear(); actions.Clear(); homeRows.Clear(); flyout = null; flyoutSelector = null; panelFill = null;
            ((TextBlock)window.FindName("Feedback")).TextWrapping = TextWrapping.Wrap;
            ((TextBlock)window.FindName("Feedback")).TextTrimming = TextTrimming.None;
            ((ScrollViewer)window.FindName("BodyScroll")).VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            ((ScrollViewer)window.FindName("BodyScroll")).ScrollToTop();
            string[] titles = { "Início", "", "", "Desempenho", "Controle e energia", "Configurações", "Som", "Lojas", "Gravação", "AMD" };
            ((TextBlock)window.FindName("PanelTitle")).Text = titles[index];
            for (int i = 0; i < rail.Count; i++) rail[i].Background = i == RailIndex(index) ? Brush("SurfaceSelected") : Brushes.Transparent;
            ClearFeedback(true);
            batteryPanelText = null;
            if (index == 0) BuildHome();
            else if (index == 3) BuildPerfPanel(true);
            else if (index == 4) BuildPowerPanel();
            else if (index == 6) BuildAudioPanel(true);
            else if (index == StoresPanel) BuildStores();
            else if (index == AmdTuningPanel) { BuildAmdTuningPanel(); if (!verify) QueueUi(delegate { ReadAmdTuning(); }, DispatcherPriority.Background); }
            else if (index == AmdPanel)
            {
                BuildAmdPanel();
                if (!verify) { amdNextRead = DateTime.MinValue; QueueUi(delegate { AmdTick(); }, DispatcherPriority.Background); }
            }
            else
            {
                // Configuracoes sempre abre na pagina principal; Conexoes (Wi-Fi e Bluetooth) fica no topo (ConnectionsPanel.cs).
                settingsPage = "main"; connectionMenu = null; connectionConfirm = null;
                BuildSettings();
                connectionsNextRead = DateTime.MinValue; devicesNextRead = DateTime.MinValue; ConnectionsTick();
            }
            WriteLog("painel: " + titles[index]);
            MotionHeight(height); MotionPanel(before, index); motionBuilding = false; MotionResumeMedia();
            if (index == StoresPanel) ReadStores();
            RenderFeedback();
            RefreshPresentationTimers();
        }

        // No trilho, voltar fecha a barra (som de fechar); nos demais casos, o som de voltar.
        void Back()
        {
            if (BackMedia()) return;
            RestartDescriptionForInput();
            if (SystemPowerBack() || GamesBack() || AppCloseBack() || ConnectionsBack() || AudioBack() || PerfBack()) { Sound("voltar"); return; }
            if (returnToHome)
            {
                returnToHome = false; motionPanelAxis = "home"; motionPanelDirection = -1; SetPanel(0);
                FocusHome(homeReturnTag); Sound("voltar"); return;
            }
            if (rail.Contains(Keyboard.FocusedElement as Button)) Dismiss();
            else { rail[RailIndex(panel)].Focus(); Sound("voltar"); }
        }
        void ChangePanel(int direction)
        {
            RestartDescriptionForInput();
            int[] panels = RailOrder;
            int current = Keyboard.FocusedElement == rail[1] ? 1 : RailIndex(panel);
            int next = panels[(Array.IndexOf(panels, current) + direction + panels.Length) % panels.Length];
            // Biblioteca e uma parada de foco: nao muda o painel nem abre a Steam sem confirmar.
            if (next != 1) { returnToHome = false; motionPanelDirection = direction; SetPanel(next); }
            rail[next].Focus();
        }
        // Som de navegacao so quando o gesto mudou algo: o foco andou ou um controle deslizante mudou de valor.
        // Foco programatico (reconstrucoes, abrir paineis) nao passa por aqui e fica mudo; a secao de midia tambem.
        void Navigate(string key)
        {
            RestartDescriptionForInput();
            var before = Keyboard.FocusedElement; var slider = before as Slider; double value = slider == null ? 0 : slider.Value;
            NavigateFocus(key);
            if ((Keyboard.FocusedElement != before || slider != null && slider.Value != value) && !SoundlessMedia(Keyboard.FocusedElement as Control)) Sound("mover");
        }
        void NavigateFocus(string key)
        {
            var focused = Keyboard.FocusedElement as Control;
            int index = rail.IndexOf(focused as Button);
            if (index >= 0)
            {
                int[] order = RailOrder;
                int position = Array.IndexOf(order, index);
                if (key == "CIMA") rail[order[(position + order.Length - 1) % order.Length]].Focus();
                if (key == "BAIXO") rail[order[(position + 1) % order.Length]].Focus();
                if (key == "DIREITA") FocusFirstAction();
                return;
            }
            if (panel == 0 && NavigateHome(focused, key)) return;
            if (panel == AmdPanel && NavigateAmd(focused, key)) return;
            if (NavigateAudio(focused, key)) return;
            var slider = focused as Slider;
            if (slider != null && (key == "ESQUERDA" || key == "DIREITA"))
            { slider.Value += key == "DIREITA" ? 5 : -5; return; }
            if (key == "ESQUERDA") { rail[RailIndex(panel)].Focus(); return; }
            int pos = actions.IndexOf(focused);
            if (pos < 0) { rail[RailIndex(panel)].Focus(); return; }
            if (key == "CIMA" || key == "BAIXO")
                for (int step = 1; step <= actions.Count; step++)
                {
                    var target = actions[(pos + (key == "CIMA" ? -step : step) + actions.Count) % actions.Count];
                    if (target.IsEnabled && target.Focus()) break;
                }
        }
        void Activate()
        {
            RestartDescriptionForInput();
            // Botoes escolhem um som apos a acao. Deslizantes (mixer, player, microfone) nao executam comandos
            // no ✕/Enter e ficam mudos; o mute e o play/pause ficam nos botoes proprios (ADRs 0047 e 0085).
            if (ActivateMedia()) return;
            var button = Keyboard.FocusedElement as Button;
            if (button != null && button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        void OnKey(object sender, KeyEventArgs e)
        {
            if (!ActivationAllowed()) return;
            if (motionClosing) { if (e.Key == Key.Escape) Show(); e.Handled = true; return; }
            if (e.Key == Key.Escape) Back();
            else if (e.Key == Key.Q || e.Key == Key.PageUp) ChangePanel(-1);
            else if (e.Key == Key.E || e.Key == Key.PageDown) ChangePanel(1);
            else if (e.Key == Key.Up) Navigate("CIMA");
            else if (e.Key == Key.Down) Navigate("BAIXO");
            else if (e.Key == Key.Left) Navigate("ESQUERDA");
            else if (e.Key == Key.Right) Navigate("DIREITA");
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                if (e.IsRepeat) { e.Handled = true; return; }
                // Num app aberto, segurar mostra o X em 2 s; a repeticao do teclado nao reabre durante o segurar.
                if (holdTarget != null) { }
                else if (!BeginHold("key")) Activate();
            }
            else return;
            e.Handled = true;
        }
#if XGB_VERIFY
        public void Verify()
        {
            // Renderiza a mesma arvore WPF e verifica transicoes sem pressionar teclas do usuario.
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); window.UpdateLayout();
            foreach (int index in new[] { 0, 3, 4, 5, 6 })
            {
                SetPanel(index);
                if (index == 0) actions[0].Focus(); else rail[index].Focus();
                window.UpdateLayout();
                SaveRender("painel-" + index);
            }
            SetPanel(3); actions[0].Focus(); Back();
            if (Keyboard.FocusedElement != rail[3]) throw new Exception("Falha: voltar ao trilho.");
            Back(); if (window.IsVisible) throw new Exception("Falha: ocultar pelo trilho.");
            RequestActivation("Win+G");
            if (!window.IsVisible) throw new Exception("Falha: abrir por ativacao Win+G.");
            RequestActivation("Win+G");
            if (window.IsVisible) throw new Exception("Falha: ocultar por ativacao Win+G.");
            // Biblioteca recebe foco no ciclo, sem despacho externo nem painel proprio.
            Show(); SetPanel(0); rail[0].Focus(); ChangePanel(1);
            if (panel != 0 || Keyboard.FocusedElement != rail[1]) throw new Exception("Falha: ancorar na Biblioteca.");
            ChangePanel(1); if (panel != 6) throw new Exception("Falha: proximo painel.");
            ChangePanel(-1); if (panel != 6 || Keyboard.FocusedElement != rail[1]) throw new Exception("Falha: voltar a Biblioteca.");
            ChangePanel(-1); if (panel != 0) throw new Exception("Falha: painel anterior.");
            ChangePanel(-1); if (panel != 4) throw new Exception("Falha: ciclo do painel anterior.");
            Show(); window.UpdateLayout();
            rail[0].Focus(); Navigate("BAIXO"); Navigate("BAIXO");
            if (Keyboard.FocusedElement != rail[6]) throw new Exception("Falha: trilho sem Jogo atual.");
            Navigate("DIREITA");
            if ((string)((Control)Keyboard.FocusedElement).Tag != "output") throw new Exception("Falha: entrada do Som pela saida de som.");
            VerifyAudioPanel();
            VerifySystemPower();
            VerifyReleaseSurface();
            VerifyAppearancePreferences();
            VerifyHome();
            VerifyGames();
            VerifyHomeGamesLayout();
            VerifyOpenApps();
            VerifyXboxOpenApps();
            VerifyAppSwitchDelivery();
            VerifyGameTransitions();
            VerifySteamSessionRelease();
            VerifyMedia();
            VerifyMediaConflicts();
            VerifyPerf();
            VerifyBattery(); VerifyStatus(); VerifyHeadsetStatus();
            VerifySteamActions();
            VerifyWindowFocus();
            VerifySteamInput();
            VerifySettingsActions();
            VerifyConnections();
            VerifySettingsExtras();
            VerifyStores();
            VerifyRailFocus();
            VerifySounds();
            VerifyLaunchSettings();
            VerifyDescriptions();
            VerifyOperationFeedback();
            VerifyActionHistory();
            VerifyButtonIcons();
            VerifyAmd();
            VerifyAmdTuning();
            VerifyCaptureCorrections();
            VerifySettingToggles();
            VerifyController();
            VerifyActivation();
            VerifyDesktopWinG();
            VerifyGamePersistence(); VerifyLauncherChildren();
            VerifyExitGamingMode(); VerifyTaskbarAutoHide(); VerifyAppVolume(); VerifySteamStatus();
            VerifyKeyboard();
            VerifyAccentTheme();
            VerifyScene();
            File.WriteAllText(Path.Combine(Output, "verificacao.txt"), "OK: 5 paineis renderizados (sem Jogo atual); camada sem titulo, selo, dicas ou botao de encerrar; cena responsiva em 8 telas (escala do Windows, area minima, margem segura, barra e status nos cantos, Home com cartoes proporcionais e rolagem quando necessaria); foco e atalho; ciclo e trilho sem Jogo atual; Som com player no topo, entrada pela saida de som, mute compacto e pelo slider, listas de dispositivos flutuantes sem deslocar controles e com foco preso, apps no painel principal, agrupamento por identidade/GUID, alvos em multiplas saidas, scroll/foco durante tracking e estado vazio; energia real com confirmacao e efeitos substituidos nos testes; Home com utilidades so por icones e nome do foco no titulo da secao; alternador de janelas, segurar 2 s mostra o X de fechar ao lado do icone (toque curto abre, Esc/foco recolhe, fechar pela janela, erro e janela que continua aberta), faixa com setas sobrepostas/degrade, paginacao sem ativar app, foco fora do esfumacado, scroll/foco, janela fechada, erros e confirmacao de foreground; player de midia com reproduzir/pausar, anterior/proxima, aleatorio, ciclo de repetir, posicao, volume/mute pelo mixer, troca de fonte, controles inativos pulados, erro e fonte encerrada; Desempenho com valores formatados sem rolagem, lista flutuante de modos de energia com foco preso, aplicacao so pela escolha, erro, estado sem HWiNFO, fora do plano Equilibrado e mapeamento de rotulos AMD/Intel/NVIDIA e dos rotulos reais AM5/RDNA4 (SVI3, TBP sem o limite, hotspot sem o maximo, FCLK/UCLK, DIMM, PresentMon fora); faixa de status com hora grande, rede (Ethernet, Wi-Fi por barras, sem internet, sem conexao, ilegivel) e DualSense com bateria (normal, baixa, carregando, erro, aguardando, troca e perda de dispositivo); Home e retorno; handlers Steam e erros; Configuracoes do Windows/Steam com despacho, ocultacao e erros; Conexoes (Wi-Fi e Bluetooth, redes e dispositivos conhecidos) com comandos substituidos, ver conexoes-verificacao.txt; Luz noturna, DSX e contorno vermelho dos botoes de fechar, ver configuracoes-extras-verificacao.txt; Lojas pelo acesso rapido, ver lojas-verificacao.txt.\r\nAudio, janelas, midia e lancamentos externos substituidos na verificacao; nao confirma tecla fisica, controle real ou telas externas. Ver verificacao de hardware de audio separada.\r\n", Encoding.UTF8);
            File.AppendAllText(Path.Combine(Output, "verificacao.txt"), "Jogos: VDF textual/binario; catalogo multipla biblioteca, atalhos unsigned e wrappers; historico atomico/protegido; sessoes por executavel/PID+criacao, foco, minimizacao e recriacao; recentes reais com imagens locais em cache e simbolo de reserva, estados vazios, confirmacao de troca, despacho substituido, erro e timeout. Ver jogos-verificacao.txt.\r\n", Encoding.UTF8);
            WriteLog("verificacao OK"); window.Hide();
        }
#endif

#if XGB_VERIFY
        void SaveRender(string name)
        {
            var scene = (FrameworkElement)window.FindName("Scene");
            var bitmap = new RenderTargetBitmap((int)Math.Round(scene.ActualWidth), (int)Math.Round(scene.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scene);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Output, name + ".png"))) encoder.Save(file);
        }
#endif

        // Idempotente e tolerante a falhas: tambem roda depois de um erro, entao cada etapa segue mesmo se a anterior falhar.
        public void Dispose()
        {
            if (disposed) return;
            CancelCapturePhoto();
            disposed = true;
            CancelGameNavigation("XGB_P encerrando."); RestoreAppVolumeNow();
            CancelSteamNavigation();
            FlushAppearancePreferences();
            FlushActionHistory(true);
            StopAccentTheme();
            StopDescription();
            feedbackTimer.Stop();
            if (mediaCommands != null) mediaCommands.Dispose();
            gameOperations.Stop(ResumePausedGames);
            if (gameSessions != null) gameSessions.FlushHistory();
            try { DisposeKeyboard(); } catch (Exception ex) { WriteLog("teclado ao encerrar: " + ex.Message); }
            ReleaseSteamInputOnExit();
            AppDomain.CurrentDomain.UnhandledException -= OnFatalError;
            clock.Stop(); repeat.Stop(); audioTimer.Stop(); openAppsTimer.Stop(); appSwitchTimer.Stop(); mediaTimer.Stop(); perfTimer.Stop(); gamesTimer.Stop(); connectionsTimer.Stop(); windowTraceTimer.Stop(); amdTimer.Stop(); amdMicTimer.Stop(); amdTuningTimer.Stop();
            try { XGBP.Amd.AdlxGraphics.Shutdown(); } catch (Exception ex) { WriteLog("adlx ao encerrar: " + ex.Message); }
            StopActivation();
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch (Exception ex) { WriteLog("bandeja: " + ex.Message); }
            try { if (controller != null) controller.Stop(); } catch (Exception ex) { WriteLog("controle: " + ex.Message); }
            StopXInputGuide();
            try { DisposeSounds(); } catch (Exception ex) { WriteLog("sons: " + ex.Message); }
            try
            {
                SystemParameters.StaticPropertyChanged -= MotionSystemChanged; StopScene(); StopMotion(); motionButtons.Clear();
                window.Close();
            }
            catch (Exception ex) { WriteLog("janela ao encerrar: " + ex.Message); }
            WriteLog("encerrado; observador de ativacao liberado");
            gameOperations.Dispose();
        }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    }
}
