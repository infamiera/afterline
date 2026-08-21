using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

internal sealed class SavedMarkerTextBlock : TextBlock
{
    public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(
        nameof(Entry),
        typeof(NoteBookmarkEntry),
        typeof(SavedMarkerTextBlock),
        new FrameworkPropertyMetadata(null, OnEntryChanged));

    public NoteBookmarkEntry? Entry
    {
        get => (NoteBookmarkEntry?)GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    private static void OnEntryChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is SavedMarkerTextBlock textBlock)
            textBlock.RefreshRuns();
    }

    private void RefreshRuns()
    {
        Inlines.Clear();
        NoteBookmarkEntry? entry = Entry;
        if (entry is null) return;

        AddRun(entry.KindLabel, EditorChatFormatter.Red, FontWeights.Bold);
        AddRun(" · ", EditorChatFormatter.White);
        AddRun(entry.ChatTimestamp.ToString("dd MMM yyyy HH:mm:ss"), EditorChatFormatter.Blue);
        AddRun(" · ", EditorChatFormatter.White);
        AddRun(entry.ServerName, EditorChatFormatter.Green);

        if (entry.LineNumber is int lineNumber)
            AddRun($" · line {lineNumber}", EditorChatFormatter.White);

        string source = string.IsNullOrWhiteSpace(entry.LineText) ? "Session note" : entry.LineText;
        AddRun("\n" + source, EditorChatFormatter.White);

        if (!string.IsNullOrWhiteSpace(entry.NoteText))
            AddRun("\n" + entry.NoteText, EditorChatFormatter.White);
    }

    private void AddRun(string text, Color color, FontWeight? weight = null)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze) brush.Freeze();
        var run = new Run(text) { Foreground = brush };
        if (weight is FontWeight fontWeight) run.FontWeight = fontWeight;
        Inlines.Add(run);
    }
}
