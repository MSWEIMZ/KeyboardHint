param([string]$Name = "shot-shift.png", [switch]$Alt)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class SI {
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
[SI]::Key(0xA2, 0x1D, $false) | Out-Null
Start-Sleep -Milliseconds 1500
if ($Alt) { [SI]::Key(0xA4, 0x38, $false) | Out-Null } else { [SI]::Key(0xA0, 0x2A, $false) | Out-Null }
Start-Sleep -Milliseconds 1200
$bmp = New-Object Drawing.Bitmap([Windows.Forms.SystemInformation]::VirtualScreen.Width, [Windows.Forms.SystemInformation]::VirtualScreen.Height)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$out = Join-Path $env:LOCALAPPDATA "KeyboardHint\$Name"
$bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved: $out"
if ($Alt) { [SI]::Key(0xA4, 0x38, $true) | Out-Null } else { [SI]::Key(0xA0, 0x2A, $true) | Out-Null }
Start-Sleep -Milliseconds 300
[SI]::Key(0xA2, 0x1D, $true) | Out-Null
