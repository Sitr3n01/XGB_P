// Redes Wi-Fi conhecidas pela Native Wifi API (wlanapi.dll): perfis salvos, alcance, conectar pelo perfil,
// desconectar e esquecer. Leitura sempre; acoes so por escolha do usuario. Nenhuma senha e lida ou exibida.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace XGBP.Network
{
    public sealed class WifiNetwork
    {
        public string Profile = "";
        public bool Connected, InRange, GroupPolicy, Secured;
        public int Signal = -1;   // 0..100 quando em alcance
        public int Bars { get { return Signal < 0 ? -1 : Signal >= 80 ? 4 : Signal >= 55 ? 3 : Signal >= 30 ? 2 : 1; } }
    }
    public sealed class WifiState
    {
        public bool Adapter;
        public Guid Interface;
        public string AdapterName = "", Note;   // Note: alcance indisponivel (ex.: localizacao negada)
        public List<WifiNetwork> Networks = new List<WifiNetwork>();
    }

    public static class Wifi
    {
        // Perfis salvos + redes em alcance. Sem a lista de alcance (permissao de localizacao no 24H2+), ficam so os perfis.
        public static WifiState Read(bool scan)
        {
            var state = new WifiState();
            using (var client = new Client())
            {
                Guid iface; string name; if (!client.FirstInterface(out iface, out name)) return state;
                state.Adapter = true; state.Interface = iface; state.AdapterName = name;
                if (scan) WlanScan(client.Handle, ref iface, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                var byName = new Dictionary<string, WifiNetwork>(StringComparer.OrdinalIgnoreCase);
                IntPtr list;
                Check(WlanGetProfileList(client.Handle, ref iface, IntPtr.Zero, out list), "ler as redes salvas");
                try
                {
                    int count = Marshal.ReadInt32(list);
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr item = new IntPtr(list.ToInt64() + 8 + i * 516);
                        string profile = Marshal.PtrToStringUni(item); uint flags = (uint)Marshal.ReadInt32(item, 512);
                        if (string.IsNullOrEmpty(profile) || byName.ContainsKey(profile)) continue;
                        var network = new WifiNetwork { Profile = profile, GroupPolicy = (flags & 1) != 0 };
                        byName[profile] = network; state.Networks.Add(network);
                    }
                }
                finally { WlanFreeMemory(list); }
                int status = WlanGetAvailableNetworkList(client.Handle, ref iface, 0, IntPtr.Zero, out list);
                if (status == 5) state.Note = "Alcance das redes indisponível: o Windows exige a localização para apps da área de trabalho.";
                else if (status != 0) state.Note = "Alcance das redes indisponível (código " + status + ").";
                else try
                {
                    int count = Marshal.ReadInt32(list);
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr item = new IntPtr(list.ToInt64() + 8 + i * 628);
                        string profile = Marshal.PtrToStringUni(item);
                        WifiNetwork network;
                        if (string.IsNullOrEmpty(profile) || !byName.TryGetValue(profile, out network)) continue;
                        network.InRange = true;
                        // WLAN_AVAILABLE_NETWORK (628 bytes): wlanSignalQuality em 604, bSecurityEnabled em 608, dwFlags em 620.
                        network.Signal = Math.Max(network.Signal, Marshal.ReadInt32(item, 604));
                        network.Secured |= Marshal.ReadInt32(item, 608) != 0;
                        network.Connected |= (Marshal.ReadInt32(item, 620) & 1) != 0;
                    }
                }
                finally { WlanFreeMemory(list); }
            }
            // Conectada primeiro, depois em alcance pelo sinal, e o restante em ordem alfabetica.
            state.Networks.Sort(delegate(WifiNetwork a, WifiNetwork b)
            {
                int c = b.Connected.CompareTo(a.Connected); if (c != 0) return c;
                c = b.InRange.CompareTo(a.InRange); if (c != 0) return c;
                c = b.Signal.CompareTo(a.Signal); return c != 0 ? c : string.Compare(a.Profile, b.Profile, StringComparison.CurrentCultureIgnoreCase);
            });
            return state;
        }
        public static void Connect(string profile)
        {
            using (var client = new Client())
            {
                Guid iface = client.Required();
                var parameters = new ConnectionParameters { Mode = 0, Profile = profile, BssType = 3 };
                Check(WlanConnect(client.Handle, ref iface, ref parameters, IntPtr.Zero), "conectar a " + profile);
            }
        }
        public static void Disconnect()
        {
            using (var client = new Client()) { Guid iface = client.Required(); Check(WlanDisconnect(client.Handle, ref iface, IntPtr.Zero), "desconectar o Wi-Fi"); }
        }
        // Apaga o perfil salvo (inclusive a senha guardada pelo Windows). Perfis de politica de grupo nao podem ser apagados.
        public static void Forget(string profile)
        {
            using (var client = new Client()) { Guid iface = client.Required(); Check(WlanDeleteProfile(client.Handle, ref iface, profile, IntPtr.Zero), "esquecer " + profile); }
        }

        static void Check(int status, string what)
        {
            if (status == 0) return;
            throw new InvalidOperationException(status == 5 ? "O Windows negou " + what + "." :
                status == 1168 ? "Rede não encontrada para " + what + "." : "Não foi possível " + what + " (código " + status + ").");
        }

        sealed class Client : IDisposable
        {
            public IntPtr Handle;
            public Client()
            {
                uint version; int status = WlanOpenHandle(2, IntPtr.Zero, out version, out Handle);
                // Sem o servico WLAN AutoConfig (PC sem Wi-Fi), nao ha adaptador para listar.
                if (status != 0) { Handle = IntPtr.Zero; if (status != 1062 && status != 2) Check(status, "abrir o Wi-Fi"); }
            }
            public bool FirstInterface(out Guid iface, out string name)
            {
                iface = Guid.Empty; name = "";
                if (Handle == IntPtr.Zero) return false;
                IntPtr list; Check(WlanEnumInterfaces(Handle, IntPtr.Zero, out list), "listar os adaptadores Wi-Fi");
                try
                {
                    if (Marshal.ReadInt32(list) == 0) return false;
                    IntPtr item = new IntPtr(list.ToInt64() + 8);
                    iface = (Guid)Marshal.PtrToStructure(item, typeof(Guid)); name = Marshal.PtrToStringUni(new IntPtr(item.ToInt64() + 16));
                    return true;
                }
                finally { WlanFreeMemory(list); }
            }
            public Guid Required()
            {
                Guid iface; string name;
                if (!FirstInterface(out iface, out name)) throw new InvalidOperationException("Nenhum adaptador Wi-Fi encontrado.");
                return iface;
            }
            public void Dispose() { if (Handle != IntPtr.Zero) { WlanCloseHandle(Handle, IntPtr.Zero); Handle = IntPtr.Zero; } }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ConnectionParameters
        {
            public int Mode;                                        // wlan_connection_mode_profile
            [MarshalAs(UnmanagedType.LPWStr)] public string Profile;
            public IntPtr Ssid, Bssids;
            public int BssType;                                     // dot11_BSS_type_any
            public uint Flags;
        }
        [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
        [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanGetProfileList(IntPtr handle, ref Guid iface, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid iface, uint flags, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanScan(IntPtr handle, ref Guid iface, IntPtr ssid, IntPtr ie, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanConnect(IntPtr handle, ref Guid iface, ref ConnectionParameters parameters, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanDisconnect(IntPtr handle, ref Guid iface, IntPtr reserved);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] static extern int WlanDeleteProfile(IntPtr handle, ref Guid iface, string profile, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);
    }
}
