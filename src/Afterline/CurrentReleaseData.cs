namespace Afterline;

internal static class CurrentReleaseData
{
    // Keep current public-release notes separate from the historical list so the
    // release-ready build can evolve without rewriting previous patch history.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.6.1", "22/AUG/2026", new[]
        {
            "Added direct drag-to-position for the Editor chat block plus horizontal and vertical position sliders in Chat & Font.",
            "Simplified the sidebar update card by removing the redundant FiveM and capture-status lines.",
            "Improved archive indexing by reusing cached line counts for unchanged chatlogs.",
            "Reduced capture and recovery polling overhead in FiveM detection, DevTools chat reading and raw recovery handling.",
            "Improved image and GIF Editor performance with neutral-tone fast paths and less repeated work during GIF export."
        }),
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
