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
        new("Square", 1920, 1920),
        new("Portrait", 1920, 2400),
        new("4K landscape", 3840, 2160)
    };

    private ComboBox? _editorCollagePresetV081;
    private ComboBox? _editorCollageCanvasV081;
    private Slider? _editorCollageGapV081;
    private bool _editorCollageGapUiUpdatingV082;
    private bool _editorCollageGapHistoryCapturedV082;
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
        _editorCollageGapV081.PreviewMouseLeftButtonDown += (_, _) => BeginLiveCollageGapEditV082();
        _editorCollageGapV081.PreviewMouseLeftButtonUp += (_, _) => EndLiveCollageGapEditV082();
        _editorCollageGapV081.LostMouseCapture += (_, _) => EndLiveCollageGapEditV082();
        _editorCollageGapV081.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
                BeginLiveCollageGapEditV082();
        };
        _editorCollageGapV081.PreviewKeyUp += (_, _) => EndLiveCollageGapEditV082();
        _editorCollageGapV081.ValueChanged += (_, _) => ApplyLiveCollageGapV082();
        content.Children.Add(gap.Panel);
        content.Children.Add(EditorSubtleNote(
            "Adjust this at any time. The Base Image and export size stay fixed while the collage frames expand or contract inside it."));

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
        ClearEditorContentBoundaryV083();
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
            Rect bounds = CalculateCollageFrameBoundsV082(slot, canvas.Width, canvas.Height, gap);
            AddImageLayerFromBitmapV067(
                empty,
                $"Collage Slot {index + 1}",
                bounds.X,
                bounds.Y,
                width: bounds.Width,
                height: bounds.Height,
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

    private void BeginLiveCollageGapEditV082()
    {
        if (_editorCollageGapHistoryCapturedV082 || _editorCollageGapUiUpdatingV082 ||
            !_editorImageLayersV067.Any(layer => layer.IsCollageFrame))
        {
            return;
        }

        PushEditorDocumentHistoryV081("collage frame gap");
        _editorCollageGapHistoryCapturedV082 = true;
    }

    private void EndLiveCollageGapEditV082()
        => _editorCollageGapHistoryCapturedV082 = false;

    private void ApplyLiveCollageGapV082()
    {
        if (_editorCollageGapUiUpdatingV082 || _editorCollageGapV081 is null || _editorComposition is null)
            return;

        List<EditorImageLayerV067> frames = GetActiveCollageFramesV082();
        if (frames.Count == 0)
            return;

        string? presetId = frames[0].CollagePresetId;
        EditorCollagePresetV081? preset = EditorCollagePresetsV081.FirstOrDefault(item =>
            string.Equals(item.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
            return;

        double gap = Math.Clamp(_editorCollageGapV081.Value, 0, 80);
        double canvasWidth = Math.Max(1, _editorComposition.Width);
        double canvasHeight = Math.Max(1, _editorComposition.Height);
        foreach (EditorImageLayerV067 frame in frames)
        {
            if (frame.CollageSlotIndex < 0 || frame.CollageSlotIndex >= preset.Slots.Count)
                continue;

            Rect bounds = CalculateCollageFrameBoundsV082(
                preset.Slots[frame.CollageSlotIndex],
                canvasWidth,
                canvasHeight,
                gap);
            frame.X = bounds.X;
            frame.Y = bounds.Y;
            frame.Width = bounds.Width;
            frame.Height = bounds.Height;
            RefreshCollageFrameBitmapV081(frame);
            UpdateImageLayerVisualV067(frame);
        }

        EnsureLayerCanvasExtentV067();
        RefreshSelectedLayerAdornerV068();
        SyncCanaryGuideHostSize();
        SetEditorStatus($"Collage frame gap updated to {gap:0}px. Undo restores the previous spacing.");
    }

    private List<EditorImageLayerV067> GetActiveCollageFramesV082()
    {
        string? selectedPreset = _editorSelectedImageLayerV067 is { IsCollageFrame: true } selected
            ? selected.CollagePresetId
            : _editorImageLayersV067.FirstOrDefault(layer => layer.IsCollageFrame)?.CollagePresetId;
        return _editorImageLayersV067
            .Where(layer => layer.IsCollageFrame &&
                string.Equals(layer.CollagePresetId, selectedPreset, StringComparison.Ordinal))
            .OrderBy(layer => layer.CollageSlotIndex)
            .ToList();
    }

    private void SyncCollageGapControlV082()
    {
        if (_editorCollageGapV081 is null || _editorComposition is null)
            return;

        List<EditorImageLayerV067> frames = GetActiveCollageFramesV082();
        if (frames.Count == 0)
            return;

        EditorImageLayerV067 frame = frames[0];
        EditorCollagePresetV081? preset = EditorCollagePresetsV081.FirstOrDefault(item =>
            string.Equals(item.Id, frame.CollagePresetId, StringComparison.Ordinal));
        if (preset is null || frame.CollageSlotIndex < 0 || frame.CollageSlotIndex >= preset.Slots.Count)
            return;

        Rect slot = preset.Slots[frame.CollageSlotIndex];
        double inferredGap = Math.Clamp(
            slot.Width * Math.Max(1, _editorComposition.Width) - frame.Width,
            0,
            80);
        _editorCollageGapUiUpdatingV082 = true;
        try
        {
            _editorCollageGapV081.Value = Math.Round(inferredGap);
        }
        finally
        {
            _editorCollageGapUiUpdatingV082 = false;
        }
    }

    private static Rect CalculateCollageFrameBoundsV082(
        Rect slot,
        double canvasWidth,
        double canvasHeight,
        double gap)
        => new(
            slot.X * canvasWidth + gap / 2,
            slot.Y * canvasHeight + gap / 2,
            Math.Max(16, slot.Width * canvasWidth - gap),
            Math.Max(16, slot.Height * canvasHeight - gap));

    private static void VerifyLiveCollageGapGeometryV082()
    {
        var leftSlot = new Rect(0, 0, 0.5, 1);
        var rightSlot = new Rect(0.5, 0, 0.5, 1);
        Rect spacedLeft = CalculateCollageFrameBoundsV082(leftSlot, 1920, 1080, 40);
        Rect spacedRight = CalculateCollageFrameBoundsV082(rightSlot, 1920, 1080, 40);
        Rect filledLeft = CalculateCollageFrameBoundsV082(leftSlot, 1920, 1080, 0);
        Rect filledRight = CalculateCollageFrameBoundsV082(rightSlot, 1920, 1080, 0);
        if (filledLeft.Width <= spacedLeft.Width || filledRight.Width <= spacedRight.Width ||
            filledLeft.X != 0 || filledRight.Right != 1920 ||
            Math.Abs(spacedRight.Left - spacedLeft.Right - 40) > 0.01)
        {
            throw new InvalidOperationException(
                "Live collage gap geometry changed the canvas boundary or failed to expand frames into removed spacing.");
        }
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
