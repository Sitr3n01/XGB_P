// Avisos do Windows sobre o microfone padrao (captura, papel console): mudo/volume pelo IAudioEndpointVolumeCallback
// e troca do padrao ou de aparelhos pelo IMMNotificationClient. Somente leitura: so registra e cancela os avisos,
// nunca chama SetMute nem SetVolume (ADR 0107). Quem muta pode ser o DSX, o Windows ou a propria barra.
// Todo o COM daqui roda em threads MTA do pool, nunca na thread da interface; os avisos chegam em threads do Windows.
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace XGBP.Audio
{
    public sealed class MicrophoneWatch : IDisposable
    {
        readonly object gate = new object();
        readonly VolumeCallback volumeCallback;
        readonly DeviceCallback deviceCallback;
        IMMDeviceEnumerator enumerator;
        IAudioEndpointVolume volume;
        bool disposed, deviceRegistered;

        // Disparado em thread do Windows ou do pool: quem consome despacha para a interface e le o estado de novo.
        public event Action Changed;
        public string BoundId { get; private set; }   // microfone padrao com aviso registrado; null sem microfone
        public bool Active { get { return deviceRegistered; } }

        public MicrophoneWatch() { volumeCallback = new VolumeCallback(this); deviceCallback = new DeviceCallback(this); }

        // Chamar fora da thread da interface. Lanca se o Windows recusar o registro dos avisos de aparelho.
        public void Start()
        {
            lock (gate)
            {
                if (disposed || deviceRegistered) return;
                enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                Marshal.ThrowExceptionForHR(enumerator.RegisterEndpointNotificationCallback(deviceCallback));
                deviceRegistered = true;
                // Sem o aviso de mudo, os avisos de aparelho seguem; a barra ainda le o estado ao abrir e a cada 5 s.
                try { Bind(); } catch (Exception) { Unbind(); }
            }
        }

        // Sob o lock: troca o aviso de mudo/volume para o padrao atual.
        void Bind()
        {
            Unbind();
            IMMDevice device;
            if (enumerator.GetDefaultAudioEndpoint(1, 0, out device) != 0 || device == null) return;   // nenhum microfone ativo
            try
            {
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A"); object value;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out value));
                var endpoint = (IAudioEndpointVolume)value;
                int hr = endpoint.RegisterControlChangeNotify(volumeCallback);
                if (hr != 0) { Marshal.ReleaseComObject(endpoint); Marshal.ThrowExceptionForHR(hr); }
                volume = endpoint;
                string id; device.GetId(out id); BoundId = id;
            }
            finally { Marshal.ReleaseComObject(device); }
        }
        void Unbind()
        {
            if (volume == null) return;
            try { volume.UnregisterControlChangeNotify(volumeCallback); } catch (Exception) { }
            try { Marshal.ReleaseComObject(volume); } catch (Exception) { }
            volume = null; BoundId = null;
        }

        // O Windows proibe registrar/cancelar avisos dentro do proprio aviso: a troca vai para o pool.
        void DefaultChanged()
        {
            Task.Factory.StartNew(delegate {
                lock (gate) { if (disposed || !deviceRegistered) return; try { Bind(); } catch (Exception) { Unbind(); } }
                Raise();
            });
        }
        void Raise()
        {
            var handler = Changed;
            if (handler != null) try { handler(); } catch (Exception) { }   // um consumidor com erro nao derruba o aviso do Windows
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                Unbind();
                if (deviceRegistered) try { enumerator.UnregisterEndpointNotificationCallback(deviceCallback); } catch (Exception) { }
                deviceRegistered = false;
                if (enumerator != null) try { Marshal.ReleaseComObject(enumerator); } catch (Exception) { }
                enumerator = null;
            }
        }

        [ComVisible(true)] sealed class VolumeCallback : IAudioEndpointVolumeCallback
        {
            readonly MicrophoneWatch owner;
            public VolumeCallback(MicrophoneWatch owner) { this.owner = owner; }
            public int OnNotify(IntPtr data) { owner.Raise(); return 0; }   // mudo ou volume: o consumidor le o estado de novo
        }
        [ComVisible(true)] sealed class DeviceCallback : IMMNotificationClient
        {
            readonly MicrophoneWatch owner;
            public DeviceCallback(MicrophoneWatch owner) { this.owner = owner; }
            public int OnDeviceStateChanged(string id, int state) { owner.Raise(); return 0; }
            public int OnDeviceAdded(string id) { owner.Raise(); return 0; }
            public int OnDeviceRemoved(string id) { owner.Raise(); return 0; }
            public int OnDefaultDeviceChanged(int flow, int role, string id) { if (flow == 1 && role == 0) owner.DefaultChanged(); return 0; }
            public int OnPropertyValueChanged(string id, PropertyKey key) { return 0; }   // frequente e irrelevante para o mudo
        }

        [StructLayout(LayoutKind.Sequential)] public struct PropertyKey { public Guid Format; public int Id; }

        [Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
        public interface IAudioEndpointVolumeCallback { [PreserveSig] int OnNotify(IntPtr data); }
        [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
        public interface IMMNotificationClient
        {
            [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int state);
            [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
        }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
        }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
            [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }
        // endpointvolume.h: so os dois primeiros metodos sao usados; os demais preservam a ordem da vtable.
        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
            [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        }
    }
}
