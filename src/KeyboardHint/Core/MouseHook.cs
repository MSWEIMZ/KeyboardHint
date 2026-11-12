using System.Runtime.InteropServices;
using KeyboardHint.Native;

namespace KeyboardHint.Core;

/// <summary>
/// Global low-level mouse hook. Passive observer — only watches wheel and
/// button presses so Ctrl+scroll zoom / Ctrl+click never triggers the overlay.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private Win32.HookProc? _proc; // keep delegate alive for GC
    private IntPtr _hook;

    /// <summary>Any relevant mouse activity (wheel or button down).</summary>
    public event Action? Activity;

    public bool Install()
    {
        _proc = HookCallback;
        _hook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, Win32.GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is Win32.WM_MOUSEWHEEL
                or Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN or Win32.WM_MBUTTONDOWN
                or Win32.WM_XBUTTONDOWN)
            {
                try { Activity?.Invoke(); }
                catch (Exception ex) { Log.Error("mouse hook callback", ex); }
            }
        }
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
