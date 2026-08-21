using System.Text.Json;

namespace Afterline.Services;

public sealed class SearchHistoryService
{
    private const int MaxEntries = 20;

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SearchHistoryFile)) return Array.Empty<string>();
            string json = File.ReadAllText(AppPaths.SearchHistoryFile);
            return (JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .ToArray();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load search history.", ex);
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> Add(string query)
    {
        string value = query.Trim();
        if (string.IsNullOrWhiteSpace(value)) return Load();

        List<string> entries = Load().ToList();
        entries.RemoveAll(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, value);
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        Save(entries);
        return entries;
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(AppPaths.SearchHistoryFile)) File.Delete(AppPaths.SearchHistoryFile);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to clear search history.", ex);
        }
    }

    private static void Save(IReadOnlyList<string> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.SearchHistoryFile)!);
            string temp = AppPaths.SearchHistoryFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, AppPaths.SearchHistoryFile, true);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save search history.", ex);
        }
    }
}
