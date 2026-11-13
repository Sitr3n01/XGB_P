// Janela invisivel em thread propria: a barreira PBT_APMSUSPEND nunca bloqueia a interface WPF.
using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace XGBP.Power
{
    public sealed class PowerEvents : IDisposable
    {
        readonly Thread thread;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        readonly Action suspend, resume, display, ending, network;
        IntPtr handle;
        int disposed;
        Exception startupError;
        public PowerEvents(Action suspend, Action resume, Action display, Action ending, Action network)
        {
            this.suspend = suspend; this.resume = resume; this.display = display; this.ending = ending; this.network = network;
            thread = new Thread(Run); thread.IsBackground = true; thread.Name = "XGBP power events"; thread.SetApartmentState(ApartmentState.STA);
            thread.Start(); ready.WaitOne();
            if (startupError != null) { thread.Join(); ready.Dispose(); throw new InvalidOperationException("Não foi possível observar energia e telas.",
                startupError); }
            NetworkChange.NetworkAddressChanged += NetworkChanged;
        }
        void NetworkChanged(object sender, EventArgs args) { Safe(network); }
        static void Safe(Action callback) { try { if (callback != null) callback(); } catch (Exception) { } }
        void Run()
        {
            using (var context = new ApplicationContext())
            {
                var window = new EventWindow(this, context);
                try { window.Open(); handle = window.Handle; ready.Set(); Application.Run(context); }
                catch (Exception ex) { startupError = ex; }
                finally { window.Close(); ready.Set(); }
            }
        }
        sealed class EventWindow : NativeWindow
        {
            readonly PowerEvents owner;
            readonly ApplicationContext context;
            public EventWindow(PowerEvents owner, ApplicationContext context) { this.owner = owner; this.context = context; }
            public void Open() { CreateHandle(new CreateParams { Caption = "XGB_P Power Events", Style = unchecked((int)0x80000000) }); }
            public void Close() { if (Handle != IntPtr.Zero) DestroyHandle(); }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x10) { context.ExitThread(); m.Result = IntPtr.Zero; return; }
                if (m.Msg == 0x218)
                {
                    int code = m.WParam.ToInt32();
                    if (code == 4) Safe(owner.suspend);
                    else if (code == 7 || code == 18) Safe(owner.resume);
                    m.Result = new IntPtr(1); return;
                }
                if (m.Msg == 0x7E) Safe(owner.display);
                if (m.Msg == 0x11) { Safe(owner.ending); m.Result = new IntPtr(1); return; }
                base.WndProc(ref m);
            }
        }
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            NetworkChange.NetworkAddressChanged -= NetworkChanged;
            if (handle != IntPtr.Zero) PostMessage(handle, 0x10, IntPtr.Zero, IntPtr.Zero);
            if (thread.Join(3000)) ready.Dispose();
        }
    }
}
