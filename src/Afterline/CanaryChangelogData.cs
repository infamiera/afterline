namespace Afterline;

internal static class CanaryChangelogData
{
    // Canary notes are deliberately kept out of the Stable-only changelog view.
    // The workflow run number is the public Canary build identity.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
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
