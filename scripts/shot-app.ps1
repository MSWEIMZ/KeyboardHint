# focus an app window then screenshot the overlay during Ctrl hold
param([string]$Proc = "wps", [string]$Name = "shot-app.png", [switch]$Shift)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class FW2 {
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
public class SI6 {
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
$ps = Get-Process -Name $Proc -ErrorAction SilentlyContinue
$h = [IntPtr]::Zero
foreach ($p in $ps) {
  if ($p.MainWindowHandle -ne 0) { $h = $p.MainWindowHandle; break }
  $h = [FW2]::FindMain($p.Id)
  if ($h -ne [IntPtr]::Zero) { break }
}
if ($h -eq [IntPtr]::Zero) { Write-Output "${Proc}: no window"; exit 1 }
[FW2]::ShowWindow($h, 9) | Out-Null
[FW2]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 600
[SI6]::Key(0xA2, 0x1D, $false) | Out-Null
Start-Sleep -Milliseconds 1400
if ($Shift) { [SI6]::Key(0xA0, 0x2A, $false) | Out-Null; Start-Sleep -Milliseconds 700 }
$bmp = New-Object Drawing.Bitmap([Windows.Forms.SystemInformation]::VirtualScreen.Width, [Windows.Forms.SystemInformation]::VirtualScreen.Height)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$out = Join-Path $env:LOCALAPPDATA "KeyboardHint\$Name"
$bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
if ($Shift) { [SI6]::Key(0xA0, 0x2A, $true) | Out-Null }
[SI6]::Key(0xA2, 0x1D, $true) | Out-Null
Write-Output "saved: $out"
