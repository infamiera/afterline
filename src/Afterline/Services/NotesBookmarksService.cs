using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class NotesBookmarksService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<NoteBookmarkEntry> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.NotesBookmarksFile)) return Array.Empty<NoteBookmarkEntry>();
            string json = File.ReadAllText(AppPaths.NotesBookmarksFile);
            return (JsonSerializer.Deserialize<List<NoteBookmarkEntry>>(json) ?? new List<NoteBookmarkEntry>())
                .OrderByDescending(entry => entry.CreatedAt)
                .ToArray();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load notes and bookmarks.", ex);
            return Array.Empty<NoteBookmarkEntry>();
        }
    }

    public async Task<NoteBookmarkEntry> AddForLineAsync(
        SavedMarkerKind kind,
        ChatEntry entry,
        string serverName,
        string? filePath,
        string? noteText,
        CancellationToken cancellationToken)
    {
        var saved = new NoteBookmarkEntry
        {
            Kind = kind,
            CreatedAt = DateTime.Now,
            ChatTimestamp = entry.CapturedAt,
            ServerName = string.IsNullOrWhiteSpace(serverName) ? "Unknown Server" : serverName,
            FilePath = filePath,
            LineText = entry.Display,
            NoteText = noteText?.Trim() ?? string.Empty,
            LineNumber = await FindLineNumberAsync(filePath, entry, cancellationToken)
        };

        await AddAsync(saved, cancellationToken);
        return saved;
    }

    public async Task<NoteBookmarkEntry> AddSessionNoteAsync(
        string noteText,
        string serverName,
        string? filePath,
        CancellationToken cancellationToken)
    {
        int? lineNumber = null;
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try { lineNumber = File.ReadLines(filePath).Count(); } catch { }
        }

        var saved = new NoteBookmarkEntry
        {
            Kind = SavedMarkerKind.Note,
            CreatedAt = DateTime.Now,
            ChatTimestamp = DateTime.Now,
            ServerName = string.IsNullOrWhiteSpace(serverName) ? "Unknown Server" : serverName,
            FilePath = filePath,
            LineNumber = lineNumber,
            LineText = string.Empty,
            NoteText = noteText.Trim()
        };

        await AddAsync(saved, cancellationToken);
        return saved;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<NoteBookmarkEntry> entries = LoadInternal();
            entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
            await SaveInternalAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task AddAsync(NoteBookmarkEntry entry, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<NoteBookmarkEntry> entries = LoadInternal();
            entries.Add(entry);
            await SaveInternalAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static List<NoteBookmarkEntry> LoadInternal()
    {
        try
        {
            if (!File.Exists(AppPaths.NotesBookmarksFile)) return new List<NoteBookmarkEntry>();
            string json = File.ReadAllText(AppPaths.NotesBookmarksFile);
            return JsonSerializer.Deserialize<List<NoteBookmarkEntry>>(json) ?? new List<NoteBookmarkEntry>();
        }
        catch
        {
            return new List<NoteBookmarkEntry>();
        }
    }

    private static async Task SaveInternalAsync(List<NoteBookmarkEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.NotesBookmarksFile)!);
        string temp = AppPaths.NotesBookmarksFile + ".tmp";
        string json = JsonSerializer.Serialize(entries.OrderByDescending(entry => entry.CreatedAt), JsonOptions);
        await File.WriteAllTextAsync(temp, json, cancellationToken);
        File.Move(temp, AppPaths.NotesBookmarksFile, true);
    }

    private static async Task<int?> FindLineNumberAsync(
        string? filePath,
        ChatEntry entry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        try
        {
            string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            string expected = entry.IsSystemMessage
                ? entry.Text
                : $"[{entry.CapturedAt:HH:mm:ss}] {entry.ContentWithoutTimestamp}";

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (string.Equals(lines[i], expected, StringComparison.Ordinal)) return i + 1;
            }

            string content = entry.ContentWithoutTimestamp;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(content) && lines[i].Contains(content, StringComparison.Ordinal))
                    return i + 1;
            }
        }
        catch
        {
        }

        return null;
    }
}
