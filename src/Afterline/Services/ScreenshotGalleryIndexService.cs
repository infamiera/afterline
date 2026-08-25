using System.Text.Json;

namespace Afterline.Services;

/// <summary>
/// Keeps the gallery responsive even when a user chooses a folder containing a
/// large number of images. Folder scans are explicit; ordinary gallery opens use
/// this compact, self-pruning index.
/// </summary>
public static class ScreenshotGalleryIndexService
{
    private const int MaximumIndexedCaptures = 200;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private sealed record IndexedCapture(string FilePath, DateTime CapturedAtUtc);

    public static IReadOnlyList<FileInfo> LoadRecent(string folder, int maximum)
    {
        if (string.IsNullOrWhiteSpace(folder) || maximum <= 0)
            return Array.Empty<FileInfo>();

        string fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        lock (Gate)
        {
            return ReadUnsafe()
                .Where(entry => string.Equals(
                    Path.GetDirectoryName(entry.FilePath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    fullFolder,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.CapturedAtUtc)
                .Select(entry => new FileInfo(entry.FilePath))
                .Where(file => file.Exists)
                .Take(maximum)
                .ToArray();
        }
    }

    public static void Record(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        lock (Gate)
        {
            var entries = ReadUnsafe()
                .Where(entry => !string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && File.Exists(entry.FilePath))
                .ToList();
            entries.Add(new IndexedCapture(Path.GetFullPath(filePath), DateTime.UtcNow));
            WriteUnsafe(entries
                .OrderByDescending(entry => entry.CapturedAtUtc)
                .Take(MaximumIndexedCaptures)
                .ToArray());
        }
    }

    public static void Remove(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        lock (Gate)
        {
            WriteUnsafe(ReadUnsafe()
                .Where(entry => !string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && File.Exists(entry.FilePath))
                .ToArray());
        }
    }

    public static void IndexExistingFiles(string folder, int maximum)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder) || maximum <= 0)
            return;

        var newest = new PriorityQueue<FileInfo, long>();
        foreach (string path in Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
        {
            var file = new FileInfo(path);
            if (!file.Name.StartsWith("Afterline_FiveM_", StringComparison.OrdinalIgnoreCase))
                continue;
            newest.Enqueue(file, file.LastWriteTimeUtc.Ticks);
            if (newest.Count > maximum)
                newest.Dequeue();
        }

        lock (Gate)
        {
            var entries = ReadUnsafe()
                .Where(entry => File.Exists(entry.FilePath))
                .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.CapturedAtUtc).First())
                .ToDictionary(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo file in newest.UnorderedItems.Select(item => item.Element))
                entries[file.FullName] = new IndexedCapture(file.FullName, file.LastWriteTimeUtc);
            WriteUnsafe(entries.Values
                .OrderByDescending(entry => entry.CapturedAtUtc)
                .Take(MaximumIndexedCaptures)
                .ToArray());
        }
    }

    private static IReadOnlyList<IndexedCapture> ReadUnsafe()
    {
        try
        {
            AppPaths.EnsureLocalDirectories();
            if (!File.Exists(AppPaths.ScreenshotGalleryIndexFile)) return Array.Empty<IndexedCapture>();
            return JsonSerializer.Deserialize<List<IndexedCapture>>(
                       File.ReadAllText(AppPaths.ScreenshotGalleryIndexFile),
                       JsonOptions)
                   ?? new List<IndexedCapture>();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to read the screenshot gallery index.", ex);
            return Array.Empty<IndexedCapture>();
        }
    }

    private static void WriteUnsafe(IReadOnlyList<IndexedCapture> entries)
    {
        try
        {
            AppPaths.EnsureLocalDirectories();
            string temporary = AppPaths.ScreenshotGalleryIndexFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, JsonOptions));
            File.Move(temporary, AppPaths.ScreenshotGalleryIndexFile, overwrite: true);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to write the screenshot gallery index.", ex);
        }
    }
}
