// Envio de teclas do teclado virtual pelo SendInput, sempre ao app em primeiro plano. Nao le o teclado fisico,
// nao instala hooks e nunca deixa um modificador apertado entre chamadas: cada toque sai num unico lote
// (modificadores, tecla, soltura em ordem inversa), que o Windows nao intercala com outra entrada.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace XGBP.Input
{
    [Flags]
    public enum KeyModifiers { None = 0, Shift = 1, Ctrl = 2, Alt = 4, Win = 8 }

    // Texto (caracteres Unicode, independentes do layout do app) ou tecla virtual com modificadores.
    public sealed class KeyStroke
    {
        public string Text;
        public ushort Vk;
        public KeyModifiers Modifiers;

        public static KeyStroke Chars(string text) { return new KeyStroke { Text = text }; }
        public static KeyStroke Key(ushort vk, KeyModifiers modifiers = KeyModifiers.None) { return new KeyStroke { Vk = vk, Modifiers = modifiers }; }
        // Descricao para testes e diagnostico. O log da previa nunca registra o texto digitado.
        public string Describe()
        {
            if (Text != null) return "texto(" + Text.Length + ")";
            var parts = new List<string>();
            if ((Modifiers & KeyModifiers.Ctrl) != 0) parts.Add("Ctrl");
            if ((Modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & KeyModifiers.Win) != 0) parts.Add("Win");
            parts.Add("0x" + Vk.ToString("X2"));
            return string.Join("+", parts.ToArray());
        }
    }

    public struct KeyEventSpec
    {
        public ushort Vk, Scan;
        public uint Flags;
        public bool Up { get { return (Flags & TextInput.KeyUp) != 0; } }
        public override string ToString()
        {
            if ((Flags & TextInput.Unicode) != 0) return (Up ? "^" : "v") + "U+" + Scan.ToString("X4");
            return (Up ? "^" : "v") + Vk.ToString("X2") + ((Flags & TextInput.ExtendedKey) != 0 ? "e" : "");
        }
    }

    public static class TextInput
    {
        public const uint ExtendedKey = 1, KeyUp = 2, Unicode = 4;
        public const ushort Back = 0x08, Tab = 0x09, Return = 0x0D, Shift = 0x10, Control = 0x11, Menu = 0x12, Escape = 0x1B, Space = 0x20,
            PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24, Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28,
            Snapshot = 0x2C, Insert = 0x2D, Delete = 0x2E, LWin = 0x5B, Apps = 0x5D, F1 = 0x70, OemPeriod = 0xBE;

        // Teclas do bloco de navegacao e Win usam o prefixo estendido; sem ele, Home/setas viram o teclado numerico.
        static bool Extended(ushort vk)
        {
            return vk >= PageUp && vk <= Down || vk == Insert || vk == Delete || vk == LWin || vk == 0x5C || vk == Apps || vk == Snapshot ||
                vk == 0x90 || vk == 0x6F || vk == 0xA3 || vk == 0xA5;
        }
        static KeyEventSpec Vk(ushort vk, bool up)
        {
            uint flags = (Extended(vk) ? ExtendedKey : 0) | (up ? KeyUp : 0);
            return new KeyEventSpec { Vk = vk, Scan = (ushort)MapVirtualKey(vk, 0), Flags = flags };
        }

        // Sequencia completa do toque, pura, para a verificacao conferir sem enviar nada.
        public static List<KeyEventSpec> Plan(KeyStroke stroke)
        {
            var events = new List<KeyEventSpec>();
            if (stroke == null) return events;
            if (stroke.Text != null)
            {
                // Pares de surrogates saem como duas unidades seguidas; o app recompoe o caractere.
                foreach (char c in stroke.Text)
                {
                    events.Add(new KeyEventSpec { Scan = c, Flags = Unicode });
                    events.Add(new KeyEventSpec { Scan = c, Flags = Unicode | KeyUp });
                }
                return events;
            }
            var modifiers = new List<ushort>();
            if ((stroke.Modifiers & KeyModifiers.Ctrl) != 0) modifiers.Add(Control);
            if ((stroke.Modifiers & KeyModifiers.Alt) != 0) modifiers.Add(Menu);
            if ((stroke.Modifiers & KeyModifiers.Shift) != 0) modifiers.Add(Shift);
            if ((stroke.Modifiers & KeyModifiers.Win) != 0) modifiers.Add(LWin);
            foreach (ushort m in modifiers) events.Add(Vk(m, false));
            if (stroke.Vk != 0) { events.Add(Vk(stroke.Vk, false)); events.Add(Vk(stroke.Vk, true)); }
            for (int i = modifiers.Count - 1; i >= 0; i--) events.Add(Vk(modifiers[i], true));
            return events;
        }
        public static string Describe(List<KeyEventSpec> events)
        {
            var text = new StringBuilder();
            foreach (var e in events) { if (text.Length > 0) text.Append(' '); text.Append(e.ToString()); }
            return text.ToString();
        }

        // Tecla virtual do caractere no layout desta thread (o do usuario, ABNT2 nesta maquina), para atalhos como Ctrl+C.
        public static bool VkForChar(char c, out ushort vk, out KeyModifiers needed)
        {
            short result = VkKeyScanW(c);
            vk = 0; needed = KeyModifiers.None;
            if (result == -1) return false;
            vk = (ushort)(result & 0xFF);
            int state = (result >> 8) & 0xFF;
            if ((state & 1) != 0) needed |= KeyModifiers.Shift;
            if ((state & 2) != 0) needed |= KeyModifiers.Ctrl;
            if ((state & 4) != 0) needed |= KeyModifiers.Alt;
            return vk != 0;
        }

        // Devolve quantos eventos o Windows aceitou. O isolamento de integridade (UIPI) descarta em silencio o envio a um
        // app elevado acima deste processo: quem chama confere a integridade do destino antes.
        public static int Send(KeyStroke stroke)
        {
            var plan = Plan(stroke);
            if (plan.Count == 0) return 0;
            var inputs = new NativeInput[plan.Count];
            for (int i = 0; i < plan.Count; i++)
                inputs[i] = new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Vk = plan[i].Vk, Scan = plan[i].Scan, Flags = plan[i].Flags } } };
            return (int)SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeInput)));
        }
        public static int Expected(KeyStroke stroke) { return Plan(stroke).Count; }

        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)] struct NativeInput { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, NativeInput[] inputs, int size);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScanW(char c);
    }
}
