// Leitor de diagnostico do DualSense (somente leitura), sobre a biblioteca lib\dualsense\DualSenseHid.cs.
// Registra dispositivos, acesso (HidHide), taxa de relatorios, apertos/solturas e gestos de cada dispositivo,
// SEM deduplicar: fisico e virtual do DSX aparecem separados, para diagnostico.
//
// Uso: DualSenseReader.exe [minutos]   (padrao 10; 0 = so lista os dispositivos e sai)
// Log: %USERPROFILE%\GameBarProbe\logs\dualsense.log
// Compilado com o csc do .NET Framework 4.x (C# 5).

using System;
using System.IO;
using System.Text;
using System.Threading;
using XGBP.Input;

namespace XGBP.Tools
{
    static class DualSenseReaderCli
    {
        static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GameBarProbe", "logs", "dualsense.log");
        static readonly object LogLock = new object();

        static int Main(string[] args)
        {
            int minutes = 10;
            if (args.Length > 0) int.TryParse(args[0], out minutes);
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
            Log("leitor iniciado (" + minutes + " min)");

            var monitor = new DualSenseMonitor();
            monitor.Deduplicate = false;
            monitor.Log += Log;
            monitor.ButtonChanged += delegate (ButtonEvent e) { Log(Format(e)); };

            if (minutes <= 0)
            {
                monitor.Scan(false);
            }
            else
            {
                monitor.Start();
                Thread.Sleep(TimeSpan.FromMinutes(minutes));
                monitor.Stop();
            }
            Log("leitor encerrado");
            return 0;
        }

        static string Format(ButtonEvent e)
        {
            string text;
            if (e.Gesture == "SEGURADO") text = e.Button + " segurado (" + e.DurationMs + " ms, ainda apertado)";
            else if (e.Down) text = e.Button + " apertado";
            else text = e.Button + " solto (" + e.DurationMs + " ms)";
            if (e.Gesture != null) text += "  => GESTO " + e.Button + " " + e.Gesture;
            return e.Device + " | " + text;
        }

        static void Log(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + text;
            lock (LogLock)
            {
                Console.WriteLine(line);
                try { File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
        }
    }
}
