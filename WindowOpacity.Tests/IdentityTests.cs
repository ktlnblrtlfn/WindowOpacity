using System;
using System.IO;
using System.Text.Json;
using WindowOpacity.Core;
using WindowOpacity.Models;
using WindowOpacity.Services;
using ApplicationIdentity = WindowOpacity.Core.ApplicationIdentity;

namespace WindowOpacity.Tests;
internal static class IdentityTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool success, string name)
        {
            if (!success) throw new InvalidOperationException(name);
            count++;
        }
        const string old = @"C:\Program Files\WindowsApps\OpenAI.Codex_26.930.7945.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe";
        const string updated = @"C:\Program Files\WindowsApps\OpenAI.Codex_26.1002.7124.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe";
        string identity = ApplicationIdentity.FromPath(old);
        Check(identity == ApplicationIdentity.FromPath(updated), "Update preserves identity");
        Check(identity == ApplicationIdentity.FromPath(updated.Replace("C:", "D:").Replace("_x64_", "_arm64_")), "Drive/architecture update preserves identity");
        Check(identity != ApplicationIdentity.FromPath(updated.Replace("2p2nqsd0c76g0", "otherpublisher")), "Different publishers remain separate");
        const string standard = @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2636.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe";
        Check(ApplicationIdentity.FromPath(standard) != ApplicationIdentity.FromPath(standard.Replace("5319275A.WhatsAppDesktop", "5319275A.51895FA4EA97F")), "WhatsApp channels remain separate");
        const string classic = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe";
        Check(ApplicationIdentity.FromPath(classic) == classic, "Classic paths preserved");
        Check(ApplicationIdentity.FromPath(old.Replace("26.930.7945.0", "invalid")) == old.Replace("26.930.7945.0", "invalid"), "Malformed package stays distinct");
        var root = Path.Combine(Path.GetTempPath(), "WindowOpacityIdentity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var value = new ApplicationSettings { MinimumOpacity = 40, RunAtStartup = true };
            value.OpacityRules[updated] = 65;
            value.OpacityRules[old] = 70;
            value.OpacityRules[standard] = 55;
            value.OpacityRules[classic] = 80;
            value.ExcludedApplications.Add(old);
            File.WriteAllText(Path.Combine(root, "settings.json"), JsonSerializer.Serialize(value));
            var settings = new SettingsService(new LoggingService(), root);
            Check(settings.Value.OpacityRules[identity] == 65, "Latest version wins legacy collision regardless of order");
            Check(settings.Value.OpacityRules.Count == 3, "No rule loss in migration");
            Check(settings.Value.ExcludedApplications[0] == identity, "Exclusion survives update");
            Check(settings.Value.MinimumOpacity == 40 && settings.Value.RunAtStartup, "Global settings preserved");
            var reloaded = new SettingsService(new LoggingService(), root);
            Check(reloaded.Value.OpacityRules[identity] == 65, "Migration persisted to disk");
            reloaded.Value.OpacityRules[identity] = 75;
            reloaded.Value.OpacityRules[updated] = 45;
            Check(reloaded.Save(false), "Save succeeds");
            Check(new SettingsService(new LoggingService(), root).Value.OpacityRules[identity] == 75, "Stable user choice wins legacy collision");
            Check(!ApplicationIdentity.Normalize(reloaded.Value), "Migration is idempotent");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"PASS: {count} identity/persistence assertions; no live windows or startup registry modified.");
        return 0;
    }
}
