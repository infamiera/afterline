using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class ArchiveService
{
    public async Task<IReadOnlyList<SessionIndexEntry>> RebuildIndexAsync(string root, CancellationToken cancellationToken)
    {
        var entries = new List<SessionIndexEntry>();
        if (Directory.Exists(root))
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Contains($"{Path.DirectorySeparatorChar}.active{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = new FileInfo(file);
                int lineCount = 0;
                using (var reader = new StreamReader(file))
                {
                    while (await reader.ReadLineAsync(cancellationToken) is not null) lineCount++;
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

        entries = entries.OrderByDescending(x => x.LastWriteUtc).ToList();
        AppPaths.EnsureLocalDirectories();
        string temp = AppPaths.ArchiveIndexFile + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        File.Move(temp, AppPaths.ArchiveIndexFile, true);
        return entries;
    }

    public IReadOnlyList<SessionIndexEntry> LoadCachedIndex()
    {
        try
        {
            if (!File.Exists(AppPaths.ArchiveIndexFile)) return Array.Empty<SessionIndexEntry>();
            List<SessionIndexEntry>? cached = JsonSerializer.Deserialize<List<SessionIndexEntry>>(File.ReadAllText(AppPaths.ArchiveIndexFile));
            return cached is null ? Array.Empty<SessionIndexEntry>() : cached;
        }
        catch
        {
            return Array.Empty<SessionIndexEntry>();
        }
    }
}
