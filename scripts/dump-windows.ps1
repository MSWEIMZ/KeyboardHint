# 枚举 KeyboardHint.exe 所有顶层窗口：句柄/类名/标题/可见性/矩形/exstyle
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class WinEnum {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  public struct RECT { public int L, T, R, B; }
}
'@
$targetPid = (Get-Process KeyboardHint -ErrorAction SilentlyContinue).Id
if (-not $targetPid) { Write-Output "KeyboardHint.exe not running"; exit }
Write-Output "KeyboardHint pid=$targetPid"
$script:results = @()
$cb = [WinEnum+EnumProc]{
  param($h, $l)
  $p = 0; [WinEnum]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
  if ($p -eq $targetPid) {
    $cls = New-Object Text.StringBuilder 256; [WinEnum]::GetClassName($h, $cls, 256) | Out-Null
    $ttl = New-Object Text.StringBuilder 256; [WinEnum]::GetWindowText($h, $ttl, 256) | Out-Null
    $r = New-Object WinEnum+RECT; [WinEnum]::GetWindowRect($h, [ref]$r) | Out-Null
    $vis = [WinEnum]::IsWindowVisible($h)
    $ex = [WinEnum]::GetWindowLong($h, -20)   # GWL_EXSTYLE
    $st = [WinEnum]::GetWindowLong($h, -16)   # GWL_STYLE
    $script:results += "hwnd=$h vis=$vis rect=($($r.L),$($r.T),$($r.R),$($r.B)) ex=0x$($ex.ToString('X8')) style=0x$($st.ToString('X8')) class='$($cls)' title='$($ttl)'"
  }
  return $true
}
[WinEnum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$script:results | ForEach-Object { Write-Output $_ }
