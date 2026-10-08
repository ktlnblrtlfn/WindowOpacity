using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WindowOpacity.Services;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Native;
public sealed class WinEventHook : IDisposable
{
    readonly List<IntPtr> handles = new(); readonly NativeMethods.WinEventProc callback; readonly LoggingService log;
    public WinEventHook(Action<uint, IntPtr, int, int> notify, LoggingService l)
    {
        log = l;
        callback = (_, evt, h, obj, child, _, _) => notify(evt, h, obj, child);
        try
        {
            foreach (var evt in new uint[] { EventForeground, EventMinimizeStart, EventMinimizeEnd, EventDestroy, EventShow, EventHide, EventLocationChange })
            {
                var hook = NativeMethods.SetWinEventHook(evt, evt, IntPtr.Zero, callback, 0, 0, WineventOutOfContext | WineventSkipOwnProcess);
                if (hook == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                handles.Add(hook);
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        foreach (var h in handles)
            if (!NativeMethods.UnhookWinEvent(h))
                log.Write("Unhook", error: Marshal.GetLastWin32Error());
        handles.Clear();
        GC.KeepAlive(callback);
    }
}
