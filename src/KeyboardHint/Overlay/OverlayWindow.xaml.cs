using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyboardHint.Core;
using KeyboardHint.Profiles;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace KeyboardHint.Overlay;

public partial class OverlayWindow : Window
{
    // Fluent-style palette: borderless keys defined by luminance layers,
    // Win11 accent blue (#60CDFF) for shortcut annotations.
    private static readonly Brush KeyBg = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
    private static readonly Brush KeyEdge = new SolidColorBrush(Color.FromArgb(0x59, 0x00, 0x00, 0x00));
    private static readonly Brush KeyLitBg = new SolidColorBrush(Color.FromArgb(0x26, 0x60, 0xCD, 0xFF));
    private static readonly Brush KeyLitEdge = new SolidColorBrush(Color.FromArgb(0xA6, 0x60, 0xCD, 0xFF));
    private static readonly Brush KeyFlashBg = new SolidColorBrush(Color.FromArgb(0x4D, 0x60, 0xCD, 0xFF));
    private static readonly Brush ModBg = new SolidColorBrush(Color.FromRgb(0x0F, 0x6C, 0xBD));
    private static readonly Brush ModEdge = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
    private static readonly Brush TextMain = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
    private static readonly Brush TextAccent = new SolidColorBrush(Color.FromRgb(0x7C, 0xD1, 0xFF));
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");

    private sealed class KeyVisual
    {
        public required Border Border { get; init; }
        public required TextBlock Shortcut { get; init; }
    }

    private readonly AppSettings _settings;
    private readonly Dictionary<string, KeyVisual> _keys = new(StringComparer.OrdinalIgnoreCase);
    private int _animToken;
    private DispatcherTimer? _flashTimer;

    public bool IsShowing { get; private set; }

    private readonly TranslateTransform _slide = new();

