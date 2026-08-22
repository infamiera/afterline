namespace Afterline.Services;

public static class AppPaths
{
    public static string LocalDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Afterline");

    public static string SettingsFile => Path.Combine(LocalDataRoot, "settings.json");
    public static string DiagnosticLog => Path.Combine(LocalDataRoot, "Logs", "afterline.log");
    public static string ArchiveIndexFile => Path.Combine(LocalDataRoot, "Cache", "archive-index.json");
    public static string LastSessionCacheFile => Path.Combine(LocalDataRoot, "Cache", "last-session.txt");
    public static string RawCaptureCacheFile => Path.Combine(LocalDataRoot, "Cache", "raw-capture.json");
    public static string RawCapturePreviousCacheFile => Path.Combine(LocalDataRoot, "Cache", "raw-capture.previous.json");
    public static string CaptureRunStateFile => Path.Combine(LocalDataRoot, "Cache", "capture-run.json");
    public static string NotesBookmarksFile => Path.Combine(LocalDataRoot, "Cache", "notes-bookmarks.json");
    public static string SearchHistoryFile => Path.Combine(LocalDataRoot, "Cache", "search-history.json");
    public static string ActiveSessionsDirectory => Path.Combine(LocalDataRoot, "Active Sessions");
    public static string RecoveryBackupsDirectory => Path.Combine(LocalDataRoot, "Recovery Backups");
    public static string ProfileDirectory => Path.Combine(LocalDataRoot, "Profile");
    public static string ProfilePictureFile => Path.Combine(ProfileDirectory, "avatar.png");

    public static void EnsureLocalDirectories()
    {
        Directory.CreateDirectory(LocalDataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLog)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ArchiveIndexFile)!);
        Directory.CreateDirectory(ActiveSessionsDirectory);
        Directory.CreateDirectory(RecoveryBackupsDirectory);
        Directory.CreateDirectory(ProfileDirectory);
    }
}
