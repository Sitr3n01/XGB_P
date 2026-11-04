// Politica das janelas: FSE confirmado permite somente a camuflagem do shell no desktop atual.
// Instancia por consulta/thread; sem estado global mutavel nem alteracao do DWM ou do desktop.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using XGBP.Gaming;

namespace XGBP.Desktop
{
    internal sealed class AppWindowQuery : IDisposable
    {
        internal readonly bool? XboxMode;
        internal readonly Func<IntPtr, uint?> Cloak;
        readonly Func<IntPtr, bool?> desktopRead;
        IVirtualDesktopManager desktops;
        bool desktopAttempted;
        HashSet<string> frameIds;

        internal AppWindowQuery() : this(GamingExperience.Active(), DesktopWindows.ReadCloak, null) { }
        // Substitutos explicitos e locais para testar a mesma politica sem alternar o FSE nem camuflar apps reais.
        internal AppWindowQuery(bool? mode, Func<IntPtr, uint?> cloak, Func<IntPtr, bool?> desktop, HashSet<string> frames = null)
        { XboxMode = mode; Cloak = cloak; desktopRead = desktop; frameIds = frames; }

        internal static bool CloakAllowed(uint cloak, bool? mode, bool? onCurrentDesktop)
        { return cloak == 0 || cloak == 2 && mode == true && onCurrentDesktop == true; }

        internal bool? OnCurrentDesktop(IntPtr hwnd)
        {
            if (desktopRead != null) return desktopRead(hwnd);
            try
            {
                if (!desktopAttempted)
                {
                    desktopAttempted = true;
                    desktops = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
                }
                bool current;
                return desktops != null && desktops.IsWindowOnCurrentVirtualDesktop(hwnd, out current) == 0 ? (bool?)current : null;
            }
            catch (Exception) { return null; }
        }
        internal bool HasFrame(string appId, uint excludedProcess)
        {
            if (string.IsNullOrEmpty(appId)) return false;
            if (frameIds == null) frameIds = DesktopWindows.FrameAppIds(excludedProcess, this);
            return frameIds.Contains(appId);
        }
        public void Dispose()
        { if (desktops != null) { Marshal.ReleaseComObject(desktops); desktops = null; } }

        [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IVirtualDesktopManager
        {
            [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] out bool current);
            [PreserveSig] int GetWindowDesktopId(IntPtr hwnd, out Guid desktop);
            [PreserveSig] int MoveWindowToDesktop(IntPtr hwnd, ref Guid desktop);
        }
    }

    public static partial class DesktopWindows
    {
        internal static uint? ReadCloak(IntPtr hwnd)
        { uint value; return DwmGetWindowAttribute(hwnd, 14, out value, 4) == 0 ? (uint?)value : null; }

        internal static List<string> AppWindowDiagnostics(uint excludedProcess)
        {
            var lines = new List<string>();
            using (var query = new AppWindowQuery())
            {
                lines.Add("FSE=" + (query.XboxMode.HasValue ? query.XboxMode.Value.ToString() : "indisponivel") + "; somente leitura");
                EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                    try
                    {
                        if (!IsWindowVisible(hwnd)) return true;
                        uint pid; GetWindowThreadProcessId(hwnd, out pid); if (pid == excludedProcess) return true;
                        var title = new StringBuilder(2048); GetWindowText(hwnd, title, title.Capacity);
                        if (string.IsNullOrWhiteSpace(title.ToString())) return true;
                        uint? cloak = query.Cloak(hwnd);
                        lines.Add(hwnd + " | pid=" + pid + " | classe=" + WindowClass(hwnd) + " | " + title.ToString().Replace('\r', ' ').Replace('\n', ' ') +
                            " | camuflada=" + (cloak.HasValue ? WindowObservation.CloakName(cloak.Value) : "indisponivel") +
                            " | desktopAtual=" + query.OnCurrentDesktop(hwnd) + " | appId=" + WindowAppId(hwnd, pid) +
                            " | " + (EligibleReason(hwnd, excludedProcess, query) ?? "incluida"));
                    }
                    catch (Exception ex) { lines.Add(hwnd + " | leitura falhou: " + ex.Message); }
                    return true;
                }, IntPtr.Zero);
            }
            return lines;
        }

        internal static bool InternalShellSurface(string className, string executablePath)
        {
            if (className != "Windows.UI.Core.CoreWindow" || string.IsNullOrEmpty(executablePath)) return false;
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            if (!executablePath.StartsWith(windows + "SystemApps\\", StringComparison.OrdinalIgnoreCase) &&
                !executablePath.StartsWith(windows + "System32\\", StringComparison.OrdinalIgnoreCase)) return false;
            string exe = Path.GetFileName(executablePath);
            return string.Equals(exe, "TextInputHost.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(exe, "ShellExperienceHost.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(exe, "StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(exe, "SearchHost.exe", StringComparison.OrdinalIgnoreCase);
        }

        internal static HashSet<string> FrameAppIds(uint excludedProcess, AppWindowQuery query)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (!EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                try
                {
                    if (WindowClass(hwnd) != "ApplicationFrameWindow" || EligibleReason(hwnd, excludedProcess, query) != null) return true;
                    uint pid; GetWindowThreadProcessId(hwnd, out pid);
                    string id = WindowAppId(hwnd, pid); if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }
                catch (Exception) { }
                return true;
            }, IntPtr.Zero)) throw new InvalidOperationException("Não foi possível identificar as janelas dos apps do Windows.");
            return ids;
        }

        internal static string WindowAppId(IntPtr hwnd, uint pid)
        {
            IPropertyStore store = null;
            var value = new AppPropVariant();
            try
            {
                var iid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
                if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) == 0 && store != null)
                {
                    var key = new AppPropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
                    if (store.GetValue(ref key, out value) == 0 && value.Type == 31 && value.Pointer != IntPtr.Zero)
                    {
                        string id = Marshal.PtrToStringUni(value.Pointer); if (!string.IsNullOrWhiteSpace(id)) return id;
                    }
                }
            }
            catch (Exception) { }
            finally
            {
                PropVariantClear(ref value);
                if (store != null) Marshal.ReleaseComObject(store);
            }
            // CoreWindow pode estar desanexada do frame no FSE. Usa a identidade do pacote, nunca titulo/PID sozinho.
            if (WindowClass(hwnd) != "Windows.UI.Core.CoreWindow") return null;
            IntPtr process = OpenProcess(0x1000, false, pid); if (process == IntPtr.Zero) return null;
            try
            {
                uint length = 0;
                if (GetApplicationUserModelId(process, ref length, null) != 122 || length == 0 || length > 32768) return null;
                var text = new StringBuilder((int)length);
                return GetApplicationUserModelId(process, ref length, text) == 0 ? text.ToString() : null;
            }
            catch (Exception) { return null; }
            finally { CloseHandle(process); }
        }

        [StructLayout(LayoutKind.Sequential)] struct AppPropertyKey { public Guid Format; public uint Id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)] struct AppPropVariant
        { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out AppPropertyKey key);
            [PreserveSig] int GetValue(ref AppPropertyKey key, out AppPropVariant value);
            [PreserveSig] int SetValue(ref AppPropertyKey key, ref AppPropVariant value);
            [PreserveSig] int Commit();
        }
        [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref AppPropVariant value);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder appId);
    }
}
