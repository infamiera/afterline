using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Afterline.Models;
using Afterline.Services;
using Forms = System.Windows.Forms;

namespace Afterline;

public partial class MainWindow : Window
{
    private const int DashboardArchiveDays = 7;
    private const int DashboardArchiveScanLimit = 250;
    private const int ArchivePageEntryLimit = 2000;

    private enum ArchiveRefreshScope
    {
        Dashboard,
        ArchivePage
    }

    private enum ArchiveRefreshMode
    {
        CachedOnly,
        RecentFolders,
        FullRecursive
    }

    private readonly SettingsService _settingsService = new();
    private readonly SessionJournal _journal = new();
    private readonly ArchiveService _archiveService = new();
    private readonly SearchService _searchService = new();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private AppSettings _settings = null!;
    private CaptureCoordinator _capture = null!;
    private BackgroundProcessor _processor = null!;
    private Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayBrandIcon;
    private bool _isExiting;

    public ObservableCollection<ChatEntry> LiveMessages { get; } = new();
    public ObservableCollection<SearchHit> SearchResults { get; } = new();
    public BulkObservableCollection<SessionIndexEntry> ArchiveSessions { get; } = new();
    public BulkObservableCollection<SessionIndexEntry> RecentlyParsedLogs { get; } = new();

    private CancellationTokenSource? _archiveRefreshCts;
    private int _archiveRefreshVersion;
    private IReadOnlyList<SessionIndexEntry> _dashboardRecentSessions = Array.Empty<SessionIndexEntry>();
    private bool _manualArchiveRefreshInProgress;
    private bool _manualArchiveRefreshCancelled;
    private readonly ConcurrentQueue<ChatEntry> _pendingLiveMessages = new();
    private int _liveMessageDrainScheduled;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _settings = _settingsService.Load();
        BottomStatusText.Text = $"Afterline {GetCurrentBuildVersion()}" +
                                (IsCanaryBinaryV062() ? " · Canary" : string.Empty);
        _capture = new CaptureCoordinator(_journal, () => _settings);
        _processor = new BackgroundProcessor(_archiveService, () => _settings);

