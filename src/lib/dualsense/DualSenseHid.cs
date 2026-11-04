// Biblioteca de leitura do DualSense (SOMENTE LEITURA), usada pelo leitor de diagnostico e pelos spikes.
// Abre todo HID 054C:0CE6 (DualSense) e 054C:0DF2 (DualSense Edge) em modo compartilhado so leitura,
// decodifica os relatorios USB/virtual 0x01, Bluetooth 0x31 e Bluetooth simples 0x01, e gera eventos
// de botao e de gesto (CURTO, LONGO, DUPLO e SEGURADO, este disparado enquanto o botao ainda esta apertado).
// Nunca escreve no controle, para nao brigar com a Steam nem com o DSX (ADR 0004).
// Layouts e medicoes: docs/referencia/dualsense-hid.md. Compilado com o csc do .NET Framework 4.x (C# 5).

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace XGBP.Input
{
    public struct Buttons
    {
        public int Hat;
        public bool HasAnalog;
        public byte LeftX, LeftY, RightX, RightY, TriggerLeft, TriggerRight;
        public bool Square, Cross, Circle, Triangle, L1, R1, L2, R2, Create, Options, L3, R3, PS, Touchpad, Mute;

        public Dictionary<string, bool> ToMap()
        {
            var m = new Dictionary<string, bool>();
            m["PS"] = PS; m["MUTE"] = Mute; m["TOUCHPAD"] = Touchpad; m["CREATE"] = Create; m["OPTIONS"] = Options;
            m["QUADRADO"] = Square; m["X"] = Cross; m["CIRCULO"] = Circle; m["TRIANGULO"] = Triangle;
            m["L1"] = L1; m["R1"] = R1; m["L2"] = L2; m["R2"] = R2; m["L3"] = L3; m["R3"] = R3;
            m["CIMA"] = Hat == 7 || Hat == 0 || Hat == 1;
            m["DIREITA"] = Hat == 1 || Hat == 2 || Hat == 3;
            m["BAIXO"] = Hat == 3 || Hat == 4 || Hat == 5;
            m["ESQUERDA"] = Hat == 5 || Hat == 6 || Hat == 7;
            return m;
        }

        public string Pressed()
        {
            var names = new List<string>();
            foreach (var kv in ToMap()) if (kv.Value) names.Add(kv.Key);
            return string.Join(",", names.ToArray());
        }
    }

    // Um eixo por vez: diagonal usa o dominante; histerese evita drift e troca repetida no limite.
    public sealed class StickDirectionTracker
    {
        public const double Enter = .40, Exit = .28, SwitchRatio = 1.25;
        public string Direction { get; private set; }
        public static double Normalize(byte value) { return (value - 128) / (value < 128 ? 128.0 : 127.0); }
        public string Update(byte rawX, byte rawY, bool available)
        {
            double x = available ? Normalize(rawX) : 0, y = available ? Normalize(rawY) : 0;
            double ax = Math.Abs(x), ay = Math.Abs(y);
            string candidate = ax > ay ? (x < 0 ? "ESQUERDA" : "DIREITA") : (y < 0 ? "CIMA" : "BAIXO");
            double magnitude = Math.Max(ax, ay);
            if (Direction != null)
            {
                double current = Direction == "ESQUERDA" ? -x : Direction == "DIREITA" ? x : Direction == "CIMA" ? -y : y;
                if (current >= Exit && (candidate == Direction || magnitude < Enter || magnitude < current * SwitchRatio)) return Direction;
            }
            Direction = magnitude >= Enter ? candidate : null;
            return Direction;
        }
    }

    public static class DualSenseReport
    {
        // USB/virtual 0x01 (64 B): botoes em 8, 9, 10. Bluetooth 0x31 (78 B): 9, 10, 11. Bluetooth simples 0x01: 5, 6, 7.
        public static bool Decode(byte[] r, int start, int n, out Buttons b)
        {
            b = new Buttons();
            if (r == null || start < 0 || n < 1 || start > r.Length - n) return false;
            int o;
            byte id = r[start];
            if (id == 0x01 && n == 64) o = 8;
            else if (id == 0x31 && ValidBluetoothReport(r, start, n)) o = 9;
            else if (id == 0x01 && (n == 10 || n == 78)) o = 5;
            else return false;
            int axes = start + (id == 0x31 ? 2 : 1);
            b.HasAnalog = true;
            b.LeftX = r[axes]; b.LeftY = r[axes + 1]; b.RightX = r[axes + 2]; b.RightY = r[axes + 3];
            int triggers = start + (o == 5 ? 8 : id == 0x31 ? 6 : 5);
            b.TriggerLeft = r[triggers]; b.TriggerRight = r[triggers + 1];
            o += start;
            byte b0 = r[o], b1 = r[o + 1], b2 = r[o + 2];
            b.Hat = b0 & 0x0F;
            b.Square = (b0 & 0x10) != 0; b.Cross = (b0 & 0x20) != 0; b.Circle = (b0 & 0x40) != 0; b.Triangle = (b0 & 0x80) != 0;
            b.L1 = (b1 & 0x01) != 0; b.R1 = (b1 & 0x02) != 0; b.L2 = (b1 & 0x04) != 0; b.R2 = (b1 & 0x08) != 0;
            b.Create = (b1 & 0x10) != 0; b.Options = (b1 & 0x20) != 0; b.L3 = (b1 & 0x40) != 0; b.R3 = (b1 & 0x80) != 0;
            b.PS = (b2 & 0x01) != 0; b.Touchpad = (b2 & 0x02) != 0; b.Mute = (b2 & 0x04) != 0;
            return true;
        }

        // Byte de status: USB/virtual 0x01 -> offset 53; Bluetooth 0x31 -> offset 54 (o relatorio simples nao tem).
        // Nibble baixo = nivel 0..10; nibble alto = estado da carga (0 descarregando, 1 carregando, 2 completa,
        // 0xA erro de tensao, 0xB erro de temperatura, 0xF erro de carga). Mesmo layout do driver hid-playstation.
        public static bool DecodeBattery(byte[] r, int start, int n, out Battery bat)
        {
            bat = new Battery();
            if (r == null || start < 0 || n < 1 || start > r.Length - n) return false;
            int o;
            byte id = r[start];
            if (id == 0x01 && n == 64) o = 53;
            else if (id == 0x31 && ValidBluetoothReport(r, start, n)) o = 54;
            else return false;
            byte s = r[start + o];
            bat.Raw = s;
            bat.Level = s & 0x0F;
            bat.ChargeState = (s & 0xF0) >> 4;
            if (bat.ChargeState <= 2 && bat.Level > 10) return false;
            int pct = Math.Min(bat.Level * 10 + 5, 100);
            switch (bat.ChargeState)
            {
                case 0x0: bat.ChargeText = "descarregando"; bat.Percent = pct; break;
                case 0x1: bat.ChargeText = "carregando"; bat.Percent = pct; break;
                case 0x2: bat.ChargeText = "completa"; bat.Percent = 100; break;
                case 0xA: bat.ChargeText = "erro de tensao"; bat.Percent = -1; break;
                case 0xB: bat.ChargeText = "erro de temperatura"; bat.Percent = -1; break;
                case 0xF: bat.ChargeText = "erro de carga"; bat.Percent = -1; break;
                default: bat.ChargeText = "estado desconhecido 0x" + bat.ChargeState.ToString("X"); bat.Percent = -1; break;
            }
            return true;
        }

        // CRC de entrada Bluetooth: semente HID 0xA1, polinomio refletido CRC-32 e complemento final.
        public static bool ValidBluetoothReport(byte[] r, int start, int n)
        {
            if (r == null || start < 0 || n != 78 || start > r.Length - n || r[start] != 0x31) return false;
            uint crc = CrcByte(0xFFFFFFFF, 0xA1);
            for (int i = 0; i < n - 4; i++) crc = CrcByte(crc, r[start + i]);
            int o = start + n - 4;
            uint expected = (uint)r[o] | (uint)r[o + 1] << 8 | (uint)r[o + 2] << 16 | (uint)r[o + 3] << 24;
            return ~crc == expected;
        }
        static uint CrcByte(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            return crc;
        }
    }

    public enum BatterySource { Unknown, PhysicalUsb, PhysicalBluetooth, Virtual }

    public struct Battery
    {
        public byte Raw;
        public int Level;        // 0..10
        public int ChargeState;  // nibble alto do status
        public int Percent;      // -1 quando o estado e de erro
        public string ChargeText;
        public BatterySource Source;
        public DateTime ObservedUtc; // Ultimo relatorio valido, mesmo quando o byte de status nao mudou.
        public bool Physical { get { return Source == BatterySource.PhysicalUsb || Source == BatterySource.PhysicalBluetooth; } }
    }

    public sealed class ButtonEvent
    {
        public DateTime Time;
        public string Device;
        public string Button;
        public bool Down;
        public int DurationMs = -1;   // duracao no solto; tempo apertado no SEGURADO
        public string Gesture;        // null, "CURTO", "LONGO", "DUPLO" ou "SEGURADO"
    }

    // Transforma o estado bruto em eventos. Gestos so para PS, MUTE e TOUCHPAD.
    public sealed class ButtonTracker
    {
        public static readonly string[] GestureButtons = { "PS", "MUTE", "TOUCHPAD" };
        public int LongPressMs = 600;
        public int DoublePressGapMs = 350;
        public event Action<ButtonEvent> Event;

        readonly string label;
        readonly Dictionary<string, bool> last = new Dictionary<string, bool>();
        readonly Dictionary<string, DateTime> downAt = new Dictionary<string, DateTime>();
        readonly Dictionary<string, DateTime> lastShortRelease = new Dictionary<string, DateTime>();
        readonly HashSet<string> holdFired = new HashSet<string>();
        readonly StickDirectionTracker leftStick = new StickDirectionTracker(), rightStick = new StickDirectionTracker();
        bool leftTrigger, rightTrigger;

        public ButtonTracker(string label) { this.label = label; }

        public void Update(Buttons b, DateTime now)
        {
            var map = b.ToMap();
            string left = leftStick.Update(b.LeftX, b.LeftY, b.HasAnalog), right = rightStick.Update(b.RightX, b.RightY, b.HasAnalog);
            foreach (string direction in new[] { "CIMA", "BAIXO", "ESQUERDA", "DIREITA" })
            {
                map["LS_" + direction] = left == direction; map["RS_" + direction] = right == direction;
            }
            leftTrigger = b.HasAnalog && b.TriggerLeft >= (leftTrigger ? 20 : 32);
            rightTrigger = b.HasAnalog && b.TriggerRight >= (rightTrigger ? 20 : 32);
            map["L2"] = b.HasAnalog ? leftTrigger : b.L2;
            map["R2"] = b.HasAnalog ? rightTrigger : b.R2;
            // Soltar o eixo anterior antes do novo impede um passo diagonal ou repeticao presa na troca.
            foreach (var kv in map)
            {
                bool was; last.TryGetValue(kv.Key, out was);
                if (was && !kv.Value) UpdateButton(kv.Key, false, now);
            }
            foreach (var kv in map) if (kv.Value) UpdateButton(kv.Key, true, now);
        }

        void UpdateButton(string button, bool down, DateTime now)
        {
            bool was;
            last.TryGetValue(button, out was);
            bool gesture = Array.IndexOf(GestureButtons, button) >= 0;
            if (down == was)
            {
                // Segurado: dispara uma vez ao passar do limite, ainda com o botao apertado.
                DateTime since;
                if (gesture && down && !holdFired.Contains(button) && downAt.TryGetValue(button, out since) &&
                    (now - since).TotalMilliseconds >= LongPressMs)
                {
                    holdFired.Add(button);
                    Raise(button, true, (int)(now - since).TotalMilliseconds, "SEGURADO", now);
                }
                return;
            }
            last[button] = down;
            if (down)
            {
                downAt[button] = now;
                holdFired.Remove(button);
                string g = null;
                DateTime prev;
                if (gesture && lastShortRelease.TryGetValue(button, out prev) &&
                    (now - prev).TotalMilliseconds <= DoublePressGapMs)
                {
                    g = "DUPLO";
                    lastShortRelease.Remove(button);
                }
                Raise(button, true, -1, g, now);
            }
            else
            {
                DateTime start;
                int ms = downAt.TryGetValue(button, out start) ? (int)(now - start).TotalMilliseconds : -1;
                string g = null;
                if (gesture)
                {
                    if (ms >= LongPressMs) { g = "LONGO"; lastShortRelease.Remove(button); }
                    else { g = "CURTO"; lastShortRelease[button] = now; }
                }
                Raise(button, false, ms, g, now);
            }
        }

        void Raise(string button, bool down, int ms, string gesture, DateTime now)
        {
            var h = Event;
            if (h == null) return;
            var e = new ButtonEvent();
            e.Time = now; e.Device = label; e.Button = button; e.Down = down; e.DurationMs = ms; e.Gesture = gesture;
            h(e);
        }
    }

    public sealed class HidDevice
    {
        public string Path;
        public string Kind;
        public string Label;
        public BatterySource BatterySource;
        public string[] Ancestors = new string[0];
        public string Product = "?";
        public int InputReportLength = 64;
        public SafeFileHandle Handle;

        public string TryOpen()
        {
            var h = Hid.CreateFile(Path, Hid.GENERIC_READ, Hid.FILE_SHARE_READ | Hid.FILE_SHARE_WRITE,
                IntPtr.Zero, Hid.OPEN_EXISTING, Hid.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                return err == 5 ? "BLOQUEADO (acesso negado, provavelmente HidHide)" : "falhou ao abrir (erro " + err + ")";
            }
            Handle = h;
            var sb = new StringBuilder(128);
            if (Hid.HidD_GetProductString(h, sb, sb.Capacity * 2)) Product = sb.ToString();
            IntPtr pre;
            if (Hid.HidD_GetPreparsedData(h, out pre))
            {
                var caps = new Hid.HIDP_CAPS();
                if (Hid.HidP_GetCaps(pre, ref caps) == 0x00110000) InputReportLength = caps.InputReportByteLength;
                Hid.HidD_FreePreparsedData(pre);
            }
            return "ok, leitura compartilhada, relatorio de entrada " + InputReportLength + " bytes";
        }

        public void Close()
        {
            if (Handle != null) { Handle.Dispose(); Handle = null; }
        }
    }

    // Procura DualSense a cada ScanIntervalMs (trocar o modo do DSX cria/remove o virtual), le cada um numa
    // thread e publica os eventos. Com Deduplicate, o mesmo evento vindo de dois dispositivos (fisico + virtual
    // do DSX) em ate DedupeWindowMs vira um so.
    public sealed class DualSenseMonitor
    {
        public event Action<string> Log;
        public event Action<ButtonEvent> ButtonChanged;
        public event Action<string, Battery> BatteryChanged;   // Mudanca ou renovacao de validade a cada segundo.
        public event Action<string> DeviceLost;
        public int ScanIntervalMs = 2000;
        public bool Deduplicate = true;
        public int DedupeWindowMs = 60;
        // Falso: o PS de cada dispositivo chega separado, para saber se o virtual do DSX (lido pela Steam) tambem o recebeu.
        public bool DeduplicateGuide = true;
        public int LongPressMs = 600;
        public int DoublePressGapMs = 350;

        readonly HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> blocked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly List<Thread> threads = new List<Thread>();
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Dictionary<string, ButtonEvent> lastByKey = new Dictionary<string, ButtonEvent>();
        Thread scanner;
        int labelCounter = -1;

        public void Start()
        {
            scanner = new Thread(delegate ()
            {
                // Uma falha numa varredura (aparelho saindo no meio, handler de log) nao pode encerrar o processo.
                do
                {
                    try { Scan(true); }
                    catch (Exception ex) { try { Emit("varredura falhou: " + ex.Message); } catch (Exception) { } }
                } while (!stop.WaitOne(ScanIntervalMs));
            });
            scanner.IsBackground = true;
            scanner.Start();
        }

        public void Stop()
        {
            stop.Set();
            if (scanner != null) scanner.Join(3000);
            Thread[] copy;
            lock (threads) copy = threads.ToArray();
            foreach (var t in copy) t.Join(2000);
        }

        // Uma varredura. Com read=false so lista os dispositivos e o acesso a cada um.
        public void Scan(bool read)
        {
            var found = Hid.FindDualSenses();
            if (found.Count == 0 && known.Count == 0 && blocked.Count == 0) Emit("nenhum DualSense encontrado");
            foreach (var dev in found)
            {
                lock (known) { if (known.Contains(dev.Path)) continue; }
                string access = dev.TryOpen();
                if (dev.Handle == null)
                {
                    string prev;
                    if (!blocked.TryGetValue(dev.Path, out prev) || prev != access)
                        Emit(string.Format("dispositivo {0} | {1} | {2}", dev.Kind, access, dev.Path));
                    blocked[dev.Path] = access;
                    continue;
                }
                if (blocked.Remove(dev.Path)) Emit("dispositivo liberado: " + dev.Path);
                dev.Label = string.Format("DualSense#{0}-{1}", Interlocked.Increment(ref labelCounter), dev.Kind);
                Emit(string.Format("dispositivo {0} | {1} | {2} | {3} | bateria={4}", dev.Label, dev.Product, access, dev.Path, dev.BatterySource));
                if (!read) { dev.Close(); continue; }
                lock (known) known.Add(dev.Path);
                var d = dev;
                var t = new Thread(delegate () {
                    try { ReadLoop(d); }
                    finally
                    {
                        d.Close(); lock (known) known.Remove(d.Path);
                        lock (threads) threads.Remove(Thread.CurrentThread);
                        try { var lost = DeviceLost; if (lost != null) lost(d.Label); }
                        catch (Exception ex) { try { Emit("notificacao de perda falhou: " + ex.Message); } catch (Exception) { } }
                    }
                });
                t.IsBackground = true;
                lock (threads) threads.Add(t);
                t.Start();
            }
        }

        void Emit(string text)
        {
            var h = Log;
            if (h != null) h(text);
        }

        void Publish(ButtonEvent e)
        {
            if (Deduplicate && (DeduplicateGuide || e.Button != "PS"))
            {
                string key = e.Button + "|" + e.Down + "|" + e.Gesture;
                lock (lastByKey)
                {
                    ButtonEvent prev;
                    if (lastByKey.TryGetValue(key, out prev) && prev.Device != e.Device &&
                        Math.Abs((e.Time - prev.Time).TotalMilliseconds) <= DedupeWindowMs) return;
                    lastByKey[key] = e;
                }
            }
            var h = ButtonChanged;
            if (h != null) h(e);
        }

        void ReadLoop(HidDevice dev)
        {
            var tracker = new ButtonTracker(dev.Label);
            tracker.LongPressMs = LongPressMs;
            tracker.DoublePressGapMs = DoublePressGapMs;
            tracker.Event += Publish;
            var seenIds = new HashSet<byte>();
            var buf = new byte[Math.Max(dev.InputReportLength, 128)];
            int reports = 0;
            int lastBatteryRaw = -1;
            DateTime lastBatterySent = DateTime.MinValue;
            DateTime rateStart = DateTime.Now;
            try
            {
                using (var fs = new FileStream(dev.Handle, FileAccess.Read, buf.Length, true))
                {
                    while (!stop.WaitOne(0))
                    {
                        IAsyncResult ar = fs.BeginRead(buf, 0, buf.Length, null, null);
                        while (!ar.AsyncWaitHandle.WaitOne(200))
                        {
                            if (stop.WaitOne(0)) return;
                        }
                        int n = fs.EndRead(ar);
                        if (n <= 0) continue;
                        // O driver HID pode entregar varios relatorios inteiros numa leitura.
                        int len = dev.InputReportLength > 0 && n % dev.InputReportLength == 0 ? dev.InputReportLength : n;
                        DateTime now = DateTime.Now;
                        for (int off = 0; off + len <= n; off += len)
                        {
                            if (seenIds.Add(buf[off])) Emit(string.Format("{0} | primeiro relatorio id 0x{1:X2}, {2} bytes", dev.Label, buf[off], len));
                            Buttons b;
                            bool inputValid = DualSenseReport.Decode(buf, off, len, out b);
                            if (inputValid)
                            {
                                // Conferencia dos offsets: em repouso o direcional (hat) vale 8.
                                if (reports == 0) Emit(string.Format("{0} | estado inicial: hat={1} apertados=[{2}]", dev.Label, b.Hat, b.Pressed()));
                                tracker.Update(b, now);
                            }
                            Battery bat;
                            bool hasBattery = DualSenseReport.DecodeBattery(buf, off, len, out bat);
                            if (hasBattery || inputValid && buf[off] == 0x01 && (len == 10 || len == 78))
                            {
                                if (!hasBattery) bat = new Battery { Percent = -1, ChargeState = -1, ChargeText = "relatorio sem bateria" };
                                DateTime observed = DateTime.UtcNow;
                                int statusKey = hasBattery ? bat.Raw : -2;
                                bool changed = statusKey != lastBatteryRaw;
                                if (changed) Emit(string.Format("{0} | bateria {1} ({2}) status=0x{3:X2} fonte={4}", dev.Label,
                                    bat.Percent >= 0 ? bat.Percent + "%" : "?", bat.ChargeText, bat.Raw, dev.BatterySource));
                                if (changed || observed - lastBatterySent >= TimeSpan.FromSeconds(1))
                                {
                                    lastBatteryRaw = statusKey; lastBatterySent = observed;
                                    bat.Source = dev.BatterySource; bat.ObservedUtc = observed;
                                    var hb = BatteryChanged; if (hb != null) hb(dev.Label, bat);
                                }
                            }
                            reports++;
                        }
                        if (reports >= 2000 && rateStart != DateTime.MinValue)
                        {
                            double secs = (DateTime.Now - rateStart).TotalSeconds;
                            Emit(string.Format("{0} | taxa ~{1:F0} relatorios/s", dev.Label, reports / Math.Max(secs, 0.001)));
                            rateStart = DateTime.MinValue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Emit(dev.Label + " | leitura parou: " + ex.Message);
            }
        }
    }

    public static class Hid
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid Guid; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetProductString(SafeFileHandle h, StringBuilder buf, int len);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, ref HIDP_CAPS caps);
        [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr h, int flags);
        [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr dev, ref Guid g, int i, ref SP_DEVICE_INTERFACE_DATA d);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr buf, int size, out int req, IntPtr dev);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr buf, int size, out int req, ref SP_DEVINFO_DATA dev);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint node, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_ID(uint node, StringBuilder id, int length, uint flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tmpl);

        // A aparencia USB do HID nao distingue o fisico do virtual do DSX. Consultar a cadeia PnP, so leitura.
        public static BatterySource ClassifyBatterySource(string kind, IEnumerable<string> ancestors)
        {
            bool bluetooth = false, usbHub = false, software = false;
            foreach (string raw in ancestors)
            {
                string id = raw.ToUpperInvariant();
                if (id.Contains("VIGEM") || id.Contains("VIRTUAL") || id.Contains("VHIDMINI")) return BatterySource.Virtual;
                software |= id.StartsWith("ROOT\\", StringComparison.Ordinal) && !id.StartsWith("ROOT\\ACPI_HAL\\", StringComparison.Ordinal) || id.StartsWith("SWD\\", StringComparison.Ordinal);
                bluetooth |= id.StartsWith("BTHENUM\\", StringComparison.Ordinal);
                usbHub |= id.StartsWith("USB\\ROOT_HUB", StringComparison.Ordinal);
            }
            if (software) return BatterySource.Unknown;
            if (kind == "BT" && bluetooth) return BatterySource.PhysicalBluetooth;
            if (kind == "USB" && usbHub) return BatterySource.PhysicalUsb;
            return BatterySource.Unknown;
        }
        static string[] ReadAncestors(uint node)
        {
            var result = new List<string>(); var seen = new HashSet<uint>();
            while (seen.Count < 32 && seen.Add(node))
            {
                var id = new StringBuilder(512);
                if (CM_Get_Device_ID(node, id, id.Capacity, 0) != 0) break;
                result.Add(id.ToString());
                uint parent; if (CM_Get_Parent(out parent, node, 0) != 0) break;
                node = parent;
            }
            return result.ToArray();
        }

        // Caminhos USB/virtual usam "vid_054c&pid_0ce6"; os Bluetooth usam "vid&0002054c_pid&0ce6".
        public static List<HidDevice> FindDualSenses()
        {
            var list = new List<HidDevice>();
            Guid g;
            HidD_GetHidGuid(out g);
            IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
            for (int i = 0; ; i++)
            {
                var d = new SP_DEVICE_INTERFACE_DATA();
                d.cbSize = Marshal.SizeOf(d);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                int req;
                SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                // Aparelho que saiu entre as duas chamadas pode devolver tamanho 0: escrever o cbSize abaixo corromperia o heap.
                if (req < 8) continue;
                IntPtr buf = Marshal.AllocHGlobal(req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    var info = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, ref info)) continue;
                    string path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                    string p = path.ToLowerInvariant();
                    bool usb = p.Contains("vid_054c&pid_0ce6") || p.Contains("vid_054c&pid_0df2");
                    bool bt = p.Contains("vid&0002054c_pid&0ce6") || p.Contains("vid&0002054c_pid&0df2");
                    if (!usb && !bt) continue;
                    // "&mi_" aparece no fisico USB e tambem no virtual do DSX (barramento ViGEm).
                    var dev = new HidDevice();
                    dev.Path = path;
                    dev.Kind = bt ? "BT" : "USB";
                    dev.Ancestors = ReadAncestors(info.DevInst);
                    dev.BatterySource = ClassifyBatterySource(dev.Kind, dev.Ancestors);
                    list.Add(dev);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            SetupDiDestroyDeviceInfoList(set);
            return list;
        }
    }
}
