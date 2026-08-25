using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private readonly UpdateService _updateService = new();
    private bool _notificationAndUpdateUiInitialized;
    private Border? _exportToast;
    private TextBlock? _exportToastTitleText;
    private TextBlock? _exportToastFileText;
    private CancellationTokenSource? _exportToastCts;
    private string? _lastExportPath;
    private TextBlock? _currentBuildText;
    private TextBlock? _latestBuildText;
    private Button? _checkUpdatesButton;
    private bool _updateInstallInProgress;

    private void EnsureNotificationAndUpdateUi()
    {
        if (_notificationAndUpdateUiInitialized) return;
        _notificationAndUpdateUiInitialized = true;

        ConfigureExportToast();
        ConfigureUpdatePanel();
        _uiTimer.Tick += UpdateVersionFooter_Tick;
        UpdateVersionFooter_Tick(this, EventArgs.Empty);
        // Canary has its own build-number-aware discovery path. Avoid spending a
        // second request on Stable during every Canary startup.
        if (!IsCanaryBinaryV062() &&
            !string.Equals(_settings.UpdateChannel, "Canary", StringComparison.OrdinalIgnoreCase))
        {
            _ = CheckForUpdatesAsync(false);
        }
    }

    private void UpdateVersionFooter_Tick(object? sender, EventArgs e)
    {
        string processInfo = _processor.LastProcessedAt is DateTime processed
            ? $" · archive processed {processed:HH:mm:ss}"
            : string.Empty;
        string channel = IsCanaryBinaryV062() ? " · Canary" : string.Empty;
        BottomStatusText.Text = $"Afterline {GetCurrentBuildVersion()}{processInfo}{channel}";
    }

    private void ConfigureExportToast()
    {
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _exportToastTitleText = new TextBlock
        {
            Text = "Chatlog saved",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(_exportToastTitleText);

        var close = new Button
        {
            Content = "×",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(8, -4, -4, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Close"
        };
        close.Click += (_, _) => HideExportNotification();
        Grid.SetColumn(close, 1);
        content.Children.Add(close);

        _exportToastFileText = new TextBlock
        {
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(_exportToastFileText, 1);
        Grid.SetColumnSpan(_exportToastFileText, 2);
        content.Children.Add(_exportToastFileText);

        var openText = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var openLink = new Hyperlink(new Run("Open file location"))
        {
            Foreground = (System.Windows.Media.Brush)FindResource("Accent")
        };
        openLink.Click += (_, _) => OpenExportLocation();
        openText.Inlines.Add(openLink);
        Grid.SetRow(openText, 2);
        Grid.SetColumnSpan(openText, 2);
        content.Children.Add(openText);

        var ok = new Button
        {
            Content = "OK",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        ok.Click += (_, _) => HideExportNotification();
        Grid.SetRow(ok, 3);
        Grid.SetColumnSpan(ok, 2);
        content.Children.Add(ok);

        _exportToast = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("Raised"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("Accent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Width = 365,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 78, 16, 0),
            Child = content,
            Visibility = Visibility.Collapsed,
            Opacity = 0
        };

        Grid.SetRowSpan(_exportToast, 5);
        Panel.SetZIndex(_exportToast, 100);
        if (LivePage.Parent is Panel mainContent)
            mainContent.Children.Add(_exportToast);
    }

    private void ShowExportSuccessNotification(string path)
    {
        ShowInAppFileNotification(
            "Chatlog saved",
            $"{Path.GetFileName(path)} was saved to {StreamerModePresentationService.PathForDisplay(Path.GetDirectoryName(path))}.",
            path);
    }

    private void ShowArchiveSuccessNotification(string path)
    {
        ShowInAppFileNotification(
            "Chatlog safely archived",
            $"{Path.GetFileName(path)} was safely parsed and archived.",
            path);
    }

    private void ShowInAppFileNotification(string title, string message, string path)
    {
        if (_exportToast is null || _exportToastTitleText is null || _exportToastFileText is null)
            return;

        _lastExportPath = path;
        _exportToastCts?.Cancel();
        _exportToastCts?.Dispose();
        _exportToastCts = new CancellationTokenSource();

        _exportToast.BeginAnimation(OpacityProperty, null);
        _exportToastTitleText.Text = title;
        _exportToastFileText.Text = message;
        _exportToast.Visibility = Visibility.Visible;
        _exportToast.Opacity = 1;

        _ = AutoHideExportNotificationAsync(_exportToastCts.Token);
    }

    private async Task AutoHideExportNotificationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                Dispatcher.Invoke(() => HideExportNotification(true));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HideExportNotification(bool animate = false)
    {
        _exportToastCts?.Cancel();
        if (_exportToast is null) return;

        if (!animate)
        {
            _exportToast.BeginAnimation(OpacityProperty, null);
            _exportToast.Opacity = 0;
            _exportToast.Visibility = Visibility.Collapsed;
            return;
        }

        var fade = new DoubleAnimation
        {
            From = _exportToast.Opacity,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(350))
        };
        fade.Completed += (_, _) =>
        {
            if (_exportToast is not null)
                _exportToast.Visibility = Visibility.Collapsed;
        };
        _exportToast.BeginAnimation(OpacityProperty, fade);
    }

    private void OpenExportLocation()
    {
        if (string.IsNullOrWhiteSpace(_lastExportPath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_lastExportPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open the exported chatlog location.", ex);
        }
    }

    private void ConfigureUpdatePanel()
    {
        if (TrayStateText.Parent is not StackPanel panel) return;

        panel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 7) });

        _currentBuildText = new TextBlock
        {
            FontSize = 9.5,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        panel.Children.Add(_currentBuildText);

        _latestBuildText = new TextBlock
        {
            FontSize = 9.5,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };
        panel.Children.Add(_latestBuildText);

        _checkUpdatesButton = new Button
        {
            Content = "Check",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(7, 5, 7, 5),
            Margin = new Thickness(0, 7, 0, 0),
            ToolTip = "Check for updates"
        };
        _checkUpdatesButton.Click += CheckForUpdates_Click;
        panel.Children.Add(_checkUpdatesButton);

        SetUpdateBuildLines(GetCurrentBuildVersion(), "Checking…");
    }

    private void SetUpdateBuildLines(string current, string latest)
    {
        bool isCanary = current.Contains("Canary", StringComparison.OrdinalIgnoreCase);
        string currentCompact = CompactUpdateIdentityV075(current);
        string latestComparable = latest.Replace(" available", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        bool upToDate = !latest.Contains("Checking", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(current.Trim(), latestComparable, StringComparison.OrdinalIgnoreCase);

        if (_currentBuildText is not null)
        {
            _currentBuildText.Inlines.Clear();
            _currentBuildText.Inlines.Add(new Run(isCanary ? "CANARY " : "STABLE ")
            {
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)FindResource(isCanary ? "Accent" : "Success")
            });
            _currentBuildText.Inlines.Add(new Run(currentCompact)
            {
                Foreground = (System.Windows.Media.Brush)FindResource("MutedText")
            });
        }

        if (_latestBuildText is not null)
        {
            _latestBuildText.Visibility = upToDate ? Visibility.Collapsed : Visibility.Visible;
            _latestBuildText.Inlines.Clear();
            _latestBuildText.Inlines.Add(new Run("Latest ") { FontWeight = FontWeights.SemiBold });
            _latestBuildText.Inlines.Add(new Run(CompactUpdateIdentityV075(latest))
            {
                Foreground = (System.Windows.Media.Brush)FindResource("Accent")
            });
        }
    }

    private static string CompactUpdateIdentityV075(string value)
    {
        string compact = value.Trim();
        compact = compact.Replace(" available", string.Empty, StringComparison.OrdinalIgnoreCase);
        compact = compact.Replace(" Canary", string.Empty, StringComparison.OrdinalIgnoreCase);
        compact = compact.Replace(" Stable", string.Empty, StringComparison.OrdinalIgnoreCase);

        int buildMarker = compact.IndexOf('#');
        if (buildMarker >= 0)
            return compact[buildMarker..];

        if (Version.TryParse(compact, out _))
            return "v" + compact;
        return compact;
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        => await CheckForUpdatesAsync(true);

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_updateInstallInProgress) return;
        if (_checkUpdatesButton is not null) _checkUpdatesButton.IsEnabled = false;
        string current = GetCurrentBuildVersion();
        SetUpdateBuildLines(current, "Checking…");

        try
        {
            UpdateCheckResult result = await _updateService.CheckAsync(CancellationToken.None);
            string latest = string.IsNullOrWhiteSpace(result.LatestVersion)
                ? "Unavailable"
                : result.LatestVersion;
            SetUpdateBuildLines(current, latest);

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                if (userInitiated)
                {
                    System.Windows.MessageBox.Show(
                        this,
                        result.Error,
                        "Afterline Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(result.LatestVersion)) return;
            if (!UpdateService.IsNewer(result.LatestVersion, current))
            {
                if (userInitiated)
                {
                    System.Windows.MessageBox.Show(
                        this,
                        $"Afterline {current} is already the latest release.",
                        "Afterline Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            SetUpdateBuildLines(current, result.LatestVersion + " available");
            if (!userInitiated) return;

            var window = new UpdateAvailableWindow(this, current, result);
            if (window.ShowDialog() == true && window.InstallRequested)
                await InstallUpdateAsyncV060(result);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Update check failed.", ex);
            SetUpdateBuildLines(current, "Unavailable");
            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    this,
                    ex.Message,
                    "Unable to check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            if (_checkUpdatesButton is not null && !_updateInstallInProgress)
                _checkUpdatesButton.IsEnabled = true;
        }
    }

    private async Task InstallUpdateAsyncV060(UpdateCheckResult release)
    {
        if (!UpdateService.CanSelfUpdate(out string? reason))
        {
            System.Windows.MessageBox.Show(
                this,
                reason ?? "Afterline cannot update itself from the current folder.",
                "Unable to self-update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _updateInstallInProgress = true;
        if (_checkUpdatesButton is not null) _checkUpdatesButton.IsEnabled = false;
        SetUpdateBuildLines(GetCurrentBuildVersion(), "Downloading…");

        try
        {
            UpdateDownloadResult download = await _updateService.DownloadVerifiedAsync(release, CancellationToken.None);
            SetUpdateBuildLines(GetCurrentBuildVersion(), "Verified · restarting…");

            // Start the verified replacement first; it waits for this process to exit.
            UpdateService.LaunchUpdater(download);

            // Dispose capture/recovery services before terminating this process so
            // the updater never turns a clean update into an unexpected-shutdown recovery.
            try { await _capture.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLogger.Error("Capture shutdown during update failed.", ex); }
            try { await _processor.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLogger.Error("Background processor shutdown during update failed.", ex); }

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
        }
        catch (Exception ex)
        {
            _updateInstallInProgress = false;
            DiagnosticLogger.Error("Unable to install the Afterline update.", ex);
            SetUpdateBuildLines(GetCurrentBuildVersion(), "Update failed");
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Unable to install update",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            if (_checkUpdatesButton is not null) _checkUpdatesButton.IsEnabled = true;
        }
    }

    private static string GetCurrentBuildVersion()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null) return "0.0.0";
        return $"{Math.Max(0, version.Major)}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";
    }
}
