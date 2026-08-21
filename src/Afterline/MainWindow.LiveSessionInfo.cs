using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _liveSessionInfoInitialized;
    private TextBlock? _liveSessionInfoText;
    private TextBlock? _captureHealthText;
    private DateTime? _localSessionObservedAt;

    private void EnsureLiveSessionInfo()
    {
        if (_liveSessionInfoInitialized) return;
        _liveSessionInfoInitialized = true;

        if (ShowLiveChatCheck.Parent is not Panel optionsParent) return;
        Grid? headerGrid = optionsParent as Grid;
        if (headerGrid is null && optionsParent.Parent is Grid parentGrid) headerGrid = parentGrid;
        if (headerGrid is null) return;

        StackPanel? leftPanel = headerGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 0);
        if (leftPanel is null) return;

        var infoPanel = new StackPanel { Margin = new Thickness(0, 9, 0, 0) };
        _liveSessionInfoText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        _captureHealthText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        };

        infoPanel.Children.Add(_liveSessionInfoText);
        infoPanel.Children.Add(_captureHealthText);
        leftPanel.Children.Add(infoPanel);

        _capture.MessageCaptured += LiveSessionInfo_MessageCaptured;
        _capture.ServerSessionChanged += LiveSessionInfo_ServerSessionChanged;
        _uiTimer.Tick += LiveSessionInfo_Tick;

        if (_capture.CurrentServer is not null)
            _localSessionObservedAt = DateTime.Now;

        UpdateLiveSessionInformation();
    }

    private void LiveSessionInfo_MessageCaptured(object? sender, ChatEntry entry)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _localSessionObservedAt ??= DateTime.Now;
            UpdateLiveSessionInformation();
        }));
    }

    private void LiveSessionInfo_ServerSessionChanged(object? sender, ServerSessionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _localSessionObservedAt = e.Server is null ? null : DateTime.Now;
            UpdateLiveSessionInformation();
        }));
    }

    private void LiveSessionInfo_Tick(object? sender, EventArgs e)
        => UpdateLiveSessionInformation();

    private void UpdateLiveSessionInformation()
    {
        if (_liveSessionInfoText is null || _captureHealthText is null) return;

        DateTime now = DateTime.Now;
        string server = _capture.CurrentServer?.DisplayName ?? _journal.ActiveServerName ?? "No active server";
        string duration = _localSessionObservedAt is DateTime started ? FormatSessionDuration(now - started) : "00:00:00";
        _liveSessionInfoText.Text = $"{server} · Session {duration}";

        string health;
        object brushKey;
        switch (_capture.State)
        {
            case CaptureState.Capturing:
                health = string.IsNullOrWhiteSpace(_capture.LastError) ? "Capture health: Healthy" : "Capture health: Recovering";
                brushKey = string.IsNullOrWhiteSpace(_capture.LastError) ? "Success" : "Warning";
                break;
            case CaptureState.WaitingForNui:
                health = "Capture health: Waiting for chat UI";
                brushKey = "Warning";
                break;
            case CaptureState.ReconnectGrace:
                health = "Capture health: Reconnect grace";
                brushKey = "Warning";
                break;
            case CaptureState.Stopped:
                health = "Capture health: Stopped";
                brushKey = "MutedText";
                break;
            default:
                health = "Capture health: Idle";
                brushKey = "MutedText";
                break;
        }

        _captureHealthText.Text = health;
        _captureHealthText.Foreground = (Brush)FindResource(brushKey);
    }

    private static string FormatSessionDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        int hours = (int)value.TotalHours;
        return $"{hours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
