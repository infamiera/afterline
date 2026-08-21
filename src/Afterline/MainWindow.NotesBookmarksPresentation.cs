using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Afterline.Models;

namespace Afterline;

public partial class MainWindow
{
    private bool _notesBookmarksPresentationInitialized;

    private void EnsureNotesBookmarksPresentation()
    {
        if (_notesBookmarksPresentationInitialized || _notesBookmarksList is null) return;
        _notesBookmarksPresentationInitialized = true;

        var textFactory = new FrameworkElementFactory(typeof(SavedMarkerTextBlock));
        textFactory.SetBinding(SavedMarkerTextBlock.EntryProperty, new Binding());
        textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        textFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 5, 4, 5));
        _notesBookmarksList.ItemTemplate = new DataTemplate(typeof(NoteBookmarkEntry)) { VisualTree = textFactory };
    }
}
