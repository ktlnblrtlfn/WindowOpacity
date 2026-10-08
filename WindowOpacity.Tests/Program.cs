using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Automation;
using System.Collections.Generic;
using System.IO;
using WindowOpacity;
using WindowOpacity.Core;
using WindowOpacity.Native;
using WindowOpacity.Services;
using WindowOpacity.UI;
using Forms = System.Windows.Forms;
using Rect = WindowOpacity.Native.Rect;
namespace WindowOpacity.Tests;
internal static class Program
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern void NotifyWinEvent(uint evt, IntPtr h, int obj, int child);
    sealed class FixtureForm : Forms.Form
    {
        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == 0x8010)
            {
                NotifyWinEvent(3, Handle, 0, 0);
                return;
            }
            base.WndProc(ref m);
        }
    }
    static bool Foreground(IntPtr h)
    {
        for (int i = 0; i < 3; i++)
        {
            uint current = GetCurrentThreadId();
            uint fg = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
            uint target = NativeMethods.GetWindowThreadProcessId(h, out _);
            bool a = current != fg && AttachThreadInput(current, fg, true);
            bool b = current != target && target != fg && AttachThreadInput(current, target, true);
            try
            {
                SetForegroundWindow(h);
            }
            finally { if (b) AttachThreadInput(current, target, false); if (a) AttachThreadInput(current, fg, false); }
            NativeMethods.GetWindowThreadProcessId(h, out var fixturePid);
            if (string.Equals(PrivilegeHelper.Executable(fixturePid), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                SendMessage(h, 0x8010, IntPtr.Zero, IntPtr.Zero);
            Pump(100);
            if (NativeMethods.GetForegroundWindow() == h)
                return true;
        }
        return false;
    }
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(12)] public uint KeyFlags;
        [FieldOffset(20)] public uint MouseFlags;
    }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out System.Drawing.Point point);
    static void EmergencyKeys()
    {
        var keys = new ushort[] { 0x11, 0x12, 0x10, 0x4F };
        var inputs = keys.Select(k => new Input { Type = 1, Key = k }).Concat(keys.Reverse().Select(k => new Input { Type = 1, Key = k, KeyFlags = 2 })).ToArray();
        Assert(SendInput((uint)inputs.Length, inputs, 40) == inputs.Length, "emergency keyboard input accepted");
    }
    static void OutsideClick(IntPtr h)
    {
        NativeMethods.GetWindowRect(h, out var rect);
        GetCursorPos(out var old);
        try
        {
            SetCursorPos(rect.Left + 80, rect.Top + 100);
            var input = new[] { new Input { MouseFlags = 2 }, new Input { MouseFlags = 4 } };
            Assert(SendInput(2, input, 40) == 2, "outside-click input accepted");
            Pump(200);
        }
        finally { SetCursorPos(old.X, old.Y); }
    }
    static int count;
    delegate bool EnumProc(IntPtr h, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr data);
    static List<IntPtr> Windows(uint pid)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { NativeMethods.GetWindowThreadProcessId(h, out var id); if (id == pid) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    static void Smoke(IntPtr provided = default)
    {
        using var fixture = provided == IntPtr.Zero ? Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fixture classic") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }) : null;
        var h = fixture != null ? new IntPtr(long.Parse(fixture.StandardOutput.ReadLine()!)) : provided;
        NativeMethods.TryStyle(h, -20, out var original);
        string exe = Environment.GetEnvironmentVariable("WINDOWOPACITY_TEST_EXE") ?? Path.Combine(AppContext.BaseDirectory, "WindowOpacity.exe");
        using var utility = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            Pump(1200);
            Assert(!utility.HasExited, "production app starts in tray");
            Foreground(h);
            Pump(300);
            var locator = new TitleBarLocator();
            locator.TryGetTransparencyButtonBounds(h, out var button);
            var overlay = Windows((uint)utility.Id).FirstOrDefault(w => NativeMethods.IsWindowVisible(w) && NativeMethods.GetWindowRect(w, out var r) && r.Equals(button));
            Assert(overlay != IntPtr.Zero, "production caption overlay placement");
            var control = AutomationElement.FromHandle(overlay).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "Control"));
            Assert(control != null, "caption button automation available");
            ((InvokePattern)control!.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            IntPtr popup = IntPtr.Zero;
            for (int attempt = 0; attempt < 20 && popup == IntPtr.Zero; attempt++)
            {
                Pump(100);
                popup = Windows((uint)utility.Id).FirstOrDefault(w => w != overlay && NativeMethods.IsWindowVisible(w));
            }
            Assert(popup != IntPtr.Zero, "clicking production caption opens popup");
            NativeMethods.TryStyle(popup, -20, out var popupEx);
            NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out var fgPid);
            Foreground(h);
            Pump(100);
            var slider = AutomationElement.FromHandle(popup).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "OpacitySlider"));
            ((RangeValuePattern)slider.GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(60);
            Pump(600);
            Assert(NativeMethods.GetLayeredWindowAttributes(h, out _, out var alpha, out _) && alpha == 153, "production popup applies real 60% alpha");
            Assert(NativeMethods.GetForegroundWindow() == h, "production popup retains target foreground");
            var reset = AutomationElement.FromHandle(popup).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ResetButton"));
            ((InvokePattern)reset.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            Pump(150);
            NativeMethods.TryStyle(h, -20, out var resetStyle);
            Assert(resetStyle == original, "production popup reset restores style");
            ((RangeValuePattern)slider.GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(60);
            Pump(150);
            EmergencyKeys();
            Pump(300);
            NativeMethods.TryStyle(h, -20, out var emergencyStyle);
            Assert(emergencyStyle == original, "registered production emergency hotkey restores style");
            OutsideClick(h);
            Assert(!NativeMethods.IsWindowVisible(popup), "outside click closes production popup");
            NativeMethods.GetWindowRect(h, out var targetBounds);
            NativeMethods.SetWindowPos(h, IntPtr.Zero, targetBounds.Left - 45, targetBounds.Top + 20, targetBounds.Width, targetBounds.Height, 0x14);
            Pump(300);
            locator.TryGetTransparencyButtonBounds(h, out button);
            NativeMethods.GetWindowRect(overlay, out var moved);
            Assert(moved.Equals(button), "production movement follows actual target");
            ShowWindow(h, 3);
            Pump(300);
            locator.TryGetTransparencyButtonBounds(h, out button);
            NativeMethods.GetWindowRect(overlay, out var maximized);
            Assert(NativeMethods.IsWindowVisible(overlay) && maximized.Equals(button), "production maximized caption placement");
            ShowWindow(h, 9);
            Foreground(h);
            Pump(300);
            ((InvokePattern)control.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            Pump(200);
            ((RangeValuePattern)slider.GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(50);
            Pump(150);
            using var exit = Process.Start(new ProcessStartInfo(exe, "--exit") { UseShellExecute = false, CreateNoWindow = true })!;
            Assert(utility.WaitForExit(5000) && utility.ExitCode == 0, "production app clean exit");
            NativeMethods.TryStyle(h, -20, out var restored);
            Assert(restored == original, "production exit restores modified window");
        }
        finally { if (!utility.HasExited) { using var exit = Process.Start(new ProcessStartInfo(exe, "--exit") { UseShellExecute = false, CreateNoWindow = true }); utility.WaitForExit(5000); } if (NativeMethods.IsWindow(h)) SendMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero); fixture?.WaitForExit(2000); }
    }
    static void Notepad()
    {
        if (Process.GetProcessesByName("notepad").Length > 0)
        {
            Console.WriteLine("SKIP: Notepad already open; existing documents were not touched.");
            return;
        }
        using var started = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true })!;
        IntPtr h = IntPtr.Zero;
        for (int i = 0; i < 20 && h == IntPtr.Zero; i++)
        {
            Pump(250);
            foreach (var p in Process.GetProcessesByName("notepad"))
            {
                using (p)
                    h = Windows((uint)p.Id).FirstOrDefault(NativeMethods.IsWindowVisible);
                if (h != IntPtr.Zero)
                    break;
            }
        }
        Assert(h != IntPtr.Zero, "Notepad launched");
        var service = new WindowOpacityService(new());
        try
        {
            var locator = new TitleBarLocator();
            Assert(locator.TryGetTransparencyButtonBounds(h, out var bounds), "installed Notepad caption location");
            NativeMethods.TryStyle(h, -20, out var original);
            Assert(service.SetOpacity(h, 60), "installed Notepad 60% opacity");
            Assert(NativeMethods.GetLayeredWindowAttributes(h, out _, out var alpha, out _) && alpha == 153, "installed Notepad alpha verified");
            Assert(service.RestoreAllWindows(), "installed Notepad reset");
            NativeMethods.TryStyle(h, -20, out var restored);
            Assert(original == restored, "installed Notepad style restored");
            Console.WriteLine($"Notepad button: {bounds.Left},{bounds.Top} {bounds.Width}x{bounds.Height}");
            Smoke(h);
        }
        finally { service.RestoreAllWindows(); if (NativeMethods.IsWindow(h)) SendMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero); }
    }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "identity")
            return IdentityTests.Run();
        if (args.Length == 1 && args[0] == "recovery")
            return RecoveryTests.Run();
        if (args.Length == 2 && args[0] == "activate-verify-overlay")
        {
            var target = new IntPtr(long.Parse(args[1]));
            var previous = NativeMethods.GetForegroundWindow();
            try
            {
                if (!Foreground(target))
                    throw new InvalidOperationException("Could not activate target for placement verification.");
                Pump(1800);
                WindowProbe.VerifyOverlay(target);
            }
            finally
            {
                if (NativeMethods.GetForegroundWindow() == target && NativeMethods.IsWindow(previous))
                    Foreground(previous);
            }
            return 0;
        }
        if (args.Length == 2 && args[0] == "verify-overlay")
        {
            WindowProbe.VerifyOverlay(new IntPtr(long.Parse(args[1])));
            return 0;
        }
        if (args.Length == 2 && args[0] == "inspect")
        {
            WindowProbe.Inspect(new IntPtr(long.Parse(args[1])));
            return 0;
        }
        if (args.Length == 2 && args[0] == "inspect-process")
        {
            WindowProbe.InspectProcess(args[1]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "verify-caption")
        {
            WindowProbe.VerifyCaption(new IntPtr(long.Parse(args[1])));
            return 0;
        }
        if (args.Length > 0 && args[0] == "--fixture")
        {
            if (args.Contains("wpf"))
            {
                var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var w = new Window { Title = "WindowOpacity WPF fixture", Width = 750, Height = 480, Content = new System.Windows.Controls.TextBox { Text = "WindowOpacity integration fixture" } };
                w.Loaded += (_, _) => { var handle = new WindowInteropHelper(w).Handle; HwndSource.FromHwnd(handle).AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) => { if (msg == 0x8010) { NotifyWinEvent(3, hwnd, 0, 0); handled = true; } return IntPtr.Zero; }); Console.WriteLine(handle.ToInt64()); };
                app.Run(w);
            }
            else
            {
                Forms.Application.EnableVisualStyles();
                var form = new FixtureForm { Text = "WindowOpacity Win32 fixture", Width = 750, Height = 480 };
                form.Controls.Add(new Forms.TextBox { Text = "WindowOpacity integration fixture", Dock = Forms.DockStyle.Fill, Multiline = true });
                form.Shown += (_, _) => Console.WriteLine(form.Handle.ToInt64());
                Forms.Application.Run(form);
            }
            return 0;
        }
        try
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources["Surface"] = System.Windows.Media.Brushes.White;
            app.Resources["Text"] = System.Windows.Media.Brushes.Black;
            app.Resources["Hover"] = System.Windows.Media.Brushes.LightBlue;
            foreach (var type in (args.Length > 0 ? args : new[] { "classic", "wpf" }))
            {
                if (type == "smoke")
                    Smoke();
                else if (type == "notepad")
                    Notepad();
                else
                    Run(type);
            }
            Console.WriteLine($"PASS: {count} integration assertions.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Assert(bool value, string name)
    {
        if (!value)
            throw new InvalidOperationException("FAIL: " + name);
        count++;
        Console.WriteLine("PASS: " + name);
    }
    static void Pump(int ms = 250)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        var frame = new DispatcherFrame();
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    static void Run(string kind)
    {
        using var fixture = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fixture " + kind) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })!;
        var line = fixture.StandardOutput.ReadLine();
        var h = new IntPtr(long.Parse(line!));
        var log = new LoggingService();
        var opacity = new WindowOpacityService(log);
        WindowTracker? tracker = null;
        try
        {
            Pump();
            Assert(NativeMethods.IsWindow(h), kind + " fixture valid");
            NativeMethods.TryStyle(h, -20, out var original);
            var settings = new SettingsService(log, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowOpacity.Tests", Guid.NewGuid().ToString("N")));
            settings.Value.ShowTransparencyButton = true;
            settings.Value.RememberOpacityPerApplication = false;
            settings.Value.ExcludedApplications.Clear();
            var filter = new WindowFilter(settings, opacity, log);
            Assert(filter.IsEligibleTargetWindow(h), kind + " eligible");
            Assert(!filter.IsEligibleTargetWindow(IntPtr.Zero), "null HWND rejected");
            var locator = new TitleBarLocator();
            Assert(locator.TryGetMinimizeButtonBounds(h, out var min), kind + " native minimize bounds");
            Assert(locator.TryGetTransparencyButtonBounds(h, out var button) && button.Right == min.Left && button.Height == min.Height, kind + " overlay immediately left");
            bool applied = opacity.SetOpacity(h, 60);
            if (kind == "wpf" && !applied)
            {
                NativeMethods.TryStyle(h, -20, out var untouched);
                Assert(untouched == original && !opacity.IsTracked(h), "WPF renderer rejects layering safely and original style is preserved");
                Console.WriteLine("COMPATIBILITY: this WPF renderer clears external WS_EX_LAYERED; target is unsupported.");
                return;
            }
            Assert(applied, kind + " set 60%");
            Assert(NativeMethods.GetLayeredWindowAttributes(h, out _, out var alpha, out var flags) && alpha == 153 && flags == 2, kind + " real Win32 alpha 153");
            Assert(opacity.ResetOpacity(h), kind + " reset");
            NativeMethods.TryStyle(h, -20, out var restored);
            Assert(restored == original, kind + " exact original style restored");
            NativeMethods.SetStyle(h, -20, original | NativeMethods.Layered);
            NativeMethods.SetLayeredWindowAttributes(h, 0, 190, 2);
            Assert(!filter.IsEligibleTargetWindow(h) && !opacity.SetOpacity(h, 40), kind + " pre-layered target rejected");
            NativeMethods.GetLayeredWindowAttributes(h, out _, out var before, out _);
            Assert(before == 190, kind + " existing alpha preserved");
            NativeMethods.SetStyle(h, -20, original);
            Assert(opacity.SetOpacity(h, 20), kind + " minimum alpha");
            Assert(opacity.SetOpacity(h, 0) && opacity.GetOpacity(h) == 20, kind + " zero clamped");
            Assert(opacity.RestoreAllWindows(), kind + " restore all");
            tracker = new(Dispatcher.CurrentDispatcher, settings, opacity, log);
            Assert(Foreground(h), kind + " test foreground acquired");
            Pump(100);
            Assert(Foreground(h), kind + " foreground stable");
            Pump(200);
            var overlay = System.Windows.Application.Current.Windows.OfType<TransparencyOverlay>().Single();
            Assert(overlay.IsVisible, kind + " foreground overlay visible");
            NativeMethods.GetWindowRect(overlay.Handle, out var actual);
            locator.TryGetTransparencyButtonBounds(h, out button);
            Assert(actual.Equals(button), kind + " overlay physical placement");
            Assert(NativeMethods.GetForegroundWindow() == h, kind + " overlay preserves focus");
            NativeMethods.GetWindowRect(h, out var bounds);
            NativeMethods.SetWindowPos(h, IntPtr.Zero, bounds.Left + 55, bounds.Top + 35, bounds.Width + 30, bounds.Height, 0x14);
            Pump();
            locator.TryGetTransparencyButtonBounds(h, out button);
            NativeMethods.GetWindowRect(overlay.Handle, out actual);
            Assert(actual.Equals(button), kind + " movement/resize event follows");
            ShowWindow(h, 3);
            Pump();
            locator.TryGetTransparencyButtonBounds(h, out button);
            NativeMethods.GetWindowRect(overlay.Handle, out actual);
            Assert(overlay.IsVisible && actual.Equals(button), kind + " maximized placement");
            ShowWindow(h, 6);
            Pump();
            Assert(!overlay.IsVisible, kind + " minimize hides");
            ShowWindow(h, 9);
            Foreground(h);
            Pump();
            Assert(overlay.IsVisible, kind + " restore shows");
            var popup = System.Windows.Application.Current.Windows.OfType<OpacityPopup>().Single();
            locator.TryGetTransparencyButtonBounds(h, out button);
            popup.ShowFor(h, button, 100, 20);
            Pump();
            var slider = (System.Windows.Controls.Slider)popup.FindName("OpacitySlider");
            slider.Value = 60;
            Assert(opacity.GetOpacity(h) == 60, kind + " popup slider changes live");
            Assert(NativeMethods.GetForegroundWindow() == h, kind + " popup preserves focus");
            NativeMethods.GetWindowRect(popup.Handle, out var p);
            MonitorHelper.TryGet(h, out var monitor);
            Assert(p.Left >= monitor.Work.Left && p.Top >= monitor.Work.Top && p.Right <= monitor.Work.Right && p.Bottom <= monitor.Work.Bottom, kind + " popup within work area");
            tracker.EmergencyReset();
            Assert(!opacity.IsTracked(h), kind + " emergency reset");
            opacity.SetOpacity(h, 60);
            tracker.Enabled = false;
            NativeMethods.TryStyle(h, -20, out restored);
            Assert(!overlay.IsVisible && !popup.IsVisible && restored == original, kind + " disable hides and restores");
            tracker.Enabled = true;
            Pump();
            Assert(overlay.IsVisible, kind + " re-enable");
            opacity.SetOpacity(h, 50);
            Assert(opacity.RestoreAllWindows(), kind + " shutdown restore");
            SendMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero);
            Pump();
            Assert(!overlay.IsVisible || (tracker.LastTarget != h && NativeMethods.GetForegroundWindow() != h), kind + " destroyed target detached");
        }
        finally { opacity.RestoreAllWindows(); tracker?.Dispose(); if (!fixture.HasExited) { SendMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero); fixture.WaitForExit(2000); if (!fixture.HasExited) fixture.Kill(); } }
    }
}
