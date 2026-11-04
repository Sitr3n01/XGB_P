// Imagens locais de jogos. Apenas metadados de atalhos e recursos de arquivos; nenhum jogo e executado.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace XGBP.Games
{
    public sealed class GameImageLink
    {
        public string Target, Arguments, Uri, IconLocation;
    }
    public sealed class GameImages
    {
        const int Size = 64;
        readonly string cacheFolder;
        readonly Dictionary<string, byte[]> memory = new Dictionary<string, byte[]>();
        List<GameImageLink> links = new List<GameImageLink>();
        public GameImages(string cacheFolder) { this.cacheFolder = cacheFolder; }
        public void RefreshDesktop()
        {
            RefreshFolders(new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) });
        }
        public void RefreshFolders(IEnumerable<string> directories)
        {
            var folders = new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase);
            var found = new List<GameImageLink>();
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    var files = Directory.GetFiles(folder); Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (var file in files)
                    {
                        try
                        {
                            GameImageLink link = null;
                            if (file.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) link = ReadUrl(file);
                            else if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) link = ReadLink(file);
                            if (link != null) found.Add(link);
                        }
                        catch { } // Atalho ilegivel nao impede os proximos candidatos.
                    }
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            links = found;
        }
        public void Fill(GamesView view)
        {
            var selected = view.ClosedRecent(); var featured = view.Featured;
            if (featured != null) selected.Add(featured);
            foreach (var activity in view.Activities)
            { var game = view.Find(activity.Key); if (game != null && game.Kind == "Game" && !selected.Contains(game)) selected.Add(game); }
            foreach (var game in selected) Resolve(game);
        }
        public void Resolve(GameEntry game)
        {
            game.IconPng = null; game.IconKey = "generic"; game.IconSource = "Símbolo de jogo";
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(game.IconLocation)) candidates.Add(game.IconLocation);
            foreach (var link in links) if (Matches(game, link) && !string.IsNullOrWhiteSpace(link.IconLocation)) candidates.Add(link.IconLocation);
            if (!Interpreter(game.LaunchFile)) candidates.Add(game.LaunchFile);
            foreach (var path in game.RuntimePaths) if (!Interpreter(path)) candidates.Add(path);
            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                try
                {
                    string path; int index; Location(candidate, out path, out index);
                    if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path) || !tried.Add(path + "," + index)) continue;
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".exe" && ext != ".dll" && ext != ".ico" && ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp") continue;
                    var info = new FileInfo(path);
                    if (ext != ".exe" && ext != ".dll" && info.Length > 16 * 1024 * 1024) continue;
                    string key = Fingerprint(path, info.Length, info.LastWriteTimeUtc.Ticks, index);
                    byte[] png;
                    if (!memory.TryGetValue(key, out png))
                    {
                        string cache = string.IsNullOrEmpty(cacheFolder) ? null : Path.Combine(cacheFolder, key + ".png");
                        png = ReadCache(cache);
                        if (png == null) { png = Extract(path, ext, index); if (png != null) WriteCache(cache, png); }
                        if (memory.Count >= 256) memory.Clear(); memory[key] = png;
                    }
                    if (png == null) continue;
                    game.IconPng = png; game.IconKey = key; game.IconSource = path + (index == 0 ? "" : "," + index); return;
                }
                catch { } // Ausente/corrompida: tentar outra fonte; nunca propagar falha de arte ao catalogo.
            }
        }
        public static void Location(string value, out string path, out int index)
        {
            index = 0; string raw = (value ?? "").Trim();
            int comma = raw.LastIndexOf(','); int parsed;
            if (comma >= 0 && int.TryParse(raw.Substring(comma + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            { index = parsed; raw = raw.Substring(0, comma); }
            path = GameFiles.Normalize(raw.TrimStart('@'));
        }
        static bool Interpreter(string path)
        {
            string name = Path.GetFileName(path ?? "").ToLowerInvariant();
            return name == "wscript.exe" || name == "cscript.exe" || name == "cmd.exe" || name == "powershell.exe" || name == "pwsh.exe" || name == "javaw.exe" || name == "java.exe" || name == "steam.exe";
        }
        public static bool Matches(GameEntry game, GameImageLink link)
        {
            if (!string.IsNullOrEmpty(game.LaunchUri) && string.Equals(game.LaunchUri, link.Uri, StringComparison.OrdinalIgnoreCase)) return true;
            if (!Interpreter(link.Target) && (GameFiles.Same(game.LaunchFile, link.Target) || Array.Exists(game.RuntimePaths, p => GameFiles.Same(p, link.Target)))) return true;
            if (game.Key != null && game.Key.StartsWith("steam:", StringComparison.Ordinal) && string.Equals(Path.GetFileName(link.Target), "steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                string[] args = (link.Arguments ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string id = game.Key.Substring(game.Key.LastIndexOf(':') + 1);
                return args.Length >= 2 && args[0].Equals("-applaunch", StringComparison.OrdinalIgnoreCase) && args[1] == id;
            }
            return false;
        }
        static GameImageLink ReadUrl(string file)
        {
            if (new FileInfo(file).Length > 65536) return null;
            var link = new GameImageLink(); string index = "", icon = ""; bool section = false;
            foreach (var line in File.ReadAllLines(file))
            {
                string text = line.Trim();
                if (text.StartsWith("[")) { section = text.Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase); continue; }
                int eq = text.IndexOf('='); if (!section || eq < 0) continue;
                string key = text.Substring(0, eq).Trim(), value = text.Substring(eq + 1).Trim();
                if (key.Equals("URL", StringComparison.OrdinalIgnoreCase)) link.Uri = value;
                else if (key.Equals("IconFile", StringComparison.OrdinalIgnoreCase)) icon = value;
                else if (key.Equals("IconIndex", StringComparison.OrdinalIgnoreCase)) index = value;
            }
            link.IconLocation = icon.Length == 0 ? null : icon + (index.Length == 0 ? "" : "," + index); return link;
        }
        static string Property(object obj, string key)
        { return Convert.ToString(obj.GetType().InvokeMember(key, BindingFlags.GetProperty, null, obj, null), CultureInfo.InvariantCulture); }
        static GameImageLink ReadLink(string file)
        {
            object shell = null, shortcut = null;
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell"); if (type == null) return null;
                shell = Activator.CreateInstance(type);
                shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { file });
                return new GameImageLink { Target = Property(shortcut, "TargetPath"), Arguments = Property(shortcut, "Arguments"), IconLocation = Property(shortcut, "IconLocation") };
                // Apenas propriedades; nunca chama Save, Run ou Exec.
            }
            finally
            { if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        }
        static string Fingerprint(string path, long length, long stamp, int index)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("game-icon-v1|64|" + path.ToUpperInvariant() + "|" + length + "|" + stamp + "|" + index))).Replace("-", "").ToLowerInvariant();
        }
        static byte[] ReadCache(string path)
        {
            if (path == null || !File.Exists(path)) return null;
            try
            {
                var info = new FileInfo(path); if (info.Length > 1024 * 1024) return null;
                byte[] data = File.ReadAllBytes(path);
                using (var stream = new MemoryStream(data)) using (var bitmap = Image.FromStream(stream, true, true))
                    return bitmap.Width == Size && bitmap.Height == Size && bitmap.RawFormat.Guid == ImageFormat.Png.Guid ? data : null;
            }
            catch { return null; }
        }
        static void WriteCache(string path, byte[] png)
        {
            if (path == null) return;
            string temp = Path.Combine(Path.GetDirectoryName(path), "tmp-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(temp, png);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            catch { } // Mesmo sem permissao de cache, a imagem em memoria continua utilizavel.
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }
        static byte[] Extract(string path, string ext, int index)
        {
            if (ext == ".exe" || ext == ".dll")
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    if (SHDefExtractIcon(path, index, 0, out handle, IntPtr.Zero, 256) != 0 || handle == IntPtr.Zero) return null;
                    using (var icon = Icon.FromHandle(handle)) using (var bitmap = icon.ToBitmap()) return Png(bitmap);
                }
                finally { if (handle != IntPtr.Zero) DestroyIcon(handle); }
            }
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (ext == ".ico") using (var icon = new Icon(file, 256, 256)) using (var bitmap = icon.ToBitmap()) return Png(bitmap);
                using (var image = Image.FromStream(file, true, true)) return Png(image);
            }
        }
        static byte[] Png(Image source)
        {
            if (source.Width < 1 || source.Height < 1 || source.Width > 4096 || source.Height > 4096) return null;
            using (var bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bitmap)) using (var stream = new MemoryStream())
            {
                g.Clear(Color.Transparent); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Min((float)Size / source.Width, (float)Size / source.Height);
                float width = source.Width * scale, height = source.Height * scale;
                g.DrawImage(source, (Size - width) / 2, (Size - height) / 2, width, height);
                bitmap.Save(stream, ImageFormat.Png); return stream.ToArray();
            }
        }
        [DllImport("shell32.dll", EntryPoint = "SHDefExtractIconW", CharSet = CharSet.Unicode)] static extern int SHDefExtractIcon(string path, int index, uint flags, out IntPtr large, IntPtr small, uint size);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
    }
}
