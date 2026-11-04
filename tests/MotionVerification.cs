// Verificacao temporal WPF com fixtures: nenhum comando externo ou setter real.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static void MotionAssert(bool value, string message) { if (!value) throw new Exception("Animacoes: " + message); }
        static void PumpMotion(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(Math.Max(1, milliseconds)) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }
        void MotionFixtures()
        {
            ConfigureSteamVerification(); steamLaunch = delegate(string uri) { lastSteamUri = uri; };
            audioWrite = delegate { }; mediaSend = delegate { }; powerSet = delegate { }; gameLaunch = delegate { };
            appAvailable = delegate { return true; }; switchApp = delegate { }; appForeground = delegate { return true; };
            audioPending.Clear(); mediaSeekTarget = -1; launchingGame = null; gameConfirmation = null;
            gamesView = ExampleGames(); openApps = ExampleOpenApps(); audioSnapshot = ExampleAudio();
            mediaSessions = ExampleMedia(); mediaPinned = null; perfSnapshot = ExamplePerf();
        }
        public void InspectMotion()
        {
            // Janela descobrivel pela automacao; somente fixtures, sem hotkey/HID ou comandos reais.
            MotionFixtures(); motionTesting = true;
            window.ShowInTaskbar = true; window.Title = "XGB_P · Inspeção de animações (dados de teste)";
            SetPanel(0); Show();
        }
        public void VerifyMotion()
        {
            MotionFixtures(); motionTesting = true; motionSystemOverride = false;
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            SetPanel(0); Show(); PumpMotion(400);
            var sidebar = MotionPart("Sidebar"); var slide = MotionTranslation(sidebar);
            MotionAssert(Math.Abs(slide.X) < .001 && sidebar.Opacity == 1, "entrada nao terminou");
            Dismiss(); PumpMotion(45);
            double interrupted = slide.X;
            MotionAssert(window.IsVisible && motionClosing && interrupted < 0, "saida manual sem transicao");
            Toggle(); MotionAssert(!motionClosing && window.IsVisible, "reabertura ignorada");
            MotionAssert(Math.Abs(slide.X - interrupted) < 2, "reabertura saltou em vez de retomar o valor corrente");
            PumpMotion(350); MotionAssert(window.IsVisible && Math.Abs(slide.X) < .001, "callback antigo ocultou reabertura");
            for (int i = 0; i < 24; i++)
            {
                ChangePanel(i % 2 == 0 ? 1 : -1);
                if (Keyboard.FocusedElement == rail[1]) ChangePanel(i % 2 == 0 ? 1 : -1);
            }
            MotionAssert(motionClocks.Count < 100, "clocks acumulados durante troca rapida");
            PumpMotion(350); MotionAssert(panel == 0, "ciclo mudou com animacoes");
            MotionAssert(double.IsNaN(sidebar.Height), "troca rapida deixou a barra com altura fixa");
            // Home -> Energia: a barra encolhe animada ate a altura do trilho e volta a seguir o conteudo.
            double tall = sidebar.ActualHeight; ChangePanel(-1); PumpMotion(60);
            double middle = PresentedSidebarHeight; var during = SceneBox(sidebar);
            MotionAssert(Math.Abs((during.Top + during.Bottom) / 2 - MotionPart("Scene").Height / 2) < .5, "barra saiu do centro vertical durante a animacao");
            PumpMotion(300);
            MotionAssert(middle < tall - 1 && sidebar.ActualHeight < middle - 1 && double.IsNaN(sidebar.Height), "altura da barra nao animou ate o conteudo: " + tall + " / " + middle + " / " + sidebar.ActualHeight);
            ChangePanel(1); PumpMotion(350); MotionAssert(panel == 0 && Math.Abs(sidebar.ActualHeight - tall) < .5, "altura da Home nao voltou");
            FocusHome("utility-audio"); Activate();
            MotionAssert(panel == 6 && (string)((Control)Keyboard.FocusedElement).Tag == "output", "entrada do Som perdeu foco");
            PumpMotion(250); ToggleAudioDevices(0); PumpMotion(250);
            Navigate("BAIXO"); MotionAssert(((string)((Control)Keyboard.FocusedElement).Tag).StartsWith("pick-"), "foco escapou da lista");
            Back(); MotionAssert(flyout == null && (string)((Control)Keyboard.FocusedElement).Tag == "output", "menu nao devolveu foco");
            Back(); MotionAssert(panel == 0 && (string)((Control)Keyboard.FocusedElement).Tag == "utility-audio", "retorno a Home perdeu origem");
            PumpMotion(350);
            var button = (Button)Keyboard.FocusedElement; var scale = MotionScale(button);
            MotionAssert(Math.Abs(scale.ScaleX - 1.04) < .001, "escala do icone incorreta: " + scale.ScaleX + " | enabled=" + MotionEnabled + " | foco=" + button.IsKeyboardFocused + " | tag=" + button.Tag);
            rail[0].Focus(); PumpMotion(250);
            MotionAssert(Math.Abs(scale.ScaleX - 1) < .001, "escala anterior nao restaurada");
            SetPanel(0); PumpMotion(350); FocusHome("resume"); PumpMotion(250);
            button = (Button)Keyboard.FocusedElement; scale = MotionScale(button);
            MotionAssert(Math.Abs(scale.ScaleX - 1.02) < .001, "escala do cartao incorreta");
            var scroll = (ScrollViewer)MotionPart("BodyScroll");
            var presenter = (ScrollContentPresenter)scroll.Template.FindName("PART_ScrollContentPresenter", scroll);
            var bounds = button.TransformToAncestor(presenter).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
            var clip = VisualTreeHelper.GetClip(presenter);
            MotionAssert(clip == null || clip.Bounds.Contains(bounds), "foco ampliado recortado");
            long before = motionStarted; RenderGames(false); RenderMedia(); UpdateStatusIcons();
            MotionAssert(motionStarted == before, "snapshot identico repetiu animacao");
            double cardHeight = gameResumeRow[0].ActualHeight;
            gamesView.Featured.IconPng = MotionArtwork(); gamesView.Featured.IconKey = "motion-art"; RenderGames(false); window.UpdateLayout();
            var iconLayer = (Grid)((Border)((Grid)((Button)gameResumeRow[0]).Content).Children[0]).Child;
            var artwork = (Image)iconLayer.Children[iconLayer.Children.Count - 1];
            PumpMotion(40); MotionAssert(artwork.Opacity > 0 && artwork.Opacity < 1, "arte local nao faz transicao");
            PumpMotion(220); MotionAssert(iconLayer.Children.Count == 1 && artwork.Opacity == 1 && gameResumeRow[0].ActualHeight == cardHeight, "arte alterou layout ou manteve simbolo antigo");
            MotionAssert((string)((Control)Keyboard.FocusedElement).Tag == "resume", "imagem perdeu identidade do foco");
            openApps = new List<XGBP.Desktop.DesktopWindow>();
            for (int i = 0; i < 18; i++) openApps.Add(new XGBP.Desktop.DesktopWindow { Handle = new IntPtr(1000 + i), ProcessId = (uint)(2000 + i), AppName = "App " + i, Title = "Janela " + i });
            RebuildOpenApps(); window.UpdateLayout(); rail[0].Focus(); ScrollOpenApps(120, true);
            PumpMotion(45); MotionAssert(openAppsScroll.HorizontalOffset > 0 && openAppsScroll.HorizontalOffset < 120, "rolagem nao interpola");
            PumpMotion(220); MotionAssert(Math.Abs(openAppsScroll.HorizontalOffset - 120) < 1, "rolagem nao terminou no alvo");
            openAppsRow[17].Focus(); PumpMotion(250);
            var focusedApp = (FrameworkElement)Keyboard.FocusedElement;
            var appBounds = focusedApp.TransformToAncestor(openAppsScroll).TransformBounds(new Rect(0, 0, focusedApp.ActualWidth, focusedApp.ActualHeight));
            MotionAssert(appBounds.Left >= 0 && appBounds.Right <= openAppsScroll.ActualWidth + .5, "app focado ficou fora da faixa");
            string appTag = (string)((Control)focusedApp).Tag; openApps.RemoveAt(0); RebuildOpenApps(); PumpMotion(250);
            MotionAssert((string)((Control)Keyboard.FocusedElement).Tag == appTag, "reconstrucao de apps perdeu foco estavel");
            int commands = 0; mediaSend = delegate { commands++; };
            PumpMotion(300); MotionAssert(commands == 0 && audioPending.Count == 0 && mediaSeekTarget < 0, "progresso visual enviou comando");
            SetPanel(3); PumpMotion(350); perfSnapshot.CpuUsage = 73; UpdatePerf();
            PumpMotion(40); MotionAssert(perfBars["cpu"].Value < 73, "barra nao interpola");
            PumpMotion(250); MotionAssert(Math.Abs(perfBars["cpu"].Value - 73) < .01 && TileText("cpu0").StartsWith("73"), "barra ou numero nao corresponde ao snapshot");
            motionReduced = true; SettleMotion(); Show(); SetPanel(0); FocusHome("resume");
            MotionAssert(motionClocks.Count == 0 && MotionScale((Button)Keyboard.FocusedElement).ScaleX == 1, "movimento reduzido ainda anima");
            Dismiss(); MotionAssert(!window.IsVisible, "saida reduzida nao imediata");
            motionReduced = false; motionSystemOverride = true; Show(); ChangePanel(1);
            MotionAssert(motionClocks.Count == 0, "preferencia Windows ignorada");
            motionSystemOverride = false; SetPanel(5); PumpMotion(250); motionSetting.Focus(); Activate();
            MotionAssert(motionReduced && motionClocks.Count == 0, "configuracao Reduzir movimento nao cancela clocks");
            Activate(); MotionAssert(!motionReduced, "configuracao nao permite reativar movimento");
            motionSystemOverride = false; Hide(); Show(); PumpMotion(60); Hide();
            MotionAssert(motionClocks.Count == 0 && !motionTicker.IsEnabled, "animacao sobreviveu oculta");
            // Abrir do zero desloca os filhos diretos do corpo; acionar um deles pelo clique derrubava a previa (crash de 2026-10-24 09:32).
            Show(); PumpMotion(350); lastSettingsUri = null;
            var row = (Button)actions.Find(c => (string)c.Tag == "windows-settings");
            MotionAssert(row.Parent == body && row.RenderTransform is TransformGroup && MotionScale(row) != null, "abertura do zero trocou a escala da linha das Configuracoes");
            row.Focus(); Activate();
            MotionAssert(!window.IsVisible && lastSettingsUri == WindowsSettingsUri, "linha das Configuracoes depois de abrir do zero");
            SetPanel(0); Show(); PumpMotion(350); FocusHome("steam-home");
            findBigPicture = delegate { return new XGBP.Desktop.WindowObservation { Window = openApps[0] }; };
            existingWindowAvailable = delegate { return true; };
            var navigationFocus = controllerFocus; controllerFocus = delegate { return true; };
            var start = (Button)Keyboard.FocusedElement;
            MotionAssert(start.Parent == body && start.RenderTransform is TransformGroup && MotionScale(start).ScaleX > 1, "Inicio perdeu a escala de foco depois de abrir do zero");
            Activate();
            MotionAssert(!window.IsVisible && lastSteamUri == SteamStartPageUri && motionClocks.Count == 0, "navegacao Home e foco confirmados nao devem esperar animacao de saida");
            controllerFocus = navigationFocus; ConfigureWindowFocusVerification();
            Show(); PumpMotion(350); OpenApp(openApps[0]);
            MotionAssert(!window.IsVisible && switchingApp == null, "troca de app perdeu confirmacao de foco");
            RecordMotionSprints();
            // Geometria fisica a 1080p/4K: a cena segue a tela (4K a 100% amplia 1,5x) e a barra ocupa a altura entre as margens.
            motionReduced = true; SettleMotion(); SetPanel(0); Show(); window.UpdateLayout();
            foreach (int width in new[] { 1920, 3840 }) {
                window.Width = width; window.Height = width * 9.0 / 16; window.UpdateLayout(); PumpMotion(60);
                var fitted = MotionPart("Scene");
                MotionAssert(sidebar.ActualWidth == 588 && Math.Abs(sceneScale - SceneScale(window.Width, window.Height)) < .001 &&
                    Math.Abs((SceneBox(sidebar).Top + SceneBox(sidebar).Bottom) / 2 - fitted.Height / 2) < .5 && SceneBox(sidebar).Top >= sceneEdge - .5 &&
                    SceneBox(sidebar).Bottom <= fitted.Height - sceneEdge + .5 && PanelFitsContent(), "cena nao acompanhou a resolucao");
                SaveRender("animacoes-estavel-" + width); SavePhysicalMotion(width);
            }
            Hide();
            File.WriteAllText(Path.Combine(Output, "animacoes-verificacao.txt"),
                "OK: entrada/saida temporal; interrupcao/reabertura sem callback antigo; 24 trocas rapidas; foco imediato/escala e limites; menus e retorno a origem; snapshots identicos sem reentrada; progresso sem comandos; barra interpolada e numeros reais; movimento reduzido e Windows desativado; clocks/timer zerados ao ocultar; Steam imediata; app com foreground substituido confirmado; scene 1080p/4K.\r\n" +
                "Gravacoes: sequencias da arvore WPF em PNG, 30 amostras/s solicitadas; tempos reais e custo de render no CSV. Fixtures em todos os backends e comandos externos. Nao mede FPS do compositor nem prova controle, TV ou jogos reais.\r\n", Encoding.UTF8);
            motionTesting = false; motionSystemOverride = null;
        }
        byte[] MotionArtwork()
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) { drawing.DrawRoundedRectangle(Brush("XboxGreen"), null, new Rect(0, 0, 64, 64), 10, 10); drawing.DrawEllipse(Brush("TextPrimary"), null, new Point(32, 32), 15, 15); }
            var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream()) { encoder.Save(stream); return stream.ToArray(); }
        }
        void SavePhysicalMotion(int width)
        {
            var lines = new List<string> { "resolution,sample,render_ms" };
            RenderTargetBitmap bitmap = null;
            for (int i = 0; i < 5; i++) {
                var watch = Stopwatch.StartNew(); bitmap = new RenderTargetBitmap(width, width * 9 / 16, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.00}", width, i, watch.Elapsed.TotalMilliseconds));
            }
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Output, "animacoes-fisico-" + width + ".png"))) encoder.Save(file);
            File.WriteAllLines(Path.Combine(Output, "render-" + width + ".csv"), lines, Encoding.UTF8);
        }
        void RecordMotionSprints()
        {
            window.Width = 1920; window.Height = 1080; MotionFixtures();
            RecordMotion("sprint-1", new Action[] { delegate { Hide(); SetPanel(0); Show(); }, Dismiss, delegate { Toggle(); } });
            RecordMotion("sprint-2", new Action[] { delegate { SetPanel(0); FocusHome("resume"); }, delegate { FocusHome("utility-audio"); }, delegate { rail[6].Focus(); }, delegate { rail[3].Focus(); } });
            RecordMotion("sprint-3", new Action[] { delegate { SetPanel(0); UseHomeUtility("utility-audio"); }, delegate { ToggleAudioDevices(0); }, delegate { Back(); }, delegate { Back(); } });
            RecordMotion("sprint-4", new Action[] {
                delegate { SetPanel(0); },
                delegate { openApps.RemoveAt(0); RebuildOpenApps(); gamesView.Featured.IconPng = MotionArtwork(); gamesView.Featured.IconKey = "record-art"; gamesView.Current = null; gamesView.Activities.Clear(); RenderGames(false); },
                delegate { SetPanel(3); }, delegate { perfSnapshot.CpuUsage = 87; perfSnapshot.GpuUsage = 32; UpdatePerf(); }
            });
            RecordMotion("sprint-5", new Action[] {
                delegate { SetPanel(0); }, delegate { MediaDo("toggle"); MediaDo("shuffle"); },
                delegate { mediaSessions[0].Title = "Nova faixa · transição"; mediaSessions[0].CoverKey = "new"; RenderMedia(); },
                delegate { Feedback("Exemplo de retorno de ação"); }
            });
        }
        void RecordMotion(string name, Action[] steps)
        {
            string folder = Path.Combine(Output, "motion-frames", name); Directory.CreateDirectory(folder);
            var lines = new List<string> { "frame,elapsed_ms,render_ms" }; var watch = Stopwatch.StartNew(); int index = 0;
            foreach (var step in steps) {
                step(); window.UpdateLayout(); var segment = Stopwatch.StartNew();
                for (int frame = 0; frame < 15; frame++) {
                    PumpMotion(Math.Max(1, (int)(frame * 1000.0 / 30 - segment.Elapsed.TotalMilliseconds)));
                    var cost = Stopwatch.StartNew();
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(MotionPart("Scene")), null, new Rect(0, 0, 960, 540));
                    var bitmap = new RenderTargetBitmap(960, 540, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(folder, (index++).ToString("D4") + ".png"))) encoder.Save(file);
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.00},{2:0.00}", index - 1, watch.Elapsed.TotalMilliseconds, cost.Elapsed.TotalMilliseconds));
                }
                PumpMotion(100);
            }
            File.WriteAllLines(Path.Combine(folder, "timing.csv"), lines, Encoding.UTF8);
        }
    }
}
