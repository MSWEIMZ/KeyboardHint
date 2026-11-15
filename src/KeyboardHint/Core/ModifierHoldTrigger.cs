using System.Windows.Threading;
using KeyboardHint.Native;
using KeyboardHint.Overlay;
using KeyboardHint.Profiles;

namespace KeyboardHint.Core;

/// <summary>
/// State machine: hold a configured modifier (Ctrl / Alt / Win / Shift, each
/// individually toggleable) for its delay → show the overlay with the
/// foreground app's shortcuts for that modifier layer. Pressing more
/// modifiers after the overlay shows switches to the chord layer
/// (ctrl+shift, win+ctrl, ...). Release → hide, unless StayOnRelease
/// pins it until the trigger is tapped again.
/// Triggers with suppressRelease (Win/Alt) get a dummy F24 keypress injected
/// while held so the release is treated as part of a chord — no Start menu,
/// no menu-bar focus grab. Never swallows real input: swallowing a key-up
/// leaves GetAsyncKeyState stuck "down".
/// </summary>
public sealed class ModifierHoldTrigger : IDisposable
{
    private static readonly Dictionary<string, HashSet<int>> ModSets = new()
    {
        ["ctrl"]  = new() { Win32.VK_LCONTROL, Win32.VK_RCONTROL, Win32.VK_CONTROL },
        ["alt"]   = new() { Win32.VK_LMENU, Win32.VK_RMENU, Win32.VK_MENU },
        ["shift"] = new() { Win32.VK_LSHIFT, Win32.VK_RSHIFT, Win32.VK_SHIFT },
        ["win"]   = new() { Win32.VK_LWIN, Win32.VK_RWIN },
    };

    private static readonly Dictionary<int, string> VkToMod = BuildMap();

    /// <summary>Canonical order used when naming combo layers.</summary>
    private static readonly string[] LayerOrder = { "win", "ctrl", "alt", "shift" };

    private static Dictionary<int, string> BuildMap()
    {
        var m = new Dictionary<int, string>();
        foreach (var (name, set) in ModSets)
            foreach (var vk in set)
                m.TryAdd(vk, name);
        return m;
    }

    private readonly AppSettings _settings;
    private readonly ProfileStore _store;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _holdTimer;
    private readonly DispatcherTimer _watchdog;

    private readonly HashSet<int> _down = new(); // keys currently held (dedupes auto-repeat)
    private string? _pendingTrigger;             // modifier group waiting out its delay
    private string? _activeTrigger;              // modifier that opened the current overlay
    private bool _pinned;                        // StayOnRelease: survived trigger release
    private long _eventsSeen;
    private long _lastLoggedEvents;
    private ShortcutProfile? _activeProfile;
    private string _activeLayer = "ctrl";

    public bool Enabled { get; set; }

    public ModifierHoldTrigger(AppSettings settings, ProfileStore store, OverlayWindow overlay)
    {
        _settings = settings;
        _store = store;
        _overlay = overlay;
        Enabled = settings.Enabled;

        _holdTimer = new DispatcherTimer();
        _holdTimer.Tick += (_, _) => { _holdTimer.Stop(); TryShow(); };

        // LL hooks can miss key-ups while the UI thread is busy — purge stale
        // tracked keys against physical state and hide if the trigger isn't held.
        // Idle 时降到 1s 省电，按住键/显示中保持 300ms 快速清理。
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _watchdog.Tick += (_, _) =>
        {
            foreach (var vk in _down.ToList())
                if (!Win32.KeyDown(vk)) _down.Remove(vk);
            if (IsShowing() && !Enabled) HideAll();
            else if (IsShowing() && !_pinned && !TriggerPhysicallyDown(_activeTrigger)) HideAll();
            else if (IsShowing()) SwitchLayer(); // re-sync if a modifier key-up was lost
            var busy = _down.Count > 0 || IsShowing() || _pendingTrigger != null;
            var want = TimeSpan.FromMilliseconds(busy ? 300 : 1000);
            if (_watchdog.Interval != want) _watchdog.Interval = want;
            if (_eventsSeen != _lastLoggedEvents)
            {
                _lastLoggedEvents = _eventsSeen;
                Log.Info($"keys seen={_eventsSeen} down=[{string.Join(",", _down)}] pending={_pendingTrigger ?? "-"} showing={IsShowing()}");
            }
        };
    }

