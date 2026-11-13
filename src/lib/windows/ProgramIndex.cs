// Programas do PC para a pesquisa da barra (ADR 0096): a lista "Todos os apps" do Menu Iniciar (shell:AppsFolder),
// lida pelo shell e so para leitura. Nenhum atalho e resolvido, nenhum programa e aberto e nada e gravado aqui.
// A pontuacao da busca (ProgramMatch) e pura, sem shell, para os testes.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace XGBP.Desktop
{
    public sealed class ProgramEntry
    {
        public string Key = "", Name = "", ParsingName = "";
        public string Kind = "program";  // program (Win32), app (pacote da Store), steam (atalho de jogo da Steam)
        public string ExecutablePath;    // .exe resolvido, quando o item aponta para um arquivo
        public string Alias = "";        // nome do executavel ou do pacote: "calc" acha "Calculadora"
        public string SteamAppId;
        public byte[] IconPng;
        public bool IconRead;
        // Abrir e o icone usam o mesmo item do Iniciar: o shell decide como abrir pacote, atalho ou executavel.
        public string Target { get { return Kind == "steam" ? ParsingName : @"shell:AppsFolder\" + ParsingName; } }
        public string IconSource { get { return @"shell:AppsFolder\" + ParsingName; } }
    }

    public static class ProgramIndex
    {
        public const string AppsFolder = "shell:::{4234d49b-0245-4df3-b780-3893943456e1}";
        // Desinstaladores, ajuda e notas de versao aparecem no Iniciar, mas nao sao programas para abrir no modo Xbox.
        static readonly string[] ExcludedWords = { "uninstall", "desinstal", "unins0", "readme", "leia-me", "leiame", "release notes",
            "notas de versao", "website", "documentation", "documentacao" };
        static readonly string[] OwnExecutables = { "xgb_p.exe", "xgbdesignpreview.exe" };

        // Chame numa thread STA. Retorna em ordem alfabetica, sem itens repetidos.
        public static List<ProgramEntry> Read()
        {
            var raw = new List<KeyValuePair<string, string>>();
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
            object folder = null, items = null;
            try
            {
                folder = Invoke(shell, "NameSpace", BindingFlags.InvokeMethod, AppsFolder);
                if (folder == null) throw new IOException("A lista de apps do shell nao respondeu.");
                items = Invoke(folder, "Items", BindingFlags.InvokeMethod);
                int count = Convert.ToInt32(Invoke(items, "Count", BindingFlags.GetProperty));
                for (int i = 0; i < count; i++)
                {
                    object item = Invoke(items, "Item", BindingFlags.InvokeMethod, i);
                    if (item == null) continue;
                    try { raw.Add(new KeyValuePair<string, string>(Invoke(item, "Name", BindingFlags.GetProperty) as string, Invoke(item, "Path",
                        BindingFlags.GetProperty) as
                        string)); }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
            finally
            {
                if (items != null) Marshal.ReleaseComObject(items);
                if (folder != null) Marshal.ReleaseComObject(folder);
                Marshal.ReleaseComObject(shell);
            }
            return Classify(raw);
        }
        static object Invoke(object target, string name, BindingFlags kind, params object[] args)
        { return target.GetType().InvokeMember(name, kind, null, target, args); }

        public static List<ProgramEntry> Classify(IEnumerable<KeyValuePair<string, string>> items)
        {
            var found = new List<ProgramEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in items)
            {
                var entry = Entry(pair.Key, pair.Value);
                if (entry != null && seen.Add(entry.Key)) found.Add(entry);
            }
            found.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return found;
        }

        // Nome e caminho de analise do item do Iniciar: AUMID (pacote ou app Win32 com ID proprio), arquivo
        // (caminho comum ou {KnownFolder}\...) ou URL (atalhos steam://rungameid/ dos jogos).
        public static ProgramEntry Entry(string name, string path)
        {
            name = (name ?? "").Trim(); path = (path ?? "").Trim();
            if (name.Length == 0 || path.Length == 0 || Excluded(name)) return null;
            var entry = new ProgramEntry { Name = name, ParsingName = path };
            var steam = Regex.Match(path, @"^steam://rungameid/(\d+)/?$", RegexOptions.IgnoreCase);
            if (steam.Success)
            {
                entry.Kind = "steam"; entry.SteamAppId = steam.Groups[1].Value; entry.Key = "steam:" + entry.SteamAppId;
                return entry;
            }
            if (path.Contains("://") || path.StartsWith("::", StringComparison.Ordinal)) return null; // sites, protocolos e pastas do shell
            if (path.IndexOf('\\') < 0)
            {
                entry.Kind = path.Contains("!") ? "app" : "program";
                entry.Alias = AumidAlias(path); entry.Key = "aumid:" + path.ToLowerInvariant();
                return entry;
            }
            string file = ResolveKnownFolder(path);
            string extension = (Path.GetExtension(file) ?? "").ToLowerInvariant();
            if (extension != ".exe" && extension != ".msc") return null; // documentos, ajuda, links .url
            string leaf = Path.GetFileName(file);
            if (Array.IndexOf(OwnExecutables, leaf.ToLowerInvariant()) >= 0 || Excluded(leaf)) return null;
            entry.ExecutablePath = file; entry.Alias = Path.GetFileNameWithoutExtension(leaf);
            entry.Key = "file:" + file.ToLowerInvariant();
            return entry;
        }
        static bool Excluded(string text)
        {
            string folded = ProgramMatch.Fold(text);
            foreach (var word in ExcludedWords) if (folded.Contains(word)) return true;
            return false;
        }
        // "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" -> "WindowsCalculator"; "Microsoft.Windows.Explorer" -> "Explorer".
        static string AumidAlias(string aumid)
        {
            string head = aumid.Split('!', ';')[0];
            int underscore = head.IndexOf('_'); if (underscore > 0) head = head.Substring(0, underscore);
            string last = head.Substring(head.LastIndexOf('.') + 1);
            return Regex.IsMatch(last, "^[0-9A-Fa-f]{12,}$") ? "" : last; // IDs hexadecimais nao ajudam a buscar
        }
        // "{6D809377-...}\Pasta\App.exe" usa a pasta conhecida do Windows (Arquivos de Programas etc.).
        public static string ResolveKnownFolder(string path)
        {
            var match = Regex.Match(path, @"^\{([0-9A-Fa-f\-]{36})\}\\(.*)$");
            if (!match.Success) return path;
            IntPtr folder = IntPtr.Zero;
            try
            {
                Guid id = new Guid(match.Groups[1].Value);
                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out folder) != 0) return path;
                return Path.Combine(Marshal.PtrToStringUni(folder), match.Groups[2].Value);
            }
            catch (FormatException) { return path; }
            finally { if (folder != IntPtr.Zero) Marshal.FreeCoTaskMem(folder); }
        }
        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    }

    public static class ProgramMatch
    {
        // Minusculas sem acentos, com o mesmo comprimento do texto original: os indices servem para destacar o trecho.
        public static string Fold(string text)
        {
            var result = new StringBuilder((text ?? "").Length);
            foreach (char c in text ?? "")
            {
                char folded = char.ToLowerInvariant(c);
                string parts = c.ToString().Normalize(NormalizationForm.FormD);
                foreach (char p in parts)
                    if (CharUnicodeInfo.GetUnicodeCategory(p) != UnicodeCategory.NonSpacingMark) { folded = char.ToLowerInvariant(p); break; }
                result.Append(folded);
            }
            return result.ToString();
        }

        // 0 = nao combina; maior e melhor. Nome exato > comeco do nome > comeco de palavra > todas as palavras >
        // iniciais ("vsc") > trecho > letras em ordem. O apelido (executavel/pacote) nao usa letras soltas.
        public static int Score(string query, string name, string alias)
        {
            string q = Fold(query).Trim();
            while (q.Contains("  ")) q = q.Replace("  ", " ");
            if (q.Length == 0) return 0;
            int score = Text(q, Fold(name), true);
            if (!string.IsNullOrEmpty(alias)) score = Math.Max(score, Text(q, Fold(alias), false) - 60);
            return Math.Max(0, score);
        }
        // Inicio e fim do trecho que combinou no nome, para o destaque; -1 sem trecho continuo.
        public static int MatchStart(string query, string name)
        {
            string q = Fold(query).Trim(), n = Fold(name);
            if (q.Length == 0) return -1;
            int word = WordStart(n, q);
            return n.StartsWith(q, StringComparison.Ordinal) ? 0 : word >= 0 ? word : n.IndexOf(q, StringComparison.Ordinal);
        }

        static int Text(string q, string n, bool loose)
        {
            if (n.Length == 0) return 0;
            if (n == q) return 1000;
            if (n.StartsWith(q, StringComparison.Ordinal)) return 900 - Math.Min(n.Length - q.Length, 50);
            int word = WordStart(n, q);
            if (word >= 0) return 800 - Math.Min(word, 50);
            if (q.Contains(" ") && AllWords(q, n)) return 700;
            if (q.Length >= 2 && Initials(n).StartsWith(q, StringComparison.Ordinal)) return 650;
            int index = n.IndexOf(q, StringComparison.Ordinal);
            if (index >= 0) return 500 - Math.Min(index, 50);
            int gaps;
            if (loose && q.Length >= 3 && Subsequence(q, n, out gaps)) return Math.Max(1, 300 - gaps * 8);
            return 0;
        }
        static bool Boundary(string n, int i) { return i == 0 || !char.IsLetterOrDigit(n[i - 1]); }
        static int WordStart(string n, string q)
        {
            for (int i = n.IndexOf(q, StringComparison.Ordinal); i >= 0; i = n.IndexOf(q, i + 1, StringComparison.Ordinal))
                if (Boundary(n, i)) return i;
            return -1;
        }
        static bool AllWords(string q, string n)
        {
            foreach (var part in q.Split(' ')) if (part.Length > 0 && WordStart(n, part) < 0) return false;
            return true;
        }
        static string Initials(string n)
        {
            var result = new StringBuilder();
            for (int i = 0; i < n.Length; i++) if (char.IsLetterOrDigit(n[i]) && Boundary(n, i)) result.Append(n[i]);
            return result.ToString();
        }
        static bool Subsequence(string q, string n, out int gaps)
        {
            gaps = 0; int at = n.IndexOf(q[0]);
            if (at < 0) return false;
            for (int i = 1; i < q.Length; i++)
            {
                if (q[i] == ' ') continue;
                int next = n.IndexOf(q[i], at + 1);
                if (next < 0) return false;
                gaps += next - at - 1; at = next;
            }
            return true;
        }

        // Ordena pela pontuacao mais o uso (frequencia e recencia); empate: nome mais curto e depois alfabetico.
        public static List<ProgramEntry> Search(List<ProgramEntry> all, string query, Func<ProgramEntry, int> usage, int max)
        {
            var scored = new List<KeyValuePair<ProgramEntry, int>>();
            foreach (var entry in all)
            {
                int score = Score(query, entry.Name, entry.Alias);
                if (score > 0) scored.Add(new KeyValuePair<ProgramEntry, int>(entry, score + (usage == null ? 0 : usage(entry))));
            }
            scored.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Key.Name.Length != b.Key.Name.Length ?
                a.Key.Name.Length.CompareTo(b.Key.Name.Length) : string.Compare(a.Key.Name, b.Key.Name, StringComparison.CurrentCultureIgnoreCase));
            var result = new List<ProgramEntry>();
            foreach (var pair in scored) { if (result.Count >= max) break; result.Add(pair.Key); }
            return result;
        }
    }
}
