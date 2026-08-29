using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Afterline.Models;

namespace Afterline.Services;

internal static class ArchiveStressTest
{
    private const int ChatlogCount = 10_000;

    public static async Task RunAsync(string testRoot)
    {
        if (string.IsNullOrWhiteSpace(testRoot))
            throw new ArgumentException("An archive stress-test folder is required.", nameof(testRoot));

        string root = Path.Combine(Path.GetFullPath(testRoot), "afterline-archive-stress-10000");
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);

        try
        {
            CreateChatlogs(root);

            var fullStopwatch = Stopwatch.StartNew();
            IReadOnlyList<SessionIndexEntry> full = await new ArchiveService()
                .RebuildIndexAsync(root, CancellationToken.None);
            fullStopwatch.Stop();
            if (full.Count != ChatlogCount)
                throw new InvalidOperationException(
                    $"The full archive stress scan indexed {full.Count:N0} of {ChatlogCount:N0} chatlogs.");

            var cachedStopwatch = Stopwatch.StartNew();
            IReadOnlyList<SessionIndexEntry> cached = new ArchiveService()
                .LoadCachedIndex()
                .Where(entry => entry.FilePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            cachedStopwatch.Stop();
            if (cached.Count != ChatlogCount)
                throw new InvalidOperationException(
                    $"The cached archive stress load returned {cached.Count:N0} of {ChatlogCount:N0} chatlogs.");

            string incrementalPath = cached[0].FilePath;
            await File.AppendAllTextAsync(incrementalPath, Environment.NewLine + "incremental archive line");
            var incrementalStopwatch = Stopwatch.StartNew();
            bool incrementallyIndexed = await new ArchiveService().EnsureFileIndexedAsync(
                root,
                incrementalPath,
                CancellationToken.None);
            incrementalStopwatch.Stop();
            SessionIndexEntry? incrementallyUpdated = new ArchiveService()
                .LoadCachedIndex()
                .FirstOrDefault(entry => string.Equals(
                    entry.FilePath,
                    incrementalPath,
                    StringComparison.OrdinalIgnoreCase));
            if (!incrementallyIndexed || incrementallyUpdated?.LineCount != 2)
                throw new InvalidOperationException(
                    "Incremental archive indexing did not update the changed chatlog directly.");

            var inspected = new ConcurrentBag<string>();
            var recentService = new ArchiveService(TimeSpan.Zero, inspected.Add);
            IReadOnlyList<SessionIndexEntry> recent = await recentService.RebuildIndexAsync(
                root,
                CancellationToken.None,
                DateTime.Today.AddDays(-6),
                DateTime.Today,
                maxEntries: 250,
                scanMode: ArchiveScanMode.DatedFoldersOnly);
            if (recent.Count == 0 || recent.Count > 250)
                throw new InvalidOperationException(
                    $"The targeted recent archive scan returned an invalid {recent.Count:N0} entries.");
            if (inspected.Count >= 500)
                throw new InvalidOperationException(
                    $"The targeted recent scan inspected {inspected.Count:N0} files instead of staying inside the current dated folders.");

            using var slowCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(175));
            var slowStopwatch = Stopwatch.StartNew();
            try
            {
                await new ArchiveService(TimeSpan.FromMilliseconds(2)).RebuildIndexAsync(
                    root,
                    slowCancellation.Token,
                    scanMode: ArchiveScanMode.FullRecursive);
                throw new InvalidOperationException("The simulated slow-storage archive scan ignored cancellation.");
            }
            catch (OperationCanceledException) when (slowCancellation.IsCancellationRequested)
            {
            }
            slowStopwatch.Stop();
            if (slowStopwatch.Elapsed > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException(
                    $"The simulated slow-storage scan took {slowStopwatch.Elapsed.TotalSeconds:N1}s to cancel.");

            DiagnosticLogger.Info(
                $"Archive stress test passed: {ChatlogCount:N0} chatlogs; " +
                $"full index {fullStopwatch.ElapsedMilliseconds:N0} ms; " +
                $"cached load {cachedStopwatch.ElapsedMilliseconds:N0} ms; " +
                $"incremental update {incrementalStopwatch.ElapsedMilliseconds:N0} ms; " +
                $"recent scan inspected {inspected.Count:N0}; " +
                $"slow-storage cancellation {slowStopwatch.ElapsedMilliseconds:N0} ms.");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static void CreateChatlogs(string root)
    {
        Parallel.For(
            0,
            ChatlogCount,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            index =>
            {
                DateTime date = DateTime.Today.AddMonths(-(index % 120));
                string folder = Path.Combine(
                    root,
                    date.ToString("yyyy", CultureInfo.InvariantCulture),
                    date.ToString("MM - MMMM", CultureInfo.InvariantCulture));
                // Directory.CreateDirectory is idempotent and concurrency-safe.
                // Every worker calls it so no writer can outrun a separate
                // thread that merely reserved the folder name.
                Directory.CreateDirectory(folder);

                string displayDate = date.ToString("dd-MMMM-yyyy", CultureInfo.InvariantCulture);
                string path = Path.Combine(
                    folder,
                    $"Chatlog [Archive Stress] [{displayDate}] ({index + 1}).txt");
                File.WriteAllText(path, $"[{date:HH:mm:ss}] archive stress line {index + 1}");
            });
    }
}
