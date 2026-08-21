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
    private TextBlock? _exportToastFileText;
    private CancellationTokenSource? _exportToastCts;
    private string? _lastExportPath;
    private TextBlock? _currentBuildText;
    private TextBlock? _latestBuildText;
    private Button? _checkUpdatesButton;

    private void EnsureNotificationAndUpdateUi()
    {
        if (_notificationAndUpdateUiInitialized) return;
        _notificationAndUpdateUiInitialized = true;

        ConfigureExportToast();
        ConfigureUpdatePanel();
        _uiTimer.Tick += UpdateVersionFooter_Tick;
        UpdateVersionFooter_Tick(this, EventArgs.Empty);
        _ = CheckForUpdatesAsync(false);
    }

    private void UpdateVersionFooter_Tick(object? sender, EventArgs e)
    {
        string processInfo = _processor.LastProcessedAt is DateTime processed
            ? $" · archive processed {processed:HH:mm:ss}"
            : string.Empty;
        BottomStatusText.Text = $"Afterline {GetCurrentBuildVersion()}{processInfo}";
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

        var title = new TextBlock
        {
            Text = "Chatlog saved",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(title);

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

        Grid.SetRowSpan(_exportToast, 3);
        Panel.SetZIndex(_exportToast, 100);
        LivePage.Children.Add(_exportToast);
    }

    private void ShowExportSuccessNotification(string path)
    {
        if (_exportToast is null || _exportToastFileText is null) return;

        _lastExportPath = path;
        _exportToastCts?.Cancel();
        _exportToastCts?.Dispose();
        _exportToastCts = new CancellationTokenSource();

        _exportToast.BeginAnimation(OpacityProperty, null);
        _exportToastFileText.Text = $"{Path.GetFileName(path)} was saved to {Path.GetDirectoryName(path)}.";
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

        panel.Children.Add(new Separator { Margin = new Thickness(0, 11, 0, 10) });

        _currentBuildText = new TextBlock
        {
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(_currentBuildText);

        _latestBuildText = new TextBlock
        {
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(_latestBuildText);

        _checkUpdatesButton = new Button
        {
            Content = "Check for updates",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(9, 6, 9, 6),
            Margin = new Thickness(0, 10, 0, 0)
        };
        _checkUpdatesButton.Click += CheckForUpdates_Click;
        panel.Children.Add(_checkUpdatesButton);

        SetUpdateBuildLines(GetCurrentBuildVersion(), "Checking…");
    }

    private void SetUpdateBuildLines(string current, string latest)
    {
        if (_currentBuildText is not null)
        {
            _currentBuildText.Inlines.Clear();
            _currentBuildText.Inlines.Add(new Run("Current Build: ") { FontWeight = FontWeights.SemiBold });
            _currentBuildText.Inlines.Add(new Run(current)
            {
                Foreground = (System.Windows.Media.Brush)FindResource("Success")
            });
        }

        if (_latestBuildText is not null)
        {
            _latestBuildText.Inlines.Clear();
            _latestBuildText.Inlines.Add(new Run("Latest: ") { FontWeight = FontWeights.SemiBold });
            _latestBuildText.Inlines.Add(new Run(latest)
            {
                Foreground = (System.Windows.Media.Brush)FindResource("Accent")
            });
        }
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        => await CheckForUpdatesAsync(true);

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
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
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Update check failed.", ex);
            SetUpdateBuildLines(current, "Unavailable");
        }
        finally
        {
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
