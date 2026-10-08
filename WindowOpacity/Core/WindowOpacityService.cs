using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
using WindowOpacity.Models;
using WindowOpacity.Services;
namespace WindowOpacity.Core;
public sealed class WindowOpacityService(LoggingService log)
{
    delegate bool EnumWindow(IntPtr h, IntPtr data);
    delegate int EnumProperty(IntPtr h, IntPtr name, IntPtr value, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "EnumPropsExW")] static extern int EnumPropsEx(IntPtr h, EnumProperty callback, IntPtr data);
    readonly Dictionary<IntPtr, TargetWindowState> states = new(); readonly string property = "WindowOpacity." + Guid.NewGuid().ToString("N"); long sequence;
    bool Matches(TargetWindowState s) => NativeMethods.IsWindow(s.Hwnd) && NativeMethods.GetWindowThreadProcessId(s.Hwnd, out var pid) != 0 && pid == s.ProcessId && NativeMethods.GetProp(s.Hwnd, property) == s.Marker;
    public bool IsTracked(IntPtr h) => states.TryGetValue(h, out var s) && Matches(s);
    public int GetOpacity(IntPtr h) => IsTracked(h) ? states[h].CurrentOpacity : 100;
    // Call only after acquiring the application's singleton mutex. The previous
    // instance cannot still be running. Unmarked layered renderers remain untouched.
    public void RecoverPreviousInstanceWindows() => EnumWindows((h, _) => { RecoverPreviousInstanceWindow(h); return true; }, IntPtr.Zero);
    public bool RecoverPreviousInstanceWindow(IntPtr h)
    {
        if (IsTracked(h)) return true;
        if (!NativeMethods.IsWindow(h) || !NativeMethods.TryStyle(h, GwlExStyle, out var ex) ||
            (ex & NativeMethods.Layered) == 0 || NativeMethods.GetWindowThreadProcessId(h, out var pid) == 0 ||
            !PrivilegeHelper.CanAccess(pid) || !NativeMethods.GetLayeredWindowAttributes(h, out var key, out var alpha, out var flags) ||
            key != 0 || flags != LwaAlpha)
            return false;
        var oldProperties = new List<string>();
        EnumPropsEx(h, (_, name, value, _) =>
        {
            if (name.ToInt64() > ushort.MaxValue && value.ToInt64() > 0)
            {
                var text = Marshal.PtrToStringUni(name);
                if (text != null && text.StartsWith("WindowOpacity.", StringComparison.Ordinal) &&
                    text != property && Guid.TryParseExact(text[14..], "N", out _))
                    oldProperties.Add(text);
            }
            return 1;
        }, IntPtr.Zero);
        if (oldProperties.Count == 0) return false;
        // Every marked window was originally non-layered: SetOpacity refuses
        // pre-existing layering before adding this marker. HWND properties vanish
        // on destruction, so a reused HWND cannot inherit this ownership proof.
        var marker = new IntPtr(++sequence);
        if (!NativeMethods.SetProp(h, property, marker)) return false;
        states[h] = new(h, pid, ex & ~NativeMethods.Layered, marker)
        {
            CurrentOpacity = Math.Clamp((int)Math.Round(alpha * 100.0 / 255), 20, 100)
        };
        foreach (var oldProperty in oldProperties) NativeMethods.RemoveProp(h, oldProperty);
        log.Write("RecoveredPreviousOpacity", h);
        return true;
    }
    public bool SetOpacity(IntPtr h, int percent)
    {
        percent = Math.Clamp(percent, 20, 100);
        if (percent == 100)
            return RestoreOriginalState(h);
        if (states.TryGetValue(h, out var stale) && !Matches(stale))
            states.Remove(h);
        if (!states.TryGetValue(h, out var state))
        {
            if (!NativeMethods.IsWindow(h) || !NativeMethods.TryStyle(h, GwlExStyle, out var ex) || (ex & NativeMethods.Layered) != 0 || NativeMethods.GetWindowThreadProcessId(h, out var pid) == 0 || !PrivilegeHelper.CanAccess(pid))
                return false;
            state = new(h, pid, ex, new IntPtr(++sequence));
            if (!NativeMethods.SetProp(h, property, state.Marker))
            {
                Fail("TrackWindow", h);
                return false;
            }
            states[h] = state;
            if (!NativeMethods.SetStyle(h, GwlExStyle, ex | NativeMethods.Layered))
            {
                Fail("AddLayered", h);
                RestoreOriginalState(h);
                return false;
            }
        }
        if (!Matches(state))
            return false;
        if (!NativeMethods.TryStyle(h, GwlExStyle, out var activeStyle) || (activeStyle & NativeMethods.Layered) == 0)
        {
            log.Write("TargetRejectedLayering", h, "Target renderer removed WS_EX_LAYERED");
            RestoreOriginalState(h);
            return false;
        }
        if (!NativeMethods.SetLayeredWindowAttributes(h, 0, (byte)Math.Round(percent * 255.0 / 100), LwaAlpha))
        {
            Fail("SetAlpha", h);
            RestoreOriginalState(h);
            return false;
        }
        state.CurrentOpacity = percent;
        return true;
    }
    public bool ResetOpacity(IntPtr h) => RestoreOriginalState(h);
    public bool RestoreOriginalState(IntPtr h)
    {
        if (!states.TryGetValue(h, out var s))
            return true;
        if (!Matches(s))
        {
            states.Remove(h);
            return true;
        }
        if (!NativeMethods.TryStyle(h, GwlExStyle, out var ex))
        {
            Fail("ReadStyleForRestore", h);
            return false;
        }
        // Only revert our layering bit; preserve unrelated changes made by the target.
        var restored = (ex & ~NativeMethods.Layered) | (s.OriginalExtendedStyle & NativeMethods.Layered);
        if (!NativeMethods.SetStyle(h, GwlExStyle, restored))
        {
            Fail("RestoreStyle", h);
            return false;
        }
        NativeMethods.RemoveProp(h, property);
        states.Remove(h);
        if (!NativeMethods.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged))
            Fail("RefreshFrame", h);
        return true;
    }
    public void ForgetDestroyed(IntPtr h)
    {
        if (states.TryGetValue(h, out var s) && !Matches(s))
            states.Remove(h);
    }
    public bool RestoreAllWindows()
    {
        bool ok = true;
        foreach (var h in new List<IntPtr>(states.Keys))
            ok = RestoreOriginalState(h) && ok;
        return ok;
    }
    void Fail(string op, IntPtr h) => log.Write(op, h, error: Marshal.GetLastWin32Error());
}
