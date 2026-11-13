// Lojas e launchers de jogos instalados: deteccao so de leitura (pacote, protocolo registrado, arquivo de
// instalacao ou caminho padrao) e abertura pelo shell, como o atalho do Menu Iniciar. Nada e instalado ou alterado.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace XGBP.Games
{
    public sealed class GameStore
    {
        public string Key = "", Name = "", Detail = "";
        public string Target = "";       // URI, executavel ou shell:AppsFolder\AUMID aberto pelo shell
        public string IconSource;        // executavel ou shell:AppsFolder\AUMID para o icone
        public byte[] IconPng;
    }

    public static class GameStores
    {
        static readonly Dictionary<string, byte[]> Icons = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        // Ordem da tela: Xbox, Steam e Epic primeiro, depois os demais launchers e a Microsoft Store.
        // Chame numa thread STA: os icones vem do shell.
        public static List<GameStore> Read(bool xboxMode)
        {
            var found = new List<GameStore>();
            Packaged(found, "xbox", "Xbox", "App Xbox · Game Pass", "Microsoft.GamingApp_8wekyb3d8bbwe", "Microsoft.Xbox.App");
            string steam = Protocol("steam");
            // Rotas do cliente instalado (steamui/sp.js): a loja do Big Picture no modo Xbox; no desktop, a pagina nomeada da loja.
            if (steam != null) Add(found, "steam", "Steam", xboxMode ? "Loja no Big Picture" : "Loja da Steam", xboxMode ? "steam://open/bigpicture/store" : "steam://url/StoreFrontPage", steam);
            string epic = Protocol("com.epicgames.launcher") ?? Existing(@"%ProgramFiles%\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe", @"%ProgramFiles(x86)%\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe");
            if (epic != null) Add(found, "epic", "Epic Games", "Loja da Epic", Protocol("com.epicgames.launcher") != null ? "com.epicgames.launcher://store" : epic, epic);
            Launcher(found, "ea", "EA app", Protocol("origin2") ?? Protocol("origin") ?? Existing(@"%ProgramFiles%\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe"));
            Launcher(found, "ubisoft", "Ubisoft Connect", Protocol("uplay") ?? Existing(@"%ProgramFiles(x86)%\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe"));
            Launcher(found, "gog", "GOG GALAXY", Protocol("goggalaxy") ?? Existing(@"%ProgramFiles(x86)%\GOG Galaxy\GalaxyClient.exe", @"%ProgramFiles%\GOG Galaxy\GalaxyClient.exe"));
            Launcher(found, "battlenet", "Battle.net", Protocol("battlenet") ?? Existing(@"%ProgramFiles(x86)%\Battle.net\Battle.net Launcher.exe", @"%ProgramFiles%\Battle.net\Battle.net Launcher.exe"));
            Launcher(found, "riot", "Riot Client", RiotClient());
            Launcher(found, "amazon", "Amazon Games", Protocol("amazon-games") ?? Existing(@"%LOCALAPPDATA%\Amazon Games\App\Amazon Games.exe"));
            Launcher(found, "rockstar", "Rockstar Games Launcher", Existing(@"%ProgramFiles%\Rockstar Games\Launcher\Launcher.exe", @"%ProgramFiles(x86)%\Rockstar Games\Launcher\Launcher.exe"));
            Launcher(found, "itch", "itch", Protocol("itch") ?? Existing(@"%LOCALAPPDATA%\itch\itch.exe"));
            Launcher(found, "heroic", "Heroic Games Launcher", Protocol("heroic") ?? Existing(@"%LOCALAPPDATA%\Programs\heroic\Heroic.exe"));
            Packaged(found, "msstore", "Microsoft Store", "Loja da Microsoft", "Microsoft.WindowsStore_8wekyb3d8bbwe", "App");
            foreach (var store in found) store.IconPng = ShellIcon(store.IconSource);
            return found;
        }
        // Lojas e a pesquisa de programas (ADR 0096) leem icones em threads proprias: o cache fica sob trava.
        public static byte[] ShellIcon(string source) { lock (Icons) return Icon(source); }
        public static void Open(GameStore store)
        {
            Process.Start(new ProcessStartInfo { FileName = store.Target, UseShellExecute = true });
        }

        static void Add(List<GameStore> found, string key, string name, string detail, string target, string icon)
        { found.Add(new GameStore { Key = key, Name = name, Detail = detail, Target = target, IconSource = icon }); }
        static void Launcher(List<GameStore> found, string key, string name, string exe)
        { if (exe != null) Add(found, key, name, "Abre o app", exe, exe); }
        static void Packaged(List<GameStore> found, string key, string name, string detail, string family, string app)
        {
            uint count = 0, length = 0;
            int status = GetPackagesByPackageFamily(family, ref count, IntPtr.Zero, ref length, IntPtr.Zero);
            if ((status != 0 && status != 122) || count == 0) return; // 122: buffer insuficiente, ou seja, ha pacote
            string aumid = @"shell:AppsFolder\" + family + "!" + app;
            Add(found, key, name, detail, aumid, aumid);
        }
        // Executavel do comando "shell\open\command" de um protocolo, se ele existir no disco.
        static string Protocol(string scheme)
        {
            using (var key = Registry.ClassesRoot.OpenSubKey(scheme + @"\shell\open\command"))
            {
                string command = key == null ? null : key.GetValue("") as string;
                if (string.IsNullOrWhiteSpace(command)) return null;
                command = command.Trim();
                string exe = command.StartsWith("\"") ? command.Substring(1, Math.Max(0, command.IndexOf('"', 1) - 1)) :
                    command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) > 0 ? command.Substring(0, command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4) : command;
                return File.Exists(exe) ? exe : null;
            }
        }
        static string Existing(params string[] paths)
        {
            foreach (var path in paths) { string full = Environment.ExpandEnvironmentVariables(path); if (File.Exists(full)) return full; }
            return null;
        }
        // O Riot Client registra a propria instalacao em ProgramData\Riot Games\RiotClientInstalls.json (rc_default).
        static string RiotClient()
        {
            try
            {
                string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Riot Games\RiotClientInstalls.json");
                if (!File.Exists(file)) return null;
                var match = Regex.Match(File.ReadAllText(file), "\"rc_default\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? Existing(match.Groups[1].Value.Replace('/', '\\')) : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        // Icone do shell (o mesmo do Menu Iniciar), 64 px com transparencia, em cache por origem.
        static byte[] Icon(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            byte[] png; if (Icons.TryGetValue(source, out png)) return png;
            IShellItemImageFactory factory = null; IntPtr bitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                if (SHCreateItemFromParsingName(source, IntPtr.Zero, ref iid, out factory) != 0 || factory.GetImage(new Size(64, 64), 0x1 | 0x4, out bitmap) != 0) return Icons[source] = null;
                DIBSECTION dib; if (GetObject(bitmap, Marshal.SizeOf(typeof(DIBSECTION)), out dib) == 0 || dib.Bm.Bits == IntPtr.Zero || dib.Bm.BitsPixel != 32) return Icons[source] = null;
                using (var raw = new Bitmap(dib.Bm.Width, dib.Bm.Height, dib.Bm.WidthBytes, PixelFormat.Format32bppPArgb, dib.Bm.Bits))
                using (var copy = new Bitmap(dib.Bm.Width, dib.Bm.Height, PixelFormat.Format32bppArgb))
                using (var stream = new MemoryStream())
                {
                    using (var g = Graphics.FromImage(copy)) g.DrawImage(raw, 0, 0, dib.Bm.Width, dib.Bm.Height);
                    // DIB com altura positiva vem de baixo para cima.
                    if (dib.Header.Height > 0) copy.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    copy.Save(stream, ImageFormat.Png); return Icons[source] = stream.ToArray();
                }
            }
            catch (COMException) { return Icons[source] = null; }
            catch (ArgumentException) { return Icons[source] = null; }
            finally
            {
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory { [PreserveSig] int GetImage(Size size, int flags, out IntPtr bitmap); }
        [StructLayout(LayoutKind.Sequential)] struct BITMAP { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public IntPtr Bits; }
        [StructLayout(LayoutKind.Sequential)] struct BITMAPINFOHEADER { public int Size, Width, Height; public ushort Planes, BitCount; public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant; }
        [StructLayout(LayoutKind.Sequential)] struct DIBSECTION { public BITMAP Bm; public BITMAPINFOHEADER Header; public uint Mask0, Mask1, Mask2; public IntPtr Section; public uint Offset; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr handle, int size, out DIBSECTION dib);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetPackagesByPackageFamily(string family, ref uint count, IntPtr fullNames, ref uint bufferLength, IntPtr buffer);
    }
}
