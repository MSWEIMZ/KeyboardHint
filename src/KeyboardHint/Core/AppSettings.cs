using System.IO;
using System.Text.Json;

namespace KeyboardHint.Core;

/// <summary>Per-modifier trigger config: which keys can be held to show the
/// overlay, at what delay, and whether the release keypress is swallowed
/// (Win needs it so Start menu doesn't open).</summary>
public class TriggerCfg
{
    public bool Enabled { get; set; } = true;
    /// <summary>null = use global delayMs.</summary>
    public int? DelayMs { get; set; }
    /// <summary>Inject an unmapped F24 keypress while the trigger is held so
    /// its release counts as a chord — Win: no Start menu; Alt: no menu-bar
    /// focus grab.</summary>
    public bool SuppressRelease { get; set; }
}

public class AppSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Hold delay in ms before the overlay pops up.</summary>
    public int DelayMs { get; set; } = 400;

    /// <summary>Overlay opacity 0..1.</summary>
    public double Opacity { get; set; } = 0.92;

    /// <summary>Pin mode: overlay stays visible after the modifier is released;
    /// tap the trigger key again or click anywhere to dismiss.</summary>
    public bool StayOnRelease { get; set; }

    /// <summary>"software" (default) or "hardware". On machines where the GPU
    /// pipeline produces no output (driver/broken D3D state), hardware mode
    /// leaves every WPF window blank — software rendering is the safe default
    /// for this small overlay. Takes effect on next start.</summary>
    public string RenderMode { get; set; } = "software";

    /// <summary>Modifier triggers: ctrl / alt / win / shift.</summary>
    public Dictionary<string, TriggerCfg> Triggers { get; set; } = new()
    {
        ["ctrl"] = new(),
        ["alt"] = new() { SuppressRelease = true },
        ["win"] = new() { DelayMs = 600, SuppressRelease = true },
        ["shift"] = new() { Enabled = false },
    };

    /// <summary>Process names (no .exe, lowercase) where the overlay never shows.</summary>
    public List<string> ExcludedProcesses { get; set; } = new();

    public TriggerCfg Trigger(string name)
        => Triggers.TryGetValue(name, out var c) ? c : new TriggerCfg { Enabled = false };

    public int DelayFor(string name) => Math.Max(150, Trigger(name).DelayMs ?? DelayMs);

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

    /// <summary>Persist to the user settings file (tray toggles / settings UI).</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.UserDir);
            File.WriteAllText(UserSettingsPath(),
                JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonOpts()) { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Error("save settings", ex); }
    }

    public static string UserSettingsPath() => Path.Combine(Log.UserDir, "settings.json");

    public static JsonSerializerOptions JsonOpts() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
