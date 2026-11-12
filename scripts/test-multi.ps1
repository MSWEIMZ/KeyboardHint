# Multi-trigger test: Win+Shift chord, then Alt hold, then Ctrl regression.
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KI2 {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint UP = 0x0002;
}
"@
function Shot($name) {
    $b = New-Object Drawing.Bitmap([Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
    $g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(0,0,0,0,$b.Size)
    $b.Save("$env:LOCALAPPDATA\KeyboardHint\$name"); $g.Dispose(); $b.Dispose()
}
# 1) Win held -> add Shift -> win+shift layer
[KI2]::keybd_event(0x5B,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1000
[KI2]::keybd_event(0xA0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 500
Shot "shot-winshift.png"
[KI2]::keybd_event(0xA0,0,[KI2]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 300
[KI2]::keybd_event(0x5B,0,[KI2]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 800
# 2) Alt hold -> alt layer
[KI2]::keybd_event(0xA4,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 700
Shot "shot-alt.png"
[KI2]::keybd_event(0xA4,0,[KI2]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 800
# 3) Ctrl regression
[KI2]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 700
Shot "shot-ctrl2.png"
[KI2]::keybd_event(0xA2,0,[KI2]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 500
Write-Output "done"
