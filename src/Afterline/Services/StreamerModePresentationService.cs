namespace Afterline.Services;

/// <summary>
/// Presentation-only path masking. File operations always continue to use the
/// original path held by settings and models.
/// </summary>
public static class StreamerModePresentationService
{
    public static bool Enabled { get; set; }

    public static string PathForDisplay(string? path)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(path)) return path ?? string.Empty;

        try
        {
            string normalized = Path.GetFullPath(path);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrWhiteSpace(profile) && normalized.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
                return profile[..Math.Max(0, profile.LastIndexOf(Path.DirectorySeparatorChar) + 1)] + "••••" + normalized[profile.Length..];

            string root = Path.GetPathRoot(normalized) ?? string.Empty;
            string leaf = Path.GetFileName(normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(leaf)
                ? root + "••••"
                : root + "••••" + Path.DirectorySeparatorChar + leaf;
        }
        catch
        {
            return "••••";
        }
    }
}
