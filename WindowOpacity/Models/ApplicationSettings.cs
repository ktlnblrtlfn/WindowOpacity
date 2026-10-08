using System.Collections.Generic;
namespace WindowOpacity.Models;
public sealed class ApplicationSettings
{
    public bool ShowTransparencyButton { get; set; } = true; public int MinimumOpacity { get; set; } = 20; public bool RememberOpacityPerApplication
    {
        get; set;
    }
    public bool EnableMouseWheel
    {
        get; set;
    }
    public bool RunAtStartup
    {
        get; set;
    }
    public bool StartMinimized { get; set; } = true; public bool ShowOnMaximized { get; set; } = true; public bool DisableInFullscreen { get; set; } = true; public List<string> ExcludedApplications { get; set; } = new(); public Dictionary<string, int> OpacityRules { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
}
