Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W32 {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfoW(IntPtr m, ref MONITORINFO mi);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
}
"@
Write-Output "=== Screens (WPF DIP view) ==="
foreach ($s in [Windows.Forms.Screen]::AllScreens) {
    Write-Output "$($s.DeviceName): Bounds=$($s.Bounds) WorkArea=$($s.WorkingArea) Primary=$($s.Primary)"
}
Write-Output "=== Foreground window's monitor ==="
$fg = [W32]::GetForegroundWindow()
$fwp = 0; [void][W32]::GetWindowThreadProcessId($fg, [ref]$fwp)
$fproc = (Get-Process -Id $fwp -ErrorAction SilentlyContinue).ProcessName
$mon = [W32]::MonitorFromWindow($fg, 2)
$mi = New-Object W32+MONITORINFO; $mi.cbSize = [Runtime.InteropServices.Marshal]::SizeOf($mi)
[void][W32]::GetMonitorInfoW($mon, [ref]$mi)
Write-Output "fg=$fproc monRect=$($mi.rcMonitor) workRect=$($mi.rcWork)"
Write-Output "=== KeyboardHint windows ==="
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue
foreach ($p in $kh) {
    $pid_ = $p.Id
    [W32]::EnumWindows({ param($h,$l)
        $wp = 0; [void][W32]::GetWindowThreadProcessId($h,[ref]$wp)
        if ($wp -eq $pid_) {
            $t = New-Object System.Text.StringBuilder 200; [void][W32]::GetWindowTextW($h,$t,200)
            $c = New-Object System.Text.StringBuilder 200; [void][W32]::GetClassNameW($h,$c,200)
            $r = New-Object W32+RECT; [void][W32]::GetWindowRect($h,[ref]$r)
            Write-Output "pid=$pid_ hwnd=$h cls='$($c)' title='$($t)' vis=$([W32]::IsWindowVisible($h)) rect=($($r.Left),$($r.Top),$($r.Right),$($r.Bottom))"
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
