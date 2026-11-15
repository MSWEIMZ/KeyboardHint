using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyboardHint.Core;
using KeyboardHint.Profiles;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace KeyboardHint.Overlay;

public partial class OverlayWindow : Window
{
    // Palette built from settings (theme + accent) — see ApplyTheme().
    private Brush KeyBg = null!;
    private Brush KeyEdge = null!;
    private Brush KeyLitBg = null!;
    private Brush KeyLitEdge = null!;
    private Brush KeyFlashBg = null!;
    private Brush ModBg = null!;
    private Brush ModEdge = null!;
    private Brush TextMain = null!;
    private Brush TextAccent = null!;
    private Brush _windowFallback = null!; // used when DWM backdrop is unavailable
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");

    private sealed class KeyVisual
    {
        public required Border Border { get; init; }
        public required TextBlock Shortcut { get; init; }
        public required TextBlock Legend { get; init; }
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
        ApplyTheme(); // brushes must exist before BuildKeyboard uses them
        System.Windows.Documents.TextElement.SetFontFamily(RootBorder, UiFont);
        RootBorder.RenderTransform = _slide;
        BuildKeyboard();
    }

    /// <summary>Rebuild the palette from settings.Theme + settings.Accent and
    /// recolor the panel and every key. Safe to call live (settings save).</summary>
    public void ApplyTheme()
    {
        bool dark = !string.Equals(_settings.Theme, "light", StringComparison.OrdinalIgnoreCase);
        Color accent;
        try { accent = (Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.Accent); }
        catch { accent = Color.FromRgb(0x60, 0xCD, 0xFF); }

        static Color Shade(Color c, double f) => Color.FromRgb(
            (byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
        static Color Tint(Color c, double f) => Color.FromRgb(
            (byte)(c.R + (255 - c.R) * f), (byte)(c.G + (255 - c.G) * f), (byte)(c.B + (255 - c.B) * f));
        static Brush A(byte a, Color c) => new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));

        _windowFallback = new SolidColorBrush(dark ? Color.FromRgb(0x14, 0x14, 0x18) : Color.FromRgb(0xF6, 0xF8, 0xFA));
        RootBorder.Background = new SolidColorBrush(dark ? Color.FromArgb(0xC0, 0x14, 0x14, 0x18) : Color.FromArgb(0xD8, 0xF7, 0xF9, 0xFC));
        RootBorder.BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
        Caption.Foreground = new SolidColorBrush(dark ? Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xA6, 0x1B, 0x1B, 0x1F));

