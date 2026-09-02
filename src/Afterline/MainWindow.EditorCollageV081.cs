using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed record EditorCollagePresetV081(string Id, string Name, IReadOnlyList<Rect> Slots)
    {
        public override string ToString() => Name;
    }

    private sealed record EditorCollageCanvasV081(string Name, int Width, int Height)
    {
        public override string ToString() => $"{Name} · {Width:N0} × {Height:N0}";
    }

    private static readonly EditorCollagePresetV081[] EditorCollagePresetsV081 =
    {
        new("split-2", "Two columns", new[]
        {
            new Rect(0, 0, 0.5, 1), new Rect(0.5, 0, 0.5, 1)
        }),
        new("stack-2", "Two rows", new[]
        {
            new Rect(0, 0, 1, 0.5), new Rect(0, 0.5, 1, 0.5)
        }),
        new("grid-4", "Four image grid", new[]
        {
            new Rect(0, 0, 0.5, 0.5), new Rect(0.5, 0, 0.5, 0.5),
            new Rect(0, 0.5, 0.5, 0.5), new Rect(0.5, 0.5, 0.5, 0.5)
        }),
        new("feature-right", "Feature + two", new[]
        {
            new Rect(0, 0, 0.64, 1), new Rect(0.64, 0, 0.36, 0.5), new Rect(0.64, 0.5, 0.36, 0.5)
        }),
        new("feature-bottom", "Feature + three", new[]
        {
            new Rect(0, 0, 1, 0.67),
            new Rect(0, 0.67, 1.0 / 3, 0.33),
            new Rect(1.0 / 3, 0.67, 1.0 / 3, 0.33),
            new Rect(2.0 / 3, 0.67, 1.0 / 3, 0.33)
        }),
        new("filmstrip-3", "Three-image filmstrip", new[]
        {
            new Rect(0, 0, 1.0 / 3, 1),
            new Rect(1.0 / 3, 0, 1.0 / 3, 1),
            new Rect(2.0 / 3, 0, 1.0 / 3, 1)
        })
    };

    private static readonly EditorCollageCanvasV081[] EditorCollageCanvasesV081 =
    {
        new("HD landscape", 1920, 1080),
        new("Square", 1080, 1080),
        new("Portrait", 1080, 1350),
        new("4K landscape", 3840, 2160)
    };

    private ComboBox? _editorCollagePresetV081;
    private ComboBox? _editorCollageCanvasV081;
    private Slider? _editorCollageGapV081;
    private bool _editorCollagePanningV081;
    private EditorImageLayerV067? _editorCollagePanLayerV081;
    private Point _editorCollagePanStartV081;
    private double _editorCollagePanStartXV081;
    private double _editorCollagePanStartYV081;

    private void ConfigureCollageMakerV081(Grid editorBody)
    {
        if (_editorToolPanels.ContainsKey("collage")) return;
        _editorToolPanels["collage"] = BuildCollagePanelV081();

        Border? rail = editorBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        StackPanel? tools = rail is null
            ? null
            : FindVisualChildrenCanary<StackPanel>(rail)
                .FirstOrDefault(stack => stack.Children.OfType<Button>().Any(button =>
                    button.ToolTip?.ToString()?.Contains("Selection", StringComparison.OrdinalIgnoreCase) == true));
        if (tools is null) return;

        var button = CreateCanaryRailButtonV2(
            "▦",
            "Collage Maker — create fixed image frames and drop Explorer images into them.",
            "collage",
            "Segoe UI Symbol",
            18);
        Button? selection = tools.Children.OfType<Button>().FirstOrDefault(existing =>
            existing.ToolTip?.ToString()?.Contains("Selection", StringComparison.OrdinalIgnoreCase) == true);
        int index = selection is null ? tools.Children.Count : tools.Children.IndexOf(selection);
        tools.Children.Insert(Math.Max(0, index), button);
    }

    private FrameworkElement BuildCollagePanelV081()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Choose a fixed collage layout, then drop images from Explorer onto its numbered frames. Drag a filled frame to reposition the image crop without moving the frame."));

        _editorCollagePresetV081 = new ComboBox { Height = 34, ItemsSource = EditorCollagePresetsV081, SelectedIndex = 0 };
        content.Children.Add(CreateEditorField("Layout", _editorCollagePresetV081));

        _editorCollageCanvasV081 = new ComboBox { Height = 34, ItemsSource = EditorCollageCanvasesV081, SelectedIndex = 0 };
        content.Children.Add(CreateEditorField("Canvas size", _editorCollageCanvasV081));

        var gap = CreateEditorV041Slider("Frame gap", 0, 80, 12, 1);
        _editorCollageGapV081 = gap.Slider;
        content.Children.Add(gap.Panel);

        var create = new Button
        {
            Content = "Create Collage",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 8, 0, 0),
            Style = (Style)FindResource("PrimaryButton"),
            ToolTip = "Create the selected frame layout. Undo restores the prior Base Image and layers."
        };
        create.Click += (_, _) => CreateCollageV081();
        content.Children.Add(create);
        content.Children.Add(EditorSubtleNote(
            "Frame borders and drop labels are preview guides only. Empty frames remain transparent in exported PNG files."));
        return WrapEditorToolPanel(content);
    }

    private void CreateCollageV081()
    {
        EditorCollagePresetV081 preset = _editorCollagePresetV081?.SelectedItem as EditorCollagePresetV081
            ?? EditorCollagePresetsV081[0];
        EditorCollageCanvasV081 canvas = _editorCollageCanvasV081?.SelectedItem as EditorCollageCanvasV081
            ?? EditorCollageCanvasesV081[0];
        double gap = Math.Clamp(_editorCollageGapV081?.Value ?? 12, 0, 80);

        PushEditorDocumentHistoryV081("collage creation");
        ClearImageLayersV067(clearHistory: false);
        ClearSelectionCanarySilently();
        _editorSyntheticBaseV081 = true;
        _editorActiveProjectBackgroundV081 = ResolveProjectBackgroundV081();
        _editorBaseOriginal = CreateProjectBackgroundBitmapV081(canvas.Width, canvas.Height, _editorActiveProjectBackgroundV081);
        _editorFilterCommittedCanary = CloneBitmapCanary(_editorBaseOriginal);
        _editorFilterPreviewCanary = null;
        _editorFilterTrackedMediaCanary = null;
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();

        BitmapSource empty = CreateProjectBackgroundBitmapV081(1, 1, "Transparent");
        for (int index = 0; index < preset.Slots.Count; index++)
        {
            Rect slot = preset.Slots[index];
            double left = slot.X * canvas.Width + gap / 2;
            double top = slot.Y * canvas.Height + gap / 2;
            double width = Math.Max(16, slot.Width * canvas.Width - gap);
            double height = Math.Max(16, slot.Height * canvas.Height - gap);
            AddImageLayerFromBitmapV067(
                empty,
                $"Collage Slot {index + 1}",
                left,
                top,
                width: width,
                height: height,
                refresh: false,
                isCollageFrame: true,
                collagePresetId: preset.Id,
                collageSlotIndex: index);
        }

        UpdateEditorLayerZOrderV067();
        RefreshLayerListV067(_editorImageLayersV067.FirstOrDefault());
        RefreshSelectedLayerAdornerV068();
        SyncCanaryGuideHostSize();
        _editorFitZoom = true;
        Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
        SetEditorStatus($"Created {preset.Name.ToLowerInvariant()} collage · {canvas.Width:N0} × {canvas.Height:N0}px. Drop images onto the numbered frames.");
    }

    private bool TryAssignDroppedImagesToCollageFrameV081(IReadOnlyList<string> paths, Point dropPoint)
    {
        if (paths.Count == 0) return false;
        EditorImageLayerV067? hit = _editorImageLayersV067
            .Where(layer => layer.IsCollageFrame && layer.IsVisible)
            .Reverse()
            .FirstOrDefault(layer => LayerBoundsV068(layer).Contains(dropPoint));
        if (hit is null) return false;

        PushEditorDocumentHistoryV081("collage image drop");
        List<EditorImageLayerV067> frames = _editorImageLayersV067
            .Where(layer => layer.IsCollageFrame)
            .OrderBy(layer => layer.CollageSlotIndex)
            .ToList();
        int start = Math.Max(0, frames.IndexOf(hit));
        List<EditorImageLayerV067> targets = new[] { hit }
            .Concat(frames.Skip(start + 1))
            .Concat(frames.Take(start))
            .Where((candidate, index) => index == 0 || candidate.CollageSource is null)
            .Take(paths.Count)
            .ToList();
        int assigned = 0;
        EditorImageLayerV067? last = null;
        for (int pathIndex = 0; pathIndex < targets.Count; pathIndex++)
        {
            EditorImageLayerV067 frame = targets[pathIndex];
            try
            {
                frame.CollageSource = LoadBitmapFileV067(paths[pathIndex]);
                frame.CollageOffsetX = 0;
                frame.CollageOffsetY = 0;
                frame.Name = $"Collage Slot {frame.CollageSlotIndex + 1} · {Path.GetFileName(paths[pathIndex])}";
                RefreshCollageFrameBitmapV081(frame);
                UpdateImageLayerVisualV067(frame);
                last = frame;
                assigned++;
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error($"Unable to fill collage frame from '{Path.GetFileName(paths[pathIndex])}'.", ex);
            }
        }

        RefreshLayerListV067(last ?? hit);
        if (last is not null) SelectImageLayerV068(last);
        RefreshSelectedLayerAdornerV068();
        SetEditorStatus(assigned == 1
            ? "Image snapped into the collage frame. Drag it to reposition the crop."
            : $"Filled {assigned:N0} collage frames. Drag a filled frame to reposition its crop.");
        return true;
    }

    private void RefreshCollageFrameBitmapV081(EditorImageLayerV067 layer)
    {
        if (!layer.IsCollageFrame) return;
        if (layer.CollageSource is null)
        {
            layer.Bitmap = CreateProjectBackgroundBitmapV081(1, 1, "Transparent");
            return;
        }

        BitmapSource source = layer.CollageSource;
        double targetAspect = Math.Max(0.001, layer.Width / Math.Max(1, layer.Height));
        double sourceAspect = source.PixelWidth / (double)Math.Max(1, source.PixelHeight);
        int cropWidth = source.PixelWidth;
        int cropHeight = source.PixelHeight;
        int x = 0;
        int y = 0;
        if (sourceAspect > targetAspect)
        {
            cropWidth = Math.Clamp((int)Math.Round(source.PixelHeight * targetAspect), 1, source.PixelWidth);
            int available = source.PixelWidth - cropWidth;
            x = (int)Math.Round(available * (Math.Clamp(layer.CollageOffsetX, -1, 1) + 1) / 2);
        }
        else if (sourceAspect < targetAspect)
        {
            cropHeight = Math.Clamp((int)Math.Round(source.PixelWidth / targetAspect), 1, source.PixelHeight);
            int available = source.PixelHeight - cropHeight;
            y = (int)Math.Round(available * (Math.Clamp(layer.CollageOffsetY, -1, 1) + 1) / 2);
        }
        x = Math.Clamp(x, 0, Math.Max(0, source.PixelWidth - cropWidth));
        y = Math.Clamp(y, 0, Math.Max(0, source.PixelHeight - cropHeight));
        var cropped = new CroppedBitmap(source, new Int32Rect(x, y, cropWidth, cropHeight));
        cropped.Freeze();
        layer.Bitmap = cropped;
    }

    private void UpdateCollageFrameVisualV081(EditorImageLayerV067 layer)
    {
        if (!layer.IsCollageFrame)
        {
            RemoveCollagePlaceholderV081(layer);
            return;
        }
        if (_editorGuideHostCanary is null) return;

        if (layer.CollagePlaceholderOverlay is null)
        {
            var dropLabel = new TextBlock
            {
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = false
            };
            var border = new Border
            {
                Child = dropLabel,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            border.SetResourceReference(Border.BorderBrushProperty, "Accent");
            layer.CollagePlaceholderOverlay = border;
            _editorGuideHostCanary.Children.Add(border);
        }

        Border overlay = layer.CollagePlaceholderOverlay;
        overlay.Width = Math.Max(1, layer.Width);
        overlay.Height = Math.Max(1, layer.Height);
        overlay.Margin = new Thickness(layer.X, layer.Y, 0, 0);
        overlay.RenderTransform = new TranslateTransform(_editorPasteboardOffsetXV078, _editorPasteboardOffsetYV078);
        overlay.Visibility = layer.IsVisible && (layer.CollageSource is null || ReferenceEquals(layer, _editorSelectedImageLayerV067))
            ? Visibility.Visible
            : Visibility.Collapsed;
        overlay.Opacity = layer.CollageSource is null ? 0.95 : 0.75;
        overlay.Background = layer.CollageSource is null
            ? new SolidColorBrush(Color.FromArgb(45, 100, 120, 145))
            : Brushes.Transparent;
        if (overlay.Child is TextBlock label)
        {
            label.Text = layer.CollageSource is null ? $"DROP IMAGE\nSLOT {layer.CollageSlotIndex + 1}" : string.Empty;
            label.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        }
    }

    private void RemoveCollagePlaceholderV081(EditorImageLayerV067 layer)
    {
        if (layer.CollagePlaceholderOverlay is not Border overlay) return;
        if (overlay.Parent is Panel parent) parent.Children.Remove(overlay);
        layer.CollagePlaceholderOverlay = null;
    }

    private bool BeginCollagePanV081(EditorImageLayerV067 layer, Point point, object sender)
    {
        if (!layer.IsCollageFrame) return false;
        if (layer.CollageSource is null)
        {
            SetEditorStatus($"Drop an image from Explorer onto collage slot {layer.CollageSlotIndex + 1} first.");
            return true;
        }
        PushLayerEditHistoryV068(layer, "collage crop position");
        _editorCollagePanningV081 = true;
        _editorCollagePanLayerV081 = layer;
        _editorCollagePanStartV081 = point;
        _editorCollagePanStartXV081 = layer.CollageOffsetX;
        _editorCollagePanStartYV081 = layer.CollageOffsetY;
        if (sender is FrameworkElement element)
        {
            element.Cursor = Cursors.Hand;
            element.CaptureMouse();
        }
        return true;
    }

    private bool ContinueCollagePanV081(Point point, MouseEventArgs e)
    {
        if (!_editorCollagePanningV081 || _editorCollagePanLayerV081 is not EditorImageLayerV067 layer)
            return false;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndCollagePanV081();
            return true;
        }
        double dx = point.X - _editorCollagePanStartV081.X;
        double dy = point.Y - _editorCollagePanStartV081.Y;
        layer.CollageOffsetX = Math.Clamp(_editorCollagePanStartXV081 - dx * 2 / Math.Max(1, layer.Width), -1, 1);
        layer.CollageOffsetY = Math.Clamp(_editorCollagePanStartYV081 - dy * 2 / Math.Max(1, layer.Height), -1, 1);
        RefreshCollageFrameBitmapV081(layer);
        UpdateImageLayerVisualV067(layer);
        return true;
    }

    private void EndCollagePanV081()
    {
        if (!_editorCollagePanningV081) return;
        EditorImageLayerV067? layer = _editorCollagePanLayerV081;
        _editorCollagePanningV081 = false;
        _editorCollagePanLayerV081 = null;
        if (layer is not null)
            RefreshLayerListV067(layer);
        SetEditorStatus("Collage crop repositioned. Undo restores the previous crop.");
    }

    private void ReplaceCollageFrameImageV081(EditorImageLayerV067 layer)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Choose image for collage slot {layer.CollageSlotIndex + 1}",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        PushLayerEditHistoryV068(layer, "collage image replacement");
        layer.CollageSource = LoadBitmapFileV067(dialog.FileName);
        layer.CollageOffsetX = 0;
        layer.CollageOffsetY = 0;
        layer.Name = $"Collage Slot {layer.CollageSlotIndex + 1} · {Path.GetFileName(dialog.FileName)}";
        RefreshCollageFrameBitmapV081(layer);
        UpdateImageLayerVisualV067(layer);
        RefreshLayerListV067(layer);
    }

    private void ClearCollageFrameV081(EditorImageLayerV067 layer)
    {
        PushLayerEditHistoryV068(layer, "collage frame clear");
        layer.CollageSource = null;
        layer.CollageOffsetX = 0;
        layer.CollageOffsetY = 0;
        layer.Name = $"Collage Slot {layer.CollageSlotIndex + 1}";
        RefreshCollageFrameBitmapV081(layer);
        UpdateImageLayerVisualV067(layer);
        RefreshLayerListV067(layer);
    }
}
