// SVG fornecido pelo usuario (ps5.svg), incorporado sem redesenhar a geometria.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Shapes = System.Windows.Shapes;

namespace XGBP.Design
{
    static class DualSenseGlyph
    {
        static readonly Geometry Outline = Load();
        static readonly Geometry FittedOutline = Fit();
        const double Width = 30, Height = 20, EdgeWeight = .35;

        static Geometry Load()
        {
            using (var stream = typeof(DualSenseGlyph).Assembly.GetManifestResourceStream("DualSense.svg"))
            {
                if (stream == null) throw new InvalidOperationException("Recurso DualSense.svg ausente.");
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(stream, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "path" ||
                            reader.NamespaceURI != "http://www.w3.org/2000/svg") continue;
                        // SVG usa nonzero por padrao; WPF usa even-odd se o prefixo F1 for omitido.
                        var geometry = Geometry.Parse("F1 " + reader.GetAttribute("d"));
                        geometry.Freeze();
                        return geometry;
                    }
                }
            }
            throw new InvalidOperationException("Geometria do DualSense.svg ausente.");
        }

        static Geometry Fit()
        {
            // Mesmo ajuste aos limites do SVG, feito antes do traco para nao encolher o desenho.
            var bounds = Outline.Bounds;
            double scale = Math.Min(Width / bounds.Width, Height / bounds.Height);
            var geometry = Outline.Clone();
            geometry.Transform = new MatrixTransform(scale, 0, 0, scale, -bounds.X * scale,
                (Height - bounds.Height * scale) / 2 - bounds.Y * scale);
            geometry.Freeze();
            return geometry;
        }

        public static FrameworkElement Create(Brush foreground)
        {
            // ADR 0113: reforco sutil aprovado, no mesmo vetor, sem mudar a vaga de 30 x 20 DIP.
            var slot = new Canvas { Width = Width, Height = Height,
                VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
            slot.Children.Add(new Shapes.Path { Data = FittedOutline, Fill = foreground, Stroke = foreground,
                StrokeThickness = EdgeWeight, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.None,
                Width = Width, Height = Height });
            return slot;
        }
    }
}
