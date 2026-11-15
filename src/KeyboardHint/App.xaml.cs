using System.IO;
using System.Windows;
using System.Windows.Threading;
using KeyboardHint.Core;
using KeyboardHint.Native;
using KeyboardHint.Overlay;
using KeyboardHint.Profiles;
using KeyboardHint.Tray;

namespace KeyboardHint;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _mutexOwned;
    private KeyboardHook? _hook;
    private MouseHook? _mouseHook;
    private ModifierHoldTrigger? _trigger;
    private OverlayWindow? _overlay;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Local\KeyboardHint_Once", out bool created);
        if (!created)
        {
            Shutdown();
            return;
        }
        _mutexOwned = true;

        Log.Init();
        Log.Info("=== KeyboardHint starting ===");

        var settings = AppSettings.Load();
        // Broken GPU pipelines (driver/D3D state) leave every WPF window
        // composited blank; software rendering is deterministic for a small
        // overlay. Must be set before the first window is shown.
        if (!string.Equals(settings.RenderMode, "hardware", StringComparison.OrdinalIgnoreCase))
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var store = ProfileStore.Load();
        _overlay = new OverlayWindow(settings);
        _trigger = new ModifierHoldTrigger(settings, store, _overlay);
        _hook = new KeyboardHook();
        _hook.KeyEvent += _trigger.OnKey;
        _mouseHook = new MouseHook();
        _mouseHook.Activity += _trigger.OnMouseActivity;
        _mouseHook.Install(); // passive; failure only loses wheel/click suppression

        if (!_hook.Install())
        {
            Log.Error("SetWindowsHookEx(WH_KEYBOARD_LL) failed");
            System.Windows.MessageBox.Show("全局键盘监听启动失败，程序将退出。\n（可能被安全软件拦截）", "键盘小助手",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        Log.Info("keyboard hook installed");

        _trigger.Start();
        _tray = new TrayIcon(_trigger, store, settings);
        _overlay.Prewarm(); // 1px invisible show warms hwnd+fonts — first trigger is instant

        // Tray apps sit idle most of their life: periodically hand managed
        // garbage and trimmed pages back to the OS instead of holding a
        // working set sized for the busiest moment.
        var memTrim = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        memTrim.Tick += (_, _) =>
        {
            if (_trigger.IsShowing()) return;
            GC.Collect(2, GCCollectionMode.Optimized, false);
            Win32.EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        };
        memTrim.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _trigger?.Dispose();
        _hook?.Dispose();
        _mouseHook?.Dispose();
        _overlay?.ForceClose();
        if (_mutexOwned) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Log.Info("=== KeyboardHint exited ===");
        base.OnExit(e);
    }
}
