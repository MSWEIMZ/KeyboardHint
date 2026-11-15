# Fully self-contained C# enumeration — results written to file by C# code itself.
Add-Type @"
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class WinDump {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowLongW(IntPtr h, int i);
    [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint ga);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    public static void Dump(int targetPid, string path) {
        var sb = new StringBuilder();
        sb.AppendLine("target pid=" + targetPid);
        EnumWindows((h, l) => {
            uint wp; GetWindowThreadProcessId(h, out wp);
            if (wp != (uint)targetPid) return true;
            var c = new StringBuilder(200); GetClassNameW(h, c, 200);
            var t = new StringBuilder(200); GetWindowTextW(h, t, 200);
            var r = new RECT(); GetWindowRect(h, out r);
            int st = GetWindowLongW(h, -16); int ex = GetWindowLongW(h, -20);
            uint key=0, fl=0; byte al=0;
            GetLayeredWindowAttributes(h, out key, out al, out fl);
            sb.AppendLine(string.Format(
                "hwnd={0} cls='{1}' title='{2}' vis={3} iconic={4} style=0x{5:X8} ex=0x{6:X8} la={7},{8} rect=({9},{10})-({11},{12})",
                h, c, t, IsWindowVisible(h), IsIconic(h), st, ex, al, fl, r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        File.WriteAllText(path, sb.ToString());
    }
}
"@
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue | Select-Object -First 1
$out = "$env:LOCALAPPDATA\KeyboardHint\enum-out.txt"
# hold ctrl while we dump
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::Flush()
Add-Type @"
using System; using System.Runtime.InteropServices;
public class KI { [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra); }
"@
[KI]::keybd_event(0xA2,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 1100
[WinDump]::Dump($kh.Id, $out)
[KI]::keybd_event(0xA2,0,2,[UIntPtr]::Zero)
Write-Output "dumped for pid=$($kh.Id) mainwnd=$($kh.MainWindowHandle) title='$($kh.MainWindowTitle)'"
