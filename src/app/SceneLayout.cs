// Cena responsiva: cobre o monitor principal, segue a escala do Windows e mantem a mesma margem segura nas bordas.
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // Unidades da cena = DIP. So reduz abaixo da area minima (barra de 820 + margens) e so amplia acima de 1440 de altura efetiva.
        const double SceneMinWidth = 1024, SceneMinHeight = 880, SceneMaxHeight = 1440;
        double sceneScale = 1, sceneEdge = 32;
        EventHandler displayChanged;
        // Corpo que cresce ate o limite e rola por dentro (o Som, com o mixer); os demais paineis rolam no BodyScroll.
        FrameworkElement panelFill;
        double panelBodyLimit = double.PositiveInfinity;

        void StartScene()
        {
            window.SizeChanged += delegate(object sender, SizeChangedEventArgs e) { FitScene(e.NewSize); };
            MotionPart("PanelFrame").SizeChanged += delegate { FitPanelBody(); };
            MotionPart("BodyScroll").SizeChanged += delegate { FitPanelBody(); };
            if (!verify)
            {
                // Resolucao ou escala alterada com o processo aberto: reposiciona na hora, mesmo oculta.
                displayChanged = delegate { Dispatch(PlaceWindow); };
                SystemEvents.DisplaySettingsChanged += displayChanged;
            }
            PlaceWindow();
        }
        void StopScene() { if (displayChanged != null) { SystemEvents.DisplaySettingsChanged -= displayChanged; displayChanged = null; } }

        // Tela inteira do monitor principal, como a Game Bar: a camada fica por cima da barra de tarefas enquanto aberta.
        void PlaceWindow()
        {
            window.Left = 0; window.Top = 0;
            window.Width = SystemParameters.PrimaryScreenWidth; window.Height = SystemParameters.PrimaryScreenHeight;
            FitScene(new Size(window.Width, window.Height));
        }

        static double SceneScale(double width, double height)
        {
            double scale = Math.Max(1, height / SceneMaxHeight);
            return Math.Min(scale, Math.Min(width / SceneMinWidth, height / SceneMinHeight));
        }
        // Margem segura de 3% do lado menor, entre 24 e 48: 32 em 1080, a mesma para a barra e para a hora.
        static double SceneEdge(double width, double height) { return Math.Round(Math.Max(24, Math.Min(48, Math.Min(width, height) * .03))); }

        void FitScene(Size size)
        {
            if (size.Width < 1 || size.Height < 1 || double.IsNaN(size.Width) || double.IsNaN(size.Height)) return;
            if (heightPresenting) ReleaseSidebarHeight();
            sceneScale = SceneScale(size.Width, size.Height);
            // Mesma proporcao da janela: o Viewbox aplica exatamente sceneScale, sem faixas nas laterais.
            var scene = MotionPart("Scene");
            scene.Width = size.Width / sceneScale; scene.Height = size.Height / sceneScale;
            sceneEdge = SceneEdge(scene.Width, scene.Height);
            MotionPart("SafeArea").Margin = new Thickness(sceneEdge);
            FitPanelBody();
        }

        // Altura maxima do corpo com a barra no limite da area segura. O cromo (titulo, rodape, bordas) vem da ultima medicao.
        void FitPanelBody()
        {
            var frame = MotionPart("PanelFrame"); var scroll = MotionPart("BodyScroll");
            panelBodyLimit = Math.Max(0, MotionPart("Scene").Height - 2 * sceneEdge - (frame.ActualHeight - scroll.ActualHeight));
            if (panelFill != null && Math.Abs(panelFill.MaxHeight - panelBodyLimit) > .5) panelFill.MaxHeight = panelBodyLimit;
        }
        // Sem sobra no painel: a viewport do corpo tem a altura do conteudo, salvo quando a barra chega ao limite e rola.
        bool PanelFitsContent()
        {
            var scroll = (ScrollViewer)MotionPart("BodyScroll");
            return Math.Abs(scroll.ViewportHeight - scroll.ExtentHeight) < 1 || scroll.ScrollableHeight > 0;
        }

        Rect SceneBox(FrameworkElement element)
        {
            return element.TransformToAncestor(MotionPart("Scene")).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }

        void SaveSceneRender(string name, int width, int height)
        {
            var scene = MotionPart("Scene");
            var brush = new VisualBrush(scene) { Viewbox = new Rect(0, 0, scene.Width, scene.Height), ViewboxUnits = BrushMappingMode.Absolute };
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Output, name + ".png"))) encoder.Save(file);
        }

