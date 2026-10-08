using System;
using System.Collections.Generic;
using System.Linq;
using WindowOpacity.Models;

namespace WindowOpacity.Core;

public static class ApplicationIdentity
{
    // MSIX: Name_Version_Architecture_ResourceId_PublisherId. The family
    // (Name_PublisherId) survives updates; the executable distinguishes hosts.
    public static string FromPath(string path) => Parse(path, out var key, out _) ? key : path;

    static bool Parse(string path, out string key, out Version version)
    {
        key = path;
        version = new Version(0, 0);
        const string marker = "\\WindowsApps\\";
        int start = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return false;
        start += marker.Length;
        int end = path.IndexOf('\\', start);
        if (end < 0 || end == path.Length - 1) return false;
        var parts = path[start..end].Split('_');
        if (parts.Length != 5 || parts[0].Length == 0 || parts[4].Length == 0 ||
            !Version.TryParse(parts[1], out var parsed) || parsed.Revision < 0)
            return false;
        version = parsed;
        key = "package:" + parts[0] + "_" + parts[4] + "/" + path[(end + 1)..].Replace('\\', '/');
        return true;
    }

    public static bool Normalize(ApplicationSettings settings)
    {
        var rules = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Latest legacy version wins a collision. A previously migrated/user-edited
        // stable rule takes precedence over every legacy rule.
        foreach (var item in settings.OpacityRules.OrderBy(x => Parse(x.Key, out _, out var version) ? version : new Version(0, 0)))
            if (!item.Key.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                rules[FromPath(item.Key)] = item.Value;
        foreach (var item in settings.OpacityRules)
            if (item.Key.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                rules[item.Key] = item.Value;
        var exclusions = settings.ExcludedApplications.Select(FromPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool changed = rules.Count != settings.OpacityRules.Count ||
            rules.Any(x => !settings.OpacityRules.TryGetValue(x.Key, out var old) || old != x.Value) ||
            !exclusions.SequenceEqual(settings.ExcludedApplications, StringComparer.OrdinalIgnoreCase);
        settings.OpacityRules = rules;
        settings.ExcludedApplications = exclusions;
        return changed;
    }
}
