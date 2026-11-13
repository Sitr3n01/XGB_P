// Reusa o scanner e os leitores existentes; nao cria um segundo monitor HID.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace XGBP.Input
{
    public sealed partial class DualSenseMonitor
    {
        readonly List<HidDevice> resumeDevices = new List<HidDevice>();
        readonly AutoResetEvent scanSoon = new AutoResetEvent(false);
        long fastScanUntil;
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CancelIoEx(SafeFileHandle device, IntPtr overlapped);
        void TrackResumeDevice(HidDevice device) { lock (resumeDevices) resumeDevices.Add(device); }
        void ForgetResumeDevice(HidDevice device) { lock (resumeDevices) resumeDevices.Remove(device); }
        bool WaitNextScan()
        {
            int delay = Stopwatch.GetTimestamp() < Interlocked.Read(ref fastScanUntil) ? 250 : ScanIntervalMs;
            return WaitHandle.WaitAny(new WaitHandle[] { stop, scanSoon }, delay) == 0;
        }
        public void ResumeScanning()
        {
            Interlocked.Exchange(ref fastScanUntil, Stopwatch.GetTimestamp() + 15L * Stopwatch.Frequency);
            HidDevice[] devices; lock (resumeDevices) devices = resumeDevices.ToArray();
            // Invalidar handles anteriores permite reabrir o mesmo caminho PnP depois da retomada.
            foreach (var device in devices) { try { if (device.Handle != null && !device.Handle.IsClosed) CancelIoEx(device.Handle, IntPtr.Zero);
                device.Close(); }
                catch (Exception) { } }
            scanSoon.Set();
        }
    }
}
