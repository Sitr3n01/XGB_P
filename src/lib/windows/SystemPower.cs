// Energia local, exclusivamente apos confirmacao na interface. Testes substituem os dois efeitos.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Forms = System.Windows.Forms;

namespace XGBP.Desktop
{
    public enum SystemPowerAction { Suspend, Restart, Shutdown }

    public static class SystemPower
    {
        internal static ProcessStartInfo ShutdownRequest(SystemPowerAction action)
        {
            if (action != SystemPowerAction.Restart && action != SystemPowerAction.Shutdown)
                throw new ArgumentOutOfRangeException("action");
            return new ProcessStartInfo {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe"),
                // /t 0 evita o /f implicito dos timeouts positivos. Nao forcar fechamento de aplicativos.
                Arguments = (action == SystemPowerAction.Restart ? "/r" : "/s") + " /t 0 /d p:0:0",
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            };
        }
        public static void Execute(SystemPowerAction action)
        {
            Execute(action, delegate(ProcessStartInfo request) {
                using (var child = Process.Start(request))
                {
                    if (child == null) throw new IOException("O Windows não iniciou o pedido de energia.");
                    if (!child.WaitForExit(10000)) throw new IOException("O Windows não confirmou o recebimento. Confira o estado antes de tentar novamente.");
                    return child.ExitCode;
                }
            }, delegate { return Forms.Application.SetSuspendState(Forms.PowerState.Suspend, false, false); });
        }
        internal static void Execute(SystemPowerAction action, Func<ProcessStartInfo, int> launch, Func<bool> suspend)
        {
            if (action == SystemPowerAction.Suspend)
            {
                if (!suspend()) throw new IOException("O Windows recusou a suspensão.");
                return;
            }
            int code = launch(ShutdownRequest(action));
            if (code != 0) throw new Win32Exception(code, "O Windows recusou o pedido de energia (código " + code + ").");
        }
    }
}
