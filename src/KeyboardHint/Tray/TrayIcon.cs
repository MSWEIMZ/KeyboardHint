using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using KeyboardHint.Core;
using KeyboardHint.Profiles;

namespace KeyboardHint.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _enabledItem;

    public TrayIcon(ModifierHoldTrigger trigger, ProfileStore store, AppSettings settings)
    {
        _enabledItem = new ToolStripMenuItem("已启用（按住修饰键显示快捷键）")
        {
            Checked = trigger.Enabled,
            CheckOnClick = true
        };
        _enabledItem.CheckedChanged += (_, _) => trigger.Enabled = _enabledItem.Checked;

        var triggersMenu = new ToolStripMenuItem("触发键");
        var modNames = new (string Id, string Label)[]
        {
            ("ctrl", "Ctrl"), ("alt", "Alt"), ("win", "Win"), ("shift", "Shift")
        };
        foreach (var (id, label) in modNames)
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = settings.Trigger(id).Enabled,
                CheckOnClick = true
            };
            item.CheckedChanged += (_, _) =>
            {
                settings.Triggers[id] = new TriggerCfg
                {
                    Enabled = item.Checked,
                    DelayMs = settings.Trigger(id).DelayMs,
                    SuppressRelease = settings.Trigger(id).SuppressRelease
                };
                settings.Save();
            };
            triggersMenu.DropDownItems.Add(item);
        }

        var pinItem = new ToolStripMenuItem("钉住模式（松开后保持显示，再按一次关闭）")
        {
            Checked = settings.StayOnRelease,
            CheckOnClick = true
        };
        pinItem.CheckedChanged += (_, _) =>
        {
            settings.StayOnRelease = pinItem.Checked;
            settings.Save();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(triggersMenu);
        menu.Items.Add(pinItem);
        menu.Items.Add(new ToolStripMenuItem("设置…", null, (_, _) =>
        {
            var w = new KeyboardHint.Overlay.SettingsWindow(settings, store);
            w.ShowDialog();
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("打开配置/快捷键目录", null, (_, _) => OpenConfigDir()));
        menu.Items.Add(new ToolStripMenuItem("重新加载快捷键配置", null, (_, _) =>
        {
            var fresh = ProfileStore.Load();
            store.ReloadFrom(fresh);
            MessageBox.Show("快捷键配置已重新加载。", "键盘小助手");
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => System.Windows.Application.Current.Shutdown()));

        _icon = new NotifyIcon
        {
            Text = "键盘小助手 — 按住 Ctrl/Alt/Win 显示快捷键",
            Icon = MakeIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
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
