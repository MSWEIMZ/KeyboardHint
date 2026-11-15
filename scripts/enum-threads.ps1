# EnumThreadWindows per thread of KeyboardHint — catches every window it owns.
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class KT {
    [DllImport("user32.dll")] public static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtrW(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint UP = 0x0002;
}
"@
[KT]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue
foreach ($p in $kh) {
    Write-Output "pid=$($p.Id) threads=$($p.Threads.Count)"
    foreach ($t in $p.Threads) {
        [KT]::EnumThreadWindows([uint32]$t.Id, { param($h,$l)
            $c = New-Object System.Text.StringBuilder 200; [void][KT]::GetClassNameW($h,$c,200)
            $r = New-Object KT+RECT; [void][KT]::GetWindowRect($h,[ref]$r)
            $ex = [KT]::GetWindowLongPtrW($h, -20)
            Write-Output ("  tid={0} hwnd={1} cls='{2}' vis={3} exStyle=0x{4} rect=({5},{6})-({7},{8})" -f $t.Id,$h,$c,[KT]::IsWindowVisible($h),$ex.ToString("X8"),$r.Left,$r.Top,$r.Right,$r.Bottom)
            return $true
        }, [IntPtr]::Zero) | Out-Null
    }
}
[KT]::keybd_event(0xA2,0,[KT]::UP,[UIntPtr]::Zero)
