// Bateria que aparelhos Bluetooth informam ao Windows (a mesma das Configuracoes). Fones e headsets mandam o nivel
// pelo perfil Hands-Free, e o Windows o guarda no no do servico: {104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2, um byte 0-100.
// Somente leitura das propriedades do dispositivo; nada e enviado ao aparelho (ADR 0094).
// O Windows conserva o ultimo nivel depois da desconexao: quem mostra precisa conferir a conexao.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace XGBP.Network
{
    public sealed class BluetoothBatteryNode
    {
        public Guid Container;
        public int Percent;
        public string InstanceId;
        // Servico de audio (Hands-Free, Headset ou A2DP do lado do fone): o aparelho e um fone.
        public bool Audio;
    }

    public sealed class HeadsetBattery
    {
        public Guid Container;
        public string Name = "";
        public int Percent;
        public bool Connected;
        // Campos do G HUB (ADR 0106). O Hands-Free do Bluetooth nao informa carga nem tempo restante.
        public bool Charging, FromGHub;
        public int HoursLeft = -1;
        public string Link = "Bluetooth";
    }

    public static class BluetoothBattery
    {
        static readonly Guid BatteryKey = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5");
        static readonly Guid ContainerKey = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C");
        // Hands-Free (fone), Headset, Headset HS e Audio Sink. 111F (gateway de audio) e o lado do celular, fora.
        static readonly string[] AudioServices = { "{0000111E-", "{00001108-", "{00001131-", "{0000110B-" };
        const uint TypeByte = 3, TypeGuid = 13;

        // Nos presentes dos enumeradores Bluetooth com nivel de bateria valido.
        public static List<BluetoothBatteryNode> Nodes()
        {
            var result = new List<BluetoothBatteryNode>();
            foreach (var enumerator in new[] { "BTHENUM", "BTHLE", "BTHLEDEVICE" })
            {
                IntPtr set = SetupDiGetClassDevs(IntPtr.Zero, enumerator, IntPtr.Zero, 0x2 | 0x4); // DIGCF_PRESENT | DIGCF_ALLCLASSES
                if (set == new IntPtr(-1)) continue; // enumerador ausente neste PC
                try
                {
                    var info = new DeviceInfo { Size = Marshal.SizeOf(typeof(DeviceInfo)) };
                    for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref info); i++)
                    {
                        int level = ByteProperty(set, ref info, BatteryKey, 2);
                        if (level < 0 || level > 100) continue;
                    var node = new BluetoothBatteryNode { Percent = level, Container = GuidProperty(set, ref info, ContainerKey, 2), InstanceId =
                        InstanceId(set, ref info) };
                        if (node.Container == Guid.Empty) continue;
                        node.Audio = IsAudioService(node.InstanceId);
                        result.Add(node);
                    }
                }
                finally { SetupDiDestroyDeviceInfoList(set); }
            }
            return result;
        }
        public static bool IsAudioService(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            foreach (var service in AudioServices) if (instanceId.IndexOf(service, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        // Nivel por aparelho: o no de audio tem prioridade sobre outros nos do mesmo container.
        public static Dictionary<Guid, BluetoothBatteryNode> ByContainer(List<BluetoothBatteryNode> nodes)
        {
            var result = new Dictionary<Guid, BluetoothBatteryNode>();
            foreach (var node in nodes)
            {
                BluetoothBatteryNode current;
                if (!result.TryGetValue(node.Container, out current) || node.Audio && !current.Audio) result[node.Container] = node;
            }
            return result;
        }

        // Fones Bluetooth pareados com bateria: nome e conexao pela mesma enumeracao publica das Conexoes, sem abrir o aparelho.
        public static List<HeadsetBattery> Headsets()
        {
            var levels = ByContainer(Nodes());
            var result = new List<HeadsetBattery>();
            if (levels.Count == 0) return result;
            var byContainer = new Dictionary<Guid, HeadsetBattery>();
            string[] props = { "System.Devices.Aep.IsConnected", "System.Devices.Aep.ContainerId" };
foreach (var selector in new[] { BluetoothDevice.GetDeviceSelectorFromPairingState(true), BluetoothLEDevice.GetDeviceSelectorFromPairingState(true) })
            foreach (var info in Radios.Wait(DeviceInformation.FindAllAsync(selector, props, DeviceInformationKind.AssociationEndpoint),
                "listar os fones Bluetooth"))
                {
                    object value; Guid container = Guid.Empty;
                    if (info.Properties.TryGetValue("System.Devices.Aep.ContainerId", out value) && value is Guid) container = (Guid)value;
                    BluetoothBatteryNode level;
                    if (container == Guid.Empty || !levels.TryGetValue(container, out level) || !level.Audio) continue;
                    HeadsetBattery headset;
                    if (!byContainer.TryGetValue(container, out headset))
                    {
                        headset = new HeadsetBattery { Container = container, Percent = level.Percent, Name =
                            string.IsNullOrWhiteSpace(info.Name) ? "Fone Bluetooth" : info.Name
                            };
                        byContainer[container] = headset; result.Add(headset);
                    }
                    if (info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out value) && value is bool && (bool)value) headset.Connected = true;
                }
            Sort(result);
            return result;
        }
        // Conectados primeiro, depois pelo nome: o primeiro conectado e o que a faixa de status mostra.
        public static void Sort(List<HeadsetBattery> list)
        {
            list.Sort(delegate(HeadsetBattery a, HeadsetBattery b) {
                int c = b.Connected.CompareTo(a.Connected);
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        static int ByteProperty(IntPtr set, ref DeviceInfo info, Guid format, uint id)
        {
            var key = new PropertyKey { Format = format, Id = id }; uint type, required; var buffer = new byte[1];
            if (!SetupDiGetDeviceProperty(set, ref info, ref key, out type, buffer, 1, out required, 0) || type != TypeByte || required != 1) return -1;
            return buffer[0];
        }
        static Guid GuidProperty(IntPtr set, ref DeviceInfo info, Guid format, uint id)
        {
            var key = new PropertyKey { Format = format, Id = id }; uint type, required; var buffer = new byte[16];
        if (!SetupDiGetDeviceProperty(set, ref info, ref key, out type, buffer, 16, out required, 0) || type != TypeGuid || required != 16) return Guid.Empty;
            return new Guid(buffer);
        }
        static string InstanceId(IntPtr set, ref DeviceInfo info)
        {
            var text = new StringBuilder(512); int required;
            return SetupDiGetDeviceInstanceId(set, ref info, text, text.Capacity, out required) ? text.ToString() : "";
        }

        [StructLayout(LayoutKind.Sequential)] struct DeviceInfo { public int Size; public Guid ClassGuid; public uint Instance; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo info);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDevicePropertyW")]
        static extern bool SetupDiGetDeviceProperty(IntPtr set, ref DeviceInfo info, ref PropertyKey key, out uint type, byte[] buffer, uint size, out uint
            required, uint flags);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SetupDiGetDeviceInstanceId(IntPtr set, ref DeviceInfo info, StringBuilder id, int size, out int required);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    }
}
