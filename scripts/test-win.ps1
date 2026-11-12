# Hold the Win key ~1.6s (overlay should appear at 600ms), screenshot, release,
# then verify Start menu did NOT open (foreground is not StartMenuExperienceHost).
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KI {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    public const uint UP = 0x0002;
}
"@
$shot = "$env:LOCALAPPDATA\KeyboardHint\shot-win.png"
[KI]::keybd_event(0x5B, 0, 0, [UIntPtr]::Zero)   # LWin down
Start-Sleep -Milliseconds 1600
# screenshot while held
$b = New-Object Drawing.Bitmap([Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
$g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(0,0,0,0,$b.Size)
$b.Save($shot); $g.Dispose(); $b.Dispose()
[KI]::keybd_event(0x5B, 0, [KI]::UP, [UIntPtr]::Zero) # LWin up
Start-Sleep -Milliseconds 1200
$fg = [KI]::GetForegroundWindow()
$procId = 0; [void][KI]::GetWindowThreadProcessId($fg, [ref]$procId)
$proc = (Get-Process -Id $procId -ErrorAction SilentlyContinue)
Write-Output "foreground after release: $($proc.ProcessName) hwnd=$fg"
Write-Output "shot: $shot"
