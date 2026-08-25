using System.Windows;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _canaryUpdateHandoffV2Initialized;

    private void EnsureCanaryUpdateHandoffV2()
    {
        if (_canaryUpdateHandoffV2Initialized || _updateChannelButtonV062 is null)
            return;

        _canaryUpdateHandoffV2Initialized = true;
        _updateChannelButtonV062.Click -= UpdateChannelV062_Click;
        _updateChannelButtonV062.Click += UpdateChannelWithResilientHandoffCanaryV2_Click;
    }

    private async void UpdateChannelWithResilientHandoffCanaryV2_Click(object sender, RoutedEventArgs e)
    {
        if (_updateInstallInProgress || _canaryCheckInProgressV062) return;

        if (!IsCanaryChannelV062())
        {
            // This source currently ships only on Canary. Keep the existing opt-in
            // behavior intact in case the code is later promoted to Stable.
            UpdateChannelV062_Click(sender, e);
            return;
        }

        MessageBoxResult confirm = System.Windows.MessageBox.Show(
            this,
            "Return to the Stable update channel?\n\nAfterline will replace this Canary build with the latest published Stable release.",
            "Return to Stable",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        _canaryCheckInProgressV062 = true;
        SetChannelButtonsEnabledV062(false);
        try
        {
            UpdateCheckResult release = await _updateService.CheckAsync(CancellationToken.None);
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

            string oldChannel = _settings.UpdateChannel;
            string? oldBuild = _settings.InstalledCanaryBuild;
            _settings.UpdateChannel = "Stable";
            _settings.InstalledCanaryBuild = null;
            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();

            bool launched = await InstallChannelUpdateWithResilientHandoffCanaryV2(release);
            if (!launched)
            {
                _settings.UpdateChannel = oldChannel;
                _settings.InstalledCanaryBuild = oldBuild;
                _settingsService.Save(_settings);
                RefreshUpdateChannelUiV062();
            }
        }
        finally
        {
            _canaryCheckInProgressV062 = false;
            SetChannelButtonsEnabledV062(true);
        }
    }

    private async Task<bool> InstallChannelUpdateWithResilientHandoffCanaryV2(UpdateCheckResult release)
    {
        if (!UpdateService.CanSelfUpdate(out string? reason))
        {
            System.Windows.MessageBox.Show(
                this,
                reason ?? "Afterline cannot update itself from the current folder.",
                "Unable to self-update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        _updateInstallInProgress = true;
        SetChannelButtonsEnabledV062(false);
        SetUpdateBuildLines(GetCurrentBuildVersion() + " Canary", "Downloading Stable…");

        try
        {
            UpdateDownloadResult download = await _updateService.DownloadVerifiedAsync(release, CancellationToken.None);
            SetUpdateBuildLines(GetCurrentBuildVersion() + " Canary", "Verified · restarting…");
            CanaryUpdateInstaller.LaunchUpdater(download);

            try { await _capture.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLogger.Error("Capture shutdown during channel update failed.", ex); }
            try { await _processor.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLogger.Error("Background processor shutdown during channel update failed.", ex); }

            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayBrandIcon?.Dispose();
            _trayBrandIcon = null;

            ApplicationHealthMonitor.Stop();
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            _updateInstallInProgress = false;
            DiagnosticLogger.Error("Unable to install the Stable channel update.", ex);
            SetUpdateBuildLines(GetCurrentBuildVersion() + " Canary", "Update failed");
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Unable to switch update channel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
}
