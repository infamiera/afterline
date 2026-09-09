using System.Text;
using Afterline.Models;

namespace Afterline.Services;

public sealed record PotentialDuplicateCleanupResult(
    int RemovedLineCount,
    string BackupPath);

public static class PotentialDuplicateCleanupService
{
    public static async Task<PotentialDuplicateCleanupResult> RemoveAsync(
        string journalPath,
        IReadOnlyList<PotentialDuplicateCandidate> candidates,
        CancellationToken cancellationToken,
        bool resolveAllConfirmedReplays = false)
    {
        if (!File.Exists(journalPath))
            throw new FileNotFoundException("The chatlog selected for duplicate review no longer exists.", journalPath);
        if (candidates.Count == 0)
            throw new InvalidOperationException("No potential duplicate ranges were selected.");
        if (candidates.Any(candidate => !string.Equals(
                candidate.JournalPath,
                journalPath,
                StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The selected duplicate ranges do not belong to this chatlog.");

        string originalText = await File.ReadAllTextAsync(journalPath, cancellationToken);
        string newline = originalText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        bool endedWithNewline = originalText.EndsWith("\n", StringComparison.Ordinal);
        string normalized = originalText.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        if (endedWithNewline && lines.Length > 0 && lines[^1].Length == 0)
            lines = lines[..^1];

        var removals = new HashSet<int>();
        foreach (PotentialDuplicateCandidate candidate in candidates)
        {
            if (candidate.Lines.Count == 0)
                throw new InvalidOperationException("A potential duplicate range contains no recoverable lines.");

            int? locatedStart = FindCandidateStart(lines, candidate);
            if (locatedStart is null)
            {
                List<int> matches = FindSequenceStarts(lines, candidate.Lines);
                throw new InvalidOperationException(
                    matches.Count == 0
                        ? "A highlighted range no longer matches the chatlog. Nothing was changed."
                        : "A highlighted range is ambiguous in the chatlog. Nothing was changed.");
            }

            int start = locatedStart.Value;
            for (int index = 0; index < candidate.Lines.Count; index++)
            {
                if (!removals.Add(start + index))
                    throw new InvalidOperationException("Potential duplicate ranges overlap. Nothing was changed.");
            }
        }

        IReadOnlyDictionary<int, ChatColorLineRecord> exactColors =
            await ChatColorSidecarService.MatchLinesAsync(
                journalPath,
                lines,
                cancellationToken);
        (string Line, int OriginalIndex)[] retained = lines
            .Select((line, index) => (Line: line, OriginalIndex: index))
            .Where(item => !removals.Contains(item.OriginalIndex))
            .ToArray();
        if (resolveAllConfirmedReplays)
        {
            // A single protected action must converge. Re-scan the remaining
            // text until no independently confirmed replay remains, keeping all
            // comparison work off the UI thread. The replay guard still requires
            // ordered, varied text plus collapsed timestamp evidence.
            retained = await Task.Run(
                () => RemoveAllRemainingConfirmedReplays(retained),
                cancellationToken);
        }
        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);
        string stem = Path.GetFileNameWithoutExtension(journalPath);
        string backupPath = UniquePath(
            AppPaths.RecoveryBackupsDirectory,
            $"Duplicate Review Backup [{DateTime.Now:yyyy-MM-dd - HH-mm-ss}] {stem}",
            ".txt");
        File.Copy(journalPath, backupPath, false);
        ChatColorSidecarService.CopyForTextFile(journalPath, backupPath, overwrite: false);

        string temporary = journalPath + $".{Environment.ProcessId}.duplicate-review.tmp";
        try
        {
            string replacement = string.Join(newline, retained.Select(item => item.Line));
            if (endedWithNewline)
                replacement += newline;
            await File.WriteAllTextAsync(
                temporary,
                replacement,
                new UTF8Encoding(false),
                cancellationToken);
            File.Move(temporary, journalPath, true);

            try
            {
                ChatColorLineRecord[] retainedColors = retained
                    .Where(item => exactColors.ContainsKey(item.OriginalIndex))
                    .Select(item => exactColors[item.OriginalIndex])
                    .ToArray();
                await ChatColorSidecarService.ReplaceAsync(
                    journalPath,
                    retainedColors,
                    CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The reviewed text edit remains authoritative and its complete
                // color sidecar is preserved with the backup.
                DiagnosticLogger.Error(
                    "Duplicate cleanup succeeded, but exact Log Reader colors could not be rebuilt.",
                    ex);
            }

            return new PotentialDuplicateCleanupResult(lines.Length - retained.Length, backupPath);
        }
        catch
        {
            // The untouched backup is deliberately retained if replacement fails.
            throw;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
        }
    }

    internal static async Task RunSmokeTestAsync(
        string testRoot,
        CancellationToken cancellationToken)
    {
        string folder = Path.Combine(testRoot, "duplicate-review-smoke");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Chatlog [Duplicate Review Smoke].txt");
        string[] original =
        {
            "[14:53:02] (( PM from (196) Player: hi ))",
            "[14:53:03] (( PM from (196) Player: hi ))",
            "[15:00:00] Original context one.",
            "[15:00:30] Original context two.",
            "[16:33:00] Original context one.",
            "[16:33:00] Original context two.",
            "[16:34:00] Genuine line after the candidate."
        };
        await File.WriteAllLinesAsync(path, original, new UTF8Encoding(false), cancellationToken);
        var candidate = new PotentialDuplicateCandidate
        {
            Id = Guid.NewGuid(),
            JournalPath = path,
            CandidateStartLine = 4,
            HistoricalStartLine = 2,
            Lines = new List<string> { original[4], original[5] }
        };

        PotentialDuplicateCleanupResult result = await RemoveAsync(
            path,
            new[] { candidate },
            cancellationToken);
        string[] repaired = await File.ReadAllLinesAsync(path, cancellationToken);
        if (result.RemovedLineCount != 2 ||
            repaired.Length != 5 ||
            !repaired.Contains(original[0], StringComparer.Ordinal) ||
            !repaired.Contains(original[1], StringComparer.Ordinal) ||
            !repaired.Contains(original[6], StringComparer.Ordinal) ||
            !File.Exists(result.BackupPath))
        {
            throw new InvalidOperationException(
                "User-confirmed duplicate cleanup did not preserve legitimate repeated lines and its backup.");
        }

        string ambiguousPath = Path.Combine(folder, "Chatlog [Ambiguous Review Smoke].txt");
        string[] ambiguousLines =
        {
            "[14:00:00] Same candidate line.",
            "[14:00:01] Same candidate ending.",
            "[14:00:00] Same candidate line.",
            "[14:00:01] Same candidate ending."
        };
        await File.WriteAllLinesAsync(
            ambiguousPath,
            ambiguousLines,
            new UTF8Encoding(false),
            cancellationToken);
        var ambiguous = new PotentialDuplicateCandidate
        {
            Id = Guid.NewGuid(),
            JournalPath = ambiguousPath,
            Lines = ambiguousLines.Take(2).ToList()
        };
        bool rejected = false;
        try
        {
            await RemoveAsync(ambiguousPath, new[] { ambiguous }, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        string[] untouched = await File.ReadAllLinesAsync(ambiguousPath, cancellationToken);
        if (!rejected || !untouched.SequenceEqual(ambiguousLines, StringComparer.Ordinal))
            throw new InvalidOperationException("Ambiguous duplicate cleanup changed an authoritative chatlog.");

        string exhaustivePath = Path.Combine(folder, "Chatlog [Exhaustive Review Smoke].txt");
        string[] originalScene = Enumerable.Range(0, CaptureReplayGuard.MinimumReplayLines)
            .Select(index => $"[18:{index / 60:00}:{index % 60:00}] Original scene row {index}.")
            .ToArray();
        string[] replayScene = Enumerable.Range(0, CaptureReplayGuard.MinimumReplayLines)
            .Select(index => $"[19:00:{index / 12:00}] Original scene row {index}.")
            .ToArray();
        await File.WriteAllLinesAsync(
            exhaustivePath,
            originalScene.Concat(replayScene).Concat(replayScene),
            new UTF8Encoding(false),
            cancellationToken);
        var firstReplay = new PotentialDuplicateCandidate
        {
            Id = Guid.NewGuid(),
            JournalPath = exhaustivePath,
            CandidateStartLine = originalScene.Length,
            HistoricalStartLine = 0,
            Lines = replayScene.ToList(),
            HistoricalLines = originalScene.ToList()
        };
        PotentialDuplicateCleanupResult exhaustive = await RemoveAsync(
            exhaustivePath,
            new[] { firstReplay },
            cancellationToken,
            resolveAllConfirmedReplays: true);
        string[] exhaustiveRepaired = await File.ReadAllLinesAsync(exhaustivePath, cancellationToken);
        if (exhaustive.RemovedLineCount != replayScene.Length * 2 ||
            !exhaustiveRepaired.SequenceEqual(originalScene, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Resolve all confirmed replays did not remove every independently proven replay in one pass.");
        }
    }

    private static List<int> FindSequenceStarts(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> sequence)
    {
        var starts = new List<int>();
        for (int start = 0; start <= lines.Count - sequence.Count; start++)
        {
            bool match = true;
            for (int index = 0; index < sequence.Count; index++)
            {
                if (string.Equals(lines[start + index], sequence[index], StringComparison.Ordinal))
                    continue;
                match = false;
                break;
            }
            if (match) starts.Add(start);
        }
        return starts;
    }

    private static int? FindCandidateStart(
        IReadOnlyList<string> lines,
        PotentialDuplicateCandidate candidate)
    {
        // Fresh scans persist the exact file row. Prefer it: a duplicate is
        // expected to have an earlier textual twin, so text-only matching is
        // inherently ambiguous for the very situation we are fixing.
        if (candidate.CandidateStartLine >= 0 &&
            SequenceMatches(lines, candidate.CandidateStartLine, candidate.Lines))
            return candidate.CandidateStartLine;

        List<int> matches = FindSequenceStarts(lines, candidate.Lines);
        if (matches.Count == 1)
            return matches[0];

        // Upgrade cards created before offsets were stored. Only choose a
        // later occurrence when exactly one candidate is paired with the saved
        // earlier scene in the same 100-line comparison context. Otherwise
        // remain safely ambiguous and leave the file untouched.
        if (matches.Count > 1 && candidate.HistoricalLines.Count > 0)
        {
            List<int> historicalMatches = FindSequenceStarts(lines, candidate.HistoricalLines);
            int[] paired = matches.Where(candidateStart => historicalMatches.Count(historyStart =>
                    historyStart < candidateStart &&
                    candidateStart - historyStart <= CaptureReplayGuard.HistoryLimit) == 1)
                .ToArray();
            if (paired.Length == 1)
                return paired[0];
        }

        return null;
    }

    private static bool SequenceMatches(
        IReadOnlyList<string> lines,
        int start,
        IReadOnlyList<string> sequence)
    {
        if (start < 0 || sequence.Count == 0 || start > lines.Count - sequence.Count)
            return false;
        for (int index = 0; index < sequence.Count; index++)
        {
            if (!string.Equals(lines[start + index], sequence[index], StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static (string Line, int OriginalIndex)[] RemoveAllRemainingConfirmedReplays(
        (string Line, int OriginalIndex)[] initial)
    {
        var working = initial.ToList();
        while (true)
        {
            IReadOnlyList<ExistingLogReplayMatch> matches = CaptureReplayGuard.FindInExistingLog(
                working.Select(item => item.Line).ToArray());
            if (matches.Count == 0)
                return working.ToArray();

            var remove = new HashSet<int>();
            foreach (ExistingLogReplayMatch match in matches)
            {
                for (int offset = 0; offset < match.CandidateCount; offset++)
                    remove.Add(match.CandidateStartIndex + offset);
            }

            if (remove.Count == 0)
                return working.ToArray();
            foreach (int index in remove.OrderByDescending(index => index))
            {
                if (index >= 0 && index < working.Count)
                    working.RemoveAt(index);
            }
        }
    }

    private static string UniquePath(string folder, string baseName, string extension)
    {
        string path = Path.Combine(folder, baseName + extension);
        if (!File.Exists(path)) return path;
        for (int suffix = 2; ; suffix++)
        {
            string candidate = Path.Combine(folder, $"{baseName} ({suffix}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
