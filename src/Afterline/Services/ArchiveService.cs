using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class ArchiveService
{
    private static readonly JsonSerializerOptions IndexJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _rebuildGate = new(1, 1);

    public async Task<IReadOnlyList<SessionIndexEntry>> RebuildIndexAsync(
        string root,
        CancellationToken cancellationToken)
    {
        await _rebuildGate.WaitAsync(cancellationToken);
        try
        {
            IReadOnlyList<SessionIndexEntry> cached = LoadCachedIndex();
            var cachedByPath = cached
                .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var entries = new List<SessionIndexEntry>();
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
                        while (await reader.ReadLineAsync(cancellationToken) is not null)
                            lineCount++;
                    }

                    entries.Add(new SessionIndexEntry
                    {
                        FilePath = file,
                        LastWriteUtc = info.LastWriteTimeUtc,
                        SizeBytes = info.Length,
                        LineCount = lineCount
                    });
                }
            }

            entries.Sort((left, right) => right.LastWriteUtc.CompareTo(left.LastWriteUtc));

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
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temp, AppPaths.ArchiveIndexFile, true);
            return entries;
        }
        finally
        {
            _rebuildGate.Release();
        }
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
