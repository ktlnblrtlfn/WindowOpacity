using System;
using System.IO;
using System.Text;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
using WindowOpacity.Services;
namespace WindowOpacity.Core;
public sealed class WindowFilter(SettingsService settings, WindowOpacityService opacity, LoggingService log)
{
    public bool IsEligibleTargetWindow(IntPtr h)
    {
        string? reason = null;
        if (h == IntPtr.Zero || !NativeMethods.IsWindow(h) || !NativeMethods.IsWindowVisible(h) || NativeMethods.IsIconic(h) || NativeMethods.GetAncestor(h, GaRoot) != h)
            return false;
        if (NativeMethods.GetWindowThreadProcessId(h, out var pid) == 0 || pid == (uint)Environment.ProcessId)
            return false;
        if (!NativeMethods.TryStyle(h, GwlStyle, out var style) || !NativeMethods.TryStyle(h, GwlExStyle, out var ex))
            return false;
        var cls = new StringBuilder(256);
        NativeMethods.GetClassName(h, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32768" or "tooltips_class32" or "Windows.UI.Core.CoreWindow" || (style & NativeMethods.Child) != 0 || (ex & NativeMethods.ToolWindow) != 0 || NativeMethods.GetWindow(h, GwOwner) != IntPtr.Zero)
            reason = "Shell or helper window";
        else if ((style & NativeMethods.Caption) != NativeMethods.Caption || (style & NativeMethods.MinimizeBox) == 0)
            reason = "No standard caption/minimize";
        else if (NativeMethods.DwmInt(h, DwmCloaked, out var cloaked, 4) != 0 || cloaked != 0)
            reason = "Cloaked or DWM unavailable";
        else if (!NativeMethods.GetWindowRect(h, out var rect) || rect.Width <= 0 || rect.Height <= 0)
            reason = "Empty bounds";
        else if ((ex & NativeMethods.Layered) != 0 && !opacity.IsTracked(h))
            reason = "Existing layered renderer";
        else if (!settings.Value.ShowOnMaximized && NativeMethods.IsZoomed(h))
            reason = "Maximized excluded";
        else if (settings.Value.DisableInFullscreen && FullscreenDetector.IsFullscreenWindow(h))
            reason = "Fullscreen";
        else if (!PrivilegeHelper.CanAccess(pid))
            reason = "Higher integrity or inaccessible process";
        else
        {
            var path = PrivilegeHelper.Executable(pid);
            if (path == null)
                reason = "No stable process identity";
            else if (settings.Value.ExcludedApplications.Exists(x => string.Equals(ApplicationIdentity.FromPath(x), ApplicationIdentity.FromPath(path), StringComparison.OrdinalIgnoreCase) || string.Equals(x, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                reason = "Application excluded";
        }
        if (reason != null)
            log.Write("TargetSkipped", h, reason);
        return reason == null;
    }
}
