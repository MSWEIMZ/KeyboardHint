# Isolated: hold Win -> wait for overlay -> press Shift -> check layer switch.
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KI3 {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint UP = 0x0002;
}
"@
[KI3]::keybd_event(0x5B,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
[KI3]::keybd_event(0xA0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 600
[KI3]::keybd_event(0xA0,0,[KI3]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 300
[KI3]::keybd_event(0x5B,0,[KI3]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 500
Write-Output "done"
