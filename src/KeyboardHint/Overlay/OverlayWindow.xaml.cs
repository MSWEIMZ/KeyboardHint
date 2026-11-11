using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyboardHint.Core;
using KeyboardHint.Profiles;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;

namespace KeyboardHint.Overlay;

public partial class OverlayWindow : Window
{
    private static readonly Brush KeyBg = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x34));
    private static readonly Brush KeyBorder = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x52));
    private static readonly Brush KeyLitBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x47, 0x41));
    private static readonly Brush KeyLitBorder = new SolidColorBrush(Color.FromRgb(0x3F, 0xB8, 0xA6));
    private static readonly Brush KeyFlashBg = new SolidColorBrush(Color.FromRgb(0x35, 0x9C, 0x8C));
    private static readonly Brush ModBg = new SolidColorBrush(Color.FromRgb(0x2B, 0x5B, 0x8C));
    private static readonly Brush ModBorder = new SolidColorBrush(Color.FromRgb(0x5C, 0x9B, 0xD6));
    private static readonly Brush TextMain = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED));
    private static readonly Brush TextDim = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
    private static readonly Brush TextAccent = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));

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

    public OverlayWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        BuildKeyboard();
    }

    private void BuildKeyboard()
    {
        foreach (var row in KeyboardLayout.Build())
        {
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
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
                    FontWeight = FontWeights.Medium,
                    Foreground = TextAccent,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Margin = new Thickness(1, 0, 1, 3),
                    MaxHeight = 26
                };
                var legend = new TextBlock
                {
                    Text = def.Label,
                    FontSize = row.Height < 40 ? 10 : 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMain,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = row.Height < 40 ? System.Windows.VerticalAlignment.Center : System.Windows.VerticalAlignment.Top,
                    Margin = new Thickness(0, row.Height < 40 ? 0 : 4, 0, 0)
                };
                var grid = new Grid();
                grid.Children.Add(legend);
                grid.Children.Add(shortcut);
                var border = new Border
                {
                    Width = def.Width * KeyboardLayout.Unit - 6,
                    Height = row.Height,
                    Margin = new Thickness(3, 0, 3, 0),
                    CornerRadius = new CornerRadius(6),
                    Background = KeyBg,
                    BorderBrush = KeyBorder,
                    BorderThickness = new Thickness(1),
                    Child = grid
                };
                rowPanel.Children.Add(border);
                _keys[def.Id] = new KeyVisual { Border = border, Shortcut = shortcut };
            }
            KeyboardPanel.Children.Add(rowPanel);
        }
    }

    public void ShowFor(ShortcutProfile profile, ForegroundContext ctx)
    {
        // WorkArea is in physical pixels; WPF window coords are DIPs.
        double s = Math.Max(0.5, ctx.DpiScale);
        double waLeft = ctx.WorkArea.Left / s;
        double waWidth = ctx.WorkArea.Width / s;
        double waBottom = ctx.WorkArea.Bottom / s;

        var layer = profile.Layer("ctrl");
        foreach (var kv in _keys)
        {
            kv.Value.Shortcut.Text = "";
            kv.Value.Border.Background = KeyBg;
            kv.Value.Border.BorderBrush = KeyBorder;
        }
        foreach (var (id, desc) in layer)
        {
            if (_keys.TryGetValue(id, out var kv))
            {
                kv.Shortcut.Text = desc;
                kv.Border.Background = KeyLitBg;
                kv.Border.BorderBrush = KeyLitBorder;
            }
        }
        // Highlight the modifier being held.
        foreach (var id in new[] { "LControl", "RControl" })
        {
            if (_keys.TryGetValue(id, out var kv))
            {
                kv.Border.Background = ModBg;
                kv.Border.BorderBrush = ModBorder;
            }
        }

        Caption.Text = $"{profile.DisplayName} · Ctrl 快捷键";

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

    /// <summary>Warm up measure+render once at startup so the first real show
    /// doesn't stall the UI thread (a stall can make the OS drop low-level hook
    /// callbacks → lost key-up events). The window must be ON-SCREEN: creating
    /// the hwnd offscreen or with Window.Opacity=0 skips WPF's composition and
    /// leaves the visual tree permanently unlaid-out (renders nothing).</summary>
    public void Prewarm()
    {
        RootBorder.Opacity = 0; // invisible but still composed
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - 100) / 2;
        Top = wa.Bottom - 200;
        Visibility = Visibility.Visible;
        // Collapse after a few frames so a real layout+render pass actually ran
        // (UpdateLayout() is not synchronous for a just-shown window).
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (!IsShowing) Visibility = Visibility.Collapsed;
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
        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(140)));
        _ = token; // token just cancels any pending hide-completion
    }

    public void ForceClose()
    {
        RootBorder.BeginAnimation(OpacityProperty, null);
        Close();
    }
}
