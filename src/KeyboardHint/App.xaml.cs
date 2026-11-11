using System.IO;
using System.Windows;
using KeyboardHint.Core;
using KeyboardHint.Overlay;
using KeyboardHint.Profiles;
using KeyboardHint.Tray;

namespace KeyboardHint;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private KeyboardHook? _hook;
    private CtrlHoldTrigger? _trigger;
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

        Log.Init();
        Log.Info("=== KeyboardHint starting ===");

        var settings = AppSettings.Load();
        var store = ProfileStore.Load();
        _overlay = new OverlayWindow(settings);
        _trigger = new CtrlHoldTrigger(settings, store, _overlay);
        _hook = new KeyboardHook();
        _hook.KeyEvent += _trigger.OnKey;

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
        _tray = new TrayIcon(_trigger, store);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _trigger?.Dispose();
        _hook?.Dispose();
        _overlay?.ForceClose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Log.Info("=== KeyboardHint exited ===");
        base.OnExit(e);
    }
}
