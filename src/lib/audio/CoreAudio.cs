// Core Audio do Windows. Controle de volumes; nao captura audio nem altera drivers/servicos.
// C# 5 / .NET Framework, x64. Interfaces publicas conferidas nos headers do SDK 10.0.26100.0.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace XGBP.Audio
{
    public sealed class AudioDeviceState
    {
        public string Id, Name, Error;
        public int Flow;
        public float Level;
        public bool Muted, Available;
    }
    // Microfone padrao do Windows (captura, papel console). Volume 0-100.
    public sealed class MicrophoneState
    {
        public string Id, Name;
        public int Volume;
        public bool Muted;
    }
    public sealed class AudioSessionState
    {
        public string Id, DeviceId, DeviceName, AppName, AppIdentity, IconPath;
        public Guid Grouping;
        public bool SystemSounds;
        public uint ProcessId;
        public float Level;
        public bool Muted;
    }
    public sealed class AudioSnapshot
    {
        public readonly List<AudioDeviceState> Devices = new List<AudioDeviceState>();
        public readonly List<AudioSessionState> Sessions = new List<AudioSessionState>();
        public readonly List<string> Warnings = new List<string>();
        public readonly string[,] Defaults = new string[2, 3];
        public AudioDeviceState Default(int flow) { return Devices.Find(d => d.Id == Defaults[flow, 1]); }
        public string Describe()
        {
            var s = new StringBuilder();
            foreach (var d in Devices) s.AppendLine(string.Format("{0} | {1} | {2:P1} | mute={3} | {4} | {5}", d.Flow == 0 ? "saida" : "entrada", d.Name, d.Level, d.Muted, d.Id, d.Error));
            for (int f = 0; f < 2; f++) for (int r = 0; r < 3; r++) s.AppendLine("padrao fluxo=" + f + " papel=" + r + " | " + Defaults[f, r]);
            foreach (var a in Sessions) s.AppendLine(string.Format("app {0} | pid={1} | {2:P1} | mute={3} | {4} | {5} | grupo={6} | identidade={7}", a.AppName, a.ProcessId, a.Level, a.Muted, a.DeviceName, a.Id, a.Grouping, a.AppIdentity));
            foreach (var w in Warnings) s.AppendLine("aviso: " + w);
            return s.ToString();
        }
    }
    // Volume de uma sessao de app: instancia (viva agora) e identificador persistente do app naquela saida.
    public sealed class SessionLevel
    {
        public string DeviceId, SessionId, SessionKey;
        public uint ProcessId;
        public float Level;
    }

    public sealed class AudioChange
    {
        // endpoint-volume, endpoint-mute, session-volume, session-mute, default
        public string Kind, DeviceId;
        public string[] SessionIds;
        public int Flow;
        public float Level;
        public bool Muted;
        public string Key;
        // Metadados da intencao na UI; o backend nativo nao os usa como identidade de sessao.
        public long Revision;
        public DateTime QueuedAt;
        public string FeedbackScope, MediaKey;
    }

    public static class CoreAudio
    {
        static readonly Guid EnumeratorClass = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        static readonly Guid EndpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static readonly Guid SessionManagerId = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        static readonly Guid Context = new Guid("F1B6F4F8-296B-451F-9DB5-9C73AB7DD521");

        sealed class ComScope : IDisposable
        {
            readonly List<object> owned = new List<object>();
            public T Own<T>(T value) { if (value != null) owned.Add(value); return value; }
            public void Dispose() { for (int i = owned.Count - 1; i >= 0; i--) if (Marshal.IsComObject(owned[i])) Marshal.ReleaseComObject(owned[i]); }
        }
        static IMMDeviceEnumerator Enumerator(ComScope scope)
        { return scope.Own((IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClass))); }
        static string DefaultId(IMMDeviceEnumerator e, int flow, int role)
        {
            try { using (var s = new ComScope()) { IMMDevice d; e.GetDefaultAudioEndpoint(flow, role, out d); s.Own(d); string id; d.GetId(out id); return id; } }
            catch (COMException) { return null; }
        }
        static IAudioEndpointVolume EndpointVolume(IMMDevice d, ComScope scope)
        { Guid iid = EndpointVolumeId; object v; d.Activate(ref iid, 23, IntPtr.Zero, out v); scope.Own(v); return (IAudioEndpointVolume)v; }
        static string FriendlyName(IMMDevice d, ComScope scope)
        {
            IPropertyStore store; d.OpenPropertyStore(0, out store); scope.Own(store);
            var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
            PropVariant value; store.GetValue(ref key, out value);
            try { return (value.Type == 31 || value.Type == 8) && value.Pointer != IntPtr.Zero ? Marshal.PtrToStringUni(value.Pointer) : "Dispositivo de áudio"; }
            finally { PropVariantClear(ref value); }
        }
        static IMMDevice Device(IMMDeviceEnumerator e, string id, ComScope scope)
        {
            IMMDevice d; e.GetDevice(id, out d); scope.Own(d); uint state; d.GetState(out state);
            if ((state & 1) == 0) throw new InvalidOperationException("O dispositivo foi desconectado.");
            return d;
        }

        public static AudioSnapshot Read()
        {
            var result = new AudioSnapshot();
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope);
                for (int flow = 0; flow < 2; flow++)
                {
                    for (int role = 0; role < 3; role++) result.Defaults[flow, role] = DefaultId(e, flow, role);
                    IMMDeviceCollection collection; e.EnumAudioEndpoints(flow, 1, out collection); scope.Own(collection);
                    uint count; collection.GetCount(out count);
                    for (uint i = 0; i < count; i++) using (var ds = new ComScope())
                    {
                        IMMDevice d; collection.Item(i, out d); ds.Own(d);
                        string id; d.GetId(out id);
                        var state = new AudioDeviceState { Id = id, Flow = flow, Name = id };
                        result.Devices.Add(state);
                        try { state.Name = FriendlyName(d, ds); var volume = EndpointVolume(d, ds); volume.GetMasterVolumeLevelScalar(out state.Level); volume.GetMute(out state.Muted); state.Available = true; }
                        catch (Exception ex) { state.Error = ex.Message; }
                        if (flow == 0)
                        {
                            try { VisitSessions(d, delegate(IAudioSessionControl2 c, ISimpleAudioVolume v) {
                                int sessionState; c.GetState(out sessionState); if (sessionState == 2) return;
                                var a = new AudioSessionState { DeviceId = id, DeviceName = state.Name };
                                c.GetSessionInstanceIdentifier(out a.Id); c.GetProcessId(out a.ProcessId);
                                v.GetMasterVolume(out a.Level); v.GetMute(out a.Muted);
                                c.GetGroupingParam(out a.Grouping);
                                a.SystemSounds = c.IsSystemSoundsSession() == 0;
                                string displayName; c.GetDisplayName(out displayName);
                                c.GetIconPath(out a.IconPath);
                                a.AppName = ResolveDisplayName(displayName);
                                if (a.SystemSounds) { a.AppName = "Sons do sistema"; a.AppIdentity = "system-sounds"; }
                                else
                                {
                                    try
                                    {
                                        using (var p = Process.GetProcessById((int)a.ProcessId))
                                        {
                                            string processName = p.ProcessName;
                                            string path = ProcessPath(a.ProcessId);
                                            a.AppIdentity = path ?? "pid:" + a.ProcessId;
                                            if (string.IsNullOrWhiteSpace(a.AppName))
                                            {
                                                try
                                                {
                                                    var info = FileVersionInfo.GetVersionInfo(path);
                                                    a.AppName = string.IsNullOrWhiteSpace(info.FileDescription) ? info.ProductName : info.FileDescription;
                                                }
                                                catch { }
                                                if (string.IsNullOrWhiteSpace(a.AppName)) a.AppName = processName;
                                            }
                                            if (string.IsNullOrWhiteSpace(a.IconPath) || !System.IO.File.Exists(a.IconPath)) a.IconPath = path;
                                        }
                                    }
                                    catch (ArgumentException) { return; } // processo encerrado entre enumeracao e leitura
                                    catch (InvalidOperationException) { return; }
                                    catch { a.AppIdentity = "pid:" + a.ProcessId; }
                                    if (string.IsNullOrWhiteSpace(a.AppName)) a.AppName = "Aplicativo (PID " + a.ProcessId + ")";
                                }
                                result.Sessions.Add(a);
                            }); } catch (Exception ex) { result.Warnings.Add(state.Name + ": " + ex.Message); }
                        }
                    }
                }
            }
            return result;
        }

        static string ResolveDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("@", StringComparison.Ordinal)) return name;
            var resolved = new StringBuilder(1024);
            return SHLoadIndirectString(name, resolved, (uint)resolved.Capacity, IntPtr.Zero) == 0 ? resolved.ToString() : null;
        }
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int SHLoadIndirectString(string source, StringBuilder output, uint count, IntPtr reserved);
        static string ProcessPath(uint pid)
        {
            // Acesso limitado permite identificar apps elevados sem pedir privilegios administrativos.
            IntPtr process = OpenProcess(0x1000, false, pid);
            if (process == IntPtr.Zero) return null;
            try { var path = new StringBuilder(32768); int size = path.Capacity; return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : null; }
            finally { CloseHandle(process); }
        }
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        static void VisitSessions(IMMDevice d, Action<IAudioSessionControl2, ISimpleAudioVolume> action)
        {
            using (var s = new ComScope())
            {
                Guid iid = SessionManagerId; object o; d.Activate(ref iid, 23, IntPtr.Zero, out o); s.Own(o);
                IAudioSessionEnumerator sessions; ((IAudioSessionManager2)o).GetSessionEnumerator(out sessions); s.Own(sessions);
                int count; sessions.GetCount(out count);
                for (int i = 0; i < count; i++) using (var cs = new ComScope())
                {
                    IAudioSessionControl c; sessions.GetSession(i, out c); cs.Own(c);
                    try { action((IAudioSessionControl2)c, (ISimpleAudioVolume)c); }
                    catch (COMException ex) { if (ex.ErrorCode != unchecked((int)0x88890004)) throw; } // dispositivo invalidado
                }
            }
        }

        public static void Apply(AudioChange change)
        {
            if (change.Level < 0 || change.Level > 1 || float.IsNaN(change.Level)) throw new ArgumentOutOfRangeException("Level");
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope); var d = Device(e, change.DeviceId, scope); Guid context = Context;
                if (change.Kind == "default") { SetDefault(e, d, change); return; }
                if (change.Kind.StartsWith("endpoint-", StringComparison.Ordinal))
                {
                    var v = EndpointVolume(d, scope);
                    if (change.Kind == "endpoint-volume") v.SetMasterVolumeLevelScalar(change.Level, ref context);
                    else if (change.Kind == "endpoint-mute") v.SetMute(change.Muted, ref context);
                    else throw new ArgumentException("Acao de audio desconhecida.");
                    return;
                }
                if (change.Kind != "session-volume" && change.Kind != "session-mute") throw new ArgumentException("Acao de audio desconhecida.");
                var ids = new HashSet<string>(change.SessionIds ?? new string[0], StringComparer.Ordinal);
                int matched = 0;
                VisitSessions(d, delegate(IAudioSessionControl2 c, ISimpleAudioVolume v) {
                    string id; c.GetSessionInstanceIdentifier(out id); if (!ids.Contains(id)) return;
                    if (change.Kind == "session-volume") v.SetMasterVolume(change.Level, ref context);
                    else v.SetMute(change.Muted, ref context);
                    matched++;
                });
                if (matched == 0) throw new InvalidOperationException("A sessão de áudio foi encerrada. Atualize o mixer.");
            }
        }

        // Sessoes de saida dos processos indicados (ou do mesmo executavel), em todas as saidas ativas.
        // pids == null le todas, para a recuperacao depois de uma queda (ADR 0092). Sons do sistema ficam de fora.
        public static List<SessionLevel> SessionLevels(ICollection<uint> pids, string executable)
        {
            var result = new List<SessionLevel>();
            var paths = new Dictionary<uint, string>();
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope);
                IMMDeviceCollection collection; e.EnumAudioEndpoints(0, 1, out collection); scope.Own(collection);
                uint count; collection.GetCount(out count);
                for (uint i = 0; i < count; i++) using (var ds = new ComScope())
                {
                    IMMDevice d; collection.Item(i, out d); ds.Own(d);
                    string id; d.GetId(out id);
                    try
                    {
                        VisitSessions(d, delegate(IAudioSessionControl2 c, ISimpleAudioVolume v) {
                            int state; c.GetState(out state); if (state == 2 || c.IsSystemSoundsSession() == 0) return;
                            uint pid; c.GetProcessId(out pid); if (pid == 0) return;
                            if (pids != null && !pids.Contains(pid))
                            {
                                string path;
                                if (!paths.TryGetValue(pid, out path)) paths[pid] = path = ProcessPath(pid);
                                if (string.IsNullOrEmpty(executable) || !string.Equals(path, executable, StringComparison.OrdinalIgnoreCase)) return;
                            }
                            var item = new SessionLevel { DeviceId = id, ProcessId = pid };
                            c.GetSessionInstanceIdentifier(out item.SessionId); c.GetSessionIdentifier(out item.SessionKey);
                            v.GetMasterVolume(out item.Level);
                            result.Add(item);
                        });
                    }
                    catch (COMException) { } // saida removida durante a leitura
                }
            }
            return result;
        }
        // Aplica o nivel de cada sessao pela identidade da instancia; devolve quantas ainda existiam.
        public static int SetSessionLevels(IList<SessionLevel> items)
        {
            var byDevice = new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item.Level < 0 || item.Level > 1 || float.IsNaN(item.Level)) throw new ArgumentOutOfRangeException("Level");
                Dictionary<string, float> sessions;
                if (!byDevice.TryGetValue(item.DeviceId, out sessions)) byDevice[item.DeviceId] = sessions = new Dictionary<string,
                    float>(StringComparer.Ordinal);
                sessions[item.SessionId] = item.Level;
            }
            int matched = 0;
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope); Guid context = Context;
                foreach (var pair in byDevice) using (var ds = new ComScope())
                {
                    IMMDevice d;
                    try { d = Device(e, pair.Key, ds); } catch (Exception) { continue; } // saida desconectada
                    VisitSessions(d, delegate(IAudioSessionControl2 c, ISimpleAudioVolume v) {
                        string id; c.GetSessionInstanceIdentifier(out id); float level;
                        if (id == null || !pair.Value.TryGetValue(id, out level)) return;
                        v.SetMasterVolume(level, ref context); matched++;
                    });
                }
            }
            return matched;
        }

        static void SetDefault(IMMDeviceEnumerator e, IMMDevice d, AudioChange change)
        {
            // IPolicyConfig e privada. Se falhar, a UI oferece as configuracoes oficiais do Windows.
            if (change.Flow != 0 && change.Flow != 1) throw new ArgumentOutOfRangeException("Flow");
            var endpoint = (IMMEndpoint)d; int flow; endpoint.GetDataFlow(out flow);
            if (flow != change.Flow) throw new InvalidOperationException("Tipo de dispositivo incorreto.");
            var previous = new string[3]; for (int role = 0; role < 3; role++) previous[role] = DefaultId(e, flow, role);
            using (var scope = new ComScope())
            {
                var policy = scope.Own((IPolicyConfig)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))));
                var changed = new List<int>();
                try
                {
                    for (int role = 0; role < 3; role++) if (previous[role] != change.DeviceId)
                    { Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(change.DeviceId, role)); changed.Add(role); }
                    for (int role = 0; role < 3; role++) if (DefaultId(e, flow, role) != change.DeviceId) throw new InvalidOperationException("Windows não confirmou o dispositivo padrão.");
                }
                catch (Exception original)
                {
                    var rollback = new List<string>();
                    foreach (int role in changed)
                    {
                        if (previous[role] == null) { rollback.Add("Papel " + role + " estava sem padrão e não pôde ser restaurado."); continue; }
                        try { Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(previous[role], role)); } catch (Exception ex) { rollback.Add(ex.Message); }
                    }
                    if (rollback.Count > 0) throw new InvalidOperationException(original.Message + " Reversão parcial: " + string.Join("; ", rollback.ToArray()), original);
                    throw;
                }
            }
        }

        // Microfone padrao: null quando nao ha dispositivo de captura ativo.
        public static MicrophoneState ReadMicrophone()
        {
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope); string id = DefaultId(e, 1, 0); if (id == null) return null;
                var d = Device(e, id, scope); var v = EndpointVolume(d, scope);
                float level; v.GetMasterVolumeLevelScalar(out level); bool muted; v.GetMute(out muted);
                return new MicrophoneState { Id = id, Name = FriendlyName(d, scope), Volume = (int)Math.Round(level * 100), Muted = muted };
            }
        }
        // So o dispositivo lido antes (por id): uma troca do padrao no meio nao muda outro microfone.
        public static void SetMicrophone(string id, int? volume, bool? muted)
        {
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope); var d = Device(e, id, scope); var v = EndpointVolume(d, scope); Guid context = Context;
                if (volume.HasValue) v.SetMasterVolumeLevelScalar(Math.Max(0, Math.Min(100, volume.Value)) / 100f, ref context);
                if (muted.HasValue) v.SetMute(muted.Value, ref context);
            }
        }

        // Testa setters com os proprios valores lidos: nao muda volume/mute nem troca dispositivos.
        public static string CheckControls()
        {
            var before = Read(); var report = new StringBuilder();
            foreach (var d in before.Devices)
            {
                if (!d.Available || (d.Id != before.Defaults[d.Flow, 1])) continue;
                Apply(new AudioChange { Kind = "endpoint-volume", DeviceId = d.Id, Level = d.Level });
                Apply(new AudioChange { Kind = "endpoint-mute", DeviceId = d.Id, Muted = d.Muted });
                var after = Read().Devices.Find(item => item.Id == d.Id);
                if (after == null || Math.Abs(after.Level - d.Level) > 0.001 || after.Muted != d.Muted) throw new InvalidOperationException("Leitura divergente apos teste de " + d.Name);
                report.AppendLine("OK volume/mute, valor preservado: " + d.Name);
            }
            foreach (var a in before.Sessions)
            {
                Apply(new AudioChange { Kind = "session-volume", DeviceId = a.DeviceId, SessionIds = new[] { a.Id }, Level = a.Level });
                Apply(new AudioChange { Kind = "session-mute", DeviceId = a.DeviceId, SessionIds = new[] { a.Id }, Muted = a.Muted });
                var session = Read().Sessions.Find(item => item.Id == a.Id);
                if (session == null || Math.Abs(session.Level - a.Level) > .001 || session.Muted != a.Muted) throw new InvalidOperationException("Leitura divergente apos teste da sessao " + a.AppName);
                report.AppendLine("OK setters de sessao, valor preservado: " + a.AppName);
            }
            using (var scope = new ComScope())
            {
                var e = Enumerator(scope);
                var policy = scope.Own((IPolicyConfig)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))));
                for (int flow = 0; flow < 2; flow++) for (int role = 0; role < 3; role++)
                {
                    string id = DefaultId(e, flow, role); if (id == null) continue;
                    Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, role));
                    if (DefaultId(e, flow, role) != id) throw new InvalidOperationException("Padrão divergente apos teste da interface de selecao.");
                }
                report.AppendLine("OK selecao: interface aceita os seis padroes atuais, sem trocar dispositivos.");
            }
            return report.ToString();
        }

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
        [ComImport, Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMEndpoint { void GetDataFlow(out int flow); }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPropertyStore
        { void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out PropVariant value); void SetValue(ref PropertyKey key, ref PropVariant value); void Commit(); }
        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioEndpointVolume
        {
            void RegisterControlChangeNotify(IntPtr c); void UnregisterControlChangeNotify(IntPtr c); void GetChannelCount(out uint count);
            void SetMasterVolumeLevel(float value, ref Guid context); void SetMasterVolumeLevelScalar(float value, ref Guid context);
            void GetMasterVolumeLevel(out float value); void GetMasterVolumeLevelScalar(out float value);
            void SetChannelVolumeLevel(uint channel, float value, ref Guid context); void SetChannelVolumeLevelScalar(uint channel, float value, ref Guid context);
            void GetChannelVolumeLevel(uint channel, out float value); void GetChannelVolumeLevelScalar(uint channel, out float value);
            void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
            void GetVolumeStepInfo(out uint step, out uint count); void VolumeStepUp(ref Guid c); void VolumeStepDown(ref Guid c); void QueryHardwareSupport(out uint mask); void GetVolumeRange(out float min, out float max, out float step);
        }
        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionManager2
        {
            void GetAudioSessionControl(IntPtr guid, uint flags, out IAudioSessionControl control); void GetSimpleAudioVolume(IntPtr guid, uint flags, out ISimpleAudioVolume volume);
            void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
            void RegisterSessionNotification(IntPtr notification); void UnregisterSessionNotification(IntPtr notification); void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr notification); void UnregisterDuckNotification(IntPtr notification);
        }
        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionEnumerator
        { void GetCount(out int count); void GetSession(int index, out IAudioSessionControl control); }
        [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionControl
        {
            void GetState(out int state); void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name); void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid c);
            void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path); void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid c);
            void GetGroupingParam(out Guid group); void SetGroupingParam(ref Guid group, ref Guid c); void RegisterAudioSessionNotification(IntPtr n); void UnregisterAudioSessionNotification(IntPtr n);
        }
        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionControl2
        {
            void GetState(out int state); void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name); void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid c);
            void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path); void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid c);
            void GetGroupingParam(out Guid group); void SetGroupingParam(ref Guid group, ref Guid c); void RegisterAudioSessionNotification(IntPtr n); void UnregisterAudioSessionNotification(IntPtr n);
            void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id); void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            void GetProcessId(out uint pid); [PreserveSig] int IsSystemSoundsSession(); void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }
        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISimpleAudioVolume
        { void SetMasterVolume(float level, ref Guid c); void GetMasterVolume(out float level); void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid c); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute); }
        [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat(); [PreserveSig] int GetDeviceFormat(); [PreserveSig] int ResetDeviceFormat(); [PreserveSig] int SetDeviceFormat();
            [PreserveSig] int GetProcessingPeriod(); [PreserveSig] int SetProcessingPeriod(); [PreserveSig] int GetShareMode(); [PreserveSig] int SetShareMode();
            [PreserveSig] int GetPropertyValue(); [PreserveSig] int SetPropertyValue();
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
            [PreserveSig] int SetEndpointVisibility();
        }
    }
}
