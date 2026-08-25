using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public sealed class ArchiveService
{
    private static readonly Regex ArchiveName = new(
        @"^Chatlog \[.+\] \[(?<date>\d{2}-[A-Za-z]+-\d{4})\](?: \(\d+\))?\.txt$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions IndexJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _rebuildGate = new(1, 1);

    public async Task<IReadOnlyList<SessionIndexEntry>> RebuildIndexAsync(
        string root,
        CancellationToken cancellationToken,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? maxEntries = null)
    {
        if (maxEntries is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        await _rebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTime? normalizedFrom = fromDate?.Date;
            DateTime? normalizedTo = toDate?.Date;
            bool filtered = normalizedFrom.HasValue || normalizedTo.HasValue;

            IReadOnlyList<SessionIndexEntry> cached = LoadCachedIndex()
                .Where(entry => IsInsideRoot(entry.FilePath, root))
                .ToArray();
            var cachedByPath = cached
                .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var candidates = new List<ArchiveCandidate>();
            PriorityQueue<ArchiveCandidate, DateTime>? newestCandidates = maxEntries.HasValue
                ? new PriorityQueue<ArchiveCandidate, DateTime>(maxEntries.Value + 1)
                : null;
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (file.Contains(
                            $"{Path.DirectorySeparatorChar}.active{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var info = new FileInfo(file);
                    DateTime archiveDate = ResolveArchiveDate(file, info.LastWriteTime);
                    if (!MatchesDateRange(archiveDate, normalizedFrom, normalizedTo))
                        continue;

                    var candidate = new ArchiveCandidate(
                        file,
                        info.LastWriteTimeUtc,
                        info.Length);
                    if (newestCandidates is null)
                    {
                        candidates.Add(candidate);
                    }
                    else
                    {
                        newestCandidates.Enqueue(candidate, candidate.LastWriteUtc);
                        if (newestCandidates.Count > maxEntries!.Value)
                            newestCandidates.Dequeue();
                    }
                }
            }

            if (newestCandidates is not null)
            {
                candidates.AddRange(newestCandidates.UnorderedItems.Select(item => item.Element));
            }

            // Select the newest bounded set before opening any chatlog. This is
            // important for flat legacy archives: even an explicit "All dates"
            // view cannot make Afterline count every line in every file at once.
            candidates.Sort((left, right) => right.LastWriteUtc.CompareTo(left.LastWriteUtc));

            var refreshedEntries = new List<SessionIndexEntry>(candidates.Count);
            foreach (ArchiveCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string file = candidate.FilePath;
                try
                {
                    int lineCount;
                    if (cachedByPath.TryGetValue(file, out SessionIndexEntry? previous) &&
                        previous.LastWriteUtc == candidate.LastWriteUtc &&
                        previous.SizeBytes == candidate.SizeBytes)
                    {
                        lineCount = previous.LineCount;
                    }
                    else
                    {
                        lineCount = 0;
                        using var reader = new StreamReader(file);
                        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not null)
                            lineCount++;
                    }

                    refreshedEntries.Add(new SessionIndexEntry
                    {
                        FilePath = file,
                        LastWriteUtc = candidate.LastWriteUtc,
                        SizeBytes = candidate.SizeBytes,
                        LineCount = lineCount
                    });
                }
                catch (IOException ex)
                {
                    DiagnosticLogger.Error($"Unable to index chatlog '{file}'.", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    DiagnosticLogger.Error($"Unable to access chatlog '{file}'.", ex);
                }
            }

            refreshedEntries.Sort((left, right) => right.LastWriteUtc.CompareTo(left.LastWriteUtc));

            // A filtered refresh deliberately avoids touching old files. Preserve
            // their cached metadata so changing the filter does not force those
            // files to be read again unless they actually enter the visible range.
            List<SessionIndexEntry> completeIndex = maxEntries.HasValue
                ? cached.Concat(refreshedEntries)
                    .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(entry => entry.LastWriteUtc).First())
                    .ToList()
                : filtered
                    ? cached.Where(entry => !MatchesDateRange(
                            ResolveArchiveDate(entry.FilePath, entry.LastWriteUtc.ToLocalTime()),
                            normalizedFrom,
                            normalizedTo))
                        .Concat(refreshedEntries)
                        .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.OrderByDescending(entry => entry.LastWriteUtc).First())
                        .ToList()
                : refreshedEntries.ToList();
            completeIndex.Sort((left, right) => right.LastWriteUtc.CompareTo(left.LastWriteUtc));

            if (File.Exists(AppPaths.ArchiveIndexFile) && IndexesMatch(cached, completeIndex))
                return refreshedEntries;

            await WriteIndexAsync(completeIndex, cancellationToken).ConfigureAwait(false);
            return refreshedEntries;
        }
        finally
        {
            _rebuildGate.Release();
        }
    }

    private static bool MatchesDateRange(
        DateTime date,
        DateTime? fromDate,
        DateTime? toDate)
    {
        DateTime value = date.Date;
        if (fromDate is DateTime from && value < from) return false;
        if (toDate is DateTime to && value > to) return false;
        return true;
    }

    private sealed record ArchiveCandidate(
        string FilePath,
        DateTime LastWriteUtc,
        long SizeBytes);

    private static DateTime ResolveArchiveDate(string filePath, DateTime fallback)
    {
        Match match = ArchiveName.Match(Path.GetFileName(filePath));
        if (match.Success && DateTime.TryParseExact(
                match.Groups["date"].Value,
                "dd-MMMM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
        {
            return parsed.Date;
        }

        return fallback.ToLocalTime().Date;
    }

    private static bool IsInsideRoot(string filePath, string root)
    {
        try
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string normalizedFile = Path.GetFullPath(filePath);
            return normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IndexesMatch(
        IReadOnlyList<SessionIndexEntry> left,
        IReadOnlyList<SessionIndexEntry> right)
    {
        if (left.Count != right.Count) return false;
        for (int index = 0; index < left.Count; index++)
        {
            SessionIndexEntry existing = left[index];
            SessionIndexEntry current = right[index];
            if (!string.Equals(existing.FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase) ||
                existing.LastWriteUtc != current.LastWriteUtc ||
                existing.SizeBytes != current.SizeBytes ||
                existing.LineCount != current.LineCount)
                return false;
        }

        return true;
    }

    public IReadOnlyList<SessionIndexEntry> LoadCachedIndex()
    {
        try
        {
            if (!File.Exists(AppPaths.ArchiveIndexFile))
                return Array.Empty<SessionIndexEntry>();

            using FileStream stream = File.OpenRead(AppPaths.ArchiveIndexFile);
            List<SessionIndexEntry>? cached =
                JsonSerializer.Deserialize<List<SessionIndexEntry>>(stream, IndexJsonOptions);
            return cached is null ? Array.Empty<SessionIndexEntry>() : cached;
        }
        catch
        {
            return Array.Empty<SessionIndexEntry>();
        }
    }

    public async Task<bool> EnsureFileIndexedAsync(
        string root,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath) || !IsInsideRoot(filePath, root))
            return false;

        var info = new FileInfo(filePath);
        string normalized = Path.GetFullPath(filePath);
        bool IsCurrentEntry(SessionIndexEntry entry) =>
            PathsEqual(entry.FilePath, normalized) &&
            entry.LastWriteUtc == info.LastWriteTimeUtc &&
            entry.SizeBytes == info.Length &&
            entry.LineCount > 0;

        if (LoadCachedIndex().Any(IsCurrentEntry))
            return true;

        await _rebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<SessionIndexEntry> cached = LoadCachedIndex();
            if (cached.Any(IsCurrentEntry))
                return true;

            int lineCount = 0;
            using (var reader = new StreamReader(filePath))
            {
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not null)
                    lineCount++;
            }

            var current = new SessionIndexEntry
            {
                FilePath = normalized,
                LastWriteUtc = info.LastWriteTimeUtc,
                SizeBytes = info.Length,
                LineCount = lineCount
            };
            List<SessionIndexEntry> updated = cached
                .Where(entry => !PathsEqual(entry.FilePath, normalized))
                .Append(current)
                .OrderByDescending(entry => entry.LastWriteUtc)
                .ToList();
            await WriteIndexAsync(updated, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _rebuildGate.Release();
        }
    }

    private static async Task WriteIndexAsync(
        IReadOnlyList<SessionIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        AppPaths.EnsureLocalDirectories();
        string temp = AppPaths.ArchiveIndexFile + ".tmp";
        await using (FileStream stream = new(
            temp,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            16 * 1024,
            FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                entries,
                IndexJsonOptions,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, AppPaths.ArchiveIndexFile, true);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