    public void Start() => _watchdog.Start();

    /// <summary>Handle a key event; return true to swallow it (Win key-up
    /// after a Win-hold show).</summary>
    public bool OnKey(int vk, bool down)
    {
        _eventsSeen++;
        if (!Enabled)
        {
            if (IsShowing()) HideAll();
            _down.Clear();
            _pendingTrigger = null;
            return false;
        }

        if (VkToMod.TryGetValue(vk, out var mod))
            return OnModifier(mod, vk, down);

        // Non-modifier key.
        if (down) _down.Add(vk); else _down.Remove(vk);
        if (!down) return false;

        if (_pendingTrigger != null)
        {
            // A real key during the hold delay = an actual shortcut combo.
            _pendingTrigger = null;
            _holdTimer.Stop();
        }
        else if (IsShowing() && !_pinned && TriggerPhysicallyDown(_activeTrigger))
        {
            _overlay.FlashKey(vk); // moment of delight: light up the key they just used
        }
        return false;
    }

    private bool OnModifier(string mod, int vk, bool down)
    {
        if (down)
        {
            _down.Add(vk);

            if (_pinned && mod == _activeTrigger)
            {
                // Tap (or hold) the trigger again while pinned → dismiss;
                // a hold can also re-pend a fresh show below.
                _overlay.SetPinned(false);
                _pinned = false;
                _overlay.HideOverlay();
                // This fresh press would open Start (Win) / grab menu focus
                // (Alt) on release without a chord-mate — inject F24 again.
                if (_settings.Trigger(mod).SuppressRelease) InjectDummyKey();
            }

            if (!IsShowing() && _pendingTrigger == null)
            {
                if (_settings.Trigger(mod).Enabled && !OtherGroupHeld(mod))
                {
                    _pendingTrigger = mod;
                    _holdTimer.Stop();
                    _holdTimer.Interval = TimeSpan.FromMilliseconds(_settings.DelayFor(mod));
                    _holdTimer.Start();
                }
            }
            else if (_pendingTrigger != null && _pendingTrigger != mod)
            {
                // Two different modifiers before the delay = a real chord
                // (e.g. Ctrl+Shift input-method switch, AltGr) — stay hidden.
                _pendingTrigger = null;
                _holdTimer.Stop();
            }
            else if (IsShowing() && mod != _activeTrigger)
            {
                SwitchLayer();
            }
            return false;
        }

        // ---- key-up ----
        _down.Remove(vk);

        if (_pendingTrigger == mod && !GroupHeld(mod))
        {
            _pendingTrigger = null;
            _holdTimer.Stop();
        }
        if (IsShowing() && mod == _activeTrigger && !GroupHeld(mod))
        {
            if (_settings.StayOnRelease)
            {
                _pinned = true;
                _overlay.SetPinned(true);
            }
            else HideAll();
        }
        else if (IsShowing()) SwitchLayer(); // released a chord modifier → back to base

        return false;
    }

    /// <summary>Wheel/button activity while a modifier is held = the user is
    /// zooming or multi-selecting — suppress pending/showing overlays.
    /// Pinned overlays survive (user wants the card up while clicking).</summary>
    public void OnMouseActivity()
    {
        if (_pinned) return;
        if (!IsShowing() && _pendingTrigger == null) return;
        HideAll();
        Log.Info("suppressed: mouse activity while modifier held");
    }

