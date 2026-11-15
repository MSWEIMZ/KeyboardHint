# Enum windows of KeyboardHint, write details to a file (Write-Output inside
# marshaled delegates is unreliable).
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class KE {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern long GetWindowLongW(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern byte GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint UP = 0x0002;
}
"@
$out = "$env:LOCALAPPDATA\KeyboardHint\enum-out.txt"
$lines = New-Object System.Collections.Generic.List[string]
[KE]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue
$pids = @{}
foreach ($p in $kh) { $pids[$p.Id] = $true; $lines.Add("pid=$($p.Id)") }
[KE]::EnumWindows({ param($h,$l)
    $wp=0; [void][KE]::GetWindowThreadProcessId($h,[ref]$wp)
    if ($pids.ContainsKey($wp)) {
        $c = New-Object System.Text.StringBuilder 200; [void][KE]::GetClassNameW($h,$c,200)
        $r = New-Object KE+RECT; [void][KE]::GetWindowRect($h,[ref]$r)
        $st = [KE]::GetWindowLongW($h, -16); $ex = [KE]::GetWindowLongW($h, -20)
        $key=[uint32]0; $al=[byte]0; $fl=[uint32]0
        [void][KE]::GetLayeredWindowAttributes($h,[ref]$key,[ref]$al,[ref]$fl)
        $lines.Add(("hwnd={0} cls='{1}' vis={2} iconic={3} style=0x{4:X8} ex=0x{5:X8} layered(alpha={6},flags={7}) rect=({8},{9})-({10},{11})" -f $h,$c,[KE]::IsWindowVisible($h),[KE]::IsIconic($h),$st,$ex,$al,$fl,$r.Left,$r.Top,$r.Right,$r.Bottom))
    }
    return $true
}, [IntPtr]::Zero) | Out-Null
[KE]::keybd_event(0xA2,0,[KE]::UP,[UIntPtr]::Zero)
[IO.File]::WriteAllLines($out, $lines)
Write-Output "wrote $out"
