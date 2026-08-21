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
    private bool _notesBookmarksPageInitialized;
    private Grid? _notesBookmarksPage;
    private ListBox? _notesBookmarksList;

    private void EnsureNotesBookmarksPage()
    {
        if (_notesBookmarksPageInitialized) return;
        _notesBookmarksPageInitialized = true;

        if (SettingsNav.Parent is not StackPanel navPanel || DashboardPage.Parent is not Grid pageHost) return;

        var navButton = new Button
        {
            Content = "Notes & Bookmarks",
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        navButton.Click += NotesBookmarksNav_Click;
        int settingsIndex = navPanel.Children.IndexOf(SettingsNav);
        navPanel.Children.Insert(Math.Max(0, settingsIndex), navButton);

        _notesBookmarksPage = new Grid { Visibility = Visibility.Collapsed };
        _notesBookmarksPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _notesBookmarksPage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        _notesBookmarksPage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_notesBookmarksPage, 2);

        var topCard = new Border { Style = (Style)FindResource("CardStyle") };
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "Notes & bookmarks", FontSize = 17, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock
        {
            Text = "Bookmark useful lines or attach notes to moments in a chatlog. Double-click an entry to reopen its source line.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        topGrid.Children.Add(heading);

        var addSessionNote = new Button
        {
            Content = "Add session note",
            Padding = new Thickness(12, 7, 12, 7),
            VerticalAlignment = VerticalAlignment.Center
        };
        addSessionNote.Click += AddSessionNote_Click;
        Grid.SetColumn(addSessionNote, 1);
        topGrid.Children.Add(addSessionNote);
        topCard.Child = topGrid;
        _notesBookmarksPage.Children.Add(topCard);

        var listCard = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(0) };
        Grid.SetRow(listCard, 2);
        var listGrid = new Grid();
        listGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        listGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var listHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x1A, 0x21)),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12),
            Child = new TextBlock { Text = "Saved moments", FontSize = 16, FontWeight = FontWeights.SemiBold }
        };
        listGrid.Children.Add(listHeader);

        _notesBookmarksList = new ListBox
        {
            ItemsSource = NotesAndBookmarks,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8)
        };
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(NoteBookmarkEntry.Display)));
        textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        textFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 5, 4, 5));
        _notesBookmarksList.ItemTemplate = new DataTemplate(typeof(NoteBookmarkEntry)) { VisualTree = textFactory };
        _notesBookmarksList.MouseDoubleClick += NotesBookmarksList_MouseDoubleClick;
        Grid.SetRow(_notesBookmarksList, 1);
        listGrid.Children.Add(_notesBookmarksList);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12)
        };
        var open = new Button { Content = "Open source", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        open.Click += OpenSelectedNoteSource_Click;
        var delete = new Button { Content = "Delete", Padding = new Thickness(12, 6, 12, 6) };
        delete.Click += DeleteSelectedNote_Click;
        actions.Children.Add(open);
        actions.Children.Add(delete);
        Grid.SetRow(actions, 2);
        listGrid.Children.Add(actions);

        listCard.Child = listGrid;
        _notesBookmarksPage.Children.Add(listCard);
        pageHost.Children.Add(_notesBookmarksPage);

        foreach (Button nav in new[] { DashboardNav, LiveNav, SearchNav, ArchiveNav, SettingsNav })
            nav.Click += (_, _) => { if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Collapsed; };
    }

    private void NotesBookmarksNav_Click(object sender, RoutedEventArgs e)
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        LivePage.Visibility = Visibility.Collapsed;
        SearchPage.Visibility = Visibility.Collapsed;
        ArchivePage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Visible;
        PageTitle.Text = "Notes & Bookmarks";
        PageSubtitle.Text = "Saved RP moments linked back to their chatlogs";
        ReloadNotesBookmarks();
    }

    private async void AddSessionNote_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new TextPromptWindow("Add session note", "Write a note for the current session.") { Owner = this };
        if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Value)) return;

        try
        {
            await _notesBookmarksService.AddSessionNoteAsync(
                prompt.Value,
                GetCurrentServerDisplayName(),
                _journal.ActiveFile,
                CancellationToken.None);
            ReloadNotesBookmarks();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save session note.", ex);
        }
    }

    private void NotesBookmarksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => OpenSelectedNoteSource();

    private void OpenSelectedNoteSource_Click(object sender, RoutedEventArgs e)
        => OpenSelectedNoteSource();

    private void OpenSelectedNoteSource()
    {
        if (_notesBookmarksList?.SelectedItem is not NoteBookmarkEntry entry) return;
        string? source = ResolveSavedMarkerSource(entry);
        if (source is null)
        {
            System.Windows.MessageBox.Show(this, "The original chatlog could not be found.", "Afterline", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var viewer = new LogViewerWindow(source, entry.LineNumber) { Owner = this };
        viewer.Show();
    }

    private string? ResolveSavedMarkerSource(NoteBookmarkEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.FilePath) && File.Exists(entry.FilePath)) return entry.FilePath;
        if (string.IsNullOrWhiteSpace(entry.FilePath) || !Directory.Exists(_settings.ArchiveRoot)) return null;

        try
        {
            string fileName = Path.GetFileName(entry.FilePath);
            return Directory.EnumerateFiles(_settings.ArchiveRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async void DeleteSelectedNote_Click(object sender, RoutedEventArgs e)
    {
        if (_notesBookmarksList?.SelectedItem is not NoteBookmarkEntry entry) return;
        MessageBoxResult result = System.Windows.MessageBox.Show(
            this,
            "Delete this saved note/bookmark?",
            "Afterline",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        await _notesBookmarksService.DeleteAsync(entry.Id, CancellationToken.None);
        ReloadNotesBookmarks();
    }
}
