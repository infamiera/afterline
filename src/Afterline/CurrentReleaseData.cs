namespace Afterline;

internal static class CurrentReleaseData
{
    // Keep current public-release notes separate from the historical list so the
    // release-ready build can evolve without rewriting previous patch history.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.6.0", "22/AUG/2026", new[]
        {
            "Added animated GIF loading, preview and export to the RP Screenshot Editor.",
            "Added non-destructive crop framing with exact output dimensions, aspect-ratio locking and common size presets.",
            "Expanded saved Editor settings to include image tone and output-size preferences.",
            "Reworked update checking around public GitHub Releases with an in-app update prompt, SHA-256 verification and automatic restart.",
            "Added safe self-replacement with rollback support plus a tag-driven release workflow that publishes the Windows executable and checksum."
        })
    };
}
