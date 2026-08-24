using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline;

public partial class MainWindow
{
    private static readonly Regex ArchiveDateV071 = new(
        @"^Chatlog \[.+\] \[(?<date>\d{2}-[A-Za-z]+-\d{4})\](?: \(\d+\))?\.txt$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private bool _archiveFilteringInitializedV071;
    private ComboBox? _archiveFilterModeV071;
    private TextBox? _archiveLastDaysV071;
    private StackPanel? _archiveLastDaysPanelV071;
    private StackPanel? _archiveBetweenPanelV071;
    private DatePicker? _archiveFromDateV071;
    private DatePicker? _archiveToDateV071;
    private TextBlock? _archiveFilterStatusV071;

    private void EnsureArchiveFilteringV071()
    {
        if (_archiveFilteringInitializedV071 || ArchiveSessionsList.Parent is not Grid archiveGrid)
            return;

        _archiveFilteringInitializedV071 = true;
        if (archiveGrid.RowDefinitions.Count < 2) return;

        archiveGrid.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(ArchiveSessionsList, 2);

        var filterBorder = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 14, 10)
        };
        Grid.SetRow(filterBorder, 1);

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _archiveFilterModeV071 = new ComboBox
        {
            Width = 142,
            Height = 34,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _archiveFilterModeV071.Items.Add(new ComboBoxItem { Content = "Last # days", Tag = "LastDays" });
        _archiveFilterModeV071.Items.Add(new ComboBoxItem { Content = "Between dates", Tag = "Between" });
        _archiveFilterModeV071.Items.Add(new ComboBoxItem { Content = "All dates", Tag = "All" });
        _archiveFilterModeV071.SelectionChanged += ArchiveFilterModeV071_Changed;
        layout.Children.Add(_archiveFilterModeV071);

        var choices = new Grid();
        Grid.SetColumn(choices, 2);

        _archiveLastDaysPanelV071 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _archiveLastDaysPanelV071.Children.Add(new TextBlock
        {
            Text = "Days",
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        });
        _archiveLastDaysV071 = new TextBox
        {
            Width = 62,
            Height = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = Math.Clamp(_settings.ArchiveLastDays, 1, 3650).ToString(CultureInfo.InvariantCulture)
        };
        _archiveLastDaysV071.KeyDown += ArchiveFilterV071_KeyDown;
        _archiveLastDaysPanelV071.Children.Add(_archiveLastDaysV071);
        choices.Children.Add(_archiveLastDaysPanelV071);

        _archiveBetweenPanelV071 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _archiveBetweenPanelV071.Children.Add(CreateArchiveFilterLabelV071("From"));
        _archiveFromDateV071 = CreateDarkSearchDatePicker(new Thickness(0, 0, 10, 0));
        _archiveFromDateV071.SelectedDate = _settings.ArchiveFromDate?.Date ?? DateTime.Today.AddDays(-29);
        AttachDarkCalendar(_archiveFromDateV071);
        _archiveBetweenPanelV071.Children.Add(_archiveFromDateV071);
        _archiveBetweenPanelV071.Children.Add(CreateArchiveFilterLabelV071("To"));
        _archiveToDateV071 = CreateDarkSearchDatePicker(new Thickness(0));
        _archiveToDateV071.SelectedDate = _settings.ArchiveToDate?.Date ?? DateTime.Today;
        AttachDarkCalendar(_archiveToDateV071);
        _archiveBetweenPanelV071.Children.Add(_archiveToDateV071);
        choices.Children.Add(_archiveBetweenPanelV071);
        layout.Children.Add(choices);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _archiveFilterStatusV071 = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 10, 0)
        };
        actions.Children.Add(_archiveFilterStatusV071);
        var apply = new Button
        {
            Content = "Apply",
            Height = 34,
            Padding = new Thickness(12, 5, 12, 5)
        };
        apply.Click += ArchiveApplyFilterV071_Click;
        actions.Children.Add(apply);
        Grid.SetColumn(actions, 3);
        layout.Children.Add(actions);

        filterBorder.Child = layout;
        archiveGrid.Children.Add(filterBorder);

        string selectedMode = NormalizeArchiveFilterModeV071(_settings.ArchiveFilterMode);
        _archiveFilterModeV071.SelectedItem = _archiveFilterModeV071.Items
            .OfType<ComboBoxItem>()
            .First(item => string.Equals(item.Tag?.ToString(), selectedMode, StringComparison.Ordinal));
        UpdateArchiveFilterControlsV071();
        UpdateArchiveFilterStatusV071(ArchiveSessions.Count);
    }

    private TextBlock CreateArchiveFilterLabelV071(string text)
        => new()
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };

    private void ArchiveFilterModeV071_Changed(object sender, SelectionChangedEventArgs e)
        => UpdateArchiveFilterControlsV071();

    private void UpdateArchiveFilterControlsV071()
    {
        string mode = SelectedArchiveFilterModeV071();
        if (_archiveLastDaysPanelV071 is not null)
            _archiveLastDaysPanelV071.Visibility = mode == "LastDays" ? Visibility.Visible : Visibility.Collapsed;
        if (_archiveBetweenPanelV071 is not null)
            _archiveBetweenPanelV071.Visibility = mode == "Between" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ArchiveApplyFilterV071_Click(object sender, RoutedEventArgs e)
        => await ApplyArchiveFilterV071Async();

    private async void ArchiveFilterV071_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await ApplyArchiveFilterV071Async();
    }

    private async Task ApplyArchiveFilterV071Async()
    {
        if (_archiveFilterStatusV071 is null) return;

        string mode = SelectedArchiveFilterModeV071();
        if (mode == "LastDays")
        {
            if (!int.TryParse(_archiveLastDaysV071?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int days) ||
                days < 1 || days > 3650)
            {
                _archiveFilterStatusV071.Foreground = (Brush)FindResource("Warning");
                _archiveFilterStatusV071.Text = "Enter 1–3650 days.";
                return;
            }
            _settings.ArchiveLastDays = days;
        }
        else if (mode == "Between")
        {
            if (_archiveFromDateV071?.SelectedDate is not DateTime from ||
                _archiveToDateV071?.SelectedDate is not DateTime to ||
                from.Date > to.Date)
            {
                _archiveFilterStatusV071.Foreground = (Brush)FindResource("Warning");
                _archiveFilterStatusV071.Text = "Choose a valid date range.";
                return;
            }

            _settings.ArchiveFromDate = from.Date;
            _settings.ArchiveToDate = to.Date;
        }

        try
        {
            _settings.ArchiveFilterMode = mode;
            _settingsService.Save(_settings);
            _archiveFilterStatusV071.Foreground = (Brush)FindResource("MutedText");
            _archiveFilterStatusV071.Text = "Loading…";
            await RefreshArchiveAsync();
        }
        catch (Exception ex)
        {
            _archiveFilterStatusV071.Foreground = (Brush)FindResource("Warning");
            _archiveFilterStatusV071.Text = "Could not apply filter.";
            DiagnosticLogger.Error("Unable to apply the Archive date filter.", ex);
        }
    }

    private string SelectedArchiveFilterModeV071()
        => NormalizeArchiveFilterModeV071(
            (_archiveFilterModeV071?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ??
            _settings.ArchiveFilterMode);

    private static string NormalizeArchiveFilterModeV071(string? mode)
        => mode switch
        {
            "All" => "All",
            "Between" => "Between",
            _ => "LastDays"
        };

    private (DateTime? FromDate, DateTime? ToDate) GetArchiveFilterRangeV071()
    {
        string mode = NormalizeArchiveFilterModeV071(_settings.ArchiveFilterMode);
        if (mode == "All") return (null, null);
        if (mode == "Between")
        {
            DateTime from = (_settings.ArchiveFromDate ?? DateTime.Today.AddDays(-29)).Date;
            DateTime to = (_settings.ArchiveToDate ?? DateTime.Today).Date;
            return from <= to ? (from, to) : (to, from);
        }

        int days = Math.Clamp(_settings.ArchiveLastDays, 1, 3650);
        return (DateTime.Today.AddDays(-(days - 1)), DateTime.Today);
    }

    private static IReadOnlyList<SessionIndexEntry> FilterCachedArchiveEntriesV071(
        IReadOnlyList<SessionIndexEntry> entries,
        DateTime? fromDate,
        DateTime? toDate)
        => entries
            .Where(entry =>
            {
                DateTime date = ResolveArchiveEntryDateV071(entry);
                return (!fromDate.HasValue || date >= fromDate.Value.Date) &&
                       (!toDate.HasValue || date <= toDate.Value.Date);
            })
            .OrderByDescending(entry => entry.LastWriteUtc)
            .ToArray();

    private static DateTime ResolveArchiveEntryDateV071(SessionIndexEntry entry)
    {
        Match match = ArchiveDateV071.Match(entry.FileName);
        return match.Success && DateTime.TryParseExact(
            match.Groups["date"].Value,
            "dd-MMMM-yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime parsed)
            ? parsed.Date
            : entry.LastWriteUtc.ToLocalTime().Date;
    }

    private void UpdateArchiveFilterStatusV071(int visibleCount)
    {
        if (_archiveFilterStatusV071 is null) return;
        _archiveFilterStatusV071.Foreground = (Brush)FindResource("MutedText");
        _archiveFilterStatusV071.Text = $"{visibleCount:N0} shown";
    }
}
