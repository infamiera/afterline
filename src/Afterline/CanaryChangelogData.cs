namespace Afterline;

internal static class CanaryChangelogData
{
    // Canary notes are deliberately kept out of the Stable-only changelog view.
    // The workflow run number is the public Canary build identity.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.7.0", "03/SEP/2026", new[]
        {
            "Live Chat colors — Restores exact per-character server colors when the current session is replayed after startup or a session transition.",
            "Private color cache — Keeps the required replay metadata inside Afterline's app-data cache while archives, recovery copies and TXT exports remain standalone plain-text files."
        }, ChangelogChannel.Canary, 221),
        new("0.7.0", "03/SEP/2026", new[]
        {
            "Chat exports — Asks for TXT or HTML from either Live Chat export action, keeps colors self-contained in explicitly selected HTML, and stops creating color sidecars beside automatic or TXT chatlogs.",
            "Live Chat toolbar — Removes the separate HTML action while retaining Log Reader's dedicated HTML export.",
            "Chat filtering — Adds poster-management and property-information blocks to the OOC toggle without hiding login or disconnect markers."
        }, ChangelogChannel.Canary, 219),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Collage logo frames — Makes logo-designated slots fill their frame and respond to drag-to-reposition cropping like every image slot.",
            "Collage layouts — Adds six non-logo grid, panorama, feature and editorial arrangements with live previews and gap controls.",
            "Chat filtering — Covers additional gameplay notifications and variable Server Staff punishment broadcasts without hiding session boundaries."
        }, ChangelogChannel.Canary, 215),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Collage layouts — Adds five logo-focused compositions, including an eight-photo brand mosaic with an optional center logo.",
            "Layout preview — Shows the selected layout, canvas shape and live frame spacing before creating a collage.",
            "Live Chat filtering — Extends OOC coverage for additional gameplay notices while keeping login and disconnect events visible."
        }, ChangelogChannel.Canary, 212),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Transactional updates — Stages and verifies updates before atomically replacing Afterline.exe, with hash-verified recovery after locks or interrupted restarts.",
            "Continuous Canary publishing — Keeps the previous download valid until the new executable, checksum and manifest are all available."
        }, ChangelogChannel.Canary, 209),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Base Image trim — Clips overlapping image layers to the fixed Base Image borders and safely normalizes trim settings saved by Canary #205.",
            "Editor sidebar — Uses a shorter Layers list, collapsed presets and compact controls so Opacity and Corner radius stay within easy reach."
        }, ChangelogChannel.Canary, 207),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Live collage spacing — Lets Frame gap expand or contract an existing collage without changing its Base Image or export dimensions.",
            "Base Image sizing — Enforces a Full HD minimum and adds common monitor-resolution presets when promoting an image layer.",
            "Selected layer guide — Adds an optional outline for the currently selected image layer alongside the existing Base Image guide.",
            "Content trim — Hides overlapping layer content outside the Base Image borders while preserving the fixed canvas, project layers and Undo or Redo history."
        }, ChangelogChannel.Canary, 205),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Transparent projects — Starts new Editor projects with a real transparent Base Image, with optional black or white defaults and alpha-safe PNG export.",
            "Background removal — Adds a non-destructive preview for removing edge-connected backgrounds from the Base Image or selected image layer.",
            "Set as Base Image — Promotes an image layer to the Base Image using its displayed dimensions or a custom pixel size.",
            "Collage Maker — Adds six fixed-frame layouts with Explorer drop targets and independent image-crop repositioning.",
            "Editor controls — Expands font size to 100 and chat width to 1,500.",
            "Editor history — Adds chronological document, image and layer Undo or Redo through shortcuts and the File menu.",
            "About Afterline — Removes the duplicated Contact block while retaining the official project link."
        }, ChangelogChannel.Canary, 203),
        new("0.7.0", "02/SEP/2026", new[]
        {
            "Layer presentation — Keeps image pixels visible across the pasteboard without changing the Base Image or exported canvas.",
            "Layer corners — Adds a per-layer corner-radius control that persists through project saves and undo history.",
            "Transform controls — Improves proportional resizing and uses thinner selection borders and handles.",
            "Base Image guide — Adds an optional thin pink preview boundary that is never exported.",
            "Editor undo — Routes configured Undo and Redo shortcuts through image-layer resize and position history when applicable."
        }, ChangelogChannel.Canary, 199),
        new("0.7.0", "01/SEP/2026", new[]
        {
            "Editor image drops — Adds Explorer images as new layers whenever a rendered Base Image already exists, without changing the Base Image or export dimensions.",
            "Layer transforms — Replaces unstable resize deltas with direct pointer geometry, supports proportional corner resizing, and allows layers to move across the Base Image and pasteboard.",
            "Transform controls — Uses thinner zoom-independent outlines and handles for more precise layer positioning and resizing."
        }, ChangelogChannel.Canary, 195),
        new("0.7.0", "01/SEP/2026", new[]
        {
            "Duplicate review — Detects only varied ordered chat-buffer replays with collapsed replacement timestamps, retains every line, and asks the user to review highlighted candidates after the session.",
            "Safe cleanup — Removes only user-confirmed exact ranges, rejects ambiguous matches, and creates a complete text and color-metadata backup before changing a chatlog.",
            "Explorer image drops — Makes the first dropped image the Base Image and adds later or multi-selected images as immediately transformable layers.",
            "Off-canvas layers — Allows image layers to move and resize beyond every Base Image edge while preserving the Base Image as the saved export boundary.",
            "Editor workspace — Adds a compact theme-aware layout, Space or middle-button panning, pointer-anchored zoom, and a larger navigable pasteboard.",
            "Editor validation — Extends the packaged project test with Explorer import, negative-coordinate round trips, off-canvas resize geometry, and export-boundary checks."
        }, ChangelogChannel.Canary, 191),
        new("0.7.0", "29/AUG/2026", new[]
        {
            "Startup responsiveness — Opens the usable application shell first and prepares optional Editor and Settings features in deferred steps.",
            "Archive loading — Shows cached recent sessions immediately and limits automatic discovery to the relevant dated folders.",
            "Archive maintenance — Updates finalized chatlogs directly and reserves cancellable recursive rebuilding for a user-requested refresh.",
            "Capture continuity — Keeps FiveM polling and journal writes independent of Live Chat rendering, with separate last-message and successful-check timestamps.",
            "Freeze diagnostics — Persists five-second UI delays and fifteen-second freezes, recovers interrupted incidents after forced closure, and retains an exportable previous-session log.",
            "Stress testing — Adds a packaged 10,000-chatlog archive test with cached, incremental, targeted, and simulated slow-storage cancellation checks."
        }, ChangelogChannel.Canary, 188),
        new("0.7.0", "26/AUG/2026", new[]
        {
            "Capture shortcut activation — Confirm now saves and activates a recognized keyboard or Mouse 4/5 shortcut immediately, without requiring the separate Settings save action.",
            "Capture shortcut feedback — Added a persistent active or rejected status and diagnostic entries for both registration and shortcut detection.",
            "Live Chat filtering — Extended Show OOC chat to cover colon-delimited INFO notices, including property access and ownership warnings.",
            "Chat exports — Prevented a temporarily unresolved connection from replacing the active session's known server name with Unknown Server."
        }, ChangelogChannel.Canary, 183),
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
