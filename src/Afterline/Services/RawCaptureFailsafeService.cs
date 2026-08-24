using System.Text;
using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class RawCaptureFailsafeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _lastServerKey = string.Empty;
    private string[] _lastLines = Array.Empty<string>();
    private RawCaptureSnapshot? _lastSnapshot;
    private bool _snapshotNeedsProcessedMark;

    public async Task BeginRunAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await RecoverInterruptedRawWriteAsync(cancellationToken);

            CaptureRunManifest? previous = await ReadJsonAsync<CaptureRunManifest>(
                AppPaths.CaptureRunStateFile,
                cancellationToken);

            bool previousUnexpected = previous is not null && !previous.CleanShutdown;
            if (previousUnexpected)
            {
                RawCaptureSnapshot? snapshot = await ReadJsonAsync<RawCaptureSnapshot>(
                    AppPaths.RawCaptureCacheFile,
                    cancellationToken);

                if (snapshot is not null && snapshot.ProcessedAt is null && snapshot.Lines.Count > 0)
                {
                    await PreserveCrashSnapshotAsync(snapshot, cancellationToken);
                    _lastSnapshot = snapshot;
                    _lastServerKey = snapshot.ServerKey;
                    _lastLines = NormalizeLines(snapshot.Lines);
                    _snapshotNeedsProcessedMark = true;
                }
            }

            var manifest = new CaptureRunManifest
            {
                StartedAt = DateTime.Now,
                CleanShutdown = false,
                PreviousRunEndedUnexpectedly = previousUnexpected
            };
            await WriteJsonAtomicAsync(AppPaths.CaptureRunStateFile, manifest, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteSnapshotAsync(
        IReadOnlyList<string> lines,
        ServerSessionInfo server,
        CancellationToken cancellationToken)
    {
        string serverKey = server.StableKey;
        if (MatchesLastSnapshot(lines, serverKey))
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (MatchesLastSnapshot(lines, serverKey))
                return;

            string[] normalized = NormalizeLines(lines);
            var snapshot = new RawCaptureSnapshot
            {
                CapturedAt = DateTime.Now,
                ServerName = server.DisplayName,
                ServerAddress = server.Address,
                ServerKey = serverKey,
                Lines = normalized.ToList()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.RawCaptureCacheFile)!);
            string temp = AppPaths.RawCaptureCacheFile + ".tmp";
            await WriteJsonFileAsync(temp, snapshot, cancellationToken);

            if (File.Exists(AppPaths.RawCaptureCacheFile))
                File.Copy(AppPaths.RawCaptureCacheFile, AppPaths.RawCapturePreviousCacheFile, true);

            File.Move(temp, AppPaths.RawCaptureCacheFile, true);
            _lastServerKey = serverKey;
            _lastLines = normalized;
            _lastSnapshot = snapshot;
            _snapshotNeedsProcessedMark = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkProcessedAsync(CancellationToken cancellationToken)
    {
        if (!_snapshotNeedsProcessedMark)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_snapshotNeedsProcessedMark)
                return;

            RawCaptureSnapshot? snapshot = _lastSnapshot;
            if (snapshot is null)
            {
                snapshot = await ReadJsonAsync<RawCaptureSnapshot>(
                    AppPaths.RawCaptureCacheFile,
                    cancellationToken);
            }

            if (snapshot is null || snapshot.ProcessedAt is not null)
            {
                _snapshotNeedsProcessedMark = false;
                return;
            }

            snapshot.ProcessedAt = DateTime.Now;
            await WriteJsonAtomicAsync(AppPaths.RawCaptureCacheFile, snapshot, cancellationToken);
            _lastSnapshot = snapshot;
            _snapshotNeedsProcessedMark = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkRunCleanlyClosedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            CaptureRunManifest? manifest = await ReadJsonAsync<CaptureRunManifest>(
                AppPaths.CaptureRunStateFile,
                cancellationToken);

            if (manifest is null) return;
            manifest.CleanShutdown = true;
            manifest.ClosedAt = DateTime.Now;
            await WriteJsonAtomicAsync(AppPaths.CaptureRunStateFile, manifest, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RawCaptureSnapshot?> ReadLatestRecoverableAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadLatestRecoverableCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CaptureRunManifest?> ReadRunManifestAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadJsonAsync<CaptureRunManifest>(
                AppPaths.CaptureRunStateFile,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public int CountPreservedCrashSnapshots()
    {
        try
        {
            if (!Directory.Exists(AppPaths.RecoveryBackupsDirectory)) return 0;
            return Directory.EnumerateFiles(
                    AppPaths.RecoveryBackupsDirectory,
                    "Raw Capture [*].json",
                    SearchOption.TopDirectoryOnly)
                .Count();
        }
        catch
        {
            return 0;
        }
    }

    public async Task<string> SaveRecoveryCopyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RawCaptureSnapshot? snapshot = await ReadLatestRecoverableCoreAsync(cancellationToken);
            if (snapshot is null || snapshot.Lines.Count == 0)
                throw new InvalidOperationException("No raw capture backup is available.");

            Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);
            string baseName = $"Recovered Raw Chat [{snapshot.CapturedAt:yyyy-MM-dd - HH-mm-ss}]";
            string destination = UniquePath(
                AppPaths.RecoveryBackupsDirectory,
                baseName,
                ".txt");

            await using FileStream stream = new(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false));

            await writer.WriteLineAsync(
                $"[AFTERLINE RAW RECOVERY: {snapshot.ServerName} · {snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss}]"
                    .AsMemory(),
                cancellationToken);

            foreach (string line in snapshot.Lines)
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);

            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return destination;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool MatchesLastSnapshot(IReadOnlyList<string> lines, string serverKey)
    {
        if (!string.Equals(_lastServerKey, serverKey, StringComparison.Ordinal))
            return false;

        int normalizedIndex = 0;
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (normalizedIndex >= _lastLines.Length ||
                !string.Equals(_lastLines[normalizedIndex], line.Trim(), StringComparison.Ordinal))
                return false;

            normalizedIndex++;
        }

        return normalizedIndex == _lastLines.Length;
    }

    private static string[] NormalizeLines(IReadOnlyList<string> lines)
    {
        var normalized = new List<string>(lines.Count);
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                normalized.Add(line.Trim());
        }
        return normalized.ToArray();
    }

    private static async Task RecoverInterruptedRawWriteAsync(
        CancellationToken cancellationToken)
    {
        string temp = AppPaths.RawCaptureCacheFile + ".tmp";
        if (!File.Exists(temp)) return;

        try
        {
            if (File.Exists(AppPaths.RawCaptureCacheFile) &&
                File.GetLastWriteTimeUtc(temp) <= File.GetLastWriteTimeUtc(AppPaths.RawCaptureCacheFile))
            {
                File.Delete(temp);
                return;
            }

            RawCaptureSnapshot? pending =
                await ReadJsonAsync<RawCaptureSnapshot>(temp, cancellationToken);
            if (pending is null)
            {
                File.Delete(temp);
                return;
            }

            if (File.Exists(AppPaths.RawCaptureCacheFile))
            {
                File.Copy(
                    AppPaths.RawCaptureCacheFile,
                    AppPaths.RawCapturePreviousCacheFile,
                    true);
            }

            File.Move(temp, AppPaths.RawCaptureCacheFile, true);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to recover an interrupted raw capture cache write.",
                ex);
        }
    }

    private async Task<RawCaptureSnapshot?> ReadLatestRecoverableCoreAsync(
        CancellationToken cancellationToken)
    {
        var snapshots = new List<RawCaptureSnapshot>();

        RawCaptureSnapshot? current = await ReadJsonAsync<RawCaptureSnapshot>(
            AppPaths.RawCaptureCacheFile,
            cancellationToken);
        if (current is not null) snapshots.Add(current);

        RawCaptureSnapshot? previous = await ReadJsonAsync<RawCaptureSnapshot>(
            AppPaths.RawCapturePreviousCacheFile,
            cancellationToken);
        if (previous is not null) snapshots.Add(previous);

        if (Directory.Exists(AppPaths.RecoveryBackupsDirectory))
        {
            foreach (string path in Directory.EnumerateFiles(
                         AppPaths.RecoveryBackupsDirectory,
                         "Raw Capture [*].json",
                         SearchOption.TopDirectoryOnly))
            {
                RawCaptureSnapshot? preserved =
                    await ReadJsonAsync<RawCaptureSnapshot>(path, cancellationToken);
                if (preserved is not null) snapshots.Add(preserved);
            }
        }

        return snapshots
            .Where(snapshot => snapshot.Lines.Count > 0)
            .OrderBy(snapshot => snapshot.ProcessedAt is null ? 0 : 1)
            .ThenByDescending(snapshot => snapshot.CapturedAt)
            .FirstOrDefault();
    }

    private static async Task PreserveCrashSnapshotAsync(
        RawCaptureSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);
        string destination = UniquePath(
            AppPaths.RecoveryBackupsDirectory,
            $"Raw Capture [{snapshot.CapturedAt:yyyy-MM-dd - HH-mm-ss}]",
            ".json");
        await WriteJsonFileAsync(destination, snapshot, cancellationToken);
    }

    private static async Task<T?> ReadJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteJsonAtomicAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        await WriteJsonFileAsync(temp, value, cancellationToken);
        File.Move(temp, path, true);
    }

    private static async Task WriteJsonFileAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            16 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(
            stream,
            value,
            JsonOptions,
            cancellationToken);
        await stream.FlushAsync(cancellationToken);
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
}

public sealed class RawCaptureSnapshot
{
    public DateTime CapturedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string ServerName { get; set; } = "Unknown Server";
    public string? ServerAddress { get; set; }
    public string ServerKey { get; set; } = "unknown";
    public List<string> Lines { get; set; } = new();
}

public sealed class CaptureRunManifest
{
    public DateTime StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool CleanShutdown { get; set; }
    public bool PreviousRunEndedUnexpectedly { get; set; }
}
