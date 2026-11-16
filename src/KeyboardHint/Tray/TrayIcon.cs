using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using KeyboardHint.Core;
using KeyboardHint.Profiles;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;

namespace KeyboardHint.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ModifierHoldTrigger _trigger;
    private readonly ProfileStore _store;
    private readonly AppSettings _settings;
    private readonly KeyboardHint.Overlay.OverlayWindow _overlay;

    public TrayIcon(ModifierHoldTrigger trigger, ProfileStore store, AppSettings settings,
                    KeyboardHint.Overlay.OverlayWindow overlay)
    {
        _trigger = trigger;
        _store = store;
        _settings = settings;
        _overlay = overlay;

        _icon = new NotifyIcon
        {
            Text = "键盘小助手 — 按住 Ctrl/Alt/Win 显示快捷键",
            Icon = MakeIcon(),
            Visible = true
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button is MouseButtons.Right or MouseButtons.Left) ShowTrayMenu();
        };

        // First-run onboarding: without a hint nobody knows the gesture exists.
        var marker = Path.Combine(Log.UserDir, ".welcomed");
        if (!File.Exists(marker))
        {
            try { File.WriteAllText(marker, ""); } catch { }
            _icon.ShowBalloonTip(8000, "键盘小助手已启动",
                "在任意应用中按住 Ctrl、Alt 或 Win 键约半秒，\n屏幕底部会弹出当前应用的快捷键键盘。\n右键托盘图标可设置触发键和延迟。",
                ToolTipIcon.Info);
        }
    }

    /// <summary>Win11-style WPF context menu (Fluent styles from Themes/Fluent.xaml).
    /// Rebuilt on every open so check states and the foreground-app header are live.</summary>
    internal void ShowTrayMenu(System.Windows.Point? at = null)
    {
        var menu = new WpfContextMenu();

        // ---- status header ----
        var status = new WpfMenuItem { IsEnabled = false, FontWeight = System.Windows.FontWeights.SemiBold };
        try
        {
            var ctx = ForegroundApp.Capture();
            var p = _store.Resolve(ctx.ProcessName);
            status.Header = string.IsNullOrEmpty(ctx.ProcessName)
                ? "当前应用：—"
                : $"当前应用：{ctx.ProcessName} → {p.DisplayName}";
        }
        catch { status.Header = "当前应用：—"; }
        menu.Items.Add(status);
        menu.Items.Add(new WpfSeparator());

        // ---- toggles ----
        menu.Items.Add(Toggle("启用快捷键提示", _trigger.Enabled, on => _trigger.Enabled = on));

        var trig = new WpfMenuItem { Header = "触发键" };
        foreach (var (id, label) in new[] { ("ctrl", "Ctrl"), ("alt", "Alt"), ("win", "Win"), ("shift", "Shift") })
        {
            trig.Items.Add(Toggle(label, _settings.Trigger(id).Enabled, on =>
            {
                var c = _settings.Trigger(id);
                _settings.Triggers[id] = new TriggerCfg { Enabled = on, DelayMs = c.DelayMs, SuppressRelease = c.SuppressRelease };
                _settings.Save();
            }));
        }
        menu.Items.Add(trig);

        menu.Items.Add(Toggle("钉住模式（松开后保持显示）", _settings.StayOnRelease, on =>
        {
            _settings.StayOnRelease = on;
            _settings.Save();
        }));
        menu.Items.Add(new WpfSeparator());

        // ---- actions ----
        menu.Items.Add(Item("快捷键编辑器…", () =>
        {
            string? proc = null;
            try { proc = ForegroundApp.Capture().ProcessName; } catch { }
            new KeyboardHint.Overlay.ShortcutEditorWindow(_store,
                () => _store.ReloadFrom(ProfileStore.Load()), proc).ShowDialog();
        }));
        menu.Items.Add(Item("设置…", () =>
            new KeyboardHint.Overlay.SettingsWindow(_settings, _store, _trigger, _overlay).ShowDialog()));
        menu.Items.Add(new WpfSeparator());
        menu.Items.Add(Item("自定义当前应用快捷键（JSON）…", () => CustomizeCurrentApp(_store)));
        menu.Items.Add(Item("打开配置/快捷键目录", OpenConfigDir));
        menu.Items.Add(Item("重新加载快捷键配置", () =>
        {
            _store.ReloadFrom(ProfileStore.Load());
            System.Windows.MessageBox.Show("快捷键配置已重新加载。", "键盘小助手");
        }));
        menu.Items.Add(new WpfSeparator());
        menu.Items.Add(Item("关于", () => System.Windows.MessageBox.Show(
            "键盘小助手 v1.2\n\n按住 Ctrl / Alt / Win 显示当前应用的快捷键键盘。\n数据目录：%LOCALAPPDATA%\\KeyboardHint\n日志同目录可查。",
            "关于 键盘小助手", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information)));
        menu.Items.Add(Item("退出", () => System.Windows.Application.Current.Shutdown()));

        if (at.HasValue)
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            menu.PlacementRectangle = new System.Windows.Rect(at.Value, System.Windows.Size.Empty);
        }
        else
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        }
        menu.IsOpen = true;
    }

    private static WpfMenuItem Item(string header, Action action)
    {
        var mi = new WpfMenuItem { Header = header };
        mi.Click += (_, _) =>
        {
            try { action(); } catch (Exception ex) { Log.Error("tray menu action", ex); }
        };
        return mi;
    }

    private static WpfMenuItem Toggle(string header, bool isChecked, Action<bool> set)
    {
        var mi = new WpfMenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = isChecked,
            StaysOpenOnClick = true
        };
        mi.Click += (_, _) =>
        {
            try { set(mi.IsChecked); } catch (Exception ex) { Log.Error("tray toggle", ex); }
        };
        return mi;
    }

    /// <summary>Export the current foreground app's profile to the user dir
    /// (or create a fresh one pre-filled with its process name), then open it
    /// in notepad for editing. User-dir files override bundled ones.</summary>
    private static void CustomizeCurrentApp(ProfileStore store)
    {
        try
        {
            var ctx = ForegroundApp.Capture();
            var profile = store.Resolve(ctx.ProcessName);
            var dir = Path.Combine(Log.UserDir, "shortcuts");
            Directory.CreateDirectory(dir);

            ShortcutProfile toWrite = profile;
            var id = profile.Id;
            if (id == "default" && !string.IsNullOrEmpty(ctx.ProcessName))
            {
                id = ctx.ProcessName.ToLowerInvariant();
                toWrite = new ShortcutProfile
                {
                    Id = id,
                    DisplayName = ctx.ProcessName,
                    Processes = new List<string> { id },
                    Layers = new Dictionary<string, Dictionary<string, string>>(profile.Layers)
                };
            }
            var path = Path.Combine(dir, id + ".json");
            if (!File.Exists(path))
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(toWrite,
                    new System.Text.Json.JsonSerializerOptions(AppSettings.JsonOpts()) { WriteIndented = true }));
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            System.Windows.MessageBox.Show($"已为「{profile.DisplayName}」生成可编辑配置：\n{path}\n\n改完后托盘菜单 → 重新加载快捷键配置 即生效。",
                "键盘小助手", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex) { Log.Error("customize profile", ex); }
    }

    private static void OpenConfigDir()
    {
        try
        {
            Directory.CreateDirectory(Log.UserDir);
            Directory.CreateDirectory(Path.Combine(Log.UserDir, "shortcuts"));
            Process.Start(new ProcessStartInfo("explorer.exe", Log.UserDir) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("open config dir", ex); }
    }

    /// <summary>Draw a simple tray icon at runtime: dark rounded square with 键.</summary>
    private static Icon MakeIcon()
    {
        try
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var bg = new SolidBrush(Color.FromArgb(0x1B, 0x1B, 0x20));
                using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 7);
                g.FillPath(bg, path);
                using var fg = new SolidBrush(Color.FromArgb(0x4E, 0xC9, 0xB0));
                using var font = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("键", font, fg, new RectangleF(0, 1, 32, 32), fmt);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
