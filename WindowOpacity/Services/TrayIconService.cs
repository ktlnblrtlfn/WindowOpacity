using System;
using System.Drawing;
using System.Windows.Forms;
using WindowOpacity.Core;
namespace WindowOpacity.Services;
public sealed class TrayIconService : IDisposable
{
    readonly NotifyIcon icon; readonly Icon artwork; readonly ContextMenuStrip menu;
    public TrayIconService(WindowTracker tracker, SettingsService settings, Action showSettings, Action exit)
    {
        artwork = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? (Icon)SystemIcons.Application.Clone();
        menu = new();
        var enabled = new ToolStripMenuItem("Enable WindowOpacity") { Checked = true, CheckOnClick = true };
        enabled.CheckedChanged += (_, _) => tracker.Enabled = enabled.Checked;
        menu.Items.Add(enabled);
        menu.Items.Add("Reset All Windows", null, (_, _) => tracker.ResetAll());
        menu.Items.Add("Settings", null, (_, _) => showSettings());
        var startup = new ToolStripMenuItem("Run at Startup") { Checked = StartupService.IsEnabled(), CheckOnClick = true };
        startup.CheckedChanged += (_, _) => { try { StartupService.SetEnabled(startup.Checked); settings.Value.RunAtStartup = startup.Checked; settings.Save(); } catch (Exception e) { MessageBox.Show(e.Message, "WindowOpacity"); } };
        menu.Items.Add(startup);
        menu.Opening += (_, _) => startup.Checked = StartupService.IsEnabled();
        menu.Items.Add("About", null, (_, _) => MessageBox.Show("WindowOpacity 1.0\nExternal Win32 window transparency.\nEmergency reset: Ctrl + Alt + Shift + O", "About WindowOpacity"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());
        icon = new()
        {
            Icon = artwork,
            Text = "WindowOpacity",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => showSettings();
    }
    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
        artwork.Dispose();
    }
}
