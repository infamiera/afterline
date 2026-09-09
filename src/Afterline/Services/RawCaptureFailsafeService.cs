using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Afterline.Models;

namespace Afterline.Services;

public sealed class RawCaptureFailsafeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions EventJournalJsonOptions = new()
    {
        WriteIndented = false
    };
    private static readonly byte[] NewLine = { (byte)'\n' };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _lastServerKey = string.Empty;
    private string[] _lastLines = Array.Empty<string>();
    private RawCaptureSnapshot? _lastSnapshot;
    private bool _snapshotNeedsProcessedMark;
    private string _runId = string.Empty;
    private string? _interruptedRunId;

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
            _interruptedRunId = previousUnexpected ? previous?.RunId : null;
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
                    _lastLines = snapshot.GetCapturedLines()
                        .Select(line => line.Text)
                        .ToArray();
                    _snapshotNeedsProcessedMark = true;
                }
            }

            var manifest = new CaptureRunManifest
            {
                RunId = Guid.NewGuid().ToString("N"),
                StartedAt = DateTime.Now,
                CleanShutdown = false,
                PreviousRunEndedUnexpectedly = previousUnexpected
            };
            _runId = manifest.RunId;
            await WriteJsonAtomicAsync(AppPaths.CaptureRunStateFile, manifest, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> WriteSnapshotAsync(
        IReadOnlyList<CapturedChatLine> lines,
        ServerSessionInfo server,
        CancellationToken cancellationToken,
        bool isEventStream = false,
        long eventSequence = 0,
        DateTimeOffset? observedAtUtc = null,
        long readerGeneration = 0)
    {
        string serverKey = server.StableKey;
        // A player may legitimately repeat an identical message. Event-stream
        // checkpoints therefore must never be coalesced by visible text.
        if (!isEventStream && MatchesLastSnapshot(lines, serverKey))
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!isEventStream && MatchesLastSnapshot(lines, serverKey))
                return null;

            CapturedChatLine[] normalized = NormalizeLines(lines);
            DateTimeOffset observedAt = observedAtUtc ?? DateTimeOffset.UtcNow;
            string? eventId = null;
            string? payloadHash = null;
            if (isEventStream)
            {
                string runId = string.IsNullOrWhiteSpace(_runId)
                    ? "legacy"
                    : _runId;
                eventId = $"{runId}:{readerGeneration}:{eventSequence}";
                payloadHash = CreatePayloadHash(normalized);
                await AppendEventRecordAsync(
                    new CaptureEventJournalRecord
                    {
                        Kind = CaptureEventRecordKind.Observed,
                        EventId = eventId,
                        RunId = runId,
                        ReaderGeneration = readerGeneration,
                        EventSequence = eventSequence,
                        ObservedAtUtc = observedAt,
                        CapturedAtUtc = DateTimeOffset.UtcNow,
                        ServerName = server.DisplayName,
                        ServerAddress = server.Address,
                        ServerKey = serverKey,
                        PayloadHash = payloadHash,
                        Lines = normalized.ToList()
                    },
                    cancellationToken);

                // The append-only record is the durable pre-parse source for
                // direct observer events. Avoid also rewriting the complete
                // JSON snapshot on every chat row; that would turn busy scenes
                // into unnecessary disk work without adding recovery value.
                return eventId;
            }
            var snapshot = new RawCaptureSnapshot
            {
                CapturedAt = DateTime.Now,
                ServerName = server.DisplayName,
                ServerAddress = server.Address,
                ServerKey = serverKey,
                IsEventStream = isEventStream,
                EventSequence = eventSequence,
                ReaderGeneration = readerGeneration,
                ObservedAtUtc = observedAt,
                EventId = eventId,
                PayloadHash = payloadHash,
                Lines = normalized.Select(line => line.Text).ToList(),
                StyledLines = normalized.ToList()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.RawCaptureCacheFile)!);
            string temp = AppPaths.RawCaptureCacheFile + ".tmp";
            await WriteJsonFileAsync(temp, snapshot, cancellationToken);

            if (File.Exists(AppPaths.RawCaptureCacheFile))
                File.Copy(AppPaths.RawCaptureCacheFile, AppPaths.RawCapturePreviousCacheFile, true);

            File.Move(temp, AppPaths.RawCaptureCacheFile, true);
            _lastServerKey = serverKey;
            _lastLines = normalized.Select(line => line.Text).ToArray();
            _lastSnapshot = snapshot;
            _snapshotNeedsProcessedMark = true;
            return eventId;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkEventProcessedAsync(
        string? eventId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await AppendEventRecordAsync(
                new CaptureEventJournalRecord
                {
                    Kind = CaptureEventRecordKind.Processed,
                    EventId = eventId,
                    CapturedAtUtc = DateTimeOffset.UtcNow
                },
                cancellationToken);
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
            await CompactProcessedEventRecordsAsync(cancellationToken);
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

    public async Task<IReadOnlyList<RawCaptureSnapshot>> ReadInterruptedEventCheckpointsAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(_interruptedRunId))
                return Array.Empty<RawCaptureSnapshot>();

            IReadOnlyList<CaptureEventJournalRecord> records =
                await ReadEventRecordsAsync(cancellationToken);
            var observed = records
                .Where(record => record.Kind == CaptureEventRecordKind.Observed &&
                                 string.Equals(record.RunId, _interruptedRunId, StringComparison.Ordinal) &&
                                 !string.IsNullOrWhiteSpace(record.EventId))
                .GroupBy(record => record.EventId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(record => record.CapturedAtUtc).First())
                .ToDictionary(record => record.EventId, StringComparer.Ordinal);
            var processed = records
                .Where(record => record.Kind == CaptureEventRecordKind.Processed)
                .Select(record => record.EventId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            return observed.Values
                .Where(record => !processed.Contains(record.EventId))
                .OrderBy(record => record.ReaderGeneration)
                .ThenBy(record => record.EventSequence)
                .ThenBy(record => record.ObservedAtUtc)
                .Select(record => new RawCaptureSnapshot
                {
                    CapturedAt = record.CapturedAtUtc.LocalDateTime,
                    ServerName = record.ServerName,
                    ServerAddress = record.ServerAddress,
                    ServerKey = record.ServerKey,
                    IsEventStream = true,
                    EventSequence = record.EventSequence,
                    ReaderGeneration = record.ReaderGeneration,
                    ObservedAtUtc = record.ObservedAtUtc,
                    EventId = record.EventId,
                    PayloadHash = record.PayloadHash,
                    Lines = record.Lines.Select(line => line.Text).ToList(),
                    StyledLines = record.Lines
                })
                .ToArray();
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

    internal static void RunEventCheckpointSmokeTest()
    {
        var source = new RawCaptureSnapshot
        {
            CapturedAt = DateTime.Today.AddHours(18),
            ServerKey = "127.0.0.1:30120",
            IsEventStream = true,
            EventSequence = 42,
            ReaderGeneration = 3,
            EventId = "run:3:42",
            Lines = new List<string> { "Bianca says: Immediate event." }
        };
        RawCaptureSnapshot? restored = JsonSerializer.Deserialize<RawCaptureSnapshot>(
            JsonSerializer.Serialize(source, JsonOptions),
            JsonOptions);
        if (restored is null ||
            !restored.IsEventStream ||
            restored.EventSequence != source.EventSequence ||
            restored.ReaderGeneration != source.ReaderGeneration ||
            restored.EventId != source.EventId ||
            restored.Lines.Count != 1)
        {
            throw new InvalidOperationException(
                "The durable FiveM event checkpoint lost its stream provenance.");
        }

        var eventRecord = new CaptureEventJournalRecord
        {
            Kind = CaptureEventRecordKind.Observed,
            EventId = source.EventId!,
            RunId = "run",
            ReaderGeneration = source.ReaderGeneration,
            EventSequence = source.EventSequence,
            ObservedAtUtc = DateTimeOffset.UtcNow,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Lines = new List<CapturedChatLine>
            {
                new("Bianca says: Immediate event.")
            }
        };
        string journalLine = JsonSerializer.Serialize(eventRecord, EventJournalJsonOptions);
        CaptureEventJournalRecord? restoredRecord = JsonSerializer.Deserialize<CaptureEventJournalRecord>(
            journalLine,
            EventJournalJsonOptions);
        if (journalLine.Contains('\n') ||
            restoredRecord?.EventId != eventRecord.EventId ||
            restoredRecord.Lines.Count != 1)
        {
            throw new InvalidOperationException(
                "The durable FiveM event journal is not single-line recoverable.");
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
            IReadOnlyList<CapturedChatLine> capturedLines = snapshot.GetCapturedLines();

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

            foreach (CapturedChatLine line in capturedLines)
                await writer.WriteLineAsync(line.Text.AsMemory(), cancellationToken);

            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return destination;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool MatchesLastSnapshot(IReadOnlyList<CapturedChatLine> lines, string serverKey)
    {
        if (!string.Equals(_lastServerKey, serverKey, StringComparison.Ordinal))
            return false;

        int normalizedIndex = 0;
        foreach (CapturedChatLine line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            if (normalizedIndex >= _lastLines.Length ||
                !string.Equals(_lastLines[normalizedIndex], line.Text.Trim(), StringComparison.Ordinal))
                return false;

            normalizedIndex++;
        }

        return normalizedIndex == _lastLines.Length;
    }

    private static CapturedChatLine[] NormalizeLines(IReadOnlyList<CapturedChatLine> lines)
    {
        var normalized = new List<CapturedChatLine>(lines.Count);
        foreach (CapturedChatLine line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            string source = line.Text;
            string text = source.Trim();
            int start = source.IndexOf(text, StringComparison.Ordinal);
            IReadOnlyList<ChatColorRun> runs = ChatColorData.SliceRuns(
                source,
                line.ColorRuns,
                Math.Max(0, start),
                text.Length);
            normalized.Add(new CapturedChatLine(text, runs));
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

    private static string CreatePayloadHash(IReadOnlyList<CapturedChatLine> lines)
    {
        string payload = string.Join("\u001f", lines.Select(line => line.Text));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static async Task AppendEventRecordAsync(
        CaptureEventJournalRecord record,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.EventCaptureJournalFile)!);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(record, EventJournalJsonOptions);
        await using FileStream stream = new(
            AppPaths.EventCaptureJournalFile,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.WriteAsync(NewLine, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CaptureEventJournalRecord>> ReadEventRecordsAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.EventCaptureJournalFile))
            return Array.Empty<CaptureEventJournalRecord>();

        var records = new List<CaptureEventJournalRecord>();
        try
        {
            await using FileStream stream = new(
                AppPaths.EventCaptureJournalFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 16 * 1024);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    CaptureEventJournalRecord? record = JsonSerializer.Deserialize<CaptureEventJournalRecord>(line, EventJournalJsonOptions);
                    if (record is not null) records.Add(record);
                }
                catch (JsonException)
                {
                    // An interrupted final append is not a valid checkpoint and
                    // must never prevent recovery of earlier complete records.
                }
            }
        }
        catch (IOException)
        {
            return Array.Empty<CaptureEventJournalRecord>();
        }

        return records;
    }

    private static async Task CompactProcessedEventRecordsAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CaptureEventJournalRecord> records =
            await ReadEventRecordsAsync(cancellationToken);
        if (records.Count == 0) return;

        var processed = records
            .Where(record => record.Kind == CaptureEventRecordKind.Processed)
            .Select(record => record.EventId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        CaptureEventJournalRecord[] pending = records
            .Where(record => record.Kind == CaptureEventRecordKind.Observed &&
                             !string.IsNullOrWhiteSpace(record.EventId) &&
                             !processed.Contains(record.EventId))
            .GroupBy(record => record.EventId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(record => record.CapturedAtUtc).First())
            .OrderBy(record => record.CapturedAtUtc)
            .ToArray();

        string temporary = AppPaths.EventCaptureJournalFile + ".tmp";
        try
        {
            await using (FileStream stream = new(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                foreach (CaptureEventJournalRecord record in pending)
                {
                    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(record, EventJournalJsonOptions);
                    await stream.WriteAsync(payload, cancellationToken);
                    await stream.WriteAsync(NewLine, cancellationToken);
                }
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, AppPaths.EventCaptureJournalFile, true);
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
            // Retaining processed records is safe; compaction is only an
            // end-of-run storage optimization and must never affect capture.
        }
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
    public bool IsEventStream { get; set; }
    public long EventSequence { get; set; }
    public long ReaderGeneration { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public string? EventId { get; set; }
    public string? PayloadHash { get; set; }
    public List<string> Lines { get; set; } = new();
    public List<CapturedChatLine> StyledLines { get; set; } = new();

    public IReadOnlyList<CapturedChatLine> GetCapturedLines()
    {
        if (StyledLines.Count > 0)
            return StyledLines
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .Select(line => new CapturedChatLine(line.Text, line.ColorRuns))
                .ToArray();

        return Lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new CapturedChatLine(line.Trim()))
            .ToArray();
    }
}

public sealed class CaptureRunManifest
{
    public string RunId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool CleanShutdown { get; set; }
    public bool PreviousRunEndedUnexpectedly { get; set; }
}

public enum CaptureEventRecordKind
{
    Observed,
    Processed
}

public sealed class CaptureEventJournalRecord
{
    public CaptureEventRecordKind Kind { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public long ReaderGeneration { get; set; }
    public long EventSequence { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string ServerName { get; set; } = "Unknown Server";
    public string? ServerAddress { get; set; }
    public string ServerKey { get; set; } = "unknown";
    public string? PayloadHash { get; set; }
    public List<CapturedChatLine> Lines { get; set; } = new();
}
