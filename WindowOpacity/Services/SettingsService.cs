using System;
using System.IO;
using System.Text.Json;
using WindowOpacity.Models;
namespace WindowOpacity.Services;
public sealed class SettingsService
{
    readonly LoggingService log; public ApplicationSettings Value { get; private set; } = new(); public event Action? Changed;
    readonly string path;
    public SettingsService(LoggingService l, string? directory = null)
    {
        log = l;
        path = Path.Combine(directory ?? LoggingService.Root, "settings.json");
        try
        {
            if (File.Exists(path))
                Value = JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { log.Write("SettingsLoad", reason: e.Message); }
        Value.MinimumOpacity = Math.Clamp(Value.MinimumOpacity, 20, 95);
        Value.ExcludedApplications ??= new();
        Value.OpacityRules = new(Value.OpacityRules ?? new(), StringComparer.OrdinalIgnoreCase);
        if (WindowOpacity.Core.ApplicationIdentity.Normalize(Value))
            Save(false);
    }
    public bool Save(bool notify = true)
    {
        try
        {
            WindowOpacity.Core.ApplicationIdentity.Normalize(Value);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log.Write("SettingsSave", reason: e.Message); return false; }
        if (notify)
            Changed?.Invoke();
        return true;
    }
    public void Reset()
    {
        Value = new();
        Save();
    }
}
