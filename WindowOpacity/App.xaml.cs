using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using WindowOpacity.Core;
using WindowOpacity.Services;
using WindowOpacity.UI;
namespace WindowOpacity;
public partial class App : Application
{
    readonly LoggingService log = new(); WindowOpacityService? opacity; WindowTracker? tracker; TrayIconService? tray; HotkeyService? hotkey; SettingsService? settings; SettingsWindow? settingsWindow; Mutex? mutex; EventWaitHandle? exitSignal; RegisteredWaitHandle? exitWait; bool cleaned, ownsMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Array.Exists(e.Args, x => x == "--exit"))
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(@"Local\WindowOpacity.Exit");
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }
        mutex = new Mutex(true, @"Local\WindowOpacity.Singleton", out ownsMutex);
        if (!ownsMutex)
        {
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) => { log.Write("UnhandledException", reason: args.Exception.ToString()); Cleanup(); args.Handled = true; Shutdown(1); };
        try
        {
            exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\WindowOpacity.Exit");
            exitWait = ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) => Dispatcher.BeginInvoke(new Action(() => Shutdown())), null, Timeout.Infinite, true);
            UpdateTheme();
            SystemEvents.UserPreferenceChanged += OnTheme;
            settings = new(log);
            if (settings.Value.RunAtStartup)
            {
                try { StartupService.SetEnabled(true); }
                catch (Exception startupError) { log.Write("StartupRegistrationFailure", reason: startupError.Message); }
            }
            opacity = new(log);
            opacity.RecoverPreviousInstanceWindows();
            tracker = new(Dispatcher, settings, opacity, log);
            hotkey = new(tracker.EmergencyReset, log);
            tray = new(tracker, settings, ShowSettings, () => Shutdown());
            if (!settings.Value.StartMinimized)
                ShowSettings();
        }
        catch (Exception ex) { log.Write("StartupFailure", reason: ex.ToString()); MessageBox.Show(ex.Message, "WindowOpacity", MessageBoxButton.OK, MessageBoxImage.Error); Cleanup(); Shutdown(1); }
    }
    void ShowSettings()
    {
        if (settings == null || tracker == null)
            return;
        if (settingsWindow == null)
        {
            settingsWindow = new(settings, tracker);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }
        settingsWindow.Show();
        settingsWindow.Activate();
    }
    void OnTheme(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateTheme));
    void UpdateTheme()
    {
        bool light = true;
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            light = key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
        Resources["Surface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#FFF0F3F8" : "#FF202735"));
        Resources["Text"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#FF202735" : "#FFF2F5FB"));
        Resources["IconContrastColor"] = light ? Colors.White : Color.FromRgb(16, 20, 28);
        Resources["Muted"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#FF717B8C" : "#FFA8B2C5"));
        Resources["Hover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#403E8FF5" : "#60438FF5"));
        Resources["ControlSurface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#80FFFFFF" : "#25FFFFFF"));
        var glass = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(light ? "#F5F8FAFF" : "#F53D485D"),
            (Color)ColorConverter.ConvertFromString(light ? "#DCDFE7F4" : "#E0263043"), 45);
        glass.Freeze();
        Resources["GlassSurface"] = glass;
    }
    void Cleanup()
    {
        if (cleaned)
            return;
        cleaned = true;
        Clean(() => { if (opacity?.RestoreAllWindows() == false) log.Write("RestoreIncomplete", reason: "Some target windows could not be restored"); });
        Clean(() => tracker?.Dispose());
        Clean(() => hotkey?.Dispose());
        Clean(() => tray?.Dispose());
        SystemEvents.UserPreferenceChanged -= OnTheme;
        exitWait?.Unregister(null);
        exitSignal?.Dispose();
        if (ownsMutex)
            mutex?.ReleaseMutex();
        mutex?.Dispose();
    }
    void Clean(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) { log.Write("CleanupFailure", reason: e.ToString()); }
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Cleanup();
        base.OnSessionEnding(e);
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }
}
