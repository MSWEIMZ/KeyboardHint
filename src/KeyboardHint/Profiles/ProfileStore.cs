using System.IO;
using System.Text.Json;
using KeyboardHint.Core;

namespace KeyboardHint.Profiles;

public class ProfileStore
{
    private readonly List<ShortcutProfile> _profiles = new();
    private ShortcutProfile _fallback;

    private ProfileStore(List<ShortcutProfile> profiles, ShortcutProfile fallback)
    {
        _profiles = profiles;
        _fallback = fallback;
    }

    public IReadOnlyList<ShortcutProfile> All => _profiles;

    /// <summary>The "default" profile — also the fallback source for layers a
    /// specific app profile doesn't define (e.g. OS-level win/alt layers).</summary>
    public ShortcutProfile Default => _fallback;

    public void ReloadFrom(ProfileStore fresh)
    {
        _profiles.Clear();
        _profiles.AddRange(fresh._profiles);
        _fallback = fresh._fallback;
    }

    public ShortcutProfile Resolve(string processName)
    {
        if (!string.IsNullOrEmpty(processName))
        {
            foreach (var p in _profiles)
                if (p.Processes.Any(x => string.Equals(x, processName, StringComparison.OrdinalIgnoreCase)))
                    return p;
        }
        return _fallback;
    }

    /// <summary>User dir wins over bundled dir on id collision.</summary>
    public static ProfileStore Load()
    {
        var map = new Dictionary<string, ShortcutProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in new[]
        {
            Path.Combine(AppSettings.BundledDir, "shortcuts"),
            Path.Combine(Log.UserDir, "shortcuts")
        })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var p = JsonSerializer.Deserialize<ShortcutProfile>(
                        File.ReadAllText(file), AppSettings.JsonOpts());
                    if (p is { Id.Length: > 0 })
                        map[p.Id] = p;
                }
                catch (Exception ex) { Log.Error($"load profile {file}", ex); }
            }
        }

        var profiles = map.Values.ToList();
        var fallback = profiles.FirstOrDefault(p => p.Id == "default")
            ?? new ShortcutProfile { Id = "default", DisplayName = "通用" };
        Log.Info($"profiles loaded: {profiles.Count} ({string.Join(", ", profiles.Select(p => p.Id))})");
        return new ProfileStore(profiles, fallback);
    }
}
