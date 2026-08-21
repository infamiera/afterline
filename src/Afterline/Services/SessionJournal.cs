using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public sealed class SessionJournal
{
    private static readonly Regex TimestampPrefix = new(@"^\[\d{1,2}:\d{2}:\d{2}\]\s+", RegexOptions.Compiled);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _activeFile;
    private string? _stateFile;
    private string? _backupFile;
    private SessionState? _state;

    public bool HasActiveSession => _activeFile is not null && File.Exists(_activeFile);
    public DateTime? StartedAt => _state?.StartedAt;
    public DateTime? LastMessageAt => _state?.LastMessageAt;
    public int MessageCount => _state?.MessageCount ?? 0;
    public string? ActiveFile => _activeFile;

    public async Task<IReadOnlyList<string>> RecoverAsync(string archiveRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.ActiveSessionsDirectory);
        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);

        string[] stateFiles = Directory.GetFiles(AppPaths.ActiveSessionsDirectory, "*.state.json")
            .OrderBy(File.GetLastWriteTimeUtc)
            .ToArray();

        IReadOnlyList<string> previousVisible = Array.Empty<string>();

        foreach (string stateFile in stateFiles)
        {
            SessionState? state = await LoadStateAsync(stateFile, cancellationToken);
            if (state is null)
            {
                DeleteIfExists(stateFile);
                continue;
            }

            previousVisible = state.LastVisibleSnapshot.ToArray();
            await RecoverSessionAsync(state, archiveRoot, cancellationToken);
            DeleteIfExists(state.BackupFile);
            DeleteIfExists(stateFile);
        }

        _activeFile = null;
        _stateFile = null;
        _backupFile = null;
        _state = null;
        return previousVisible;
    }

    public async Task<ChatEntry?> EnsureStartedAsync(string archiveRoot, DateTime startedAt, CancellationToken cancellationToken)
    {
        if (HasActiveSession) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (HasActiveSession) return null;

            Directory.CreateDirectory(AppPaths.ActiveSessionsDirectory);
            Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);

            string year = startedAt.ToString("yyyy");
            string month = startedAt.ToString("MM - MMMM");
            string monthDir = Path.Combine(archiveRoot, year, month);
            Directory.CreateDirectory(monthDir);

            string? existing = FindSameDayArchive(monthDir, startedAt.Date);
            bool continuingSameDay = existing is not null;

            _activeFile = existing ?? UniquePath(
                monthDir,
                $"Afterline Chatlog [{startedAt:yyyy-MM-dd - HH-mm-ss}]",
                ".txt");

            _backupFile = Path.Combine(
                AppPaths.RecoveryBackupsDirectory,
                $"Afterline Recovery [{startedAt:yyyy-MM-dd - HH-mm-ss}].txt");
            _stateFile = Path.Combine(
                AppPaths.ActiveSessionsDirectory,
                $"Afterline [{startedAt:yyyy-MM-dd - HH-mm-ss}].state.json");

            _state = new SessionState
            {
                StartedAt = startedAt,
                ArchiveFile = _activeFile,
                BackupFile = _backupFile
            };

            await WriteNewFileAsync(
                _backupFile,
                $"[AFTERLINE RECOVERY: {startedAt:yyyy-MM-dd HH:mm:ss}]",
                cancellationToken);

            ChatEntry? marker = null;
            if (continuingSameDay)
            {
                marker = await AppendLoginMarkerCoreAsync(startedAt, cancellationToken);
            }
            else
            {
                await WriteNewFileAsync(
                    _activeFile,
                    $"[AFTERLINE SESSION: {startedAt:yyyy-MM-dd HH:mm:ss}]",
                    cancellationToken);
            }

            await SaveStateAsync(cancellationToken);
            return marker;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatEntry?> AppendLoginMarkerAsync(DateTime startedAt, CancellationToken cancellationToken)
    {
        if (!HasActiveSession || _state is null) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!HasActiveSession || _state is null) return null;
            ChatEntry marker = await AppendLoginMarkerCoreAsync(startedAt, cancellationToken);
            await SaveStateAsync(cancellationToken);
            return marker;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatEntry?> MarkDisconnectedAsync(DateTime observedAt, CancellationToken cancellationToken)
    {
        if (!HasActiveSession || _state is null || _activeFile is null) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!HasActiveSession || _state is null || _activeFile is null) return null;

            DateTime timestamp = _state.LastMessageAt ?? observedAt;
            string marker = $"==================== [DISCONNECTED] - {timestamp:HH:mm:ss} ====================";

            await AppendLineAsync(_activeFile, string.Empty, cancellationToken);
            await AppendLineAsync(_activeFile, marker, cancellationToken);

            if (_backupFile is not null)
            {
                try
                {
                    await AppendLineAsync(_backupFile, string.Empty, cancellationToken);
                    await AppendLineAsync(_backupFile, marker, cancellationToken);
                }
                catch (Exception ex)
                {
                    DiagnosticLogger.Error("Failsafe disconnect marker backup write failed.", ex);
                }
            }

            await SaveStateAsync(cancellationToken);
            return ChatEntry.System(timestamp, marker);
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

            try
            {
                await AppendLineAsync(_activeFile, line, cancellationToken);
            }
            catch
            {
                if (_backupFile is not null)
                {
                    try { await AppendLineAsync(_backupFile, line, cancellationToken); } catch { }
                }
                throw;
            }

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
            if (_state is null) return;
            _state.LastVisibleSnapshot = snapshot.TakeLast(250).ToList();
            await SaveStateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> ExportCurrentLogAsync(string archiveRoot, string downloadsFolder, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(downloadsFolder);
            DateTime now = DateTime.Now;
            string destination = UniquePath(
                downloadsFolder,
                $"Afterline Chatlog Export [{now:yyyy-MM-dd - HH-mm-ss}]",
                ".txt");

            if (_state is not null && !string.IsNullOrWhiteSpace(_backupFile) && File.Exists(_backupFile))
            {
                string[] sessionLines = File.ReadLines(_backupFile).Skip(1).ToArray();
                if (sessionLines.Length == 0)
                    throw new InvalidOperationException("The current login does not contain any captured chat yet.");

                await using FileStream stream = new(
                    destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
                await using StreamWriter writer = new(stream, new UTF8Encoding(false));
                await writer.WriteLineAsync($"[AFTERLINE LIVE EXPORT: {now:yyyy-MM-dd HH:mm:ss}]".AsMemory(), cancellationToken);
                foreach (string line in sessionLines)
                    await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return destination;
            }

            string year = now.ToString("yyyy");
            string month = now.ToString("MM - MMMM");
            string monthDir = Path.Combine(archiveRoot, year, month);
            string? sameDay = FindSameDayArchive(monthDir, now.Date);
            if (sameDay is null || !File.Exists(sameDay))
                throw new InvalidOperationException("No captured chatlog is available to export yet.");

            File.Copy(sameDay, destination, false);
            return destination;
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
            string destination = _activeFile;
            if (_state is not null)
                await RecoverSessionAsync(_state, archiveRoot, cancellationToken);

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

    private async Task<ChatEntry> AppendLoginMarkerCoreAsync(DateTime startedAt, CancellationToken cancellationToken)
    {
        if (_activeFile is null)
            throw new InvalidOperationException("No active chatlog is available for a login marker.");

        string marker = $"==================== NEW LOGIN - {startedAt:HH:mm:ss} ====================";
        await AppendLineAsync(_activeFile, string.Empty, cancellationToken);
        await AppendLineAsync(_activeFile, marker, cancellationToken);

        if (_backupFile is not null)
        {
            try
            {
                await AppendLineAsync(_backupFile, string.Empty, cancellationToken);
                await AppendLineAsync(_backupFile, marker, cancellationToken);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Failsafe login marker backup write failed.", ex);
            }
        }

        return ChatEntry.System(startedAt, marker);
    }

    private static async Task RecoverSessionAsync(SessionState state, string archiveRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.BackupFile) || !File.Exists(state.BackupFile))
            return;

        string archiveFile = state.ArchiveFile;
        if (string.IsNullOrWhiteSpace(archiveFile))
        {
            string year = state.StartedAt.ToString("yyyy");
            string month = state.StartedAt.ToString("MM - MMMM");
            string monthDir = Path.Combine(archiveRoot, year, month);
            Directory.CreateDirectory(monthDir);
            archiveFile = Path.Combine(monthDir, $"Afterline Chatlog [{state.StartedAt:yyyy-MM-dd - HH-mm-ss}].txt");
        }

        string[] backupLines = File.ReadLines(state.BackupFile)
            .Skip(1)
            .ToArray();
        if (backupLines.Length == 0) return;

        if (!File.Exists(archiveFile))
        {
            await WriteNewFileAsync(
                archiveFile,
                $"[AFTERLINE SESSION: {state.StartedAt:yyyy-MM-dd HH:mm:ss}]",
                cancellationToken);
            foreach (string line in backupLines)
                await AppendLineAsync(archiveFile, line, cancellationToken);
            return;
        }

        string[] archiveTail = File.ReadLines(archiveFile)
            .TakeLast(Math.Max(backupLines.Length, 250))
            .ToArray();
        int overlap = FindOverlap(archiveTail, backupLines);
        foreach (string line in backupLines.Skip(overlap))
            await AppendLineAsync(archiveFile, line, cancellationToken);
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
        await using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));
        await writer.WriteLineAsync(firstLine.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task AppendLineAsync(string path, string line, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
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

    private static int FindOverlap(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        int max = Math.Min(oldLines.Count, newLines.Count);
        for (int length = max; length > 0; length--)
        {
            bool same = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(oldLines[oldLines.Count - length + i], newLines[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
            if (same) return length;
        }
        return 0;
    }

    private static string UniquePath(string folder, string baseName, string extension)
    {
        string path = Path.Combine(folder, baseName + extension);
        if (!File.Exists(path)) return path;
        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(folder, $"{baseName} ({i}){extension}");
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
        public string ArchiveFile { get; set; } = string.Empty;
        public string BackupFile { get; set; } = string.Empty;
        public List<string> LastVisibleSnapshot { get; set; } = new();
    }
}
