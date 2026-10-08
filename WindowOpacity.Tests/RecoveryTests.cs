using System;
using WindowOpacity.Core;
using WindowOpacity.Native;
using WindowOpacity.Services;
using Forms = System.Windows.Forms;
namespace WindowOpacity.Tests;
internal static class RecoveryTests
{
    public static int Run()
    {
        using var fixture = new Forms.Form();
        var h = fixture.Handle;
        var first = new WindowOpacityService(new LoggingService());
        var second = new WindowOpacityService(new LoggingService());
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); count++; }
        NativeMethods.TryStyle(h, -20, out var original);
        try
        {
            Check(first.SetOpacity(h, 70), "Original instance applies opacity");
            Check(second.RecoverPreviousInstanceWindow(h), "New instance recovers marked window");
            Check(second.IsTracked(h) && second.GetOpacity(h) == 70, "Recovered opacity is tracked accurately");
            Check(!first.IsTracked(h), "Old marker relinquished");
            Check(second.SetOpacity(h, 60), "Recovered slider can apply changes");
            Check(second.RestoreAllWindows(), "New instance cleanly restores recovered window");
            NativeMethods.TryStyle(h, -20, out var restored);
            Check(restored == original, "Original style restored");
            NativeMethods.SetStyle(h, -20, original | NativeMethods.Layered);
            NativeMethods.SetLayeredWindowAttributes(h, 0, 190, 2);
            Check(!second.RecoverPreviousInstanceWindow(h), "Unmarked renderer refused");
            NativeMethods.GetLayeredWindowAttributes(h, out _, out var alpha, out _);
            Check(alpha == 190, "Unmarked alpha preserved");
        }
        finally { second.RestoreAllWindows(); first.RestoreAllWindows(); NativeMethods.SetStyle(h, -20, original); }
        Console.WriteLine($"PASS: {count} recovery assertions on an isolated hidden fixture.");
        return 0;
    }
}
