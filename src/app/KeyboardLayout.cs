// Teclado virtual (ADRs 0052/0053): camadas como dados puros, em unidades de tecla, sem WPF. As quatro linhas de teclas
// somam 13 unidades; cabecalho e atalhos tem intervalos vazios. A navegacao usa a posicao real de cada tecla.
// Letras no ABNT2 da maquina (pt-BR 0416, com ç), simbolos em duas paginas e Completo (F1-F12 e navegacao).
using System;
using System.Collections.Generic;
using XGBP.Input;

namespace XGBP.Design
{
    enum KeyKind { Char, Key, Modifier, Layer, Action }

    sealed class KeyDef
    {
        public string Id, Label, Shifted, Glyph, Hint, Corner;
        public double Width = 1, Left;
        public KeyKind Kind;
        public string Text;              // Char: texto em minusculas
        public ushort Vk;                // Key/Action: tecla virtual
        public KeyModifiers Modifiers;   // Modifier: qual; Action: combinacao fixa (Ctrl+C)
        public string Command;           // Layer/Action
        public string[] Alternates;      // segurar ✕ (acentos e o numero da linha de cima)
        public bool Repeat, Navigation;  // repete segurando; setas/Home/End nao consomem o Shift (selecao)
        public bool Strip;               // faixa de atalhos no topo
        public double Center { get { return Left + Width / 2; } }
    }

    sealed class KeyboardLayer
    {
        public string Name;
        public readonly List<List<KeyDef>> Rows = new List<List<KeyDef>>();
        public bool Locate(string id, out int row, out int col)
        {
            for (row = 0; row < Rows.Count; row++)
                for (col = 0; col < Rows[row].Count; col++) if (Rows[row][col].Id == id) return true;
            row = col = -1; return false;
        }
        public KeyDef At(int row, int col)
        {
            return row >= 0 && row < Rows.Count && col >= 0 && col < Rows[row].Count ? Rows[row][col] : null;
        }
    }

    static class KeyboardLayouts
    {
        public const double RowUnits = 13;
        const string Glyphs = "Segoe MDL2 Assets";

        public static KeyboardLayer Letters()
        {
            var layer = new KeyboardLayer { Name = "letters" };
            layer.Rows.Add(Header());
            layer.Rows.Add(Strip(false));
            var top = new List<KeyDef> { Esc(1.5) };
            string[][] numbers = { new[] { "1" }, new[] { "2" }, new[] { "3", "é", "è", "ê", "ë", "€" }, new[] { "4" }, new[] { "5" },
                new[] { "6", "ý", "ÿ" }, new[] { "7", "ú", "ù", "û", "ü" }, new[] { "8", "í", "ì", "î", "ï" }, new[] { "9", "ó", "ò", "ô", "õ", "ö", "º" }, new[] { "0" }
                    };
            string row1 = "qwertyuiop";
            for (int i = 0; i < row1.Length; i++) { var key = Char(row1[i].ToString(), numbers[i]); key.Corner = numbers[i][0]; top.Add(key); }
            top.Add(Backspace(1.5));
            layer.Rows.Add(top);
            var home = new List<KeyDef> { Tab(1.5) };
            foreach (char c in "asdfghjklç")
            {
                string[] alt = c == 'a' ? new[] { "á", "à", "â", "ã", "ä", "ª" } : c == 's' ? new[] { "ß", "š", "$" } : null;
                home.Add(Char(c.ToString(), alt));
            }
            home.Add(Enter(1.5));
            layer.Rows.Add(home);
            var bottom = new List<KeyDef> { ShiftKey("shift-left") };
            foreach (char c in "zxcvbnm")
            {
                string[] alt = c == 'c' ? new[] { "ç", "ć", "¢", "©" } : c == 'n' ? new[] { "ñ", "ń" } : c == 'z' ? new[] { "ž" } : null;
                bottom.Add(Char(c.ToString(), alt));
            }
            bottom.Add(Char(",", null, ";")); bottom.Add(Char(".", null, ":")); bottom.Add(Char("'", null, "\""));
            bottom.Add(ShiftKey("shift-right"));
            layer.Rows.Add(bottom);
            layer.Rows.Add(Space(LayerKey("layer-symbols", "&123", "symbols", "L2")));
            return Finish(layer);
        }

