using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public sealed class SessionJournal
{
    private static readonly Regex TimestampPrefix = new(
        @"^\[\d{1,2}:\d{2}:\d{2}\]\s+",
        RegexOptions.Compiled);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _activeFile;
    private string? _stateFile;
    private string? _backupFile;
    private string? _archiveRoot;
    private SessionState? _state;

    public bool HasActiveSession => _activeFile is not null && File.Exists(_activeFile);
    public DateTime? StartedAt => _state?.StartedAt;
    public DateTime? LastMessageAt => _state?.LastMessageAt;
    public DateTime? ActiveDate => _state is null
        ? null
        : (_state.ArchiveDate == default ? _state.StartedAt.Date : _state.ArchiveDate.Date);
    public int MessageCount => _state?.MessageCount ?? 0;
    public string? ActiveFile => _activeFile;
    public string? ActiveServerName => _state?.ServerName;

    public event EventHandler<DailyLogRolloverEventArgs>? DailyLogRolledOver;

    public async Task<IReadOnlyList<string>> RecoverAsync(
        string archiveRoot,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.ActiveSessionsDirectory);
        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);

        string[] stateFiles = Directory.GetFiles(
                AppPaths.ActiveSessionsDirectory,
                "*.state.json")
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

        ClearActiveState();
        return previousVisible;
    }

    public async Task<ChatEntry?> EnsureStartedAsync(
        string archiveRoot,
        DateTime startedAt,
        ServerSessionInfo server,
        CancellationToken cancellationToken)
    {
        if (HasActiveSession) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (HasActiveSession) return null;

            Directory.CreateDirectory(AppPaths.ActiveSessionsDirectory);
            Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);

            _archiveRoot = archiveRoot;

            string serverName = server.ArchiveLabel;
            string requestedPath = GetArchivePath(archiveRoot, serverName, startedAt);
            bool identityKnown = !string.Equals(
                server.StableKey,
                "unknown",
                StringComparison.Ordinal);

            if (!identityKnown && File.Exists(requestedPath))
            {
                string folder = Path.GetDirectoryName(requestedPath)!;
                string baseName = Path.GetFileNameWithoutExtension(requestedPath);
                _activeFile = UniquePath(folder, baseName, ".txt");
            }
            else
            {
                _activeFile = requestedPath;
            }

            bool continuingSameServerDay = identityKnown && File.Exists(_activeFile);
            Directory.CreateDirectory(Path.GetDirectoryName(_activeFile)!);

            _backupFile = Path.Combine(
                AppPaths.RecoveryBackupsDirectory,
                $"Afterline Recovery [{startedAt:yyyy-MM-dd - HH-mm-ss}].txt");
            _stateFile = Path.Combine(
                AppPaths.ActiveSessionsDirectory,
                $"Afterline [{startedAt:yyyy-MM-dd - HH-mm-ss}].state.json");

            _state = new SessionState
            {
                StartedAt = startedAt,
                ArchiveDate = startedAt.Date,
                ArchiveFile = _activeFile,
                BackupFile = _backupFile,
                ServerName = serverName,
                ServerKey = server.StableKey
            };

            await WriteNewFileAsync(
                _backupFile,
                $"[AFTERLINE RECOVERY: {serverName} · {startedAt:yyyy-MM-dd HH:mm:ss}]",
                cancellationToken);

            if (!continuingSameServerDay)
            {
                await WriteNewFileAsync(
                    _activeFile,
                    $"[AFTERLINE SERVER: {serverName}]",
                    cancellationToken);
            }

            ChatEntry marker = await AppendLoginMarkerCoreAsync(startedAt, cancellationToken);
            await SaveStateAsync(cancellationToken);
            return marker;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatEntry?> MarkDisconnectedAsync(
        DateTime observedAt,
        CancellationToken cancellationToken)
    {
        if (!HasActiveSession || _state is null || _activeFile is null) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!HasActiveSession || _state is null || _activeFile is null) return null;

            DateTime timestamp = _state.LastMessageAt ?? observedAt;
            string marker =
                $"==================== [DISCONNECTED] - {timestamp:HH:mm:ss} ====================";

            await AppendMarkerToBothAsync(marker, cancellationToken);
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

        DailyLogRolloverEventArgs? rollover = null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!HasActiveSession || _state is null || _activeFile is null) return;

            DateTime activeDate = _state.ArchiveDate == default
                ? _state.StartedAt.Date
                : _state.ArchiveDate.Date;

            if (entry.CapturedAt.Date > activeDate)
                rollover = await RollOverDayCoreAsync(entry.CapturedAt, cancellationToken);

            if (_state is null || _activeFile is null) return;

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
                    try { await AppendLineAsync(_backupFile, line, cancellationToken); }
                    catch { }
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

        if (rollover is not null)
            DailyLogRolledOver?.Invoke(this, rollover);
    }

    public async Task UpdateVisibleSnapshotAsync(
        IReadOnlyList<string> snapshot,
        CancellationToken cancellationToken)
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

    public async Task<string> ExportCurrentLogAsync(
        string archiveRoot,
        string downloadsFolder,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(downloadsFolder);
            DateTime now = DateTime.Now;
            string destination = UniquePath(
                downloadsFolder,
                $"Chatlog Export [{now:dd-MMMM-yyyy - HH-mm-ss}]",
                ".txt");

            if (_state is not null &&
                File.Exists(AppPaths.LastSessionCacheFile))
            {
                string[] sessionLines = File.ReadLines(AppPaths.LastSessionCacheFile)
                    .Skip(1)
                    .ToArray();

                if (sessionLines.Length > 0)
                {
                    await WriteExportAsync(
                        destination,
                        _state.ServerName,
                        now,
                        sessionLines,
                        cancellationToken);
                    return destination;
                }
            }

            if (_state is not null &&
                !string.IsNullOrWhiteSpace(_backupFile) &&
                File.Exists(_backupFile))
            {
                string[] sessionLines = File.ReadLines(_backupFile).Skip(1).ToArray();
                if (sessionLines.Length == 0)
                    throw new InvalidOperationException(
                        "The current server session does not contain any captured chat yet.");

                await WriteExportAsync(
                    destination,
                    _state.ServerName,
                    now,
                    sessionLines,
                    cancellationToken);
                return destination;
            }

            string? latest = FindLatestSameDayArchive(archiveRoot, now);
            if (latest is null)
                throw new InvalidOperationException(
                    "No captured chatlog is available to export yet.");

            File.Copy(latest, destination, false);
            return destination;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> FinalizeAsync(
        string archiveRoot,
        CancellationToken cancellationToken)
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
            ClearActiveState();
            return destination;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DailyLogRolloverEventArgs?> RollOverDayCoreAsync(
        DateTime observedAt,
        CancellationToken cancellationToken)
    {
        if (_state is null ||
            _activeFile is null ||
            string.IsNullOrWhiteSpace(_archiveRoot))
            return null;

        DateTime previousDate = _state.ArchiveDate == default
            ? _state.StartedAt.Date
            : _state.ArchiveDate.Date;
        DateTime newDate = observedAt.Date;

        if (newDate <= previousDate) return null;

        string previousLogPath = _activeFile;
        DateTime dayEndedAt = previousDate.AddDays(1).AddSeconds(-1);
        string dayEndedMarker =
            $"==================== [DAY ENDED] - {dayEndedAt:HH:mm:ss} ====================";

        await AppendMarkerToBothAsync(dayEndedMarker, cancellationToken);
        await SaveStateAsync(cancellationToken);
        await RecoverSessionAsync(_state, _archiveRoot, cancellationToken);

        DateTime sessionStartedAt = _state.StartedAt;
        DateTime? lastMessageAt = _state.LastMessageAt;
        int messageCount = _state.MessageCount;
        string serverName = _state.ServerName;
        string serverKey = _state.ServerKey;
        List<string> visibleSnapshot = _state.LastVisibleSnapshot.ToList();

        DeleteIfExists(_stateFile);
        DeleteIfExists(_backupFile);

        string requestedPath = GetArchivePath(_archiveRoot, serverName, newDate);
        bool identityKnown = !string.Equals(
            serverKey,
            "unknown",
            StringComparison.Ordinal);

        if (!identityKnown && File.Exists(requestedPath))
        {
            string folder = Path.GetDirectoryName(requestedPath)!;
            string baseName = Path.GetFileNameWithoutExtension(requestedPath);
            _activeFile = UniquePath(folder, baseName, ".txt");
        }
        else
        {
            _activeFile = requestedPath;
        }

        bool continuingSameServerDay = identityKnown && File.Exists(_activeFile);
        Directory.CreateDirectory(Path.GetDirectoryName(_activeFile)!);

        _backupFile = Path.Combine(
            AppPaths.RecoveryBackupsDirectory,
            $"Afterline Recovery [{observedAt:yyyy-MM-dd - HH-mm-ss}].txt");
        _stateFile = Path.Combine(
            AppPaths.ActiveSessionsDirectory,
            $"Afterline [{observedAt:yyyy-MM-dd - HH-mm-ss}].state.json");

        _state = new SessionState
        {
            StartedAt = sessionStartedAt,
            ArchiveDate = newDate,
            LastMessageAt = lastMessageAt,
            MessageCount = messageCount,
            ArchiveFile = _activeFile,
            BackupFile = _backupFile,
            ServerName = serverName,
            ServerKey = serverKey,
            LastVisibleSnapshot = visibleSnapshot
        };

        await WriteNewFileAsync(
            _backupFile,
            $"[AFTERLINE RECOVERY: {serverName} · {observedAt:yyyy-MM-dd HH:mm:ss}]",
            cancellationToken);

        if (!continuingSameServerDay)
        {
            await WriteNewFileAsync(
                _activeFile,
                $"[AFTERLINE SERVER: {serverName}]",
                cancellationToken);
        }

        DateTime rolloverAt = newDate;
        string rolloverMarker =
            $"==================== [DATE ROLLOVER] - {rolloverAt:HH:mm:ss} ====================";
        await AppendMarkerToBothAsync(rolloverMarker, cancellationToken);
        await AppendRolloverMarkersToLastSessionCacheAsync(
            dayEndedMarker,
            rolloverMarker,
            cancellationToken);
        await SaveStateAsync(cancellationToken);

        return new DailyLogRolloverEventArgs(
            previousDate,
            newDate,
            previousLogPath,
            _activeFile);
    }

    private async Task<ChatEntry> AppendLoginMarkerCoreAsync(
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        if (_activeFile is null)
            throw new InvalidOperationException(
                "No active chatlog is available for a login marker.");

        string marker =
            $"==================== [NEW LOGIN] - {startedAt:HH:mm:ss} ====================";
        await AppendMarkerToBothAsync(marker, cancellationToken);
        return ChatEntry.System(startedAt, marker);
    }

    private async Task AppendMarkerToBothAsync(
        string marker,
        CancellationToken cancellationToken)
    {
        if (_activeFile is null) return;

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
                DiagnosticLogger.Error("Failsafe marker backup write failed.", ex);
            }
        }
    }

    private static async Task AppendRolloverMarkersToLastSessionCacheAsync(
        string dayEndedMarker,
        string rolloverMarker,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(AppPaths.LastSessionCacheFile)) return;

            await AppendLineAsync(
                AppPaths.LastSessionCacheFile,
                string.Empty,
                cancellationToken);
            await AppendLineAsync(
                AppPaths.LastSessionCacheFile,
                dayEndedMarker,
                cancellationToken);
            await AppendLineAsync(
                AppPaths.LastSessionCacheFile,
                string.Empty,
                cancellationToken);
            await AppendLineAsync(
                AppPaths.LastSessionCacheFile,
                rolloverMarker,
                cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to add daily rollover markers to the session cache.",
                ex);
        }
    }

    private static async Task WriteExportAsync(
        string destination,
        string serverName,
        DateTime exportedAt,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync(
            $"[AFTERLINE LIVE EXPORT: {serverName} · {exportedAt:yyyy-MM-dd HH:mm:ss}]"
                .AsMemory(),
            cancellationToken);

        foreach (string line in lines)
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);

        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task RecoverSessionAsync(
        SessionState state,
        string archiveRoot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.BackupFile) ||
            !File.Exists(state.BackupFile))
            return;

        string archiveFile = state.ArchiveFile;
        if (string.IsNullOrWhiteSpace(archiveFile))
        {
            DateTime archiveDate = state.ArchiveDate == default
                ? state.StartedAt
                : state.ArchiveDate;
            archiveFile = GetArchivePath(
                archiveRoot,
                state.ServerName,
                archiveDate);
        }

        string[] backupLines = File.ReadLines(state.BackupFile)
            .Skip(1)
            .ToArray();
        if (backupLines.Length == 0) return;

        if (!File.Exists(archiveFile))
        {
            await WriteNewFileAsync(
                archiveFile,
                $"[AFTERLINE SERVER: {state.ServerName}]",
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
        string json = JsonSerializer.Serialize(
            _state,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(temp, json, cancellationToken);
        File.Move(temp, _stateFile, true);
    }

    private static async Task<SessionState?> LoadStateAsync(
        string path,
        CancellationToken cancellationToken)
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

    private static async Task WriteNewFileAsync(
        string path,
        string firstLine,
        CancellationToken cancellationToken)
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

    private static async Task AppendLineAsync(
        string path,
        string line,
        CancellationToken cancellationToken)
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

    private static string GetArchivePath(
        string archiveRoot,
        string serverName,
        DateTime date)
    {
        string year = date.ToString("yyyy", CultureInfo.InvariantCulture);
        string month = date.ToString("MM - MMMM", CultureInfo.InvariantCulture);
        string monthDir = Path.Combine(archiveRoot, year, month);
        string safeServer = SanitizeFileComponent(serverName);
        string displayDate = date.ToString("dd-MMMM-yyyy", CultureInfo.InvariantCulture);

        return Path.Combine(
            monthDir,
            $"Chatlog [{safeServer}] [{displayDate}].txt");
    }

    private static string? FindLatestSameDayArchive(
        string archiveRoot,
        DateTime date)
    {
        string year = date.ToString("yyyy", CultureInfo.InvariantCulture);
        string month = date.ToString("MM - MMMM", CultureInfo.InvariantCulture);
        string monthDir = Path.Combine(archiveRoot, year, month);
        if (!Directory.Exists(monthDir)) return null;

        string suffix =
            $"[{date.ToString("dd-MMMM-yyyy", CultureInfo.InvariantCulture)}].txt";

        return Directory.GetFiles(
                monthDir,
                "Chatlog *.txt",
                SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path)
                .EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string SanitizeFileComponent(string value)
    {
        string source = string.IsNullOrWhiteSpace(value)
            ? "Unknown Server"
            : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(source.Length);

        foreach (char c in source)
        {
            if (invalid.Contains(c) || c == '[' || c == ']')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        string safe = string.Join(
                " ",
                builder.ToString()
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '.');

        if (string.IsNullOrWhiteSpace(safe))
            safe = "Unknown Server";
        if (safe.Length > 80)
            safe = safe[..80].TrimEnd();

        return safe;
    }

    private static int FindOverlap(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
    {
        int max = Math.Min(oldLines.Count, newLines.Count);

        for (int length = max; length > 0; length--)
        {
            bool same = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(
                        oldLines[oldLines.Count - length + i],
                        newLines[i],
                        StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }

            if (same) return length;
        }

        return 0;
    }

    private static string UniquePath(
        string folder,
        string baseName,
        string extension)
    {
        string path = Path.Combine(folder, baseName + extension);
        if (!File.Exists(path)) return path;

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(
                folder,
                $"{baseName} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private void ClearActiveState()
    {
        _activeFile = null;
        _stateFile = null;
        _backupFile = null;
        _archiveRoot = null;
        _state = null;
    }

    private sealed class SessionState
    {
        public DateTime StartedAt { get; set; }
        public DateTime ArchiveDate { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessageCount { get; set; }
        public string ArchiveFile { get; set; } = string.Empty;
        public string BackupFile { get; set; } = string.Empty;
        public string ServerName { get; set; } = "Unknown Server";
        public string ServerKey { get; set; } = "unknown";
        public List<string> LastVisibleSnapshot { get; set; } = new();
    }
}