        _capture.MessageCaptured += Capture_MessageCaptured;
        _capture.StateChanged += Capture_StateChanged;
        _capture.SessionFinalized += Capture_SessionFinalized;
        _processor.Processed += Processor_Processed;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        _uiTimer.Tick += UiTimer_Tick;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TryEnableDarkTitleBar();
        try
        {
            StartupService.Reconcile(_settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to repair the Windows startup registration.", ex);
        }
        StreamerModePresentationService.Enabled = _settings.StreamerModeEnabled;
        SetupTrayIcon();
        ShowPage(DashboardPage, "Dashboard", "FiveM capture and session overview");
        DiagnosticLogger.Info("Startup: responsive shell displayed.");

        try
        {
            await RunStartupStageAsync("cached archive load", async () =>
            {
                Directory.CreateDirectory(_settings.ArchiveRoot);
                await RefreshArchiveAsync(
                    ArchiveRefreshScope.Dashboard,
                    ArchiveRefreshMode.CachedOnly);
            });
            await RunStartupStageAsync("capture initialization", () => _capture.StartAsync());
            _processor.Start();
            _uiTimer.Start();
            _ = RefreshRecentArchiveAfterStartupAsync();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Afterline startup failed.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Afterline", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase)) && _settings.StartMinimized)
        {
            _uiTimer.Stop();
            Hide();
            ShowInTaskbar = false;
        }
    }

    private static async Task RunStartupStageAsync(string stage, Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        DiagnosticLogger.Info($"Startup: {stage} started.");
        try
        {
            await action();
            DiagnosticLogger.Info($"Startup: {stage} completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                $"Startup: {stage} failed after {stopwatch.ElapsedMilliseconds:N0} ms.",
                ex);
            throw;
        }
    }

    private async Task RefreshRecentArchiveAfterStartupAsync()
    {
        try
        {
            await RunStartupStageAsync("recent archive scan", () => RefreshArchiveAsync(
                ArchiveRefreshScope.Dashboard,
                ArchiveRefreshMode.RecentFolders));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Startup: recent archive scan failed; the cached dashboard remains available.", ex);
        }
    }

    private void SetupTrayIcon()
    {
        try
        {
            var iconResource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/AfterlineTray.ico", UriKind.Absolute));
            if (iconResource is not null)
            {
                using var sourceIcon = new System.Drawing.Icon(iconResource.Stream);
                _trayBrandIcon = (System.Drawing.Icon)sourceIcon.Clone();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load the Afterline tray icon.", ex);
        }

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Afterline — waiting for FiveM",
            Icon = _trayBrandIcon ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Afterline", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.Invoke(OpenExportLocation);
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        _uiTimer.Interval = _journal.HasActiveSession
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromSeconds(5);
        _uiTimer.Start();
        UiTimer_Tick(this, EventArgs.Empty);
        Activate();
        ShowPendingArchiveNotification();
    }

    private void ScrollLiveTop_Click(object sender, RoutedEventArgs e)
    {
        if (LiveChatList.Items.Count > 0)
            LiveChatList.ScrollIntoView(LiveChatList.Items[0]);
    }

    private void ScrollLiveBottom_Click(object sender, RoutedEventArgs e)
    {
        int lastIndex = LiveChatList.Items.Count - 1;
        if (lastIndex >= 0)
            LiveChatList.ScrollIntoView(LiveChatList.Items[lastIndex]);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            _uiTimer.Stop();
            Hide();
            ShowInTaskbar = false;
            return;
        }

        // Keep the last dirty Editor state recoverable during an intentional exit.
        // The project writer uses an atomic temporary file, so a failed shutdown
        // save cannot damage the previous autosave or named project.
        _editorAutosaveTimerV073.Stop();
        TryAutosaveEditorProjectV073(showToast: false);
        ReleaseFiveMScreenshotHotkeyV074();

        _uiTimer.Stop();
        _archiveRefreshCts?.Cancel();
        _archiveRefreshCts?.Dispose();
        _archiveRefreshCts = null;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _trayBrandIcon?.Dispose();
        _trayBrandIcon = null;

        await _capture.DisposeAsync();
        await _processor.DisposeAsync();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            _uiTimer.Stop();
            Hide();
            ShowInTaskbar = false;
        }
        else if (IsVisible && WindowState != WindowState.Minimized)
        {
            _uiTimer.Start();
        }
    }

    private void Capture_MessageCaptured(object? sender, ChatEntry entry)
    {
        if (!_settings.ShowLiveChat)
            return;

        _pendingLiveMessages.Enqueue(entry);
        int pendingLimit = Math.Max(100, _settings.MaxLiveMessages);
        while (_pendingLiveMessages.Count > pendingLimit &&
               _pendingLiveMessages.TryDequeue(out _))
        {
        }

        ScheduleLiveMessageDrain();
    }

