using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class PotentialDuplicateCandidate
{
    public Guid Id { get; set; }
    public DateTime DetectedAt { get; set; }
    public string JournalPath { get; set; } = string.Empty;
    public string ServerName { get; set; } = "Unknown Server";
    public string Evidence { get; set; } = string.Empty;
    public List<string> Lines { get; set; } = new();
    public List<string> HistoricalLines { get; set; } = new();
    public bool Reviewed { get; set; }
    public bool Removed { get; set; }
}

public sealed class PotentialDuplicateCandidateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal async Task<PotentialDuplicateCandidate> RecordAsync(
        Guid candidateId,
        string journalPath,
        ServerSessionInfo server,
        IReadOnlyList<CapturedChatLine> incoming,
        IReadOnlyList<string> committedHistory,
        CaptureReplayDecision decision,
        CancellationToken cancellationToken)
    {
        var candidate = new PotentialDuplicateCandidate
        {
            Id = candidateId,
            DetectedAt = DateTime.Now,
            JournalPath = journalPath,
            ServerName = server.DisplayName,
            Evidence = decision.Evidence,
            Lines = incoming
                .Skip(decision.CandidateStartIndex)
                .Take(decision.CandidateCount)
                .Select(line => line.Text)
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
