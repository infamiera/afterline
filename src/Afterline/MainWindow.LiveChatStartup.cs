namespace Afterline;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureFirstRunSetup();
        EnsureLiveChatEnhancements();
        EnsureOocExportFiltering();
        EnsureCachedReplayUi();
        EnsureUiBehaviorFixes();
        EnsureLiveContextBookmarks();
        EnsureLiveChatContextMenuRepair();
        EnsureExportChoices();
        EnsureNotesBookmarksPage();
        EnsureNotesBookmarksPresentation();
        EnsureLogReader();
        EnsureLogReaderPresentationSync();
        EnsureLogReaderToolbar();
        EnsureLiveSessionInfo();
        EnsureQolSearchRecoveryStats();
        EnsureDarkSearchCalendarPopups();
        EnsureSessionTrayNotification();
        EnsureEditor();
        EnsureEditorV041();
        EnsureSettingsButtonPlacement();
        EnsureThemeAndAbout();
        EnsureUnifiedChatPresentation();
        EnsureEditorPreferences();
        EnsureQolV050();
        EnsureChangelogUi();
    }
}
