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

        // Probe mode: parent spawns us as "KeyboardHint --render-probe <outfile>"
        // to verify hardware rendering actually produces pixels. Runs before
        // the single-instance mutex so it works while the parent is alive.
        if (e.Args is { Length: > 0 } && e.Args[0] == "--render-probe")
        {
            RunRenderProbe(e.Args.Length > 1 ? e.Args[1] : "");
            return;
        }

        // Editor mode: "KeyboardHint --editor" opens the shortcut editor
        // standalone (useful for a desktop shortcut / testing) — runs before
        // the single-instance mutex so it works while the tray app is alive.
        // Save still writes to the user dir; use tray "重新加载" to pick it up.
        if (e.Args is { Length: > 0 } && e.Args[0] == "--editor")
        {
            Log.Init();
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error("unhandled UI exception", args.Exception);
                args.Handled = true;
            };
            try
            {
                var editorStore = ProfileStore.Load();
                string? proc = null;
                try { proc = ForegroundApp.Capture().ProcessName; } catch { }
                new ShortcutEditorWindow(editorStore, () => { }, proc).ShowDialog();
            }
            catch (Exception ex) { Log.Error("editor mode", ex); }
            Shutdown();
            return;
        }

        // "--settings" opens the settings panel standalone (desktop-shortcut
        // friendly). Runs before the single-instance mutex like --editor.
        if (e.Args is { Length: > 0 } && e.Args[0] == "--settings")
        {
            Log.Init();
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error("unhandled UI exception", args.Exception);
                args.Handled = true;
            };
            try
            {
                var s = AppSettings.Load();
                var st = ProfileStore.Load();
                var ov = new OverlayWindow(s);
                var tr = new ModifierHoldTrigger(s, st, ov);
                new SettingsWindow(s, st, tr, ov).ShowDialog();
            }
            catch (Exception ex) { Log.Error("settings mode", ex); }
            Shutdown();
            return;
        }

        // "--menu" previews the tray flyout near screen center (debug/screenshot).
        if (e.Args is { Length: > 0 } && e.Args[0] == "--menu")
        {
            Log.Init();
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error("unhandled UI exception", args.Exception);
                args.Handled = true;
            };
            try
            {
                var s = AppSettings.Load();
                var st = ProfileStore.Load();
                var ov = new OverlayWindow(s);
                var tr = new ModifierHoldTrigger(s, st, ov);
                var ti = new TrayIcon(tr, st, s, ov);
                ti.ShowTrayMenu(new System.Windows.Point(
                    SystemParameters.PrimaryScreenWidth / 2 - 140,
                    SystemParameters.PrimaryScreenHeight / 2 - 260));
            }
            catch (Exception ex) { Log.Error("menu mode", ex); }
            return; // keep process alive so the flyout stays open for inspection
        }

        _mutex = new Mutex(true, @"Local\KeyboardHint_Once", out bool created);
        if (!created)
        {
            Shutdown();
            return;
        }
        _mutexOwned = true;

        Log.Init();
        Log.Info("=== KeyboardHint starting ===");
        // Probe: PredefinedCulturesOnly must still resolve real cultures —
        // InvariantGlobalization throws here and breaks every WPF binding.
        Log.Info($"culture: {System.Globalization.CultureInfo.CurrentCulture.Name} en-US={System.Globalization.CultureInfo.GetCultureInfo("en-US").Name}");

        // A tray app should degrade to a log line, not die in a modal dialog —
        // any unexpected UI exception is logged and the app keeps running.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("unhandled UI exception", args.Exception);
            args.Handled = true;
        };

        var settings = AppSettings.Load();
        // renderMode=hardware is opt-in, but a broken GPU/D3D pipeline makes it
        // worse than useless (every window renders blank). Probe it for real:
        // spawn ourselves as a tiny red window and read the pixels back; blank
        // → persist "software" so we never probe again. Probing costs ~0.5s
        // and only runs for users who explicitly opted into hardware.
        bool hardware = string.Equals(settings.RenderMode, "hardware", StringComparison.OrdinalIgnoreCase);
        if (hardware)
        {
            var verdict = ProbeHardwareRender();
            Log.Info($"hardware render probe: {verdict}");
            if (verdict != "ok")
            {
                settings.RenderMode = "software";
                settings.Save();
                hardware = false;
            }
        }
        if (!hardware)
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
        _tray = new TrayIcon(_trigger, store, settings, _overlay);
        _overlay.Prewarm(); // 1px invisible show warms hwnd+fonts — first trigger is instant

        // Tray apps sit idle most of their life: periodically compact the
        // managed heap so the process doesn't hold peak-time memory forever.
        // NOTE: no EmptyWorkingSet here — evicting code/render pages makes the
        // next Ctrl press pay hard page faults (perceived as lag).
        var memTrim = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        memTrim.Tick += (_, _) =>
        {
            if (_trigger.IsShowing()) return;
            GC.Collect(2, GCCollectionMode.Optimized, false);
        };
        memTrim.Start();
    }

    /// <summary>Spawn a copy of ourselves in probe mode; it shows a small red
    /// window (default/hardware pipeline — ProcessRenderMode is only set in
    /// the parent when needed) and writes "ok"/"blank" to a result file.</summary>
    private static string ProbeHardwareRender()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"kh-rp-{Guid.NewGuid():N}.txt");
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return "noexe";
            var psi = new System.Diagnostics.ProcessStartInfo(exe, $"--render-probe \"{outFile}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return "nostart";
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return "timeout"; }
            return File.Exists(outFile) ? File.ReadAllText(outFile).Trim() : "nofile";
        }
        catch (Exception ex) { return $"error {ex.GetType().Name}"; }
        finally { try { File.Delete(outFile); } catch { } }
    }

    /// <summary>Probe instance body: paint a small opaque red window on screen,
    /// give DWM a moment to composite, then read the pixels back through GDI
    /// (the exact path that was blank when the GPU pipeline was broken).</summary>
    private void RunRenderProbe(string outFile)
    {
        string verdict = "blank";
        try
        {
            var w = new Window
            {
                Width = 8,
                Height = 8,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.Red,
                Left = SystemParameters.WorkArea.Right - 16,
                Top = SystemParameters.WorkArea.Bottom - 16
            };
            w.Show();
            for (int i = 0; i < 2 && verdict == "blank"; i++)
            {
                // Pump the dispatcher so the render thread gets its frame out.
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                System.Threading.Thread.Sleep(400);

                var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (hwnd == IntPtr.Zero || !Win32.GetWindowRect(hwnd, out var r)) continue;
                int wpx = r.Right - r.Left, hpx = r.Bottom - r.Top;
                if (wpx <= 0 || hpx <= 0) continue;
                using var bmp = new System.Drawing.Bitmap(wpx, hpx);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    g.CopyFromScreen(r.Left, r.Top, 0, 0, new System.Drawing.Size(wpx, hpx));
                int red = 0;
                for (int x = 0; x < wpx; x++)
                    for (int y = 0; y < hpx; y++)
                    {
                        var c = bmp.GetPixel(x, y);
                        if (c.R > 200 && c.G < 90 && c.B < 90) red++;
                    }
                if (red * 4 >= wpx * hpx) verdict = "ok"; // ≥25% red = real pixels
            }
            w.Close();
        }
        catch { verdict = "error"; }
        try { if (outFile.Length > 0) File.WriteAllText(outFile, verdict); } catch { }
        Shutdown();
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
