using System.Text;
using System.Reflection;

namespace Afterline.Services;

public static class DiagnosticLogger
{
    private static readonly object Gate = new();
    private const int MaximumReportErrors = 250;
    private const int MaximumDiagnosticTimelineLines = 400;

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
                SnapshotCurrentSessionLogs();
                string previousBuild = File.Exists(AppPaths.DiagnosticBuildMarker)
                    ? File.ReadAllText(AppPaths.DiagnosticBuildMarker).Trim()
                    : string.Empty;
                if (string.Equals(previousBuild, currentBuild, StringComparison.Ordinal)) return;

                DeleteCurrentLogFiles();
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
                DeleteCurrentLogFiles();
                DeletePreviousSessionLogFiles();
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
        => ReadErrorsFromPaths(
            maximum,
            AppPaths.DiagnosticLog + ".1",
            AppPaths.DiagnosticLog);

    public static IReadOnlyList<string> ReadPreviousSessionErrors(int maximum = 100)
        => ReadErrorsFromPaths(
            maximum,
            AppPaths.DiagnosticPreviousSessionBackup,
            AppPaths.DiagnosticPreviousSessionLog);

    private static IReadOnlyList<string> ReadErrorsFromPaths(int maximum, params string[] paths)
    {
        maximum = Math.Clamp(maximum, 1, MaximumReportErrors);
        var recent = new Queue<string>(maximum);
        try
        {
            lock (Gate)
            {
                foreach (string path in paths)
                    ReadErrorsFromFile(path, maximum, recent);
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
    public static bool HasPreviousSessionErrors => ReadPreviousSessionErrors(1).Count > 0;

    internal static void RunPreviousSessionSnapshotSmokeTest(string testRoot)
    {
        string folder = Path.Combine(testRoot, "diagnostic-snapshot");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "current.log");
        string snapshot = Path.Combine(folder, "previous.log");
        const string expected = "[2026-08-29 12:00:00.000] [ERROR] previous-session freeze smoke";
        try
        {
            File.WriteAllText(source, expected + Environment.NewLine, new UTF8Encoding(false));
            lock (Gate)
                SnapshotLogFile(source, snapshot);
            IReadOnlyList<string> restored = ReadErrorsFromPaths(10, snapshot);
            IReadOnlyList<string> timeline = ReadRecentDiagnosticLines(10, snapshot);
            if (restored.Count != 1 ||
                !restored[0].Contains("previous-session freeze smoke", StringComparison.Ordinal) ||
                timeline.Count != 1 ||
                !timeline[0].Contains("previous-session freeze smoke", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The previous-session diagnostic snapshot could not be restored.");
            }
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
            catch { }
        }
    }

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
        HashSet<string> currentRecords = errors.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> previousErrors = ReadPreviousSessionErrors(MaximumReportErrors)
            .Where(error => !currentRecords.Contains(error))
            .ToArray();
        IReadOnlyList<string> currentTimeline = ReadRecentDiagnosticLines(
            MaximumDiagnosticTimelineLines,
            AppPaths.DiagnosticLog + ".1",
            AppPaths.DiagnosticLog);
        HashSet<string> currentTimelineLines = currentTimeline.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> previousTimeline = ReadRecentDiagnosticLines(
                MaximumDiagnosticTimelineLines,
                AppPaths.DiagnosticPreviousSessionBackup,
                AppPaths.DiagnosticPreviousSessionLog)
            .Where(line => !currentTimelineLines.Contains(line))
            .ToArray();
        string informational = GetCurrentBuildIdentity();
        var report = new StringBuilder();
        report.AppendLine("AFTERLINE ERROR REPORT");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        report.AppendLine($"Build: {informational}");
        report.AppendLine($"Windows: {Environment.OSVersion}");
        report.AppendLine($"Current diagnostic errors included: {errors.Count} (maximum {MaximumReportErrors})");
        report.AppendLine($"Previous-session-only errors included: {previousErrors.Count} (maximum {MaximumReportErrors})");
        report.AppendLine($"Current diagnostic timeline lines included: {currentTimeline.Count} (maximum {MaximumDiagnosticTimelineLines})");
        report.AppendLine($"Previous-session-only timeline lines included: {previousTimeline.Count} (maximum {MaximumDiagnosticTimelineLines})");
        report.AppendLine("Discord: https://discord.gg/At2znTygfV");
        report.AppendLine("Support channel: #afterline forum channel on Discord");
        report.AppendLine();
        report.AppendLine("Send this report only in the #afterline forum channel on Discord.");
        report.AppendLine("Common Windows user-profile paths have been redacted automatically.");
        report.AppendLine(new string('-', 78));
        if (errors.Count == 0 && previousErrors.Count == 0)
        {
            report.AppendLine("No errors are currently recorded.");
        }
        else
        {
            report.AppendLine("CURRENT DIAGNOSTIC LOG");
            report.AppendLine(new string('-', 78));
            foreach (string error in errors)
            {
                report.AppendLine(RedactPersonalPaths(error));
                report.AppendLine(new string('-', 78));
            }

            if (previousErrors.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("PREVIOUS SESSION SNAPSHOT");
                report.AppendLine(new string('-', 78));
                foreach (string error in previousErrors)
                {
                    report.AppendLine(RedactPersonalPaths(error));
                    report.AppendLine(new string('-', 78));
                }
            }
        }

        AppendDiagnosticTimeline(report, "CURRENT DIAGNOSTIC TIMELINE", currentTimeline);
        AppendDiagnosticTimeline(report, "PREVIOUS SESSION DIAGNOSTIC TIMELINE", previousTimeline);

        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static IReadOnlyList<string> ReadRecentDiagnosticLines(int maximum, params string[] paths)
    {
        maximum = Math.Clamp(maximum, 1, MaximumDiagnosticTimelineLines);
        var recent = new Queue<string>(maximum);
        try
        {
            lock (Gate)
            {
                foreach (string path in paths)
                {
                    if (!File.Exists(path)) continue;
                    foreach (string line in File.ReadLines(path))
                    {
                        recent.Enqueue(line);
                        while (recent.Count > maximum)
                            recent.Dequeue();
                    }
                }
            }
        }
        catch
        {
        }
        return recent.ToArray();
    }

    private static void AppendDiagnosticTimeline(
        StringBuilder report,
        string heading,
        IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return;
        report.AppendLine();
        report.AppendLine(heading);
        report.AppendLine(new string('-', 78));
        foreach (string line in lines)
            report.AppendLine(RedactPersonalPaths(line));
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

    private static void SnapshotCurrentSessionLogs()
    {
        SnapshotLogFile(AppPaths.DiagnosticLog, AppPaths.DiagnosticPreviousSessionLog);
        SnapshotLogFile(AppPaths.DiagnosticLog + ".1", AppPaths.DiagnosticPreviousSessionBackup);
    }

    private static void SnapshotLogFile(string source, string destination)
    {
        if (!File.Exists(source))
        {
            if (File.Exists(destination)) File.Delete(destination);
            return;
        }

        string temp = destination + $".{Environment.ProcessId}.tmp";
        try
        {
            File.Copy(source, temp, true);
            File.Move(temp, destination, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void DeleteCurrentLogFiles()
    {
        if (File.Exists(AppPaths.DiagnosticLog)) File.Delete(AppPaths.DiagnosticLog);
        string backup = AppPaths.DiagnosticLog + ".1";
        if (File.Exists(backup)) File.Delete(backup);
    }

    private static void DeletePreviousSessionLogFiles()
    {
        if (File.Exists(AppPaths.DiagnosticPreviousSessionLog))
            File.Delete(AppPaths.DiagnosticPreviousSessionLog);
        if (File.Exists(AppPaths.DiagnosticPreviousSessionBackup))
            File.Delete(AppPaths.DiagnosticPreviousSessionBackup);
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