        public static KeyboardLayer Symbols(int page)
        {
            var layer = new KeyboardLayer { Name = "symbols" + page };
            layer.Rows.Add(Header());
            layer.Rows.Add(Strip(false));
            string[] rows = page == 1 ? new[] { "1234567890", "@#$%&*-+()", "!?\"':;/,._" } : new[] { "~`´^¨°ªº§¬", "[]{}<>=\\|€", "£¢¥¹²³«»…•" };
            var top = new List<KeyDef> { Esc(1.5) }; foreach (char c in rows[0]) top.Add(Char(c.ToString(), null)); top.Add(Backspace(1.5));
            var middle = new List<KeyDef> { Tab(1.5) }; foreach (char c in rows[1]) middle.Add(Char(c.ToString(), null)); middle.Add(Enter(1.5));
            string pageLabel = page + "/2";
            var lower = new List<KeyDef> { LayerKey("page-left", pageLabel, "page", null) };
            lower[0].Width = 1.5;
            foreach (char c in rows[2]) lower.Add(Char(c.ToString(), null));
            var right = LayerKey("page-right", pageLabel, "page", null); right.Width = 1.5; lower.Add(right);
            layer.Rows.Add(top); layer.Rows.Add(middle); layer.Rows.Add(lower);
            layer.Rows.Add(Space(LayerKey("layer-letters", "abc", "letters", "L2")));
            return Finish(layer);
        }

        public static KeyboardLayer Full()
        {
            var layer = new KeyboardLayer { Name = "full" };
            layer.Rows.Add(Header());
            layer.Rows.Add(Strip(true));
            var functions = new List<KeyDef> { Esc(1) };
            for (int i = 1; i <= 12; i++) functions.Add(new KeyDef { Id = "f" + i, Label = "F" + i, Kind = KeyKind.Key, Vk = (ushort)(TextInput.F1 + i - 1) });
            layer.Rows.Add(functions);
            layer.Rows.Add(new List<KeyDef> {
                Tab(1.5), Named("insert", "Ins", TextInput.Insert, 1.5), Nav("home", "Home", null, TextInput.Home, 1.5), Nav("pageup", "PgUp", null, TextInput.PageUp,
                    1.5),
                Named("printscreen", "PrtSc", TextInput.Snapshot, 1.5), Named("apps", "Menu", TextInput.Apps, 1.5), Backspace(4) });
            var delete = Named("delete", "Del", TextInput.Delete, 1.5); delete.Repeat = true;
            layer.Rows.Add(new List<KeyDef> {
                ShiftKey("shift-left"), delete, Nav("end", "End", null, TextInput.End, 1.5), Nav("pagedown", "PgDn", null, TextInput.PageDown, 1),
                Nav("up", null, "", TextInput.Up, 1), Enter(6.5) });
            layer.Rows.Add(new List<KeyDef> {
            LayerKey("layer-letters", "abc", "letters", "L2"), Modifier("ctrl", "Ctrl", KeyModifiers.Ctrl, "R2"), Modifier("win", "⊞", KeyModifiers.Win, null),
                Modifier("alt", "Alt", KeyModifiers.Alt, null), Nav("left", null, "", TextInput.Left, 1), Nav("down", null, "", TextInput.Down, 1),
                Nav("right", null, "", TextInput.Right, 1), SpaceKey(5.5) });
            return Finish(layer);
        }

