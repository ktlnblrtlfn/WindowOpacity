using System;
using System.Windows;
using WindowOpacity.Core;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
using WindowOpacity.Services;
namespace WindowOpacity.UI;
public partial class SettingsWindow : Window
{
    readonly SettingsService settings; readonly WindowTracker tracker;
    public SettingsWindow(SettingsService s, WindowTracker t)
    {
        InitializeComponent();
        settings = s;
        tracker = t;
        Load();
        Minimum.ValueChanged += (_, _) => MinimumLabel.Text = $"{Minimum.Value:0}%";
        Save.Click += (_, _) => Store();
        Reset.Click += (_, _) => { try { StartupService.SetEnabled(false); tracker.ResetAll(); s.Reset(); Load(); } catch (Exception e) { Status.Text = e.Message; } };
        Add.Click += (_, _) => { var h = t.LastTarget; if (h == IntPtr.Zero || !NativeMethods.IsWindow(h)) { Status.Text = "Activate the application first, then open Settings."; return; } NativeMethods.GetWindowThreadProcessId(h, out var pid); var path = PrivilegeHelper.Executable(pid); if (path != null && !Excluded.Items.Contains(path)) Excluded.Items.Add(path); };
        Remove.Click += (_, _) => { if (Excluded.SelectedItem != null) Excluded.Items.Remove(Excluded.SelectedItem); };
    }
    void Load()
    {
        var v = settings.Value;
        ShowButton.IsChecked = v.ShowTransparencyButton;
        Minimum.Value = v.MinimumOpacity;
        MinimumLabel.Text = $"{v.MinimumOpacity}%";
        Remember.IsChecked = v.RememberOpacityPerApplication;
        Wheel.IsChecked = v.EnableMouseWheel;
        Startup.IsChecked = StartupService.IsEnabled();
        Minimized.IsChecked = v.StartMinimized;
        Maximized.IsChecked = v.ShowOnMaximized;
        Fullscreen.IsChecked = v.DisableInFullscreen;
        Excluded.Items.Clear();
        foreach (var p in v.ExcludedApplications)
            Excluded.Items.Add(p);
    }
    void Store()
    {
        try
        {
            StartupService.SetEnabled(Startup.IsChecked == true);
            var v = settings.Value;
            v.ShowTransparencyButton = ShowButton.IsChecked == true;
            v.MinimumOpacity = (int)Minimum.Value;
            v.RememberOpacityPerApplication = Remember.IsChecked == true;
            v.EnableMouseWheel = Wheel.IsChecked == true;
            v.RunAtStartup = Startup.IsChecked == true;
            v.StartMinimized = Minimized.IsChecked == true;
            v.ShowOnMaximized = Maximized.IsChecked == true;
            v.DisableInFullscreen = Fullscreen.IsChecked == true;
            v.ExcludedApplications.Clear();
            foreach (string p in Excluded.Items)
                v.ExcludedApplications.Add(p);
            Status.Text = settings.Save() ? "Settings saved." : "Could not save settings. Check the log.";
        }
        catch (Exception e) { Status.Text = e.Message; }
    }
}
