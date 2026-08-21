using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public sealed class SessionJournal
{
    private static readonly Regex TimestampPrefix = new(@"^\[\d{1,2}:\d{2}:\d{2}\]\s+", RegexOptions.Compiled);
    private static readonly Regex FileTimestamp = new(@"Afterline Chatlog \[(?<date>\d{4}-\d{2}-\d{2}) - (?<time>\d{2}-\d{2}-\d{2})\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _activeFile;
    private string? _stateFile;
    private string? _backupFile;
    private SessionState? _state;

    public bool HasActiveSession => _activeFile is not null && File.Exists(_activeFile);
    public DateTime? StartedAt => _state?.StartedAt;
    public int MessageCount => _state?.MessageCount ?? 0;
    public string? ActiveFile => _activeFile;

    public async Task<IReadOnlyList<string>> RecoverAsync(string archiveRoot, CancellationToken cancellationToken)
    {
        string activeDir = GetActiveDirectory();
        Directory.CreateDirectory(activeDir);
        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);

        RestoreFromRecoveryBackups(activeDir);

        string[] activeLogs = Directory.GetFiles(activeDir, "*.txt")
            .OrderBy(File.GetCreationTimeUtc)
            .ToArray();

        IReadOnlyList<string> previousVisible = Array.Empty<string>();
        if (activeLogs.Length > 0)
        {
            string newestStateFile = Path.ChangeExtension(activeLogs[^1], ".state.json");
            SessionState? newestState = await LoadStateAsync(newestStateFile, cancellationToken);
            if (newestState is not null)
                previousVisible = newestState.LastVisibleSnapshot.ToArray();
        }

        foreach (string stale in activeLogs)
            await FinalizeSpecificAsync(stale, archiveRoot, cancellationToken);

        _activeFile = null;
        _stateFile = null;
        _backupFile = null;
        _state = null;
        return previousVisible;
    }

    public async Task EnsureStartedAsync(string archiveRoot, DateTime startedAt, CancellationToken cancellationToken)
    {
        if (HasActiveSession) return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (HasActiveSession) return;

            string activeDir = GetActiveDirectory();
            Directory.CreateDirectory(activeDir);
            Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);

            string baseName = $"Afterline Chatlog [{startedAt:yyyy-MM-dd - HH-mm-ss}]";
            _activeFile = UniquePath(activeDir, baseName, ".txt");
            _stateFile = Path.ChangeExtension(_activeFile, ".state.json");
            _backupFile = Path.Combine(AppPaths.RecoveryBackupsDirectory, Path.GetFileName(_activeFile));
            _state = new SessionState { StartedAt = startedAt };

            string header = $"[AFTERLINE SESSION: {startedAt:yyyy-MM-dd HH:mm:ss}]";
            await WriteNewFileAsync(_activeFile, header, cancellationToken);
            await WriteNewFileAsync(_backupFile, header, cancellationToken);
            await SaveStateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendAsync(ChatEntry entry, CancellationToken cancellationToken)
    {
        if (!HasActiveSession || _state is null || _activeFile is null) return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string line = TimestampPrefix.IsMatch(entry.Text)
                ? entry.Text
                : $"[{entry.CapturedAt:HH:mm:ss}] {entry.Text}";

            await AppendLineAsync(_activeFile, line, cancellationToken);

            if (_backupFile is not null)
            {
                try
                {
                    await AppendLineAsync(_backupFile, line, cancellationToken);
                }
                catch (Exception ex)
                {
                    DiagnosticLogger.Error("Failsafe chatlog backup write failed.", ex);
                }
            }

            _state.MessageCount++;
            _state.LastMessageAt = entry.CapturedAt;
            await SaveStateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateVisibleSnapshotAsync(IReadOnlyList<string> snapshot, CancellationToken cancellationToken)
    {
        if (_state is null) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _state.LastVisibleSnapshot = snapshot.TakeLast(250).ToList();
            await SaveStateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> FinalizeAsync(string archiveRoot, CancellationToken cancellationToken)
    {
        if (!HasActiveSession || _activeFile is null) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string source = _activeFile;
            DateTime started = _state?.StartedAt ?? InferStartedAt(source);
            string destination = await ArchiveOrMergeAsync(source, archiveRoot, started, cancellationToken);

            DeleteIfExists(_stateFile);
            DeleteIfExists(_backupFile);

            _activeFile = null;
            _stateFile = null;
            _backupFile = null;
            _state = null;
            return destination;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task FinalizeSpecificAsync(string source, string archiveRoot, CancellationToken cancellationToken)
    {
        string stateFile = Path.ChangeExtension(source, ".state.json");
        string backupFile = Path.Combine(AppPaths.RecoveryBackupsDirectory, Path.GetFileName(source));
        SessionState? state = await LoadStateAsync(stateFile, cancellationToken);
        DateTime started = state?.StartedAt ?? InferStartedAt(source);

        await ArchiveOrMergeAsync(source, archiveRoot, started, cancellationToken);
        DeleteIfExists(stateFile);
        DeleteIfExists(backupFile);
    }

    private static async Task<string> ArchiveOrMergeAsync(string source, string archiveRoot, DateTime startedAt, CancellationToken cancellationToken)
    {
        string year = startedAt.ToString("yyyy");
        string month = startedAt.ToString("MM - MMMM");
        string monthDir = Path.Combine(archiveRoot, year, month);
        Directory.CreateDirectory(monthDir);

        string? sameDay = FindSameDayArchive(monthDir, startedAt.Date);
        if (sameDay is null)
        {
            string destination = EnsureUniqueFile(BuildArchivePath(source, archiveRoot, startedAt));
            MoveAcrossVolumes(source, destination);
            return destination;
        }

        string divider = $"==================== NEW LOGIN - {startedAt:HH:mm:ss} ====================";

        await using (FileStream stream = new(sameDay, FileMode.Append, FileAccess.Write, FileShare.Read,
                         4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        await using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
        {
            await writer.WriteLineAsync();
            await writer.WriteLineAsync(divider.AsMemory(), cancellationToken);

            bool firstLine = true;
            foreach (string line in File.ReadLines(source))
            {
                if (firstLine && line.StartsWith("[AFTERLINE SESSION:", StringComparison.Ordinal))
                {
                    firstLine = false;
                    continue;
                }

                firstLine = false;
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            }

            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Delete(source);
        return sameDay;
    }

    private async Task SaveStateAsync(CancellationToken cancellationToken)
    {
        if (_stateFile is null || _state is null) return;
        string temp = _stateFile + ".tmp";
        string json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(temp, json, cancellationToken);
        File.Move(temp, _stateFile, true);
    }

    private static async Task<SessionState?> LoadStateAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteNewFileAsync(string path, string firstLine, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));
        await writer.WriteLineAsync(firstLine.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task AppendLineAsync(string path, string line, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static void RestoreFromRecoveryBackups(string activeDir)
    {
        foreach (string backup in Directory.GetFiles(AppPaths.RecoveryBackupsDirectory, "*.txt"))
        {
            string target = Path.Combine(activeDir, Path.GetFileName(backup));
            try
            {
                if (!File.Exists(target))
                {
                    File.Copy(backup, target, false);
                    continue;
                }

                var backupInfo = new FileInfo(backup);
                var activeInfo = new FileInfo(target);
                if (backupInfo.Length > activeInfo.Length && backupInfo.LastWriteTimeUtc >= activeInfo.LastWriteTimeUtc)
                    File.Copy(backup, target, true);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error($"Unable to restore failsafe backup {backup}.", ex);
            }
        }
    }

    private static string? FindSameDayArchive(string monthDir, DateTime date)
    {
        if (!Directory.Exists(monthDir)) return null;

        string prefix = $"Afterline Chatlog [{date:yyyy-MM-dd} - ";
        return Directory.GetFiles(monthDir, "*.txt", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static DateTime InferStartedAt(string path)
    {
        Match match = FileTimestamp.Match(Path.GetFileName(path));
        if (match.Success && DateTime.TryParseExact(
                $"{match.Groups["date"].Value} {match.Groups["time"].Value}",
                "yyyy-MM-dd HH-mm-ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
            return parsed;

        return File.GetCreationTime(path);
    }

    private static string GetActiveDirectory() => AppPaths.ActiveSessionsDirectory;

    private static string BuildArchivePath(string source, string archiveRoot, DateTime startedAt)
    {
        string year = startedAt.ToString("yyyy");
        string month = startedAt.ToString("MM - MMMM");
        return Path.Combine(archiveRoot, year, month, Path.GetFileName(source));
    }

    private static void MoveAcrossVolumes(string source, string destination)
    {
        try
        {
            File.Move(source, destination);
        }
        catch (IOException)
        {
            File.Copy(source, destination, false);
            File.Delete(source);
        }
    }

    private static string UniquePath(string folder, string baseName, string extension)
        => EnsureUniqueFile(Path.Combine(folder, baseName + extension));

    private static string EnsureUniqueFile(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private sealed class SessionState
    {
        public DateTime StartedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessageCount { get; set; }
        public List<string> LastVisibleSnapshot { get; set; } = new();
    }
}
