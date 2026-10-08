using System;
using System.Runtime.InteropServices;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Core;
public static class MonitorHelper
{
    public static bool TryGet(IntPtr h, out MonitorInfo info)
    {
        info = new()
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        return NativeMethods.GetMonitorInfo(NativeMethods.MonitorFromWindow(h, MonitorNearest), ref info);
    }
}
public static class DpiHelper
{
    public static double Scale(IntPtr h) => Math.Max(96, NativeMethods.GetDpiForWindow(h)) / 96.0;
}
public static class FullscreenDetector
{
    public static bool IsFullscreenWindow(IntPtr h)
    {
        if (NativeMethods.IsZoomed(h) || !MonitorHelper.TryGet(h, out var m) || !NativeMethods.GetWindowRect(h, out var r))
            return false;
        return r.Left <= m.Monitor.Left && r.Top <= m.Monitor.Top && r.Right >= m.Monitor.Right && r.Bottom >= m.Monitor.Bottom;
    }
}
