using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed class QuickLogItem
    {
        public required string FilePath { get; init; }
        public string FileName => Path.GetFileName(FilePath);
        public string Detail
        {
            get
            {
                try
                {
                    DateTime written = File.GetLastWriteTime(FilePath);
                    return $"{written:dd MMM yyyy · HH:mm}  ·  {StreamerModePresentationService.PathForDisplay(Path.GetDirectoryName(FilePath))}";
                }
                catch
                {
                    return StreamerModePresentationService.PathForDisplay(Path.GetDirectoryName(FilePath));
                }
            }
        }
    }

    private Button? _archivePinButtonV050;
    private ListBox? _pinnedLogsListV050;
    private ListBox? _recentOpenedLogsListV050;
    private readonly ObservableCollection<QuickLogItem> _pinnedQuickLogsV050 = new();
    private readonly ObservableCollection<QuickLogItem> _recentQuickLogsV050 = new();
    private DispatcherTimer? _recentLogRecordTimerV050;

    private void ConfigureArchiveQuickAccessV050()
    {
        if (ArchivePage.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "AfterlineQuickLogs"))) return;

        UIElement[] existing = ArchivePage.Children.Cast<UIElement>().ToArray();
        ArchivePage.RowDefinitions.Clear();
        ArchivePage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ArchivePage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        ArchivePage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        foreach (UIElement child in existing) Grid.SetRow(child, 2);

        var card = new Border
        {
            Tag = "AfterlineQuickLogs",
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(14)
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "Quick access", FontSize = 16, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock
        {
            Text = "Pinned chatlogs and files you've recently opened in Log Reader.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        });
        heading.Children.Add(title);

        var headingActions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        _archivePinButtonV050 = new Button
        {
            Content = "Pin selected",
            Padding = new Thickness(9, 5, 9, 5),
            Margin = new Thickness(0, 0, 7, 0),
            IsEnabled = ArchiveSessionsList.SelectedItem is SessionIndexEntry
        };
        _archivePinButtonV050.Click += ArchivePinSelectedV050_Click;
        headingActions.Children.Add(_archivePinButtonV050);
        var pinCurrent = new Button
        {
            Content = "Pin current log",
            Padding = new Thickness(9, 5, 9, 5),
            ToolTip = "Pin the currently active chatlog for quick access."
        };
        pinCurrent.Click += PinCurrentLogV050_Click;
        headingActions.Children.Add(pinCurrent);
        Grid.SetColumn(headingActions, 1);
        heading.Children.Add(headingActions);
        root.Children.Add(heading);

        var lists = new Grid();
        lists.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lists.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        lists.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(lists, 2);

        _pinnedLogsListV050 = BuildQuickLogListV050(_pinnedQuickLogsV050, "Pinned", "No pinned chatlogs yet.");
        _pinnedLogsListV050.MouseDoubleClick += QuickLogListV050_MouseDoubleClick;
        lists.Children.Add(WrapQuickLogListV050("PINNED", _pinnedLogsListV050, UnpinSelectedQuickLogV050_Click));

        _recentOpenedLogsListV050 = BuildQuickLogListV050(_recentQuickLogsV050, "Recently opened", "Open a chatlog in Log Reader and it will appear here.");
        _recentOpenedLogsListV050.MouseDoubleClick += QuickLogListV050_MouseDoubleClick;
        FrameworkElement recentPanel = WrapQuickLogListV050("RECENTLY OPENED", _recentOpenedLogsListV050, PinSelectedRecentLogV050_Click, "Pin");
        Grid.SetColumn(recentPanel, 2);
        lists.Children.Add(recentPanel);

        root.Children.Add(lists);
        card.Child = root;
        ArchivePage.Children.Add(card);

        ArchiveSessionsList.SelectionChanged += (_, _) =>
        {
            if (_archivePinButtonV050 is not null)
                _archivePinButtonV050.IsEnabled = ArchiveSessionsList.SelectedItem is SessionIndexEntry;
        };

        ConfigureRecentLogTrackingV050();
        RefreshQuickLogCollectionsV050();
    }

    private ListBox BuildQuickLogListV050(IEnumerable<QuickLogItem> source, string name, string emptyHint)
    {
        var list = new ListBox
        {
            ItemsSource = source,
            MaxHeight = 112,
            MinHeight = 58,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ToolTip = $"{name}. Double-click a file to open it in Log Reader. {emptyHint}"
        };

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 3, 3, 5));
        var file = new FrameworkElementFactory(typeof(TextBlock));
        file.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(QuickLogItem.FileName)));
        file.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        file.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        stack.AppendChild(file);
        var detail = new FrameworkElementFactory(typeof(TextBlock));
        detail.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(QuickLogItem.Detail)));
        detail.SetValue(TextBlock.ForegroundProperty, FindResource("MutedText"));
        detail.SetValue(TextBlock.FontSizeProperty, 10.5d);
        detail.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        detail.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
        stack.AppendChild(detail);
        list.ItemTemplate = new DataTemplate(typeof(QuickLogItem)) { VisualTree = stack };
        return list;
    }

    private FrameworkElement WrapQuickLogListV050(string title, ListBox list, RoutedEventHandler action, string actionLabel = "Unpin")
    {
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var button = new Button
        {
            Content = actionLabel,
            Padding = new Thickness(8, 4, 8, 4),
            MinHeight = 28
        };
        button.Click += action;
        Grid.SetColumn(button, 1);
        heading.Children.Add(button);
        panel.Children.Add(heading);

        Grid.SetRow(list, 2);
        panel.Children.Add(list);
        return panel;
    }

    private void ConfigureRecentLogTrackingV050()
    {
        _recentLogRecordTimerV050 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _recentLogRecordTimerV050.Tick += (_, _) =>
        {
            _recentLogRecordTimerV050.Stop();
            if (!string.IsNullOrWhiteSpace(_logReaderCurrentPath) && File.Exists(_logReaderCurrentPath))
                RememberLogOpenedV050(_logReaderCurrentPath);
        };

        _logReaderLines.CollectionChanged += (_, _) =>
        {
            _recentLogRecordTimerV050.Stop();
            _recentLogRecordTimerV050.Start();
        };
    }

    private void RememberLogOpenedV050(string path)
    {
        string fullPath = Path.GetFullPath(path);
        _settings.RecentLogPaths ??= new List<string>();
        _settings.RecentLogPaths.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
        _settings.RecentLogPaths.Insert(0, fullPath);
        if (_settings.RecentLogPaths.Count > 20)
            _settings.RecentLogPaths.RemoveRange(20, _settings.RecentLogPaths.Count - 20);
        SaveQolStateV050();
        RefreshQuickLogCollectionsV050();
    }

    private void ArchivePinSelectedV050_Click(object sender, RoutedEventArgs e)
    {
        if (ArchiveSessionsList.SelectedItem is SessionIndexEntry entry) PinLogV050(entry.FilePath);
    }

    private void PinCurrentLogV050_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_journal.ActiveFile) && File.Exists(_journal.ActiveFile))
            PinLogV050(_journal.ActiveFile);
    }

    private void PinSelectedRecentLogV050_Click(object sender, RoutedEventArgs e)
    {
        if (_recentOpenedLogsListV050?.SelectedItem is QuickLogItem item) PinLogV050(item.FilePath);
    }

    private void UnpinSelectedQuickLogV050_Click(object sender, RoutedEventArgs e)
    {
        if (_pinnedLogsListV050?.SelectedItem is not QuickLogItem item) return;
        _settings.PinnedLogPaths ??= new List<string>();
        _settings.PinnedLogPaths.RemoveAll(p => string.Equals(p, item.FilePath, StringComparison.OrdinalIgnoreCase));
        SaveQolStateV050();
        RefreshQuickLogCollectionsV050();
    }

    private void PinLogV050(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) return;
        _settings.PinnedLogPaths ??= new List<string>();
        _settings.PinnedLogPaths.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
        _settings.PinnedLogPaths.Insert(0, fullPath);
        if (_settings.PinnedLogPaths.Count > 40)
            _settings.PinnedLogPaths.RemoveRange(40, _settings.PinnedLogPaths.Count - 40);
        SaveQolStateV050();
        RefreshQuickLogCollectionsV050();
    }

    private async void QuickLogListV050_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is QuickLogItem item)
            await OpenLogInReaderAsync(item.FilePath, null);
    }

    private void RefreshQuickLogCollectionsV050()
    {
        _settings.PinnedLogPaths ??= new List<string>();
        _settings.RecentLogPaths ??= new List<string>();

        _pinnedQuickLogsV050.Clear();
        foreach (string path in _settings.PinnedLogPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            _pinnedQuickLogsV050.Add(new QuickLogItem { FilePath = path });

        _recentQuickLogsV050.Clear();
        foreach (string path in _settings.RecentLogPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(12))
            _recentQuickLogsV050.Add(new QuickLogItem { FilePath = path });
    }

    private void SaveQolStateV050()
    {
        try { _settingsService.Save(_settings); }
        catch (Exception ex) { DiagnosticLogger.Error("Unable to save quick-access state.", ex); }
    }
}
