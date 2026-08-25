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
    public bool UseWindowsArchiveNotifications { get; set; }
    public bool StreamerModeEnabled { get; set; }
    // Screenshot capture is opt-in at the application level: it only registers a
    // global hotkey while enabled, and never polls or captures outside FiveM/GTA.
    public bool EnableFiveMScreenshotCapture { get; set; } = true;
    public string ScreenshotFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Afterline",
        "Screenshots");
    public string ScreenshotHotkey { get; set; } = "Ctrl+Shift+F12";
    public string ScreenshotCaptureSound { get; set; } = "Shutter";
    public int ScreenshotCaptureSoundVolume { get; set; } = 60;
    public int MaxLiveMessages { get; set; } = 2000;
    public int ReconnectGraceMinutes { get; set; } = 0;
    public int ProcessingIntervalMinutes { get; set; } = 1;

    // Keep the archive cheap to open on large installations. Users can opt into
    // an explicit range or the complete archive from the Archive page.
    public string ArchiveFilterMode { get; set; } = "LastDays";
    public int ArchiveLastDays { get; set; } = 7;
    public int? ArchiveLoadingPolicyVersion { get; set; }
    public DateTime? ArchiveFromDate { get; set; }
    public DateTime? ArchiveToDate { get; set; }

    public string UpdateChannel { get; set; } = "Stable";
    public string? InstalledCanaryBuild { get; set; }

    public string FindKeybind { get; set; } = "Ctrl+F";
    public string OpenLogKeybind { get; set; } = "Ctrl+O";
    public string CopyKeybind { get; set; } = "Ctrl+C";
    public string CopyContextKeybind { get; set; } = "Ctrl+Shift+C";

    // Existing installations deserialize this as true when the property is absent.
    // SettingsService explicitly sets it to false only when no settings file exists.
    public bool FirstRunCompleted { get; set; } = true;
    public List<string> RecentLogPaths { get; set; } = new();
    public List<string> PinnedLogPaths { get; set; } = new();

    public EditorPreferences Editor { get; set; } = new();
    public ThemePreferences Theme { get; set; } = new();
    public List<SavedThemePreset> CustomThemes { get; set; } = new();
}

public sealed class EditorPreferences
{
    public string ProjectsFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Afterline Projects");
    public int ProjectAutosaveMinutes { get; set; } = 5;

    public string Font { get; set; } = "Arial Bold";
    public double FontSize { get; set; } = 18;
    public double LineSpacing { get; set; } = 1;
    public double ChatWidth { get; set; } = 900;
    public string ChatTextAlignment { get; set; } = "Left";
    public bool ShowTimestamps { get; set; }
    public string CanvasBackground { get; set; } = "Black";
    public double ChatHorizontalPosition { get; set; }
    public double ChatVerticalPosition { get; set; }

    public string ExportKeybind { get; set; } = "Ctrl+S";
    public string UndoKeybind { get; set; } = "Ctrl+Z";
    public string RedoKeybind { get; set; } = "Ctrl+Shift+Z";
    public string FullscreenKeybind { get; set; } = "F11";
    public string RulerKeybind { get; set; } = "R";

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

    public double ImageBrightness { get; set; }
    public double ImageContrast { get; set; }
    public double ImageSaturation { get; set; }
    public double ImageWarmth { get; set; }
    public double ImageTint { get; set; }
    public double ImageBlur { get; set; }

    public string OutputPreset { get; set; } = "Original";
    public int OutputWidth { get; set; }
    public int OutputHeight { get; set; }
    public bool OutputLockAspect { get; set; } = true;
}

public sealed class ThemePreferences
{
    public string Background { get; set; } = "#11151B";
    public string Sidebar { get; set; } = "#0E1217";
    public string Panel { get; set; } = "#181E26";
    public string Raised { get; set; } = "#202832";
    public string Inset { get; set; } = "#141A21";
    public string Border { get; set; } = "#2C3744";
    public string Accent { get; set; } = "#5B9FEF";
    public string AccentHover { get; set; } = "#70AEF2";
    public string ControlHover { get; set; } = "#293544";
    public string PrimaryText { get; set; } = "#EDF2F7";
    public string SecondaryText { get; set; } = "#AAB6C3";
}

public sealed class SavedThemePreset
{
    public string Name { get; set; } = "Custom Theme";
    public ThemePreferences Theme { get; set; } = new();
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public override string ToString() => Name;
}
