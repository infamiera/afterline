using System.Text.Json;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public sealed class PotentialDuplicateCandidate
{
    public Guid Id { get; set; }
    public DateTime DetectedAt { get; set; }
    public string JournalPath { get; set; } = string.Empty;
    public string ServerName { get; set; } = "Unknown Server";
    public string Evidence { get; set; } = string.Empty;
    // These offsets identify the exact two ranges that the scanner compared.
    // They let a later review remove the replay even when the same wording also
    // appears elsewhere in a busy chatlog.
    public int CandidateStartLine { get; set; } = -1;
    public int HistoricalStartLine { get; set; } = -1;
    public List<string> Lines { get; set; } = new();
    public List<string> HistoricalLines { get; set; } = new();
    public bool Reviewed { get; set; }
    public bool Removed { get; set; }
}

public sealed class PotentialDuplicateCandidateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Regex TimestampPrefix = new(
        @"^\[\d{1,2}:\d{2}:\d{2}\]\s*",
        RegexOptions.Compiled);
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal async Task<PotentialDuplicateCandidate> RecordAsync(
        Guid candidateId,
        string journalPath,
        ServerSessionInfo server,
        IReadOnlyList<string> incoming,
        IReadOnlyList<string> committedHistory,
        CaptureReplayDecision decision,
        CancellationToken cancellationToken)
    {
        DateTime detectedAt = DateTime.Now;
        var candidate = new PotentialDuplicateCandidate
        {
            Id = candidateId,
            DetectedAt = detectedAt,
            JournalPath = journalPath,
            ServerName = server.DisplayName,
            Evidence = decision.Evidence,
            CandidateStartLine = -1,
            HistoricalStartLine = -1,
            Lines = incoming
                .Skip(decision.CandidateStartIndex)
                .Take(decision.CandidateCount)
                // These are the timestamp-normalized values that were written
                // to the journal, so a confirmed cleanup can match precisely.
                .Select(line => line)
                .ToList(),
            HistoricalLines = committedHistory
                .Skip(decision.HistoricalStartIndex)
                .Take(decision.CandidateCount)
                .ToList()
        };

        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<PotentialDuplicateCandidate> candidates = await ReadCoreAsync(cancellationToken);
            candidates.Add(candidate);
            await WriteCoreAsync(candidates, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return candidate;
    }

    internal async Task<IReadOnlyList<PotentialDuplicateCandidate>> RecordExistingLogMatchesAsync(
        string journalPath,
        string serverName,
        IReadOnlyList<string> allLines,
        IReadOnlyList<ExistingLogReplayMatch> matches,
        CancellationToken cancellationToken)
    {
        var recorded = new List<PotentialDuplicateCandidate>(matches.Count);
        List<PotentialDuplicateCandidate> candidates;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            candidates = await ReadCoreAsync(cancellationToken);
            var refreshedIds = new HashSet<Guid>();
            foreach (ExistingLogReplayMatch match in matches)
            {
                var refreshed = new PotentialDuplicateCandidate
                {
                    Id = Guid.NewGuid(),
                    DetectedAt = DateTime.Now,
                    JournalPath = journalPath,
                    ServerName = serverName,
                    Evidence = match.Evidence,
                    CandidateStartLine = match.CandidateStartIndex,
                    HistoricalStartLine = match.HistoricalStartIndex,
                    Lines = allLines.Skip(match.CandidateStartIndex).Take(match.CandidateCount).ToList(),
                    HistoricalLines = allLines.Skip(match.HistoricalStartIndex).Take(match.CandidateCount).ToList()
                };

                // Rebase old cards against the file currently on disk. This
                // repairs candidates saved by earlier builds with a different
                // timestamp representation and prevents review-card buildup.
                PotentialDuplicateCandidate? existing = candidates.FirstOrDefault(existing =>
                    !existing.Reviewed &&
                    !existing.Removed &&
                    !refreshedIds.Contains(existing.Id) &&
                    string.Equals(existing.JournalPath, journalPath, StringComparison.OrdinalIgnoreCase) &&
                    SameBodies(existing.Lines, refreshed.Lines));
                if (existing is not null)
                {
                    existing.DetectedAt = refreshed.DetectedAt;
                    existing.ServerName = refreshed.ServerName;
                    existing.Evidence = refreshed.Evidence;
                    existing.CandidateStartLine = refreshed.CandidateStartLine;
                    existing.HistoricalStartLine = refreshed.HistoricalStartLine;
                    existing.Lines = refreshed.Lines;
                    existing.HistoricalLines = refreshed.HistoricalLines;
                    refreshedIds.Add(existing.Id);
                    recorded.Add(existing);
                }
                else
                {
                    candidates.Add(refreshed);
                    refreshedIds.Add(refreshed.Id);
                    recorded.Add(refreshed);
                }
            }

            // Any unmatched pending card is stale rather than a new deletion
            // target. Marking its metadata reviewed never changes chat text.
            foreach (PotentialDuplicateCandidate stale in candidates.Where(candidate =>
                         !candidate.Reviewed &&
                         !candidate.Removed &&
                         string.Equals(candidate.JournalPath, journalPath, StringComparison.OrdinalIgnoreCase) &&
                         !refreshedIds.Contains(candidate.Id)))
            {
                stale.Reviewed = true;
            }

            await WriteCoreAsync(candidates, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return recorded;
    }

    private static bool SameBodies(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
        => left.Count == right.Count && left
            .Select(NormalizeBody)
            .SequenceEqual(right.Select(NormalizeBody), StringComparer.Ordinal);

    private static string NormalizeBody(string line)
        => TimestampPrefix.Replace(line ?? string.Empty, string.Empty).Trim();

    public async Task<IReadOnlyList<PotentialDuplicateCandidate>> ReadPendingAsync(
        string? journalPath,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IEnumerable<PotentialDuplicateCandidate> candidates =
                await ReadCoreAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(journalPath))
            {
                candidates = candidates.Where(candidate => string.Equals(
                    candidate.JournalPath,
                    journalPath,
                    StringComparison.OrdinalIgnoreCase));
            }

            return candidates
                .Where(candidate => !candidate.Reviewed && !candidate.Removed)
                .OrderBy(candidate => candidate.DetectedAt)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkReviewedAsync(
        IEnumerable<Guid> candidateIds,
        bool removed,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> ids = candidateIds.ToHashSet();
        if (ids.Count == 0) return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<PotentialDuplicateCandidate> candidates = await ReadCoreAsync(cancellationToken);
            foreach (PotentialDuplicateCandidate candidate in candidates.Where(candidate => ids.Contains(candidate.Id)))
            {
                candidate.Reviewed = true;
                candidate.Removed = removed;
            }
            await WriteCoreAsync(candidates, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<List<PotentialDuplicateCandidate>> ReadCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.PotentialDuplicateCandidatesFile))
            return new List<PotentialDuplicateCandidate>();

        try
        {
            await using FileStream stream = new(
                AppPaths.PotentialDuplicateCandidatesFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<List<PotentialDuplicateCandidate>>(
                       stream,
                       JsonOptions,
                       cancellationToken) ?? new List<PotentialDuplicateCandidate>();
        }
        catch (JsonException ex)
        {
            DiagnosticLogger.Error("Potential duplicate review data could not be read.", ex);
            return new List<PotentialDuplicateCandidate>();
        }
    }

    private static async Task WriteCoreAsync(
        IReadOnlyList<PotentialDuplicateCandidate> candidates,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.PotentialDuplicateCandidatesFile)!);
        string temporary = AppPaths.PotentialDuplicateCandidatesFile + $".{Environment.ProcessId}.tmp";
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
                await JsonSerializer.SerializeAsync(
                    stream,
                    candidates,
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            File.Move(temporary, AppPaths.PotentialDuplicateCandidatesFile, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
        }
    }
}
