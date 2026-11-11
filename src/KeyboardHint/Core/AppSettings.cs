using System.IO;
using System.Text.Json;

namespace KeyboardHint.Core;

public class AppSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Hold delay in ms before the overlay pops up.</summary>
    public int DelayMs { get; set; } = 400;

    /// <summary>Overlay opacity 0..1.</summary>
    public double Opacity { get; set; } = 0.92;

    /// <summary>Process names (no .exe, lowercase) where the overlay never shows.</summary>
    public List<string> ExcludedProcesses { get; set; } = new();

    /// <summary>Where to find shortcut profiles: bundled dir, then user dir overrides.</summary>
    public static string BundledDir => AppContext.BaseDirectory;

    public static AppSettings Load()
    {
        foreach (var path in new[] { UserSettingsPath(), Path.Combine(BundledDir, "settings.json") })
        {
            try
            {
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts()) ?? new AppSettings();
            }
            catch (Exception ex) { Log.Error($"load settings {path}", ex); }
        }
        return new AppSettings();
    }

    public static string UserSettingsPath() => Path.Combine(Log.UserDir, "settings.json");

    public static JsonSerializerOptions JsonOpts() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
