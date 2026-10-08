using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Services;
// Low-level hooks execute in our message loop, without loading code into other processes.
public sealed class MouseDismissService : IDisposable
{
    delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookExW(int id, MouseProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr message, IntPtr data);
    readonly MouseProc callback; readonly Dispatcher dispatcher; readonly LoggingService log; IntPtr hook; Rect popup, button;
    public MouseDismissService(Dispatcher d, Action close, LoggingService l)
    {
        dispatcher = d;
        log = l;
        callback = (code, msg, data) => { if (code >= 0 && (msg.ToInt32() is 0x201 or 0x204 or 0x207)) { int x = Marshal.ReadInt32(data), y = Marshal.ReadInt32(data, 4); if (!Contains(popup, x, y) && !Contains(button, x, y)) dispatcher.BeginInvoke(close); } return CallNextHookEx(hook, code, msg, data); };
    }
    static bool Contains(Rect r, int x, int y) => x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;
    public void Start(IntPtr popupHandle, Rect anchor)
    {
        NativeMethods.GetWindowRect(popupHandle, out popup);
        button = anchor;
        if (hook != IntPtr.Zero)
            return;
        hook = SetWindowsHookExW(WhMouseLl, callback, IntPtr.Zero, 0);
        if (hook == IntPtr.Zero)
            log.Write("MouseDismissHook", error: Marshal.GetLastWin32Error());
    }
    public void Dispose()
    {
        if (hook != IntPtr.Zero)
        {
            if (!UnhookWindowsHookEx(hook))
                log.Write("MouseDismissUnhook", error: Marshal.GetLastWin32Error());
            hook = IntPtr.Zero;
        }
        GC.KeepAlive(callback);
    }
}
