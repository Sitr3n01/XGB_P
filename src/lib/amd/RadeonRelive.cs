// Radeon ReLive do AMD Software: estado pelo registro, comandos pelo RSServCmd.exe instalado e capturas da pasta de saida.
// Interface nao documentada, levantada por REA (docs/evidencias/2026-10-29-amd-investigacao, ADR 0081).
// Sem atalhos simulados, injecao ou escrita nas configuracoes da AMD.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace XGBP.Amd
{
    public sealed class ReliveState
    {
        public bool Installed, Running, Enabled, InstantReplay, Desktop, Microphone, Camera, InstantGif, InGameReplay;
        public int ReplaySeconds;
        public string OutputFolder, Version, InstallFolder;
    }
    public sealed class ReliveCapture
    {
        public string Path, Game;
        public bool Video, Replay;
        public DateTime Modified, Created;
        public long Bytes;
        public TimeSpan? Length;
    }
    public static class RadeonRelive
    {
        public const string SaveReplay = "save_ir", Screenshot = "still_cap", StartRecording = "capture_to_file_start", StopRecording = "capture_to_file_stop",
            ToggleReplay = "toggle_ir", ToggleDesktop = "toggle_desktop", ToggleMicrophone = "toggle_mic", ToggleCamera = "toggle_camera",
            SaveGif = "save_instant_gif", SaveInGameReplay = "save_ingame_replay",
            // Metricas da AMD: so logica. A interface mostra o painel Desempenho, que ja cobre o mesmo (pedido do usuario).
            ToggleMetricsOverlay = "toggle_in_content_rs_perf_from_rsx", ToggleMetricsLog = "toggle_rs_perf_rec";
        // Somente comandos de acao; ajustes persistentes (set_*), transmissao, saida e diagnostico ficam de fora.
        static readonly string[] Allowed = { SaveReplay, Screenshot, StartRecording, StopRecording, ToggleReplay, ToggleDesktop, ToggleMicrophone,
            ToggleCamera, SaveGif, SaveInGameReplay, ToggleMetricsOverlay, ToggleMetricsLog };
        static readonly string[] CaptureExtensions = { ".mp4", ".mkv", ".mov", ".png", ".jpg", ".jpeg", ".gif" };
        const string DvrKey = @"Software\AMD\DVR";

        public static bool IsAllowed(string command) { return Array.IndexOf(Allowed, command) >= 0; }
        public static string InstallFolder()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AMD\CN"))
            {
                string folder = key == null ? null : key.GetValue("InstallDir") as string;
                return string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder) ? null : folder;
            }
        }
        public static string CommandTool(string installFolder)
        {
            if (installFolder == null) return null;
            string tool = Path.Combine(installFolder, "RSServCmd.exe");
            return File.Exists(tool) ? tool : null;
        }
        // Janela de comandos do DVR (AMDRSServ.exe); sem ela o ReLive esta desligado ou o AMD Software nao iniciou.
        public static bool DvrRunning() { return FindWindow("DVREventWindowClass", null) != IntPtr.Zero; }

        public static ReliveState Read()
        {
            string install = InstallFolder();
            var state = new ReliveState { InstallFolder = install, Installed = CommandTool(install) != null, Running = DvrRunning() };
            using (var user = Registry.CurrentUser.OpenSubKey(DvrKey))
            using (var machine = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AMD\DVR"))
            {
                Func<string, int, int> dword = delegate(string name, int fallback) {
                    object value = user == null ? null : user.GetValue(name);
                    if (value == null && machine != null) value = machine.GetValue(name);
                    return value is int ? (int)value : fallback;
                };
                state.Enabled = dword("DvrEnabled", 0) != 0;
                state.InstantReplay = dword("InstantReplayEnabled", 0) != 0;
                state.ReplaySeconds = Math.Max(0, dword("InstantReplayDuration", 0));
                state.Desktop = dword("DesktopRecordingEnabled", 0) != 0;
                state.Microphone = dword("AudioInputCaptureEnabled", 0) != 0;
                state.Camera = dword("CameraEnabled", 0) != 0;
                state.InstantGif = dword("InstantGifEnabled", 0) != 0;
                state.InGameReplay = dword("InGameReplayEnabled", 0) != 0;
                string output = user == null ? null : user.GetValue("CaptureFileOutput") as string;
                if (string.IsNullOrWhiteSpace(output) && machine != null) output = machine.GetValue("CaptureFileOutput") as string;
                // Vazio no registro: o ReLive usa Videos\Radeon ReLive.
                state.OutputFolder = string.IsNullOrWhiteSpace(output) ?
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Radeon ReLive") : output;
            }
            using (var cn = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AMD\CN")) state.Version = cn == null ? null : cn.GetValue("CNVersion") as string;
            return state;
        }

        // RSServCmd.exe <comando>: o proprio programa da AMD procura a janela do DVR e entrega a mensagem da versao instalada.
        public static void Send(string command)
        {
            if (!IsAllowed(command)) throw new ArgumentException("Comando não permitido: " + command);
            string tool = CommandTool(InstallFolder());
            if (tool == null) throw new InvalidOperationException("O AMD Software não foi encontrado neste PC.");
            if (!DvrRunning()) throw new InvalidOperationException("O Radeon ReLive não está ativo. Ative a gravação no AMD Software.");
            var info = new ProcessStartInfo(tool, command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(tool), StandardOutputEncoding = Encoding.UTF8 };
            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(5000)) throw new TimeoutException("O AMD Software não respondeu ao comando.");
                // O RSServCmd sai com 1 tambem no sucesso; so a mensagem de entrada invalida indica recusa.
                if (output.IndexOf("Input not supported", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException("Esta versão do AMD Software não aceita o comando " + command + ".");
            }
        }

        // Mais recentes primeiro, na pasta de saida e nas subpastas por jogo (um nivel, como o ReLive organiza).
        public static List<ReliveCapture> Recent(string folder, int count)
        {
            var found = new List<FileInfo>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return new List<ReliveCapture>();
            var root = new DirectoryInfo(folder);
            Collect(root, found);
            foreach (var game in root.GetDirectories()) { try { Collect(game, found); } catch (UnauthorizedAccessException) { } catch (IOException) { } }
            found.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            var result = new List<ReliveCapture>();
            foreach (var file in found)
            {
                if (result.Count >= count) break;
                string ext = file.Extension.ToLowerInvariant(); bool video = ext == ".mp4" || ext == ".mkv" || ext == ".mov";
                var capture = new ReliveCapture { Path = file.FullName, Video = video, Modified = file.LastWriteTime, Created = file.CreationTime, Bytes = file.Length,
                    Replay = file.Name.IndexOf("_replay_", StringComparison.OrdinalIgnoreCase) >= 0,
                    Game = string.Equals(file.DirectoryName.TrimEnd('\\'), root.FullName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? "Área de trabalho" : file.Directory.Name };
                if (ext == ".mp4" || ext == ".mov") try { capture.Length = Mp4Duration(file.FullName); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                result.Add(capture);
            }
            return result;
        }
        static void Collect(DirectoryInfo folder, List<FileInfo> found)
        {
            foreach (var file in folder.GetFiles())
                if (Array.IndexOf(CaptureExtensions, file.Extension.ToLowerInvariant()) >= 0) found.Add(file);
        }

        public static TimeSpan? Mp4Duration(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) return Mp4Duration(file);
        }
        // Duracao do cabecalho mvhd dentro de moov. Gravacao em andamento ainda nao tem moov: null.
        public static TimeSpan? Mp4Duration(Stream stream)
        {
            long end = stream.Length, position = 0; var buffer = new byte[16];
            while (position + 8 <= end)
            {
                long size; string type; int header;
                if (!BoxHeader(stream, position, end, buffer, out size, out type, out header)) return null;
                if (type == "moov")
                {
                    long child = position + header, stop = Math.Min(end, position + size);
                    while (child + 8 <= stop)
                    {
                        long childSize; string childType; int childHeader;
                        if (!BoxHeader(stream, child, stop, buffer, out childSize, out childType, out childHeader)) return null;
                        if (childType == "mvhd")
                        {
                            stream.Position = child + childHeader;
                            if (Read(stream, buffer, 4) < 4) return null;
                            bool wide = buffer[0] == 1;
                            stream.Position += wide ? 16 : 8;
                            if (Read(stream, buffer, wide ? 12 : 8) < (wide ? 12 : 8)) return null;
                            long scale = BigEndian(buffer, 0, 4), duration = BigEndian(buffer, 4, wide ? 8 : 4);
                            return scale <= 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(duration / (double)scale);
                        }
                        child += childSize;
                    }
                    return null;
                }
                position += size;
            }
            return null;
        }
        static bool BoxHeader(Stream stream, long position, long end, byte[] buffer, out long size, out string type, out int header)
        {
            size = 0; type = null; header = 8;
            stream.Position = position;
            if (Read(stream, buffer, 8) < 8) return false;
            size = BigEndian(buffer, 0, 4); type = Encoding.ASCII.GetString(buffer, 4, 4);
            if (size == 1) { if (Read(stream, buffer, 8) < 8) return false; size = BigEndian(buffer, 0, 8); header = 16; }
            else if (size == 0) size = end - position;
            return size >= header;
        }
        static int Read(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count) { int n = stream.Read(buffer, total, count - total); if (n <= 0) break; total += n; }
            return total;
        }
        static long BigEndian(byte[] buffer, int offset, int count)
        {
            long value = 0;
            for (int i = 0; i < count; i++) value = (value << 8) | buffer[offset + i];
            return value;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string title);
    }
}
