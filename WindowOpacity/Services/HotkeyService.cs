using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Services;
public sealed class HotkeyService : IDisposable
{
    readonly HwndSource source; readonly LoggingService log; readonly bool registered; public HotkeyService(Action reset, LoggingService l)
    {
        log = l;
        source = new HwndSource(new HwndSourceParameters("WindowOpacity hotkey") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 });
        source.AddHook((IntPtr h, int m, IntPtr w, IntPtr p, ref bool handled) => { if (m == WmHotkey && w.ToInt32() == 1) { reset(); handled = true; } return IntPtr.Zero; });
        registered = NativeMethods.RegisterHotKey(source.Handle, 1, ModAlt | ModControl | ModShift | ModNoRepeat, 0x4F);
        if (!registered)
            log.Write("RegisterHotKey", error: Marshal.GetLastWin32Error());
    }
    public void Dispose()
    {
        if (registered && !NativeMethods.UnregisterHotKey(source.Handle, 1))
            log.Write("UnregisterHotKey", error: Marshal.GetLastWin32Error());
        source.Dispose();
    }
}
