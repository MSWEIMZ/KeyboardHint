# Compare KeyboardHint UI thread desktop vs our own thread's desktop.
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class KD {
    [DllImport("user32.dll")] public static extern IntPtr GetThreadDesktop(uint tid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint UP = 0x0002;
}
"@
[KD]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1000
$myDesk = [KD]::GetThreadDesktop([KD]::GetCurrentThreadId())
Write-Output "my thread desktop: 0x$($myDesk.ToString('X'))"
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue
foreach ($p in $kh) {
    foreach ($t in $p.Threads) {
        $d = [KD]::GetThreadDesktop([uint32]$t.Id)
        if ($d -ne [IntPtr]::Zero) { Write-Output "kh tid=$($t.Id) desktop=0x$($d.ToString('X'))" }
    }
    # enum with a hint: also try EnumWindows again in case
    $found = 0
    [KD]::EnumWindows({ param($h,$l)
        $wp=0; [void][KD]::GetWindowThreadProcessId($h,[ref]$wp)
        if ($wp -eq $p.Id) { $script:found++; Write-Output "  found hwnd=$h" }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    Write-Output "pid=$($p.Id) top-level windows on my desktop: $found"
}
[KD]::keybd_event(0xA2,0,[KD]::UP,[UIntPtr]::Zero)
