using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed record EditorCollagePresetV081(
        string Id,
        string Name,
        IReadOnlyList<Rect> Slots,
        int LogoSlotIndex = -1)
    {
        public override string ToString() => Name;

        public bool IsLogoSlot(int index) => index == LogoSlotIndex;
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
        }),
        new("grid-6", "Six image grid", new[]
        {
            new Rect(0, 0, 1.0 / 3, 0.5),
            new Rect(1.0 / 3, 0, 1.0 / 3, 0.5),
            new Rect(2.0 / 3, 0, 1.0 / 3, 0.5),
            new Rect(0, 0.5, 1.0 / 3, 0.5),
            new Rect(1.0 / 3, 0.5, 1.0 / 3, 0.5),
            new Rect(2.0 / 3, 0.5, 1.0 / 3, 0.5)
        }),
        new("grid-9", "Nine image grid", new[]
        {
            new Rect(0, 0, 1.0 / 3, 1.0 / 3),
            new Rect(1.0 / 3, 0, 1.0 / 3, 1.0 / 3),
            new Rect(2.0 / 3, 0, 1.0 / 3, 1.0 / 3),
            new Rect(0, 1.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(1.0 / 3, 1.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(2.0 / 3, 1.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(0, 2.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(1.0 / 3, 2.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(2.0 / 3, 2.0 / 3, 1.0 / 3, 1.0 / 3)
        }),
        new("feature-center-5", "Center feature + four", new[]
        {
            new Rect(0, 0, 0.25, 0.5),
            new Rect(0, 0.5, 0.25, 0.5),
            new Rect(0.25, 0, 0.5, 1),
            new Rect(0.75, 0, 0.25, 0.5),
            new Rect(0.75, 0.5, 0.25, 0.5)
        }),
        new("panorama-4", "Panorama + three", new[]
        {
            new Rect(0, 0, 1, 0.55),
            new Rect(0, 0.55, 1.0 / 3, 0.45),
            new Rect(1.0 / 3, 0.55, 1.0 / 3, 0.45),
            new Rect(2.0 / 3, 0.55, 1.0 / 3, 0.45)
        }),
        new("feature-7", "Feature + six", new[]
        {
            new Rect(0, 0, 0.5, 0.65),
            new Rect(0.5, 0, 0.5, 0.325),
            new Rect(0.5, 0.325, 0.5, 0.325),
            new Rect(0, 0.65, 0.25, 0.35),
            new Rect(0.25, 0.65, 0.25, 0.35),
            new Rect(0.5, 0.65, 0.25, 0.35),
            new Rect(0.75, 0.65, 0.25, 0.35)
        }),
        new("editorial-5", "Editorial five", new[]
        {
            new Rect(0, 0, 0.6, 0.55),
            new Rect(0.6, 0, 0.4, 0.3),
            new Rect(0.6, 0.3, 0.4, 0.25),
            new Rect(0, 0.55, 0.35, 0.45),
            new Rect(0.35, 0.55, 0.65, 0.45)
        }),
        new("brand-mosaic-8", "Brand mosaic · 8 + logo", new[]
        {
            new Rect(0, 0, 1.0 / 3, 0.20),
            new Rect(0, 0.20, 1.0 / 3, 0.40),
            new Rect(1.0 / 3, 0, 1.0 / 3, 0.42),
            new Rect(2.0 / 3, 0, 1.0 / 3, 0.20),
            new Rect(2.0 / 3, 0.20, 1.0 / 3, 0.40),
            new Rect(0, 0.60, 1.0 / 3, 0.40),
            new Rect(1.0 / 3, 0.60, 1.0 / 3, 0.40),
            new Rect(2.0 / 3, 0.60, 1.0 / 3, 0.40),
            new Rect(1.0 / 3, 0.42, 1.0 / 3, 0.18)
        }, LogoSlotIndex: 8),
        new("logo-grid-8", "Logo grid · 8 + logo", new[]
        {
            new Rect(0, 0, 1.0 / 3, 1.0 / 3),
            new Rect(1.0 / 3, 0, 1.0 / 3, 1.0 / 3),
            new Rect(2.0 / 3, 0, 1.0 / 3, 1.0 / 3),
            new Rect(0, 1.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(2.0 / 3, 1.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(0, 2.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(1.0 / 3, 2.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(2.0 / 3, 2.0 / 3, 1.0 / 3, 1.0 / 3),
            new Rect(1.0 / 3, 1.0 / 3, 1.0 / 3, 1.0 / 3)
        }, LogoSlotIndex: 8),
        new("logo-cross-4", "Logo cross · 4 + logo", new[]
        {
            new Rect(0.25, 0, 0.50, 0.28),
            new Rect(0, 0.28, 0.25, 0.44),
            new Rect(0.75, 0.28, 0.25, 0.44),
            new Rect(0.25, 0.72, 0.50, 0.28),
            new Rect(0.25, 0.28, 0.50, 0.44)
        }, LogoSlotIndex: 4),
        new("logo-band-6", "Logo band · 6 + logo", new[]
        {
            new Rect(0, 0, 1.0 / 3, 0.40),
            new Rect(1.0 / 3, 0, 1.0 / 3, 0.40),
            new Rect(2.0 / 3, 0, 1.0 / 3, 0.40),
            new Rect(0, 0.60, 1.0 / 3, 0.40),
            new Rect(1.0 / 3, 0.60, 1.0 / 3, 0.40),
            new Rect(2.0 / 3, 0.60, 1.0 / 3, 0.40),
            new Rect(0, 0.40, 1, 0.20)
        }, LogoSlotIndex: 6),
        new("logo-feature-5", "Logo feature · 5 + logo", new[]
        {
            new Rect(0, 0, 0.28, 0.50),
            new Rect(0, 0.50, 0.28, 0.50),
            new Rect(0.28, 0, 0.44, 0.72),
            new Rect(0.72, 0, 0.28, 0.50),
            new Rect(0.72, 0.50, 0.28, 0.50),
            new Rect(0.28, 0.72, 0.44, 0.28)
        }, LogoSlotIndex: 5)
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
    private Canvas? _editorCollageLayoutPreviewV086;
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
        _editorCollagePresetV081.SelectionChanged += (_, _) => RefreshCollageLayoutPreviewV086();
        content.Children.Add(CreateEditorField("Layout", _editorCollagePresetV081));

        _editorCollageCanvasV081 = new ComboBox { Height = 34, ItemsSource = EditorCollageCanvasesV081, SelectedIndex = 0 };
        _editorCollageCanvasV081.SelectionChanged += (_, _) => RefreshCollageLayoutPreviewV086();
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
        _editorCollageGapV081.ValueChanged += (_, _) =>
        {
            ApplyLiveCollageGapV082();
            RefreshCollageLayoutPreviewV086();
        };
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
            "Frame borders and drop labels are preview guides only. Empty frames remain transparent in exported PNG files. Every filled frame can be dragged to reposition its crop."));

        content.Children.Add(new TextBlock
        {
            Text = "Layout preview",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 5)
        });
        _editorCollageLayoutPreviewV086 = new Canvas
        {
            Width = 320,
            Height = 180,
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        var previewViewbox = new Viewbox
        {
            Height = 142,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _editorCollageLayoutPreviewV086
        };
        var previewBorder = new Border
        {
            Child = previewViewbox,
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        previewBorder.SetResourceReference(Border.BackgroundProperty, "Panel");
        previewBorder.SetResourceReference(Border.BorderBrushProperty, "Border");
        content.Children.Add(previewBorder);
        RefreshCollageLayoutPreviewV086();
        return WrapEditorToolPanel(content);
    }

    private void RefreshCollageLayoutPreviewV086()
    {
        if (_editorCollageLayoutPreviewV086 is null)
            return;

        EditorCollagePresetV081 preset = _editorCollagePresetV081?.SelectedItem as EditorCollagePresetV081
            ?? EditorCollagePresetsV081[0];
        EditorCollageCanvasV081 canvas = _editorCollageCanvasV081?.SelectedItem as EditorCollageCanvasV081
            ?? EditorCollageCanvasesV081[0];
        double previewWidth = 320;
        double previewHeight = Math.Clamp(previewWidth * canvas.Height / Math.Max(1d, canvas.Width), 100, 400);
        double previewGap = Math.Clamp(_editorCollageGapV081?.Value ?? 12, 0, 80) * previewWidth / canvas.Width;

        _editorCollageLayoutPreviewV086.Children.Clear();
        _editorCollageLayoutPreviewV086.Width = previewWidth;
        _editorCollageLayoutPreviewV086.Height = previewHeight;
        for (int index = 0; index < preset.Slots.Count; index++)
        {
            Rect slot = preset.Slots[index];
            bool isLogo = preset.IsLogoSlot(index);
            var label = new TextBlock
            {
                Text = isLogo ? "LOGO" : (index + 1).ToString(),
                FontSize = isLogo ? 11 : 10,
                FontWeight = isLogo ? FontWeights.Bold : FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, isLogo ? "Text" : "MutedText");
            var frame = new Border
            {
                Child = label,
                Width = Math.Max(3, slot.Width * previewWidth - previewGap),
                Height = Math.Max(3, slot.Height * previewHeight - previewGap),
                BorderThickness = new Thickness(isLogo ? 1.5 : 1),
                CornerRadius = new CornerRadius(1),
                Opacity = isLogo ? 0.82 : 0.66
            };
            frame.SetResourceReference(Border.BackgroundProperty, isLogo ? "Accent" : "Border");
            frame.SetResourceReference(Border.BorderBrushProperty, isLogo ? "Accent" : "Border");
            Canvas.SetLeft(frame, slot.X * previewWidth + previewGap / 2);
            Canvas.SetTop(frame, slot.Y * previewHeight + previewGap / 2);
            _editorCollageLayoutPreviewV086.Children.Add(frame);
        }
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
            string slotName = preset.IsLogoSlot(index) ? "Collage Logo" : $"Collage Slot {index + 1}";
            AddImageLayerFromBitmapV067(
                empty,
                slotName,
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

    private static bool IsCollageLogoSlotV086(EditorImageLayerV067 layer)
    {
        if (!layer.IsCollageFrame || string.IsNullOrWhiteSpace(layer.CollagePresetId))
            return false;

        EditorCollagePresetV081? preset = EditorCollagePresetsV081.FirstOrDefault(item =>
            string.Equals(item.Id, layer.CollagePresetId, StringComparison.Ordinal));
        return preset?.IsLogoSlot(layer.CollageSlotIndex) == true;
    }

    private static string CollageFrameNameV086(EditorImageLayerV067 layer, string? fileName = null)
    {
        string name = IsCollageLogoSlotV086(layer)
            ? "Collage Logo"
            : $"Collage Slot {layer.CollageSlotIndex + 1}";
        return string.IsNullOrWhiteSpace(fileName) ? name : $"{name} · {fileName}";
    }

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

        Int32Rect cropLeft = CalculateCollageCropRectV087(400, 200, 200, 200, -1, 0);
        Int32Rect cropRight = CalculateCollageCropRectV087(400, 200, 200, 200, 1, 0);
        Int32Rect cropTop = CalculateCollageCropRectV087(200, 400, 200, 200, 0, -1);
        Int32Rect cropBottom = CalculateCollageCropRectV087(200, 400, 200, 200, 0, 1);
        if (cropLeft != new Int32Rect(0, 0, 200, 200) ||
            cropRight != new Int32Rect(200, 0, 200, 200) ||
            cropTop != new Int32Rect(0, 0, 200, 200) ||
            cropBottom != new Int32Rect(0, 200, 200, 200))
        {
            throw new InvalidOperationException("Collage fill or drag-to-reposition crop geometry regressed.");
        }

        if (EditorCollagePresetsV081.Count(preset => preset.LogoSlotIndex < 0) < 12)
            throw new InvalidOperationException("The non-logo collage layout set is incomplete.");

        foreach (EditorCollagePresetV081 preset in EditorCollagePresetsV081)
        {
            if (preset.LogoSlotIndex >= preset.Slots.Count || preset.LogoSlotIndex < -1 ||
                preset.Slots.Any(slot => slot.X < 0 || slot.Y < 0 || slot.Width <= 0 || slot.Height <= 0 ||
                    slot.Right > 1.0001 || slot.Bottom > 1.0001))
            {
                throw new InvalidOperationException($"Collage preset '{preset.Id}' contains invalid frame geometry.");
            }

            for (int first = 0; first < preset.Slots.Count; first++)
            {
                for (int second = first + 1; second < preset.Slots.Count; second++)
                {
                    Rect a = preset.Slots[first];
                    Rect b = preset.Slots[second];
                    double overlapWidth = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
                    double overlapHeight = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
                    if (overlapWidth > 0.0001 && overlapHeight > 0.0001)
                    {
                        throw new InvalidOperationException(
                            $"Collage preset '{preset.Id}' contains overlapping frame definitions.");
                    }
                }
            }
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
                frame.Name = CollageFrameNameV086(frame, Path.GetFileName(paths[pathIndex]));
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
        Int32Rect crop = CalculateCollageCropRectV087(
            source.PixelWidth,
            source.PixelHeight,
            layer.Width,
            layer.Height,
            layer.CollageOffsetX,
            layer.CollageOffsetY);
        var cropped = new CroppedBitmap(source, crop);
        cropped.Freeze();
        layer.Bitmap = cropped;
    }

    private static Int32Rect CalculateCollageCropRectV087(
        int sourceWidth,
        int sourceHeight,
        double targetWidth,
        double targetHeight,
        double offsetX,
        double offsetY)
    {
        sourceWidth = Math.Max(1, sourceWidth);
        sourceHeight = Math.Max(1, sourceHeight);
        double targetAspect = Math.Max(0.001, targetWidth / Math.Max(1, targetHeight));
        double sourceAspect = sourceWidth / (double)sourceHeight;
        int cropWidth = sourceWidth;
        int cropHeight = sourceHeight;
        int x = 0;
        int y = 0;
        if (sourceAspect > targetAspect)
        {
            cropWidth = Math.Clamp((int)Math.Round(sourceHeight * targetAspect), 1, sourceWidth);
            int available = sourceWidth - cropWidth;
            x = (int)Math.Round(available * (Math.Clamp(offsetX, -1, 1) + 1) / 2);
        }
        else if (sourceAspect < targetAspect)
        {
            cropHeight = Math.Clamp((int)Math.Round(sourceWidth / targetAspect), 1, sourceHeight);
            int available = sourceHeight - cropHeight;
            y = (int)Math.Round(available * (Math.Clamp(offsetY, -1, 1) + 1) / 2);
        }
        x = Math.Clamp(x, 0, Math.Max(0, sourceWidth - cropWidth));
        y = Math.Clamp(y, 0, Math.Max(0, sourceHeight - cropHeight));
        return new Int32Rect(x, y, cropWidth, cropHeight);
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
            label.Text = layer.CollageSource is null
                ? IsCollageLogoSlotV086(layer)
                    ? "DROP LOGO\nOPTIONAL"
                    : $"DROP IMAGE\nSLOT {layer.CollageSlotIndex + 1}"
                : string.Empty;
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
            Title = IsCollageLogoSlotV086(layer)
                ? "Choose image for collage logo"
                : $"Choose image for collage slot {layer.CollageSlotIndex + 1}",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        PushLayerEditHistoryV068(layer, "collage image replacement");
        layer.CollageSource = LoadBitmapFileV067(dialog.FileName);
        layer.CollageOffsetX = 0;
        layer.CollageOffsetY = 0;
        layer.Name = CollageFrameNameV086(layer, Path.GetFileName(dialog.FileName));
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
        layer.Name = CollageFrameNameV086(layer);
        RefreshCollageFrameBitmapV081(layer);
        UpdateImageLayerVisualV067(layer);
        RefreshLayerListV067(layer);
    }
}
