namespace KeyboardHint.Profiles;

/// <summary>
/// One JSON file per app. Keys in <see cref="Layers"/> are modifier layers
/// ("ctrl" today; "ctrl+shift" etc. reserved) mapping key ids to descriptions.
/// </summary>
public class ShortcutProfile
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> Processes { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> Layers { get; set; } = new();

    public IReadOnlyDictionary<string, string> Layer(string name)
        => Layers.TryGetValue(name, out var d) ? d : new Dictionary<string, string>();
}
