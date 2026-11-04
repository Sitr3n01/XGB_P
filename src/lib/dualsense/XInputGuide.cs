// Botao Guia dos controles XInput, como o Xbox 360 virtual do DSX em emulacao Xbox (ADR 0080).
// So leitura: XInputGetStateEx (ordinal 100 do xinput1_4.dll), o unico que expoe o Guia. Sem vibracao nem saida.
// Na emulacao DualSense nao existe controle XInput (ADR 0004): o leitor fica so na varredura lenta.
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace XGBP.Input
{
    public sealed class XInputGuideMonitor
    {
        public const ushort GuideMask = 0x0400;
        public event Action<ButtonEvent> ButtonChanged;
        public event Action<string> DeviceLost;
        public event Action<string> Log;
        public event Action<bool> PresenceChanged; // algum controle XInput conectado ou nao
        public int PollMs = 8, ScanIntervalMs = 2000;
        // Substituiveis na verificacao: botoes do slot (null = desconectado) e relogio dos eventos.
        public Func<int, ushort?> ReadButtons = ReadSlot;
        public Func<DateTime> Now = delegate { return DateTime.Now; };
        readonly bool[] connected = new bool[4], down = new bool[4];
        readonly DateTime[] pressedAt = new DateTime[4];
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        DateTime nextScan = DateTime.MinValue;
        Thread thread;
        volatile bool any;

        public bool AnyConnected { get { return any; } }
        public void Start()
        {
            thread = new Thread(Loop) { IsBackground = true, Name = "XInput Guia" };
            thread.Start();
        }
        public void Stop()
        {
            stop.Set();
            if (thread != null) thread.Join(2000);
        }
        void Loop()
        {
            do
            {
                try { Poll(); }
                catch (Exception ex)
                {
                    Emit("xinput: leitura falhou | " + ex.Message);
                    if (ex is DllNotFoundException || ex is EntryPointNotFoundException) return; // sem XInput neste sistema
                }
            } while (!stop.WaitOne(any ? PollMs : 250));
        }
        // Uma passada. Slots conectados sempre; desconectados so a cada ScanIntervalMs, porque consultar slot vazio e lento.
        public void Poll()
        {
            DateTime now = Now(); bool scan = now >= nextScan;
            if (scan) nextScan = now.AddMilliseconds(ScanIntervalMs);
            for (int slot = 0; slot < 4; slot++)
            {
                if (!connected[slot] && !scan) continue;
                ushort? buttons = ReadButtons(slot);
                string label = "XInput#" + slot;
                if (buttons == null)
                {
                    if (!connected[slot]) continue;
                    connected[slot] = down[slot] = false; // sumir nao prova soltura: quem consome trata como perda
                    Emit("xinput: controle desconectado | " + label);
                    var lost = DeviceLost; if (lost != null) lost(label);
                    UpdatePresence();
                    continue;
                }
                if (!connected[slot]) { connected[slot] = true; Emit("xinput: controle conectado | " + label); UpdatePresence(); }
                bool guide = (buttons.Value & GuideMask) != 0;
                if (guide == down[slot]) continue;
                down[slot] = guide;
                var e = new ButtonEvent { Time = now, Device = label, Button = "PS", Down = guide };
                if (guide) pressedAt[slot] = now; else e.DurationMs = (int)(now - pressedAt[slot]).TotalMilliseconds;
                var changed = ButtonChanged; if (changed != null) changed(e);
            }
        }
        void UpdatePresence()
        {
            bool now = Array.IndexOf(connected, true) >= 0;
            if (now == any) return;
            any = now;
            var presence = PresenceChanged; if (presence != null) presence(now);
        }
        void Emit(string text) { var log = Log; if (log != null) log(text); }

        static ushort? ReadSlot(int slot)
        {
            XInputState state;
            return XInputGetStateEx(slot, out state) == 0 ? (ushort?)state.Buttons : null;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct XInputState
        {
            public uint PacketNumber;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
            public uint Reserved; // o ordinal 100 pode escrever 4 bytes alem do XINPUT_STATE publico
        }
        [DllImport("xinput1_4.dll", EntryPoint = "#100")] static extern uint XInputGetStateEx(int index, out XInputState state);
    }
}
