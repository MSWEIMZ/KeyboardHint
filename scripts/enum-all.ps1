# Enumerate ALL top-level windows, flag HwndWrapper (WPF) ones + KeyboardHint-owned.
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class KZ {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint UP = 0x0002;
}
"@
[KZ]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
$total = 0; $wpf = 0
[KZ]::EnumWindows({ param($h,$l)
    $script:total++
    $c = New-Object System.Text.StringBuilder 200; [void][KZ]::GetClassNameW($h,$c,200)
    if ($c.ToString() -match "HwndWrapper|KeyboardHint") {
        $script:wpf++
        $wp = 0; [void][KZ]::GetWindowThreadProcessId($h,[ref]$wp)
        $pn = (Get-Process -Id $wp -ErrorAction SilentlyContinue).ProcessName
        $t = New-Object System.Text.StringBuilder 200; [void][KZ]::GetWindowTextW($h,$t,200)
        $r = New-Object KZ+RECT; [void][KZ]::GetWindowRect($h,[ref]$r)
        Write-Output "wpf hwnd=$h pid=$wp proc=$pn cls='$($c)' title='$($t)' vis=$([KZ]::IsWindowVisible($h)) rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom))"
    }
    return $true
}, [IntPtr]::Zero) | Out-Null
Write-Output "total top-level=$total wpf-ish=$wpf"
[KZ]::keybd_event(0xA2,0,[KZ]::UP,[UIntPtr]::Zero)
