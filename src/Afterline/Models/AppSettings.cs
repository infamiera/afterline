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
    public bool ShowOocChat { get; set; } = true;
    public bool ColorizeRoleplayLines { get; set; } = true;
    public bool ShowLiveTimestamps { get; set; } = true;
    public bool AutoScrollLiveChat { get; set; } = true;
    public int MaxLiveMessages { get; set; } = 2000;
    public int ReconnectGraceMinutes { get; set; } = 0;
    public int ProcessingIntervalMinutes { get; set; } = 1;
    public EditorPreferences Editor { get; set; } = new();
}

public sealed class EditorPreferences
{
    public string Font { get; set; } = "Arial Bold";
    public double FontSize { get; set; } = 18;
    public double LineSpacing { get; set; } = 1;
    public double ChatWidth { get; set; } = 900;
    public bool ShowTimestamps { get; set; }
    public string CanvasBackground { get; set; } = "Black";
    public double ChatHorizontalPosition { get; set; }
    public double ChatVerticalPosition { get; set; }

    public bool StrokeEnabled { get; set; }
    public double StrokeWidth { get; set; } = 1;
    public string StrokeColor { get; set; } = "Black";

    public bool ShadowEnabled { get; set; }
    public double ShadowOpacity { get; set; } = 75;
    public double ShadowSoftness { get; set; } = 5;
    public double ShadowX { get; set; } = 2;
    public double ShadowY { get; set; } = 2;
    public string ShadowColor { get; set; } = "Black";

    public string PaintColor { get; set; } = "White";
    public double BrushSize { get; set; } = 5;
}
