# Hold Ctrl 1.5s, take full-screen screenshot mid-hold, then check window rect.
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KX {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint UP = 0x0002;
}
"@
[KX]::keybd_event(0xA2,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 1300
# screenshot of the real screen size
$vs = [Windows.Forms.SystemInformation]::VirtualScreen
$b = New-Object Drawing.Bitmap($vs.Width, $vs.Height)
$g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen($vs.Left,$vs.Top,0,0,$b.Size)
$b.Save("$env:LOCALAPPDATA\KeyboardHint\diag-shot.png"); $g.Dispose(); $b.Dispose()
# enumerate KeyboardHint windows
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue
Write-Output "kh procs: $(($kh | ForEach-Object Id) -join ',')"
foreach ($p in $kh) {
    $pid_ = $p.Id
    [KX]::EnumWindows({ param($h,$l)
        $wp = 0; [void][KX]::GetWindowThreadProcessId($h,[ref]$wp)
        if ($wp -eq $pid_) {
            $c = New-Object System.Text.StringBuilder 100; [void][KX]::GetClassNameW($h,$c,100)
            $r = New-Object KX+RECT; [void][KX]::GetWindowRect($h,[ref]$r)
            Write-Output "pid=$pid_ hwnd=$h cls='$($c)' vis=$([KX]::IsWindowVisible($h)) iconic=$([KX]::IsIconic($h)) rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom))"
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
[KX]::keybd_event(0xA2,0,[KX]::UP,[UIntPtr]::Zero)
Write-Output "shot saved"