    private void ScheduleLiveMessageDrain()
    {
        if (Interlocked.CompareExchange(ref _liveMessageDrainScheduled, 1, 0) != 0)
            return;

        try
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(DrainPendingLiveMessages));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _liveMessageDrainScheduled, 0);
        }
    }

    private void DrainPendingLiveMessages()
    {
        try
        {
            if (!_settings.ShowLiveChat)
            {
                while (_pendingLiveMessages.TryDequeue(out _))
                {
                }
                return;
            }

            const int maximumBatch = 75;
            int added = 0;
            while (added < maximumBatch && _pendingLiveMessages.TryDequeue(out ChatEntry? entry))
            {
                if (entry is null) continue;
                LiveMessages.Add(entry);
                added++;
            }

            int visibleLimit = Math.Max(100, _settings.MaxLiveMessages);
            while (LiveMessages.Count > visibleLimit)
                LiveMessages.RemoveAt(0);

            if (added == 0) return;
            LiveCountText.Text = $"{LiveMessages.Count:N0} messages shown";
            if (_settings.AutoScrollLiveChat && LiveMessages.Count > 0)
                LiveChatList.ScrollIntoView(LiveMessages[^1]);
        }
        finally
        {
            Interlocked.Exchange(ref _liveMessageDrainScheduled, 0);
            if (!_pendingLiveMessages.IsEmpty)
                ScheduleLiveMessageDrain();
        }
    }

    private void Capture_StateChanged(object? sender, CaptureState state)
        => _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateStatusUi));

    private async void Capture_SessionFinalized(object? sender, string path)
    {
        DiagnosticLogger.Info($"Session archived: {path}");
        try
        {
            bool indexed = await _archiveService.EnsureFileIndexedAsync(
                _settings.ArchiveRoot,
                path,
                CancellationToken.None);
            if (!indexed)
                throw new IOException("The finalized chatlog could not be verified in the archive index.");
            await Dispatcher.InvokeAsync(() => RefreshArchiveAsync(
                ArchiveRefreshScope.Dashboard,
                ArchiveRefreshMode.CachedOnly)).Task.Unwrap();
            await Dispatcher.InvokeAsync(() => ShowSessionArchivedNotification(path));
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Failed to refresh archive after finalizing a session.", ex);
        }
    }

    private void Processor_Processed(object? sender, EventArgs e)
        => _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateStatusUi));

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        TimeSpan desiredInterval = _journal.HasActiveSession
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromSeconds(5);
        if (_uiTimer.Interval != desiredInterval)
            _uiTimer.Interval = desiredInterval;

        UpdateStatusUi();
        SessionCountText.Text = $"{_journal.MessageCount:N0} messages";
        if (_journal.StartedAt is DateTime started)
        {
            TimeSpan duration = DateTime.Now - started;
            SessionTimeText.Text = $"Started {started:HH:mm} · {duration:hh\\:mm\\:ss}";
        }
        else
        {
            SessionTimeText.Text = "No active session";
        }

        string pollStatus = _capture.LastSuccessfulReadAt is DateTime checkedAt
            ? $"Last successful capture check {Math.Max(0, (DateTime.Now - checkedAt).TotalSeconds):0}s ago"
            : "Waiting for a successful FiveM check";
        AutosaveText.Text = _capture.LastCaptureAt is DateTime last
            ? $"Last message saved {Math.Max(0, (DateTime.Now - last).TotalSeconds):0}s ago · {pollStatus}"
            : $"Waiting for first chat message · {pollStatus}";
    }

    private void UpdateStatusUi()
    {
        string status;
        Brush dot;
        switch (_capture.State)
        {
            case CaptureState.Capturing:
                status = "Capturing";
                dot = (Brush)FindResource("Success");
                FiveMStateText.Text = "Connected";
                FooterCaptureText.Text = "Chat capture active";
                break;
            case CaptureState.WaitingForNui:
                status = "FiveM detected — waiting for chat";
                dot = (Brush)FindResource("Warning");
                FiveMStateText.Text = "Detected";
                FooterCaptureText.Text = "Waiting for NUI";
                break;
            case CaptureState.ReconnectGrace:
                status = "FiveM disconnected — reconnect grace";
                dot = (Brush)FindResource("Warning");
                FiveMStateText.Text = "Reconnect grace";
                FooterCaptureText.Text = "Session protected on disk";
                break;
            default:
                status = "Waiting for FiveM";
                dot = (Brush)FindResource("MutedText");
                FiveMStateText.Text = "Not detected";
                FooterCaptureText.Text = "Capture idle";
                break;
        }

        TopStatusText.Text = status;
        TrayStateText.Text = status;
        StatusDot.Fill = dot;
        if (_trayIcon is not null)
        {
            string trayText = "Afterline — " + status;
            _trayIcon.Text = trayText[..Math.Min(63, trayText.Length)];
        }

        string processInfo = _processor.LastProcessedAt is DateTime processed ? $" · archive processed {processed:HH:mm:ss}" : string.Empty;
        string channel = IsCanaryBinaryV062() ? " · Canary" : string.Empty;
        BottomStatusText.Text = $"Afterline {GetCurrentBuildVersion()}{processInfo}{channel}";
    }

    private async Task RefreshArchiveAsync(
        ArchiveRefreshScope scope,
        ArchiveRefreshMode mode = ArchiveRefreshMode.CachedOnly,
        IProgress<ArchiveScanProgress>? progress = null)
    {
        int refreshVersion = Interlocked.Increment(ref _archiveRefreshVersion);
        _archiveRefreshCts?.Cancel();
        _archiveRefreshCts?.Dispose();
        _archiveRefreshCts = new CancellationTokenSource();
        CancellationToken cancellationToken = _archiveRefreshCts.Token;
        (DateTime? fromDate, DateTime? toDate) = scope == ArchiveRefreshScope.Dashboard
            ? (DateTime.Today.AddDays(-(DashboardArchiveDays - 1)), DateTime.Today)
            : GetArchiveFilterRangeV071();
        int maxEntries = scope == ArchiveRefreshScope.Dashboard
            ? DashboardArchiveScanLimit
            : ArchivePageEntryLimit;

        IReadOnlyList<SessionIndexEntry> entries;
        try
        {
            if (mode == ArchiveRefreshMode.CachedOnly)
            {
                entries = await Task.Run(() => FilterCachedArchiveEntriesV071(
                    _archiveService.LoadCachedIndex(),
                    _settings.ArchiveRoot,
                    fromDate,
                    toDate,
                    maxEntries), cancellationToken);
            }
            else
            {
                // File discovery, metadata inspection, and first-time line counts
                // always stay off WPF's dispatcher. Automatic refreshes inspect
                // only the year/month folders intersecting the requested range.
                ArchiveScanMode scanMode = mode == ArchiveRefreshMode.RecentFolders
                    ? ArchiveScanMode.DatedFoldersOnly
                    : ArchiveScanMode.FullRecursive;
                IReadOnlyList<SessionIndexEntry> rebuilt = await Task.Run(
                    () => _archiveService.RebuildIndexAsync(
                        _settings.ArchiveRoot,
                        cancellationToken,
                        mode == ArchiveRefreshMode.FullRecursive ? null : fromDate,
                        mode == ArchiveRefreshMode.FullRecursive ? null : toDate,
                        mode == ArchiveRefreshMode.FullRecursive ? null : maxEntries,
                        scanMode,
                        progress),
                    cancellationToken);
                entries = mode == ArchiveRefreshMode.FullRecursive
                    ? await Task.Run(() => FilterCachedArchiveEntriesV071(
                        rebuilt,
                        _settings.ArchiveRoot,
                        fromDate,
                        toDate,
                        maxEntries), cancellationToken)
                    : rebuilt;
            }
        }
        catch (OperationCanceledException) when (mode != ArchiveRefreshMode.FullRecursive)
        {
            return;
        }
        catch (Exception) when (mode == ArchiveRefreshMode.FullRecursive)
        {
            throw;
        }
        catch
        {
            entries = await Task.Run(() => FilterCachedArchiveEntriesV071(
                _archiveService.LoadCachedIndex(),
                _settings.ArchiveRoot,
                fromDate,
                toDate,
                maxEntries));
        }

        if (refreshVersion != _archiveRefreshVersion || cancellationToken.IsCancellationRequested)
            return;

        if (scope == ArchiveRefreshScope.ArchivePage)
        {
            ArchiveSessions.ReplaceAll(entries);
            UpdateArchiveFilterStatusV071(entries.Count, entries.Count >= ArchivePageEntryLimit);
        }

        IReadOnlyList<SessionIndexEntry> dashboardEntries = scope == ArchiveRefreshScope.Dashboard
            ? entries
            : await Task.Run(() => FilterCachedArchiveEntriesV071(
                _archiveService.LoadCachedIndex(),
                _settings.ArchiveRoot,
                DateTime.Today.AddDays(-(DashboardArchiveDays - 1)),
                DateTime.Today,
                DashboardArchiveScanLimit));
        _dashboardRecentSessions = dashboardEntries.Take(8).ToArray();
        RecentlyParsedLogs.ReplaceAll(dashboardEntries.Take(14));

        RecentSessionsList.Items.Clear();
        foreach (SessionIndexEntry entry in _dashboardRecentSessions)
            RecentSessionsList.Items.Add($"{entry.LastWriteUtc.ToLocalTime():dd MMM yyyy · HH:mm}     {entry.LineCount:N0} lines     {entry.FileName}");

        ArchiveRootText.Text = StreamerModePresentationService.PathForDisplay(_settings.ArchiveRoot);
    }

    private void PopulateSettingsUi()
    {
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows || StartupService.IsEnabled();
        StartMinimizedCheck.IsChecked = _settings.StartMinimized;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        AutoDetectCheck.IsChecked = _settings.AutoDetectFiveM;
        AutoCaptureCheck.IsChecked = _settings.AutoCapture;
        SettingsShowLiveCheck.IsChecked = _settings.ShowLiveChat;
        AutoScrollCheck.IsChecked = _settings.AutoScrollLiveChat;
        WindowsArchiveNotificationCheck.IsChecked = _settings.UseWindowsArchiveNotifications;
        ScreenshotCaptureEnabledCheck.IsChecked = _settings.EnableFiveMScreenshotCapture;
        ScreenshotFolderBox.Text = _settings.ScreenshotFolder;
        ScreenshotHotkeyBox.Text = _settings.ScreenshotHotkey;
        _screenshotHotkeyConfirmedV076 = true;
        ScreenshotHotkeyConfirmationPanelV076.Visibility = Visibility.Collapsed;
        ScreenshotCaptureNotificationCheckV076.IsChecked = _settings.ScreenshotCaptureNotificationEnabled;
        SelectComboItem(ScreenshotCaptureSoundBox, _settings.ScreenshotCaptureSound);
        ScreenshotCaptureSoundVolumeSlider.Value = Math.Clamp(_settings.ScreenshotCaptureSoundVolume, 0, 100);
        ScreenshotCaptureSoundVolumeText.Text = $"{Math.Round(ScreenshotCaptureSoundVolumeSlider.Value):0}%";
        StreamerModeCheck.IsChecked = _settings.StreamerModeEnabled;
        ShowLiveChatCheck.IsChecked = _settings.ShowLiveChat;
        ArchiveRootBox.Text = _settings.ArchiveRoot;
        SearchRootBox.Text = _settings.ArchiveRoot;
        ArchiveRootText.Text = _settings.ArchiveRoot;
        SelectComboItem(ReconnectBox, _settings.ReconnectGraceMinutes);
        SelectComboItem(ProcessingBox, _settings.ProcessingIntervalMinutes);
        SelectComboItem(MaxMessagesBox, _settings.MaxLiveMessages);
        LiveChatList.Visibility = _settings.ShowLiveChat ? Visibility.Visible : Visibility.Collapsed;
        ApplyStreamerModePresentationV075();
    }

    private static void SelectComboItem(System.Windows.Controls.ComboBox box, int value)
    {
        foreach (var item in box.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (item.Content?.ToString() == value.ToString())
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private static void SelectComboItem(System.Windows.Controls.ComboBox box, string value)
    {
        foreach (var item in box.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private void ScreenshotCaptureSoundVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScreenshotCaptureSoundVolumeText is not null)
            ScreenshotCaptureSoundVolumeText.Text = $"{Math.Round(e.NewValue):0}%";
    }

    private void PlayScreenshotCaptureSound_Click(object sender, RoutedEventArgs e)
    {
        string sound = (ScreenshotCaptureSoundBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Shutter";
        int volume = Math.Clamp((int)Math.Round(ScreenshotCaptureSoundVolumeSlider.Value), 0, 100);
        CaptureFeedbackSoundService.Play(sound, volume);
    }

    private static int ComboInt(System.Windows.Controls.ComboBox box, int fallback)
        => box.SelectedItem is System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int value) ? value : fallback;

    private void ShowPage(UIElement page, string title, string subtitle)
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        LivePage.Visibility = Visibility.Collapsed;
        SearchPage.Visibility = Visibility.Collapsed;
        ArchivePage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        if (_fiveMScreenshotGalleryPageV074 is not null)
            _fiveMScreenshotGalleryPageV074.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowPage(DashboardPage, "Dashboard", "FiveM capture and session overview");
    private void LiveNav_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmStreamerSensitiveViewV076("Live Chat")) return;
        ShowPage(LivePage, "Live Chat", "Optional real-time view of everything FiveM displays in chat");
    }

    private async void SearchNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(SearchPage, "Search", "Search one or multiple terms across your chatlog folders");
        await RefreshArchiveAsync(
            ArchiveRefreshScope.Dashboard,
            ArchiveRefreshMode.CachedOnly);
    }

    private async void ArchiveNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(ArchivePage, "Archive", "Browse plain-text sessions organized by year and month");
        await RefreshArchiveAsync(
            ArchiveRefreshScope.ArchivePage,
            ArchiveRefreshMode.CachedOnly);
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, "Settings", "Capture, startup, processing and chatlog storage preferences");

    private async void FinishSession_Click(object sender, RoutedEventArgs e)
    {
        await _capture.FinishSessionAsync();
        await RefreshArchiveAsync(
            ArchiveRefreshScope.Dashboard,
            ArchiveRefreshMode.CachedOnly);
    }

    private async void RefreshArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_manualArchiveRefreshInProgress)
        {
            _manualArchiveRefreshCancelled = true;
            _archiveRefreshCts?.Cancel();
            return;
        }

        _manualArchiveRefreshInProgress = true;
        _manualArchiveRefreshCancelled = false;
        bool failed = false;
        SetManualArchiveRefreshUi(true, "Scanning… 0 files");
        var progress = new Progress<ArchiveScanProgress>(UpdateManualArchiveProgress);
        var stopwatch = Stopwatch.StartNew();
        DiagnosticLogger.Info("Archive scan: user-requested full rebuild started.");
        try
        {
            await RefreshArchiveAsync(
                ArchivePage.Visibility == Visibility.Visible
                    ? ArchiveRefreshScope.ArchivePage
                    : ArchiveRefreshScope.Dashboard,
                ArchiveRefreshMode.FullRecursive,
                progress);
            DiagnosticLogger.Info(
                $"Archive scan: user-requested full rebuild completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
        }
        catch (OperationCanceledException)
        {
            _manualArchiveRefreshCancelled = true;
            DiagnosticLogger.Info(
                $"Archive scan: user-requested full rebuild cancelled after {stopwatch.ElapsedMilliseconds:N0} ms.");
        }
        catch (Exception ex)
        {
            failed = true;
            DiagnosticLogger.Error(
                $"Archive scan: user-requested full rebuild failed after {stopwatch.ElapsedMilliseconds:N0} ms.",
                ex);
        }
        finally
        {
            string status = failed
                ? "Archive scan failed. See Error Logs."
                : _manualArchiveRefreshCancelled
                    ? "Archive scan cancelled."
                    : "Archive index refreshed.";
            SetManualArchiveRefreshUi(false, status);
            _manualArchiveRefreshInProgress = false;
        }
    }

    private void UpdateManualArchiveProgress(ArchiveScanProgress progress)
    {
        string text = progress.Phase == "Updating archive index"
            ? $"Indexing… {progress.IndexedFiles:N0} files"
            : $"Scanning… {progress.DiscoveredFiles:N0} files";
        SetManualArchiveRefreshUi(true, text);
    }

    private void SetManualArchiveRefreshUi(bool inProgress, string status)
    {
        DashboardRefreshArchiveButton.Content = inProgress ? "Cancel scan" : "Refresh archive";
        ArchiveRefreshButton.Content = inProgress ? "Cancel scan" : "Refresh";
        if (_archiveFilterStatusV071 is not null)
        {
            _archiveFilterStatusV071.Foreground = (Brush)FindResource("MutedText");
            _archiveFilterStatusV071.Text = status;
        }
    }

    private async void RefreshSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchSummaryText.Text = "Refreshing parsed logs…";
        await _processor.ProcessNowAsync();
        await RefreshArchiveAsync(
            ArchiveRefreshScope.Dashboard,
            ArchiveRefreshMode.CachedOnly);
        SearchSummaryText.Text = "Parsed log list refreshed.";
    }

    private void ClearLive_Click(object sender, RoutedEventArgs e)
    {
        LiveMessages.Clear();
        LiveCountText.Text = "0 messages shown";
    }

    private void ShowLiveChatCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        _settings.ShowLiveChat = ShowLiveChatCheck.IsChecked == true;
        SettingsShowLiveCheck.IsChecked = _settings.ShowLiveChat;
        LiveChatList.Visibility = _settings.ShowLiveChat ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ExtendedSearchCheck_Changed(object sender, RoutedEventArgs e)
    {
        ExtendedSearchPanel.Visibility = ExtendedSearchCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await RunSearchAsync();

    private async Task RunSearchAsync()
    {
        SearchSummaryText.Text = "Searching…";
        SearchResults.Clear();
        try
        {
            SearchMode mode = SearchModeBox.SelectedIndex switch
            {
                1 => SearchMode.Exact,
                2 => SearchMode.WholeWord,
                3 => SearchMode.Regex,
                _ => SearchMode.Contains
            };

            var extraTerms = new List<string>();
            if (ExtendedSearchCheck.IsChecked == true)
            {
                if (!string.IsNullOrWhiteSpace(SearchTerm2Box.Text)) extraTerms.Add(SearchTerm2Box.Text);
                if (!string.IsNullOrWhiteSpace(SearchTerm3Box.Text)) extraTerms.Add(SearchTerm3Box.Text);
            }

            var criteria = new SearchCriteria
            {
                PrimaryTerm = SearchQueryBox.Text,
                AdditionalTerms = extraTerms,
                Mode = mode,
                CaseSensitive = CaseSensitiveCheck.IsChecked == true,
                ContextLines = ComboInt(ContextLinesBox, 3)
            };

            IReadOnlyList<SearchHit> hits = await _searchService.SearchAsync(SearchRootBox.Text, criteria, CancellationToken.None);
            foreach (var hit in hits) SearchResults.Add(hit);

            string filterText = extraTerms.Count > 0 ? $" · AND {extraTerms.Count} additional term{(extraTerms.Count == 1 ? string.Empty : "s")}" : string.Empty;
            SearchSummaryText.Text = $"{hits.Count:N0} matches{filterText}";
        }
        catch (Exception ex)
        {
            SearchSummaryText.Text = "Search failed: " + ex.Message;
        }
    }

    private async void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) await RunSearchAsync();
    }

    private void BrowseSearchRoot_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = SearchRootBox.Text, Description = "Choose a folder to search recursively" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SearchRootBox.Text = dialog.SelectedPath;
            ApplyStreamerModePresentationV075();
        }
    }

    private void BrowseArchiveRoot_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = ArchiveRootBox.Text, Description = "Choose where Afterline stores completed chatlogs" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ArchiveRootBox.Text = dialog.SelectedPath;
            ApplyStreamerModePresentationV075();
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string oldRoot = _settings.ArchiveRoot;
            string oldSearchRoot = SearchRootBox.Text;
            string screenshotHotkey = string.IsNullOrWhiteSpace(ScreenshotHotkeyBox.Text)
                ? "Ctrl+Shift+F12"
                : ScreenshotHotkeyBox.Text.Trim();
            if (!_screenshotHotkeyConfirmedV076)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Confirm the recognized capture shortcut before saving settings.",
                    "Confirm capture hotkey",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            if (!TryParseFiveMScreenshotHotkeyV074(screenshotHotkey, out _, out _))
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Use a hotkey such as Ctrl+Shift+F12, Ctrl+Alt+S, or F10.",
                    "Invalid screenshot hotkey",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
            _settings.StartMinimized = StartMinimizedCheck.IsChecked == true;
            _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
            _settings.AutoDetectFiveM = AutoDetectCheck.IsChecked == true;
            _settings.AutoCapture = AutoCaptureCheck.IsChecked == true;
            _settings.ShowLiveChat = SettingsShowLiveCheck.IsChecked == true;
            _settings.AutoScrollLiveChat = AutoScrollCheck.IsChecked == true;
            _settings.UseWindowsArchiveNotifications = WindowsArchiveNotificationCheck.IsChecked == true;
            _settings.EnableFiveMScreenshotCapture = ScreenshotCaptureEnabledCheck.IsChecked == true;
            _settings.ScreenshotFolder = string.IsNullOrWhiteSpace(ScreenshotFolderBox.Text)
                ? _settings.ScreenshotFolder
                : ScreenshotFolderBox.Text.Trim();
            _settings.ScreenshotHotkey = string.IsNullOrWhiteSpace(ScreenshotHotkeyBox.Text)
                ? "Ctrl+Shift+F12"
                : screenshotHotkey;
            _settings.ScreenshotCaptureNotificationEnabled = ScreenshotCaptureNotificationCheckV076.IsChecked == true;
            _settings.ScreenshotCaptureSound = (ScreenshotCaptureSoundBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Shutter";
            _settings.ScreenshotCaptureSoundVolume = Math.Clamp((int)Math.Round(ScreenshotCaptureSoundVolumeSlider.Value), 0, 100);
            _settings.StreamerModeEnabled = StreamerModeCheck.IsChecked == true;
            _settings.ReconnectGraceMinutes = ComboInt(ReconnectBox, 5);
            _settings.ProcessingIntervalMinutes = ComboInt(ProcessingBox, 1);
            _settings.MaxLiveMessages = ComboInt(MaxMessagesBox, 2000);
            _settings.ArchiveRoot = string.IsNullOrWhiteSpace(ArchiveRootBox.Text) ? oldRoot : ArchiveRootBox.Text.Trim();

            Directory.CreateDirectory(_settings.ArchiveRoot);
            StartupService.SetEnabled(_settings.StartWithWindows);
            _settingsService.Save(_settings);
            ApplyFiveMScreenshotCaptureSettingsV074();

            if (string.IsNullOrWhiteSpace(oldSearchRoot) || string.Equals(oldSearchRoot, oldRoot, StringComparison.OrdinalIgnoreCase))
                SearchRootBox.Text = _settings.ArchiveRoot;

            ApplyStreamerModePresentationV075();
            ShowLiveChatCheck.IsChecked = _settings.ShowLiveChat;
            LiveChatList.Visibility = _settings.ShowLiveChat ? Visibility.Visible : Visibility.Collapsed;
            await RefreshArchiveAsync(
                ArchiveRefreshScope.Dashboard,
                ArchiveRefreshMode.CachedOnly);

            SettingsSavedText.Text = "Saved";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => { SettingsSavedText.Text = string.Empty; timer.Stop(); };
            timer.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to save settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenArchiveFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_settings.ArchiveRoot);
        OpenPath(_settings.ArchiveRoot);
    }

    private void ArchiveSessionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ArchiveSessionsList.SelectedItem is SessionIndexEntry entry) OpenPath(entry.FilePath);
    }

    private void RecentlyParsedLogsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentlyParsedLogsList.SelectedItem is SessionIndexEntry entry) OpenPath(entry.FilePath);
    }

    private void SearchResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SearchResultsList.SelectedItem is SearchHit hit) OpenPath(hit.FilePath);
    }

    private void RecentSessionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        int index = RecentSessionsList.SelectedIndex;
        if (index >= 0 && index < _dashboardRecentSessions.Count)
            OpenPath(_dashboardRecentSessions[index].FilePath);
    }

    private static void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { DiagnosticLogger.Error($"Unable to open {path}", ex); }
    }

    private void TryEnableDarkTitleBar()
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int enabled = 1;
            DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int));
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
