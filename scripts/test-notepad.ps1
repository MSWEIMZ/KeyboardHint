# 端到端测试：开记事本 → 枚举其窗口并聚焦 → 按住Ctrl → 截图 → 松开
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class T3 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public struct RECT { public int Left, Top, Right, Bottom; }
  public static IntPtr FindWin(uint wantPid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint p = 0; GetWindowThreadProcessId(h, out p);
      if (p == wantPid && IsWindowVisible(h)) {
        var t = new StringBuilder(256); GetWindowText(h, t, 256);
        RECT r; GetWindowRect(h, out r);
        if (t.Length > 0 && r.Right - r.Left > 200) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
'@
param([int]$HoldMs = 2500)

$p = Start-Process notepad -PassThru
Start-Sleep -Milliseconds 1500
$hwnd = [T3]::FindWin([uint32]$p.Id)
Write-Output "notepad pid=$($p.Id) hwnd=$hwnd"
if ($hwnd -ne [IntPtr]::Zero) {
  [T3]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
  [T3]::SetForegroundWindow($hwnd) | Out-Null
  Start-Sleep -Milliseconds 600
}
[T3]::keybd_event(0xA2, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds ($HoldMs - 300)
$bmp = New-Object Drawing.Bitmap([Windows.Forms.SystemInformation]::VirtualScreen.Width, [Windows.Forms.SystemInformation]::VirtualScreen.Height)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$out = "$env:LOCALAPPDATA\KeyboardHint\shot.png"
$bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved: $out"
Start-Sleep -Milliseconds 300
[T3]::keybd_event(0xA2, 0, 2, [UIntPtr]::Zero)
