# layer-switch test via SendInput with correct x64 INPUT union (40 bytes)
Add-Type -AssemblyName System.Drawing
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
function Shot($name) {
  $bmp = New-Object Drawing.Bitmap([Windows.Forms.SystemInformation]::VirtualScreen.Width, [Windows.Forms.SystemInformation]::VirtualScreen.Height)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
  $out = Join-Path $env:LOCALAPPDATA "KeyboardHint\$name"
  $bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Output "saved: $out"
}
$r1 = [SI]::Key(0xA2, 0x1D, $false)   # LCtrl down
Start-Sleep -Milliseconds 1500
$r2 = [SI]::Key(0xA0, 0x2A, $false)   # LShift down -> ctrl+shift
Start-Sleep -Milliseconds 900
Shot "shot-shift.png"
$r3 = [SI]::Key(0xA0, 0x2A, $true)    # LShift up -> ctrl
Start-Sleep -Milliseconds 700
Shot "shot-ctrl.png"
$r4 = [SI]::Key(0xA2, 0x1D, $true)    # LCtrl up -> hide
Write-Output "sent: $r1 $r2 $r3 $r4 (expect 1 1 1 1)"
