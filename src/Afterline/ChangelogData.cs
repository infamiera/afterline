namespace Afterline;

internal sealed record ChangelogEntry(string Version, string Date, string[] Changes);

internal static class ChangelogData
{
    // RELEASE CHECKLIST: update this list before every version bump / public build.
    // The newest release must stay first so the in-app changelog always opens on the latest notes.
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new ChangelogEntry[]
    {
        new("0.5.4", "22/AUG/2026", new[]
        {
            "Added automatic daily chatlog rollover at midnight without interrupting the active server session.",
            "Added clear day-end and date-rollover markers, continuous current-session exports and unobtrusive Live Chat rollover feedback.",
            "Added a pre-parse raw capture failsafe with current and previous cache generations plus preserved crash snapshots for unexpected shutdowns.",
            "Expanded Settings recovery options to replay raw captures, save recovery copies and show raw-backup and unexpected-shutdown status."
        }),
        new("0.5.3", "22/AUG/2026", new[]
        {
            "Added automatic chat coloring for friend-login messages so \"logged in\" is highlighted green.",
            "Added property-light success message coloring so [SUCCESS] is green, with off shown in red and on shown in green.",
            "Fixed Live Chat right-click targeting so context actions consistently use the line that was actually right-clicked.",
            "Added brief visual feedback to the right-clicked Live Chat line while its context menu is open."
        }),
        new("0.5.2", "22/AUG/2026", new[]
        {
            "Disabled the Frost theme template because its light palette can still leave some native/WPF controls unreadable.",
            "Removed Frost from theme selectors, including the one-time first-run setup.",
            "Added an automatic migration that returns existing Frost users to the Afterline Default theme on startup."
        }),
        new("0.5.1", "22/AUG/2026", new[]
        {
            "Added an in-app Changelog button directly beneath Check for updates.",
            "Added a themed, scrollable release-history window with the newest patch notes first.",
            "Backfilled patch notes for previous Afterline releases.",
            "Fixed Live Chat and Log Reader search so Enter jumps to the next matching line, scrolls it into view and briefly highlights it; Shift+Enter searches backwards.",
            "Changed active-chat search from hiding non-matching lines to navigation-style finding, preserving the surrounding chat while searching.",
            "Added theme-aware tooltip styling so chatlog hover hints and other tooltips remain readable in dark, light and custom themes.",
            "Added a release checklist reminder in source so future builds update the changelog before release."
        }),
        new("0.5.0", "21/AUG/2026", new[]
        {
            "Added Open current log shortcuts to Dashboard and Live Chat.",
            "Added drag-and-drop support for .txt files in Log Reader and images in the Screenshot Editor.",
            "Added common keyboard shortcuts for search, opening logs, Editor export and copying chat context.",
            "Added pinned and recently opened logs for faster archive access.",
            "Added Ctrl/Shift multi-line selection in Live Chat and Log Reader.",
            "Added a one-time first-run setup for archive location, startup behaviour and theme.",
            "Added search bars for working with the chat currently loaded in Live Chat or Log Reader."
        }),
        new("0.4.10", "21/AUG/2026", new[]
        {
            "Reworked the theme-template selector to stay bound to live theme resources.",
            "Forced the template selector to refresh its WPF layout when previewing light and dark themes.",
            "Improved Frost theme compatibility for the template dropdown."
        }),
        new("0.4.9", "21/AUG/2026", new[]
        {
            "Separated profile-picture management from the main Settings page.",
            "Made the profile circle open a dedicated local profile-picture manager.",
            "Expanded the About window so its information blocks display together more clearly.",
            "Compacted Settings and moved Recovery Center higher for quicker access.",
            "Centered the main application footer and refined theme-template layout behaviour."
        }),
        new("0.4.8", "21/AUG/2026", new[]
        {
            "Made popup windows, native title bars and scrollbars follow the active theme more consistently.",
            "Added a top-right profile circle with local profile-picture selection.",
            "Added crop, reposition and zoom controls before saving a profile picture.",
            "Moved Themes to its own footer shortcut and shortened the About disclaimer."
        }),
        new("0.4.7", "21/AUG/2026", new[]
        {
            "Added the Theme Creator with safe surface and text color customization.",
            "Added built-in theme templates including Afterline Default, Midnight Violet, Deep Ocean, Carbon Ember, Graphite Rose and Frost.",
            "Added About / Info, disclaimer and a placeholder Contact / Support section.",
            "Updated the Live Chat server label so Server: is bold and clearly separated from the server name."
        }),
        new("0.4.6", "21/AUG/2026", new[]
        {
            "Moved the Settings gear into the bottom-right application footer.",
            "Freed additional vertical space in the sidebar for navigation and Editor access.",
            "Simplified the update card into stacked Current Build and Latest values.",
            "Removed redundant private-update explanatory text and the Open latest builds link."
        }),
        new("0.4.5", "21/AUG/2026", new[]
        {
            "Added Reset and Save Settings controls to the Screenshot Editor.",
            "Made preferred Editor text, canvas, stroke, shadow and paint settings persist locally.",
            "Improved Notes & Bookmarks presentation with colored type, date/time and server labels.",
            "Removed unreliable Local Time and Server Time displays from Live Chat.",
            "Expanded automatic chat-color recognition for online-time, premium and faction-member messages."
        }),
        new("0.4.4", "21/AUG/2026", new[]
        {
            "Added detailed automatic coloring for character statistics output.",
            "Added segmented colors for wallet, bank, assets, organization, businesses and routing information.",
            "Added segmented colors for properties, phone numbers, premium, points, current time and activity values.",
            "Applied the same stats recognition to Live Chat, Log Reader and the Screenshot Editor."
        }),
        new("0.4.3", "21/AUG/2026", new[]
        {
            "Added automatic coloring for door lock and unlock messages.",
            "Added recognition for MOTD, welcome, weather, temperature, wind, humidity and precipitation messages.",
            "Colored login, disconnect and logout separators consistently.",
            "Kept the refinements shared across Live Chat, Log Reader and the Screenshot Editor."
        }),
        new("0.4.2", "21/AUG/2026", new[]
        {
            "Unified automatic chat-color recognition across Live Chat, Log Reader and the Screenshot Editor.",
            "Added mixed-color spans for recognized chat messages instead of relying only on whole-line colors.",
            "Moved Log Reader into the Library navigation category.",
            "Extended Screenshot Editor horizontal and vertical chat-position controls to 1000 pixels."
        }),
        new("0.4.1", "21/AUG/2026", new[]
        {
            "Added Editor zoom, fit-to-screen and fullscreen preview controls.",
            "Added configurable text drop shadows and text strokes up to 5px.",
            "Expanded automatic roleplay chat coloring and added manual per-line color overrides.",
            "Reorganized Editor tools into a cleaner compact tool-panel layout.",
            "Added additional image, paint and export usability improvements."
        }),
        new("0.4.0", "21/AUG/2026", new[]
        {
            "Introduced the RP Screenshot Editor.",
            "Added chat text rendering with roleplay-aware colors and selectable system fonts.",
            "Added screenshot loading, positioning, image adjustments, paint, erase and text markup.",
            "Added PNG export and clipboard image copying while keeping capture/archive behaviour independent."
        }),
        new("0.3.2", "21/AUG/2026", new[]
        {
            "Polished sidebar navigation with clear Overview, Chat and Library sections.",
            "Improved dark calendar popups and custom dark context menus.",
            "Expanded Log Reader toolbar and archive-opening controls.",
            "Fixed Log Reader imports, collection-view wiring and dispatcher warnings."
        }),
        new("0.3.1", "21/AUG/2026", new[]
        {
            "Added the dedicated Log Reader with line numbers and shared Live Chat presentation settings.",
            "Added exact-line navigation from archive, search, bookmarks and notes.",
            "Added bookmark, note and context-copy actions inside Log Reader.",
            "Improved date controls, Settings placement and live session information."
        }),
        new("0.3.0", "21/AUG/2026", new[]
        {
            "Added Bookmarks & Notes with source log and exact-line references.",
            "Added server/date search filters and persistent search history.",
            "Added archive statistics, capture health and Recovery Center tools.",
            "Added visible/complete export choices and collision-safe export naming.",
            "Expanded live session details and general quality-of-life tools."
        }),
        new("0.2.9", "21/AUG/2026", new[]
        {
            "Expanded OOC filtering to cover information/system-style chat lines.",
            "Improved session finalization behaviour when FiveM closes.",
            "Added clearer saved-session feedback around disconnects and completed logs."
        }),
        new("0.2.8", "21/AUG/2026", new[]
        {
            "Fixed OOC Live Chat filtering so hidden OOC content remains safely archived.",
            "Improved private-message and OOC presentation.",
            "Compacted FiveM connection/status presentation."
        }),
        new("0.2.7", "21/AUG/2026", new[]
        {
            "Added a persistent last-session chat replay cache stored locally.",
            "Allowed Parse current chat to replay the most recently captured session when FiveM is unavailable.",
            "Kept the replay cache separate from permanent archive and recovery files."
        }),
        new("0.2.6", "21/AUG/2026", new[]
        {
            "Refined Live Chat OOC filtering and visible-export behaviour.",
            "Improved search clearing and presentation behaviour.",
            "Cleaned Windows build workflow warnings and related UI edge cases."
        }),
        new("0.2.5", "21/AUG/2026", new[]
        {
            "Added export-success notifications and quick access to exported file locations.",
            "Added the first update-checking interface.",
            "Improved server detection and server-aware chatlog handling leading into later releases."
        }),
        new("0.2.4", "21/AUG/2026", new[]
        {
            "Established the initial Afterline Windows application for chat capture, archive browsing and search.",
            "Added Live Chat, crash/recovery protection and same-day chatlog continuation.",
            "Added server-aware daily log storage and application/tray branding.",
            "Established the single-file Windows x64 build workflow."
        })
    };
}
