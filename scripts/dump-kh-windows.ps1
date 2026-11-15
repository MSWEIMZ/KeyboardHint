# Dump KeyboardHint's windows using the self-contained C# path.
Add-Type @"
using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
public static class WD2 {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowLongW(IntPtr h, int i);
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
            sb.AppendLine(string.Format("hwnd={0} cls='{1}' title='{2}' vis={3} style=0x{4:X8} ex=0x{5:X8} rect=({6},{7})-({8},{9})",
                h, c, t, IsWindowVisible(h), st, ex, r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        File.WriteAllText(path, sb.ToString());
    }
}
"@
$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue | Select-Object -First 1
[WD2]::Dump($kh.Id, "$env:LOCALAPPDATA\KeyboardHint\enum2.txt")
Write-Output "dumped pid=$($kh.Id)"
