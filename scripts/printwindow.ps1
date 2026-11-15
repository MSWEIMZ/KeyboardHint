# Find KeyboardHint's visible overlay hwnd, PrintWindow it to a PNG.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
public static class PW {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int cx, int cy, uint fl);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT rect, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    static IntPtr found = IntPtr.Zero;
    public static IntPtr FindVisible(int targetPid) {
        found = IntPtr.Zero;
        EnumWindows((h, l) => {
            uint wp; GetWindowThreadProcessId(h, out wp);
            if (wp == (uint)targetPid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static RECT Rect(IntPtr h) { var r = new RECT(); GetWindowRect(h, out r); return r; }
    public static void Capture(IntPtr h, string path) {
        var r = Rect(h);
        int w = r.Right - r.Left, ht = r.Bottom - r.Top;
        using (var bmp = new Bitmap(w, ht, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp)) {
            IntPtr hdc = g.GetHdc();
            PrintWindow(h, hdc, 2); // PW_RENDERFULLCONTENT
            g.ReleaseHdc(hdc);
            bmp.Save(path, ImageFormat.Png);
        }
    }
    public static string Probe(IntPtr h) {
        var sb = new StringBuilder();
        // Try to force-update: nudge position by 0 and force repaint
        ShowWindow(h, 5);
        SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001|0x0002|0x0040);
        return "nudged";
    }
}
"@ -ReferencedAssemblies System.Drawing
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue | Select-Object -First 1
[PW]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1100
$h = [PW]::FindVisible($kh.Id)
if ($h -eq [IntPtr]::Zero) { Write-Output "no visible window"; [PW]::keybd_event(0xA2,0,2,[UIntPtr]::Zero); exit }
$r = [PW]::Rect($h)
Write-Output "hwnd=$h rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom))"
# capture BEFORE nudge
[PW]::Capture($h, "$env:LOCALAPPDATA\KeyboardHint\pw-before.png")
[PW]::Probe($h) | Out-Null
Start-Sleep -Milliseconds 300
[PW]::Capture($h, "$env:LOCALAPPDATA\KeyboardHint\pw-after.png")
[PW]::keybd_event(0xA2,0,2,[UIntPtr]::Zero)
Write-Output "saved pw-before/pw-after"
