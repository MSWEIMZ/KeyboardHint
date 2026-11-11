using System.Windows.Threading;
using KeyboardHint.Native;
using KeyboardHint.Overlay;
using KeyboardHint.Profiles;

namespace KeyboardHint.Core;

/// <summary>
/// State machine: hold Ctrl (and only Ctrl) for DelayMs → show overlay
/// with the foreground app's Ctrl-layer shortcuts. Release → hide.
/// Never swallows input.
/// </summary>
public sealed class CtrlHoldTrigger : IDisposable
{
    private static readonly HashSet<int> CtrlKeys = new()
        { Win32.VK_LCONTROL, Win32.VK_RCONTROL };

    private static readonly HashSet<int> OtherModifiers = new()
    {
        Win32.VK_LSHIFT, Win32.VK_RSHIFT, Win32.VK_LMENU, Win32.VK_RMENU,
        Win32.VK_LWIN, Win32.VK_RWIN, Win32.VK_SHIFT, Win32.VK_CONTROL, Win32.VK_MENU
    };

    private readonly AppSettings _settings;
    private readonly ProfileStore _store;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _holdTimer;
    private readonly DispatcherTimer _watchdog;

    private readonly HashSet<int> _down = new(); // keys currently held (dedupes auto-repeat)
    private bool _pendingShow;

    public bool Enabled { get; set; }

    public CtrlHoldTrigger(AppSettings settings, ProfileStore store, OverlayWindow overlay)
    {
        _settings = settings;
        _store = store;
        _overlay = overlay;
        Enabled = settings.Enabled;

        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(150, settings.DelayMs)) };
        _holdTimer.Tick += (_, _) => { _holdTimer.Stop(); TryShow(); };

        // LL hooks can miss key-ups (focus steals etc.) — poll as a safety net.
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _watchdog.Tick += (_, _) => { if (IsShowing() && !AnyCtrlDown()) HideAll(); };
    }

    public void Start() => _watchdog.Start();

    private int CtrlHeldCount => _down.Count(CtrlKeys.Contains);

    public void OnKey(int vk, bool down)
    {
        if (!Enabled)
        {
            if (IsShowing()) HideAll();
            _down.Clear();
            return;
        }

        if (CtrlKeys.Contains(vk))
        {
            bool hadCtrl = CtrlHeldCount > 0;
            if (down) _down.Add(vk); else _down.Remove(vk);
            bool hasCtrl = CtrlHeldCount > 0;

            if (hasCtrl && !hadCtrl && !_pendingShow && !IsShowing())
            {
                _pendingShow = true;
                _holdTimer.Stop();
                _holdTimer.Start();
            }
            else if (!hasCtrl)
            {
                HideAll();
            }
            return;
        }

        if (down) _down.Add(vk); else _down.Remove(vk);
        if (!down) return;

        if (OtherModifiers.Contains(vk))
        {
            // Ctrl+Shift/Alt/Win held → not the "pure Ctrl" gesture; hide/cancel.
            HideAll();
            return;
        }

        // A real key while Ctrl is held = an actual shortcut combo.
        if (_pendingShow)
        {
            _pendingShow = false;
            _holdTimer.Stop();
        }
        else if (IsShowing() && AnyCtrlDown())
        {
            _overlay.FlashKey(vk); // moment of delight: light up the key they just used
        }
    }

    private void TryShow()
    {
        _pendingShow = false;
        if (!Enabled || CtrlHeldCount == 0 || !AnyCtrlDown()) return;
        if (AnyOtherModifierDown()) return;
        if (AnyMouseButtonDown()) return; // Ctrl+click / Ctrl+drag multi-select

        var ctx = ForegroundApp.Capture();
        if (ctx.IsSuppressed(_settings.ExcludedProcesses)) return;

        var profile = _store.Resolve(ctx.ProcessName);
        Log.Info($"show overlay: process={ctx.ProcessName} profile={profile.Id}");
        _overlay.ShowFor(profile, ctx.WorkArea);
    }

    private void HideAll()
    {
        _pendingShow = false;
        _holdTimer.Stop();
        _overlay.HideOverlay();
    }

    private bool IsShowing() => _overlay.IsShowing;

    private static bool AnyCtrlDown()
        => Win32.KeyDown(Win32.VK_LCONTROL) || Win32.KeyDown(Win32.VK_RCONTROL);

    private static bool AnyOtherModifierDown()
        => Win32.KeyDown(Win32.VK_LSHIFT) || Win32.KeyDown(Win32.VK_RSHIFT)
        || Win32.KeyDown(Win32.VK_LMENU) || Win32.KeyDown(Win32.VK_RMENU)
        || Win32.KeyDown(Win32.VK_LWIN) || Win32.KeyDown(Win32.VK_RWIN);

    private static bool AnyMouseButtonDown()
        => Win32.KeyDown(Win32.VK_LBUTTON) || Win32.KeyDown(Win32.VK_RBUTTON)
        || Win32.KeyDown(Win32.VK_MBUTTON) || Win32.KeyDown(Win32.VK_XBUTTON1)
        || Win32.KeyDown(Win32.VK_XBUTTON2);

    public void Dispose()
    {
        _holdTimer.Stop();
        _watchdog.Stop();
    }
}
