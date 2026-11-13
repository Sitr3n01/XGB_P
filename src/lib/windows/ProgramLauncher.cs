// Abrir um programa da pesquisa (ADR 0096). A barra costuma rodar como administrador (ADR 0040), e um programa aberto
// direto por ela herdaria esse token. Por isso o pedido vai ao Explorer, pela janela da area de trabalho do shell
// (IShellWindows -> IShellBrowser -> IShellView -> IShellDispatch2.ShellExecute): o programa nasce como num clique no
// Iniciar, sem administrador. Sem a area de trabalho do shell, quem chama decide abrir direto e avisa.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace XGBP.Desktop
{
    public static class ProgramLauncher
    {
        const int SwcDesktop = 8, SwfoNeedDispatch = 1, SvgioBackground = 0, ShowNormal = 1, ShowMaximized = 3;
        static readonly Guid TopLevelBrowser = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");

        // false: a area de trabalho do shell nao respondeu (nada foi aberto).
        public static bool OpenThroughShell(string target, bool maximized)
        {
            object shell = DesktopShell();
            if (shell == null) return false;
            try
            {
                shell.GetType().InvokeMember("ShellExecute", BindingFlags.InvokeMethod, null, shell,
                    new object[] { target, "", "", "open", maximized ? ShowMaximized : ShowNormal });
                return true;
            }
            finally { Marshal.ReleaseComObject(shell); }
        }
        // So leitura: obtem e solta o objeto do shell, sem executar nada.
        public static bool ShellAvailable()
        {
            object shell = DesktopShell();
            if (shell == null) return false;
            Marshal.ReleaseComObject(shell);
            return true;
        }
        // Abre por este processo: herda o token da barra (administrador, se ela estiver elevada).
        public static void OpenDirect(string target, bool maximized)
        {
            using (Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true,
                WindowStyle = maximized ? ProcessWindowStyle.Maximized : ProcessWindowStyle.Normal })) { }
        }

        static object DesktopShell()
        {
            object windows = null, found = null, browser = null, view = null, background = null;
            try
            {
                windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), true));
                object location = 0, root = null; int hwnd; // CSIDL_DESKTOP
                found = ((IShellWindows)windows).FindWindowSW(ref location, ref root, SwcDesktop, out hwnd, SwfoNeedDispatch);
                if (found == null) return null;
                Guid service = TopLevelBrowser, browserId = typeof(IShellBrowser).GUID;
                ((IServiceProvider)found).QueryService(ref service, ref browserId, out browser);
                ((IShellBrowser)browser).QueryActiveShellView(out view);
                Guid dispatch = new Guid("00020400-0000-0000-C000-000000000046");
                ((IShellView)view).GetItemObject(SvgioBackground, ref dispatch, out background);
                return background.GetType().InvokeMember("Application", BindingFlags.GetProperty, null, background, null);
            }
            catch (COMException) { return null; }
            catch (InvalidCastException) { return null; }
            finally
            {
                foreach (var item in new[] { background, view, browser, found, windows })
                    if (item != null && Marshal.IsComObject(item)) Marshal.ReleaseComObject(item);
            }
        }

        // Os metodos sem uso so ocupam a posicao na vtable; nunca sao chamados.
        [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
        interface IShellWindows
        {
            void Count(); void Item(); void NewEnum(); void Register(); void RegisterPending(); void Revoke(); void OnNavigate(); void OnActivated();
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object location, [MarshalAs(UnmanagedType.Struct)] ref object root,
                int windowClass, out int hwnd, int options);
        }
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IServiceProvider
        {
            void QueryService(ref Guid service, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result);
        }
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellBrowser
        {
            void GetWindow(); void ContextSensitiveHelp(); void InsertMenusSB(); void SetMenuSB(); void RemoveMenusSB(); void SetStatusTextSB();
        void EnableModelessSB(); void TranslateAcceleratorSB(); void BrowseObject(); void GetViewStateStream(); void GetControlWindow(); void SendControlMsg();
            void QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object view);
        }
        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellView
        {
            void GetWindow(); void ContextSensitiveHelp(); void TranslateAccelerator(); void EnableModeless(); void UIActivate(); void Refresh();
            void CreateViewWindow(); void DestroyViewWindow(); void GetCurrentInfo(); void AddPropertySheetPages(); void SaveViewState(); void SelectItem();
            void GetItemObject(uint item, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result);
        }
    }
}
