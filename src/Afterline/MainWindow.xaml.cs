using System.Collections.ObjectModel;
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
    private readonly SettingsService _settingsService = new();
    private readonly SessionJournal _journal = new();
    private readonly ArchiveService _archiveService = new();
    private readonly SearchService _searchService = new();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private AppSettings _settings = null!;
    private CaptureCoordinator _capture = null!;
    private BackgroundProcessor _processor = null!;
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;

    public ObservableCollection<ChatEntry> LiveMessages { get; } = new();
    public ObservableCollection<SearchHit> SearchResults { get; } = new();
    public ObservableCollection<SessionIndexEntry> ArchiveSessions { get; } = new();
    public ObservableCollection<SessionIndexEntry> RecentlyParsedLogs { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _settings = _settingsService.Load();
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
        PopulateSettingsUi();
        SetupTrayIcon();
        ShowPage(DashboardPage, "Dashboard", "FiveM capture and session overview");

        try
        {
            Directory.CreateDirectory(_settings.ArchiveRoot);
            await _capture.StartAsync();
            _processor.Start();
            await RefreshArchiveAsync();
            _uiTimer.Start();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Afterline startup failed.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Afterline", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase)) && _settings.StartMinimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Afterline — waiting for FiveM",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Afterline", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
            Hide();
            ShowInTaskbar = false;
            return;
        }

        _uiTimer.Stop();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        await _capture.DisposeAsync();
        await _processor.DisposeAsync();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void Capture_MessageCaptured(object? sender, ChatEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_settings.ShowLiveChat) return;
            LiveMessages.Add(entry.WithColorization(_settings.ColorizeLiveChat));
            while (LiveMessages.Count > Math.Max(100, _settings.MaxLiveMessages)) LiveMessages.RemoveAt(0);
            LiveCountText.Text = $"{LiveMessages.Count:N0} messages shown";
            if (_settings.AutoScrollLiveChat && LiveMessages.Count > 0)
                LiveChatList.ScrollIntoView(LiveMessages[^1]);
        });
    }

    private void Capture_StateChanged(object? sender, CaptureState state) => Dispatcher.Invoke(UpdateStatusUi);

    private async void Capture_SessionFinalized(object? sender, string path)
    {
        DiagnosticLogger.Info($"Session archived: {path}");
        try
        {
            await _processor.ProcessNowAsync();
            await Dispatcher.InvokeAsync(() => RefreshArchiveAsync()).Task.Unwrap();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Failed to refresh archive after finalizing a session.", ex);
        }
    }

    private void Processor_Processed(object? sender, EventArgs e) => Dispatcher.Invoke(UpdateStatusUi);

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
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

        AutosaveText.Text = _capture.LastCaptureAt is DateTime last
            ? $"Last write {(DateTime.Now - last).TotalSeconds:0}s ago"
            : "Waiting for first chat message";
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
        BottomStatusText.Text = $"Afterline 0.2.1{processInfo}";
    }

    private async Task RefreshArchiveAsync()
    {
        IReadOnlyList<SessionIndexEntry> entries;
        try
        {
            entries = await _archiveService.RebuildIndexAsync(_settings.ArchiveRoot, CancellationToken.None);
        }
        catch
        {
            entries = _archiveService.LoadCachedIndex();
        }

        ArchiveSessions.Clear();
        foreach (var entry in entries) ArchiveSessions.Add(entry);

        RecentlyParsedLogs.Clear();
        foreach (var entry in entries.Take(14)) RecentlyParsedLogs.Add(entry);

        RecentSessionsList.Items.Clear();
        foreach (var entry in entries.Take(8))
            RecentSessionsList.Items.Add($"{entry.LastWriteUtc.ToLocalTime():dd MMM yyyy · HH:mm}     {entry.LineCount:N0} lines     {entry.FileName}");

        ArchiveRootText.Text = _settings.ArchiveRoot;
    }

    private void PopulateSettingsUi()
    {
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows || StartupService.IsEnabled();
        StartMinimizedCheck.IsChecked = _settings.StartMinimized;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        AutoDetectCheck.IsChecked = _settings.AutoDetectFiveM;
        AutoCaptureCheck.IsChecked = _settings.AutoCapture;
        SettingsShowLiveCheck.IsChecked = _settings.ShowLiveChat;
        SettingsColorizeLiveCheck.IsChecked = _settings.ColorizeLiveChat;
        AutoScrollCheck.IsChecked = _settings.AutoScrollLiveChat;
        ShowLiveChatCheck.IsChecked = _settings.ShowLiveChat;
        ColorizeLiveChatCheck.IsChecked = _settings.ColorizeLiveChat;
        ArchiveRootBox.Text = _settings.ArchiveRoot;
        SearchRootBox.Text = _settings.ArchiveRoot;
        ArchiveRootText.Text = _settings.ArchiveRoot;
        SelectComboItem(ReconnectBox, _settings.ReconnectGraceMinutes);
        SelectComboItem(ProcessingBox, _settings.ProcessingIntervalMinutes);
        SelectComboItem(MaxMessagesBox, _settings.MaxLiveMessages);
        LiveChatList.Visibility = _settings.ShowLiveChat ? Visibility.Visible : Visibility.Collapsed;
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

    private static int ComboInt(System.Windows.Controls.ComboBox box, int fallback)
        => box.SelectedItem is System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int value) ? value : fallback;

    private void ShowPage(UIElement page, string title, string subtitle)
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        LivePage.Visibility = Visibility.Collapsed;
        SearchPage.Visibility = Visibility.Collapsed;
        ArchivePage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowPage(DashboardPage, "Dashboard", "FiveM capture and session overview");
    private void LiveNav_Click(object sender, RoutedEventArgs e) => ShowPage(LivePage, "Live Chat", "Optional real-time view of everything FiveM displays in chat");

    private async void SearchNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(SearchPage, "Search", "Search one or multiple terms across your chatlog folders");
        await RefreshArchiveAsync();
    }

    private async void ArchiveNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(ArchivePage, "Archive", "Browse plain-text sessions organized by year and month");
        await RefreshArchiveAsync();
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, "Settings", "Capture, startup, processing and chatlog storage preferences");

    private async void FinishSession_Click(object sender, RoutedEventArgs e)
    {
        await _capture.FinishSessionAsync();
        await RefreshArchiveAsync();
    }

    private async void RefreshArchive_Click(object sender, RoutedEventArgs e) => await RefreshArchiveAsync();

    private async void RefreshSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchSummaryText.Text = "Refreshing parsed logs…";
        await _processor.ProcessNowAsync();
        await RefreshArchiveAsync();
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

    private void ColorizeLiveChatCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        _settings.ColorizeLiveChat = ColorizeLiveChatCheck.IsChecked == true;
        SettingsColorizeLiveCheck.IsChecked = _settings.ColorizeLiveChat;

        if (LiveMessages.Count == 0) return;
        ChatEntry[] recolored = LiveMessages.Select(x => x.WithColorization(_settings.ColorizeLiveChat)).ToArray();
        LiveMessages.Clear();
        foreach (var entry in recolored) LiveMessages.Add(entry);
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
        if (dialog.ShowDialog() == Forms.DialogResult.OK) SearchRootBox.Text = dialog.SelectedPath;
    }

    private void BrowseArchiveRoot_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = ArchiveRootBox.Text, Description = "Choose where Afterline stores completed chatlogs" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) ArchiveRootBox.Text = dialog.SelectedPath;
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string oldRoot = _settings.ArchiveRoot;
            string oldSearchRoot = SearchRootBox.Text;

            _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
            _settings.StartMinimized = StartMinimizedCheck.IsChecked == true;
            _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
            _settings.AutoDetectFiveM = AutoDetectCheck.IsChecked == true;
            _settings.AutoCapture = AutoCaptureCheck.IsChecked == true;
            _settings.ShowLiveChat = SettingsShowLiveCheck.IsChecked == true;
            _settings.ColorizeLiveChat = SettingsColorizeLiveCheck.IsChecked == true;
            _settings.AutoScrollLiveChat = AutoScrollCheck.IsChecked == true;
            _settings.ReconnectGraceMinutes = ComboInt(ReconnectBox, 5);
            _settings.ProcessingIntervalMinutes = ComboInt(ProcessingBox, 1);
            _settings.MaxLiveMessages = ComboInt(MaxMessagesBox, 2000);
            _settings.ArchiveRoot = string.IsNullOrWhiteSpace(ArchiveRootBox.Text) ? oldRoot : ArchiveRootBox.Text.Trim();

            Directory.CreateDirectory(_settings.ArchiveRoot);
            StartupService.SetEnabled(_settings.StartWithWindows);
            _settingsService.Save(_settings);

            if (string.IsNullOrWhiteSpace(oldSearchRoot) || string.Equals(oldSearchRoot, oldRoot, StringComparison.OrdinalIgnoreCase))
                SearchRootBox.Text = _settings.ArchiveRoot;

            ArchiveRootText.Text = _settings.ArchiveRoot;
            ShowLiveChatCheck.IsChecked = _settings.ShowLiveChat;
            ColorizeLiveChatCheck.IsChecked = _settings.ColorizeLiveChat;
            LiveChatList.Visibility = _settings.ShowLiveChat ? Visibility.Visible : Visibility.Collapsed;
            await RefreshArchiveAsync();

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
        if (index >= 0 && index < ArchiveSessions.Count) OpenPath(ArchiveSessions[index].FilePath);
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
