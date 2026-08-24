namespace Afterline;

internal static class CurrentReleaseData
{
    // Keep current public-release notes separate from the historical list so the
    // release-ready build can evolve without rewriting previous patch history.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.7.0", "24/AUG/2026", new[]
        {
            "Image Editor — Added independently editable image layers with direct dragging, mouse resizing, exact pixel dimensions, four-edge snapping, locking, visible lock badges, drag-to-reorder stacking, and per-layer paint and transparent eraser tools with undo and redo.",
            "Image Editor — Added top and left rulers with a configurable keybind, collapsible Filter Presets and Filters & Adjustments panels, a taller Layers panel, smaller controls, automatic main-sidebar collapse, and an explicit Close Editor button that restores the application sidebar.",
            "Image Editor — Fixed invisible Base Images, black previews caused by missing ruler theme resources, layer dragging being blocked by Marquee mode, layer-only exports, and exports containing layers beyond the original canvas.",
            "Projects — Fixed .afterlineproj PNG serialization, preserved edited pixels and complete layer state, and added Documents\\Afterline Projects as the configurable default project folder.",
            "Performance — Moved full-resolution filter work off the UI thread, removed blocking preview warm-up, and reused frozen chat brushes to improve initial slider, scrolling, and long-chat responsiveness.",
            "Live Chat — Added direct capture of FiveM's rendered text colors while retaining the existing automatic formatter as a safe fallback for neutral, incomplete, older, or unsupported chat data.",
            "Live Chat — Added reliable coloring for tattoo purchases, attachment commands, Panda Points activity rewards, and other recognized messages when FiveM briefly exposes an all-white chat row.",
            "Live Chat — Added top and bottom jump arrows beside Clear Display.",
            "Exports — Added self-contained, color-preserving HTML export for the current Live Chat view and opened or filtered Log Reader files, including safe HTML escaping and Log Reader line numbers.",
            "Storage — Kept standard chatlogs as readable .txt files and stored exact per-character colors in optional .colors.jsonl sidecars; all chat, project, settings, cache, and recovery data remains local.",
            "Recovery — Made Parse Current Chat replay persisted cache data, resume interrupted journals after crashes, outages, updates, and ordinary restarts, and avoid false [NEW LOGIN] markers for program interruptions.",
            "Updater — Replaced rate-limited Canary polling with a lightweight release manifest, removed duplicate checks, added exact build-number and commit comparison, and prevented installed or older Canary builds from being offered as updates.",
            "Updater — Retained the previous executable until the replacement stays healthy, kept Retry and Check Again usable after failures, and added packaged startup validation before publication.",
            "Idle usage — Reduced disconnected FiveM detection, backed off unchanged chat polling, stopped hidden UI refreshes, avoided duplicate snapshot and archive-index writes, and limited page-specific status scans to visible pages.",
            "Quality — Expanded the Windows release gate with startup survival, Base Image rendering, project round-trip, interrupted-session recovery, exact and fallback color checks, safe HTML export, updater-manifest validation, and measured idle CPU and memory limits."
        }),
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
