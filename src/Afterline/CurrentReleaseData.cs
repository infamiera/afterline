namespace Afterline;

internal static class CurrentReleaseData
{
    // Keep current public-release notes separate from the historical list so the
    // release-ready build can evolve without rewriting previous patch history.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.6.6", "23/AUG/2026", new[]
        {
            "Promoted the Canary Editor workspace to Stable with compact top menus, dedicated tool panels, full-screen editing, configurable Editor keybinds, selection tools, snapping, multi-chat overlays, saved filter presets, pixelation, and expanded image adjustments.",
            "Promoted the reorganized Settings experience with dedicated navigation for general settings, keybinds, recovery, raw capture failsafe, and the Canary Branch.",
            "Hardened Stable and Canary updates with unique build identities, automatic build refresh, SHA-256 verification, and a detached retrying installer for both channel directions.",
            "Optimized startup by consolidating superseded Canary UI initialization layers, avoiding duplicate handlers and redundant filter prewarming, and leaving the removed Object Select subsystem inactive.",
            "Reduced background release polling while preserving immediate refresh when Afterline becomes active.",
            "Fixed Editor project saving, added a configurable Documents-based projects folder, and added a packaged project round-trip regression check.",
            "Moved full-resolution filter preview work off the UI thread so first-use sliders and scrolling remain responsive.",
            "Made Live Chat resume active journal and raw-cache checkpoints across crashes, power loss, updates, and ordinary Afterline restarts without adding a false new-login marker."
        }),
        new("0.6.5", "23/AUG/2026", new[]
        {
            "Introduced distinct Stable and Canary build identities so update checks no longer rely on the base version alone.",
            "Updated Stable releases to use descriptive titles while retaining unique semantic-version tags and executable names.",
            "Prepared Canary update parsing for build-number and commit-based identities while remaining compatible with older SHA-only Canary assets."
        }),
        new("0.6.4", "23/AUG/2026", new[]
        {
            "Moved Stable and Canary update-channel controls from the sidebar update card into Settings to keep the sidebar compact."
        }),
        new("0.6.3", "23/AUG/2026", new[]
        {
            "Fixed Editor Left, Center and Right alignment so it changes text alignment inside the chat block without moving the block itself."
        }),
        new("0.6.2", "23/AUG/2026", new[]
        {
            "Added Stable and Canary update channels with an in-app opt-in warning and one-click return to Stable.",
            "Added automatic public Canary prerelease publishing so opted-in testers can receive experimental builds.",
            "Added left, center and right chat alignment controls to the RP Screenshot Editor.",
            "Fixed the Live Chat find toolbar so Clear and Copy selected remain aligned together at the standard window size."
        }),
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
