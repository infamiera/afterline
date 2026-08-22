using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private readonly CanaryUpdateService _canaryUpdateServiceV062 = new();
    private bool _updateChannelV062Initialized;
    private TextBlock? _updateChannelTextV062;
    private TextBlock? _updateChannelWarningV062;
    private Button? _updateChannelButtonV062;
    private bool _canaryCheckInProgressV062;

    private void EnsureUpdateChannelV062()
    {
        if (_updateChannelV062Initialized ||
            _checkUpdatesButton is null ||
            SettingsPage.Content is not StackPanel settingsStack)
            return;

        _updateChannelV062Initialized = true;

        if (IsCanaryBinaryV062())
        {
            string? build = GetCurrentCanaryBuildIdV062();
            if (!string.Equals(_settings.UpdateChannel, "Canary", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(build) && !string.Equals(_settings.InstalledCanaryBuild, build, StringComparison.OrdinalIgnoreCase)))
            {
                _settings.UpdateChannel = "Canary";
                if (!string.IsNullOrWhiteSpace(build))
                    _settings.InstalledCanaryBuild = build;
                _settingsService.Save(_settings);
            }
        }

        var settingsCard = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var settingsContent = new StackPanel();
        settingsContent.Children.Add(new TextBlock
        {
            Text = "Update channel",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });

        _updateChannelTextV062 = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        };
        settingsContent.Children.Add(_updateChannelTextV062);

        _updateChannelWarningV062 = new TextBlock
        {
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        settingsContent.Children.Add(_updateChannelWarningV062);

        _updateChannelButtonV062 = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 10, 0, 0),
            MinWidth = 138
        };
        _updateChannelButtonV062.Click += UpdateChannelV062_Click;
        settingsContent.Children.Add(_updateChannelButtonV062);

        settingsCard.Child = settingsContent;
        int insertAt = Math.Max(0, settingsStack.Children.Count - 1);
        settingsStack.Children.Insert(insertAt, settingsCard);

        _checkUpdatesButton.Click -= CheckForUpdates_Click;
        _checkUpdatesButton.Click += ChannelAwareCheckForUpdatesV062_Click;

        _uiTimer.Tick += UpdateChannelFooterV062_Tick;
        RefreshUpdateChannelUiV062();

        if (IsCanaryChannelV062())
            _ = CheckForCanaryUpdatesAsyncV062(false);
    }

    private void UpdateChannelFooterV062_Tick(object? sender, EventArgs e)
    {
        if (!IsCanaryChannelV062()) return;
        if (!BottomStatusText.Text.Contains("Canary", StringComparison.OrdinalIgnoreCase))
            BottomStatusText.Text += " · Canary";
    }

    private bool IsCanaryChannelV062()
        => IsCanaryBinaryV062() ||
           string.Equals(_settings.UpdateChannel, "Canary", StringComparison.OrdinalIgnoreCase);

    private static bool IsCanaryBinaryV062()
    {
        string informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;
        return informational.Contains("-canary.", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetCurrentCanaryBuildIdV062()
    {
        string informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;

        int plus = informational.LastIndexOf('+');
        if (plus >= 0 && plus < informational.Length - 1)
            return informational[(plus + 1)..].Trim();

        return _settings.InstalledCanaryBuild;
    }

    private void RefreshUpdateChannelUiV062()
    {
        if (_updateChannelTextV062 is null ||
            _updateChannelWarningV062 is null ||
            _updateChannelButtonV062 is null)
            return;

        bool canary = IsCanaryChannelV062();
        _updateChannelTextV062.Text = canary ? "Current channel: Canary" : "Current channel: Stable";
        _updateChannelWarningV062.Text = canary
            ? "Canary receives experimental builds frequently. Features may be unfinished or break without warning."
            : "Switch to Canary to test experimental builds before they reach Stable. Canary may update frequently and can contain unfinished or broken changes.";
        _updateChannelButtonV062.Content = canary ? "Return to Stable" : "Try Canary builds";
    }

    private async void ChannelAwareCheckForUpdatesV062_Click(object sender, RoutedEventArgs e)
    {
        if (IsCanaryChannelV062())
            await CheckForCanaryUpdatesAsyncV062(true);
        else
            await CheckForUpdatesAsync(true);
    }

    private async void UpdateChannelV062_Click(object sender, RoutedEventArgs e)
    {
        if (_updateInstallInProgress || _canaryCheckInProgressV062) return;

        if (IsCanaryChannelV062())
            await ReturnToStableV062Async();
        else
            await OptIntoCanaryV062Async();
    }

    private async Task OptIntoCanaryV062Async()
    {
        MessageBoxResult confirm = System.Windows.MessageBox.Show(
            this,
            "Switch to Afterline Canary?\n\nCanary builds are experimental. They may be updated frequently, contain unfinished features, or break unexpectedly.\n\nOnly continue if you're comfortable testing unstable builds.",
            "Switch to Canary",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        _canaryCheckInProgressV062 = true;
        SetChannelButtonsEnabledV062(false);
        try
        {
            CanaryUpdateCheckResult canary = await _canaryUpdateServiceV062.CheckAsync(CancellationToken.None);
            UpdateCheckResult release = canary.Release;
            if (!string.IsNullOrWhiteSpace(release.Error))
            {
                System.Windows.MessageBox.Show(this, release.Error, "Canary unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string oldChannel = _settings.UpdateChannel;
            string? oldBuild = _settings.InstalledCanaryBuild;
            _settings.UpdateChannel = "Canary";
            _settings.InstalledCanaryBuild = canary.BuildId;
            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();

            await InstallUpdateAsyncV060(release);

            _settings.UpdateChannel = oldChannel;
            _settings.InstalledCanaryBuild = oldBuild;
            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();
        }
        finally
        {
            _canaryCheckInProgressV062 = false;
            SetChannelButtonsEnabledV062(true);
        }
    }

    private async Task ReturnToStableV062Async()
    {
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
                System.Windows.MessageBox.Show(this, release.Error, "Stable release unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string oldChannel = _settings.UpdateChannel;
            string? oldBuild = _settings.InstalledCanaryBuild;
            _settings.UpdateChannel = "Stable";
            _settings.InstalledCanaryBuild = null;
            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();

            await InstallUpdateAsyncV060(release);

            _settings.UpdateChannel = oldChannel;
            _settings.InstalledCanaryBuild = oldBuild;
            _settingsService.Save(_settings);
            RefreshUpdateChannelUiV062();
        }
        finally
        {
            _canaryCheckInProgressV062 = false;
            SetChannelButtonsEnabledV062(true);
        }
    }

    private async Task CheckForCanaryUpdatesAsyncV062(bool userInitiated)
    {
        if (_updateInstallInProgress || _canaryCheckInProgressV062) return;

        _canaryCheckInProgressV062 = true;
        SetChannelButtonsEnabledV062(false);

        string current = GetCurrentBuildVersion();
        string currentLabel = current + " Canary";
        SetUpdateBuildLines(currentLabel, "Checking Canary…");

        try
        {
            CanaryUpdateCheckResult canary = await _canaryUpdateServiceV062.CheckAsync(CancellationToken.None);
            UpdateCheckResult result = canary.Release;
            string latestLabel = string.IsNullOrWhiteSpace(result.LatestVersion)
                ? "Unavailable"
                : result.LatestVersion + " Canary";
            SetUpdateBuildLines(currentLabel, latestLabel);

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                if (userInitiated)
                    System.Windows.MessageBox.Show(this, result.Error, "Afterline Canary", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? currentBuild = GetCurrentCanaryBuildIdV062();
            bool available = string.IsNullOrWhiteSpace(currentBuild) ||
                             string.IsNullOrWhiteSpace(canary.BuildId) ||
                             !string.Equals(currentBuild, canary.BuildId, StringComparison.OrdinalIgnoreCase);

            if (!available)
            {
                if (userInitiated)
                    System.Windows.MessageBox.Show(this, "This is already the latest Canary build.", "Afterline Canary", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetUpdateBuildLines(currentLabel, latestLabel + " available");
            if (!userInitiated) return;

            var window = new UpdateAvailableWindow(this, currentLabel, result);
            if (window.ShowDialog() == true && window.InstallRequested)
            {
                string? oldBuild = _settings.InstalledCanaryBuild;
                _settings.UpdateChannel = "Canary";
                _settings.InstalledCanaryBuild = canary.BuildId;
                _settingsService.Save(_settings);

                await InstallUpdateAsyncV060(result);

                _settings.InstalledCanaryBuild = oldBuild;
                _settingsService.Save(_settings);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Canary update check failed.", ex);
            SetUpdateBuildLines(currentLabel, "Unavailable");
            if (userInitiated)
                System.Windows.MessageBox.Show(this, ex.Message, "Unable to check Canary updates", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _canaryCheckInProgressV062 = false;
            SetChannelButtonsEnabledV062(true);
        }
    }

    private void SetChannelButtonsEnabledV062(bool enabled)
    {
        if (_updateChannelButtonV062 is not null)
            _updateChannelButtonV062.IsEnabled = enabled && !_updateInstallInProgress;
        if (_checkUpdatesButton is not null)
            _checkUpdatesButton.IsEnabled = enabled && !_updateInstallInProgress;
    }
}
