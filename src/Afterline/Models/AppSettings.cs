namespace Afterline.Models;

public sealed class AppSettings
{
    public string ArchiveRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Afterline Chatlogs");

    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoDetectFiveM { get; set; } = true;
    public bool AutoCapture { get; set; } = true;
    public bool ShowLiveChat { get; set; } = true;
    public bool ColorizeRoleplayLines { get; set; } = true;
    public bool ShowLiveTimestamps { get; set; } = true;
    public bool AutoScrollLiveChat { get; set; } = true;
    public int MaxLiveMessages { get; set; } = 2000;
    public int ReconnectGraceMinutes { get; set; } = 0;
    public int ProcessingIntervalMinutes { get; set; } = 1;
}
