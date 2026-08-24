using System.Windows;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _finalRuntimeOptimizationV066Initialized;
    private bool _finalChannelHandoffV066Initialized;

    private void EnsureFinalRuntimeOptimizationV066()
    {
        if (_finalRuntimeOptimizationV066Initialized || _editorPage is null)
            return;

        _finalRuntimeOptimizationV066Initialized = true;

        // Promote only the final Editor refinement behavior. Earlier Canary passes
        // remain compiled for compatibility, but their superseded initialization
        // paths are deliberately skipped to avoid rebuilding the same controls,
        // attaching duplicate handlers, and prewarming the same image twice.
        _canaryEditorRefinementV2Initialized = true;
        RemovePaintAndMarkupCanaryV2();
        RebuildEditorToolRailCanaryV2();
        BuildEditorTaskBarCanaryV2();
        ConfigureFilterEnhancementsCanaryV2();
        ConfigureEditorHistoryCanaryV2();
        ConfigureFullscreenSpacingCanaryV2();
        // Object Select is intentionally not initialized. The final selection panel
        // exposes Rectangular, Lasso, and Polygonal selection only.

        // V3 is now retained only for the shared shortcut parser/dispatcher used by
        // the final controls. Do not initialize its superseded menu, updater, object
        // selection, settings, or slider-prewarm patches.
        _canaryUiFixesV3Initialized = true;
        ConfigureCustomKeybindsCanaryV3();

        // Build the final V4 interface directly, excluding its transitional updater
        // wiring. BuildIdentityV065 owns update polling and the update action button.
        _canaryRuntimeFixesV4Initialized = true;
        RebuildEditorTaskbarCanaryV4();
        RebuildSelectionPanelCanaryV4();
        RebuildExportPanelCanaryV4();
        RebuildEditorSettingsPanelCanaryV4();
        RebuildApplicationKeybindSettingsCanaryV4();
        CenterSettingsNavigationCanaryV4();
        ConfigureEditorPrewarmCanaryV4();

        // Canary discovery now uses a lightweight release manifest. Ten-minute
        // polling plus a throttled activation refresh keeps discovery prompt without
        // hammering GitHub or racing several duplicate checks during startup.
        _buildIdentityRefreshTimerV065.Interval = TimeSpan.FromMinutes(10);
    }

    private void EnsureFinalChannelHandoffV066()
    {
        if (_finalChannelHandoffV066Initialized || _updateChannelButtonV062 is null)
            return;

        _finalChannelHandoffV066Initialized = true;
        _updateChannelButtonV062.Click -= UpdateChannelV062_Click;
        _updateChannelButtonV062.Click -= UpdateChannelWithResilientHandoffCanaryV2_Click;
        _updateChannelButtonV062.Click += UpdateChannelWithFinalHandoffV066_Click;
    }

    private async void UpdateChannelWithFinalHandoffV066_Click(object sender, RoutedEventArgs e)
    {
        if (_updateInstallInProgress || _canaryCheckInProgressV062)
            return;

        bool currentlyCanary = IsCanaryChannelV062();
        MessageBoxResult confirm = System.Windows.MessageBox.Show(
            this,
            currentlyCanary
                ? "Return to the Stable update channel?\n\nAfterline will replace this Canary build with the latest published Stable release."
                : "Switch to Afterline Canary?\n\nCanary builds are experimental. They may update frequently, contain unfinished features, or break unexpectedly.\n\nOnly continue if you're comfortable testing unstable builds.",
            currentlyCanary ? "Return to Stable" : "Switch to Canary",
            MessageBoxButton.YesNo,
            currentlyCanary ? MessageBoxImage.Question : MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        _canaryCheckInProgressV062 = true;
        SetChannelButtonsEnabledV062(false);

        string oldChannel = _settings.UpdateChannel;
        string? oldBuild = _settings.InstalledCanaryBuild;

        try
        {
            UpdateCheckResult release;
            string? targetBuild = null;

            if (currentlyCanary)
            {
                release = await _updateService.CheckAsync(CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(release.Error))
                {
                    System.Windows.MessageBox.Show(
                        this,
                        release.Error,
                        "Stable release unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                _settings.UpdateChannel = "Stable";
                _settings.InstalledCanaryBuild = null;
            }
            else
            {
                CanaryUpdateCheckResult canary = await _canaryUpdateServiceV062.CheckAsync(CancellationToken.None);
                release = canary.Release;
                targetBuild = canary.BuildId;
                if (!string.IsNullOrWhiteSpace(release.Error))
                {
                    System.Windows.MessageBox.Show(
                        this,
                        release.Error,
                        "Canary unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                _settings.UpdateChannel = "Canary";
                _settings.InstalledCanaryBuild = targetBuild;
            }

            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();

            // Both Stable -> Canary and Canary -> Stable now use the same detached,
            // retrying installer. This removes the last path that could fall back to
            // the older self-copy updater and stall on a locked Afterline.exe.
            bool launched = await InstallAvailableReleaseWithResilientHandoffCanaryV4(release);
            if (!launched)
            {
                _settings.UpdateChannel = oldChannel;
                _settings.InstalledCanaryBuild = oldBuild;
                _settingsService.Save(_settings);
                RefreshUpdateChannelUiV062();
            }
        }
        catch (Exception ex)
        {
            _settings.UpdateChannel = oldChannel;
            _settings.InstalledCanaryBuild = oldBuild;
            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();
            DiagnosticLogger.Error("Unable to switch Afterline update channel.", ex);
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Unable to switch update channel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _canaryCheckInProgressV062 = false;
            if (!_updateInstallInProgress)
                SetChannelButtonsEnabledV062(true);
        }
    }
}
