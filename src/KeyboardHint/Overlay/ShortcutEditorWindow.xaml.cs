using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyboardHint.Core;
using KeyboardHint.Profiles;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace KeyboardHint.Overlay;

/// <summary>Visual shortcut editor — pick an app, pick a modifier layer, edit
/// key→description rows in a table. Saves to the user shortcuts dir (which
/// overrides bundled profiles) and hot-reloads. No JSON editing required.</summary>
public partial class ShortcutEditorWindow : Window
{
    private readonly ProfileStore _store;
    private readonly Action _reload;
    private readonly List<ShortcutProfile> _working;
    private readonly HashSet<ShortcutProfile> _dirty = new();
    private readonly List<(ComboBox Key, TextBox Desc)> _rows = new();
    private ShortcutProfile? _current;
    private readonly string? _preselectProcess;
    private string _layer = "ctrl";
    private bool _loading;

    private static readonly (string Id, string Label)[] LayerChoices =
    {
        ("ctrl", "Ctrl"), ("ctrl+shift", "Ctrl + Shift"), ("ctrl+alt", "Ctrl + Alt"),
        ("alt", "Alt"), ("alt+shift", "Alt + Shift"), ("shift", "Shift"),
        ("win", "Win"), ("win+ctrl", "Win + Ctrl"), ("win+shift", "Win + Shift"),
        ("ctrl+alt+shift", "Ctrl + Alt + Shift"),
    };

    private static readonly (string Id, string Label)[] KeyChoices = BuildKeys();

    private static (string Id, string Label)[] BuildKeys()
    {
        var list = new List<(string, string)>();
        for (char c = 'A'; c <= 'Z'; c++) list.Add((c.ToString(), c.ToString()));
        for (int i = 0; i <= 9; i++) list.Add(("D" + i, i.ToString()));
        for (int i = 1; i <= 12; i++) list.Add(("F" + i, "F" + i));
        foreach (var (id, label) in new (string, string)[]
        {
            ("Esc", "Esc"), ("Back", "Backspace 退格"), ("Tab", "Tab"), ("Caps", "CapsLock"),
            ("Enter", "Enter 回车"), ("Space", "空格"), ("PgUp", "PageUp"), ("PgDn", "PageDown"),
            ("Home", "Home"), ("End", "End"), ("Ins", "Insert"), ("Del", "Delete"),
            ("Left", "← 左"), ("Up", "↑ 上"), ("Right", "→ 右"), ("Down", "↓ 下"),
            ("Oem3", "` ~"), ("OemMinus", "- _"), ("OemPlus", "= +"),
            ("OemComma", ", <"), ("OemPeriod", ". >"), ("Oem1", "; :"), ("Oem2", "/ ?"),
            ("Oem4", "[ {"), ("Oem5", "\\ |"), ("Oem6", "] }"), ("Oem7", "' \""),
        }) list.Add((id, label));
        return list.ToArray();
    }