    public OverlayWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        System.Windows.Documents.TextElement.SetFontFamily(RootBorder, UiFont);
        RootBorder.RenderTransform = _slide;
        BuildKeyboard();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Win11 native rounded corners + acrylic transient backdrop (fluent flyout look).
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int round = Native.Win32.DWMWCP_ROUND;
        Native.Win32.DwmSetWindowAttribute(hwnd, Native.Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        int backdrop = Native.Win32.DWMSBT_TRANSIENTWINDOW;
        int hr = Native.Win32.DwmSetWindowAttribute(hwnd, Native.Win32.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        // Only go transparent where the backdrop actually applied — on older
        // Windows a transparent opaque window renders solid black.
        if (hr == 0)
            Background = System.Windows.Media.Brushes.Transparent;
    }

    private void BuildKeyboard()
    {
        foreach (var row in KeyboardLayout.Build())
        {
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            foreach (var def in row.Keys)
            {
                if (def.Id.Length == 0)
                {
                    rowPanel.Children.Add(new FrameworkElement { Width = def.Width * KeyboardLayout.Unit });
                    continue;
                }

                var shortcut = new TextBlock
                {
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextAccent,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Margin = new Thickness(3, 0, 3, 5)
                };
                var legend = new TextBlock
                {
                    Text = def.Label,
                    FontSize = row.Height < 40 ? 10 : 12.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = TextMain,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                var grid = new Grid();
                grid.Children.Add(legend);
                grid.Children.Add(shortcut);
                var border = new Border
                {
                    Width = def.Width * KeyboardLayout.Unit - 6,
                    Height = row.Height,
                    Margin = new Thickness(3, 0, 3, 0),
                    CornerRadius = new CornerRadius(5),
                    Background = KeyBg,
                    // bottom-only dark edge reads as a physical key "lip"
                    BorderBrush = KeyEdge,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = grid
                };
                rowPanel.Children.Add(border);
                _keys[def.Id] = new KeyVisual { Border = border, Shortcut = shortcut };
            }
            KeyboardPanel.Children.Add(rowPanel);
        }
    }

    private static readonly Dictionary<string, string[]> LayerModifiers = new()
    {
        ["ctrl"] = new[] { "LControl", "RControl" },
        ["ctrl+shift"] = new[] { "LControl", "RControl", "LShift", "RShift" },
        ["ctrl+alt"] = new[] { "LControl", "RControl", "LAlt", "RAlt" },
    };

    /// <summary>Re-render shortcut annotations for a modifier layer — used both
    /// at show time and when the user presses Shift/Alt while Ctrl is held.</summary>
    public void SetLayer(ShortcutProfile profile, string layerName)
    {
        var layer = profile.Layer(layerName);
        foreach (var kv in _keys)
        {
            kv.Value.Shortcut.Text = "";
            kv.Value.Border.Background = KeyBg;
            kv.Value.Border.BorderBrush = KeyEdge;
        }
        foreach (var (id, desc) in layer)
        {
            if (_keys.TryGetValue(id, out var kv))
            {
                kv.Shortcut.Text = desc;
                kv.Border.Background = KeyLitBg;
                kv.Border.BorderBrush = KeyLitEdge;
            }
        }
        // Highlight every modifier in the active chord.
        var mods = LayerModifiers.TryGetValue(layerName, out var m) ? m : LayerModifiers["ctrl"];
        foreach (var id in mods)
        {
            if (_keys.TryGetValue(id, out var kv))
            {
                kv.Border.Background = ModBg;
                kv.Border.BorderBrush = ModEdge;
            }
        }

        var pretty = string.Join("+", layerName.Split('+')
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
        Caption.Text = layer.Count > 0
            ? $"{profile.DisplayName} · {pretty} 快捷键"
            : $"{profile.DisplayName} · {pretty}（暂无数据）";
    }

    public void ShowFor(ShortcutProfile profile, ForegroundContext ctx)
    {
        // WorkArea is in physical pixels; WPF window coords are DIPs.
        double s = Math.Max(0.5, ctx.DpiScale);
        double waLeft = ctx.WorkArea.Left / s;
        double waWidth = ctx.WorkArea.Width / s;
        double waBottom = ctx.WorkArea.Bottom / s;

        SetLayer(profile, "ctrl");

        // Show while RootBorder.Opacity is still 0 (invisible), then position
        // AFTER the first real layout pass — UpdateLayout() does not produce a
        // non-zero ActualSize synchronously for a just-shown window.
        KeyboardPanel.LayoutTransform = Transform.Identity;
        Visibility = Visibility.Visible;
        IsShowing = true;
        _posTries = 0;
        Dispatcher.BeginInvoke(new Action(() => PositionWhenReady(waLeft, waWidth, waBottom)),
            DispatcherPriority.Render);
    }

    private int _posTries;

    private void PositionWhenReady(double waLeft, double waWidth, double waBottom)
    {
        if (!IsShowing) return; // user released Ctrl before layout settled
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            if (_posTries++ < 20)
                Dispatcher.BeginInvoke(new Action(() => PositionWhenReady(waLeft, waWidth, waBottom)),
                    DispatcherPriority.Render);
            else
                Log.Error("overlay position: layout never produced a size");
            return;
        }
        double w = ActualWidth, h = ActualHeight;
        double scale = Math.Min(1.0, waWidth * 0.75 / w);
        if (scale < 1.0)
        {
            KeyboardPanel.LayoutTransform = new ScaleTransform(scale, scale);
            w *= scale;
            h *= scale;
        }
        Left = waLeft + (waWidth - w) / 2;
        Top = waBottom - h - 40;
        Log.Info($"overlay pos: L={Left:F0} T={Top:F0} size={w:F0}x{h:F0} waL={waLeft:F0} waW={waWidth:F0} waB={waBottom:F0}");
        FadeTo(_settings.Opacity);

        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Native.Win32.GetWindowRect(hwnd, out var wr);
            Log.Info($"overlay state: vis={Visibility} isVisible={IsVisible} op={RootBorder.Opacity:F2} " +
                     $"actual={ActualWidth:F0}x{ActualHeight:F0} L={Left:F0} T={Top:F0} W={Width:F0} H={Height:F0} " +
                     $"hwnd={hwnd} winrect=({wr.Left},{wr.Top},{wr.Right},{wr.Bottom}) " +
                     $"rbDesired={RootBorder.DesiredSize.Width:F0}x{RootBorder.DesiredSize.Height:F0} " +
                     $"rbRender={RootBorder.RenderSize.Width:F0}x{RootBorder.RenderSize.Height:F0} " +
                     $"panelChildren={KeyboardPanel.Children.Count} rbVis={RootBorder.Visibility}");
        };
        t.Start();
    }

    // Note: no render prewarm — a hidden/offscreen warm-up breaks WPF layout
    // (visual tree never arranges) and a visible one flashes on screen. First
    // show costs ~300ms once; the hook watchdog heals any missed key-up.

    public void HideOverlay()
    {
        if (!IsShowing && Visibility != Visibility.Visible) return;
        int token = ++_animToken;
        IsShowing = false;
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
        anim.Completed += (_, _) =>
        {
            if (token == _animToken)
            {
                Visibility = Visibility.Collapsed;
                RootBorder.Opacity = 0;
            }
        };
        RootBorder.BeginAnimation(OpacityProperty, anim);
    }

    /// <summary>Briefly light up the key that was just pressed with Ctrl held.</summary>
    public void FlashKey(int vk)
    {
        var id = KeyIds.FromVk(vk);
        if (id == null || !_keys.TryGetValue(id, out var kv)) return;
        if (kv.Shortcut.Text.Length == 0) return;

        kv.Border.Background = KeyFlashBg;
        _flashTimer?.Stop();
        var b = kv.Border;
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _flashTimer.Tick += (_, _) =>
        {
            _flashTimer.Stop();
            if (b.Background == KeyFlashBg) b.Background = KeyLitBg;
        };
        _flashTimer.Start();
    }

    private void FadeTo(double target)
    {
        int token = ++_animToken;
        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(180)));
        // Win11 flyout-style rise: slide up 14px while fading in.
        _slide.Y = 14;
        _slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        _ = token; // token just cancels any pending hide-completion
    }

    public void ForceClose()
    {
        RootBorder.BeginAnimation(OpacityProperty, null);
        Close();
    }
}
