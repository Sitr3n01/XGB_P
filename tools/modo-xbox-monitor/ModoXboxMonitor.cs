// Monitor do modo Xbox (spike S3): registra suporte, estado inicial e cada mudanca do modo Xbox,
// dizendo se veio da notificacao oficial ou da consulta periodica. Somente leitura: nao liga nem desliga o modo.
//
// Uso: ModoXboxMonitor.exe [minutos]   (padrao 30; 0 = so mostra o estado atual e sai)
// Log: %USERPROFILE%\GameBarProbe\logs\modo-xbox.log
// Compilado com o csc do .NET Framework 4.x (C# 5).

using System;
using System.IO;
using System.Text;
using System.Threading;
using XGBP.Gaming;

namespace XGBP.Tools
{
    static class ModoXboxMonitorCli
    {
        static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GameBarProbe", "logs", "modo-xbox.log");
        static readonly object LogLock = new object();

        static int Main(string[] args)
        {
            int minutes = 30;
            if (args.Length > 0) int.TryParse(args[0], out minutes);
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
            Log(string.Format("monitor iniciado ({0} min) | suportado={1} ativo={2} podeAlternar={3}", minutes,
                Text(GamingExperience.Supported()), Text(GamingExperience.Active()), Text(GamingExperience.CanSet())));
            if (minutes <= 0) return 0;

            using (var watcher = new ModeWatcher())
            {
                watcher.Log += Log;
                watcher.Changed += delegate (bool active, string source)
                {
                    Log("MODO XBOX " + (active ? "ATIVO" : "DESLIGADO") + " (percebido por " + source + ")");
                };
                watcher.Start();
                Thread.Sleep(TimeSpan.FromMinutes(minutes));
            }
            Log("monitor encerrado");
            return 0;
        }

        static string Text(bool? b) { return b.HasValue ? (b.Value ? "sim" : "nao") : "indisponivel"; }

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
