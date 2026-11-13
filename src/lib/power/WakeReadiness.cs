// Despertar pelo PS do DualSense (ADR 0102, metodo BootPS): somente leitura, sem elevacao. Confere a chave de COD do
// BthEnum, o RemoteWakeEnabled do no HID do controle, se o no existe nesta sessao de boot e os outros dispositivos com
// despertar ligado. Nunca escreve: corrigir sao os scripts do usuario (scripts\ativar-despertar-ps.ps1, politica-despertar.ps1).
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace XGBP.Power
{
    public enum WakeStatus { Unknown, Ready, ConnectOnce, NotArmed, NotConfigured, Divergent, NeverPaired }
    public sealed class WakeReadiness
    {
        public WakeStatus Status;
        public bool KeyPresent, KeyCorrect, Paired, Present;
        public int RemoteWake = -1; // -1 ausente
        public string Instance, Error, OthersError;
        public List<string> OthersAwake = new List<string>(); // outros dispositivos com "permitir que ative o computador"
        public string Describe()
        {
            return "estado=" + Status + "; chave=" + (KeyPresent ? (KeyCorrect ? "correta" : "divergente") : "ausente") + "; pareado=" + Paired +
                "; RemoteWakeEnabled=" + RemoteWake + "; presente=" + Present + (Instance != null ? "; no=" + Instance : "") +
                "; outros=" + (OthersAwake.Count == 0 ? "nenhum" : string.Join(", ", OthersAwake.ToArray())) + (Error != null ? "; erro=" + Error : "") +
                (OthersError != null ? "; outros incompletos=" + OthersError : "");
        }
    }
    public static class WakeReadinessReader
    {
        // Pares big-endian (mascara, valor): teclado e apontador (tabela original do BthEnum) e gamepad.
        static readonly uint[] Needed = { 0x0540, 0x0580, 0x0508 };
        // DualSense e DualSense Edge pelo Bluetooth (servico HID 00001124).
        static readonly Regex Controller = new Regex(@"^\{00001124-0000-1000-8000-00805f9b34fb\}_VID&0002054c_PID&(0ce6|0df2)$", RegexOptions.IgnoreCase);
        static readonly Regex ControllerInstance = new Regex(@"^HID\\\{00001124-0000-1000-8000-00805f9b34fb\}_VID&0002054c_PID&(0ce6|0df2)\\",
            RegexOptions.IgnoreCase);

        public static WakeReadiness Read()
        {
            var r = new WakeReadiness();
            try { ReadKey(r); } catch (Exception ex) { r.Error = "chave Bluetooth: " + ex.Message; }
            try { ReadController(r); } catch (Exception ex) { r.Error = (r.Error == null ? "" : r.Error + "; ") + "controle: " + ex.Message; }
            try { ReadOthers(r); } catch (Exception ex) { r.OthersError = ex.Message; }
            r.Status = Classify(r);
            return r;
        }
        public static WakeStatus Classify(WakeReadiness r)
        {
            if (!string.IsNullOrEmpty(r.Error)) return WakeStatus.Unknown;
            if (!r.KeyPresent) return WakeStatus.NotConfigured;
            if (!r.KeyCorrect) return WakeStatus.Divergent;
            if (!r.Paired) return WakeStatus.NeverPaired;
            if (r.RemoteWake != 1) return WakeStatus.NotArmed;
            return r.Present ? WakeStatus.Ready : WakeStatus.ConnectOnce;
        }
        // Contrato estrito compartilhado pelos scripts: exatamente os tres pares, na mesma ordem.
        public static bool KeyHasEntries(byte[] value)
        {
            if (value == null || value.Length != Needed.Length * 8) return false;
            for (int i = 0; i < Needed.Length; i++)
            {
                if (Be(value, i * 8) != 0x1FFC || Be(value, i * 8 + 4) != Needed[i]) return false;
            }
            return true;
        }
        static uint Be(byte[] b, int at) { return (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]); }

        static void ReadKey(WakeReadiness r)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters"))
            {
                var value = key == null ? null : key.GetValue("WaitWake Capable COD Masks") as byte[];
                r.KeyPresent = value != null; r.KeyCorrect = KeyHasEntries(value);
            }
        }
        static void ReadController(WakeReadiness r)
        {
            using (var hid = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\HID"))
            {
                if (hid == null) return;
                foreach (string device in hid.GetSubKeyNames())
                {
                    if (!Controller.IsMatch(device)) continue;
                    using (var node = hid.OpenSubKey(device))
                    {
                        if (node == null) continue;
                        foreach (string instance in node.GetSubKeyNames())
                        {
                            r.Paired = true;
                            string id = @"HID\" + device + @"\" + instance;
                            using (var parameters = node.OpenSubKey(instance + @"\Device Parameters"))
                            {
                                object wake = parameters == null ? null : parameters.GetValue("RemoteWakeEnabled");
                                int value = wake is int ? (int)wake : -1;
                                bool present = Present(id);
                                ConsiderController(r, id, value, present);
                            }
                        }
                    }
                }
            }
        }
        // Um no historico armado nunca mascara a instancia que esta presente agora.
        public static void ConsiderController(WakeReadiness r, string id, int remoteWake, bool present)
        {
            if (r.Instance == null || present && !r.Present || present == r.Present && remoteWake == 1 && r.RemoteWake != 1)
            { r.Instance = id; r.RemoteWake = remoteWake; r.Present = present; }
        }
        // Outros com despertar ligado pelo usuario (MSPower_DeviceWakeEnable), pelo nome que o barramento informa.
        static void ReadOthers(WakeReadiness r)
        {
            var names = new List<string>();
            using (var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new ObjectQuery(
                "SELECT InstanceName, Enable FROM MSPower_DeviceWakeEnable")))
            using (var results = searcher.Get())
                foreach (ManagementBaseObject item in results)
                    using (item)
                    {
                        if (!(item["Enable"] is bool) || !(bool)item["Enable"]) continue;
                        string id = Regex.Replace((string)item["InstanceName"] ?? "", @"_\d+$", "");
                        if (id.Length == 0 || ControllerInstance.IsMatch(id)) continue;
                        string name = DeviceName(id);
                        if (!names.Contains(name)) names.Add(name);
                    }
            r.OthersAwake = names;
        }

        // ---------- cfgmgr32 ----------
        static bool Present(string instanceId) { uint node; return CM_Locate_DevNodeW(out node, instanceId, 0) == 0; }
        static readonly DevPropKey BusReported = new DevPropKey(new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), 4);
        static readonly DevPropKey FriendlyName = new DevPropKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        static readonly DevPropKey DeviceDesc = new DevPropKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
        // HID genericos ("Dispositivo de teclado HID") ganham o nome do aparelho USB acima deles; ausente, o identificador.
        static string DeviceName(string instanceId)
        {
            uint node;
            if (CM_Locate_DevNodeW(out node, instanceId, 0) != 0) return instanceId;
            string own = Text(node, FriendlyName) ?? Text(node, DeviceDesc);
            if (!instanceId.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase)) return string.IsNullOrEmpty(own) ? instanceId : own;
            uint current = node;
            for (int depth = 0; depth < 3; depth++)
            {
                uint parent;
                if (CM_Get_Parent(out parent, current, 0) != 0) break;
                current = parent;
                string reported = Text(current, BusReported);
                if (!string.IsNullOrEmpty(reported)) return reported.Trim();
            }
            return string.IsNullOrEmpty(own) ? instanceId : own;
        }
        static string Text(uint node, DevPropKey key)
        {
            uint type, size = 0;
            CM_Get_DevNode_PropertyW(node, ref key, out type, null, ref size, 0); // primeira chamada: so o tamanho
            if (size == 0 || size > 4096) return null;
            var buffer = new byte[size];
            if (CM_Get_DevNode_PropertyW(node, ref key, out type, buffer, ref size, 0) != 0 || type != 0x12) return null; // DEVPROP_TYPE_STRING
            return Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
        }
        [StructLayout(LayoutKind.Sequential)]
        struct DevPropKey { public Guid Fmtid; public uint Pid; public DevPropKey(Guid fmtid, uint pid) { Fmtid = fmtid; Pid = pid; } }
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint node, string instanceId, uint flags);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint node, uint flags);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_DevNode_PropertyW(uint node, ref DevPropKey key, out uint type, byte[] buffer, ref uint size, uint
            flags);
    }
}
