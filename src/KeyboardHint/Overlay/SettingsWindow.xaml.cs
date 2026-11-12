using System.Diagnostics;
using System.IO;
using System.Windows;
using KeyboardHint.Core;
using KeyboardHint.Profiles;

namespace KeyboardHint.Overlay;

/// <summary>Compact settings panel opened from the tray — writes to the
/// user settings.json so it survives updates to the bundled defaults.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ProfileStore _store;

    public SettingsWindow(AppSettings settings, ProfileStore store)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;

        ChkCtrl.IsChecked = settings.Trigger("ctrl").Enabled;
        ChkAlt.IsChecked = settings.Trigger("alt").Enabled;
        ChkWin.IsChecked = settings.Trigger("win").Enabled;
        ChkShift.IsChecked = settings.Trigger("shift").Enabled;
        ChkPin.IsChecked = settings.StayOnRelease;
        DelaySlider.Value = settings.DelayMs;
        OpacitySlider.Value = settings.Opacity * 100;
        ExcludedBox.Text = string.Join(", ", settings.ExcludedProcesses);

        DelaySlider.ValueChanged += (_, _) => DelayText.Text = $"{DelaySlider.Value:F0} ms";
        OpacitySlider.ValueChanged += (_, _) => OpacityText.Text = $"{OpacitySlider.Value:F0}%";
        DelayText.Text = $"{settings.DelayMs} ms";
        OpacityText.Text = $"{settings.Opacity * 100:F0}%";
    }

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
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void SetEnabled(string id, bool enabled)
    {
        var c = _settings.Trigger(id);
        _settings.Triggers[id] = new TriggerCfg { Enabled = enabled, DelayMs = c.DelayMs, SuppressRelease = c.SuppressRelease };
    }
}
