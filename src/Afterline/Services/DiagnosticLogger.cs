using System.Text;

namespace Afterline.Services;

public static class DiagnosticLogger
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message} | {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureLocalDirectories();
            lock (Gate)
            {
                RotateIfNeeded();
                File.AppendAllText(AppPaths.DiagnosticLog,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}",
                    new UTF8Encoding(false));
            }
        }
        catch
        {
            // Diagnostics must never crash the capture path.
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(AppPaths.DiagnosticLog);
        if (!file.Exists || file.Length < 2 * 1024 * 1024) return;

        string backup = AppPaths.DiagnosticLog + ".1";
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(AppPaths.DiagnosticLog, backup);
    }
}
