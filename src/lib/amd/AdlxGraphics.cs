// Ajustes graficos globais da AMD pelo ADLX do driver (amdadlx64.dll), sem SDK: vtables C++ pelas posicoes dos cabecalhos
// publicos do SDK ADLX (GPUOpen, MIT), conferidas por sonda somente leitura (docs/evidencias/2026-10-29-amd-fase2-adlx,
// docs/evidencias/2026-11-06-amd-opcoes-globais; ADRs 0082 e 0100).
// Fora de escopo: ajuste de GPU (overclock, ventoinha, energia), SmartShift, SAM e as opcoes que so existem no registro do driver.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace XGBP.Amd
{
    public sealed class GraphicsFeature
    {
        public string Key;
        public bool Supported, Enabled;
        public int Error;
        // Faixa do valor ajustavel (sliders); -1 sem faixa.
        public int Min = -1, Max = -1, Step = -1;
        // Valores lidos por campo (modo, nivel, nitidez, FPS...). Campo ausente: o driver nao entregou.
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public int Get(string field) { int v; return Values.TryGetValue(field, out v) ? v : -1; }
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
        // Onde cada recurso mora: servico (0 base, 1-3 Services1-3, 4 multimidia), posicao do Get e se pede a GPU;
        // posicoes de IsSupported, IsEnabled (-1 sem), SetEnabled (-1 sem interruptor), GetRange (-1 sem) e dos campos.
        sealed class Feature
        {
            public string Key; public int Service, Getter, Supported = 3, Enabled = 4, Set = -1, Range = -1; public bool PerGpu;
            public Field[] Fields = new Field[0];
        }
        // Campo inteiro: posicao do Get e do Set. Extended: mora na interface estendida do recurso (IADLX3DAMDFluidMotionFrames1).
        sealed class Field
        {
            public string Name; public int Get, Set; public bool Extended;
            public Field(string name, int get, int set, bool extended = false) { Name = name; Get = get; Set = set; Extended = extended; }
        }
        static readonly Feature[] Features = {
            new Feature { Key = "fsr", Service = 3, Getter = 19, PerGpu = true, Set = 5 },
            new Feature { Key = "framegen", Service = 3, Getter = 20, PerGpu = true, Supported = 5, Enabled = 6, Set = 7 },
            new Feature { Key = "afmf", Service = 1, Getter = 17, Set = 5, Fields = new[] {
                new Field("search", 9, 10, true), new Field("performance", 11, 12, true), new Field("motion", 13, 14, true) } },
            new Feature { Key = "rsr", Service = 0, Getter = 14, Set = 5, Range = 6, Fields = new[] { new Field("sharpness", 7, 8) } },
            new Feature { Key = "antilag", Service = 0, Getter = 3, PerGpu = true, Set = 5 },
            new Feature { Key = "boost", Service = 0, Getter = 5, PerGpu = true, Set = 7, Range = 5, Fields = new[] { new Field("resolution", 6, 8) } },
            new Feature { Key = "chill", Service = 0, Getter = 4, PerGpu = true, Set = 8, Range = 5, Fields = new[] { new Field("min", 6, 9), new Field("max", 7, 10)
                } },
            new Feature { Key = "frtc", Service = 0, Getter = 9, PerGpu = true, Set = 7, Range = 5, Fields = new[] { new Field("fps", 6, 8) } },
            new Feature { Key = "vsync", Service = 0, Getter = 8, PerGpu = true, Fields = new[] { new Field("mode", 5, 6) } },
            new Feature { Key = "enhancedsync", Service = 0, Getter = 7, PerGpu = true, Set = 5 },
            new Feature { Key = "sharpening", Service = 0, Getter = 6, PerGpu = true, Set = 7, Range = 5, Fields = new[] { new Field("sharpness", 6, 8) } },
            new Feature { Key = "sharpdesktop", Service = 2, Getter = 18, PerGpu = true, Set = 5 },
            new Feature { Key = "video", Service = 4, Getter = 4, PerGpu = true, Set = 7, Range = 5, Fields = new[] { new Field("sharpness", 6, 8) } },
            new Feature { Key = "aa", Service = 0, Getter = 10, PerGpu = true, Enabled = -1, Fields = new[] {
                new Field("mode", 4, 7), new Field("level", 5, 8), new Field("method", 6, 9) } },
            new Feature { Key = "mlaa", Service = 0, Getter = 11, PerGpu = true, Set = 5 },
            new Feature { Key = "aniso", Service = 0, Getter = 12, PerGpu = true, Set = 6, Fields = new[] { new Field("level", 5, 7) } },
        new Feature { Key = "tess", Service = 0, Getter = 13, PerGpu = true, Enabled = -1, Fields = new[] { new Field("mode", 4, 6), new Field("level", 5, 7) } },
            new Feature { Key = "resetcache", Service = 0, Getter = 15, PerGpu = true, Enabled = -1 } };
        public static string[] Keys { get { return Array.ConvertAll(Features, f => f.Key); } }
        // Recursos com interruptor (SetEnabled); os demais so tem campos ou acao.
        public static string[] Toggles { get { return Array.ConvertAll(Array.FindAll(Features, f => f.Set >= 0), f => f.Key); } }

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
                var services = new IntPtr[5]; IntPtr gpu = IntPtr.Zero;
                try
                {
                    gpu = SelectGpu(out state.Gpu);
                    OpenServices(services);
                    foreach (var f in Features) state.Features.Add(ReadFeature(f, services[f.Service], gpu));
                }
                finally { ReleaseServices(services); Release(gpu); }
                return state;
            }
        }
        public static void SetEnabled(string key, bool enable)
        {
            var f = Find(key);
            if (f.Set < 0) throw new ArgumentException("O recurso " + key + " não tem interruptor.");
            WithFeature(f, false, feature => Accept(Call<SetBool>(feature, f.Set)(feature, (byte)(enable ? 1 : 0)), "O driver da AMD recusou a mudança"));
        }
        // Modo, nivel, nitidez ou FPS: um campo por vez; o painel rele tudo depois.
        public static void SetValue(string key, string field, int value)
        {
            var f = Find(key);
            var c = Array.Find(f.Fields, x => x.Name == field);
            if (c == null) throw new ArgumentException("Campo desconhecido: " + key + "." + field);
            WithFeature(f, c.Extended, feature => Accept(Call<SetInt>(feature, c.Set)(feature, value), "O driver da AMD recusou o valor"));
        }
        public static void ResetShaderCache()
        {
            var f = Find("resetcache");
            WithFeature(f, false, feature => {
                byte supported;
                if (Call<GetBool>(feature, f.Supported)(feature, out supported) != 0 || supported == 0) throw new InvalidOperationException(
                    "Este driver não permite redefinir o cache do sombreador.");
                Accept(Call<ActionFn>(feature, 4)(feature), "O driver da AMD não redefiniu o cache");
            });
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

        static Feature Find(string key)
        {
            var f = Array.Find(Features, x => x.Key == key);
            if (f == null) throw new ArgumentException("Recurso desconhecido: " + key);
            return f;
        }
        static void Accept(int result, string prefix) { if (result != 0 && result != 1) throw new InvalidOperationException(Describe(result, prefix)); }
        // Abre o recurso (ou a interface estendida dele), executa e libera tudo; serializado com as leituras.
        static void WithFeature(Feature f, bool extended, Action<IntPtr> work)
        {
            lock (Gate)
            {
                Open();
                var services = new IntPtr[5]; IntPtr gpu = IntPtr.Zero, feature = IntPtr.Zero, target = IntPtr.Zero;
                try
                {
                    string name; gpu = SelectGpu(out name);
                    OpenServices(services);
                    if (services[f.Service] == IntPtr.Zero) throw new InvalidOperationException(Describe(6, "O driver da AMD não oferece " + f.Key));
                    feature = Feature3D(f, services[f.Service], gpu);
                    target = extended ? Query(feature, ExtendedId(f)) : feature;
                    if (target == IntPtr.Zero) throw new InvalidOperationException(Describe(6, "O driver da AMD não oferece esta opção de " + f.Key));
                    work(target);
                }
                finally { if (target != feature) Release(target); Release(feature); ReleaseServices(services); Release(gpu); }
            }
        }
        static string ExtendedId(Feature f) { return f.Key == "afmf" ? "IADLX3DAMDFluidMotionFrames1" : null; }
        // 0 base, 1-3 servicos 3D estendidos (QueryInterface), 4 multimidia (IADLXSystem2). Ausente fica zero.
        static void OpenServices(IntPtr[] services)
        {
            Check(Call<GetIface>(system, 7)(system, out services[0]), "serviços 3D");
            services[1] = Query(services[0], "IADLX3DSettingsServices1");
            services[2] = Query(services[0], "IADLX3DSettingsServices2");
            services[3] = Query(services[0], "IADLX3DSettingsServices3");
            IntPtr system2 = Query(system, "IADLXSystem2"), media = IntPtr.Zero;
            if (system2 != IntPtr.Zero && Call<GetIface>(system2, 4)(system2, out media) == 0) services[4] = media;
            Release(system2);
        }
        static void ReleaseServices(IntPtr[] services) { for (int i = services.Length - 1; i >= 0; i--) { Release(services[i]); services[i] = IntPtr.Zero; } }
        static IntPtr Query(IntPtr obj, string id)
        {
            IntPtr result; if (obj == IntPtr.Zero || id == null) return IntPtr.Zero;
            return Call<QueryFn>(obj, 2)(obj, id, out result) == 0 ? result : IntPtr.Zero;
        }

        static GraphicsFeature ReadFeature(Feature f, IntPtr service, IntPtr gpu)
        {
            var result = new GraphicsFeature { Key = f.Key };
            if (service == IntPtr.Zero) { result.Error = 6; return result; } // ADLX_UNKNOWN_INTERFACE: driver sem o servico
            IntPtr feature = IntPtr.Zero, extended = IntPtr.Zero;
            try
            {
                int r = f.PerGpu ? Call<GetIfaceGpu>(service, f.Getter)(service, gpu, out feature) : Call<GetIface>(service, f.Getter)(service, out feature);
                if (r != 0 || feature == IntPtr.Zero) { result.Error = r == 0 ? 3 : r; return result; }
                byte flag;
                if ((r = Call<GetBool>(feature, f.Supported)(feature, out flag)) != 0) { result.Error = r; return result; }
                result.Supported = flag != 0;
                if (!result.Supported) return result;
                if (f.Enabled >= 0)
                {
                    if ((r = Call<GetBool>(feature, f.Enabled)(feature, out flag)) != 0) { result.Error = r; return result; }
                    result.Enabled = flag != 0;
                }
                IntRange range;
                if (f.Range >= 0 && Call<GetRange>(feature, f.Range)(feature, out range) == 0 && range.Max >= range.Min)
                { result.Min = range.Min; result.Max = range.Max; result.Step = Math.Max(1, range.Step); }
                foreach (var c in f.Fields)
                {
                    if (c.Extended && extended == IntPtr.Zero) extended = Query(feature, ExtendedId(f));
                    IntPtr target = c.Extended ? extended : feature; int value;
                    if (target != IntPtr.Zero && Call<GetInt>(target, c.Get)(target, out value) == 0) result.Values[c.Name] = value;
                }
                return result;
            }
            finally { Release(extended); Release(feature); }
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
                    if (score > rank) { Release(chosen); chosen = gpu; rank = score; name = Call<GetText>(gpu, 7)(gpu, out text) == 0 ? Marshal.PtrToStringAnsi(text) :
                        "GPU AMD"; }
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
            if (module == IntPtr.Zero) { retryAt = DateTime.UtcNow.AddSeconds(30); initError = -2; throw new InvalidOperationException(
                "O ADLX da AMD não está instalado neste PC."); }
            var full = Export<QueryFullVersionFn>("ADLXQueryFullVersion"); var init = Export<InitializeFn>("ADLXInitialize");
            terminate = Export<TerminateFn>("ADLXTerminate");
            ulong version; int r = full(out version);
            IntPtr created = IntPtr.Zero;
            if (r == 0) r = init(version, out created);
            if (r != 0 || created == IntPtr.Zero) { initError = r; retryAt = DateTime.UtcNow.AddSeconds(30); throw new InvalidOperationException(Describe(r,
                "O driver da AMD não iniciou o ADLX")); }
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
        static void Check(int result, string what) { if (result != 0) throw new InvalidOperationException(Describe(result, "Falha ao ler " + what +
            " no driver da AMD")); }
        static string Describe(int result, string prefix)
        {
            string reason = result == 12 ? "não suportado" : result == 6 ? "interface ausente nesta versão do driver" : result == 5 ? "versão incompatível" :
        result == 4 ? "valor inválido" : result == 8 ? "ADL não iniciou" : result == 14 ? "GPU inativa" : result == 15 ? "GPU em uso" : "código " + result;
            return prefix + " (" + reason + ").";
        }

        [StructLayout(LayoutKind.Sequential)] struct IntRange { public int Min, Max, Step; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int QueryFullVersionFn(out ulong version);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int InitializeFn(ulong version, out IntPtr system);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int TerminateFn();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetIface(IntPtr self, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetIfaceGpu(IntPtr self, IntPtr gpu, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QueryFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetBool(IntPtr self, out byte value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetBool(IntPtr self, byte value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetInt(IntPtr self, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetInt(IntPtr self, int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetRange(IntPtr self, out IntRange value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ActionFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDouble(IntPtr self, out double value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetText(IntPtr self, out IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint SizeFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int AtFn(IntPtr self, uint index, out IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ReleaseFn(IntPtr self);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    }
}
