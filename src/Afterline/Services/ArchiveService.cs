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
        DateTime? toDate = null)
    {
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

            var refreshedEntries = new List<SessionIndexEntry>();
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

                    int lineCount;
                    if (cachedByPath.TryGetValue(file, out SessionIndexEntry? previous) &&
                        previous.LastWriteUtc == info.LastWriteTimeUtc &&
                        previous.SizeBytes == info.Length)
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
                        LastWriteUtc = info.LastWriteTimeUtc,
                        SizeBytes = info.Length,
                        LineCount = lineCount
                    });
                }
            }

            refreshedEntries.Sort((left, right) => right.LastWriteUtc.CompareTo(left.LastWriteUtc));

            // A filtered refresh deliberately avoids touching old files. Preserve
            // their cached metadata so changing the filter does not force those
            // files to be read again unless they actually enter the visible range.
            List<SessionIndexEntry> completeIndex = filtered
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
                    completeIndex,
                    IndexJsonOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, AppPaths.ArchiveIndexFile, true);
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
}
