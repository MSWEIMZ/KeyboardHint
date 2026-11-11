using System.IO;

namespace KeyboardHint.Core;

public static class Log
{
    private static string _path = "";
    private static readonly object _lock = new();

    public static string UserDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardHint");

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(UserDir);
            _path = Path.Combine(UserDir, "keyboardhint.log");
            var fi = new FileInfo(_path);
            if (fi.Exists && fi.Length > 512 * 1024)
                File.WriteAllText(_path, "");
        }
        catch { /* never let logging kill the app */ }
    }

    public static void Info(string msg) => Write("INFO", msg);

    public static void Error(string msg, Exception? ex = null)
        => Write("ERROR", ex == null ? msg : $"{msg} :: {ex}");

    private static void Write(string level, string msg)
    {
        if (_path.Length == 0) return;
        try
        {
            lock (_lock)
                File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}\n");
        }
        catch { }
    }
}
