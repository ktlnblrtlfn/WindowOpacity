using System;
using System.Text;
using System.Runtime.InteropServices;
using WindowOpacity.Native;
using WindowOpacity.Core;
namespace WindowOpacity.Tests;
internal static class WindowProbe
{
    delegate bool EnumProc(IntPtr h, IntPtr data);
    public static void VerifyCaption(IntPtr h)
    {
        var verified = System.Threading.Tasks.Task.Run(() =>
        {
            NativeMethods.GetWindowRect(h, out var window);
            bool found = AccessibilityCaptionLocator.TryReadVerifiedBounds(h, window, out var minimize);
            NativeMethods.GetWindowRect(h, out var after);
            if (!found || !after.Equals(window))
                throw new InvalidOperationException("Caption triplet was not verified against stable window bounds.");
            Console.WriteLine($"PASS: measured minimize={minimize.Left},{minimize.Top},{minimize.Right},{minimize.Bottom}; transparency={minimize.Left - minimize.Width},{minimize.Top},{minimize.Width},{minimize.Height}; DPI={NativeMethods.GetDpiForWindow(h)}");
        });
        verified.GetAwaiter().GetResult();
    }
    public static void VerifyOverlay(IntPtr h)
    {
        NativeMethods.GetWindowRect(h, out var window);
        var expected = System.Threading.Tasks.Task.Run(() =>
        {
            if (!AccessibilityCaptionLocator.TryReadVerifiedBounds(h, window, out _, out var measured))
                throw new InvalidOperationException("Caption not verified.");
            return measured;
        }).GetAwaiter().GetResult();
        var ids = new System.Collections.Generic.HashSet<uint>();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("WindowOpacity"))
        {
            ids.Add((uint)p.Id);
            p.Dispose();
        }
        bool matched = false;
        EnumWindows((candidate, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(candidate, out var pid);
            if (ids.Contains(pid) && NativeMethods.IsWindowVisible(candidate) && NativeMethods.GetWindowRect(candidate, out var actual))
            {
                Console.WriteLine($"visibleOverlay={actual.Left},{actual.Top},{actual.Right},{actual.Bottom}");
                matched |= actual.Equals(expected);
            }
            return true;
        }, IntPtr.Zero);
        NativeMethods.TryStyle(h, -20, out var ex);
        Console.WriteLine($"foreground={NativeMethods.GetForegroundWindow():X}, target={h:X}, expected={expected.Left},{expected.Top},{expected.Right},{expected.Bottom}, matched={matched}, targetExStyle={ex:X}");
        if (NativeMethods.GetForegroundWindow() == h && !matched)
            throw new InvalidOperationException("Live caption overlay does not match measured position.");
    }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr data);
    public static void InspectProcess(string name)
    {
        var ids = new System.Collections.Generic.HashSet<uint>();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
        {
            ids.Add((uint)p.Id);
            p.Dispose();
        }
        EnumWindows((h, _) => { NativeMethods.GetWindowThreadProcessId(h, out var pid); if (ids.Contains(pid) && NativeMethods.IsWindowVisible(h)) Inspect(h); return true; }, IntPtr.Zero);
    }
    public static void Inspect(IntPtr h)
    {
        var cls = new StringBuilder(256);
        NativeMethods.GetClassName(h, cls, cls.Capacity);
        NativeMethods.GetWindowThreadProcessId(h, out var pid);
        NativeMethods.TryStyle(h, -16, out var style);
        NativeMethods.TryStyle(h, -20, out var ex);
        var owner = NativeMethods.GetWindow(h, 4);
        Console.WriteLine($"hwnd={h:X}, pid={pid}, class={cls}, style={style:X}, ex={ex:X}, owner={owner:X}, ownerVisible={NativeMethods.IsWindowVisible(owner)}");
        Console.WriteLine($"visible={NativeMethods.IsWindowVisible(h)}, iconic={NativeMethods.IsIconic(h)}, zoomed={NativeMethods.IsZoomed(h)}, root={NativeMethods.GetAncestor(h, 2):X}, integrityAccessible={PrivilegeHelper.CanAccess(pid)}");
        NativeMethods.GetWindowRect(h, out var bounds);
        Console.WriteLine($"window={bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}");
        var info = new TitlebarInfo { Size = (uint)Marshal.SizeOf<TitlebarInfo>(), States = new uint[6], Rectangles = new Rect[6] };
        var result = NativeMethods.SendMessageTimeout(h, 0x033F, IntPtr.Zero, ref info, 2, 100, out _);
        Console.WriteLine($"titlebarResult={result}, error={Marshal.GetLastWin32Error()}");
        for (int i = 0; i < 6; i++)
        {
            var r = info.Rectangles[i];
            Console.WriteLine($"caption[{i}] state={info.States[i]:X} rect={r.Left},{r.Top},{r.Right},{r.Bottom}");
        }
        int hr = NativeMethods.DwmRect(h, 5, out var cluster, 16);
        Console.WriteLine($"dwmCaption={hr:X} rect={cluster.Left},{cluster.Top},{cluster.Right},{cluster.Bottom}");
        bool layered = NativeMethods.GetLayeredWindowAttributes(h, out var key, out var alpha, out var flags);
        Console.WriteLine($"layeredAttributesReadable={layered}, alpha={alpha}, flags={flags}, key={key}");
        var opacity = new WindowOpacityService(new());
        var filter = new WindowFilter(new(new()), opacity, new());
        Console.WriteLine($"eligible={filter.IsEligibleTargetWindow(h)}, located={new TitleBarLocator().TryGetTransparencyButtonBounds(h, out _)}");
        if (filter.IsEligibleTargetWindow(h))
        {
            var root = System.Windows.Automation.AutomationElement.FromHandle(h);
            var condition = new System.Windows.Automation.AndCondition(
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button),
                new System.Windows.Automation.OrCondition(
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Minimize"),
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Minimize window"),
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Simge durumuna küçült"),
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Küçült")));
            var buttons = root.FindAll(System.Windows.Automation.TreeScope.Descendants, condition);
            Console.WriteLine($"accessibleMinimizeButtons={buttons.Count}");
            foreach (System.Windows.Automation.AutomationElement b in buttons)
                Console.WriteLine($"captionButton name={b.Current.Name}, id={b.Current.AutomationId}, bounds={b.Current.BoundingRectangle}, offscreen={b.Current.IsOffscreen}");
            var all = root.FindAll(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button));
            double scale = DpiHelper.Scale(h);
            int candidates = 0;
            foreach (System.Windows.Automation.AutomationElement b in all)
            {
                var rect = b.Current.BoundingRectangle;
                if (!rect.IsEmpty && rect.Top >= bounds.Top && rect.Top < bounds.Top + 70 * scale && rect.Left > bounds.Right - 300 * scale)
                {
                    candidates++;
                    Console.WriteLine($"topRightButton name={b.Current.Name}, id={b.Current.AutomationId}, bounds={rect}, offscreen={b.Current.IsOffscreen}");
                }
            }
            Console.WriteLine($"topRightButtonCount={candidates}");
        }
    }
}
