// Canal observado no steam.exe local: Home/Biblioteca e, por entrada separada, desktop/liberacao da barra.
// Nao cria chaves, altera permissoes ou executa comandos arbitrarios. Cliente desconhecido usa o protocolo.
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed class SteamNavigationBusyException : IOException
    { public SteamNavigationBusyException() : base("A Steam ainda tem outro comando pendente.") { } }

    sealed class SteamDeliveryUncertainException : IOException
    { public SteamDeliveryUncertainException(Exception cause) : base("Entrega à Steam incerta. Confira o destino antes de tentar novamente.", cause) { } }

    static class SteamFastNavigation
    {
        const string KnownSha256 = "F8A4CA44008C516EEBECAFAA4BC26772B2A6EB0FEF8034BCE56A3311E21AB705";
        const string SteamKey = @"SOFTWARE\Valve\Steam", PendingValue = "TempAppCmdLine";
        static readonly object Sync = new object();
        static string verifiedPath;
        static long verifiedLength, verifiedStamp;
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint pid);

        public static bool Supported(string uri)
        { return uri == "steam://open/library" || uri == "steam://open/library/collection/all-games"; }

        public static bool TrySend(string uri)
        {
            if (!Supported(uri)) return false;
            return Send(uri, true);
        }
        // Transporte exclusivo da barra. O teclado e as paginas conservam suas rotas.
        public static bool InputSupported(string uri)
        { return uri == "steam://forceinputappid/413080" || uri == "steam://forceinputappid/0"; }
        public static bool TrySendInput(string uri)
        {
            if (!InputSupported(uri)) return false;
            return Send(uri, false);
        }
        static bool Send(string uri, bool grantForeground)
        {
            lock (Sync)
            {
                bool attemptedWrite = false;
                try
                {
                    using (var root64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var root32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (var key64 = root64.OpenSubKey(SteamKey, true))
                    using (var key32 = root32.OpenSubKey(SteamKey, true))
                    {
                        if (key64 == null || key32 == null) return false;
                        uint pid64, pid32;
                        if (!uint.TryParse(Convert.ToString(key64.GetValue("SteamPID")), out pid64) || pid64 == 0 ||
                            !uint.TryParse(Convert.ToString(key32.GetValue("SteamPID")), out pid32) || pid32 != pid64) return false;
                        string executable = DesktopWindows.ProcessPath(pid64);
                        if (!KnownClient(executable)) return false;
                        using (var process = Process.GetProcessById((int)pid64))
                        using (var self = Process.GetCurrentProcess())
                        {
                            if (process.HasExited || process.SessionId != self.SessionId) return false;
                            using (var signal = EventWaitHandle.OpenExisting(@"Global\Valve_SteamIPC_Class", EventWaitHandleRights.Modify))
                            {
                                string command = "\"" + executable + "\" -- \"" + uri + "\"";
                                Queue(command,
                                    delegate { return key64.GetValue(PendingValue) as string; },
                                    delegate { return key32.GetValue(PendingValue) as string; },
                                    delegate(string value) { attemptedWrite = true; key64.SetValue(PendingValue, value, RegistryValueKind.String); },
                                    delegate(string value) { attemptedWrite = true; key32.SetValue(PendingValue, value, RegistryValueKind.String); },
                                    delegate { if (grantForeground) AllowSetForegroundWindow(pid64); },
                                    delegate { if (!signal.Set()) throw new IOException("A Steam não confirmou a sinalização do comando."); });
                                return true;
                            }
                        }
                    }
                }
                catch (SteamNavigationBusyException) { throw; }
                catch (Exception ex)
                {
                    // Antes de escrever: protocolo normal. Depois de tentar escrever: entrega incerta,
                    // nao repetir por outro transporte nem apagar o comando que a Steam pode consumir.
                    if (attemptedWrite) throw new SteamDeliveryUncertainException(ex);
                    if (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException ||
                        ex is ArgumentException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception ||
                        ex is WaitHandleCannotBeOpenedException) return false;
                    throw;
                }
            }
        }
        static bool KnownClient(string path)
        {
            if (string.IsNullOrEmpty(path) || !string.Equals(Path.GetFileName(path), "steam.exe", StringComparison.OrdinalIgnoreCase)) return false;
            var file = new FileInfo(path);
            if (string.Equals(path, verifiedPath, StringComparison.OrdinalIgnoreCase) &&
                file.Length == verifiedLength && file.LastWriteTimeUtc.Ticks == verifiedStamp) return true;
            string hash;
            using (var input = file.OpenRead())
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            if (hash != KnownSha256) return false;
            verifiedPath = path; verifiedLength = file.Length; verifiedStamp = file.LastWriteTimeUtc.Ticks;
            return true;
        }
        internal static void Queue(string command, Func<string> read64, Func<string> read32,
            Action<string> write64, Action<string> write32, Action foreground, Action signal)
        {
            if (!string.IsNullOrEmpty(read64()) || !string.IsNullOrEmpty(read32())) throw new SteamNavigationBusyException();
            write64(command); write32(command); foreground(); signal();
        }
    }

    sealed partial class Preview
    {
#if XGB_VERIFY
        void VerifySteamFastNavigation()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) {
                checks++; if (!ok) throw new Exception("Canal Steam: " + message);
            };
            check(SteamFastNavigation.Supported("steam://open/library"), "Home autorizada");
            check(SteamFastNavigation.Supported("steam://open/library/collection/all-games"), "Biblioteca autorizada");
            foreach (string uri in new[] { "steam://exit", "steam://forceinputappid/0", "steam://open/games", "steam://open/bigpicture", "ms-settings:", "steam://open/library\" -shutdown", null })
                check(!SteamFastNavigation.TrySend(uri), "outros comandos nao entram no canal rapido");
            var trace = new List<string>(); string pending64 = null, pending32 = null;
            Action<string> write64 = delegate(string value) { trace.Add("64"); pending64 = value; };
            Action<string> write32 = delegate(string value) { trace.Add("32"); pending32 = value; };
            Action foreground = delegate { trace.Add("foreground"); }, signal = delegate { trace.Add("signal"); };
            Action send = delegate { SteamFastNavigation.Queue("pagina", delegate { return pending64; }, delegate { return pending32; }, write64, write32, foreground, signal); };
            send();
            check(string.Join(",", trace) == "64,32,foreground,signal", "duas visoes antes de sinalizar");
            check(pending64 == "pagina" && pending32 == "pagina", "comando consistente nas duas visoes");
            foreach (bool busy32 in new[] { false, true })
            {
                trace.Clear(); pending64 = busy32 ? null : "outro"; pending32 = busy32 ? "outro" : null;
                bool busy = false; try { send(); } catch (SteamNavigationBusyException) { busy = true; }
                check(busy && trace.Count == 0, "conteudo pendente preservado sem efeito");
            }
            pending64 = pending32 = null; trace.Clear();
            bool failed = false;
            try { SteamFastNavigation.Queue("pagina", delegate { return null; }, delegate { return null; },
                delegate { throw new UnauthorizedAccessException(); }, write32, foreground, signal); }
            catch (UnauthorizedAccessException) { failed = true; }
            check(failed && trace.Count == 0, "falha na primeira escrita nao sinaliza");
            failed = false; trace.Clear();
            try { SteamFastNavigation.Queue("pagina", delegate { return null; }, delegate { return null; },
                write64, delegate { throw new IOException(); }, foreground, signal); }
            catch (IOException) { failed = true; }
            check(failed && string.Join(",", trace) == "64", "falha parcial preserva comando sem sinalizar ou repetir");
            failed = false; trace.Clear();
            try { SteamFastNavigation.Queue("pagina", delegate { return null; }, delegate { return null; },
                write64, write32, foreground, delegate { trace.Add("falhou"); throw new IOException(); }); }
            catch (IOException) { failed = true; }
            check(failed && string.Join(",", trace) == "64,32,foreground,falhou", "entrega incerta nao e repetida");
            File.WriteAllText(Path.Combine(Output, "steam-canal-verificacao.txt"),
                "OK: " + checks + " verificacoes com substitutos; sem abrir eventos, ler/escrever Registro ou despachar comandos reais. " +
                "Lista limitada de destinos, ordem das visoes, preservacao de canal ocupado e falhas parciais.\r\n");
        }
#endif
    }
}
