namespace Afterline.Services;

public static class AppPaths
{
    public static string LocalDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Afterline");

    public static string SettingsFile => Path.Combine(LocalDataRoot, "settings.json");
    public static string DiagnosticLog => Path.Combine(LocalDataRoot, "Logs", "afterline.log");
    public static string ArchiveIndexFile => Path.Combine(LocalDataRoot, "Cache", "archive-index.json");
    public static string ActiveSessionsDirectory => Path.Combine(LocalDataRoot, "Active Sessions");
    public static string RecoveryBackupsDirectory => Path.Combine(LocalDataRoot, "Recovery Backups");

    public static void EnsureLocalDirectories()
    {
        Directory.CreateDirectory(LocalDataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLog)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ArchiveIndexFile)!);
        Directory.CreateDirectory(ActiveSessionsDirectory);
        Directory.CreateDirectory(RecoveryBackupsDirectory);
    }
}
