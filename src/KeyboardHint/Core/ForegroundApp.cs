using System.Diagnostics;
using System.Windows;
using KeyboardHint.Native;

namespace KeyboardHint.Core;

public sealed class ForegroundContext
{
    public required string ProcessName { get; init; }
    /// <summary>Monitor work area in PHYSICAL pixels — divide by <see cref="DpiScale"/> for WPF DIPs.</summary>
    public required Win32.RECT WorkArea { get; init; }
    /// <summary>DPI scale of the foreground window's monitor (1.5 = 150%).</summary>
    public required double DpiScale { get; init; }
    public bool SuppressedByFullscreen { get; init; }

    public bool IsSuppressed(List<string> excluded)
        => SuppressedByFullscreen
        || excluded.Any(x => string.Equals(x, ProcessName, StringComparison.OrdinalIgnoreCase));
}

public static class ForegroundApp
{
    /// <summary>Snapshot of the current foreground window: process name,
    /// monitor work area, and whether it looks like an exclusive fullscreen app.</summary>
    public static ForegroundContext Capture()
    {
        var hwnd = Win32.GetForegroundWindow();
        var name = "";
        try
        {
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0)
                name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
        }
        catch (Exception ex) { Log.Error("get foreground process", ex); }

        var work = MonitorWorkArea(hwnd);
        uint dpi = 96;
        try { dpi = Win32.GetDpiForWindow(hwnd) & 0xFFFF; } catch { }
        return new ForegroundContext
        {
            ProcessName = name,
            WorkArea = work,
            DpiScale = dpi > 0 ? dpi / 96.0 : 1.0,
            SuppressedByFullscreen = LooksFullscreen(hwnd)
        };
    }

    public static Win32.RECT MonitorWorkArea(IntPtr hwnd)
    {
        var mi = new Win32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
        var mon = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        if (Win32.GetMonitorInfo(mon, ref mi))
            return mi.rcWork;
        // fallback: primary work area
        return new Win32.RECT { Left = 0, Top = 0, Right = (int)SystemParameters.WorkArea.Width, Bottom = (int)SystemParameters.WorkArea.Height };
    }

    /// <summary>True when the foreground window covers its whole monitor and has
    /// no caption — the signature of fullscreen games/video. Normal maximized
    /// windows keep WS_CAPTION and are not suppressed.</summary>
    private static bool LooksFullscreen(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == Win32.GetDesktopWindow() || hwnd == Win32.GetShellWindow())
            return false;
        if (!Win32.GetWindowRect(hwnd, out var r))
            return false;
        var mi = new Win32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
        var mon = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        if (!Win32.GetMonitorInfo(mon, ref mi))
            return false;
        const int t = 4;
        bool covers = r.Left <= mi.rcMonitor.Left + t && r.Top <= mi.rcMonitor.Top + t
                    && r.Right >= mi.rcMonitor.Right - t && r.Bottom >= mi.rcMonitor.Bottom - t;
        if (!covers) return false;
        return (Win32.GetWindowStyle(hwnd) & Win32.WS_CAPTION) != Win32.WS_CAPTION;
    }
}
