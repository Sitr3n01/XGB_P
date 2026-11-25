// Jogos instalados pelo app Xbox / Game Pass (ADR 0117). So leitura: pacotes do usuario, MicrosoftGame.config e AppxManifest.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace XGBP.Games
{
    public sealed class XboxPackage
    {
        public string FullName, Root, DisplayName;
        // Outros caminhos da mesma pasta: WindowsApps costuma apontar para D:\WindowsApps ou C:\XboxGames.
        public string[] Aliases = new string[0];
    }
    public static class XboxCatalog
    {
        const string PackagesKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
        const string GamingRootKey = @"SOFTWARE\Microsoft\GamingServices\PackageRepository\Root";
        const string ConfigName = "MicrosoftGame.config";
        const long MaxConfig = 1024 * 1024;

        // Falha do Xbox nunca derruba o catalogo: o chamador registra o texto devolvido.
        public static string Append(GameCatalogState state, GameSettings settings)
        {
            try { return Append(state, settings, Installed()); }
            catch (Exception ex) { return "leitura falhou | " + ex.Message; }
        }
        public static string Append(GameCatalogState state, GameSettings settings, IEnumerable<XboxPackage> packages)
        {
            var problems = new List<string>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in state.Entries) keys.Add(e.Key);
            foreach (var package in packages)
            {
                List<GameEntry> games;
                try { games = Read(package); }
                catch (Exception ex) { problems.Add(package.FullName + ": " + ex.Message); continue; }
                foreach (var e in games)
                {
                    // Atalho da Steam ou cadastro local para o mesmo executavel: o registro existente continua unico dono.
                    if (state.Entries.Exists(other => Array.Exists(other.RuntimePaths, p => Array.Exists(e.RuntimePaths, r => GameFiles.Same(p, r))))) continue;
                    var mapping = (settings.Entries ?? new List<GameMapping>()).Find(m => m.Key == e.Key);
                    if (mapping != null && mapping.Hidden || !keys.Add(e.Key)) continue;
                    if (mapping != null) { if (!string.IsNullOrEmpty(mapping.Name)) e.Name = mapping.Name; if
                        (!string.IsNullOrEmpty(mapping.Kind)) e.Kind = mapping.Kind; }
                    e.Available = GameFiles.Ready(e); e.UnavailableReason = e.Available ? "" : "Caminho ou executável indisponível";
                    state.Entries.Add(e);
                }
            }
            return string.Join("; ", problems.ToArray());
        }
        // Pacotes instalados para este usuario, com o config de jogo. Sem APIs de instalacao.
        public static List<XboxPackage> Installed()
        {
            var result = new List<XboxPackage>(); var roots = GamingRoots();
            using (var packages = Registry.CurrentUser.OpenSubKey(PackagesKey))
            {
                if (packages == null) return result;
                foreach (string name in packages.GetSubKeyNames())
                {
                    string root, display;
                    using (var key = packages.OpenSubKey(name))
                    {
                        if (key == null) continue;
                        root = key.GetValue("PackageRootFolder") as string; display = key.GetValue("DisplayName") as string;
                    }
                    if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, ConfigName))) continue;
                    var aliases = new List<string>(); string alias;
                    if (roots.TryGetValue(name, out alias)) aliases.Add(alias);
                    alias = FinalPath(root); if (alias != null) aliases.Add(alias);
                    result.Add(new XboxPackage { FullName = name, Root = root, DisplayName = display, Aliases = aliases.ToArray() });
                }
            }
            return result;
        }
        public static List<GameEntry> Read(XboxPackage package)
        {
            var result = new List<GameEntry>();
            string family = Family(package.FullName);
            if (family == null) return result;
            var config = Load(Path.Combine(package.Root, ConfigName)).DocumentElement;
            if (config == null || config.LocalName != "Game") return result;
            var applications = Applications(package.Root);
            var visuals = Child(config, "ShellVisuals"); var list = Child(config, "ExecutableList");
            if (list == null) return result; // conteudo adicional, sem programa
            var roots = new List<string>();
            foreach (string root in Prepend(package.Root, package.Aliases))
            {
                string normal = GameFiles.Normalize(root);
                if (normal.Length > 0 && !roots.Exists(r => GameFiles.Same(r, normal))) roots.Add(normal);
            }
            foreach (XmlNode node in list.ChildNodes)
            {
                var exe = node as XmlElement; if (exe == null || exe.LocalName != "Executable") continue;
                string device = exe.GetAttribute("TargetDeviceFamily"), relative = exe.GetAttribute("Name");
                if (device.Length > 0 && !device.Equals("PC", StringComparison.OrdinalIgnoreCase)) continue; // builds de console
                if (exe.GetAttribute("IsDevOnly").Equals("true", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Inside(roots[0], relative) || !relative.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                string id = exe.GetAttribute("Id"); if (id.Length == 0) id = "Game";
                if (!applications.Contains(id)) continue; // sem identificador no manifesto o Windows nao sabe abrir
                string key = "xbox:" + family + "!" + id;
                var entry = result.Find(e => e.Key == key);
                var paths = roots.ConvertAll(r => GameFiles.Normalize(Path.Combine(r, relative)));
                if (entry != null) { paths.InsertRange(0, entry.RuntimePaths); entry.RuntimePaths = paths.ToArray(); continue; }
                string title = exe.GetAttribute("OverrideDisplayName");
                if (!Plain(title)) title = visuals == null ? "" : visuals.GetAttribute("DefaultDisplayName");
                if (!Plain(title)) title = package.DisplayName;
                if (!Plain(title)) title = package.FullName.Substring(0, package.FullName.IndexOf('_'));
                result.Add(new GameEntry { Key = key, Profile = "xbox", Source = "Xbox", Kind = "Game", Name = title, InstallRoot = roots[0],
                    LaunchApp = family + "!" + id, PackageFullName = package.FullName, RuntimePaths = paths.ToArray(), IconLocation =
                        Logo(roots[0], visuals) });
            }
            return result;
        }
        // Nome_Versao_Arquitetura_Recurso_Editor -> Nome_Editor.
        public static string Family(string fullName)
        {
            var parts = (fullName ?? "").Split('_');
            return parts.Length == 5 && parts[0].Length > 0 && parts[4].Length == 13 ? parts[0] + "_" + parts[4] : null;
        }
        static bool Plain(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && !text.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase) && text[0] != '@';
        }
        static IEnumerable<string> Prepend(string first, string[] rest) { yield return first; foreach (string s in rest ?? new string[0]) yield return s; }
        static bool Inside(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return false;
            string full = GameFiles.Normalize(Path.Combine(root, relative));
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        static string Logo(string root, XmlElement visuals)
        {
            if (visuals == null) return null;
            foreach (string name in new[] { "Square480x480Logo", "Square150x150Logo", "StoreLogo" })
            {
                string value = visuals.GetAttribute(name);
                if (!Inside(root, value)) continue;
                string path = GameFiles.Normalize(Path.Combine(root, value));
                if (File.Exists(path)) return path;
                foreach (string scale in new[] { "200", "100", "400", "150", "125" })
                {
                    string scaled = Path.ChangeExtension(path, null) + ".scale-" + scale + Path.GetExtension(path);
                    if (File.Exists(scaled)) return scaled;
                }
            }
            return null;
        }
        static HashSet<string> Applications(string root)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode node in Load(Path.Combine(root, "AppxManifest.xml")).GetElementsByTagName("Application", "*"))
            { var element = node as XmlElement; if (element != null && element.GetAttribute("Id").Length > 0) result.Add(element.GetAttribute("Id")); }
            return result;
        }
        static XmlElement Child(XmlElement parent, string name)
        {
            foreach (XmlNode node in parent.ChildNodes) { var e = node as XmlElement; if (e != null && e.LocalName == name) return e; }
            return null;
        }
        // Arquivo de terceiro: sem DTD, sem recurso externo e com tamanho limitado.
        static XmlDocument Load(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (file.Length > MaxConfig) throw new InvalidDataException(Path.GetFileName(path) + " muito grande.");
                using (var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                { var document = new XmlDocument { XmlResolver = null }; document.Load(reader); return document; }
            }
        }
        // Pasta real informada pelos Servicos de Jogos, por nome completo do pacote.
        static Dictionary<string, string> GamingRoots()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var root = machine.OpenSubKey(GamingRootKey)) if (root != null)
                    foreach (string id in root.GetSubKeyNames()) using (var volume = root.OpenSubKey(id)) if (volume != null)
                        foreach (string item in volume.GetSubKeyNames()) using (var key = volume.OpenSubKey(item)) if (key != null)
                        {
                            string name = key.GetValue("Package") as string, path = key.GetValue("Root") as string;
                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path)) result[name] = Trim(path);
                        }
            }
            catch (System.Security.SecurityException) { } catch (UnauthorizedAccessException) { } catch (IOException) { }
            return result;
        }
        static string Trim(string path) { return path.StartsWith(@"\\?\", StringComparison.Ordinal) && !path.StartsWith(@"\\?\Volume",
            StringComparison.OrdinalIgnoreCase) ? path.Substring(4) : path; }
        static string FinalPath(string folder)
        {
            using (var handle = CreateFile(folder, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero)) // so atributos, FILE_FLAG_BACKUP_SEMANTICS
            {
                if (handle.IsInvalid) return null;
                var path = new StringBuilder(1024); uint length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 0);
                return length == 0 || length >= path.Capacity ? null : Trim(path.ToString());
            }
        }
        // Identidade do pacote no processo: reconhece o jogo mesmo quando o Windows informa outro caminho da mesma pasta.
        internal static string PackageOf(IntPtr process)
        {
            uint length = 0;
            if (GetPackageFullName(process, ref length, null) != 122 || length == 0 || length > 512) return null; // 15700: sem pacote
            var name = new StringBuilder((int)length);
            return GetPackageFullName(process, ref length, name) == 0 ? name.ToString() : null;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFinalPathNameByHandleW")]
        static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder name);
    }
}
