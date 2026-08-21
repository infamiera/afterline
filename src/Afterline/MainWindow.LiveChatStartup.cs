namespace Afterline;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureLiveChatEnhancements();
        EnsureOocExportFiltering();
        EnsureCachedReplayUi();
        EnsureUiBehaviorFixes();
        EnsureLiveContextBookmarks();
        EnsureExportChoices();
        EnsureNotesBookmarksPage();
        EnsureLiveSessionInfo();
        EnsureQolSearchRecoveryStats();
        EnsureSessionTrayNotification();
    }
}
