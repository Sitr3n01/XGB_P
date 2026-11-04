// Destaque do Windows (ADR 0055). Somente leitura; superficies e cores semanticas continuam locais.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Windows.UI.ViewManagement;

namespace XGBP.Design
{
    // Fonte explicita das ligacoes: nao depende do contexto de heranca de pinceis compartilhados.
    public sealed class AccentState : DependencyObject
    {
        public static readonly DependencyProperty FillProperty = DependencyProperty.Register("Fill", typeof(Color), typeof(AccentState), new PropertyMetadata(Color.FromRgb(0x10, 0x7C, 0x10)));
        public static readonly DependencyProperty FocusProperty = DependencyProperty.Register("Focus", typeof(Color), typeof(AccentState), new PropertyMetadata(Color.FromRgb(0x32, 0xA8, 0x32)));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(Color), typeof(AccentState), new PropertyMetadata(Color.FromRgb(0x6C, 0xCB, 0x5F)));
        public Color Fill { get { return (Color)GetValue(FillProperty); } set { SetValue(FillProperty, value); } }
        public Color Focus { get { return (Color)GetValue(FocusProperty); } set { SetValue(FocusProperty, value); } }
        public Color Text { get { return (Color)GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
    }

    sealed class AccentPalette
    {
        public Color Source, Fill, Focus, Text;
        public const double GraphicTarget = 3.1, TextTarget = 4.6; // margem sobre os minimos 3 e 4,5
        public static readonly Color Fallback = Color.FromRgb(0x10, 0x7C, 0x10);

        public static double Luminance(Color color)
        { return .2126 * Linear(color.R / 255.0) + .7152 * Linear(color.G / 255.0) + .0722 * Linear(color.B / 255.0); }
        static double Linear(double value) { return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        static byte Srgb(double value)
        { return (byte)Math.Max(0, Math.Min(255, Math.Round(255 * (value <= .0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - .055)))); }
        public static double Contrast(Color a, Color b)
        { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
        public static Color OverWhite(Color value)
        {
            double alpha = value.A / 255.0;
            return Color.FromRgb((byte)Math.Round(value.R * alpha + 255 * (1 - alpha)),
                (byte)Math.Round(value.G * alpha + 255 * (1 - alpha)), (byte)Math.Round(value.B * alpha + 255 * (1 - alpha)));
        }
        public static double Minimum(Color value, Color[] backgrounds)
        {
            double minimum = double.MaxValue;
            foreach (var background in backgrounds) minimum = Math.Min(minimum, Contrast(value, background));
            return minimum;
        }
        static Color Lighter(Color value, double amount)
        {
            return Color.FromRgb(Srgb(Linear(value.R / 255.0) * (1 - amount) + amount),
                Srgb(Linear(value.G / 255.0) * (1 - amount) + amount), Srgb(Linear(value.B / 255.0) * (1 - amount) + amount));
        }
        // Menor mistura linear com branco que passa. Compara os bytes finais, sem arredondar a razao.
        static Color Legible(Color value, double target, Color[] backgrounds)
        {
            if (Minimum(value, backgrounds) >= target) return value;
            if (Minimum(Colors.White, backgrounds) < target) throw new InvalidOperationException("Superficie fora da paleta escura de destaque.");
            double low = 0, high = 1;
            for (int n = 0; n < 28; n++)
            {
                double mid = (low + high) / 2;
                if (Minimum(Lighter(value, mid), backgrounds) >= target) high = mid; else low = mid;
            }
            return Lighter(value, high);
        }
        public static AccentPalette Create(Color value, Color[] surfaces, Color meter)
        {
            value.A = 255;
            var fillBackgrounds = new Color[surfaces.Length + 1]; surfaces.CopyTo(fillBackgrounds, 0); fillBackgrounds[surfaces.Length] = meter;
            return new AccentPalette { Source = value, Fill = Legible(value, GraphicTarget, fillBackgrounds),
                Focus = Legible(value, GraphicTarget, surfaces), Text = Legible(value, TextTarget, surfaces) };
        }
        public static string Hex(Color value) { return "#" + value.R.ToString("X2") + value.G.ToString("X2") + value.B.ToString("X2"); }
    }

    sealed partial class Preview
    {
        UISettings accentSettings;
        bool accentSubscribed;
        Func<Color?> accentRead;
        AccentPalette accentPalette;
        bool accentReadFailed;
        int accentRevision;
        Color[] AccentSurfaces()
        {
            string[] keys = { "SurfaceRail", "SurfacePanel", "SurfaceCard", "SurfaceHover", "SurfaceSelected", "SurfacePressed", "SurfaceFlyout", "DescriptionSurface" };
            var values = new Color[keys.Length];
            for (int n = 0; n < keys.Length; n++) values[n] = AccentPalette.OverWhite(Brush(keys[n]).Color);
            return values;
        }
        Color? ReadWindowsAccent()
        {
            var settings = accentSettings ?? new UISettings();
            var color = settings.GetColorValue(UIColorType.Accent);
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        void StartAccentTheme()
        {
            accentRead = verify ? (Func<Color?>)delegate { return AccentPalette.Fallback; } : ReadWindowsAccent;
            RefreshAccentTheme();
        }
        void EnsureAccentSubscription(bool diagnostic = false)
        {
            if ((!verify || diagnostic) && !accentSubscribed)
            {
                try {
                    accentSettings = new UISettings();
                    accentSettings.ColorValuesChanged += WindowsAccentChanged; accentSubscribed = true;
                }
                catch (Exception ex) { WriteLog("destaque: notificacao indisponivel | " + ex.Message); }
            }
        }
        void WindowsAccentChanged(UISettings sender, object args)
        {
            // O evento WinRT pode chegar em outra thread; leitura e recursos WPF ficam na UI.
            Dispatch(RefreshAccentTheme);
        }
        void RefreshAccentTheme()
        {
            if (disposed || accentRead == null) return;
            EnsureAccentSubscription();
            Color? source = null;
            try { source = accentRead(); } catch (Exception) { }
            bool failed = !source.HasValue || source.Value.A == 0;
            if (failed && !accentReadFailed) WriteLog("destaque: leitura indisponivel; mantendo a ultima cor ou verde de reserva");
            accentReadFailed = failed;
            if (failed && accentPalette != null) return;
            Color color = failed ? AccentPalette.Fallback : source.Value; color.A = 255;
            if (accentPalette != null && accentPalette.Source == color) return;
            var palette = AccentPalette.Create(color, AccentSurfaces(), Brush("MeterTrack").Color);
            // Nomes Green historicos. Ligacoes a uma fonte explicita atualizam estilos e C# sem reconstruir a UI.
            var state = (AccentState)window.FindResource("AccentState");
            state.Fill = palette.Fill; state.Focus = palette.Focus; state.Text = palette.Text;
            accentPalette = palette; accentRevision++;
            WriteLog("destaque: " + (verify ? "fixture" : failed ? "reserva" : "Windows") + " | base=" + AccentPalette.Hex(color) +
                "; preenchimento=" + AccentPalette.Hex(palette.Fill) + "; foco=" + AccentPalette.Hex(palette.Focus) + "; texto=" + AccentPalette.Hex(palette.Text));
        }
        bool StopAccentTheme()
        {
            if (accentSettings != null && accentSubscribed)
                try { accentSettings.ColorValuesChanged -= WindowsAccentChanged; }
                catch (Exception ex) { WriteLog("destaque: falha ao remover observador | " + ex.Message); return false; }
            accentSubscribed = false; accentSettings = null; accentRead = null;
            return true;
        }
#if XGB_VERIFY
        public void VerifyAccentLive()
        {
            EnsureAccentSubscription(true);
            if (!accentSubscribed) throw new InvalidOperationException("Observador do destaque do Windows indisponivel.");
            Color? source = ReadWindowsAccent();
            if (!source.HasValue || source.Value.A == 0) throw new InvalidOperationException("Cor de destaque do Windows indisponivel.");
            accentRead = delegate { return source; }; RefreshAccentTheme();
            var surfaces = AccentSurfaces();
            var output = "API publica UISettings.GetColorValue(Accent), somente leitura; sem mudar tema/configuracoes.\r\n" +
                "Base=" + AccentPalette.Hex(accentPalette.Source) + "; preenchimento=" + AccentPalette.Hex(accentPalette.Fill) +
                "; foco=" + AccentPalette.Hex(accentPalette.Focus) + "; texto=" + AccentPalette.Hex(accentPalette.Text) + "\r\n" +
                "Contraste minimo: preenchimento/trilha=" + AccentPalette.Contrast(accentPalette.Fill, Brush("MeterTrack").Color).ToString("F3", CultureInfo.InvariantCulture) +
                "; foco/superficies=" + AccentPalette.Minimum(accentPalette.Focus, surfaces).ToString("F3", CultureInfo.InvariantCulture) +
                "; texto/superficies=" + AccentPalette.Minimum(accentPalette.Text, surfaces).ToString("F3", CultureInfo.InvariantCulture) + ".\r\n";
            if (!StopAccentTheme()) throw new InvalidOperationException("A remocao do observador do destaque falhou.");
            output += "API de registro/remocao retornou sem erro; sem troca real de cor ou prova externa de descarte.\r\n";
            File.WriteAllText(Path.Combine(Output, "destaque-real.txt"), output, Encoding.UTF8);
        }
#endif
    }
}
