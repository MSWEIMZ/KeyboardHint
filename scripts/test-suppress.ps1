# suppression test: Ctrl+Shift pressed together BEFORE overlay delay -> must NOT show
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class SI2 {
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
# Ctrl down, then Shift down 100ms later (before 400ms delay), hold 700ms total, release both
[SI2]::Key(0xA2, 0x1D, $false) | Out-Null
Start-Sleep -Milliseconds 100
[SI2]::Key(0xA0, 0x2A, $false) | Out-Null
Start-Sleep -Milliseconds 700
[SI2]::Key(0xA0, 0x2A, $true) | Out-Null
[SI2]::Key(0xA2, 0x1D, $true) | Out-Null
Write-Output "suppress-test done"
