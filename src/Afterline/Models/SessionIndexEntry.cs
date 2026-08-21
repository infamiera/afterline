namespace Afterline.Models;

public sealed class SessionIndexEntry
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastWriteUtc { get; set; }
    public long SizeBytes { get; set; }
    public int LineCount { get; set; }

    public string FileName => Path.GetFileName(FilePath);
    public string RecentTimestamp => $"{LastWriteUtc.ToLocalTime():dd MMM yyyy · HH:mm} · {LineCount:N0} lines";
}
