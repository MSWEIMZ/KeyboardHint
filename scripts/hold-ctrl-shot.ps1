# 按住 Ctrl → 截屏（此时 overlay 应可见）→ 松开。验证提示键盘位置用。
Add-Type @'
using System.Runtime.InteropServices;
public class KeyInject {
  [DllImport("user32.dll")]
  public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);
}
'@
Add-Type -AssemblyName System.Drawing, System.Windows.Forms

[KeyInject]::keybd_event(0xA2, 0, 0, [System.UIntPtr]::Zero)   # LCtrl down
Start-Sleep -Milliseconds 2500

$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
$dir = Join-Path $env:LOCALAPPDATA 'KeyboardHint'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$out = Join-Path $dir 'shot.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved: $out"

Start-Sleep -Milliseconds 500
[KeyInject]::keybd_event(0xA2, 0, 2, [System.UIntPtr]::Zero)   # LCtrl up
