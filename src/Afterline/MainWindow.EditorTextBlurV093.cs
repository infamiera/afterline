using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed record EditorTextBlurTargetV093(IReadOnlyList<EditorTextColorOverride> Ranges)
    {
        internal string DisplayText
        {
            get
            {
                string joined = string.Join(" … ", Ranges.Select(range => range.Text.Trim()));
                if (joined.Length > 72) joined = joined[..69] + "…";
                return joined;
            }
        }
    }

    private sealed record EditorTextBlurSettingsV093(
        double Radius,
        double OffsetX,
        double OffsetY,
        double ExpandX,
        double ExpandY);

    private EditorTextBlurTargetV093? _editorTextBlurTargetV093;
    private TextBlock? _editorTextBlurTargetHintV093;

    private void CaptureTextBlurTargetV093()
    {
        if (_editorInput is null || _editorInput.SelectionLength <= 0)
            return;

        IReadOnlyList<EditorTextColorOverride> ranges = GetSelectedTextRangesV071(EditorChatFormatter.White);
        if (ranges.Count == 0)
            return;

        _editorTextBlurTargetV093 = new EditorTextBlurTargetV093(ranges);
        UpdateTextBlurTargetHintV093();
    }

    private IReadOnlyList<EditorTextColorOverride> GetTextBlurTargetRangesV093()
    {
        CaptureTextBlurTargetV093();
        if (_editorTextBlurTargetV093 is null || _editorInput is null)
            return Array.Empty<EditorTextColorOverride>();

        string[] lines = NormalizeEditorTextV071(_editorInput.Text).Split('\n');
        IReadOnlyList<EditorTextColorOverride> ranges = _editorTextBlurTargetV093.Ranges
            .Where(range =>
                range.SourceIndex >= 0 &&
                range.SourceIndex < lines.Length &&
                range.Start >= 0 &&
                range.Length > 0 &&
                range.End <= lines[range.SourceIndex].Length &&
                string.Equals(
                    lines[range.SourceIndex].Substring(range.Start, range.Length),
                    range.Text,
                    StringComparison.Ordinal))
            .ToArray();

        if (ranges.Count != _editorTextBlurTargetV093.Ranges.Count)
        {
            _editorTextBlurTargetV093 = null;
            UpdateTextBlurTargetHintV093();
            return Array.Empty<EditorTextColorOverride>();
        }

        return ranges;
    }

    private void UpdateTextBlurTargetHintV093()
    {
        if (_editorTextBlurTargetHintV093 is null)
            return;

        IReadOnlyList<EditorTextColorOverride> ranges = GetTextBlurTargetRangesWithoutCaptureV093();
        _editorTextBlurTargetHintV093.Text = ranges.Count == 0
            ? "No blur target selected. Highlight text in Chat & Font, or use right-click → Blur Text…"
            : $"Blur target · “{_editorTextBlurTargetV093!.DisplayText}” · {ranges.Sum(range => range.Length):N0} characters";
    }

    private IReadOnlyList<EditorTextColorOverride> GetTextBlurTargetRangesWithoutCaptureV093()
    {
        if (_editorTextBlurTargetV093 is null || _editorInput is null)
            return Array.Empty<EditorTextColorOverride>();

        string[] lines = NormalizeEditorTextV071(_editorInput.Text).Split('\n');
        IReadOnlyList<EditorTextColorOverride> ranges = _editorTextBlurTargetV093.Ranges
            .Where(range =>
                range.SourceIndex >= 0 &&
                range.SourceIndex < lines.Length &&
                range.Start >= 0 &&
                range.Length > 0 &&
                range.End <= lines[range.SourceIndex].Length &&
                string.Equals(lines[range.SourceIndex].Substring(range.Start, range.Length), range.Text, StringComparison.Ordinal))
            .ToArray();
        return ranges.Count == _editorTextBlurTargetV093.Ranges.Count
            ? ranges
            : Array.Empty<EditorTextColorOverride>();
    }

    private void EditorOpenTextBlurEditorV093_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<EditorTextColorOverride> ranges = GetTextBlurTargetRangesV093();
        if (ranges.Count == 0)
        {
            SetEditorStatus("Highlight text in Chat & Font first, then choose Blur Text…");
            return;
        }

        EditorTextBlurSettingsV093 settings = ResolveTextBlurSettingsV093(ranges);
        var dialog = new EditorTextBlurDialogV093(
            this,
            _editorTextBlurTargetV093!,
            settings,
            previewSettings => BuildTextBlurPreviewBitmapV093(_editorTextBlurTargetV093!, previewSettings));

        if (dialog.ShowDialog() != true)
            return;

        ApplyTextBlurTargetV093(ranges, dialog.Settings);
    }

    private EditorTextBlurSettingsV093 ResolveTextBlurSettingsV093(IReadOnlyList<EditorTextColorOverride> ranges)
    {
        EditorTextBlurOverride? existing = _editorTextBlurOverridesV092.LastOrDefault(value =>
            ranges.Any(range =>
                range.SourceIndex == value.SourceIndex &&
                range.Start == value.Start &&
                range.Length == value.Length &&
                string.Equals(range.Text, value.Text, StringComparison.Ordinal)));

        return existing is null
            ? new EditorTextBlurSettingsV093(_editorTextBlurRadiusSliderV092?.Value ?? 5, 0, 0, 0, 0)
            : new EditorTextBlurSettingsV093(
                existing.Radius,
                existing.OffsetX,
                existing.OffsetY,
                existing.ExpandX,
                existing.ExpandY);
    }

    private void ApplyTextBlurTargetV093(
        IReadOnlyList<EditorTextColorOverride> ranges,
        EditorTextBlurSettingsV093? settings)
    {
        if (ranges.Count == 0)
        {
            SetEditorStatus("Select text in Chat & Font before applying text blur.");
            return;
        }

        foreach (EditorTextColorOverride range in ranges)
        {
            RemoveOverlappingTextBlurV092(range.SourceIndex, range.Start, range.End);
            if (settings is not null)
            {
                _editorTextBlurOverridesV092.Add(new EditorTextBlurOverride(
                    range.SourceIndex,
                    range.Start,
                    range.Length,
                    range.Text,
                    Math.Clamp(settings.Radius, 1, 16),
                    Math.Clamp(settings.OffsetX, -80, 80),
                    Math.Clamp(settings.OffsetY, -40, 40),
                    Math.Clamp(settings.ExpandX, -8, 28),
                    Math.Clamp(settings.ExpandY, -4, 20)));
            }
        }

        _editorTextBlurOverridesV092.Sort((left, right) =>
        {
            int line = left.SourceIndex.CompareTo(right.SourceIndex);
            return line != 0 ? line : left.Start.CompareTo(right.Start);
        });
        ScheduleEditorChatRender();
        UpdateTextBlurTargetHintV093();
        SetEditorStatus(settings is null
            ? "Removed blur from the selected text."
            : "Applied text blur. You can reopen Blur Text… to refine it.");
    }

    private BitmapSource? BuildTextBlurPreviewBitmapV093(
        EditorTextBlurTargetV093 target,
        EditorTextBlurSettingsV093 settings)
    {
        if (_editorInput is null || target.Ranges.Count == 0)
            return null;

        int first = target.Ranges.Min(range => range.SourceIndex);
        int last = target.Ranges.Max(range => range.SourceIndex);
        var effective = _editorTextBlurOverridesV092
            .Where(value => !target.Ranges.Any(range =>
                range.SourceIndex == value.SourceIndex &&
                value.Start < range.End &&
                value.End > range.Start))
            .ToList();
        effective.AddRange(target.Ranges.Select(range => new EditorTextBlurOverride(
            range.SourceIndex,
            range.Start,
            range.Length,
            range.Text,
            Math.Clamp(settings.Radius, 1, 16),
            Math.Clamp(settings.OffsetX, -80, 80),
            Math.Clamp(settings.OffsetY, -40, 40),
            Math.Clamp(settings.ExpandX, -8, 28),
            Math.Clamp(settings.ExpandY, -4, 20))));

        bool showTimestamps = _editorShowTimestampsCheck?.IsChecked == true;
        double fontSize = _editorFontSizeSlider?.Value ?? 18;
        double lineSpacing = _editorLineSpacingSlider?.Value ?? 1;
        double chatWidth = Math.Max(320, _editorChatWidthSlider?.Value ?? 900);
        (FontFamily fontFamily, FontWeight fontWeight) = ResolveEditorFont();
        IReadOnlyList<EditorChatLine> lines = UnifiedChatFormatter.FormatLines(
            _editorInput.Text,
            showTimestamps,
            _editorLineColorOverrides,
            _editorExactChatColorsV068,
            _editorTextColorOverridesV071,
            effective);

        var stack = new StackPanel { Width = Math.Max(1, chatWidth - 16) };
        foreach (EditorChatLine line in lines.Where(line => line.SourceIndex >= first - 1 && line.SourceIndex <= last + 1))
        {
            var text = new TextBlock
            {
                FontFamily = fontFamily,
                FontWeight = fontWeight,
                FontSize = fontSize,
                TextAlignment = _editorChatTextAlignmentV063,
                TextWrapping = TextWrapping.Wrap,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = Math.Max(fontSize + lineSpacing, fontSize + 0.5),
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
            foreach (EditorChatSegment segment in line.Segments)
            {
                var brush = new SolidColorBrush(segment.Color);
                if (brush.CanFreeze) brush.Freeze();
                AddEditorChatSegmentInlineV092(text, segment, brush, fontFamily, fontWeight, fontSize);
            }
            stack.Children.Add(text);
        }

        var host = new Border
        {
            Width = chatWidth,
            Padding = new Thickness(8, 4, 8, 4),
            Background = Brushes.Transparent,
            Child = stack
        };
        host.Measure(new Size(chatWidth, double.PositiveInfinity));
        double height = Math.Max(1, Math.Ceiling(host.DesiredSize.Height));
        host.Arrange(new Rect(0, 0, chatWidth, height));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(chatWidth)),
            Math.Max(1, (int)Math.Ceiling(height)),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();
        return ApplyEditorChatTextEffects(bitmap);
    }

    private sealed class EditorTextBlurDialogV093 : Window
    {
        private readonly Func<EditorTextBlurSettingsV093, BitmapSource?> _previewFactory;
        private readonly Image _previewImage;
        private readonly ScaleTransform _previewScale = new(1, 1);
        private readonly Slider _radius;
        private readonly Slider _expandX;
        private readonly Slider _expandY;
        private readonly Slider _offsetX;
        private readonly Slider _offsetY;
        private readonly Slider _zoom;
        private bool _dragging;
        private Point _dragStart;
        private double _dragOffsetX;
        private double _dragOffsetY;

        internal EditorTextBlurSettingsV093 Settings => new(
            _radius.Value,
            _offsetX.Value,
            _offsetY.Value,
            _expandX.Value,
            _expandY.Value);

        internal EditorTextBlurDialogV093(
            MainWindow owner,
            EditorTextBlurTargetV093 target,
            EditorTextBlurSettingsV093 initial,
            Func<EditorTextBlurSettingsV093, BitmapSource?> previewFactory)
        {
            Owner = owner;
            Title = "Blur Text";
            Width = 820;
            Height = 600;
            MinWidth = 660;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)owner.FindResource("Bg");
            Foreground = (Brush)owner.FindResource("Text");
            _previewFactory = previewFactory;

            var root = new Grid { Margin = new Thickness(18) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new StackPanel { Margin = new Thickness(0, 0, 14, 12) };
            heading.Children.Add(new TextBlock
            {
                Text = "Blur selected text",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold
            });
            heading.Children.Add(new TextBlock
            {
                Text = $"Target · “{target.DisplayText}”",
                Foreground = (Brush)owner.FindResource("MutedText"),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            root.Children.Add(heading);

            var previewBorder = new Border
            {
                Background = (Brush)owner.FindResource("Raised"),
                BorderBrush = (Brush)owner.FindResource("Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 14, 12)
            };
            Grid.SetRow(previewBorder, 1);
            root.Children.Add(previewBorder);

            var previewRoot = new Grid();
            previewRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var previewToolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
            previewToolbar.Children.Add(new TextBlock
            {
                Text = "Rendered chat preview · drag the blurred block to reposition it",
                Foreground = (Brush)owner.FindResource("MutedText"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            var zoomButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Button zoomOut = CreateDialogButton(owner, "−", "Zoom out");
            Button fit = CreateDialogButton(owner, "100%", "Reset preview zoom");
            Button zoomIn = CreateDialogButton(owner, "+", "Zoom in");
            zoomButtons.Children.Add(zoomOut);
            zoomButtons.Children.Add(fit);
            zoomButtons.Children.Add(zoomIn);
            DockPanel.SetDock(zoomButtons, Dock.Right);
            previewToolbar.Children.Add(zoomButtons);
            previewRoot.Children.Add(previewToolbar);

            _previewImage = new Image
            {
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                LayoutTransform = _previewScale,
                Cursor = Cursors.SizeAll,
                SnapsToDevicePixels = true
            };
            _previewImage.MouseLeftButtonDown += BlurPreviewMouseDown;
            _previewImage.MouseMove += BlurPreviewMouseMove;
            _previewImage.MouseLeftButtonUp += BlurPreviewMouseUp;
            _previewImage.LostMouseCapture += (_, _) => _dragging = false;
            var previewScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _previewImage
            };
            Grid.SetRow(previewScroll, 1);
            previewRoot.Children.Add(previewScroll);
            previewBorder.Child = previewRoot;

            var controls = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetColumn(controls, 1);
            Grid.SetRow(controls, 1);
            root.Children.Add(controls);
            controls.Children.Add(CreateDialogCaption(owner, "BLUR CONTROLS"));
            _radius = CreateDialogSlider(owner, controls, "Strength", 1, 16, initial.Radius, 1);
            controls.Children.Add(CreateDialogCaption(owner, "BLUR BLOCK"));
            _expandX = CreateDialogSlider(owner, controls, "Horizontal coverage", -8, 28, initial.ExpandX, 1);
            _expandY = CreateDialogSlider(owner, controls, "Vertical coverage", -4, 20, initial.ExpandY, 1);
            _offsetX = CreateDialogSlider(owner, controls, "Horizontal position", -80, 80, initial.OffsetX, 1);
            _offsetY = CreateDialogSlider(owner, controls, "Vertical position", -40, 40, initial.OffsetY, 1);
            controls.Children.Add(CreateDialogCaption(owner, "PREVIEW ZOOM"));
            _zoom = CreateDialogSlider(owner, controls, "Zoom", 50, 300, 100, 10);

            var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(footer, 2);
            Grid.SetColumnSpan(footer, 2);
            root.Children.Add(footer);
            footer.Children.Add(new TextBlock
            {
                Text = "Changes are previewed here and are only saved when you apply them.",
                Foreground = (Brush)owner.FindResource("MutedText"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = CreateDialogButton(owner, "Cancel", "Discard these blur changes");
            Button apply = CreateDialogButton(owner, "Apply blur", "Apply this blur to the selected text");
            apply.Style = (Style)owner.FindResource("PrimaryButton");
            cancel.Click += (_, _) => DialogResult = false;
            apply.Click += (_, _) => DialogResult = true;
            actions.Children.Add(cancel);
            actions.Children.Add(apply);
            DockPanel.SetDock(actions, Dock.Right);
            footer.Children.Add(actions);

            Content = root;
            zoomOut.Click += (_, _) => _zoom.Value = Math.Max(_zoom.Minimum, _zoom.Value - 25);
            fit.Click += (_, _) => _zoom.Value = 100;
            zoomIn.Click += (_, _) => _zoom.Value = Math.Min(_zoom.Maximum, _zoom.Value + 25);
            foreach (Slider slider in new[] { _radius, _expandX, _expandY, _offsetX, _offsetY })
                slider.ValueChanged += (_, _) => RefreshPreview();
            _zoom.ValueChanged += (_, _) => _previewScale.ScaleX = _previewScale.ScaleY = _zoom.Value / 100d;
            Loaded += (_, _) => RefreshPreview();
        }

        private void RefreshPreview()
        {
            BitmapSource? preview = _previewFactory(Settings);
            if (preview is not null)
                _previewImage.Source = preview;
        }

        private void BlurPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _dragStart = e.GetPosition(_previewImage);
            _dragOffsetX = _offsetX.Value;
            _dragOffsetY = _offsetY.Value;
            _previewImage.CaptureMouse();
            e.Handled = true;
        }

        private void BlurPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point current = e.GetPosition(_previewImage);
            double zoom = Math.Max(0.5, _zoom.Value / 100d);
            _offsetX.Value = Math.Clamp(_dragOffsetX + (current.X - _dragStart.X) / zoom, _offsetX.Minimum, _offsetX.Maximum);
            _offsetY.Value = Math.Clamp(_dragOffsetY + (current.Y - _dragStart.Y) / zoom, _offsetY.Minimum, _offsetY.Maximum);
        }

        private void BlurPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            if (_previewImage.IsMouseCaptured)
                _previewImage.ReleaseMouseCapture();
            e.Handled = true;
        }

        private static TextBlock CreateDialogCaption(MainWindow owner, string text)
            => new()
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)owner.FindResource("MutedText"),
                Margin = new Thickness(0, 8, 0, 6)
            };

        private static Slider CreateDialogSlider(
            MainWindow owner,
            Panel parent,
            string label,
            double minimum,
            double maximum,
            double value,
            double tick)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = (Brush)owner.FindResource("Text") });
            var valueText = new TextBlock { FontSize = 10, Foreground = (Brush)owner.FindResource("MutedText") };
            Grid.SetColumn(valueText, 1);
            header.Children.Add(valueText);
            panel.Children.Add(header);
            var slider = new Slider
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Clamp(value, minimum, maximum),
                TickFrequency = tick,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 3, 0, 0)
            };
            slider.ValueChanged += (_, _) => valueText.Text = slider.Value.ToString("0");
            panel.Children.Add(slider);
            parent.Children.Add(panel);
            return slider;
        }

        private static Button CreateDialogButton(MainWindow owner, string text, string tooltip)
            => new()
            {
                Content = text,
                MinHeight = 30,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = tooltip
            };
    }
}
