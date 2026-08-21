using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline;

public partial class MainWindow
{
    private static readonly Regex LiveClockTimestamp = new(
        @"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]",
        RegexOptions.Compiled);

    private bool _liveSessionInfoInitialized;
    private TextBlock? _liveSessionInfoText;
    private TextBlock? _captureHealthText;
    private DateTime? _localSessionObservedAt;
    private TimeSpan? _estimatedServerClockDelta;

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
            TryInferServerClock(entry);
            UpdateLiveSessionInformation();
        }));
    }

    private void LiveSessionInfo_ServerSessionChanged(object? sender, ServerSessionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _localSessionObservedAt = e.Server is null ? null : DateTime.Now;
            _estimatedServerClockDelta = null;
            UpdateLiveSessionInformation();
        }));
    }

    private void TryInferServerClock(ChatEntry entry)
    {
        if (_estimatedServerClockDelta is not null || entry.IsSystemMessage) return;
        Match match = LiveClockTimestamp.Match(entry.Text);
        if (!match.Success || !DateTime.TryParseExact(
                match.Groups["time"].Value,
                "H:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
            return;

        DateTime observed = DateTime.Now;
        DateTime serverClock = observed.Date.Add(parsed.TimeOfDay);
        TimeSpan delta = serverClock - observed;
        while (delta > TimeSpan.FromHours(12)) delta -= TimeSpan.FromDays(1);
        while (delta < TimeSpan.FromHours(-12)) delta += TimeSpan.FromDays(1);

        double roundedMinutes = Math.Round(delta.TotalMinutes / 15.0) * 15.0;
        TimeSpan rounded = TimeSpan.FromMinutes(roundedMinutes);
        if (Math.Abs(rounded.TotalHours) <= 14)
            _estimatedServerClockDelta = rounded;
    }

    private void LiveSessionInfo_Tick(object? sender, EventArgs e)
        => UpdateLiveSessionInformation();

    private void UpdateLiveSessionInformation()
    {
        if (_liveSessionInfoText is null || _captureHealthText is null) return;

        DateTime now = DateTime.Now;
        string server = _capture.CurrentServer?.DisplayName ?? _journal.ActiveServerName ?? "No active server";
        string duration = _localSessionObservedAt is DateTime started ? FormatSessionDuration(now - started) : "00:00:00";

        string serverClock = "Server time: detecting…";
        if (_estimatedServerClockDelta is TimeSpan delta)
        {
            DateTime serverNow = now + delta;
            TimeSpan utcOffset = NormalizeUtcOffset(TimeZoneInfo.Local.GetUtcOffset(now) + delta);
            serverClock = $"Server {serverNow:HH:mm:ss} ({FormatUtcOffset(utcOffset)})";
        }

        _liveSessionInfoText.Text = $"{server} · Session {duration} · Local {now:HH:mm:ss} · {serverClock}";

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

    private static TimeSpan NormalizeUtcOffset(TimeSpan value)
    {
        while (value > TimeSpan.FromHours(14)) value -= TimeSpan.FromDays(1);
        while (value < TimeSpan.FromHours(-12)) value += TimeSpan.FromDays(1);
        return value;
    }

    private static string FormatUtcOffset(TimeSpan value)
    {
        string sign = value < TimeSpan.Zero ? "-" : "+";
        value = value.Duration();
        return $"UTC{sign}{(int)value.TotalHours:00}:{value.Minutes:00}";
    }
}
