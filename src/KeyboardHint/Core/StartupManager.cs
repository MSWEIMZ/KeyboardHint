using Microsoft.Win32;

namespace KeyboardHint.Core;

/// <summary>HKCU Run-key autostart — no admin needed, standard for utilities.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "KeyboardHint";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) is string s && s.Contains("KeyboardHint");
        }
        catch { return false; }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (enabled)
                key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(AppName, false);
        }
        catch (Exception ex) { Log.Error("autostart set", ex); }
    }
}
