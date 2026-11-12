using System.Runtime.InteropServices;
using KeyboardHint.Native;

namespace KeyboardHint.Core;

/// <summary>
/// Global low-level keyboard hook. Events are forwarded to CallNextHookEx and
/// nothing is recorded — the ONLY swallowed event is a trigger key's key-up
/// after the overlay was shown (e.g. Win, so Start menu doesn't open).
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private Win32.HookProc? _proc; // keep delegate alive for GC
    private IntPtr _hook;

    /// <summary>(vkCode, isDown) → true = swallow this event.</summary>
    public event Func<int, bool, bool>? KeyEvent;

    public bool Install()
    {
        _proc = HookCallback;
        _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool down = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
            bool up = msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;
            if (down || up)
            {
                try
                {
                    var kbd = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                    if (KeyEvent?.Invoke((int)kbd.vkCode, down) == true)
                        return (IntPtr)1;
                }
                catch (Exception ex)
                {
                    Log.Error("hook callback", ex);
                }
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
