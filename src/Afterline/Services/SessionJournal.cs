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
    private SessionState? _state;

    public bool HasActiveSession => _activeFile is not null && File.Exists(_activeFile);
    public DateTime? StartedAt => _state?.StartedAt;
    public int MessageCount => _state?.MessageCount ?? 0;
    public string? ActiveFile => _activeFile;
    public IReadOnlyList<string> LastVisibleSnapshot => _state is null
        ? Array.Empty<string>()
        : _state.LastVisibleSnapshot;

    public async Task RecoverAsync(string archiveRoot, bool fiveMIsRunning, CancellationToken cancellationToken)
    {
        string activeDir = GetActiveDirectory();
        Directory.CreateDirectory(activeDir);

        string[] activeLogs = Directory.GetFiles(activeDir, "*.txt")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        if (activeLogs.Length == 0) return;

        if (fiveMIsRunning)
        {
            _activeFile = activeLogs[0];
            _stateFile = Path.ChangeExtension(_activeFile, ".state.json");
            _state = await LoadStateAsync(_stateFile, cancellationToken) ?? new SessionState
            {
                StartedAt = File.GetCreationTime(_activeFile),
                MessageCount = Math.Max(0, File.ReadLines(_activeFile).Count() - 1)
            };

            foreach (string stale in activeLogs.Skip(1))
                await FinalizeSpecificAsync(stale, archiveRoot, cancellationToken);
        }
        else
        {
            foreach (string stale in activeLogs)
                await FinalizeSpecificAsync(stale, archiveRoot, cancellationToken);
        }
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

            string baseName = $"Afterline Chatlog [{startedAt:yyyy-MM-dd - HH-mm-ss}]";
            _activeFile = UniquePath(activeDir, baseName, ".txt");
            _stateFile = Path.ChangeExtension(_activeFile, ".state.json");
            _state = new SessionState { StartedAt = startedAt };

            await File.WriteAllTextAsync(_activeFile,
                $"[AFTERLINE SESSION: {startedAt:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}",
                new UTF8Encoding(false), cancellationToken);
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

            await using FileStream stream = new(_activeFile, FileMode.Append, FileAccess.Write, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);

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
            string destination = BuildArchivePath(source, archiveRoot, _state?.StartedAt ?? File.GetCreationTime(source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            destination = EnsureUniqueFile(destination);
            MoveAcrossVolumes(source, destination);
            if (_stateFile is not null && File.Exists(_stateFile)) File.Delete(_stateFile);
            _activeFile = null;
            _stateFile = null;
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
        SessionState? state = await LoadStateAsync(stateFile, cancellationToken);
        DateTime started = state?.StartedAt ?? File.GetCreationTime(source);
        string destination = EnsureUniqueFile(BuildArchivePath(source, archiveRoot, started));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        MoveAcrossVolumes(source, destination);
        if (File.Exists(stateFile)) File.Delete(stateFile);
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

    private sealed class SessionState
    {
        public DateTime StartedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessageCount { get; set; }
        public List<string> LastVisibleSnapshot { get; set; } = new();
    }
}


