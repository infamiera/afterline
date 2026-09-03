using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private static readonly Regex ArchiveServerName = new(
        @"^Chatlog \[(?<server>.+)\] \[\d{2}-[A-Za-z]+-\d{4}\](?: \(\d+\))?\.txt$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly SearchHistoryService _searchHistoryService = new();
    private bool _qolSearchRecoveryInitialized;
    private TextBox? _searchServerFilterBox;
    private DatePicker? _searchFromDatePicker;
    private DatePicker? _searchToDatePicker;
    private ComboBox? _searchHistoryBox;
    private TextBlock? _archiveStatsText;
    private TextBlock? _recoveryStatusText;
    private Button? _replayRecoveryButton;
    private DateTime _nextRecoveryCenterRefreshUtc = DateTime.MinValue;

    private void EnsureQolSearchRecoveryStats()
    {
        if (_qolSearchRecoveryInitialized) return;
        _qolSearchRecoveryInitialized = true;

        ConfigureSearchFiltersAndHistory();
        ConfigureExactLineSearchOpening();
        ConfigureRecoveryCenter();
        ConfigureArchiveStatistics();
        _uiTimer.Tick += QolRecoveryStatus_Tick;

        UpdateRecoveryCenterStatus();
        UpdateArchiveStatistics();
    }

    private void ConfigureSearchFiltersAndHistory()
    {
        if (ExtendedSearchPanel.Parent is not Grid optionsGrid) return;

        // Explicitly align the stock search checkboxes with the controls around them.
        CaseSensitiveCheck.VerticalAlignment = VerticalAlignment.Center;
        CaseSensitiveCheck.VerticalContentAlignment = VerticalAlignment.Center;
        CaseSensitiveCheck.Margin = new Thickness(14, 0, 0, 0);
        ExtendedSearchCheck.VerticalAlignment = VerticalAlignment.Center;
        ExtendedSearchCheck.VerticalContentAlignment = VerticalAlignment.Center;
        ExtendedSearchCheck.Margin = new Thickness(16, 0, 0, 0);

        optionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = optionsGrid.RowDefinitions.Count - 1;

        var filterBorder = new Border
        {
            Background = (Brush)FindResource("AfterlineInset"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(filterBorder, row);

        var filters = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            ItemHeight = 36
        };

        filters.Children.Add(CreateSearchFilterLabel("Server"));
        _searchServerFilterBox = new TextBox
        {
            Width = 185,
            Height = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            ToolTip = "Only search chatlogs whose archived server name contains this text."
        };
        _searchServerFilterBox.TextChanged += (_, _) => _searchService.ServerFilter = _searchServerFilterBox.Text;
        filters.Children.Add(_searchServerFilterBox);

        filters.Children.Add(CreateSearchFilterLabel("From"));
        _searchFromDatePicker = CreateDarkSearchDatePicker(new Thickness(0, 0, 12, 0));
        _searchFromDatePicker.SelectedDateChanged += (_, _) => _searchService.FromDateFilter = _searchFromDatePicker.SelectedDate;
        filters.Children.Add(_searchFromDatePicker);

        filters.Children.Add(CreateSearchFilterLabel("To"));
        _searchToDatePicker = CreateDarkSearchDatePicker(new Thickness(0, 0, 16, 0));
        _searchToDatePicker.SelectedDateChanged += (_, _) => _searchService.ToDateFilter = _searchToDatePicker.SelectedDate;
        filters.Children.Add(_searchToDatePicker);

        filters.Children.Add(CreateSearchFilterLabel("History"));
        _searchHistoryBox = new ComboBox
        {
            Width = 190,
            Height = 34,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Choose one of your recent searches."
        };
        _searchHistoryBox.SelectionChanged += SearchHistoryBox_SelectionChanged;
        filters.Children.Add(_searchHistoryBox);

        var clearHistory = new Button
        {
            Content = "Clear history",
            Height = 34,
            Padding = new Thickness(9, 5, 9, 5),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Remove all saved search history."
        };
        clearHistory.Click += (_, _) =>
        {
            _searchHistoryService.Clear();
            RefreshSearchHistoryBox(Array.Empty<string>());
        };
        filters.Children.Add(clearHistory);

        filterBorder.Child = filters;
        optionsGrid.Children.Add(filterBorder);

        RefreshSearchHistoryBox(_searchHistoryService.Load());

        Button? searchButton = FindButtonByContent(SearchPage, "Search");
        if (searchButton is not null) searchButton.Click += (_, _) => RememberCurrentSearch();
        SearchQueryBox.KeyDown += SearchHistory_KeyDown;
    }

    private DatePicker CreateDarkSearchDatePicker(Thickness margin)
    {
        var picker = new DatePicker
        {
            Width = 138,
            Height = 34,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("Text"),
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            SelectedDateFormat = DatePickerFormat.Short,
            Margin = margin
        };

        picker.Loaded += (_, _) => ApplyDarkDatePickerParts(picker);
        return picker;
    }

    private void ApplyDarkDatePickerParts(DatePicker picker)
    {
        picker.ApplyTemplate();

        if (picker.Template.FindName("PART_TextBox", picker) is DatePickerTextBox textBox)
        {
            textBox.Foreground = (Brush)FindResource("Text");
            textBox.Background = (Brush)FindResource("Raised");
            textBox.BorderThickness = new Thickness(0);
            textBox.Padding = new Thickness(8, 0, 4, 0);
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            textBox.CaretBrush = (Brush)FindResource("Accent");
        }

        if (picker.Template.FindName("PART_Button", picker) is Button button)
        {
            button.Width = 34;
            button.Height = 32;
            button.Padding = new Thickness(0);
            button.Margin = new Thickness(0);
            button.Background = (Brush)FindResource("Raised");
            button.Foreground = (Brush)FindResource("Text");
            button.BorderBrush = (Brush)FindResource("Border");
            button.BorderThickness = new Thickness(1, 0, 0, 0);
            button.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    private TextBlock CreateSearchFilterLabel(string text)
        => new()
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

    private void SearchHistory_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) RememberCurrentSearch();
    }

    private void RememberCurrentSearch()
    {
        string query = SearchQueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        RefreshSearchHistoryBox(_searchHistoryService.Add(query));
    }

    private void RefreshSearchHistoryBox(IReadOnlyList<string> entries)
    {
        if (_searchHistoryBox is null) return;
        string? selected = _searchHistoryBox.SelectedItem as string;
        _searchHistoryBox.Items.Clear();
        foreach (string entry in entries) _searchHistoryBox.Items.Add(entry);
        if (selected is not null && entries.Contains(selected, StringComparer.OrdinalIgnoreCase))
            _searchHistoryBox.SelectedItem = entries.First(entry => string.Equals(entry, selected, StringComparison.OrdinalIgnoreCase));
    }

    private void SearchHistoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_searchHistoryBox?.SelectedItem is string query && !string.IsNullOrWhiteSpace(query))
        {
            SearchQueryBox.Text = query;
            SearchQueryBox.CaretIndex = SearchQueryBox.Text.Length;
            SearchQueryBox.Focus();
        }
    }

    private void ConfigureExactLineSearchOpening()
    {
        SearchResultsList.PreviewMouseDoubleClick += SearchResultsList_OpenExactLine;
    }

    private async void SearchResultsList_OpenExactLine(object sender, MouseButtonEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not SearchHit hit || !File.Exists(hit.FilePath)) return;
        e.Handled = true;
        await OpenLogInReaderAsync(hit.FilePath, hit.LineNumber);
    }

    private void ConfigureRecoveryCenter()
    {
        if (SettingsPage.Content is not StackPanel settingsStack) return;

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = "Recovery Center", FontSize = 18, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock
        {
            Text = "Review Afterline's recovery state, replay the last captured session, or open the local recovery folder.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 10)
        });

        _recoveryStatusText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        content.Children.Add(_recoveryStatusText);

        var actions = new WrapPanel();
        _replayRecoveryButton = new Button
        {
            Content = "Replay last session",
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _replayRecoveryButton.Click += ReplayLastSessionFromRecovery_Click;
        var openRecovery = new Button
        {
            Content = "Open recovery folder",
            Padding = new Thickness(11, 6, 11, 6)
        };
        openRecovery.Click += (_, _) => OpenRecoveryFolder();
        actions.Children.Add(_replayRecoveryButton);
        actions.Children.Add(openRecovery);
        content.Children.Add(actions);
        card.Child = content;

        int insertAt = Math.Max(0, settingsStack.Children.Count - 1);
        settingsStack.Children.Insert(insertAt, card);
    }

    private async void ReplayLastSessionFromRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (_replayRecoveryButton is not null) _replayRecoveryButton.IsEnabled = false;
        try
        {
            var cache = new LastSessionCacheService();
            IReadOnlyList<ChatEntry> entries = await cache.ReadAsync(CancellationToken.None);
            if (entries.Count == 0)
            {
                if (_recoveryStatusText is not null) _recoveryStatusText.Text = "No persistent last-session cache is currently available.";
                return;
            }

            LiveMessages.Clear();
            foreach (ChatEntry entry in entries) LiveMessages.Add(entry);
            UpdateVisibleLiveCount();

            if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Collapsed;
            if (_logReaderPage is not null) _logReaderPage.Visibility = Visibility.Collapsed;
            ShowPage(LivePage, "Live Chat", "Recovered last captured session");
            SetLiveActionStatus($"Replayed {entries.Count:N0} cached message{(entries.Count == 1 ? string.Empty : "s")}.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to replay the last cached session from Recovery Center.", ex);
            if (_recoveryStatusText is not null) _recoveryStatusText.Text = "Unable to replay cached session: " + ex.Message;
        }
        finally
        {
            if (_replayRecoveryButton is not null) _replayRecoveryButton.IsEnabled = true;
        }
    }

    private static void OpenRecoveryFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{AppPaths.LocalDataRoot}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open Afterline recovery folder.", ex);
        }
    }

    private void QolRecoveryStatus_Tick(object? sender, EventArgs e)
    {
        if (SettingsPage.Visibility != Visibility.Visible ||
            DateTime.UtcNow < _nextRecoveryCenterRefreshUtc)
            return;

        _nextRecoveryCenterRefreshUtc = DateTime.UtcNow.AddSeconds(10);
        UpdateRecoveryCenterStatus();
    }

    private void UpdateRecoveryCenterStatus()
    {
        if (_recoveryStatusText is null) return;

        try
        {
            bool cacheAvailable = File.Exists(AppPaths.LastSessionCacheFile) && new FileInfo(AppPaths.LastSessionCacheFile).Length > 0;
            int recoveryCopies = Directory.Exists(AppPaths.RecoveryBackupsDirectory)
                ? Directory.GetFiles(AppPaths.RecoveryBackupsDirectory, "*.txt").Length
                : 0;
            int pendingStates = Directory.Exists(AppPaths.ActiveSessionsDirectory)
                ? Directory.GetFiles(AppPaths.ActiveSessionsDirectory, "*.state.json").Length
                : 0;
            string lastWrite = _capture.LastCaptureAt is DateTime last ? last.ToString("dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "No chat captured this run";
            string active = _journal.HasActiveSession && !string.IsNullOrWhiteSpace(_journal.ActiveFile)
                ? Path.GetFileName(_journal.ActiveFile)
                : "None";

            _recoveryStatusText.Text =
                $"Last-session cache: {(cacheAvailable ? "Available" : "Not available")} · Recovery copies: {recoveryCopies:N0} · Pending states: {pendingStates:N0}\n" +
                $"Active chatlog: {active} · Last successful write: {lastWrite}";
        }
        catch (Exception ex)
        {
            _recoveryStatusText.Text = "Recovery state could not be read.";
            DiagnosticLogger.Error("Unable to update Recovery Center status.", ex);
        }
    }

    private void ConfigureArchiveStatistics()
    {
        if (ArchiveRootText.Parent is StackPanel panel)
        {
            _archiveStatsText = new TextBlock
            {
                Foreground = (Brush)FindResource("MutedText"),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            int index = panel.Children.IndexOf(ArchiveRootText);
            panel.Children.Insert(Math.Min(index + 1, panel.Children.Count), _archiveStatsText);
        }

        ArchiveSessions.CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(UpdateArchiveStatistics));
    }

    private void UpdateArchiveStatistics()
    {
        if (_archiveStatsText is null) return;

        int logs = ArchiveSessions.Count;
        long lines = ArchiveSessions.Sum(entry => (long)entry.LineCount);
        long bytes = ArchiveSessions.Sum(entry => entry.SizeBytes);
        int servers = ArchiveSessions
            .Select(entry => ParseServerFromArchiveName(entry.FileName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        _archiveStatsText.Text = $"{logs:N0} chatlogs · {lines:N0} lines · {servers:N0} servers · {FormatArchiveSize(bytes)}";
    }

    private static string ParseServerFromArchiveName(string fileName)
    {
        Match match = ArchiveServerName.Match(fileName);
        return match.Success ? match.Groups["server"].Value : string.Empty;
    }

    private static string FormatArchiveSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes:N0} B";
        double kb = bytes / 1024d;
        if (kb < 1024) return $"{kb:N1} KB";
        double mb = kb / 1024d;
        if (mb < 1024) return $"{mb:N1} MB";
        return $"{mb / 1024d:N2} GB";
    }
}