        KeyBg = dark ? new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF))
                     : new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
        KeyEdge = new SolidColorBrush(dark ? Color.FromArgb(0x59, 0x00, 0x00, 0x00)
                                           : Color.FromArgb(0x30, 0x00, 0x00, 0x00));
        KeyLitBg = A(0x26, accent);
        KeyLitEdge = A(0xA6, accent);
        KeyFlashBg = A(0x4D, accent);
        ModBg = new SolidColorBrush(Shade(accent, 0.5)); // dark enough for white legend
        ModEdge = new SolidColorBrush(dark ? accent : Shade(accent, 0.7));
        TextMain = new SolidColorBrush(dark ? Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)
                                            : Color.FromArgb(0xF2, 0x1B, 0x1B, 0x1F));
        TextAccent = new SolidColorBrush(dark ? Tint(accent, 0.2) : Shade(accent, 0.55));

        // Window background is only a fallback — the DWM acrylic backdrop
        // replaces it visually once applied, so don't fight transparency.
        if (Background != System.Windows.Media.Brushes.Transparent)
            Background = _windowFallback;

        if (_keys.Count > 0)
        {
            foreach (var kv in _keys.Values)
            {
                kv.Legend.Foreground = TextMain;
                kv.Shortcut.Foreground = TextAccent;
            }
            if (_lastProfile != null && _lastLayer != null)
                SetLayer(_lastProfile, _lastLayerName, _lastLayer); // restores lit/mod colors
        }
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
        Background = hr == 0
            ? System.Windows.Media.Brushes.Transparent
            : _windowFallback;
    }

    private void BuildKeyboard()
    {
        foreach (var row in KeyboardLayout.Build())
        {
            bool shortRow = row.Height < 40;
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            foreach (var def in row.Keys)
            {
                if (def.Id.Length == 0)
                {
                    rowPanel.Children.Add(new FrameworkElement { Width = def.Width * KeyboardLayout.Unit });
                    continue;
                }

                double keyWidth = def.Width * KeyboardLayout.Unit - 6;
                var shortcut = new TextBlock
                {
                    FontSize = shortRow ? 8.5 : 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextAccent,
                    TextAlignment = TextAlignment.Center,
                    // short rows: single line + shrink to fit; tall rows: wrap to 2 lines first
                    TextWrapping = shortRow ? TextWrapping.NoWrap : TextWrapping.Wrap,
                    MaxWidth = keyWidth - 4,
                    LineHeight = shortRow ? 10 : 12,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                };
                // Viewbox scales over-long text down instead of clipping it.
                var shortcutBox = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Margin = new Thickness(1, 0, 1, shortRow ? 1 : 4),
                    MaxHeight = shortRow ? 10 : 24,
                    Child = shortcut
                };
                var legend = new TextBlock
                {
                    Text = def.Label,
                    FontSize = shortRow ? 9 : 13,
                    FontWeight = FontWeights.Medium,
                    Foreground = TextMain,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    // short rows: legend pinned top so the annotation owns the bottom
                    VerticalAlignment = shortRow
                        ? System.Windows.VerticalAlignment.Top
                        : System.Windows.VerticalAlignment.Center,
                    Margin = shortRow ? new Thickness(0, 2, 0, 0) : new Thickness(0)
                };
                var grid = new Grid();
                grid.Children.Add(legend);
                grid.Children.Add(shortcutBox);
                var border = new Border
                {
                    Width = keyWidth,
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
                _keys[def.Id] = new KeyVisual { Border = border, Shortcut = shortcut, Legend = legend };
            }
            KeyboardPanel.Children.Add(rowPanel);
        }
    }

    private static readonly Dictionary<string, string[]> ModKeyIds = new()
    {
        ["ctrl"] = new[] { "LControl", "RControl" },
        ["alt"] = new[] { "LAlt", "RAlt" },
        ["shift"] = new[] { "LShift", "RShift" },
        ["win"] = new[] { "LWin", "RWin" },
    };

    private bool _pinned;
    private ShortcutProfile? _lastProfile;
    private string _lastLayerName = "ctrl";
    private IReadOnlyDictionary<string, string>? _lastLayer;

    /// <summary>Pinned overlays get a hint in the caption so the dismissal
    /// gesture (tap the trigger key again) is discoverable.</summary>
    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        if (_lastProfile != null && _lastLayer != null)
            SetLayer(_lastProfile, _lastLayerName, _lastLayer); // refresh caption
    }

    /// <summary>Re-render shortcut annotations for a modifier layer — used both
    /// at show time and when the user presses another modifier while holding
    /// the trigger key.</summary>
    public void SetLayer(ShortcutProfile profile, string layerName, IReadOnlyDictionary<string, string> layer)
    {
        _lastProfile = profile;
        _lastLayerName = layerName;
        _lastLayer = layer;
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
        foreach (var id in layerName.Split('+').SelectMany(p => ModKeyIds.TryGetValue(p, out var m) ? m : Array.Empty<string>()))
        {
            if (_keys.TryGetValue(id, out var kv))
            {
                kv.Border.Background = ModBg;
                kv.Border.BorderBrush = ModEdge;
            }
        }

        var pretty = string.Join("+", layerName.Split('+')
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
        Caption.Text = (layer.Count > 0
            ? $"{profile.DisplayName} · {pretty} 快捷键"
            : $"{profile.DisplayName} · {pretty}（暂无数据）")
            + (_pinned ? "　·　已钉住（再按一次修饰键关闭）" : "　·　加按其他修饰键切层");
    }

    public void ShowFor(ShortcutProfile profile, ForegroundContext ctx, string layerName, IReadOnlyDictionary<string, string> layer)
    {
        // WorkArea is in physical pixels; WPF window coords are DIPs.
        double s = Math.Max(0.5, ctx.DpiScale);
        double waLeft = ctx.WorkArea.Left / s;
        double waWidth = ctx.WorkArea.Width / s;
        double waBottom = ctx.WorkArea.Bottom / s;

        SetLayer(profile, layerName, layer);

        Width = double.NaN; Height = double.NaN; // in case Prewarm's 1px size is still on
        KeyboardPanel.LayoutTransform = Transform.Identity;
        Visibility = Visibility.Visible;
        IsShowing = true;
        // Once we've rendered at least once we know the size: position
        // synchronously so the panel appears already centered — no invisible
        // layout hop. The Render-priority pass below corrects any drift.
        _faded = false;
        if (_posSize.Width > 0)
        {
            Position(_posSize, waLeft, waWidth, waBottom);
            FadeTo(_settings.Opacity);
            _faded = true;
        }
        _posTries = 0;
        Dispatcher.BeginInvoke(new Action(() => PositionWhenReady(waLeft, waWidth, waBottom)),
            DispatcherPriority.Render);
    }

    private Size _posSize; // last rendered size in DIPs; zero until first layout
    private int _posTries;
    private bool _faded;   // fade-in already kicked off for the current show

    private void Position(Size size, double waLeft, double waWidth, double waBottom)
    {
        double w = size.Width, h = size.Height;
        double scale = Math.Min(1.0, waWidth * 0.75 / w);
        if (scale < 1.0)
        {
            KeyboardPanel.LayoutTransform = new ScaleTransform(scale, scale);
            w *= scale;
            h *= scale;
        }
        Left = waLeft + (waWidth - w) / 2;
        Top = waBottom - h - 40;
    }

    private void PositionWhenReady(double waLeft, double waWidth, double waBottom)
    {
        if (!IsShowing) return; // user released before layout settled
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            if (_posTries++ < 20)
                Dispatcher.BeginInvoke(new Action(() => PositionWhenReady(waLeft, waWidth, waBottom)),
                    DispatcherPriority.Render);
            else
                Log.Error("overlay position: layout never produced a size");
            return;
        }
        _posSize = new Size(ActualWidth, ActualHeight);
        Position(_posSize, waLeft, waWidth, waBottom);
        Log.Info($"overlay pos: L={Left:F0} T={Top:F0} size={ActualWidth:F0}x{ActualHeight:F0}");
        if (!_faded) FadeTo(_settings.Opacity);
    }

    /// <summary>Materialize hwnd + fonts + render pipeline once at startup via
    /// an imperceptible 1px transparent show — first real trigger is then warm.
    /// Must not be offscreen (that poisons WPF composition).</summary>
    public void Prewarm()
    {
        Width = 1; Height = 1;
        Left = SystemParameters.WorkArea.Right - 2;
        Top = SystemParameters.WorkArea.Bottom - 2;
        RootBorder.Opacity = 0;
        Visibility = Visibility.Visible;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (IsShowing) return; // a real trigger already took over
            Visibility = Visibility.Collapsed;
            Width = double.NaN; Height = double.NaN;
            if (ActualWidth > 0) _posSize = new Size(ActualWidth, ActualHeight);
        };
        t.Start();
    }

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
        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(80)));
        // Win11 flyout-style rise: slide up slightly while fading in.
        _slide.Y = 10;
        _slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
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
