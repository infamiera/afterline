using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private static readonly Regex LiveClockTimestamp = new(
        @"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]",
        RegexOptions.Compiled);

    private bool _liveSessionInfoInitialized;
    private TextBlock? _liveSessionInfoText;
    private TextBlock? _serverClockText;
    private TextBlock? _captureHealthText;
    private DateTime? _localSessionObservedAt;
    private TimeSpan? _estimatedServerClockDelta;
    private bool _serverClockSyncedFromLive;
    private string? _serverClockSyncSource;

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

        var serverClockRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _serverClockText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        var resyncClockButton = new Button
        {
            Content = "↻",
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Resync server time from the latest generated chatlog."
        };
        resyncClockButton.Click += ServerClockResync_Click;

        serverClockRow.Children.Add(_serverClockText);
        serverClockRow.Children.Add(resyncClockButton);

        _captureHealthText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        };

        infoPanel.Children.Add(_liveSessionInfoText);
        infoPanel.Children.Add(serverClockRow);
        infoPanel.Children.Add(_captureHealthText);
        leftPanel.Children.Add(infoPanel);

        _capture.MessageCaptured += LiveSessionInfo_MessageCaptured;
        _capture.ServerSessionChanged += LiveSessionInfo_ServerSessionChanged;
        _uiTimer.Tick += LiveSessionInfo_Tick;

        if (_capture.CurrentServer is not null)
            _localSessionObservedAt = DateTime.Now;

        TrySyncServerClockFromLatestChatlog();
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
            _serverClockSyncedFromLive = false;
            _serverClockSyncSource = null;

            if (e.Server is not null)
                TrySyncServerClockFromLatestChatlog(e.Server.ArchiveLabel);

            UpdateLiveSessionInformation();
        }));
    }

    private void ServerClockResync_Click(object sender, RoutedEventArgs e)
    {
        _estimatedServerClockDelta = null;
        _serverClockSyncedFromLive = false;
        _serverClockSyncSource = null;

        string? archiveLabel = _capture.CurrentServer?.ArchiveLabel ?? _journal.ActiveServerName;
        TrySyncServerClockFromLatestChatlog(archiveLabel);
        UpdateLiveSessionInformation();
    }

    private void TryInferServerClock(ChatEntry entry)
    {
        if (_serverClockSyncedFromLive || entry.IsSystemMessage) return;
        if (!TryCalculateClockDelta(entry.Text, DateTime.Now, out TimeSpan rounded)) return;

        _estimatedServerClockDelta = rounded;
        _serverClockSyncedFromLive = true;
        _serverClockSyncSource = "live chat";
    }

    private bool TrySyncServerClockFromLatestChatlog(string? preferredServer = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ArchiveRoot) || !Directory.Exists(_settings.ArchiveRoot))
                return false;

            string? currentServer = string.IsNullOrWhiteSpace(preferredServer)
                ? _capture.CurrentServer?.ArchiveLabel ?? _journal.ActiveServerName
                : preferredServer;

            var candidates = new List<FileInfo>();
            if (!string.IsNullOrWhiteSpace(_journal.ActiveFile) && File.Exists(_journal.ActiveFile))
                candidates.Add(new FileInfo(_journal.ActiveFile));

            candidates.AddRange(
                Directory.EnumerateFiles(_settings.ArchiveRoot, "*.txt", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .Where(file => candidates.All(existing =>
                        !string.Equals(existing.FullName, file.FullName, StringComparison.OrdinalIgnoreCase))));

            if (candidates.Count == 0) return false;

            IEnumerable<FileInfo> ordered = candidates.OrderByDescending(file => file.LastWriteTimeUtc);
            if (!string.IsNullOrWhiteSpace(currentServer))
            {
                FileInfo? matching = ordered.FirstOrDefault(file =>
                    string.Equals(
                        ParseServerFromArchiveName(file.Name),
                        currentServer,
                        StringComparison.OrdinalIgnoreCase));

                if (matching is not null && TrySyncServerClockFromFile(matching))
                    return true;
            }

            foreach (FileInfo file in ordered)
            {
                if (TrySyncServerClockFromFile(file)) return true;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to sync server clock from the latest chatlog.", ex);
        }

        return false;
    }

    private bool TrySyncServerClockFromFile(FileInfo file)
    {
        try
        {
            string[] lines = File.ReadAllLines(file.FullName);
            DateTime observedAt = file.LastWriteTime;

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (!TryCalculateClockDelta(lines[i], observedAt, out TimeSpan rounded)) continue;

                _estimatedServerClockDelta = rounded;
                _serverClockSyncedFromLive = false;
                _serverClockSyncSource = file.Name;
                return true;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error($"Unable to use {file.FullName} for server time sync.", ex);
        }

        return false;
    }

    private static bool TryCalculateClockDelta(string text, DateTime observedAt, out TimeSpan rounded)
    {
        rounded = default;
        Match match = LiveClockTimestamp.Match(text);
        if (!match.Success || !DateTime.TryParseExact(
                match.Groups["time"].Value,
                "H:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
            return false;

        DateTime serverClock = observedAt.Date.Add(parsed.TimeOfDay);
        TimeSpan delta = serverClock - observedAt;
        while (delta > TimeSpan.FromHours(12)) delta -= TimeSpan.FromDays(1);
        while (delta < TimeSpan.FromHours(-12)) delta += TimeSpan.FromDays(1);

        double roundedMinutes = Math.Round(delta.TotalMinutes / 15.0) * 15.0;
        rounded = TimeSpan.FromMinutes(roundedMinutes);
        return Math.Abs(rounded.TotalHours) <= 14;
    }

    private void LiveSessionInfo_Tick(object? sender, EventArgs e)
        => UpdateLiveSessionInformation();

    private void UpdateLiveSessionInformation()
    {
        if (_liveSessionInfoText is null || _serverClockText is null || _captureHealthText is null) return;

        DateTime now = DateTime.Now;
        string server = _capture.CurrentServer?.DisplayName ?? _journal.ActiveServerName ?? "No active server";
        string duration = _localSessionObservedAt is DateTime started ? FormatSessionDuration(now - started) : "00:00:00";

        _liveSessionInfoText.Text = $"{server} · Session {duration} · Local {now:HH:mm:ss}";

        if (_estimatedServerClockDelta is TimeSpan delta)
        {
            DateTime serverNow = now + delta;
            TimeSpan utcOffset = NormalizeUtcOffset(TimeZoneInfo.Local.GetUtcOffset(now) + delta);
            _serverClockText.Text = $"Server {serverNow:HH:mm:ss} ({FormatUtcOffset(utcOffset)})";
            _serverClockText.ToolTip = string.IsNullOrWhiteSpace(_serverClockSyncSource)
                ? "Server time estimate."
                : $"Server time synchronized from {_serverClockSyncSource}.";
        }
        else
        {
            _serverClockText.Text = "Server time: detecting…";
            _serverClockText.ToolTip = "Waiting for a timestamped chat line or a usable recent chatlog.";
        }

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
