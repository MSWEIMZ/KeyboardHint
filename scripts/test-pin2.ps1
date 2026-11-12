# Pin caption check: hold Ctrl -> release -> wait -> screenshot (should show 已钉住 caption)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KI5 {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint UP = 0x0002;
}
"@
[KI5]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 900
[KI5]::keybd_event(0xA2,0,[KI5]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1000
$b = New-Object Drawing.Bitmap([Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
$g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(0,0,0,0,$b.Size)
$b.Save("$env:LOCALAPPDATA\KeyboardHint\shot-pin2.png"); $g.Dispose(); $b.Dispose()
Write-Output "shot taken; overlay should be pinned-visible"