    /// <param name="preselectProcess">Process to preselect — pass the foreground
    /// app's process name captured BEFORE this window steals focus.</param>
    public ShortcutEditorWindow(ProfileStore store, Action reload, string? preselectProcess)
    {
        InitializeComponent();
        _store = store;
        _reload = reload;
        _preselectProcess = preselectProcess;

        // Deep-copy profiles so Cancel truly discards edits.
        _working = JsonSerializer.Deserialize<List<ShortcutProfile>>(
            JsonSerializer.Serialize(store.All, AppSettings.JsonOpts()), AppSettings.JsonOpts())
            ?? new List<ShortcutProfile>();

        foreach (var (id, label) in LayerChoices)
            LayerBox.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        LayerBox.SelectedIndex = 0;

        RefillAppBox();
        if (preselectProcess != null)
        {
            var hit = _working.FirstOrDefault(p =>
                p.Processes.Any(x => string.Equals(x, preselectProcess, StringComparison.OrdinalIgnoreCase)));
            if (hit != null)
                AppBox.SelectedItem = AppBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => ReferenceEquals(i.Tag, hit));
        }
        if (AppBox.SelectedIndex < 0) AppBox.SelectedIndex = 0;
    }

    private void RefillAppBox()
    {
        _loading = true;
        var keep = AppBox.SelectedItem as ComboBoxItem;
        AppBox.Items.Clear();
        foreach (var p in _working.OrderBy(p => p.DisplayName))
            AppBox.Items.Add(new ComboBoxItem { Content = p.DisplayName, Tag = p });
        if (keep != null && AppBox.Items.Contains(keep)) AppBox.SelectedItem = keep;
        _loading = false;
    }

    private void OnAppChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        FlushRows();
        _current = (AppBox.SelectedItem as ComboBoxItem)?.Tag as ShortcutProfile;
        ProcText.Text = _current == null
            ? ""
            : _current.Processes.Count == 0
                ? $"id: {_current.Id} · 未绑定进程（作为默认/通用配置）"
                : $"id: {_current.Id} · 匹配进程：{string.Join(", ", _current.Processes.Select(p => p + ".exe"))}";
        LoadRows();
    }

    private void OnLayerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        FlushRows();
        _layer = (LayerBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ctrl";
        LoadRows();
    }

    private void LoadRows()
    {
        RowsPanel.Children.Clear();
        _rows.Clear();
        if (_current != null && _current.Layers.TryGetValue(_layer, out var dict))
            foreach (var (k, v) in dict.OrderBy(x => SortKey(x.Key)))
                AddRow(k, v);
        UpdateHint();
    }

    private static int SortKey(string keyId)
    {
        if (keyId.Length == 1 && char.IsLetter(keyId[0])) return keyId[0];
        if (keyId.StartsWith("D") && keyId.Length == 2) return 100 + (keyId[1] - '0');
        if (keyId.StartsWith("F") && int.TryParse(keyId[1..], out var f)) return 200 + f;
        return 300;
    }

    private void FlushRows()
    {
        if (_current == null) return;
        var dict = new Dictionary<string, string>();
        foreach (var (key, desc) in _rows)
        {
            var id = (key.SelectedItem as KeyItem)?.Id;
            var d = desc.Text.Trim();
            if (id != null && d.Length > 0) dict[id] = d;
        }
        _current.Layers.TryGetValue(_layer, out var old);
        var changed = (old?.Count ?? 0) != dict.Count
            || dict.Any(kv => old == null || !old.TryGetValue(kv.Key, out var v) || v != kv.Value);
        if (changed) _dirty.Add(_current);
        if (dict.Count > 0) _current.Layers[_layer] = dict;
        else _current.Layers.Remove(_layer);
    }

    private void AddRow(string? keyId, string desc)
    {
        var key = new ComboBox { Width = 150, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6, 3, 6, 3) };
        foreach (var (id, label) in KeyChoices)
            key.Items.Add(new KeyItem { Id = id, Label = label });
        key.SelectedItem = key.Items.Cast<KeyItem>().FirstOrDefault(i => i.Id == keyId) ?? key.Items[0];

        var descBox = new TextBox { MinWidth = 180, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6, 3, 6, 3), Text = desc, FontSize = 12 };
        var del = new Button { Content = "×", Width = 26, Padding = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C)) };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(key);
        row.Children.Add(descBox);
        row.Children.Add(del);
        var tuple = (key, descBox);
        del.Click += (_, _) => { RowsPanel.Children.Remove(row); _rows.Remove(tuple); UpdateHint(); };
        _rows.Add(tuple);
        RowsPanel.Children.Add(row);
    }

    private void OnAddRow(object sender, RoutedEventArgs e) { AddRow(null, ""); UpdateHint(); }

    private void UpdateHint()
        => HintText.Text = _current == null ? "" : $"{_current.DisplayName} · {(LayerBox.SelectedItem as ComboBoxItem)?.Content} 层共 {_rows.Count} 条";

    private void OnNewApp(object sender, RoutedEventArgs e)
    {
        var dlg = new NewAppDialog(_preselectProcess) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.ResultProfile is not { } p) return;
        _working.Add(p);
        _dirty.Add(p);
        RefillAppBox();
        AppBox.SelectedItem = AppBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, p));
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog { Filter = "快捷键配置 (*.json)|*.json", Title = "导入快捷键配置" };
            if (dlg.ShowDialog(this) != true) return;
            var p = JsonSerializer.Deserialize<ShortcutProfile>(File.ReadAllText(dlg.FileName), AppSettings.JsonOpts());
            if (p is not { Id.Length: > 0 })
            {
                MessageBox.Show(this, "文件不是合法的快捷键配置（缺少 id 字段）。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _working.RemoveAll(x => string.Equals(x.Id, p.Id, StringComparison.OrdinalIgnoreCase));
            _working.Add(p);
            _dirty.Add(p);
            RefillAppBox();
            AppBox.SelectedItem = AppBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, p));
            UpdateHint();
            HintText.Text = $"已导入 {p.DisplayName}（保存后生效）";
        }
        catch (Exception ex)
        {
            Log.Error("import profile", ex);
            MessageBox.Show(this, "导入失败：" + ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            FlushRows();
            var dir = Path.Combine(Log.UserDir, "shortcuts");
            Directory.CreateDirectory(dir);
            foreach (var p in _dirty)
            {
                var path = Path.Combine(dir, p.Id + ".json");
                File.WriteAllText(path, JsonSerializer.Serialize(p,
                    new JsonSerializerOptions(AppSettings.JsonOpts()) { WriteIndented = true }));
            }
            _reload();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error("editor save", ex);
            MessageBox.Show(this, "保存失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class KeyItem
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public override string ToString() => Label;
    }
}
