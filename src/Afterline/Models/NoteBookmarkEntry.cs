namespace Afterline.Models;

public enum SavedMarkerKind
{
    Bookmark,
    Note
}

public sealed class NoteBookmarkEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public SavedMarkerKind Kind { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ChatTimestamp { get; set; } = DateTime.Now;
    public string ServerName { get; set; } = "Unknown Server";
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string LineText { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;

    public string KindLabel => Kind == SavedMarkerKind.Bookmark ? "BOOKMARK" : "NOTE";

    public string Display
    {
        get
        {
            string source = string.IsNullOrWhiteSpace(LineText) ? "Session note" : LineText;
            string note = string.IsNullOrWhiteSpace(NoteText) ? string.Empty : $"\n{NoteText}";
            string line = LineNumber is int number ? $" · line {number}" : string.Empty;
            return $"{KindLabel} · {ChatTimestamp:dd MMM yyyy HH:mm:ss} · {ServerName}{line}\n{source}{note}";
        }
    }
}
