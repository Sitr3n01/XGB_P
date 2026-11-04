// Ajustes graficos globais da AMD pelo ADLX do driver (amdadlx64.dll), sem SDK: vtables C++ pelas posicoes dos cabecalhos
// publicos do SDK ADLX (GPUOpen, MIT), conferidas por sonda somente leitura (docs/evidencias/2026-10-29-amd-fase2-adlx, ADR 0082).
// Fora de escopo: ajuste de GPU (overclock, ventoinha, energia), SmartShift e SAM.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace XGBP.Amd
{
    public sealed class GraphicsFeature
    {
        public string Key;
        public bool Supported, Enabled;
        public int Value = -1, Value2 = -1, Error;
    }
    public sealed class GraphicsState
    {
        public string Gpu;
        public List<GraphicsFeature> Features = new List<GraphicsFeature>();
        public GraphicsFeature Find(string key) { return Features.Find(f => f.Key == key); }
    }
    // Metricas da AMD: so logica (o painel Desempenho ja mostra o mesmo, pedido do usuario).
    public sealed class GpuMetrics
    {
        public double Usage = -1, Temperature = -1, BoardPower = -1;
        public int Clock = -1, Fan = -1, Vram = -1;
    }
    public static class AdlxGraphics
    {
        // Onde cada recurso mora: servico (0 base, 1 Services1, 3 Services3), posicao do Get, se pede a GPU,
        // e as posicoes de IsSupported, IsEnabled, SetEnabled e dos valores (-1 sem valor).
        sealed class Feature
        {
            public string Key; public int Service, Getter, Supported, Enabled, Set, Value = -1, Value2 = -1; public bool PerGpu;
        }
        static readonly Feature[] Features = {
            new Feature { Key = "fsr", Service = 3, Getter = 19, PerGpu = true, Supported = 3, Enabled = 4, Set = 5 },
            new Feature { Key = "framegen", Service = 3, Getter = 20, PerGpu = true, Supported = 5, Enabled = 6, Set = 7 },
            new Feature { Key = "afmf", Service = 1, Getter = 17, Supported = 3, Enabled = 4, Set = 5 },
            new Feature { Key = "rsr", Service = 0, Getter = 14, Supported = 3, Enabled = 4, Set = 5, Value = 7 },
            new Feature { Key = "antilag", Service = 0, Getter = 3, PerGpu = true, Supported = 3, Enabled = 4, Set = 5 },
            new Feature { Key = "boost", Service = 0, Getter = 5, PerGpu = true, Supported = 3, Enabled = 4, Set = 7, Value = 6 },
            new Feature { Key = "chill", Service = 0, Getter = 4, PerGpu = true, Supported = 3, Enabled = 4, Set = 8, Value = 6, Value2 = 7 },
            new Feature { Key = "frtc", Service = 0, Getter = 9, PerGpu = true, Supported = 3, Enabled = 4, Set = 7, Value = 6 },
            new Feature { Key = "sharpening", Service = 0, Getter = 6, PerGpu = true, Supported = 3, Enabled = 4, Set = 7, Value = 6 },
            new Feature { Key = "enhancedsync", Service = 0, Getter = 7, PerGpu = true, Supported = 3, Enabled = 4, Set = 5 } };
        public static string[] Keys { get { return Array.ConvertAll(Features, f => f.Key); } }

        static readonly object Gate = new object();
        static IntPtr module, system;
        static TerminateFn terminate;
        static int initError = -1;
        static DateTime retryAt = DateTime.MinValue;

        public static GraphicsState Read()
        {
            lock (Gate)
            {
                Open();
                var state = new GraphicsState();
                IntPtr gpu = IntPtr.Zero, base3d = IntPtr.Zero, s1 = IntPtr.Zero, s3 = IntPtr.Zero;
                try
                {
                    gpu = SelectGpu(out state.Gpu);
                    Check(Call<GetIface>(system, 7)(system, out base3d), "serviços 3D");
                    Call<QueryFn>(base3d, 2)(base3d, "IADLX3DSettingsServices1", out s1);
                    Call<QueryFn>(base3d, 2)(base3d, "IADLX3DSettingsServices3", out s3);
                    foreach (var f in Features) state.Features.Add(ReadFeature(f, f.Service == 0 ? base3d : f.Service == 1 ? s1 : s3, gpu));
                }
                finally { Release(s3); Release(s1); Release(base3d); Release(gpu); }
                return state;
            }
        }
        public static void SetEnabled(string key, bool enable)
        {
            var f = Array.Find(Features, x => x.Key == key);
            if (f == null) throw new ArgumentException("Recurso desconhecido: " + key);
            lock (Gate)
            {
                Open();
                IntPtr gpu = IntPtr.Zero, base3d = IntPtr.Zero, service = IntPtr.Zero, feature = IntPtr.Zero;
                try
                {
                    string name; gpu = SelectGpu(out name);
                    Check(Call<GetIface>(system, 7)(system, out base3d), "serviços 3D");
                    if (f.Service == 0) service = base3d;
                    else Check(Call<QueryFn>(base3d, 2)(base3d, f.Service == 1 ? "IADLX3DSettingsServices1" : "IADLX3DSettingsServices3", out service), "serviços estendidos");
                    feature = Feature3D(f, service, gpu);
                    int result = Call<SetBool>(feature, f.Set)(feature, (byte)(enable ? 1 : 0));
                    if (result != 0 && result != 1) throw new InvalidOperationException(Describe(result, "O driver da AMD recusou a mudança"));
                }
                finally { Release(feature); if (service != base3d) Release(service); Release(base3d); Release(gpu); }
            }
        }
        public static GpuMetrics ReadMetrics()
        {
            lock (Gate)
            {
                Open();
                var m = new GpuMetrics(); IntPtr gpu = IntPtr.Zero, perf = IntPtr.Zero, metrics = IntPtr.Zero;
                try
                {
                    string name; gpu = SelectGpu(out name);
                    Check(Call<GetIface>(system, 9)(system, out perf), "métricas");
                    Check(Call<GetIfaceGpu>(perf, 18)(perf, gpu, out metrics), "métricas da GPU");
                    double d; int i;
                    if (Call<GetDouble>(metrics, 4)(metrics, out d) == 0) m.Usage = d;
                    if (Call<GetInt>(metrics, 5)(metrics, out i) == 0) m.Clock = i;
                    if (Call<GetDouble>(metrics, 7)(metrics, out d) == 0) m.Temperature = d;
                    if (Call<GetDouble>(metrics, 10)(metrics, out d) == 0) m.BoardPower = d;
                    if (Call<GetInt>(metrics, 11)(metrics, out i) == 0) m.Fan = i;
                    if (Call<GetInt>(metrics, 12)(metrics, out i) == 0) m.Vram = i;
                }
                finally { Release(metrics); Release(perf); Release(gpu); }
                return m;
            }
        }
        // So existe com jogo 3D em tela cheia exclusiva (documentacao do ADLX); senao, null.
        public static int? ReadFps()
        {
            lock (Gate)
            {
                Open();
                IntPtr perf = IntPtr.Zero, fps = IntPtr.Zero;
                try
                {
                    Check(Call<GetIface>(system, 9)(system, out perf), "métricas");
                    if (Call<GetIface>(perf, 20)(perf, out fps) != 0 || fps == IntPtr.Zero) return null;
                    int value; return Call<GetInt>(fps, 4)(fps, out value) == 0 ? (int?)value : null;
                }
                finally { Release(fps); Release(perf); }
            }
        }
        public static void Shutdown()
        {
            lock (Gate)
            {
                if (system != IntPtr.Zero && terminate != null) try { terminate(); } catch (Exception) { }
                system = IntPtr.Zero;
            }
        }

        static GraphicsFeature ReadFeature(Feature f, IntPtr service, IntPtr gpu)
        {
            var result = new GraphicsFeature { Key = f.Key };
            if (service == IntPtr.Zero) { result.Error = 6; return result; } // ADLX_UNKNOWN_INTERFACE: driver sem o servico
            IntPtr feature = IntPtr.Zero;
            try
            {
                int r = f.PerGpu ? Call<GetIfaceGpu>(service, f.Getter)(service, gpu, out feature) : Call<GetIface>(service, f.Getter)(service, out feature);
                if (r != 0 || feature == IntPtr.Zero) { result.Error = r == 0 ? 3 : r; return result; }
                byte flag;
                if ((r = Call<GetBool>(feature, f.Supported)(feature, out flag)) != 0) { result.Error = r; return result; }
                result.Supported = flag != 0;
                if (!result.Supported) return result;
                if ((r = Call<GetBool>(feature, f.Enabled)(feature, out flag)) != 0) { result.Error = r; return result; }
                result.Enabled = flag != 0;
                int value;
                if (f.Value >= 0 && Call<GetInt>(feature, f.Value)(feature, out value) == 0) result.Value = value;
                if (f.Value2 >= 0 && Call<GetInt>(feature, f.Value2)(feature, out value) == 0) result.Value2 = value;
                return result;
            }
            finally { Release(feature); }
        }
        static IntPtr Feature3D(Feature f, IntPtr service, IntPtr gpu)
        {
            IntPtr feature;
            int r = f.PerGpu ? Call<GetIfaceGpu>(service, f.Getter)(service, gpu, out feature) : Call<GetIface>(service, f.Getter)(service, out feature);
            Check(r, "recurso " + f.Key);
            if (feature == IntPtr.Zero) throw new InvalidOperationException("O driver da AMD não entregou o recurso " + f.Key + ".");
            return feature;
        }
        // A placa de video: discreta e com telas; sem ela, a primeira com telas, depois a primeira.
        static IntPtr SelectGpu(out string name)
        {
            IntPtr list; Check(Call<GetIface>(system, 1)(system, out list), "lista de GPUs");
            IntPtr chosen = IntPtr.Zero; int rank = -1; name = null;
            try
            {
                uint count = Call<SizeFn>(list, 3)(list);
                for (uint i = 0; i < count; i++)
                {
                    IntPtr gpu; if (Call<AtFn>(list, 11)(list, i, out gpu) != 0 || gpu == IntPtr.Zero) continue;
                    int type; byte desktops; IntPtr text;
                    Call<GetInt>(gpu, 5)(gpu, out type); Call<GetBool>(gpu, 10)(gpu, out desktops);
                    int score = (type == 2 ? 2 : 0) + (desktops != 0 ? 1 : 0);
                    if (score > rank) { Release(chosen); chosen = gpu; rank = score; name = Call<GetText>(gpu, 7)(gpu, out text) == 0 ? Marshal.PtrToStringAnsi(text) : "GPU AMD"; }
                    else Release(gpu);
                }
            }
            finally { Release(list); }
            if (chosen == IntPtr.Zero) throw new InvalidOperationException("Nenhuma GPU AMD encontrada pelo driver.");
            return chosen;
        }
        static void Open()
        {
            if (system != IntPtr.Zero) return;
            if (DateTime.UtcNow < retryAt) throw new InvalidOperationException(Describe(initError, "O driver da AMD não iniciou o ADLX"));
            if (module == IntPtr.Zero) module = LoadLibrary(Environment.ExpandEnvironmentVariables(@"%WINDIR%\System32\amdadlx64.dll"));
            if (module == IntPtr.Zero) { retryAt = DateTime.UtcNow.AddSeconds(30); initError = -2; throw new InvalidOperationException("O ADLX da AMD não está instalado neste PC."); }
            var full = Export<QueryFullVersionFn>("ADLXQueryFullVersion"); var init = Export<InitializeFn>("ADLXInitialize");
            terminate = Export<TerminateFn>("ADLXTerminate");
            ulong version; int r = full(out version);
            IntPtr created = IntPtr.Zero;
            if (r == 0) r = init(version, out created);
            if (r != 0 || created == IntPtr.Zero) { initError = r; retryAt = DateTime.UtcNow.AddSeconds(30); throw new InvalidOperationException(Describe(r, "O driver da AMD não iniciou o ADLX")); }
            system = created; initError = -1;
        }
        static T Export<T>(string name) where T : class
        {
            IntPtr p = GetProcAddress(module, name);
            if (p == IntPtr.Zero) throw new InvalidOperationException("O ADLX instalado não exporta " + name + ".");
            return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
        }
        static readonly Dictionary<long, Delegate> Cache = new Dictionary<long, Delegate>();
        static T Call<T>(IntPtr obj, int slot) where T : class
        {
            IntPtr fn = Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size);
            long key = fn.ToInt64() ^ ((long)typeof(T).GetHashCode() << 1);
            Delegate d;
            if (!Cache.TryGetValue(key, out d) || d.GetType() != typeof(T)) Cache[key] = d = Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
            return (T)(object)d;
        }
        static void Release(IntPtr obj) { if (obj != IntPtr.Zero) Call<ReleaseFn>(obj, 1)(obj); }
        static void Check(int result, string what) { if (result != 0) throw new InvalidOperationException(Describe(result, "Falha ao ler " + what + " no driver da AMD")); }
        static string Describe(int result, string prefix)
        {
            string reason = result == 12 ? "não suportado" : result == 6 ? "interface ausente nesta versão do driver" : result == 5 ? "versão incompatível" :
                result == 8 ? "ADL não iniciou" : result == 14 ? "GPU inativa" : result == 15 ? "GPU em uso" : "código " + result;
            return prefix + " (" + reason + ").";
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int QueryFullVersionFn(out ulong version);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int InitializeFn(ulong version, out IntPtr system);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int TerminateFn();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetIface(IntPtr self, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetIfaceGpu(IntPtr self, IntPtr gpu, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QueryFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetBool(IntPtr self, out byte value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetBool(IntPtr self, byte value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetInt(IntPtr self, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDouble(IntPtr self, out double value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetText(IntPtr self, out IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint SizeFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int AtFn(IntPtr self, uint index, out IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ReleaseFn(IntPtr self);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    }
}
