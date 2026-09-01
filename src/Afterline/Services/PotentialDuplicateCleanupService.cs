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
        CancellationToken cancellationToken)
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

            List<int> matches = FindSequenceStarts(lines, candidate.Lines);
            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    matches.Count == 0
                        ? "A highlighted range no longer matches the chatlog. Nothing was changed."
                        : "A highlighted range is ambiguous in the chatlog. Nothing was changed.");
            }

            int start = matches[0];
            for (int index = 0; index < candidate.Lines.Count; index++)
            {
                if (!removals.Add(start + index))
                    throw new InvalidOperationException("Potential duplicate ranges overlap. Nothing was changed.");
            }
        }

        string[] retained = lines.Where((_, index) => !removals.Contains(index)).ToArray();
        IReadOnlyDictionary<int, ChatColorLineRecord> retainedColors =
            await ChatColorSidecarService.MatchLinesAsync(
                journalPath,
                retained,
                cancellationToken);

        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);
        string stem = Path.GetFileNameWithoutExtension(journalPath);
        string backupPath = UniquePath(
            AppPaths.RecoveryBackupsDirectory,
            $"Duplicate Review Backup [{DateTime.Now:yyyy-MM-dd - HH-mm-ss}] {stem}",
            ".txt");
        File.Copy(journalPath, backupPath, false);
        ChatColorSidecarService.CopyForTextFile(journalPath, backupPath, false);

        string temporary = journalPath + $".{Environment.ProcessId}.duplicate-review.tmp";
        try
        {
            string replacement = string.Join(newline, retained);
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
                ChatColorSidecarService.DeleteForTextFile(journalPath);
                foreach ((int lineIndex, ChatColorLineRecord record) in retainedColors.OrderBy(pair => pair.Key))
                {
                    await ChatColorSidecarService.AppendAsync(
                        journalPath,
                        retained[lineIndex],
                        record.ColorRuns,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // Exact-color metadata is optional. The authoritative text and
                // its complete backup have already been written successfully.
                DiagnosticLogger.Error(
                    "The reviewed chatlog was updated, but optional exact-color metadata could not be rebuilt.",
                    ex);
            }

            return new PotentialDuplicateCleanupResult(removals.Count, backupPath);
        }
        catch
        {
            // The untouched backup is deliberately retained even if replacement
            // or optional color-sidecar reconstruction fails.
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
