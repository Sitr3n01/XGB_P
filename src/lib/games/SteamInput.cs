// Layout do Steam Input pelo protocolo da propria Steam (steam://forceinputappid). Nao escreve arquivos nem configuracoes.
// O protocolo nao e documentado pela Valve; o comportamento foi conferido no console_log desta maquina (ADR 0041).
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace XGBP.Games
{
    public static class SteamInput
    {
        // Configuracao de desktop do Steam Input (k_nGameIDControllerConfigs_Desktop).
        public const string ForceDesktop = "steam://forceinputappid/413080";
        // Libera o layout; a Steam so volta a escolher pelo foco na proxima troca de janela.
        public const string Release = "steam://forceinputappid/0";

        // Nunca despachar sem a Steam aberta: o protocolo iniciaria o cliente.
        public static bool Running()
        {
            var processes = Process.GetProcessesByName("steam");
            foreach (var process in processes) process.Dispose();
            return processes.Length > 0;
        }
        public static string ConsoleLog()
        {
            string root = null;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) if (key != null) root = key.GetValue("SteamPath") as string;
            return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, "logs", "console_log.txt");
        }
        // Linha que a Steam grava ao executar o protocolo, com as aspas que delimitam o endereco inteiro.
        public static string Executed(string uri) { return "ExecuteSteamURL: \"" + uri + "\""; }

        // Teclado virtual (ADR 0052): fixar o layout de um app conhecido, como o do Big Picture (769) ou o de um jogo.
        public static string Force(string appId) { return "steam://forceinputappid/" + appId; }
        public const string DesktopAppId = "413080", GuideAppId = "443510", ClientUiAppId = "769";
        public static string ControllerLog()
        {
            string console = ConsoleLog();
            return console == null ? null : Path.Combine(Path.GetDirectoryName(console), "controller_ui.txt");
        }
        // Ultimo layout escolhido pelo foco ("OnFocusWindowChanged to window type: ..., AppID N"), ignorando o do Guia,
        // que a Steam carrega so enquanto o PS esta apertado, e as linhas forcadas por protocolo.
        public static string LastLayout(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            const string marker = "OnFocusWindowChanged to window type:";
            string found = null;
            foreach (string line in text.Split('\n'))
            {
                int at = line.IndexOf(marker, StringComparison.Ordinal);
                if (at < 0) continue;
                int app = line.LastIndexOf("AppID ", StringComparison.Ordinal);
                if (app < at) continue;
                string id = line.Substring(app + 6).Trim();
                int digits = 0; while (digits < id.Length && char.IsDigit(id[digits])) digits++;
                id = id.Substring(0, digits);
                if (id.Length > 0 && id != GuideAppId) found = id;
            }
            return found;
        }
        // Final do controller_ui.txt (ultimos 64 KB), lido sem bloquear a Steam.
        public static string ControllerTail()
        {
            string path = ControllerLog();
            if (path == null) return null;
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long count = Math.Min(file.Length, 64 * 1024);
                    var buffer = new byte[count]; file.Seek(file.Length - count, SeekOrigin.Begin);
                    int read = 0; while (read < count) { int n = file.Read(buffer, read, (int)count - read); if (n <= 0) break; read += n; }
                    return Encoding.UTF8.GetString(buffer, 0, read);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }

    // Le so o que a Steam acrescentou ao console_log depois da criacao, para confirmar a ordem dos protocolos.
    public sealed class SteamConsoleTail
    {
        const int Limit = 256 * 1024;
        readonly string path;
        long start;

        public SteamConsoleTail(string path)
        {
            this.path = path;
            try { start = path != null && File.Exists(path) ? new FileInfo(path).Length : 0; } catch (IOException) { start = 0; } catch (UnauthorizedAccessException) { start = 0; }
        }
        public string ReadNew()
        {
            if (path == null) return null;
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (file.Length < start) start = 0; // Steam recriou o arquivo
                    long count = Math.Min(file.Length - start, Limit);
                    if (count <= 0) return "";
                    var buffer = new byte[count]; file.Seek(start, SeekOrigin.Begin);
                    int read = 0; while (read < count) { int n = file.Read(buffer, read, (int)count - read); if (n <= 0) break; read += n; }
                    return Encoding.UTF8.GetString(buffer, 0, read);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
