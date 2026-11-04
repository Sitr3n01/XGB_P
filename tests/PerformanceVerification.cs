// Regressao de custo/agenda com fixtures. Nao certifica FPS do compositor, jogo ou controle fisico.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XGBP.Audio;
using XGBP.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        int performanceChecks;
        double performancePressureLag;
        void PerformanceCheck(bool value, string name)
        {
            if (!value) throw new Exception("Performance: " + name);
            performanceChecks++;
        }
        static BitmapSource PerformanceBitmap(ImageSource image, int width, int height)
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) drawing.DrawImage(image, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); return bitmap;
        }
        static byte[] PerformancePixels(BitmapSource bitmap)
        {
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels;
        }
        static bool SamePerformancePixels(BitmapSource a, BitmapSource b)
        {
            var left = PerformancePixels(a); var right = PerformancePixels(b);
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
        public void VerifyPerformance()
        {
            performanceChecks = 0;
            MotionFixtures(); motionTesting = true; motionSystemOverride = false;
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            VerifyRenderOnlyHeight();
            VerifyRetainedExit();
            VerifyMediaSelectionCost();
            VerifyPresentationTimers();
            VerifyOpeningPresentation();
            string order = "";
            DispatchPresentation(delegate { order += "snapshot"; });
            Dispatch(delegate { order += "input/"; });
            PumpMotion(20);
            PerformanceCheck(order == "input/snapshot", "snapshot precedeu entrada ou callback desapareceu");
            VerifyDispatchPressure();
            Hide();
            File.WriteAllText(Path.Combine(Output, "performance-verificacao.txt"),
                "OK: " + performanceChecks + " verificacoes. Altura visual sem redimensionar o conteudo; interrupcao e descarte; copia vetorial independente da origem; selecao de midia equivalente; agenda oculta sem ticks decorativos, PS/teclado/operacoes preservados e prioridade de snapshots.\r\n" +
                "Contencao sintetica: dois workers BelowNormal, 50 comandos FIFO e 50 snapshots; maior espera observada na fila=" + performancePressureLag.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms.\r\n" +
                "Capacidade WPF: tier=" + (RenderCapability.Tier >> 16) + "; modo solicitado=" + RenderOptions.ProcessRenderMode + ". Tier indica capacidade, nao aceleracao de toda operacao.\r\n" +
                "Fixtures e arvore WPF fora da tela. Nao mede FPS do compositor/jogo nem latencia fisica. Microbenchmark comparativo separado.\r\n", Encoding.UTF8);
        }
        void VerifyRenderOnlyHeight()
        {
            SetPanel(0); Show(); PumpMotion(300);
            double tall = PresentedSidebarHeight;
            SetPanel(4); window.UpdateLayout();
            double target = MotionPart("Sidebar").ActualHeight, content = MotionPart("PanelContent").ActualHeight;
            int resized = 0; SizeChangedEventHandler resize = delegate { resized++; };
            MotionPart("PanelContent").SizeChanged += resize;
            PumpMotion(45);
            PerformanceCheck(heightPresenting && PresentedSidebarHeight > target + 1 && PresentedSidebarHeight < tall - 1, "altura visual nao interpola");
            PerformanceCheck(Math.Abs(MotionPart("Sidebar").ActualHeight - target) < .01 &&
                Math.Abs(MotionPart("PanelContent").ActualHeight - content) < .01 && resized == 0, "altura visual redimensionou a arvore");
            double interrupted = PresentedSidebarHeight; SetPanel(0);
            PerformanceCheck(Math.Abs(PresentedSidebarHeight - interrupted) < 3, "troca rapida perdeu a altura visual corrente");
            PumpMotion(300);
            PerformanceCheck(!heightPresenting && HeightSurface.Visibility == Visibility.Collapsed && MotionPart("PanelFrame").Clip == null, "altura deixou camada ou recorte");
            PerformanceCheck(Math.Abs(MotionPart("Sidebar").ActualHeight - tall) < .5 && double.IsNaN(MotionPart("Sidebar").Height), "altura natural nao voltou");
            SetPanel(4); PumpMotion(35); motionReduced = true; SettleMotion();
            PerformanceCheck(!heightPresenting && motionClocks.Count == 0 && ((Border)MotionPart("PanelFrame")).Background == Brush("SurfacePanel"), "movimento reduzido deixou apresentacao parcial");
            motionReduced = false; SetPanel(0); PumpMotion(35); window.Width = 1280; window.Height = 720; window.UpdateLayout();
            PerformanceCheck(!heightPresenting && MotionPart("BodyScroll").Clip == null, "troca de resolucao deixou recorte");
            window.Width = 1920; window.Height = 1080; window.UpdateLayout(); PumpMotion(250);
            MotionPart("PanelContent").SizeChanged -= resize;
        }
        void VerifyRetainedExit()
        {
            SetPanel(0); Show(); PumpMotion(300); StopMotion();
            var frame = MotionPart("PanelFrame");
            var overlay = (Canvas)MotionPart("MotionOverlay");
            MotionExit(frame);
            PerformanceCheck(overlay.Children.Count == 1 && ((Image)overlay.Children[0]).Source is DrawingImage, "saida nao usa desenho retido");
            var image = (Image)overlay.Children[0];
            int width = (int)Math.Ceiling(frame.ActualWidth), height = (int)Math.Ceiling(frame.ActualHeight);
            var before = PerformanceBitmap(image.Source, width, height);
            string title = ((TextBlock)MotionPart("PanelTitle")).Text;
            ((TextBlock)MotionPart("PanelTitle")).Text = "Origem alterada";
            frame.Visibility = Visibility.Collapsed; window.UpdateLayout();
            var after = PerformanceBitmap(image.Source, width, height);
            PerformanceCheck(SamePerformancePixels(before, after), "saida depende da origem removida/alterada");
            var pixels = PerformancePixels(after); bool visible = false;
            for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 0) { visible = true; break; }
            PerformanceCheck(visible, "copia retida ficou vazia");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(after));
            using (var file = File.Create(Path.Combine(Output, "performance-saida-vetorial.png"))) encoder.Save(file);
            frame.Visibility = Visibility.Visible; ((TextBlock)MotionPart("PanelTitle")).Text = title;
            PumpMotion(250);
            PerformanceCheck(overlay.Children.Count == 0, "saida nao liberou a copia");
            for (int i = 0; i < 24; i++) MotionExit(frame);
            PerformanceCheck(overlay.Children.Count == 8, "rajada excedeu o limite de copias decorativas");
            StopMotion();
            MotionExit(frame); Hide();
            PerformanceCheck(overlay.Children.Count == 0 && motionClocks.Count == 0, "ocultacao reteve desenho ou clock");
        }
        void VerifyMediaSelectionCost()
        {
            var original = mediaSessions; string pin = mediaPinned;
            for (int scenario = 0; scenario < 24; scenario++)
            {
                mediaSessions = ExampleMedia();
                if ((scenario & 1) != 0) mediaSessions.Reverse();
                if ((scenario & 2) != 0) mediaSessions[0].Status = "closed";
                if ((scenario & 4) != 0) mediaSessions[1].Current = true;
                mediaPinned = scenario % 3 == 0 ? mediaSessions[0].Key : scenario % 3 == 1 ? "missing" : null;
                var choices = MediaChoices();
                var expected = choices.Find(s => s.Key == mediaPinned) ?? choices.Find(s => s.Current) ??
                    choices.Find(s => s.Playing) ?? (choices.Count == 0 ? null : choices[0]);
                PerformanceCheck(MediaSelected() == expected, "selecao de midia divergiu no caso " + scenario);
            }
            mediaSessions = original; mediaPinned = pin; SetPanel(0); Show();
            SetMediaClockText(TimeSpan.FromSeconds(10)); SetMediaClockText(TimeSpan.FromSeconds(10.5));
            PerformanceCheck(mediaElapsed.Text == "0:10", "texto de progresso mudou dentro do mesmo segundo");
            SetMediaClockText(null); PerformanceCheck(mediaElapsed.Text == "--:--", "tempo desconhecido");
            SetMediaClockText(TimeSpan.FromSeconds(11)); PerformanceCheck(mediaElapsed.Text == "0:11", "cache de texto nao voltou de tempo desconhecido");
        }
        void VerifyPresentationTimers()
        {
            presentationSchedulingTesting = true;
            audioInitial = false; audioPending.Clear(); audioInFlight.Clear(); feedbackNotices.Clear();
            launchingGame = closingGame = null; closingApp = null; pauseNotices.Clear();
            mediaSeekTarget = -1; mediaDeferred = null; steamGate = false; guideCycleActive = false; gamingModeExitPending = false;
            kbVisible = kbSuspended = false; kbPending = IntPtr.Zero; activationMode = false;
            Hide(); RefreshPresentationTimers();
            foreach (var timer in PresentationTimers()) PerformanceCheck(!timer.IsEnabled, "timer decorativo ativo oculta");
            int ticks = 0; EventHandler tick = delegate { ticks++; };
            foreach (var timer in PresentationTimers()) timer.Tick += tick;
            PumpMotion(200); PerformanceCheck(ticks == 0, "agenda oculta ainda acorda");
            foreach (var timer in PresentationTimers()) timer.Tick -= tick;
            activationMode = true; bool auto = kbPrefs.AutoOpen; kbPrefs.AutoOpen = true; RefreshPresentationTimers();
            PerformanceCheck(repeat.IsEnabled && repeat.Interval.TotalMilliseconds == 1000, "watchdog do teclado perdeu cadencia ociosa");
            guideCycleActive = true; RefreshPresentationTimers();
            PerformanceCheck(repeat.IsEnabled && repeat.Interval.TotalMilliseconds == 40, "PS nao acordou a decisao rapida");
            guideCycleActive = false; kbPending = new IntPtr(1); RefreshPresentationTimers();
            PerformanceCheck(repeat.IsEnabled && repeat.Interval.TotalMilliseconds == 40, "teclado pendente nao acordou");
            kbPending = IntPtr.Zero; gamingModeExitPending = true; RefreshPresentationTimers();
            PerformanceCheck(repeat.IsEnabled && repeat.Interval.TotalMilliseconds == 40, "saida do FSE perdeu acompanhamento");
            gamingModeExitPending = false; activationMode = false; kbPrefs.AutoOpen = auto;
            QueueAudio(new AudioChange { Key = "performance", DeviceId = "fixture" });
            PerformanceCheck(audioTimer.IsEnabled, "audio pendente foi desligado com a janela oculta");
            audioPending.Clear(); mediaSeekTarget = 10; RefreshPresentationTimers();
            PerformanceCheck(mediaTimer.IsEnabled, "seek pendente foi desligado");
            mediaSeekTarget = -1;
            DateTime now = DateTime.UtcNow; var oldNow = feedbackNow; feedbackNow = delegate { return now; };
            NewFeedback("Operacao de teste", FeedbackKind.Info, "performance"); RefreshPresentationTimers();
            PerformanceCheck(feedbackTimer.IsEnabled, "feedback nao acordou");
            now = now.AddSeconds(20); FeedbackTick(); RefreshPresentationTimers();
            PerformanceCheck(!feedbackTimer.IsEnabled, "feedback expirado reteve timer");
            feedbackNow = oldNow; activationMode = true; SetPanel(3); Show(); RefreshPresentationTimers();
            PerformanceCheck(perfTimer.IsEnabled && !connectionsTimer.IsEnabled && !openAppsTimer.IsEnabled, "agenda nao segue painel");
            SetPanel(5); RefreshPresentationTimers();
            PerformanceCheck(connectionsTimer.IsEnabled && !perfTimer.IsEnabled, "mudanca de painel manteve sensor desnecessario");
            Hide(); presentationSchedulingTesting = false;
        }
        void VerifyDispatchPressure()
        {
            var stop = new ManualResetEvent(false);
            var workers = new List<Thread>();
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var worker = new Thread(delegate() { while (!stop.WaitOne(0)) Thread.SpinWait(8192); })
                        { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "XGBP teste CPU" };
                    workers.Add(worker); worker.Start();
                }
                var order = new List<int>(); int snapshots = 0;
                var watch = Stopwatch.StartNew(); performancePressureLag = 0;
                for (int i = 0; i < 50; i++)
                {
                    int command = i; double queued = watch.Elapsed.TotalMilliseconds;
                    DispatchPresentation(delegate { snapshots++; });
                    Dispatch(delegate { order.Add(command); performancePressureLag = Math.Max(performancePressureLag, watch.Elapsed.TotalMilliseconds - queued); });
                    PumpMotion(10);
                }
                PumpMotion(20);
                PerformanceCheck(order.Count == 50 && snapshots == 50, "perdeu comandos/snapshots sob contencao");
                for (int i = 0; i < order.Count; i++) PerformanceCheck(order[i] == i, "reordenou comando sob contencao");
            }
            finally
            {
                stop.Set(); bool stopped = true;
                foreach (var worker in workers) stopped &= worker.Join(2000);
                if (stopped) stop.Dispose();
                PerformanceCheck(stopped, "worker de carga nao encerrou");
            }
        }
    }
}
