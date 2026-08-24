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
        EnsureEditorPositioningV061();
        EnsureEditorAlignmentV062();
        EnsureSettingsButtonPlacement();
        EnsureThemeAndAbout();
        EnsureUnifiedChatPresentation();
        EnsureEditorPreferences();
        EnsureEditorCanaryWorkspace();
        EnsureCanaryMiscPolish();
        EnsureQolV050();
        EnsureLiveFindLayoutV062();
        EnsureChangelogUi();
        EnsureUpdatePanelV061();
        EnsureUpdateChannelV062();
        EnsureCanaryUpdateHandoffV2();
        EnsureSettingsCanarySidebarV2();

        // Finalized Editor/UI initialization replaces the superseded V2/V3/V4
        // startup passes while preserving their tested final behavior.
        EnsureFinalRuntimeOptimizationV066();
        EnsureBuildIdentityV065();
        EnsureFinalChannelHandoffV066();

        // Canary 0.6.7 workspace: persistent selections, visual presets, layers,
        // right-side editing tools, and local project save/load support.
        try
        {
            EnsureEditorWorkspaceV067();
            EnsureEditorSelectionGuardV067();
        }
        catch (Exception ex)
        {
            // Optional Editor enhancements must never take down capture, archives,
            // settings, or the rest of Afterline during application startup.
            Afterline.Services.DiagnosticLogger.Error(
                "Advanced Editor initialization failed; Afterline continued with the available Editor controls.",
                ex);
        }

        if (System.Windows.Application.Current is App app)
            app.ConfirmHealthyStartup();
    }
}
