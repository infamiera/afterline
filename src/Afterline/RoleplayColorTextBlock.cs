using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.Concurrent;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

internal sealed class RoleplayColorTextBlock : TextBlock
{
    private static readonly ConcurrentDictionary<uint, SolidColorBrush> FrozenBrushes = new();
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

    public static readonly DependencyProperty ExactColorRunsProperty = DependencyProperty.Register(
        nameof(ExactColorRuns),
        typeof(IReadOnlyList<ChatColorRun>),
        typeof(RoleplayColorTextBlock),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualInputChanged));

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

    public IReadOnlyList<ChatColorRun>? ExactColorRuns
    {
        get => (IReadOnlyList<ChatColorRun>?)GetValue(ExactColorRunsProperty);
        set => SetValue(ExactColorRunsProperty, value);
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
            SolidColorBrush markerBrush = GetFrozenBrush(EditorChatFormatter.Blue);
            Inlines.Add(new Run(text) { Foreground = markerBrush });
            return;
        }

        if (!UseAutomaticColors || IsSystemMessage)
        {
            AddPlainTypographyRuns(text, fallback);
            return;
        }

        IReadOnlyList<ChatColorRun> exactRuns = ChatColorReliabilityService.EnsureExpectedAccents(text, ExactColorRuns);
        if (exactRuns.Count > 0 && ChatColorData.HasCompleteCoverage(text, exactRuns))
        {
            AddExactColorRuns(text, exactRuns, fallback);
            return;
        }

        IReadOnlyList<EditorChatLine> formatted = UnifiedChatFormatter.FormatLines(text, showTimestamps: true);
        EditorChatLine? line = formatted.FirstOrDefault();
        if (line is null || line.Segments.Count == 0)
        {
            Inlines.Add(new Run(text) { Foreground = fallback });
            return;
        }

        foreach (EditorChatSegment segment in line.Segments)
            AddSegment(segment);
    }

    private void AddExactColorRuns(
        string text,
        IReadOnlyList<ChatColorRun> colorRuns,
        Brush fallback)
    {
        var segments = new List<EditorChatSegment>(colorRuns.Count + 2);
        int cursor = 0;
        foreach (ChatColorRun colorRun in colorRuns)
        {
            if (colorRun.Start > cursor)
            {
                Color gapColor = fallback is SolidColorBrush gapBrush
                    ? gapBrush.Color
                    : EditorChatFormatter.White;
                segments.Add(new EditorChatSegment(text[cursor..colorRun.Start], gapColor));
            }

            segments.Add(new EditorChatSegment(
                text.Substring(colorRun.Start, colorRun.Length),
                Color.FromArgb(colorRun.Alpha, colorRun.Red, colorRun.Green, colorRun.Blue),
                colorRun.Italic));
            cursor = colorRun.End;
        }

        if (cursor < text.Length)
        {
            Color gapColor = fallback is SolidColorBrush gapBrush
                ? gapBrush.Color
                : EditorChatFormatter.White;
            segments.Add(new EditorChatSegment(text[cursor..], gapColor));
        }

        foreach (EditorChatSegment segment in ChatTypographyService.ApplySlashItalics(segments))
            AddSegment(segment);
    }

    private void AddPlainTypographyRuns(string text, Brush fallback)
    {
        IReadOnlyList<ChatColorRun> exact = ChatColorData.NormalizeRuns(text, ExactColorRuns);
        IReadOnlyList<EditorChatSegment> segments =
            exact.Count > 0 && ChatColorData.HasCompleteCoverage(text, exact)
                ? exact.Select(run => new EditorChatSegment(
                        text.Substring(run.Start, run.Length),
                        EditorChatFormatter.White,
                        run.Italic))
                    .ToArray()
                : new[] { new EditorChatSegment(text, EditorChatFormatter.White) };

        foreach (EditorChatSegment segment in ChatTypographyService.ApplySlashItalics(segments))
        {
            var run = new Run(segment.Text) { Foreground = fallback };
            if (segment.IsItalic) run.FontStyle = FontStyles.Italic;
            Inlines.Add(run);
        }
    }

    private void AddSegment(EditorChatSegment segment)
    {
        var run = new Run(segment.Text)
        {
            Foreground = GetFrozenBrush(segment.Color)
        };
        if (segment.IsItalic) run.FontStyle = FontStyles.Italic;
        Inlines.Add(run);
    }

    private static SolidColorBrush GetFrozenBrush(Color color)
    {
        uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        return FrozenBrushes.GetOrAdd(key, _ =>
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        });
    }
}
