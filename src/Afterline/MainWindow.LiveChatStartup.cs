namespace Afterline;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureFirstRunSetup();
        EnsureLiveChatEnhancements();
        EnsureDailyLogRolloverUiV054();
        EnsureOocExportFiltering();
        EnsureCachedReplayUi();
        EnsureUiBehaviorFixes();
        EnsureLiveContextBookmarks();
        EnsureLiveChatContextMenuRepair();
        EnsureLiveRightClickFixV053();
        EnsureExportChoices();
        EnsureNotesBookmarksPage();
        EnsureNotesBookmarksPresentation();
        EnsureLogReader();
        EnsureLogReaderPresentationSync();
        EnsureLogReaderToolbar();
        EnsureLiveSessionInfo();
        EnsureQolSearchRecoveryStats();
        EnsureRawCaptureRecoveryV054();
        EnsureDarkSearchCalendarPopups();
        EnsureSessionTrayNotification();
        EnsureEditor();
        EnsureEditorV041();
        EnsureEditorMediaV060();
        EnsureSettingsButtonPlacement();
        EnsureThemeAndAbout();
        EnsureUnifiedChatPresentation();
        EnsureEditorPreferences();
        EnsureQolV050();
        EnsureChangelogUi();
    }
}
