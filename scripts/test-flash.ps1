# flash test: Ctrl hold -> overlay -> tap C -> screenshot during 350ms flash window
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class SI4 {
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
[SI4]::Key(0xA2, 0x1D, $false) | Out-Null   # Ctrl down
Start-Sleep -Milliseconds 1500
[SI4]::Key(0x43, 0x2E, $false) | Out-Null  # C down (vk 0x43, scan 0x2E)
Start-Sleep -Milliseconds 120
$bmp = New-Object Drawing.Bitmap([Windows.Forms.SystemInformation]::VirtualScreen.Width, [Windows.Forms.SystemInformation]::VirtualScreen.Height)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$out = Join-Path $env:LOCALAPPDATA "KeyboardHint\shot-flash.png"
$bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
[SI4]::Key(0x43, 0x2E, $true) | Out-Null   # C up
Start-Sleep -Milliseconds 300
[SI4]::Key(0xA2, 0x1D, $true) | Out-Null   # Ctrl up
Write-Output "flash test done"
