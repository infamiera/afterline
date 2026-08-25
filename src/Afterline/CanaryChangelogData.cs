namespace Afterline;

internal static class CanaryChangelogData
{
    // Canary notes are deliberately kept out of the Stable-only changelog view.
    // The workflow run number is the public Canary build identity.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.7.0", "26/AUG/2026", new[]
        {
            "Editor rulers — Anchored both zero points to the Base Image, added adaptive zoom-aware ticks through 5,000 pixels, and repaired Fit alignment.",
            "Full Screen Editor — Restored the Preview toolbar control and hardened the borderless transition from an already-maximized window.",
            "Live Chat filters — Added an independent Show IC chat control for OOC and Server Staff review, plus pet status coverage for Show OOC chat.",
            "Chat actions — Renamed Open Today's Log and aligned it with neighboring Dashboard and Live Chat actions.",
            "Capture shortcuts — Added friendly key names, modifier combinations, Mouse 4/5 support, restricted unsafe keys, and a Confirm or Re-do step before saving.",
            "Capture notifications — Added an optional, default-off Windows notification after a screenshot is safely saved without taking focus from the game.",
            "Streamer reminder — Added a once-per-run privacy confirmation before Live Chat or Log Reader is shown while Streamer mode is active.",
            "Recent Editor Projects — Added theme-aware Recycle Bin deletion with confirmation and clearer double-click guidance.",
            "Updates — Renamed the sidebar actions to Updates and Changes, with a dismissible accent highlight when a newer build is available."
        }, ChangelogChannel.Canary, 177),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Capture hotkeys — Custom shortcuts now register reliably, support direct key recording, and clearly report Windows shortcut conflicts.",
            "Capture handoff — A shortcut pressed while Afterline is active can briefly switch to and capture a verified running game window without using a desktop fallback.",
            "Gallery controls — Added more capture sounds, local Recycle Bin deletion, and retained the bounded 20-thumbnail source-quality gallery.",
            "Streamer mode — Added a presentation-only Settings section that masks local paths without changing stored folders or file operations.",
            "Editor preview — Removed the fixed gap above the canvas and ruler so loaded images sit flush beneath the Preview header.",
            "Live Chat — Expanded Show OOC chat coverage to include vehicle-location teleport notices alongside the existing gameplay and partial Server Staff filters.",
            "Update display — Reworked the sidebar build card into a compact status with side-by-side actions.",
            "Changelog — Canary builds now see Canary and Stable notes, while Stable builds are strictly limited to Stable notes; changes use separate headings and brief descriptions."
        }, ChangelogChannel.Canary, 175),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Screen capture — Renamed Screenshots to Gallery, clarified that captures are stored locally, and moved all capture controls into their own Screen capture Settings section.",
            "Capture feedback — Added an optional Shutter, Chime, Soft, or Off audio confirmation with adjustable volume, instant Play preview, and a Reset control for the default Ctrl+Shift+F12 hotkey.",
            "Live Chat — Extended Show OOC chat filtering for AFK checks and store prompts such as Press Y to browse ammunation, regardless of the location name.",
            "Usability — Added concise, theme-compliant tooltips to all main sidebar navigation buttons and shortened Gallery action tooltips so they display completely.",
            "Gallery visibility — Disabling Screen capture now hides Gallery immediately and keeps it hidden after collapsing or expanding the main sidebar."
        }, ChangelogChannel.Canary, 165),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "FiveM screenshots — Added an on-demand, source-resolution PNG capture hotkey restricted to the foreground FiveM game subprocess, GTA5.exe, or GTAVLauncher.exe.",
            "Screenshot gallery — Added a theme-aware Gallery with a 20-thumbnail limit, a background-only folder scan, configurable storage, and one-click Open in Editor handoff.",
            "Capture controls — Added a master setting that unregisters the hotkey, hides the Gallery, clears previews, and performs no screenshot work while disabled.",
            "Live Chat — Expanded OOC filtering with additional gameplay notices and partial Server Staff coverage, including toggleable admin messages."
        }, ChangelogChannel.Canary, 164),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Live Chat filtering — Expanded Show OOC chat to hide ANTI-FALL notices, login and weather summaries, faction rosters, friend-login alerts, payphone instructions, property/stat readouts, and action cooldown warnings.",
            "Capture safety — These additions affect only Live Chat and filtered exports; complete captured and archived logs remain unchanged.",
            "Changelog — Vertically aligned the Stable and Canary channel badges with their release titles."
        }, ChangelogChannel.Canary, 162),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Diagnostics — Simplified Discord guidance to the #afterline forum channel, retained the linked server invite, and added bounded detection for unclean exits and 15-second UI hangs.",
            "Live Chat — Separated the action row from header toggles, increased button spacing, and expanded Show OOC chat to cover MAPPING, SUCCESS, ERROR, PM, INFO and related mapping-status notices.",
            "Image Editor — Widened the tool rail so its buttons render cleanly and made the return control more prominent with a longer three-second entry highlight.",
            "Themes — Added three local named custom-theme slots with preview, apply, update and delete controls, plus named saving directly from Theme Creator."
        }, ChangelogChannel.Canary, 161),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Diagnostics — Error Logs now show only errors recorded by the currently installed build; installing a new update clears diagnostics left by earlier builds.",
            "Diagnostics control — Added a confirmed Clear error logs action that wipes the current diagnostic log and its rotated backup, then immediately resets the footer warning state.",
            "Archive reliability — Added shared-read access for active chatlogs, process-wide index serialization, and unique temporary index files to avoid transient file-lock failures.",
            "Error review — Confirmed the submitted Editor project, ruler-resource, server-clock, archive, and updater errors all predated Canary #159 and are covered by current fixes or this additional hardening."
        }, ChangelogChannel.Canary, 160),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Diagnostics — Added an Error Logs button beside Settings with a bounded recent-error viewer and a one-click .txt export to the Windows Downloads folder.",
            "Support — Added visible links to the permanent Afterline Discord invite and the dedicated Afterline forum, with clear guidance to post exported logs only in that forum.",
            "Privacy and stability — Error reports include the Canary build and Windows version, redact common user-profile paths, exclude ordinary info messages, and retain the existing 2 MB rotating log limit.",
            "Editor recovery — Moved the project autosave interval out of General Settings and into the Editor Settings panel where it belongs."
        }, ChangelogChannel.Canary, 159),
        new("0.7.0", "25/AUG/2026", new[]
        {
            "Archive — Changed the standard Archive range to seven days, migrated untouched 30-day settings, and added a clear warning before users request heavier ranges.",
            "Dashboard — Split Recent Sessions with a new Recent Editor Projects list, project thumbnails, recovery labels, and double-click project opening.",
            "Editor recovery — Added configurable project autosave with Off, 1, 5, 10, 15, and 30 minute choices; the default is five minutes and successful saves show a compact Editor notification.",
            "Image Editor — Repaired Fit and automatic fitting after layout changes, made collapsed panels fully release their columns, and moved collapse controls onto their associated panels.",
            "Changelog — Separated Canary history from Stable releases, introduced distinct channel titles, and improved scanning with colored category labels and lighter Stable borders.",
            "Build identity — Removed the legacy 0.2.4 footer placeholder so the active 0.7.0 Canary identity is shown consistently from the first render."
        }, ChangelogChannel.Canary, 158),
        new("0.7.0", "24/AUG/2026", new[]
        {
            "Archive performance — Limited automatic dashboard loading to the latest seven days and bounded archive indexing before log contents are opened.",
            "FiveM recovery — Added refresh controls to the Dashboard connection card and Live Chat toolbar for an immediate connection and active-chat re-check.",
            "Idle usage — Reduced disconnected detection work, backed off unchanged polling, and avoided redundant archive, cache, and snapshot writes."
        }, ChangelogChannel.Canary, 157),
        new("0.7.0", "24/AUG/2026", new[]
        {
            "Windows startup — Repaired stale startup registrations so they follow the currently running Canary executable.",
            "Image Editor — Attached rulers to the preview, removed ruler gaps, automatically fitted Base Images, and added whole-panel sidebar collapse controls.",
            "Archive notifications — Added a choice between in-app and Windows confirmations after a finalized log is verified."
        }, ChangelogChannel.Canary, 156),
        new("0.7.0", "24/AUG/2026", new[]
        {
            "Live Chat — Preserved exact FiveM text colors while retaining automatic fallback colors for incomplete or neutral rows.",
            "Image Editor — Applied the same exact color runs to imported chat text and project saves."
        }, ChangelogChannel.Canary, 154),
        new("0.7.0", "24/AUG/2026", new[]
        {
            "Chat colors — Introduced captured whole-line color support for Live Chat, Log Reader, and Editor rendering.",
            "Compatibility — Kept the existing formatter available when rendered color data is unavailable."
        }, ChangelogChannel.Canary, 152)
    };
}
