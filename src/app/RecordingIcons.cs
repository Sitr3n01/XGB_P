// Icones vetoriais de captura: grade de 24 DIP e traco uniforme, sem depender de glifos da fonte.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        FrameworkElement RecordingIcon(string kind, double size)
        {
            var canvas = new Canvas { Width = 24, Height = 24, IsHitTestVisible = false };
            Brush ink = Brush(kind == "microphone-muted" ? "Critical" : "TextPrimary");
            switch (kind)
            {
                case "replay":
                    // Arco aberto com seta tangente, sem simbolos sobrepostos no centro.
                    RecordingStroke(canvas, "M5,6.5 A8,8 0 1 1 4.6,16.8 M5,2.5 L5,6.5 9,6.5", ink);
                    break;
                case "photo":
                    RecordingStroke(canvas, "M3,7 L7,7 9,4 15,4 17,7 21,7 Q22,7 22,8 L22,19 Q22,20 21,20 L3,20 Q2,20 2,19 L2,8 Q2,7 3,7 Z", ink);
                    RecordingStroke(canvas, "M16,13 A4,4 0 1 1 8,13 A4,4 0 1 1 16,13", ink);
                    break;
                case "record":
                    RecordingStroke(canvas, "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12", Brush("CloseRed"));
                    RecordingFill(canvas, "M17,12 A5,5 0 1 1 7,12 A5,5 0 1 1 17,12", Brush("CloseRed"));
                    break;
                case "stop":
                    var stop = new Rectangle { Width = 16, Height = 16, RadiusX = 2, RadiusY = 2, Fill = Brush("CloseRed") };
                    Canvas.SetLeft(stop, 4); Canvas.SetTop(stop, 4); canvas.Children.Add(stop);
                    break;
                case "gif":
                    RecordingStroke(canvas, "M3,4 L21,4 Q22,4 22,5 L22,19 Q22,20 21,20 L3,20 Q2,20 2,19 L2,5 Q2,4 3,4 Z M6,4 L6,20 M18,4 L18,20 M2,9 L6,9 M2,15 L6,15 M18,9 L22,9 M18,15 L22,15", ink);
                    RecordingFill(canvas, "M10,9 L15,12 10,15 Z", ink);
                    break;
                case "ingame":
                    RecordingStroke(canvas, "M3,3 L21,3 Q22,3 22,4 L22,16 Q22,17 21,17 L3,17 Q2,17 2,16 L2,4 Q2,3 3,3 Z M12,17 L12,21 M8,21 L16,21", ink);
                    RecordingFill(canvas, "M10,6.5 L15.5,10 10,13.5 Z", ink);
                    break;
                case "microphone":
                case "microphone-muted":
                    RecordingStroke(canvas, "M9,5 A3,3 0 0 1 15,5 L15,12 A3,3 0 0 1 9,12 Z M6,10 L6,12 A6,6 0 0 0 18,12 L18,10 M12,18 L12,22 M9,22 L15,22", ink);
                    if (kind == "microphone-muted") RecordingStroke(canvas, "M3,3 L21,21", ink);
                    break;
                default: // Camera de video: painel Gravacao e selo dos videos na galeria.
                    RecordingStroke(canvas, "M3,5 L14,5 Q16,5 16,7 L16,17 Q16,19 14,19 L3,19 Q1,19 1,17 L1,7 Q1,5 3,5 Z M16,9 L22,6 22,18 16,15", ink);
                    break;
            }
            return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
        }
        static void RecordingStroke(Canvas canvas, string data, Brush ink)
        {
            canvas.Children.Add(new Path { Data = Geometry.Parse(data), Stroke = ink, StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
        }
        static void RecordingFill(Canvas canvas, string data, Brush ink) { canvas.Children.Add(new Path { Data = Geometry.Parse(data), Fill = ink }); }
    }
}
