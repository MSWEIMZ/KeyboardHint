# Pin mode: hold Ctrl (overlay shows), release (should STAY), tap Ctrl (should hide).
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KI4 {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint UP = 0x0002;
}
"@
function Shot($name) {
    $b = New-Object Drawing.Bitmap([Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
    $g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(0,0,0,0,$b.Size)
    $b.Save("$env:LOCALAPPDATA\KeyboardHint\$name"); $g.Dispose(); $b.Dispose()
}
[KI4]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 800
[KI4]::keybd_event(0xA2,0,[KI4]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 600
Shot "shot-pinned.png"   # overlay should still be visible here
[KI4]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 120
[KI4]::keybd_event(0xA2,0,[KI4]::UP,[UIntPtr]::Zero); Start-Sleep -Milliseconds 600
Shot "shot-unpinned.png" # overlay should be gone now
Write-Output "done"
