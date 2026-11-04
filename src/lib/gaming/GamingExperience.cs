// API oficial do modo Xbox (Gaming Full Screen Experience), api-ms-win-gaming-experience-l1-1-0.
// Assinaturas de gamingexperience.h (Windows SDK 10.0.26100). Usada para a regra dos dois modos (ADR 0005).
// Compilado com o csc do .NET Framework 4.x (C# 5).

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace XGBP.Gaming
{
    public static class GamingExperience
    {
        const string Dll = "api-ms-win-gaming-experience-l1-1-0.dll";

        [DllImport(Dll)] static extern bool IsGamingFullScreenExperienceSupported();
        [DllImport(Dll)] static extern bool IsGamingFullScreenExperienceActive();
        [DllImport(Dll)] static extern bool CanSetGamingFullScreenExperience();
        [DllImport(Dll)] static extern int SetGamingFullScreenExperience([MarshalAs(UnmanagedType.Bool)] bool active);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate void ChangeRoutine(IntPtr context);

        [DllImport(Dll)] static extern int RegisterGamingFullScreenExperienceChangeNotification(ChangeRoutine routine, IntPtr context, out IntPtr registration);
        [DllImport(Dll)] static extern void UnregisterGamingFullScreenExperienceChangeNotification(IntPtr registration);

        // null = API indisponivel neste Windows.
        public static bool? Supported() { try { return IsGamingFullScreenExperienceSupported(); } catch (Exception) { return null; } }
        public static bool? Active() { try { return IsGamingFullScreenExperienceActive(); } catch (Exception) { return null; } }
        public static bool? CanSet() { try { return CanSetGamingFullScreenExperience(); } catch (Exception) { return null; } }
        // Somente por pedido do usuario; nunca entra no modo Xbox nem altera configuracao persistente.
        public static void Exit()
        {
            bool? active = Active();
            if (active == false) return;
            if (active != true) throw new InvalidOperationException("Não foi possível consultar o modo Xbox do Windows.");
            if (CanSet() != true) throw new InvalidOperationException("O Windows não permite sair do modo Xbox neste momento.");
            int result = SetGamingFullScreenExperience(false);
            if (result < 0) throw new InvalidOperationException("O Windows recusou sair do modo Xbox.", Marshal.GetExceptionForHR(result));
        }

        public static int Register(ChangeRoutine routine, out IntPtr registration)
        {
            registration = IntPtr.Zero;
            try { return RegisterGamingFullScreenExperienceChangeNotification(routine, IntPtr.Zero, out registration); }
            catch (Exception) { return unchecked((int)0x80004001); } // E_NOTIMPL
        }

        public static void Unregister(IntPtr registration)
        {
            if (registration == IntPtr.Zero) return;
            try { UnregisterGamingFullScreenExperienceChangeNotification(registration); } catch (Exception) { }
        }
    }

    // Avisa quando o modo Xbox liga ou desliga: pela notificacao oficial e, como conferencia, por consulta
    // periodica (PollMs). Source diz quem percebeu primeiro: "notificacao" ou "consulta".
    public sealed class ModeWatcher : IDisposable
    {
        public event Action<bool, string> Changed;   // ativo, origem
        public event Action<bool?, string> StateChanged; // inclui API indisponivel: consumidores devem desativar.
        public event Action<string> Log;
        public int PollMs = 1000;

        readonly object gate = new object();
        GamingExperience.ChangeRoutine routine;     // mantido vivo enquanto registrado
        IntPtr registration;
        Timer poll;
        bool? last;

        public void Start()
        {
            last = GamingExperience.Active();
            routine = delegate (IntPtr ctx) { Check("notificacao"); };
            int hr = GamingExperience.Register(routine, out registration);
            Emit(string.Format("notificacao registrada: hr=0x{0:X8}", hr));
            poll = new Timer(delegate { Check("consulta"); }, null, PollMs, PollMs);
        }

        public bool? Current { get { lock (gate) return last; } }

        void Check(string source)
        {
            bool? now = GamingExperience.Active();
            bool changed;
            lock (gate)
            {
                changed = now != last;
                last = now;
            }
            if (source == "notificacao") Emit("notificacao recebida: ativo=" + Text(now));
            if (changed)
            {
                var state = StateChanged;
                if (state != null) state(now, source);
                var h = Changed;
                if (h != null && now.HasValue) h(now.Value, source);
            }
        }

        static string Text(bool? b) { return b.HasValue ? (b.Value ? "sim" : "nao") : "indisponivel"; }

        void Emit(string text)
        {
            var h = Log;
            if (h != null) h(text);
        }

        public void Dispose()
        {
            if (poll != null) { poll.Dispose(); poll = null; }
            GamingExperience.Unregister(registration);
            registration = IntPtr.Zero;
        }
    }
}
