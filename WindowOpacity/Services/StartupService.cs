using System;
using Microsoft.Win32;
namespace WindowOpacity.Services;
public static class StartupService
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run"; public static bool IsEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        return k?.GetValue("WindowOpacity") != null;
    }
    public static void SetEnabled(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled)
            k.SetValue("WindowOpacity", "\"" + Environment.ProcessPath + "\"");
        else
            k.DeleteValue("WindowOpacity", false);
    }
}