#if XGB_VERIFY
        void VerifyScene()
        {
            // Telas em DIP: 1080p a 100/125/150%, 1440p, 4K a 100%, ultrawide, 16:10 portatil e retrato.
            var cases = new[] {
                new[] { 1920, 1080, 1.0 }, new[] { 1536, 864, 864 / SceneMinHeight }, new[] { 1280, 720, 720 / SceneMinHeight },
                new[] { 2560, 1440, 1.0 }, new[] { 3840, 2160, 1.5 }, new[] { 3440, 1440, 1.0 },
                new[] { 1280, 800, 800 / SceneMinHeight }, new[] { 1080, 1920, 1080 / SceneMinWidth } };
            var report = new StringBuilder("tela,escala,cena,margem\r\n");
            MotionFixtures(); SetPanel(0); Show();
            double homeHeight = -1;
            try
            {
                foreach (var c in cases)
                {
                    int width = (int)c[0], height = (int)c[1];
                    string label = width + "x" + height;
                    FitScene(new Size(width, height)); window.UpdateLayout();
                    var scene = MotionPart("Scene");
                    if (Math.Abs(sceneScale - c[2]) > .001) throw new Exception("Falha: escala da cena em " + label + ": " + sceneScale);
                    if (Math.Abs(scene.Width * sceneScale - width) > .5 || Math.Abs(scene.Height * sceneScale - height) > .5) throw new Exception("Falha: cena com faixas em " + label + ".");
                    if (sceneEdge < 24 || sceneEdge > 48) throw new Exception("Falha: margem segura em " + label + ".");
                    var bar = SceneBox(MotionPart("Sidebar")); var status = SceneBox(MotionPart("StatusArea"));
                    if (Math.Abs(bar.Width - 588) > .5 || Math.Abs(bar.Left - sceneEdge) > .5 || Math.Abs((bar.Top + bar.Bottom) / 2 - scene.Height / 2) > .5 ||
                        bar.Top < sceneEdge - .5 || bar.Bottom > scene.Height - sceneEdge + .5)
                        throw new Exception("Falha: barra fora do centro vertical ou da margem segura em " + label + ".");
                    if (!PanelFitsContent()) throw new Exception("Falha: espaco livre no pe da Home em " + label + ".");
                    if (Math.Abs(status.Right - (scene.Width - sceneEdge)) > .5 || Math.Abs(status.Top + ClockInkTop() - sceneEdge) > .5 || status.Left < bar.Right + 24)
                        throw new Exception("Falha: faixa de status fora do canto em " + label + ".");
                    // Cartoes mantem proporcao: telas pequenas usam a rolagem existente ao atingir a margem segura.
                    if (((ScrollViewer)MotionPart("BodyScroll")).ScrollableHeight > 0 && Math.Abs(bar.Height - (scene.Height - 2 * sceneEdge)) > .5)
                        throw new Exception("Falha: Home rola antes de ocupar a altura segura em " + label + ".");
                    // A altura vem do conteudo, limitada somente pela area segura disponivel.
                    if (homeHeight < 0) homeHeight = bar.Height;
                    else if (Math.Abs(bar.Height - Math.Min(homeHeight, scene.Height - 2 * sceneEdge)) > .5)
                        throw new Exception("Falha: altura da Home nao respeita conteudo/limite em " + label + ".");
                    SaveSceneRender("camada-" + label, width, height);
                    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:0.###},{2:0}x{3:0},{4}\r\n", label, sceneScale, scene.Width, scene.Height, sceneEdge);
                }
            }
            finally { FitScene(new Size(window.Width, window.Height)); window.UpdateLayout(); }
            // Paineis curtos ficam na altura do trilho; o Som com muitos apps para no limite e rola so o mixer.
            SetPanel(4); window.UpdateLayout();
            var railFrame = MotionPart("RailFrame");
            if (Math.Abs(SceneBox(MotionPart("Sidebar")).Height - railFrame.DesiredSize.Height) > .5) throw new Exception("Falha: painel curto deve ficar na altura do trilho.");
            var saved = audioSnapshot; audioSnapshot = ExampleAudio();
            try
            {
                SetPanel(6); window.UpdateLayout();
                if (!PanelFitsContent() || audioAppsScroll.ScrollableHeight > 0) throw new Exception("Falha: Som com poucos apps deve ter a altura do conteudo.");
                for (int i = 0; i < 20; i++) audioSnapshot.Sessions.Add(new AudioSessionState { Id = "fill" + i, AppIdentity = "fill" + i, DeviceId = "out", AppName = "Teste " + i, Level = 1 });
                BuildAudioPanel(true); window.UpdateLayout();
                var full = SceneBox(MotionPart("Sidebar"));
                if (Math.Abs(full.Bottom - (MotionPart("Scene").Height - sceneEdge)) > .5 || audioAppsScroll.ScrollableHeight <= 0 || ((ScrollViewer)MotionPart("BodyScroll")).ScrollableHeight > 0)
                    throw new Exception("Falha: Som cheio deve parar na margem e rolar so o mixer.");
                SaveSceneRender("camada-som-cheio", (int)window.Width, (int)window.Height);
            }
            finally { audioSnapshot = saved; SetPanel(0); window.UpdateLayout(); }
            File.WriteAllText(Path.Combine(Output, "camada-escalas.csv"), report.ToString(), Encoding.UTF8);
        }
#endif
    }
}