        // Faixa de atalhos do Windows: desfazer, area de transferencia, emoji (Win+.) e a troca para o Completo.
        static List<KeyDef> Strip(bool full)
        {
            var strip = new List<KeyDef> {
                Combo("undo", "Desfazer", "", 0x5A, KeyModifiers.Ctrl), Combo("cut", "Recortar", "", 0x58, KeyModifiers.Ctrl),
                Combo("copy", "Copiar", "", 0x43, KeyModifiers.Ctrl), Combo("paste", "Colar", "", 0x56, KeyModifiers.Ctrl),
                Combo("selectall", "Tudo", "", 0x41, KeyModifiers.Ctrl), Combo("clipboard", "Histórico", "", 0x56, KeyModifiers.Win),
                Combo("emoji", "Emoji", "", TextInput.OemPeriod, KeyModifiers.Win),
                new KeyDef { Id = "layer-full", Label = full ? "Letras" : "Completo", Glyph = "", Kind = KeyKind.Layer, Command = "full" } };
            foreach (var key in strip) { key.Strip = true; key.Width = .65; }
            strip[strip.Count - 1].Width = 1.7;
            return strip;
        }
        static List<KeyDef> Header()
        {
            return new List<KeyDef> {
            new KeyDef { Id = "keyboard-settings", Label = "Configurações do teclado", Kind = KeyKind.Action, Command = "settings", Width = .6, Strip = true },
                new KeyDef { Id = "close", Kind = KeyKind.Action, Command = "close", Width = .6, Strip = true } };
        }
        static List<KeyDef> Space(KeyDef layerKey)
        {
            return new List<KeyDef> {
layerKey, Modifier("ctrl", "Ctrl", KeyModifiers.Ctrl, "R2"), Modifier("win", "⊞", KeyModifiers.Win, null), Modifier("alt", "Alt", KeyModifiers.Alt, null),
                SpaceKey(5.5), new KeyDef { Id = "dictation", Glyph = "", Kind = KeyKind.Action, Command = "dictation" },
                    // sem dica de botao: o MUTE e do DSX (ADR 0107)
                Nav("left", null, "", TextInput.Left, 1, "L1"), Nav("right", null, "", TextInput.Right, 1, "R1") };
        }
        static KeyDef Char(string text, string[] alternates, string shifted = null)
        {
            return new KeyDef { Id = "char-" + text, Label = text, Text = text, Shifted = shifted, Kind = KeyKind.Char, Alternates = alternates };
        }
        static KeyDef Esc(double width) { return new KeyDef { Id = "esc", Label = "Esc", Kind = KeyKind.Key, Vk = TextInput.Escape, Width = width }; }
        static KeyDef Tab(double width) { return new KeyDef { Id = "tab", Label = "Tab", Kind = KeyKind.Key, Vk = TextInput.Tab, Width = width }; }
        static KeyDef Backspace(double width) { return new KeyDef { Id = "backspace", Glyph = "", Kind = KeyKind.Key, Vk = TextInput.Back, Width = width, Repeat
            = true, Hint = "□" }; }
        static KeyDef Enter(double width) { return new KeyDef { Id = "enter", Glyph = "", Kind = KeyKind.Key, Vk = TextInput.Return, Width = width, Hint = "≡" };
            }
        static KeyDef SpaceKey(double width) { return new KeyDef { Id = "space", Label = "", Kind = KeyKind.Key, Vk = TextInput.Space, Width = width, Hint = "△"
            }; }
        static KeyDef ShiftKey(string id) { return new KeyDef { Id = id, Label = "⇧", Kind = KeyKind.Modifier, Modifiers = KeyModifiers.Shift, Width = 1.5, Hint =
            "L3" }; }
        static KeyDef Modifier(string id, string label, KeyModifiers modifier, string hint) { return new KeyDef { Id = id, Label = label, Kind = KeyKind.Modifier,
            Modifiers = modifier, Hint = hint }; }
        static KeyDef LayerKey(string id, string label, string command, string hint) { return new KeyDef { Id = id, Label = label, Kind = KeyKind.Layer, Command =
            command, Width = 1.5, Hint = hint }; }
        static KeyDef Named(string id, string label, ushort vk, double width) { return new KeyDef { Id = id, Label = label, Kind = KeyKind.Key, Vk = vk, Width =
            width }; }
        static KeyDef Nav(string id, string label, string glyph, ushort vk, double width, string hint = null)
        {
    return new KeyDef { Id = id, Label = label, Glyph = glyph, Kind = KeyKind.Key, Vk = vk, Width = width, Repeat = true, Navigation = true, Hint = hint };
        }
        static KeyDef Combo(string id, string label, string glyph, ushort vk, KeyModifiers modifiers)
        {
            return new KeyDef { Id = id, Label = label, Glyph = glyph, Kind = KeyKind.Action, Vk = vk, Modifiers = modifiers, Width = 1.5 };
        }
        public static string GlyphFont { get { return Glyphs; } }

        static KeyboardLayer Finish(KeyboardLayer layer)
        {
            for (int r = 0; r < layer.Rows.Count; r++)
            {
                var row = layer.Rows[r];
                double left = 0;
                foreach (var key in row) { key.Left = left; left += key.Width; }
                // Cabecalho e atalhos tem espaco vazio. A navegacao usa suas posicoes visuais reais.
                if (r < 2) row[row.Count - 1].Left = RowUnits - row[row.Count - 1].Width;
        else if (Math.Abs(left - RowUnits) > .001) throw new InvalidOperationException("Linha do teclado " + layer.Name + " com " + left + " unidades.");
            }
            return layer;
        }

        // Esquerda/direita andam na linha sem dar a volta; cima/baixo mantem a coluna de origem (anchor) e caem na tecla
        // que cobre essa posicao, como numa grade: do espaco para cima volta a mesma letra de onde veio.
        public static bool Move(KeyboardLayer layer, ref int row, ref int col, string direction, ref double anchor)
        {
            var current = layer.At(row, col);
            if (current == null) return false;
            if (direction == "ESQUERDA" || direction == "DIREITA")
            {
                int next = col + (direction == "DIREITA" ? 1 : -1);
                anchor = -1;
                if (next < 0 || next >= layer.Rows[row].Count) return false;
                col = next; return true;
            }
            int target = row + (direction == "BAIXO" ? 1 : -1);
            if (target < 0 || target >= layer.Rows.Count) return false;
            double x = anchor >= 0 ? anchor : current.Center;
            anchor = x;
            var keys = layer.Rows[target];
            int best = 0; double bestDistance = double.MaxValue;
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                double distance = x >= key.Left && x < key.Left + key.Width ? 0 : Math.Min(Math.Abs(x - key.Left), Math.Abs(x - (key.Left + key.Width)));
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            row = target; col = best; return true;
        }
    }
}
