using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private readonly NotesBookmarksService _notesBookmarksService = new();
    private bool _liveContextBookmarksInitialized;

    public ObservableCollection<NoteBookmarkEntry> NotesAndBookmarks { get; } = new();

    private void EnsureLiveContextBookmarks()
    {
        if (_liveContextBookmarksInitialized) return;
        _liveContextBookmarksInitialized = true;

        ContextMenu menu = LiveChatList.ContextMenu ?? new ContextMenu
        {
            Background = (Brush)FindResource("Raised"),
            Foreground = (Brush)FindResource("Text"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1)
        };

        menu.Items.Add(new Separator());
        menu.Items.Add(CreateReadableLiveMenuItem("Copy ±5 lines", (_, _) => CopyLiveContext(5)));
        menu.Items.Add(CreateReadableLiveMenuItem("Copy ±10 lines", (_, _) => CopyLiveContext(10)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateReadableLiveMenuItem("Bookmark line", BookmarkSelectedLiveLine_Click));
        menu.Items.Add(CreateReadableLiveMenuItem("Add note to line…", AddNoteToSelectedLiveLine_Click));
        LiveChatList.ContextMenu = menu;

        ReloadNotesBookmarks();
    }

    private MenuItem CreateReadableLiveMenuItem(string text, RoutedEventHandler handler)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("Text")
        };
        var item = new MenuItem
        {
            Header = label,
            Foreground = (Brush)FindResource("Text"),
            Background = (Brush)FindResource("Raised"),
            Padding = new Thickness(12, 7, 12, 7)
        };
        item.Click += handler;
        return item;
    }

    private void CopyLiveContext(int radius)
    {
        if (LiveChatList.SelectedItem is not ChatEntry selected) return;

        List<ChatEntry> visible = LiveChatList.Items.OfType<ChatEntry>().ToList();
        int index = visible.IndexOf(selected);
        if (index < 0) return;

        int start = Math.Max(0, index - radius);
        int end = Math.Min(visible.Count - 1, index + radius);
        string text = string.Join(Environment.NewLine, visible.Skip(start).Take(end - start + 1).Select(entry => entry.Display));

        try
        {
            Clipboard.SetText(text);
            SetLiveActionStatus($"Copied {end - start + 1:N0} lines to clipboard.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to copy Live Chat context.", ex);
            SetLiveActionStatus("Unable to copy surrounding lines.");
        }
    }

    private async void BookmarkSelectedLiveLine_Click(object sender, RoutedEventArgs e)
    {
        if (LiveChatList.SelectedItem is not ChatEntry entry) return;

        try
        {
            await _notesBookmarksService.AddForLineAsync(
                SavedMarkerKind.Bookmark,
                entry,
                GetCurrentServerDisplayName(),
                _journal.ActiveFile,
                null,
                CancellationToken.None);
            ReloadNotesBookmarks();
            SetLiveActionStatus("Line bookmarked.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to bookmark Live Chat line.", ex);
            SetLiveActionStatus("Unable to bookmark line.");
        }
    }

    private async void AddNoteToSelectedLiveLine_Click(object sender, RoutedEventArgs e)
    {
        if (LiveChatList.SelectedItem is not ChatEntry entry) return;

        var prompt = new TextPromptWindow("Add note", "Write a note for the selected chat line.") { Owner = this };
        if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Value)) return;

        try
        {
            await _notesBookmarksService.AddForLineAsync(
                SavedMarkerKind.Note,
                entry,
                GetCurrentServerDisplayName(),
                _journal.ActiveFile,
                prompt.Value,
                CancellationToken.None);
            ReloadNotesBookmarks();
            SetLiveActionStatus("Note saved.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save note for Live Chat line.", ex);
            SetLiveActionStatus("Unable to save note.");
        }
    }

    private void SetLiveActionStatus(string text)
    {
        if (_liveActionStatus is not null) _liveActionStatus.Text = text;
    }

    private void ReloadNotesBookmarks()
    {
        NotesAndBookmarks.Clear();
        foreach (NoteBookmarkEntry entry in _notesBookmarksService.Load()) NotesAndBookmarks.Add(entry);
    }
}
