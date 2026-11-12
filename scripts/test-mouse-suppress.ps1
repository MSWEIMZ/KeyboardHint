# mouse suppression: hold Ctrl, press LMB during the delay -> must NOT show
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class SI3 {
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] p, int cb);
  public static uint Key(ushort vk, ushort scan, bool up) {
    var i = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = up ? 2u : 0u } } };
    return SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());
  }
  public static uint Mouse(uint flags) {
    var i = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags } } };
    return SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());
  }
}
'@
$r1 = [SI3]::Key(0xA2, 0x1D, $false)           # Ctrl down
Start-Sleep -Milliseconds 200
$r2 = [SI3]::Mouse(0x0002)                     # LMB down  (before 400ms delay elapses)
Start-Sleep -Milliseconds 500
$r3 = [SI3]::Mouse(0x0004)                     # LMB up
Start-Sleep -Milliseconds 400
$r4 = [SI3]::Key(0xA2, 0x1D, $true)            # Ctrl up
Write-Output "mouse-suppress done sent=$r1,$r2,$r3,$r4"
