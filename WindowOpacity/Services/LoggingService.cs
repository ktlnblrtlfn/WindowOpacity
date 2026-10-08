using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace WindowOpacity.Services;
public sealed class LoggingService
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowOpacity");
    readonly Dictionary<string, DateTime> recent = new(); readonly object gate = new();
    public void Write(string operation, IntPtr hwnd = default, string? reason = null, int error = 0)
    {
        lock (gate)
        {
            var key = $"{operation}:{hwnd}:{reason}";
            if (recent.TryGetValue(key, out var time) && DateTime.UtcNow - time < TimeSpan.FromSeconds(30))
                return;
            recent[key] = DateTime.UtcNow;
            if (recent.Count > 512)
                recent.Clear();
            try
            {
                var dir = Path.Combine(Root, "Logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl"), JsonSerializer.Serialize(new
                {
                    timestamp = DateTime.UtcNow,
                    operation,
                    hwnd = hwnd.ToString("X"),
                    reason,
                    error
                }) + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
