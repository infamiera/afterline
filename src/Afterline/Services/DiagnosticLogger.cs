using System.Text;
using System.Reflection;

namespace Afterline.Services;

public static class DiagnosticLogger
{
    private static readonly object Gate = new();
    private const int MaximumReportErrors = 250;

    public static event EventHandler? ErrorWritten;
    public static event EventHandler? LogsChanged;

    public static void InitializeForCurrentBuild()
    {
        try
        {
            AppPaths.EnsureLocalDirectories();
            string currentBuild = GetCurrentBuildIdentity();
            lock (Gate)
            {
                string previousBuild = File.Exists(AppPaths.DiagnosticBuildMarker)
                    ? File.ReadAllText(AppPaths.DiagnosticBuildMarker).Trim()
                    : string.Empty;
                if (string.Equals(previousBuild, currentBuild, StringComparison.Ordinal)) return;

                DeleteLogFiles();
                string tempMarker = AppPaths.DiagnosticBuildMarker + $".{Environment.ProcessId}.tmp";
                try
                {
                    File.WriteAllText(tempMarker, currentBuild, new UTF8Encoding(false));
                    File.Move(tempMarker, AppPaths.DiagnosticBuildMarker, true);
                }
                finally
                {
                    if (File.Exists(tempMarker)) File.Delete(tempMarker);
                }
            }
        }
        catch
        {
            // Build isolation is best-effort and must never prevent startup. If it
            // fails, the marker remains unchanged so the next launch retries it.
        }
    }

    public static void Info(string message) => _ = Write("INFO", message);
    public static void Warn(string message) => _ = Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        if (!Write("ERROR", ex is null ? message : $"{message} | {ex}")) return;
        try { ErrorWritten?.Invoke(null, EventArgs.Empty); }
        catch { }
        RaiseLogsChanged();
    }

    public static bool ClearErrors()
    {
        try
        {
            lock (Gate)
            {
                DeleteLogFiles();
            }
            RaiseLogsChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool Write(string level, string message)
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
            return true;
        }
        catch
        {
            // Diagnostics must never crash the capture path.
            return false;
        }
    }

    public static IReadOnlyList<string> ReadRecentErrors(int maximum = 100)
    {
        maximum = Math.Clamp(maximum, 1, MaximumReportErrors);
        var recent = new Queue<string>(maximum);
        try
        {
            lock (Gate)
            {
                ReadErrorsFromFile(AppPaths.DiagnosticLog + ".1", maximum, recent);
                ReadErrorsFromFile(AppPaths.DiagnosticLog, maximum, recent);
            }
        }
        catch
        {
            // A locked, missing, or malformed log should produce an empty viewer,
            // never another application failure.
        }
        return recent.Reverse().ToArray();
    }

    public static bool HasErrors => ReadRecentErrors(1).Count > 0;

    public static string ExportErrorReportToDownloads()
    {
        string downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(downloads);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(downloads, $"Afterline-Error-Report-{stamp}.txt");
        for (int suffix = 2; File.Exists(path); suffix++)
            path = Path.Combine(downloads, $"Afterline-Error-Report-{stamp}-{suffix}.txt");

        IReadOnlyList<string> errors = ReadRecentErrors(MaximumReportErrors);
        string informational = GetCurrentBuildIdentity();
        var report = new StringBuilder();
        report.AppendLine("AFTERLINE ERROR REPORT");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        report.AppendLine($"Build: {informational}");
        report.AppendLine($"Windows: {Environment.OSVersion}");
        report.AppendLine($"Current-build errors included: {errors.Count} (maximum {MaximumReportErrors})");
        report.AppendLine("Discord: https://discord.gg/At2znTygfV");
        report.AppendLine("Afterline error-log forum: https://discord.com/channels/1388519828553203818/1541203371455942748");
        report.AppendLine();
        report.AppendLine("Only post this report in the Afterline forum linked above.");
        report.AppendLine("Common Windows user-profile paths have been redacted automatically.");
        report.AppendLine(new string('-', 78));
        if (errors.Count == 0)
        {
            report.AppendLine("No errors are currently recorded.");
        }
        else
        {
            foreach (string error in errors)
            {
                report.AppendLine(RedactPersonalPaths(error));
                report.AppendLine(new string('-', 78));
            }
        }

        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static void ReadErrorsFromFile(string path, int maximum, Queue<string> recent)
    {
        if (!File.Exists(path)) return;
        StringBuilder? currentError = null;
        foreach (string line in File.ReadLines(path))
        {
            bool newRecord = line.StartsWith("[", StringComparison.Ordinal) &&
                             line.IndexOf("] [", StringComparison.Ordinal) > 0;
            if (newRecord)
            {
                CommitError(currentError, maximum, recent);
                currentError = line.Contains("] [ERROR] ", StringComparison.Ordinal)
                    ? new StringBuilder(line)
                    : null;
            }
            else if (currentError is not null)
            {
                currentError.AppendLine();
                currentError.Append(line);
            }
        }
        CommitError(currentError, maximum, recent);
    }

    private static void CommitError(StringBuilder? error, int maximum, Queue<string> recent)
    {
        if (error is null || error.Length == 0) return;
        recent.Enqueue(error.ToString());
        while (recent.Count > maximum)
            recent.Dequeue();
    }

    private static string RedactPersonalPaths(string value)
    {
        (string Path, string Token)[] paths =
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "%DOCUMENTS%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%")
        };
        foreach ((string personalPath, string token) in paths
                     .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                     .OrderByDescending(item => item.Path.Length))
        {
            value = value.Replace(personalPath, token, StringComparison.OrdinalIgnoreCase);
        }
        return value;
    }

    private static string GetCurrentBuildIdentity()
        => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

    private static void DeleteLogFiles()
    {
        if (File.Exists(AppPaths.DiagnosticLog)) File.Delete(AppPaths.DiagnosticLog);
        string backup = AppPaths.DiagnosticLog + ".1";
        if (File.Exists(backup)) File.Delete(backup);
    }

    private static void RaiseLogsChanged()
    {
        try { LogsChanged?.Invoke(null, EventArgs.Empty); }
        catch { }
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
