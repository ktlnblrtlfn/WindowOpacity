using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Threading;
using WindowOpacity.Native;
using Rect = WindowOpacity.Native.Rect;

namespace WindowOpacity.Core;

/// <summary>Read-only adapter for verified custom Chromium caption buttons.</summary>
public sealed class AccessibilityCaptionLocator : IDisposable
{
    enum CaptionButton
    {
        Minimize, Maximize, Close, TabSearch
    }
    static readonly Dictionary<string, CaptionButton> ButtonNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Minimise"] = CaptionButton.Minimize,
        ["Minimize"] = CaptionButton.Minimize,
        ["Minimize window"] = CaptionButton.Minimize,
        ["Minimise window"] = CaptionButton.Minimize,
        ["Küçült"] = CaptionButton.Minimize,
        ["Simge durumuna küçült"] = CaptionButton.Minimize,
        ["Pencereyi küçült"] = CaptionButton.Minimize,
        ["Maximise"] = CaptionButton.Maximize,
        ["Maximize"] = CaptionButton.Maximize,
        ["Restore"] = CaptionButton.Maximize,
        ["Restore down"] = CaptionButton.Maximize,
        ["Maximize window"] = CaptionButton.Maximize,
        ["Restore window"] = CaptionButton.Maximize,
        ["Ekranı kapla"] = CaptionButton.Maximize,
        ["Büyüt"] = CaptionButton.Maximize,
        ["Geri yükle"] = CaptionButton.Maximize,
        ["Önceki boyut"] = CaptionButton.Maximize,
        ["Close"] = CaptionButton.Close,
        ["Close window"] = CaptionButton.Close,
        ["Kapat"] = CaptionButton.Close,
        ["Pencereyi kapat"] = CaptionButton.Close,
        ["Tab search"] = CaptionButton.TabSearch,
        ["Search tabs"] = CaptionButton.TabSearch,
        ["Sekme araması"] = CaptionButton.TabSearch,
        ["Sekmelerde ara"] = CaptionButton.TabSearch,
        ["Sekmeleri ara"] = CaptionButton.TabSearch
    };
    sealed record Snapshot(IntPtr Hwnd, uint Pid, Rect Window, uint Dpi, Rect Minimize, Rect Transparency);
    readonly Dispatcher dispatcher;
    Snapshot? snapshot;
    bool busy, disposed;
    IntPtr failedWindow;
    IntPtr pendingWindow;
    DateTime retryAfter;
    long generation;
    public event Action? BoundsAvailable;

    public AccessibilityCaptionLocator(Dispatcher dispatcher) => this.dispatcher = dispatcher;

    public static bool IsBraveWindow(IntPtr h) => NativeMethods.GetWindowThreadProcessId(h, out uint pid) != 0 && string.Equals(Path.GetFileName(PrivilegeHelper.Executable(pid)), "brave.exe", StringComparison.OrdinalIgnoreCase);

    public bool TryGetTransparency(IntPtr h, out Rect result)
    {
        result = default;
        if (!TryGetMinimize(h, out _) || snapshot is not { } cached || !NativeMethods.GetWindowRect(h, out var window))
            return false;
        int x = window.Left - cached.Window.Left, y = window.Top - cached.Window.Top;
        result = new(cached.Transparency.Left + x, cached.Transparency.Top + y, cached.Transparency.Right + x, cached.Transparency.Bottom + y);
        return true;
    }

    public bool TryGetMinimize(IntPtr h, out Rect result)
    {
        result = default;
        if (disposed || !NativeMethods.GetWindowRect(h, out var window) || NativeMethods.GetWindowThreadProcessId(h, out uint pid) == 0)
            return false;
        uint dpi = NativeMethods.GetDpiForWindow(h);
        if (snapshot is { } cached && cached.Hwnd == h && cached.Pid == pid && cached.Dpi == dpi && cached.Window.Width == window.Width && cached.Window.Height == window.Height)
        {
            int x = window.Left - cached.Window.Left, y = window.Top - cached.Window.Top;
            result = new(cached.Minimize.Left + x, cached.Minimize.Top + y, cached.Minimize.Right + x, cached.Minimize.Bottom + y);
            return true;
        }
        if (busy || (failedWindow == h && DateTime.UtcNow < retryAfter))
            return false;
        var cls = new StringBuilder(256);
        string executable = Path.GetFileName(PrivilegeHelper.Executable(pid)) ?? "";
        if (NativeMethods.GetClassName(h, cls, cls.Capacity) == 0)
            return false;
        bool chromiumCaption = cls.ToString() == "Chrome_WidgetWin_1" &&
            (string.Equals(executable, "ChatGPT.exe", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(executable, "brave.exe", StringComparison.OrdinalIgnoreCase));
        // Both installed WhatsApp channels use this WinUI host and executable.
        bool whatsAppCaption = cls.ToString() == "WinUIDesktopWin32WindowClass" &&
            string.Equals(executable, "WhatsApp.Root.exe", StringComparison.OrdinalIgnoreCase);
        if (!chromiumCaption && !whatsAppCaption)
            return false;

        busy = true;
        pendingWindow = h;
        long requestGeneration = generation;
        // UIA can wait on the target's provider. Never query it on the WPF/message-hook thread.
        _ = Task.Run(() =>
        {
            Rect minimize = default;
            Rect transparency = default;
            bool found = false;
            try
            {
                found = TryReadVerifiedBounds(h, window, out minimize, out transparency);
            }
            catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or COMException) { }
            catch (Exception e) { new Services.LoggingService().Write("CaptionAccessibilityQuery", h, e.Message); }
            if (dispatcher.HasShutdownStarted)
                return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                busy = false;
                if (disposed || requestGeneration != generation)
                    return;
                if (found && NativeMethods.IsWindow(h) && NativeMethods.GetWindowThreadProcessId(h, out uint currentPid) != 0 && currentPid == pid && NativeMethods.GetWindowRect(h, out var currentWindow) && currentWindow.Equals(window) && NativeMethods.GetDpiForWindow(h) == dpi)
                {
                    snapshot = new(h, pid, window, dpi, minimize, transparency);
                    failedWindow = IntPtr.Zero;
                }
                else if (!found)
                {
                    failedWindow = h;
                    retryAfter = DateTime.UtcNow.AddSeconds(30);
                    new Services.LoggingService().Write("CaptionAccessibilitySkipped", h, "No verified minimize/maximize/close triplet");
                }
                BoundsAvailable?.Invoke();
            }));
        });
        return false;
    }

    public static bool TryReadVerifiedBounds(IntPtr h, Rect window, out Rect result)
        => TryReadVerifiedBounds(h, window, out result, out _);

    public static bool TryReadVerifiedBounds(IntPtr h, Rect window, out Rect result, out Rect transparency)
    {
        result = default;
        transparency = default;
        var root = AutomationElement.FromHandle(h);
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new OrCondition(ButtonNames.Keys.Select(name => (Condition)new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase)).ToArray()));
        var request = new CacheRequest { TreeScope = TreeScope.Element, AutomationElementMode = AutomationElementMode.None };
        request.Add(AutomationElement.NameProperty);
        request.Add(AutomationElement.BoundingRectangleProperty);
        request.Add(AutomationElement.IsOffscreenProperty);
        request.Add(AutomationElement.IsEnabledProperty);
        AutomationElementCollection elements;
        using (request.Activate())
            elements = root.FindAll(TreeScope.Descendants, condition);
        System.Windows.Rect? min = null, max = null, close = null, tabSearch = null;
        double scale = DpiHelper.Scale(h);
        foreach (AutomationElement element in elements)
        {
            var info = element.Cached;
            var bounds = info.BoundingRectangle;
            if (info.IsOffscreen || !info.IsEnabled || bounds.IsEmpty || bounds.Width < 12 * scale || bounds.Width > 100 * scale || bounds.Height < 12 * scale || bounds.Height > 64 * scale || bounds.Top < window.Top - 2 * scale || bounds.Bottom > window.Top + 64 * scale || bounds.Left < window.Right - 320 * scale || bounds.Right > window.Right + 3 * scale)
                continue;
            if (!ButtonNames.TryGetValue(info.Name, out var kind))
                continue;
            switch (kind)
            {
                case CaptionButton.Minimize:
                    if (min != null)
                        return false;
                    min = bounds;
                    break;
                case CaptionButton.Maximize:
                    if (max != null)
                        return false;
                    max = bounds;
                    break;
                case CaptionButton.Close:
                    if (close != null)
                        return false;
                    close = bounds;
                    break;
                case CaptionButton.TabSearch:
                    if (tabSearch != null)
                        return false;
                    tabSearch = bounds;
                    break;
            }
        }
        if (min is not { } a || max is not { } b || close is not { } c || !Aligned(a, b, scale) || !Aligned(b, c, scale))
            return false;
        result = new((int)Math.Round(a.Left), (int)Math.Round(a.Top), (int)Math.Round(a.Right), (int)Math.Round(a.Bottom));
        int right = result.Left;
        if (IsBraveWindow(h) && tabSearch is { } tab && tab.Left < a.Left && tab.Right <= a.Left + 3 * scale && a.Left - tab.Right <= 64 * scale && Math.Abs(tab.Top - a.Top) <= 3 * scale && Math.Abs(tab.Bottom - a.Bottom) <= 3 * scale)
            right = (int)Math.Floor(tab.Left - 2 * scale);
        transparency = new(right - result.Width, result.Top, right, result.Bottom);
        return result.Width > 0 && result.Height > 0 && transparency.Left >= window.Left;
    }

    static bool Aligned(System.Windows.Rect left, System.Windows.Rect right, double scale) =>
        left.Left < right.Left && Math.Abs(left.Right - right.Left) <= 3 * scale && Math.Abs(left.Top - right.Top) <= 2 * scale && Math.Abs(left.Bottom - right.Bottom) <= 2 * scale && Math.Abs(left.Width - right.Width) <= 3 * scale;

    public void ForgetDestroyed(IntPtr h)
    {
        if (snapshot?.Hwnd == h)
            snapshot = null;
        if (failedWindow == h)
            failedWindow = IntPtr.Zero;
        if (pendingWindow == h)
            generation++;
    }

    public void Dispose()
    {
        disposed = true;
        generation++;
        snapshot = null;
    }
}
