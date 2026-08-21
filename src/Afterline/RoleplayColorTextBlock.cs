using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class RoleplayColorTextBlock : TextBlock
{
    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
        nameof(DisplayText),
        typeof(string),
        typeof(RoleplayColorTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualInputChanged));

    public static readonly DependencyProperty FallbackBrushProperty = DependencyProperty.Register(
        nameof(FallbackBrush),
        typeof(Brush),
        typeof(RoleplayColorTextBlock),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualInputChanged));

    public static readonly DependencyProperty UseAutomaticColorsProperty = DependencyProperty.Register(
        nameof(UseAutomaticColors),
        typeof(bool),
        typeof(RoleplayColorTextBlock),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualInputChanged));

    public static readonly DependencyProperty IsSystemMessageProperty = DependencyProperty.Register(
        nameof(IsSystemMessage),
        typeof(bool),
        typeof(RoleplayColorTextBlock),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualInputChanged));

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public Brush FallbackBrush
    {
        get => (Brush)GetValue(FallbackBrushProperty);
        set => SetValue(FallbackBrushProperty, value);
    }

    public bool UseAutomaticColors
    {
        get => (bool)GetValue(UseAutomaticColorsProperty);
        set => SetValue(UseAutomaticColorsProperty, value);
    }

    public bool IsSystemMessage
    {
        get => (bool)GetValue(IsSystemMessageProperty);
        set => SetValue(IsSystemMessageProperty, value);
    }

    private static void OnVisualInputChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is RoleplayColorTextBlock textBlock)
            textBlock.RefreshRuns();
    }

    private void RefreshRuns()
    {
        Inlines.Clear();
        string text = DisplayText ?? string.Empty;
        Brush fallback = FallbackBrush ?? Brushes.White;

        if (text.Length == 0)
        {
            Inlines.Add(new Run(" ") { Foreground = Brushes.Transparent });
            return;
        }

        // Session boundaries are presentation markers rather than captured chat. Keep them
        // consistently blue in Live Chat and Log Reader even if automatic colors are disabled.
        if (IsSystemMessage && EditorChatFormatter.IsSessionBoundaryMarker(text))
        {
            var markerBrush = new SolidColorBrush(EditorChatFormatter.Blue);
            markerBrush.Freeze();
            Inlines.Add(new Run(text) { Foreground = markerBrush });
            return;
        }

        if (!UseAutomaticColors || IsSystemMessage)
        {
            Inlines.Add(new Run(text) { Foreground = fallback });
            return;
        }

        IReadOnlyList<EditorChatLine> formatted = EditorChatFormatter.FormatLines(text, showTimestamps: true);
        EditorChatLine? line = formatted.FirstOrDefault();
        if (line is null || line.Segments.Count == 0)
        {
            Inlines.Add(new Run(text) { Foreground = fallback });
            return;
        }

        foreach (EditorChatSegment segment in line.Segments)
        {
            var brush = new SolidColorBrush(segment.Color);
            if (brush.CanFreeze) brush.Freeze();
            Inlines.Add(new Run(segment.Text) { Foreground = brush });
        }
    }
}
