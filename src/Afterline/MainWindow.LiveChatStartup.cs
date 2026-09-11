namespace Afterline;

public partial class MainWindow
{
    private bool _deferredUiInitializationStarted;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureFirstRunSetup();

        if (System.Windows.Application.Current is App app)
            app.ConfirmHealthyStartup();

        if (_deferredUiInitializationStarted) return;
        _deferredUiInitializationStarted = true;
        _ = InitializeDeferredUiAsync();
    }

    private async Task InitializeDeferredUiAsync()
    {
        await RunDeferredUiStageAsync(
            "UI initialization",
            PopulateSettingsUi,
            EnsureLiveChatEnhancements,
            EnsureDailyLogRolloverUiV054,
            EnsureOocExportFiltering,
            EnsureCachedReplayUi,
            EnsureUiBehaviorFixes,
            EnsureLiveContextBookmarks,
            EnsureLiveChatContextMenuRepair,
            EnsureLiveRightClickFixV053,
            EnsureExportChoices,
            EnsureNotesBookmarksPage,
            EnsureNotesBookmarksPresentation,
            EnsureLogReader,
            EnsureLogReaderPresentationSync,
            EnsureLogReaderToolbar,
            EnsureLiveSessionInfo,
            EnsureQolSearchRecoveryStats,
            EnsureRawCaptureRecoveryV054,
            EnsureDarkSearchCalendarPopups,
            EnsureSessionArchiveNotification);

        await RunDeferredUiStageAsync(
            "Editor initialization",
            EnsureEditor,
            EnsureFiveMScreenshotCaptureV074,
            EnsureSettingsButtonPlacement,
            EnsureThemeAndAbout,
            EnsureUnifiedChatPresentation,
            EnsureCanaryMiscPolish,
            EnsureQolV050,
            EnsureArchiveFilteringV071,
            EnsureLiveFindLayoutV062,
            EnsureChangelogUi,
            EnsureUpdatePanelV061,
            EnsureUpdateChannelV062,
            EnsureCanaryUpdateHandoffV2,
            EnsureSettingsCanarySidebarV2);

        await RunDeferredUiStageAsync(
            "final interface initialization",
            EnsureBuildIdentityV065,
            EnsureFinalChannelHandoffV066,
            EnsureCoreRuntimeOptimizationV091,
            EnsureModernThemeShellV090);

        RunEditorImageSmokeTestIfRequestedV069();
    }

    private async Task RunDeferredUiStageAsync(string stage, params Action[] initializers)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Afterline.Services.DiagnosticLogger.Info($"Startup: {stage} started.");
        foreach (Action initialize in initializers)
        {
            // Input and rendering receive a dispatcher turn between every optional
            // initializer, keeping the already-visible shell navigable.
            await System.Windows.Threading.Dispatcher.Yield(
                System.Windows.Threading.DispatcherPriority.Background);
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                Afterline.Services.DiagnosticLogger.Error(
                    $"Startup: {stage} step '{initialize.Method.Name}' failed.",
                    ex);
            }
        }

        Afterline.Services.DiagnosticLogger.Info(
            $"Startup: {stage} completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
    }
}