    /// <summary>Layer name = held modifier groups joined in canonical order
    /// (win, ctrl, alt, shift) → "ctrl", "ctrl+shift", "win+shift", ...</summary>
    private string CurrentLayer()
    {
        var held = LayerOrder.Where(GroupHeld).ToList();
        return held.Count > 0 ? string.Join("+", held) : _activeTrigger ?? "ctrl";
    }

    private void SwitchLayer()
    {
        if (_activeProfile == null) return;
        var layer = CurrentLayer();
        if (layer == _activeLayer) return;
        var data = LayerData(_activeProfile, layer);
        if (data.Count == 0) return; // nothing defined for this chord — keep current view
        _activeLayer = layer;
        Log.Info($"layer → {layer}");
        _overlay.SetLayer(_activeProfile, layer, data);
    }

    /// <summary>Effective shortcuts for a layer: app profile first, then the
    /// default profile — so every app inherits the OS-level win/alt lists.</summary>
    private IReadOnlyDictionary<string, string> LayerData(ShortcutProfile profile, string layer)
    {
        var d = profile.Layer(layer);
        if (d.Count == 0 && !string.Equals(profile.Id, "default", StringComparison.OrdinalIgnoreCase))
            d = _store.Default.Layer(layer);
        return d;
    }

    private void TryShow()
    {
        var mod = _pendingTrigger;
        _pendingTrigger = null;
        if (mod == null || !Enabled) return;
        if (!TriggerPhysicallyDown(mod)) { Log.Info($"suppressed: {mod} no longer held"); return; }
        if (AnyOtherModifierPhysicallyDown(mod)) { Log.Info($"suppressed: other modifier down"); return; }
        if (AnyMouseButtonDown()) { Log.Info("suppressed: mouse button down"); return; }

        var ctx = ForegroundApp.Capture();
        if (ctx.IsSuppressed(_settings.ExcludedProcesses)) { Log.Info($"suppressed: {ctx.ProcessName} excluded/fullscreen"); return; }

        var profile = _store.Resolve(ctx.ProcessName);
        _activeProfile = profile;
        _activeTrigger = mod;
        _activeLayer = mod;
        _pinned = false;
        _overlay.SetPinned(false);
        Log.Info($"show overlay: process={ctx.ProcessName} profile={profile.Id} trigger={mod} dpi={ctx.DpiScale}");
        _overlay.ShowFor(profile, ctx, mod, LayerData(profile, mod));

        if (_settings.Trigger(mod).SuppressRelease) InjectDummyKey();
    }

    /// <summary>Windows treats a modifier tap as a "chord" if any other key
    /// was pressed while held → injecting unmapped F24 makes the release a
    /// no-op (no Start menu for Win, no menu-bar grab for Alt). Safer than
    /// swallowing the key-up, which leaves GetAsyncKeyState stuck "down" and
    /// breaks every later trigger.</summary>
    private static void InjectDummyKey()
    {
        Win32.keybd_event((byte)Win32.VK_F24, 0, 0, UIntPtr.Zero);
        Win32.keybd_event((byte)Win32.VK_F24, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private void HideAll()
    {
        _pendingTrigger = null;
        _holdTimer.Stop();
        _activeProfile = null;
        _activeTrigger = null;
        _pinned = false;
        _overlay.SetPinned(false);
        _overlay.HideOverlay();
    }

    public bool IsShowing() => _overlay.IsShowing;

    private bool GroupHeld(string mod) => _down.Any(ModSets[mod].Contains);

    private bool OtherGroupHeld(string mod)
        => _down.Any(vk => VkToMod.TryGetValue(vk, out var g) && g != mod);

    private static bool TriggerPhysicallyDown(string? mod)
        => mod != null && ModSets[mod].Any(Win32.KeyDown);

    private static bool AnyOtherModifierPhysicallyDown(string mod)
        => ModSets.Where(kv => kv.Key != mod).SelectMany(kv => kv.Value).Any(Win32.KeyDown);

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
