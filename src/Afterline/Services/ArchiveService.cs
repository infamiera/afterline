using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public enum ArchiveScanMode
{
    FullRecursive,
    DatedFoldersOnly
}

public sealed record ArchiveScanProgress(
    string Phase,
    int DiscoveredFiles,
    int IndexedFiles);

public sealed class ArchiveService
{
    private static readonly Regex ArchiveName = new(
        @"^Chatlog \[.+\] \[(?<date>\d{2}-[A-Za-z]+-\d{4})\](?: \(\d+\))?\.txt$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions IndexJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly SemaphoreSlim RebuildGate = new(1, 1);
    private readonly TimeSpan _candidateInspectionDelay;
    private readonly Action<string>? _candidateObserver;

    public ArchiveService()
    {
    }

    internal ArchiveService(
        TimeSpan candidateInspectionDelay,
        Action<string>? candidateObserver = null)
    {
        _candidateInspectionDelay = candidateInspectionDelay;
        _candidateObserver = candidateObserver;
    }

    public async Task<IReadOnlyList<SessionIndexEntry>> RebuildIndexAsync(
        string root,
        CancellationToken cancellationToken,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? maxEntries = null,
        ArchiveScanMode scanMode = ArchiveScanMode.FullRecursive,
        IProgress<ArchiveScanProgress>? progress = null)
    {
        if (maxEntries is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        await RebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            int discoveredFiles = 0;
            progress?.Report(new ArchiveScanProgress("Discovering chatlogs", 0, 0));
            foreach (string file in EnumerateArchiveFiles(
                         root,
                         scanMode,
                         normalizedFrom,
                         normalizedTo))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_candidateInspectionDelay > TimeSpan.Zero)
                    await Task.Delay(_candidateInspectionDelay, cancellationToken).ConfigureAwait(false);
                _candidateObserver?.Invoke(file);

                if (file.Contains(
                        $"{Path.DirectorySeparatorChar}.active{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                discoveredFiles++;
                if (discoveredFiles % 100 == 0)
                    progress?.Report(new ArchiveScanProgress(
                        "Discovering chatlogs",
                        discoveredFiles,
                        0));

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

            if (newestCandidates is not null)
            {
                candidates.AddRange(newestCandidates.UnorderedItems.Select(item => item.Element));
            }

            // Select the newest bounded set before opening any chatlog. This is
            // important for flat legacy archives: even an explicit "All dates"
            // view cannot make Afterline count every line in every file at once.
            candidates.Sort((left, right) => right.LastWriteUtc.CompareTo(left.LastWriteUtc));

            var refreshedEntries = new List<SessionIndexEntry>(candidates.Count);
            int indexedFiles = 0;
            progress?.Report(new ArchiveScanProgress(
                "Updating archive index",
                discoveredFiles,
                0));
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
                        using var reader = OpenSharedReader(file);
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
                    indexedFiles++;
                    if (indexedFiles % 50 == 0 || indexedFiles == candidates.Count)
                        progress?.Report(new ArchiveScanProgress(
                            "Updating archive index",
                            discoveredFiles,
                            indexedFiles));
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
            {
                progress?.Report(new ArchiveScanProgress(
                    "Archive index ready",
                    discoveredFiles,
                    indexedFiles));
                return refreshedEntries;
            }

            await WriteIndexAsync(completeIndex, cancellationToken).ConfigureAwait(false);
            progress?.Report(new ArchiveScanProgress(
                "Archive index ready",
                discoveredFiles,
                indexedFiles));
            return refreshedEntries;
        }
        finally
        {
            RebuildGate.Release();
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

    private static IEnumerable<string> EnumerateArchiveFiles(
        string root,
        ArchiveScanMode scanMode,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (!Directory.Exists(root)) yield break;

        if (scanMode == ArchiveScanMode.FullRecursive ||
            fromDate is null ||
            toDate is null)
        {
            foreach (string file in Directory.EnumerateFiles(
                         root,
                         "*.txt",
                         SearchOption.AllDirectories))
                yield return file;
            yield break;
        }

        // Older Afterline versions could store chatlogs directly in the archive
        // root. Inspect that one legacy level without walking every subfolder.
        foreach (string file in Directory.EnumerateFiles(
                     root,
                     "*.txt",
                     SearchOption.TopDirectoryOnly))
            yield return file;

        DateTime cursor = new(fromDate.Value.Year, fromDate.Value.Month, 1);
        DateTime lastMonth = new(toDate.Value.Year, toDate.Value.Month, 1);
        while (cursor <= lastMonth)
        {
            string monthFolder = Path.Combine(
                root,
                cursor.ToString("yyyy", CultureInfo.InvariantCulture),
                cursor.ToString("MM - MMMM", CultureInfo.InvariantCulture));
            if (Directory.Exists(monthFolder))
            {
                foreach (string file in Directory.EnumerateFiles(
                             monthFolder,
                             "*.txt",
                             SearchOption.TopDirectoryOnly))
                    yield return file;
            }

            cursor = cursor.AddMonths(1);
        }
    }

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

            using FileStream stream = new(
                AppPaths.ArchiveIndexFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
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

        await RebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<SessionIndexEntry> cached = LoadCachedIndex();
            if (cached.Any(IsCurrentEntry))
                return true;

            int lineCount = 0;
            using (var reader = OpenSharedReader(filePath))
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
            RebuildGate.Release();
        }
    }

    private static async Task WriteIndexAsync(
        IReadOnlyList<SessionIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        AppPaths.EnsureLocalDirectories();
        string temp = AppPaths.ArchiveIndexFile + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(
                temp,
                FileMode.CreateNew,
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
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
        }
    }

    private static StreamReader OpenSharedReader(string path)
        => new(new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            16 * 1024,
            FileOptions.SequentialScan));

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
