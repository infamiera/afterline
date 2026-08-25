using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private readonly ObservableCollection<LogReaderLineItem> _logReaderLines = new();
    private bool _logReaderInitialized;
    private Grid? _logReaderPage;
    private Button? _logReaderNavButton;
    private Button? _archiveOpenInAppButton;
    private ListBox? _logReaderList;
    private ICollectionView? _logReaderView;
    private TextBlock? _logReaderTitleText;
    private TextBlock? _logReaderPathText;
    private TextBlock? _logReaderStatusText;
    private CheckBox? _logReaderOocCheck;
    private CheckBox? _logReaderRpCheck;
    private CheckBox? _logReaderTimestampCheck;
    private Button? _logReaderJumpTopButton;
    private Button? _logReaderJumpBottomButton;
    private string? _logReaderCurrentPath;
    private string _logReaderCurrentServer = "Unknown Server";

    private void EnsureLogReader()
    {
        if (_logReaderInitialized) return;
        _logReaderInitialized = true;

        if (SettingsNav.Parent is not StackPanel navPanel || DashboardPage.Parent is not Grid pageHost) return;

        _logReaderNavButton = new Button
        {
            Content = "Log Reader",
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _logReaderNavButton.Click += LogReaderNav_Click;

        int notesIndex = navPanel.Children
            .OfType<Button>()
            .Select((button, index) => new { button, index })
            .FirstOrDefault(x => string.Equals(x.button.Content?.ToString(), "Notes & Bookmarks", StringComparison.Ordinal))?.index
            ?? navPanel.Children.IndexOf(SettingsNav);
        navPanel.Children.Insert(Math.Max(0, notesIndex), _logReaderNavButton);

        _logReaderPage = BuildLogReaderPage();
        Grid.SetRow(_logReaderPage, 2);
        pageHost.Children.Add(_logReaderPage);

        foreach (Button nav in new[] { DashboardNav, LiveNav, SearchNav, ArchiveNav, SettingsNav })
            nav.Click += (_, _) => { if (_logReaderPage is not null) _logReaderPage.Visibility = Visibility.Collapsed; };

        ConfigureArchiveOpenInApp();
    }

    private Grid BuildLogReaderPage()
    {
        var page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerCard = new Border { Style = (Style)FindResource("CardStyle") };
        var header = new Grid();
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var fileInfo = new StackPanel();
        _logReaderTitleText = new TextBlock
        {
            Text = "No chatlog open",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        };
        _logReaderPathText = new TextBlock
        {
            Text = "Open a chatlog from Archive, Search, or Notes & Bookmarks.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        fileInfo.Children.Add(_logReaderTitleText);
        fileInfo.Children.Add(_logReaderPathText);
        header.Children.Add(fileInfo);

        var options = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(options, 1);

        _logReaderOocCheck = new CheckBox
        {
            Content = "Show OOC chat",
            IsChecked = _settings.ShowOocChat,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        _logReaderOocCheck.Checked += LogReaderOocCheck_Changed;
        _logReaderOocCheck.Unchecked += LogReaderOocCheck_Changed;

        _logReaderRpCheck = new CheckBox
        {
            Content = "RP line colors",
            IsChecked = _settings.ColorizeRoleplayLines,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        _logReaderRpCheck.Checked += LogReaderRpCheck_Changed;
        _logReaderRpCheck.Unchecked += LogReaderRpCheck_Changed;

        _logReaderTimestampCheck = new CheckBox
        {
            Content = "Show timestamps",
            IsChecked = _settings.ShowLiveTimestamps,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0)
        };
        _logReaderTimestampCheck.Checked += LogReaderTimestampCheck_Changed;
        _logReaderTimestampCheck.Unchecked += LogReaderTimestampCheck_Changed;

        options.Children.Add(_logReaderOocCheck);
        options.Children.Add(_logReaderRpCheck);
        options.Children.Add(_logReaderTimestampCheck);
        header.Children.Add(options);

        _logReaderStatusText = new TextBlock
        {
            Text = "No log loaded.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(_logReaderStatusText, 1);
        Grid.SetColumnSpan(_logReaderStatusText, 2);
        header.Children.Add(_logReaderStatusText);

        headerCard.Child = header;
        page.Children.Add(headerCard);

        var bodyCard = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(0)
        };
        Grid.SetRow(bodyCard, 2);

        _logReaderList = new ListBox
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10),
            ItemsSource = _logReaderLines,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _logReaderList.SetValue(ScrollViewer.CanContentScrollProperty, true);
        _logReaderList.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        _logReaderList.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        _logReaderList.PreviewMouseRightButtonDown += LogReaderList_PreviewMouseRightButtonDown;
        _logReaderList.ContextMenu = BuildLogReaderContextMenu();
        _logReaderList.ItemTemplate = BuildLogReaderItemTemplate();

        _logReaderView = CollectionViewSource.GetDefaultView(_logReaderLines);
        _logReaderView.Filter = item =>
            item is not LogReaderLineItem line || _settings.ShowOocChat || !line.IsOocLine;
        _logReaderList.ItemsSource = _logReaderView;

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(_logReaderList);

        var jumpBar = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        Grid.SetRow(jumpBar, 1);
        var jumpButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _logReaderJumpTopButton = new Button
        {
            Content = "↑",
            Width = 32,
            Padding = new Thickness(6, 5, 6, 5),
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = "Jump to the top of the opened log"
        };
        _logReaderJumpTopButton.Click += (_, _) => ScrollLogReaderToBoundary(top: true);
        _logReaderJumpBottomButton = new Button
        {
            Content = "↓",
            Width = 32,
            Padding = new Thickness(6, 5, 6, 5),
            ToolTip = "Jump to the bottom of the opened log"
        };
        _logReaderJumpBottomButton.Click += (_, _) => ScrollLogReaderToBoundary(top: false);
        jumpButtons.Children.Add(_logReaderJumpTopButton);
        jumpButtons.Children.Add(_logReaderJumpBottomButton);
        jumpBar.Child = jumpButtons;
        body.Children.Add(jumpBar);

        bodyCard.Child = body;
        page.Children.Add(bodyCard);
        return page;
    }

    private void ScrollLogReaderToBoundary(bool top)
    {
        if (_logReaderList is null || _logReaderList.Items.Count == 0) return;
        object target = _logReaderList.Items[top ? 0 : _logReaderList.Items.Count - 1];
        _logReaderList.ScrollIntoView(target);
    }

    private DataTemplate BuildLogReaderItemTemplate()
    {
        var row = new FrameworkElementFactory(typeof(DockPanel));
        row.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 2, 2, 2));
        row.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);

        var number = new FrameworkElementFactory(typeof(TextBlock));
        number.SetBinding(TextBlock.TextProperty, new Binding(nameof(LogReaderLineItem.LineNumber)));
        number.SetValue(FrameworkElement.WidthProperty, 54d);
        number.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
        number.SetValue(TextBlock.ForegroundProperty, FindResource("MutedText"));
        number.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
        number.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
        number.SetValue(DockPanel.DockProperty, Dock.Left);
        row.AppendChild(number);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(LogReaderLineItem.Display)));
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(LogReaderLineItem.Foreground)));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        row.AppendChild(text);

        return new DataTemplate(typeof(LogReaderLineItem)) { VisualTree = row };
    }

    private ContextMenu BuildLogReaderContextMenu()
    {
        var menu = CreateAfterlineContextMenu();
        menu.Items.Add(CreateAfterlineContextMenuItem("Copy line", (_, _) => CopyLogReaderLine()));
        menu.Items.Add(CreateAfterlineContextMenuSeparator());
        menu.Items.Add(CreateAfterlineContextMenuItem("Copy ±5 lines", (_, _) => CopyLogReaderContext(5)));
        menu.Items.Add(CreateAfterlineContextMenuItem("Copy ±10 lines", (_, _) => CopyLogReaderContext(10)));
        menu.Items.Add(CreateAfterlineContextMenuSeparator());
        menu.Items.Add(CreateAfterlineContextMenuItem("Bookmark line", BookmarkLogReaderLine_Click));
        menu.Items.Add(CreateAfterlineContextMenuItem("Add note to line…", AddNoteToLogReaderLine_Click));
        return menu;
    }

    private void ConfigureArchiveOpenInApp()
    {
        Button? openFolder = FindButtonByContent(ArchivePage, "Open folder");
        if (openFolder?.Parent is not StackPanel actions) return;

        _archiveOpenInAppButton = new Button
        {
            Content = "Open in app",
            IsEnabled = ArchiveSessionsList.SelectedItem is SessionIndexEntry,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Open the selected chatlog in Afterline's Log Reader."
        };
        _archiveOpenInAppButton.Click += ArchiveOpenInApp_Click;
        int index = actions.Children.IndexOf(openFolder);
        actions.Children.Insert(Math.Max(0, index), _archiveOpenInAppButton);

        ArchiveSessionsList.SelectionChanged += (_, _) =>
        {
            if (_archiveOpenInAppButton is not null)
                _archiveOpenInAppButton.IsEnabled = ArchiveSessionsList.SelectedItem is SessionIndexEntry;
        };
    }

    private async void ArchiveOpenInApp_Click(object sender, RoutedEventArgs e)
    {
        if (ArchiveSessionsList.SelectedItem is SessionIndexEntry entry)
            await OpenLogInReaderAsync(entry.FilePath, null);
    }

    private void LogReaderNav_Click(object sender, RoutedEventArgs e)
    {
        if (_logReaderPage is null) return;
        if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Collapsed;
        ShowPage(_logReaderPage, "Log Reader", "Read archived chatlogs with Live Chat presentation settings");
    }

    internal async Task OpenLogInReaderAsync(string filePath, int? lineNumber)
    {
        if (_logReaderPage is null || _logReaderList is null || _logReaderView is null) return;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            System.Windows.MessageBox.Show(this, "The selected chatlog could not be found.", "Afterline", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            string[] lines = await File.ReadAllLinesAsync(filePath);
            IReadOnlyDictionary<int, ChatColorLineRecord> exactColors =
                await ChatColorSidecarService.MatchLinesAsync(
                    filePath,
                    lines,
                    CancellationToken.None);
            _logReaderLines.Clear();

            DateTime observedAt = File.GetLastWriteTime(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                bool system = IsLogReaderSystemLine(raw);
                exactColors.TryGetValue(i, out ChatColorLineRecord? exact);
                ChatEntry entry = system
                    ? ChatEntry.System(observedAt, raw)
                    : new ChatEntry(
                        observedAt,
                        raw,
                        capturedColorRuns: exact?.ColorRuns);
                _logReaderLines.Add(new LogReaderLineItem(i + 1, raw, entry));
            }

            _logReaderCurrentPath = Path.GetFullPath(filePath);
            _logReaderCurrentServer = ResolveLogReaderServerName(lines, filePath);
            if (_logReaderTitleText is not null) _logReaderTitleText.Text = Path.GetFileName(filePath);
            if (_logReaderPathText is not null)
                _logReaderPathText.Text = StreamerModePresentationService.PathForDisplay(_logReaderCurrentPath);

            _logReaderView.Refresh();
            _logReaderList.Items.Refresh();
            UpdateLogReaderStatus();

            if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Collapsed;
            ShowPage(_logReaderPage, "Log Reader", $"{_logReaderCurrentServer} · {Path.GetFileName(filePath)}");

            if (lineNumber is int requested)
            {
                LogReaderLineItem? target = _logReaderLines.FirstOrDefault(item => item.LineNumber == requested);
                if (target is not null)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_logReaderView.Contains(target))
                        {
                            _logReaderList.SelectedItem = target;
                            _logReaderList.ScrollIntoView(target);
                            if (_logReaderList.ItemContainerGenerator.ContainerFromItem(target) is ListBoxItem item)
                                item.Focus();
                        }
                        else if (_logReaderStatusText is not null)
                        {
                            _logReaderStatusText.Text = $"Line {requested:N0} is hidden by the current OOC/gameplay-status filter.";
                        }
                    }));
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open chatlog in Log Reader.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to open chatlog", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsLogReaderSystemLine(string line)
        => string.IsNullOrWhiteSpace(line) ||
           line.StartsWith("====================", StringComparison.Ordinal) ||
           line.StartsWith("[AFTERLINE ", StringComparison.OrdinalIgnoreCase);

    private static string ResolveLogReaderServerName(IReadOnlyList<string> lines, string filePath)
    {
        foreach (string line in lines.Take(10))
        {
            const string prefix = "[AFTERLINE SERVER: ";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && line.EndsWith("]", StringComparison.Ordinal))
            {
                string value = line[prefix.Length..^1].Trim();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        string fileName = Path.GetFileName(filePath);
        const string chatlogPrefix = "Chatlog [";
        int dateMarker = fileName.LastIndexOf("] [", StringComparison.Ordinal);
        if (fileName.StartsWith(chatlogPrefix, StringComparison.OrdinalIgnoreCase) && dateMarker > chatlogPrefix.Length)
            return fileName[chatlogPrefix.Length..dateMarker].Trim();

        return "Unknown Server";
    }

    private void LogReaderList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_logReaderList is null || e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(_logReaderList, source) is not ListBoxItem item) return;
        item.IsSelected = true;
        item.Focus();
    }

    private void CopyLogReaderLine()
    {
        if (_logReaderList?.SelectedItem is not LogReaderLineItem selected) return;
        try { Clipboard.SetText(selected.Display); }
        catch (Exception ex) { DiagnosticLogger.Error("Unable to copy Log Reader line.", ex); }
    }

    private void CopyLogReaderContext(int radius)
    {
        if (_logReaderList?.SelectedItem is not LogReaderLineItem selected) return;
        List<LogReaderLineItem> visible = _logReaderList.Items.OfType<LogReaderLineItem>().ToList();
        int index = visible.IndexOf(selected);
        if (index < 0) return;

        int start = Math.Max(0, index - radius);
        int end = Math.Min(visible.Count - 1, index + radius);
        string text = string.Join(Environment.NewLine, visible.Skip(start).Take(end - start + 1).Select(item => item.Display));
        try { Clipboard.SetText(text); }
        catch (Exception ex) { DiagnosticLogger.Error("Unable to copy Log Reader context.", ex); }
    }

    private async void BookmarkLogReaderLine_Click(object sender, RoutedEventArgs e)
    {
        if (_logReaderList?.SelectedItem is not LogReaderLineItem selected || string.IsNullOrWhiteSpace(_logReaderCurrentPath)) return;
        try
        {
            await _notesBookmarksService.AddForKnownLineAsync(
                SavedMarkerKind.Bookmark,
                selected.Entry,
                _logReaderCurrentServer,
                _logReaderCurrentPath,
                selected.LineNumber,
                null,
                CancellationToken.None);
            ReloadNotesBookmarks();
            if (_logReaderStatusText is not null) _logReaderStatusText.Text = $"Line {selected.LineNumber:N0} bookmarked.";
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to bookmark Log Reader line.", ex);
        }
    }

    private async void AddNoteToLogReaderLine_Click(object sender, RoutedEventArgs e)
    {
        if (_logReaderList?.SelectedItem is not LogReaderLineItem selected || string.IsNullOrWhiteSpace(_logReaderCurrentPath)) return;

        var prompt = new TextPromptWindow("Add note", "Write a note for the selected chatlog line.") { Owner = this };
        if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Value)) return;

        try
        {
            await _notesBookmarksService.AddForKnownLineAsync(
                SavedMarkerKind.Note,
                selected.Entry,
                _logReaderCurrentServer,
                _logReaderCurrentPath,
                selected.LineNumber,
                prompt.Value,
                CancellationToken.None);
            ReloadNotesBookmarks();
            if (_logReaderStatusText is not null) _logReaderStatusText.Text = $"Note saved for line {selected.LineNumber:N0}.";
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to add a note to Log Reader line.", ex);
        }
    }

    private void LogReaderOocCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_logReaderOocCheck is null) return;
        bool value = _logReaderOocCheck.IsChecked == true;
        _settings.ShowOocChat = value;
        if (_showOocChatCheck is not null && _showOocChatCheck.IsChecked != value)
            _showOocChatCheck.IsChecked = value;
        _liveChatView?.Refresh();
        _logReaderView?.Refresh();
        UpdateVisibleLiveCount();
        UpdateLogReaderStatus();
        SaveLivePresentationSettings();
    }

    private void LogReaderRpCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_logReaderRpCheck is null) return;
        bool value = _logReaderRpCheck.IsChecked == true;
        _settings.ColorizeRoleplayLines = value;
        ChatEntry.ColorizeRoleplayLines = value;
        if (_roleplayColorsCheck is not null && _roleplayColorsCheck.IsChecked != value)
            _roleplayColorsCheck.IsChecked = value;
        _liveChatView?.Refresh();
        _logReaderList?.Items.Refresh();
        SaveLivePresentationSettings();
    }

    private void LogReaderTimestampCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_logReaderTimestampCheck is null) return;
        bool value = _logReaderTimestampCheck.IsChecked == true;
        _settings.ShowLiveTimestamps = value;
        ChatEntry.ShowTimestamps = value;
        if (_showLiveTimestampsCheck is not null && _showLiveTimestampsCheck.IsChecked != value)
            _showLiveTimestampsCheck.IsChecked = value;
        _liveChatView?.Refresh();
        _logReaderList?.Items.Refresh();
        SaveLivePresentationSettings();
    }

    private void UpdateLogReaderStatus()
    {
        if (_logReaderStatusText is null || _logReaderList is null) return;
        _logReaderStatusText.Text = string.IsNullOrWhiteSpace(_logReaderCurrentPath)
            ? "No log loaded."
            : $"{_logReaderList.Items.Count:N0} of {_logReaderLines.Count:N0} lines shown · {_logReaderCurrentServer}";
    }
}
