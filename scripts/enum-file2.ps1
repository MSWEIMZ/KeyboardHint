# v2: no closure variables inside the callback; append each hit straight to file.
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class KF {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern long GetWindowLongW(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern byte GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint UP = 0x0002;
}
"@
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue | Select-Object -First 1
$targetPid = $kh.Id
$out = "$env:LOCALAPPDATA\KeyboardHint\enum-out.txt"
[IO.File]::WriteAllText($out, "target pid=$targetPid`n")
[KF]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
$tpl = @'
{ param($h,$l)
    $wp=0; [void][KF]::GetWindowThreadProcessId($h,[ref]$wp)
    if ($wp -eq TARGETPID) {
        $c = New-Object System.Text.StringBuilder 200; [void][KF]::GetClassNameW($h,$c,200)
        $t = New-Object System.Text.StringBuilder 200; [void][KF]::GetWindowTextW($h,$t,200)
        $r = New-Object KF+RECT; [void][KF]::GetWindowRect($h,[ref]$r)
        $st = [KF]::GetWindowLongW($h, -16); $ex = [KF]::GetWindowLongW($h, -20)
        $key=[uint32]0; $al=[byte]0; $fl=[uint32]0
        [void][KF]::GetLayeredWindowAttributes($h,[ref]$key,[ref]$al,[ref]$fl)
        [IO.File]::AppendAllText("ENUMOUT", ("hwnd={0} cls='{1}' title='{2}' vis={3} iconic={4} style=0x{5:X8} ex=0x{6:X8} la={7},{8} rect=({9},{10})-({11},{12})`n" -f $h,$c,$t,[KF]::IsWindowVisible($h),[KF]::IsIconic($h),$st,$ex,$al,$fl,$r.Left,$r.Top,$r.Right,$r.Bottom))
    }
    return $true
}
'@
$src = $tpl.Replace("TARGETPID", $targetPid).Replace("ENUMOUT", ($out -replace '\\','/'))
$cb2 = [ScriptBlock]::Create($src)
[KF]::EnumWindows($cb2, [IntPtr]::Zero) | Out-Null
[KF]::keybd_event(0xA2,0,[KF]::UP,[UIntPtr]::Zero)
Write-Output "done"
