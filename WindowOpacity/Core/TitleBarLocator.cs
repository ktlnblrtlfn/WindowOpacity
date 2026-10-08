using System;
using System.Runtime.InteropServices;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Core;
public sealed class TitleBarLocator : IDisposable
{
    readonly AccessibilityCaptionLocator? accessibility;
    public event Action? BoundsAvailable;
    public TitleBarLocator(System.Windows.Threading.Dispatcher? dispatcher = null)
    {
        if (dispatcher != null)
        {
            accessibility = new(dispatcher);
            accessibility.BoundsAvailable += () => BoundsAvailable?.Invoke();
        }
    }
    public bool TryGetMinimizeButtonBounds(IntPtr h, out Rect r)
    {
        var info = new TitlebarInfo { Size = (uint)Marshal.SizeOf<TitlebarInfo>(), States = new uint[6], Rectangles = new Rect[6] };
        r = default;
        if (NativeMethods.SendMessageTimeout(h, WmGetTitlebarInfoEx, IntPtr.Zero, ref info, SmtoAbortIfHung, 60, out _) != IntPtr.Zero && (info.States[2] & 0x18001) == 0)
        {
            r = info.Rectangles[2];
            if (Valid(h, r))
                return true;
        }
        return CaptionFallback.TryGetMinimize(h, out r) || (accessibility?.TryGetMinimize(h, out r) == true);
    }
    public bool TryGetCaptionButtonBounds(IntPtr h, out Rect r) => NativeMethods.DwmRect(h, DwmCaptionButtonBounds, out r, 16) == 0 && r.Width > 0 && r.Height > 0;
    public bool TryGetTransparencyButtonBounds(IntPtr h, out Rect r)
    {
        r = default;
        if (accessibility != null && AccessibilityCaptionLocator.IsBraveWindow(h))
            return accessibility.TryGetTransparency(h, out r) && Valid(h, r);
        if (!TryGetMinimizeButtonBounds(h, out var min))
            return false;
        r = new(min.Left - min.Width, min.Top, min.Left, min.Bottom);
        return Valid(h, r);
    }
    static bool Valid(IntPtr h, Rect r) => r.Width > 0 && r.Height > 0 && NativeMethods.GetWindowRect(h, out var w) && r.Left >= w.Left && r.Right <= w.Right && r.Top >= w.Top && r.Bottom <= w.Bottom;
    public void ForgetDestroyed(IntPtr h) => accessibility?.ForgetDestroyed(h);
    public void Dispose() => accessibility?.Dispose();
}
public static class CaptionFallback
{
    public static bool TryGetMinimize(IntPtr h, out Rect r)
    {
        r = default;
        // Restrict inferred DWM cluster partitioning to known classic caption implementations.
        var cls = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(h, cls, 256);
        if (cls.ToString() != "#32770" && cls.ToString() != "WindowOpacity.TestWindow")
            return false;
        if (!NativeMethods.TryStyle(h, GwlStyle, out var style) || (style & 0x30000) != 0x30000 || !NativeMethods.GetWindowRect(h, out var w) || NativeMethods.DwmRect(h, DwmCaptionButtonBounds, out var cluster, 16) != 0 || cluster.Width <= 0 || cluster.Height <= 0)
            return false;
        var dpi = NativeMethods.GetDpiForWindow(h);
        var metric = NativeMethods.GetSystemMetricsForDpi(SmCxSize, dpi);
        if (metric <= 0 || cluster.Width % 3 != 0)
            return false;
        int width = cluster.Width / 3;
        if (width < metric / 2 || width > metric * 3)
            return false;
        if (NativeMethods.DwmRect(h, DwmExtendedFrameBounds, out var frame, 16) != 0)
            frame = w;
        r = new(w.Left + cluster.Left, w.Top + cluster.Top, w.Left + cluster.Left + width, w.Top + cluster.Bottom);
        return r.Left >= frame.Left && r.Right <= frame.Right && r.Top >= w.Top;
    }
}
