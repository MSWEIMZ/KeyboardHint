# 模拟"按住 Ctrl 1 秒"用于联调触发提示键盘（注入真实输入事件）
Add-Type @'
using System.Runtime.InteropServices;
public class KeyInject {
  [DllImport("user32.dll")]
  public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);
}
'@
$ms = if ($args.Count -gt 0) { [int]$args[0] } else { 1000 }
[KeyInject]::keybd_event(0xA2, 0, 0, [System.UIntPtr]::Zero)   # LCtrl down
Start-Sleep -Milliseconds $ms
[KeyInject]::keybd_event(0xA2, 0, 2, [System.UIntPtr]::Zero)   # LCtrl up
