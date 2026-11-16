using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KeyboardHint.Core;
using KeyboardHint.Profiles;

namespace KeyboardHint.Overlay;

/// <summary>Compact settings panel opened from the tray — writes to the
/// user settings.json so it survives updates to the bundled defaults.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ProfileStore _store;
    private readonly ModifierHoldTrigger _trigger;
    private readonly OverlayWindow _overlay;

    private static readonly (string Name, string Hex)[] Accents =
    {
        ("蓝色", "#60CDFF"), ("紫色", "#B16CE5"), ("绿色", "#6CCB5F"),
        ("橙色", "#F7630C"), ("粉色", "#E3008C"), ("青色", "#00B7C3"),
        ("红色", "#E81123"), ("金色", "#FFB900"),
    };

    public SettingsWindow(AppSettings settings, ProfileStore store, ModifierHoldTrigger trigger, OverlayWindow overlay)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;
        _trigger = trigger;
        _overlay = overlay;

        ChkEnabled.IsChecked = _trigger.Enabled;
        ChkAutostart.IsChecked = StartupManager.IsEnabled();
        ChkCtrl.IsChecked = settings.Trigger("ctrl").Enabled;
        ChkAlt.IsChecked = settings.Trigger("alt").Enabled;
        ChkWin.IsChecked = settings.Trigger("win").Enabled;
        ChkShift.IsChecked = settings.Trigger("shift").Enabled;
        ChkPin.IsChecked = settings.StayOnRelease;
        DelaySlider.Value = settings.DelayMs;
        OpacitySlider.Value = settings.Opacity * 100;
        ExcludedBox.Text = string.Join(", ", settings.ExcludedProcesses);

        foreach (ComboBoxItem item in ThemeBox.Items)
            if (string.Equals(item.Tag as string, settings.Theme, StringComparison.OrdinalIgnoreCase))
                ThemeBox.SelectedItem = item;
        if (ThemeBox.SelectedItem == null) ThemeBox.SelectedIndex = 0;
        foreach (var (name, hex) in Accents)
        {
            var item = new ComboBoxItem { Content = name, Tag = hex };
            AccentBox.Items.Add(item);
            if (string.Equals(hex, settings.Accent, StringComparison.OrdinalIgnoreCase))
                AccentBox.SelectedItem = item;
        }
        if (AccentBox.SelectedItem == null) AccentBox.SelectedIndex = 0;

        DelaySlider.ValueChanged += (_, _) => DelayText.Text = $"{DelaySlider.Value:F0} ms";
        OpacitySlider.ValueChanged += (_, _) => OpacityText.Text = $"{OpacitySlider.Value:F0}%";
        DelayText.Text = $"{settings.DelayMs} ms";
        OpacityText.Text = $"{settings.Opacity * 100:F0}%";
    }

    private void OnClose(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void OnOpenDir(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Log.UserDir, "shortcuts"));
            Process.Start(new ProcessStartInfo("explorer.exe", Log.UserDir) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("settings open dir", ex); }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.Enabled = _trigger.Enabled = ChkEnabled.IsChecked == true;
        StartupManager.Set(ChkAutostart.IsChecked == true);
        SetEnabled("ctrl", ChkCtrl.IsChecked == true);
        SetEnabled("alt", ChkAlt.IsChecked == true);
        SetEnabled("win", ChkWin.IsChecked == true);
        SetEnabled("shift", ChkShift.IsChecked == true);
        _settings.StayOnRelease = ChkPin.IsChecked == true;
        _settings.DelayMs = (int)DelaySlider.Value;
        _settings.Opacity = OpacitySlider.Value / 100.0;
        _settings.ExcludedProcesses = ExcludedBox.Text
            .Split(',', ';', '，', '；')
            .Select(s => s.Trim().TrimEnd('.')).Where(s => s.EndsWith(".exe") == false && s.Length > 0)
            .Select(s => s.ToLowerInvariant()).ToList();
        if ((ThemeBox.SelectedItem as ComboBoxItem)?.Tag is string theme) _settings.Theme = theme;
        if ((AccentBox.SelectedItem as ComboBoxItem)?.Tag is string accent) _settings.Accent = accent;
        _settings.Save();
        _overlay.ApplyTheme();
        DialogResult = true;
        Close();
    }

    private void SetEnabled(string id, bool enabled)
    {
        var c = _settings.Trigger(id);
        _settings.Triggers[id] = new TriggerCfg { Enabled = enabled, DelayMs = c.DelayMs, SuppressRelease = c.SuppressRelease };
    }
}
