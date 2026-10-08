using System;
using System.Text;
using System.Runtime.InteropServices;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Native;
[StructLayout(LayoutKind.Sequential)]
public struct Rect
{
    public int Left, Top, Right, Bottom; public readonly int Width => Right - Left; public readonly int Height => Bottom - Top; public Rect(int l, int t, int r, int b)
    {
        Left = l;
        Top = t;
        Right = r;
        Bottom = b;
    }
}
[StructLayout(LayoutKind.Sequential)]
public struct TitlebarInfo
{
    public uint Size; public Rect Title; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] States; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public Rect[] Rectangles;
}
[StructLayout(LayoutKind.Sequential)]
public struct MonitorInfo
{
    public int Size; public Rect Monitor, Work; public uint Flags;
}
public static class NativeMethods
{
    public const long Layered = 0x80000, ToolWindow = 0x80, NoActivate = 0x08000000, Caption = 0x00C00000, MinimizeBox = 0x20000, Child = 0x40000000;
    public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] public static extern IntPtr GetWindowLongPtr(IntPtr h, int n);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] public static extern IntPtr SetWindowLongPtr(IntPtr h, int n, IntPtr v);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder text, int size);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, ref TitlebarInfo info, uint flags, uint timeout, out IntPtr result);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmRect(IntPtr h, int attribute, out Rect rect, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmInt(IntPtr h, int attribute, out int value, int size);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool GetMonitorInfo(IntPtr h, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SetProp(IntPtr h, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetProp(IntPtr h, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr RemoveProp(IntPtr h, string name);
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr info, int length, out int needed);
    [DllImport("advapi32.dll")] public static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] public static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    public static bool TryStyle(IntPtr h, int index, out long value)
    {
        Marshal.SetLastPInvokeError(0);
        value = GetWindowLongPtr(h, index).ToInt64();
        return value != 0 || Marshal.GetLastWin32Error() == 0;
    }
    public static bool SetStyle(IntPtr h, int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        return SetWindowLongPtr(h, index, new IntPtr(value)) != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
    }
}
