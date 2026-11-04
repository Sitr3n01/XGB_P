// Leitores limitados de KeyValues textual/binario. Nenhuma escrita no cliente Steam.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XGBP.Games
{
    public sealed class ValveNode
    {
        public readonly Dictionary<string, ValveNode> Children = new Dictionary<string, ValveNode>(StringComparer.OrdinalIgnoreCase);
        public string Value;
        public ValveNode Get(string key) { ValveNode n; return Children.TryGetValue(key, out n) ? n : new ValveNode(); }
        public string Text(string key) { return Get(key).Value ?? ""; }
    }

    public static class ValveData
    {
        const int Limit = 32 * 1024 * 1024;
        public static ValveNode Read(string path, bool binary)
        {
            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (f.Length > Limit) throw new InvalidDataException("Arquivo Valve muito grande.");
                using (var memory = new MemoryStream())
                { f.CopyTo(memory); if (memory.Length > Limit) throw new InvalidDataException("Arquivo Valve muito grande."); return binary ? Binary(memory.ToArray()) : Text(Encoding.UTF8.GetString(memory.ToArray())); }
            }
        }
        public static ValveNode Text(string source) { return new TextReader(source).Parse(); }
        public static ValveNode Binary(byte[] bytes)
        {
            if (bytes.Length > Limit) throw new InvalidDataException("Arquivo Valve muito grande.");
            using (var r = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
            { var root = ReadObject(r, 0, null); if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Sobra no VDF binario."); return root; }
        }
        static string CString(BinaryReader r)
        {
            using (var b = new MemoryStream())
            { for (int i = 0; i < 65536; i++) { byte c = r.ReadByte(); if (c == 0) return Encoding.UTF8.GetString(b.ToArray()); b.WriteByte(c); } }
            throw new InvalidDataException("String VDF muito grande.");
        }
        // Layout appinfo v40/v41 publicado pelo projeto SteamAppInfo; somente tipos dos IDs instalados.
        public static Dictionary<uint, string> AppTypes(string path, HashSet<uint> wanted)
        {
            var result = new Dictionary<uint, string>();
            foreach (var pair in AppInfo(path, wanted)) result[pair.Key] = pair.Value.Get("common").Text("type");
            return result;
        }
        public static Dictionary<uint, ValveNode> AppInfo(string path, HashSet<uint> wanted)
        {
            var result = new Dictionary<uint, ValveNode>(); if (!File.Exists(path)) return result;
            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new BinaryReader(f, Encoding.UTF8))
            {
                if (f.Length > 128 * 1024 * 1024) throw new InvalidDataException("appinfo muito grande.");
                uint version = r.ReadUInt32(); r.ReadUInt32();
                if (version != 0x07564428 && version != 0x07564429) throw new InvalidDataException("Versao appinfo nao suportada.");
                string[] strings = null; long end = f.Length;
                if (version == 0x07564429)
                {
                    end = r.ReadInt64(); if (end < 16 || end > f.Length - 4) throw new InvalidDataException("Tabela appinfo invalida.");
                    f.Position = end; uint count = r.ReadUInt32(); if (count > 200000) throw new InvalidDataException("Tabela appinfo muito grande.");
                    strings = new string[count]; for (int i = 0; i < strings.Length; i++) strings[i] = CString(r);
                    f.Position = 16;
                }
                while (f.Position < end)
                {
                    uint id = r.ReadUInt32(); if (id == 0) return result;
                    uint size = r.ReadUInt32(); long start = f.Position, next = start + size;
                    if (size < 60 || next > end || size > Limit) throw new InvalidDataException("Registro appinfo invalido.");
                    if (wanted.Contains(id))
                    {
                        f.Position += 60; byte[] data = r.ReadBytes((int)size - 60);
                        using (var body = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
                        {
                            var node = ReadObject(body, 0, strings);
                            result[id] = node.Get("appinfo");
                        }
                    }
                    f.Position = next;
                }
                throw new InvalidDataException("appinfo incompleto.");
            }
        }
        static ValveNode ReadObject(BinaryReader r, int depth, string[] strings)
        {
            if (depth > 48) throw new InvalidDataException("VDF muito profundo.");
            var result = new ValveNode();
            while (true)
            {
                byte type = r.ReadByte(); if (type == 8) return result;
                string key;
                if (strings == null) key = CString(r);
                else { uint index = r.ReadUInt32(); if (index >= strings.Length) throw new InvalidDataException("Indice appinfo invalido."); key = strings[index]; }
                var n = new ValveNode();
                if (type == 0) n = ReadObject(r, depth + 1, strings);
                else if (type == 1) n.Value = CString(r);
                else if (type == 2) n.Value = r.ReadUInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if (type == 7) n.Value = r.ReadUInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if (type == 3) n.Value = r.ReadSingle().ToString(System.Globalization.CultureInfo.InvariantCulture);
                else throw new InvalidDataException("Tipo VDF nao suportado: " + type);
                result.Children[key] = n;
                if (result.Children.Count > 100000) throw new InvalidDataException("Muitos campos VDF.");
            }
        }
        sealed class TextReader
        {
            readonly string source; int p;
            public TextReader(string source) { if (source.Length > Limit) throw new InvalidDataException("VDF muito grande."); this.source = source; }
            void Space()
            {
                while (p < source.Length)
                {
                    if (char.IsWhiteSpace(source[p]) || source[p] == '\uFEFF') { p++; continue; }
                    if (p + 1 < source.Length && source[p] == '/' && source[p + 1] == '/') { while (p < source.Length && source[p] != '\n') p++; continue; }
                    break;
                }
            }
            string Token()
            {
                Space(); if (p == source.Length) return null;
                char c = source[p++]; if (c == '{' || c == '}') return c.ToString();
                var b = new StringBuilder();
                if (c == '"')
                {
                    while (p < source.Length)
                    {
                        c = source[p++]; if (c == '"') return b.ToString();
                        if (c == '\\' && p < source.Length && (source[p] == '\\' || source[p] == '"')) c = source[p++];
                        b.Append(c);
                    }
                    throw new InvalidDataException("String VDF incompleta.");
                }
                b.Append(c); while (p < source.Length && !char.IsWhiteSpace(source[p]) && source[p] != '{' && source[p] != '}') b.Append(source[p++]);
                return b.ToString();
            }
            public ValveNode Parse() { return Object(0, false); }
            ValveNode Object(int depth, bool nested)
            {
                if (depth > 48) throw new InvalidDataException("VDF muito profundo.");
                var result = new ValveNode();
                while (true)
                {
                    string key = Token();
                    if (key == null) { if (nested) throw new InvalidDataException("Objeto VDF incompleto."); return result; }
                    if (key == "}") { if (!nested) throw new InvalidDataException("Fechamento VDF inesperado."); return result; }
                    if (key == "{") throw new InvalidDataException("Chave VDF invalida.");
                    string value = Token(); if (value == null || value == "}") throw new InvalidDataException("Valor VDF ausente.");
                    result.Children[key] = value == "{" ? Object(depth + 1, true) : new ValveNode { Value = value };
                }
            }
        }
    }
}
