using System.Windows;
using KeyboardHint.Profiles;
using MessageBox = System.Windows.MessageBox;

namespace KeyboardHint.Overlay;

/// <summary>Tiny dialog: create a profile for an app that has none yet —
/// display name + process names (the pieces Resolve() needs to match it).</summary>
public partial class NewAppDialog : Window
{
    public ShortcutProfile? ResultProfile { get; private set; }

    /// <param name="prefillProcess">Foreground process captured before the editor took focus.</param>
    public NewAppDialog(string? prefillProcess = null)
    {
        InitializeComponent();
        if (!string.IsNullOrEmpty(prefillProcess))
        {
            ProcBox.Text = prefillProcess;
            NameBox.Text = prefillProcess;
        }
        NameBox.Focus();
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var procs = ProcBox.Text.Split(',', ';', '，', '；')
            .Select(s => s.Trim().TrimEnd('.'))
            .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s)
            .Where(s => s.Length > 0).Select(s => s.ToLowerInvariant()).Distinct().ToList();
        if (procs.Count == 0)
        {
            MessageBox.Show(this, "请至少填一个进程名（如 wps）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ResultProfile = new ShortcutProfile
        {
            Id = procs[0],
            DisplayName = name.Length > 0 ? name : procs[0],
            Processes = procs,
            Layers = new Dictionary<string, Dictionary<string, string>>()
        };
        DialogResult = true;
        Close();
    }
}
