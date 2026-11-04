// Inicializacao por usuario, controlada pelo Windows. Nunca cria tarefas, servicos ou escreve StartupApproved.
// StdRegProv com SID explicito + Win32_StartupCommand conferem a mesma entrada vista pelo sistema.
using System;
using System.IO;
using System.Management;
using System.Security.Principal;

namespace XGBP.Desktop
{
    public sealed class StartupState
    {
        public bool Registered, Owned;
        public bool? Enabled;
        public string Command;
        public string Describe() { return !Registered ? "Desligado" : !Owned ? "Entrada de outro caminho" : Enabled == true ? "Ligado" : Enabled == false ? "Desativado no Windows" : "Estado do Windows desconhecido"; }
    }
    public static class StartupRegistration
    {
        public const string Name = "XGB_P";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        const uint UsersHive = 0x80000003;
        static string Sid() { using (var identity = WindowsIdentity.GetCurrent()) return identity.User.Value; }
        public static string Command(string exe)
        {
            string path = Path.GetFullPath(exe);
            if (path.IndexOf('"') >= 0) throw new ArgumentException("Caminho invalido.");
            string command = "\"" + path + "\" --startup";
            if (command.Length > 260) throw new InvalidOperationException("O caminho excede o limite da inicialização do Windows.");
            return command;
        }
        static ManagementClass Provider()
        {
            var context = new ManagementNamedValueCollection();
            context.Add("__ProviderArchitecture", 64); context.Add("__RequiredArchitecture", true);
            return new ManagementClass(new ManagementScope(@"\\.\root\default", new ConnectionOptions { Context = context }), new ManagementPath("StdRegProv"), null);
        }
        static object Registry(string method, string key, object value)
        {
            using (var provider = Provider())
            using (var input = provider.GetMethodParameters(method))
            {
                input["hDefKey"] = UsersHive;
                input["sSubKeyName"] = Sid() + "\\" + key;
                // Get* devolve 1 (generico) quando o valor nao existe nesta maquina.
                // EnumValues distingue ausencia de erro/tipo incorreto antes de pedir seu conteudo.
                if (method == "GetStringValue" || method == "GetBinaryValue")
                {
                    using (var namesInput = provider.GetMethodParameters("EnumValues"))
                    {
                        namesInput["hDefKey"] = UsersHive; namesInput["sSubKeyName"] = input["sSubKeyName"];
                        using (var namesOutput = provider.InvokeMethod("EnumValues", namesInput, null))
                        {
                            uint status = (uint)namesOutput["ReturnValue"];
                            if (status == 2) return null;
                            if (status != 0) throw new IOException("Windows recusou enumerar a inicialização (" + status + ").");
                            var names = namesOutput["sNames"] as string[]; var types = namesOutput["Types"] as Array;
                            int at = names == null ? -1 : Array.FindIndex(names, n => string.Equals(n, Name, StringComparison.OrdinalIgnoreCase));
                            if (at < 0) return null;
                            if (types == null || Convert.ToInt32(types.GetValue(at)) != (method == "GetStringValue" ? 1 : 3))
                                throw new InvalidDataException("Tipo de registro inesperado; entrada preservada.");
                        }
                    }
                }
                if (method != "CreateKey") input["sValueName"] = Name;
                if (method == "SetStringValue") input["sValue"] = value;
                using (var output = provider.InvokeMethod(method, input, null))
                {
                    uint status = (uint)output["ReturnValue"];
                    if (status == 2 && (method.StartsWith("Get", StringComparison.Ordinal) || method == "DeleteValue")) return null;
                    if (status != 0) throw new IOException("Windows recusou a inicialização (" + status + ").");
                    return method == "GetStringValue" ? output["sValue"] : method == "GetBinaryValue" ? output["uValue"] : null;
                }
            }
        }
        public static bool? Approval(byte[] bytes)
        {
            if (bytes == null) return true; // sem bloqueio do Windows: Run e executado no logon
            if (bytes.Length < 12 || bytes[1] != 0 || bytes[2] != 0 || bytes[3] != 0) return null;
            if (bytes[0] == 2 || bytes[0] == 6) return true;
            if (bytes[0] == 3 || bytes[0] == 7) return false;
            return null; // formato desconhecido e preservado; somente o Windows o altera
        }
        public static StartupState Read(string exe)
        {
            string command = Registry("GetStringValue", RunKey, null) as string;
            return new StartupState { Registered = command != null, Command = command,
                Owned = string.Equals(command, Command(exe), StringComparison.OrdinalIgnoreCase),
                Enabled = command == null ? false : Approval(Registry("GetBinaryValue", ApprovalKey, null) as byte[]) };
        }
        public static int SystemEntries(string exe)
        {
            int found = 0; string sid = Sid();
            using (var search = new ManagementObjectSearcher("SELECT Name, Command, UserSID FROM Win32_StartupCommand WHERE Name = 'XGB_P'"))
            using (var rows = search.Get())
                foreach (ManagementObject row in rows)
                using (row)
                    if (string.Equals(row["Command"] as string, Command(exe), StringComparison.OrdinalIgnoreCase) && (string)row["UserSID"] == sid) found++;
            return found;
        }
        public static void Set(string exe, bool enabled)
        {
            var before = Read(exe);
            if (before.Registered && !before.Owned) throw new InvalidOperationException("A entrada XGB_P pertence a outro caminho. Ela foi preservada.");
            if (!enabled) { if (before.Owned) Registry("DeleteValue", RunKey, null); return; }
            if (before.Owned) return; // nunca reativa algo desativado pelo Gerenciador de Tarefas
            try
            {
                Registry("CreateKey", RunKey, null); Registry("SetStringValue", RunKey, Command(exe));
                var after = Read(exe);
                if (!after.Owned || SystemEntries(exe) != 1) throw new IOException("O Windows não confirmou a entrada de inicialização.");
            }
            catch
            {
                // So desfaz a entrada que acabamos de criar, sem tocar em preferencias/valores de terceiros.
                try { if (Read(exe).Owned) Registry("DeleteValue", RunKey, null); } catch (Exception) { }
                throw;
            }
        }
    }
}
