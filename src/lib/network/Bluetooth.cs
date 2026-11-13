// Dispositivos Bluetooth conhecidos (pareados) pela API publica Windows.Devices.Enumeration.
// Remover desfaz o pareamento. Conectar/desconectar audio Bluetooth usa as propriedades oneshot de
// KSPROPSETID_BtAudio (ksmedia.h) no filtro de audio do dispositivo. Leitura sempre; acoes so por escolha do usuario.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace XGBP.Network
{
    public sealed class BluetoothThing
    {
        public string Name = "", Kind = "Outro", Key = "";
        public Guid Container;
        public List<string> Ids = new List<string>();
        public bool Connected, CanUnpair, Controller, AudioControl;   // AudioControl: ha filtro de audio BT para conectar/desconectar
        public int Battery = -1;   // ultimo nivel informado ao Windows (ADR 0094); -1 sem leitura. Vale so com Connected.
    }

    public static class Bluetooth
    {
        static readonly string[] Props = { "System.Devices.Aep.IsConnected", "System.Devices.Aep.ContainerId" };

        public static List<BluetoothThing> Read()
        {
            var byKey = new Dictionary<string, BluetoothThing>(); var result = new List<BluetoothThing>();
            var endpoints = AudioContainers();
            foreach (var selector in new[] { BluetoothDevice.GetDeviceSelectorFromPairingState(true), BluetoothLEDevice.GetDeviceSelectorFromPairingState(true) })
            {
                bool classic = selector == BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                foreach (var info in Radios.Wait(DeviceInformation.FindAllAsync(selector, Props, DeviceInformationKind.AssociationEndpoint), "listar os dispositivos Bluetooth"))
                {
                    object value; Guid container = Guid.Empty;
                    if (info.Properties.TryGetValue("System.Devices.Aep.ContainerId", out value) && value is Guid) container = (Guid)value;
                    // Classico e LE do mesmo aparelho ficam no mesmo container, como nas Configuracoes do Windows.
                    string key = container != Guid.Empty ? container.ToString() : info.Name ?? info.Id;
                    BluetoothThing thing;
                    if (!byKey.TryGetValue(key, out thing))
                    {
                        thing = new BluetoothThing { Key = key, Container = container, Name = string.IsNullOrWhiteSpace(info.Name) ? "Dispositivo Bluetooth" : info.Name };
                        byKey[key] = thing; result.Add(thing);
                    }
                    thing.Ids.Add(info.Id);
                    if (info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out value) && value is bool && (bool)value) thing.Connected = true;
                    thing.CanUnpair |= info.Pairing != null && info.Pairing.IsPaired;
                    if (classic) Classify(thing, info.Id);
                }
            }
            Dictionary<Guid, BluetoothBatteryNode> levels = null;
            try { levels = BluetoothBattery.ByContainer(BluetoothBattery.Nodes()); } catch (Exception) { }   // bateria e so detalhe da lista
            foreach (var thing in result)
            {
                BluetoothBatteryNode level;
                if (levels != null && thing.Container != Guid.Empty && levels.TryGetValue(thing.Container, out level)) thing.Battery = level.Percent;
                string n = thing.Name.ToLowerInvariant();
                thing.Controller |= n.Contains("dualsense") || n.Contains("wireless controller") || n.Contains("dualshock");
                if (thing.Controller) thing.Kind = "Controle";
                thing.AudioControl = thing.Container != Guid.Empty && endpoints.Contains(thing.Container) && AudioFilters(thing.Container).Count > 0;
            }
            result.Sort(delegate(BluetoothThing a, BluetoothThing b)
            {
                int c = b.Connected.CompareTo(a.Connected);
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        static void Classify(BluetoothThing thing, string id)
        {
            try
            {
                using (var device = Radios.Wait(BluetoothDevice.FromIdAsync(id), "ler o dispositivo Bluetooth"))
                {
                    if (device == null) return;
                    var major = device.ClassOfDevice.MajorClass;
                    thing.Kind = major == BluetoothMajorClass.AudioVideo ? "Áudio" : major == BluetoothMajorClass.Phone ? "Celular" :
                        major == BluetoothMajorClass.Computer ? "Computador" : major == BluetoothMajorClass.Peripheral ? "Entrada" : thing.Kind;
                    var minor = device.ClassOfDevice.MinorClass;
                    if (major == BluetoothMajorClass.Peripheral && (minor == BluetoothMinorClass.PeripheralGamepad || minor == BluetoothMinorClass.PeripheralJoystick))
                    { thing.Kind = "Controle"; thing.Controller = true; }
                }
            }
            catch (Exception) { }   // Classificacao e so rotulo; a lista continua sem ela.
        }

        // Desfaz o pareamento de todas as entradas do aparelho (classica e LE).
        public static void Unpair(BluetoothThing thing)
        {
            foreach (var id in thing.Ids)
            {
                var info = Radios.Wait(DeviceInformation.CreateFromIdAsync(id, new string[0], DeviceInformationKind.AssociationEndpoint), "ler o dispositivo Bluetooth");
                var result = Radios.Wait(info.Pairing.UnpairAsync(), "remover o dispositivo");
                if (result.Status != DeviceUnpairingResultStatus.Unpaired && result.Status != DeviceUnpairingResultStatus.AlreadyUnpaired)
                    throw new InvalidOperationException(result.Status == DeviceUnpairingResultStatus.AccessDenied ? "O Windows negou remover " + thing.Name + "." : "Não foi possível remover " + thing.Name + ".");
            }
        }

        // Pede ao driver de audio Bluetooth para conectar (RECONNECT) ou desconectar (DISCONNECT) o aparelho.
        public static void Audio(BluetoothThing thing, bool connect)
        {
            var filters = AudioFilters(thing.Container);
            if (filters.Count == 0) throw new InvalidOperationException(thing.Name + " não tem áudio Bluetooth para " + (connect ? "conectar." : "desconectar."));
            int sent = 0, last = 0;
            using (var scope = new ComScope())
            {
                var enumerator = scope.Own((IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClass)));
                foreach (var id in filters)
                {
                    var control = KsControl(enumerator, id, scope); if (control == null) continue;
                    var property = new KsPropertyHeader { Set = BtAudio, Id = connect ? 0u : 1u, Flags = 1 };
                    uint returned; last = control.KsProperty(ref property, (uint)Marshal.SizeOf(typeof(KsPropertyHeader)), IntPtr.Zero, 0, out returned);
                    if (last >= 0) sent++;
                }
            }
            if (sent == 0) throw new InvalidOperationException("O Windows recusou " + (connect ? "conectar " : "desconectar ") + thing.Name + " (0x" + last.ToString("X8") + ").");
        }

        // Containers com endpoint de audio (qualquer estado): candidatos a controle de conexao de audio.
        static HashSet<Guid> AudioContainers()
        {
            var found = new HashSet<Guid>();
            try
            {
                using (var scope = new ComScope())
                {
                    var enumerator = scope.Own((IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClass)));
                    foreach (var endpoint in Endpoints(enumerator, scope)) { Guid c = ContainerOf(endpoint, scope); if (c != Guid.Empty) found.Add(c); }
                }
            }
            catch (COMException) { } catch (InvalidCastException) { }
            return found;
        }
        // Filtros de audio do container que aceitam IKsControl. So obtem a interface; nada e enviado aqui.
        static List<string> AudioFilters(Guid container)
        {
            var ids = new List<string>();
            try
            {
                using (var scope = new ComScope())
                {
                    var enumerator = scope.Own((IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClass)));
                    foreach (var endpoint in Endpoints(enumerator, scope))
                    {
                        if (ContainerOf(endpoint, scope) != container) continue;
                        try
                        {
                            Guid iid = TopologyId; object value; endpoint.Activate(ref iid, 23, IntPtr.Zero, out value); scope.Own(value);
                            var topology = (IDeviceTopology)value; IConnector connector; topology.GetConnector(0, out connector); scope.Own(connector);
                            string filter; connector.GetDeviceIdConnectedTo(out filter);
                            if (string.IsNullOrEmpty(filter) || ids.Contains(filter)) continue;
                            if (KsControl(enumerator, filter, scope) != null) ids.Add(filter);
                        }
                        catch (COMException) { } catch (InvalidCastException) { }   // endpoint sem conector ligado
                    }
                }
            }
            catch (COMException) { } catch (InvalidCastException) { }
            return ids;
        }
        static IKsControl KsControl(IMMDeviceEnumerator enumerator, string filter, ComScope scope)
        {
            try
            {
                IMMDevice device; enumerator.GetDevice(filter, out device); scope.Own(device);
                Guid iid = KsControlId; object value; device.Activate(ref iid, 23, IntPtr.Zero, out value); scope.Own(value);
                return value as IKsControl;
            }
            catch (COMException) { return null; } catch (InvalidCastException) { return null; }
        }
        static List<IMMDevice> Endpoints(IMMDeviceEnumerator enumerator, ComScope scope)
        {
            var list = new List<IMMDevice>();
            IMMDeviceCollection all; enumerator.EnumAudioEndpoints(2, 0xF, out all); scope.Own(all);
            uint count; all.GetCount(out count);
            for (uint i = 0; i < count; i++) { IMMDevice d; all.Item(i, out d); list.Add(scope.Own(d)); }
            return list;
        }
        static Guid ContainerOf(IMMDevice device, ComScope scope)
        {
            try
            {
                IPropertyStore store; device.OpenPropertyStore(0, out store); scope.Own(store);
                var key = new PropertyKey { Format = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), Id = 2 };   // PKEY_Device_ContainerId
                PropVariant value; store.GetValue(ref key, out value);
                try { return value.Type == 72 && value.Pointer != IntPtr.Zero ? (Guid)Marshal.PtrToStructure(value.Pointer, typeof(Guid)) : Guid.Empty; }
                finally { PropVariantClear(ref value); }
            }
            catch (COMException) { return Guid.Empty; }
        }

        static readonly Guid EnumeratorClass = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        static readonly Guid TopologyId = new Guid("2A07407E-6497-4A18-9787-32F79BD0D98F");
        static readonly Guid KsControlId = new Guid("28F54685-06FD-11D2-B27A-00A0C9223196");
        static readonly Guid BtAudio = new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");   // KSPROPSETID_BtAudio

        sealed class ComScope : IDisposable
        {
            readonly List<object> owned = new List<object>();
            public T Own<T>(T value) { if (value != null) owned.Add(value); return value; }
            public void Dispose() { for (int i = owned.Count - 1; i >= 0; i--) if (Marshal.IsComObject(owned[i])) Marshal.ReleaseComObject(owned[i]); }
        }
        [StructLayout(LayoutKind.Sequential)] struct KsPropertyHeader { public Guid Set; public uint Id; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public int Id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)] struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
            void GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
            void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            void RegisterEndpointNotificationCallback(IntPtr client); void UnregisterEndpointNotificationCallback(IntPtr client);
        }
        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceCollection
        { void GetCount(out uint count); void Item(uint index, out IMMDevice device); }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDevice
        {
            void Activate(ref Guid id, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
            void OpenPropertyStore(uint access, out IPropertyStore store);
            void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id); void GetState(out uint state);
        }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPropertyStore
        { void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out PropVariant value); void SetValue(ref PropertyKey key, ref PropVariant value); void Commit(); }
        // devicetopology.h: so os dois primeiros metodos sao usados; os demais preservam a ordem da vtable.
        [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDeviceTopology
        {
            void GetConnectorCount(out uint count); void GetConnector(uint index, out IConnector connector);
            void GetSubunitCount(out uint count); void GetSubunit(uint index, out IntPtr subunit); void GetPartById(uint id, out IntPtr part);
            void GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string id); void GetSignalPath(IntPtr from, IntPtr to, bool reject, out IntPtr parts);
        }
        [ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IConnector
        {
            void GetType(out int type); void GetDataFlow(out int flow); void ConnectTo(IntPtr other); void Disconnect(); void IsConnected(out bool connected);
            void GetConnectedTo(out IntPtr other); void GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
            void GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        }
        [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IKsControl
        {
            [PreserveSig] int KsProperty(ref KsPropertyHeader property, uint length, IntPtr data, uint dataLength, out uint returned);
            [PreserveSig] int KsMethod(IntPtr method, uint length, IntPtr data, uint dataLength, out uint returned);
            [PreserveSig] int KsEvent(IntPtr ksEvent, uint length, IntPtr data, uint dataLength, out uint returned);
        }
    }
}
