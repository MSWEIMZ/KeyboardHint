# profile match test: focus each app's window, inject Ctrl hold, log shows resolved profile
param([string[]]$Procs = @("wps","wpp","wpspdf","Code","chrome","WindowsTerminal","Weixin"))
$Procs = $Procs | ForEach-Object { $_ -split ',' } | Where-Object { $_ }
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class FW {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  public static IntPtr FindMain(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
    return found;
  }
}
'@
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class SI5 {
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] p, int cb);
  public static uint Key(ushort vk, ushort scan, bool up) {
    var i = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = up ? 2u : 0u } } };
    return SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());
  }
}
'@
foreach ($name in $Procs) {
  $ps = Get-Process -Name $name -ErrorAction SilentlyContinue
  if (-not $ps) { Write-Output "$name : NOT RUNNING"; continue }
  $h = [IntPtr]::Zero
  foreach ($p in $ps) {
    if ($p.MainWindowHandle -ne 0) { $h = $p.MainWindowHandle; break }
    $h = [FW]::FindMain($p.Id)
    if ($h -ne [IntPtr]::Zero) { break }
  }
  if ($h -eq [IntPtr]::Zero) { Write-Output "$name : no visible window"; continue }
  [FW]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
  [FW]::SetForegroundWindow($h) | Out-Null
  Start-Sleep -Milliseconds 600
  [SI5]::Key(0xA2, 0x1D, $false) | Out-Null
  Start-Sleep -Milliseconds 1100
  [SI5]::Key(0xA2, 0x1D, $true) | Out-Null
  Start-Sleep -Milliseconds 300
  Write-Output "$name : injected"
}
Write-Output "done"
