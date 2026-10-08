using System;
using System.Collections.Generic;
using System.Windows.Threading;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
using WindowOpacity.Services;
using WindowOpacity.UI;
namespace WindowOpacity.Core;
public sealed class WindowTracker : IDisposable
{
    readonly Dispatcher dispatcher; readonly WindowFilter filter; readonly WindowOpacityService opacity; readonly SettingsService settings; readonly LoggingService log; readonly TitleBarLocator locator; readonly TransparencyOverlay overlay = new(); readonly OpacityPopup popup = new(); readonly WinEventHook hooks; readonly DispatcherTimer safety; readonly DispatcherTimer saveRules; readonly MouseDismissService dismiss; readonly HashSet<IntPtr> remembered = new(); readonly HashSet<IntPtr> denied = new(); bool pending, disposed, enabled = true; IntPtr target; Rect anchor;
    public IntPtr LastTarget
    {
        get; private set;
    }
    public bool Enabled
    {
        get => enabled; set
        {
            enabled = value;
            remembered.Clear();
            if (!value)
                opacity.RestoreAllWindows();
            Update();
        }
    }
    public WindowTracker(Dispatcher d, SettingsService s, WindowOpacityService o, LoggingService l)
    {
        locator = new(d);
        locator.BoundsAvailable += RequestUpdate;
        dispatcher = d;
        settings = s;
        opacity = o;
        log = l;
        filter = new(s, o, l);
        dismiss = new(d, ClosePopup, l);
        overlay.OpenRequested += () => { if (target != IntPtr.Zero) { if (popup.IsVisible) { ClosePopup(); return; } popup.ShowFor(target, anchor, opacity.GetOpacity(target), s.Value.MinimumOpacity); dismiss.Start(popup.Handle, anchor); } };
        overlay.ResetRequested += Reset;
        overlay.WheelRequested += delta => { if (s.Value.EnableMouseWheel) Apply(opacity.GetOpacity(target) + delta); };
        popup.ValueChanged += Apply;
        popup.ResetRequested += Reset;
        // Install on the WPF dispatcher thread, which provides the required message pump.
        overlay.Activated += (_, _) => RestoreTargetFocus();
        popup.Activated += (_, _) => RestoreTargetFocus();
        hooks = new WinEventHook(OnEvent, l);
        s.Changed += SettingsChanged;
        saveRules = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveRules.Tick += (_, _) => { saveRules.Stop(); settings.Save(false); };
        safety = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Update(), d);
        Update();
    }
    void RestoreTargetFocus()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (target != IntPtr.Zero && NativeMethods.IsWindow(target) && (foreground == overlay.Handle || foreground == popup.Handle))
            NativeMethods.SetForegroundWindow(target);
    }
    void SettingsChanged()
    {
        remembered.Clear();
        RequestUpdate();
    }
    void OnEvent(uint evt, IntPtr h, int obj, int child)
    {
        if (disposed)
            return;
        if (evt >= 0x8000 && (obj != 0 || child != 0))
            return;
        if (evt == EventDestroy)
        {
            dispatcher.BeginInvoke(new Action(() => { opacity.ForgetDestroyed(h); locator.ForgetDestroyed(h); remembered.Remove(h); denied.Remove(h); }));
        }
        if (evt == EventForeground || h == target || h == NativeMethods.GetForegroundWindow())
            RequestUpdate();
    }
    void RequestUpdate()
    {
        if (pending || disposed)
            return;
        pending = true;
        dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { pending = false; if (!disposed) Update(); }));
    }
    bool IsOwn(IntPtr h) => NativeMethods.GetWindowThreadProcessId(h, out var pid) != 0 && pid == (uint)Environment.ProcessId;
    void Update()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (IsOwn(foreground))
        {
            Hide();
            return;
        }
        if (!enabled || !settings.Value.ShowTransparencyButton || denied.Contains(foreground) || !filter.IsEligibleTargetWindow(foreground) || !locator.TryGetTransparencyButtonBounds(foreground, out var bounds))
        {
            Hide();
            return;
        }
        if (target != foreground)
        {
            ClosePopup();
            target = foreground;
            LastTarget = foreground;
        }
        bool moved = !anchor.Equals(bounds);
        anchor = bounds;
        if (settings.Value.RememberOpacityPerApplication && remembered.Add(target))
        {
            NativeMethods.GetWindowThreadProcessId(target, out var pid);
            var path = PrivilegeHelper.Executable(pid);
            if (path != null && settings.Value.OpacityRules.TryGetValue(ApplicationIdentity.FromPath(path), out int percent) && !opacity.SetOpacity(target, Math.Clamp(percent, settings.Value.MinimumOpacity, 100)))
            {
                denied.Add(target);
                Hide();
                return;
            }
        }
        if (!overlay.Place(bounds))
        {
            log.Write("OverlayPosition", target, error: System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            Hide();
            return;
        }
        if (popup.IsVisible && moved)
        {
            popup.ShowFor(target, anchor, opacity.GetOpacity(target), settings.Value.MinimumOpacity);
            dismiss.Start(popup.Handle, anchor);
        }
    }
    void ClosePopup()
    {
        dismiss.Dispose();
        popup.Hide();
    }
    void Hide()
    {
        overlay.Hide();
        ClosePopup();
        target = IntPtr.Zero;
    }
    void Apply(int value)
    {
        if (target == IntPtr.Zero || NativeMethods.GetForegroundWindow() != target)
            return;
        value = Math.Clamp(value, settings.Value.MinimumOpacity, 100);
        if (!opacity.SetOpacity(target, value))
        {
            popup.ShowError();
            denied.Add(target);
            RequestUpdate();
            return;
        }
        popup.SetValue(value, settings.Value.MinimumOpacity);
        if (settings.Value.RememberOpacityPerApplication)
        {
            NativeMethods.GetWindowThreadProcessId(target, out var pid);
            var path = PrivilegeHelper.Executable(pid);
            if (path != null)
            {
                settings.Value.OpacityRules[ApplicationIdentity.FromPath(path)] = value;
                saveRules.Stop();
                saveRules.Start();
            }
        }
    }
    public void Reset()
    {
        if (target != IntPtr.Zero)
            Apply(100);
    }
    public void EmergencyReset()
    {
        var h = NativeMethods.GetForegroundWindow();
        opacity.ResetOpacity(h);
        denied.Remove(h);
        if (settings.Value.RememberOpacityPerApplication)
        {
            NativeMethods.GetWindowThreadProcessId(h, out var pid);
            var path = PrivilegeHelper.Executable(pid);
            if (path != null)
            {
                settings.Value.OpacityRules.Remove(ApplicationIdentity.FromPath(path));
                settings.Save();
            }
        }
        popup.SetValue(100, settings.Value.MinimumOpacity);
    }
    public void ResetAll()
    {
        opacity.RestoreAllWindows();
        settings.Value.OpacityRules.Clear();
        settings.Save();
        popup.SetValue(100, settings.Value.MinimumOpacity);
    }
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        settings.Changed -= SettingsChanged;
        safety.Stop();
        if (saveRules.IsEnabled)
        {
            saveRules.Stop();
            settings.Save(false);
        }
        hooks.Dispose();
        locator.BoundsAvailable -= RequestUpdate;
        locator.Dispose();
        dismiss.Dispose();
        popup.Close();
        overlay.Close();
    }
}
