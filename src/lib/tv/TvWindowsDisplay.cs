// HDR somente na Samsung ativa e identificada de novo imediatamente antes da escrita CCD.
using System;
using System.Runtime.InteropServices;

namespace XGBP.Tv
{
    public sealed class TvWindowsDisplay : ITvCoreDisplay
    {
        readonly string fragment;
        readonly Func<string, TvDisplayState> read;
        readonly Func<TvDisplayTarget, bool, bool> write;
        public TvWindowsDisplay(string fragment) : this(fragment, TvDisplay.Read, Write) { }
        internal TvWindowsDisplay(string fragment, Func<string, TvDisplayState> read, Func<TvDisplayTarget, bool, bool> write)
        { this.fragment = fragment; this.read = read; this.write = write; }
        public TvDisplayState Read() { return read(fragment); }
        static bool Identified(TvDisplayState s)
        { return s != null && s.Valid && !s.MatchedByEdid && s.SamsungActive && s.Samsung != null && s.Samsung.EdidVendor == "SAM" && s.Samsung.OutputTechnology
            == "HDMI"; }
        public bool SetHdr(bool enabled)
        {
            var before = Read(); if (!Identified(before) || !before.HdrSupported.HasValue || !before.HdrEnabled.HasValue) return false;
            if (!before.HdrSupported.Value) return !enabled;
            if (before.HdrEnabled.Value == enabled) return true;
            var current = Read();
            if (!Identified(current) || current.Samsung.AdapterKey != before.Samsung.AdapterKey || current.Samsung.TargetId != before.Samsung.TargetId ||
                !string.Equals(current.Samsung.DevicePath, before.Samsung.DevicePath, StringComparison.OrdinalIgnoreCase)) return false;
            if (!write(current.Samsung, enabled)) return false;
            var after = Read(); return Identified(after) && after.Samsung.AdapterKey == current.Samsung.AdapterKey && after.Samsung.TargetId ==
                current.Samsung.TargetId &&
                string.Equals(after.Samsung.DevicePath, current.Samsung.DevicePath, StringComparison.OrdinalIgnoreCase) && after.HdrEnabled == enabled;
        }
        [StructLayout(LayoutKind.Sequential)] struct ColorState { public uint type, size, low; public int high; public uint id, value; }
        [DllImport("user32.dll")] static extern int DisplayConfigSetDeviceInfo(ref ColorState state);
        static bool Write(TvDisplayTarget target, bool enabled)
        {
            var state = new ColorState { type = 10, size = (uint)Marshal.SizeOf(typeof(ColorState)), low = target.AdapterLow, high = target.AdapterHigh,
                id = target.TargetId, value = enabled ? 1u : 0u };
            return DisplayConfigSetDeviceInfo(ref state) == 0;
        }
    }
}
