// Microbenchmark da arvore WPF com fixtures. Nao mede FPS do compositor ou de jogos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

static class PreviewBench
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static object preview;
    static Type type;
    static readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
    sealed class LayoutProbe : Decorator
    {
        public int Measures, Arranges;
        protected override Size MeasureOverride(Size constraint) { Measures++; return base.MeasureOverride(constraint); }
        protected override Size ArrangeOverride(Size size) { Arranges++; return base.ArrangeOverride(size); }
    }
    static readonly List<string> rows = new List<string> { "scenario,sample,elapsed_ms,allocated_bytes,layout_passes,cpu_ms,measure_passes,arrange_passes,set_panel_ms" };
    static object Call(string name, params object[] args)
    {
        MethodInfo method;
        if (!methods.TryGetValue(name, out method)) { method = type.GetMethod(name, Members); methods.Add(name, method); }
        return method.Invoke(preview, args);
    }
    static object Field(string name) { return type.GetField(name, Members).GetValue(preview); }
    static void Set(string name, object value) { type.GetField(name, Members).SetValue(preview, value); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Row(string scenario, int index, Stopwatch watch, long allocated, int layouts, double cpu, int measures = 0, int arranges = 0, double setPanelMs = 0)
    {
        rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F4},{3},{4},{5:F4},{6},{7},{8:F4}",
            scenario, index, watch.Elapsed.TotalMilliseconds, AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated, layouts, cpu, measures, arranges, setPanelMs));
    }
    static double Cpu() { using (var process = Process.GetCurrentProcess()) return process.TotalProcessorTime.TotalMilliseconds; }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length != 2 || !File.Exists(args[0])) return 2;
        bool created;
        using (var guard = new System.Threading.Mutex(true, "Local\\XGBPDesignVerify", out created))
        {
            if (!created) return 3;
            try
            {
                AppDomain.MonitoringIsEnabled = true;
                type = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("XGBP.Design.Preview", true);
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                preview = Activator.CreateInstance(type, Members, null, new object[] { app, true, false }, CultureInfo.InvariantCulture);
                Call("Start"); Call("MotionFixtures"); Set("motionTesting", true); Set("motionSystemOverride", (bool?)false);
                foreach (var session in (System.Collections.IEnumerable)Field("mediaSessions"))
                    session.GetType().GetField("Status").SetValue(session, "paused");
                var window = (Window)Field("window");
                var panelFrame = (Border)Call("MotionPart", "PanelFrame"); var content = panelFrame.Child;
                panelFrame.Child = null; var probe = new LayoutProbe { Child = content }; panelFrame.Child = probe;
                window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
                Call("SetPanel", 0); Call("Show"); Pump(400);
                for (int i = 0; i < 4; i++) { Call("SetPanel", i % 2 == 0 ? 4 : 0); Pump(230); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                int layouts = 0; EventHandler onLayout = delegate { layouts++; };
                window.LayoutUpdated += onLayout;
                for (int i = 0; i < 20; i++)
                {
                    int startLayouts = layouts, measures = probe.Measures, arranges = probe.Arranges;
                    long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    double cpu = Cpu(); var watch = Stopwatch.StartNew();
                    Call("SetPanel", i % 2 == 0 ? 4 : 0); double setPanelMs = watch.Elapsed.TotalMilliseconds;
                    Pump(230); watch.Stop();
                    Row("panel_transition", i, watch, allocated, layouts - startLayouts, Cpu() - cpu, probe.Measures - measures, probe.Arranges - arranges, setPanelMs);
                }
                window.LayoutUpdated -= onLayout;
                Call("SetPanel", 0); Pump(300);
                var panel = (FrameworkElement)Call("MotionPart", "PanelFrame");
                var overlay = (Canvas)Call("MotionPart", "MotionOverlay");
                for (int i = -2; i < 20; i++)
                {
                    Call("StopMotion");
                    long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    double cpu = Cpu(); var watch = Stopwatch.StartNew();
                    Call("MotionExit", panel, 167); watch.Stop();
                    if (i >= 0) Row("exit_capture", i, watch, allocated, 0, Cpu() - cpu);
                    if (i == 0 && overlay.Children.Count > 0)
                    {
                        var original = new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth), (int)Math.Ceiling(panel.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                        original.Render(panel); Save(original, Path.Combine(args[1], "source.png"));
                        var image = overlay.Children[overlay.Children.Count - 1] as Image;
                        if (image != null)
                        {
                            var drawing = new DrawingVisual();
                            using (var dc = drawing.RenderOpen()) dc.DrawImage(image.Source, new Rect(0, 0, panel.ActualWidth, panel.ActualHeight));
                            var copy = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                            copy.Render(drawing); Save(copy, Path.Combine(args[1], "exit-copy.png"));
                        }
                    }
                }
                Call("StopMotion");
                for (int i = 0; i < 8; i++)
                {
                    long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    double cpu = Cpu(); var watch = Stopwatch.StartNew();
                    for (int n = 0; n < 10000; n++) Call("MediaSelected");
                    watch.Stop(); Row("media_selection_10000", i, watch, allocated, 0, Cpu() - cpu);
                }
                return 0;
            }
            catch (Exception ex) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "error.txt"), ex.ToString()); return 1; }
            finally
            {
                if (preview != null) { try { ((IDisposable)preview).Dispose(); } catch (Exception ex) { Console.Error.WriteLine(ex); } }
                Directory.CreateDirectory(args[1]); File.WriteAllLines(Path.Combine(args[1], "samples.csv"), rows, Encoding.UTF8);
            }
        }
    }
    static void Save(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
    }
}
