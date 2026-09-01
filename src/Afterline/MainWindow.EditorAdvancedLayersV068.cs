using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Afterline;

public partial class MainWindow
{
    private enum EditorLayerPixelToolV068
    {
        None,
        Paint,
        Erase
    }

    private enum EditorLayerResizeHandleV071
    {
        NorthWest,
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West
    }

    private sealed record EditorLayerStateV068(
        string LayerId,
        BitmapSource Bitmap,
        double X,
        double Y,
        double Width,
        double Height,
        double Opacity,
        bool Visible,
        bool Locked,
        string Description);

    private bool _editorAdvancedLayersV068Initialized;
    private Expander? _editorFilterAdjustmentsExpanderV068;
    private Rectangle? _editorLayerSelectionOutlineV068;
    private readonly Dictionary<EditorLayerResizeHandleV071, Thumb> _editorLayerResizeThumbsV071 = new();
    private Button? _editorLayerLockBadgeV068;
    private ComboBox? _editorLayerPaintColorV068;
    private Slider? _editorLayerBrushSizeV068;
    private Button? _editorLayerPaintToolV068;
    private Button? _editorLayerEraseToolV068;
    private EditorLayerPixelToolV068 _editorLayerPixelToolV068 = EditorLayerPixelToolV068.Paint;

    private bool _editorLayerDraggingV068;
    private EditorImageLayerV067? _editorLayerDragTargetV068;
    private Point _editorLayerDragStartV068;
    private double _editorLayerDragStartXV068;
    private double _editorLayerDragStartYV068;

    private double _editorLayerResizeStartWidthV068;
    private double _editorLayerResizeStartHeightV068;
    private double _editorLayerResizeStartXV071;
    private double _editorLayerResizeStartYV071;
    private Point _editorLayerResizePointerStartV079;
    private EditorLayerResizeHandleV071 _editorLayerResizeHandleV071 = EditorLayerResizeHandleV071.SouthEast;

    private bool _editorLayerStrokeActiveV068;
    private Point _editorLayerStrokeLastPointV068;
    private WriteableBitmap? _editorLayerStrokeBitmapV068;
    private byte[]? _editorLayerStrokePixelsV068;
    private int _editorLayerStrokeStrideV068;

    private readonly Stack<EditorLayerStateV068> _editorLayerUndoV068 = new();
    private readonly Stack<EditorLayerStateV068> _editorLayerRedoV068 = new();
    private const int EditorLayerHistoryLimitV068 = 24;

    private Grid? _editorRulerGridV068;
    private Canvas? _editorHorizontalRulerV068;
    private Canvas? _editorVerticalRulerV068;
    private RowDefinition? _editorHorizontalRulerRowV068;
    private ColumnDefinition? _editorVerticalRulerColumnV068;
    private FrameworkElement? _editorRulerContentV068;
    private bool _editorRulersVisibleV068 = true;

    private bool _editorSidebarAutoCollapsedV068;
    private GridLength _editorSidebarWidthBeforeEditorV068;

    private void ConfigureAdvancedImageLayersV068(Grid editorBody)
    {
        if (_editorAdvancedLayersV068Initialized || _editorComposition is null)
            return;

        _editorAdvancedLayersV068Initialized = true;
        ConfigureLayerPaintPanelV068(editorBody);
        ConfigureLayerEditorOverlaysV068();
        ConfigureLayerPointerInteractionV068();
        ConfigureEditorRulersV068();

        if (_editorSnapCheckCanary is not null)
        {
            _editorSnapCheckCanary.Content = "Snap image and chat layers; show alignment guides";
            _editorSnapCheckCanary.ToolTip =
                "Snap image and chat layers to the base image edges and show guides while they are moved or resized.";
        }

        _editorComposition.SizeChanged += (_, _) =>
        {
            RefreshEditorRulersV068();
            RefreshSelectedLayerAdornerV068();
        };
    }

    private Expander CreateEditorSidebarExpanderV068(string title, UIElement content, bool expanded)
    {
        var expander = new Expander
        {
            IsExpanded = expanded,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = new TextBlock
            {
                Text = title,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("MutedText")
            },
            Content = content
        };
        expander.Expanded += (_, _) => content.Visibility = Visibility.Visible;
        expander.Collapsed += (_, _) => content.Visibility = Visibility.Collapsed;
        return expander;
    }

    private void OpenFilterAdjustmentsV068()
    {
        PrepareEditorFiltersPreservingSelectionV067();
        if (_editorFilterAdjustmentsExpanderV068 is not null)
        {
            _editorFilterAdjustmentsExpanderV068.IsExpanded = true;
            _editorFilterAdjustmentsExpanderV068.BringIntoView();
        }
        RefreshSelectionHighlightV067();
    }

    private void RemoveEditorRailButtonV068(string key)
    {
        if (!_editorToolButtons.TryGetValue(key, out Button? button))
            return;
        if (button.Parent is Panel parent)
            parent.Children.Remove(button);
        _editorToolButtons.Remove(key);
    }

    private void ConfigureLayerPaintPanelV068(Grid editorBody)
    {
        _editorToolPanels["layer-paint"] = BuildLayerPaintPanelV068();

        Border? rail = editorBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        if (rail is null)
            return;

        StackPanel? tools = FindVisualChildrenCanary<StackPanel>(rail)
            .FirstOrDefault(stack => stack.Children.OfType<Button>().Any(button =>
                button.ToolTip?.ToString()?.Contains("Image & Canvas", StringComparison.OrdinalIgnoreCase) == true));
        if (tools is null)
            return;

        var button = CreateCanaryRailButtonV2(
            "✎",
            "Layer Paint & Erase — paint directly onto or erase pixels from the selected image layer.",
            "layer-paint",
            "Segoe UI Symbol",
            16);
        Button? imageButton = tools.Children.OfType<Button>().FirstOrDefault(existing =>
            existing.ToolTip?.ToString()?.Contains("Image & Canvas", StringComparison.OrdinalIgnoreCase) == true);
        int index = imageButton is null ? tools.Children.Count : tools.Children.IndexOf(imageButton) + 1;
        tools.Children.Insert(index, button);
    }

    private FrameworkElement BuildLayerPaintPanelV068()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Paint and Erase modify only the selected added image layer. Erasing makes pixels transparent so another image underneath can blend through."));

        var tools = new WrapPanel();
        _editorLayerPaintToolV068 = CreateSmallEditorButton("Paint", (_, _) => SetLayerPixelToolV068(EditorLayerPixelToolV068.Paint));
        _editorLayerEraseToolV068 = CreateSmallEditorButton("Erase", (_, _) => SetLayerPixelToolV068(EditorLayerPixelToolV068.Erase));
        tools.Children.Add(_editorLayerPaintToolV068);
        tools.Children.Add(_editorLayerEraseToolV068);
        content.Children.Add(tools);

        _editorLayerPaintColorV068 = new ComboBox { Height = 32 };
        foreach (string color in new[] { "White", "Black", "Red", "Yellow", "Green", "Blue", "Purple", "Orange" })
            _editorLayerPaintColorV068.Items.Add(color);
        SetEditorComboSelection(_editorLayerPaintColorV068, _settings.Editor.PaintColor, "White");
        content.Children.Add(CreateEditorField("Paint color", _editorLayerPaintColorV068));

        var brush = CreateEditorV041Slider("Brush size", 1, 160, Math.Clamp(_settings.Editor.BrushSize, 1, 160), 1);
        _editorLayerBrushSizeV068 = brush.Slider;
        content.Children.Add(brush.Panel);

        var history = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        history.Children.Add(CreateSmallEditorButton("Undo Layer", (_, _) => UndoLayerEditV068()));
        history.Children.Add(CreateSmallEditorButton("Redo Layer", (_, _) => RedoLayerEditV068()));
        content.Children.Add(history);
        content.Children.Add(EditorSubtleNote(
            "Tip: use a soft sequence of small eraser strokes along an edge for cleaner compositing. Layer edits are saved inside .afterlineproj projects."));

        SetLayerPixelToolV068(EditorLayerPixelToolV068.Paint);
        return WrapEditorToolPanel(content);
    }

    private void SetLayerPixelToolV068(EditorLayerPixelToolV068 tool)
    {
        _editorLayerPixelToolV068 = tool;
        Brush raised = (Brush)FindResource("Raised");
        Brush accent = (Brush)FindResource("Accent");
        if (_editorLayerPaintToolV068 is not null)
            _editorLayerPaintToolV068.Background = tool == EditorLayerPixelToolV068.Paint ? accent : raised;
        if (_editorLayerEraseToolV068 is not null)
            _editorLayerEraseToolV068.Background = tool == EditorLayerPixelToolV068.Erase ? accent : raised;
        SetEditorStatus(tool == EditorLayerPixelToolV068.Paint
            ? "Layer paint active — select an unlocked image layer and drag on it."
            : "Layer eraser active — drag over an unlocked image layer to reveal layers beneath it.");
    }

    private void ConfigureLayerEditorOverlaysV068()
    {
        Grid? host = _editorGuideHostCanary;
        if (host is null)
            return;

        _editorLayerSelectionOutlineV068 = new Rectangle
        {
            Fill = Brushes.Transparent,
            Stroke = (Brush)FindResource("Accent"),
            StrokeThickness = 0.75,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_editorLayerSelectionOutlineV068, 20);
        host.Children.Add(_editorLayerSelectionOutlineV068);

        foreach (EditorLayerResizeHandleV071 handle in Enum.GetValues<EditorLayerResizeHandleV071>())
        {
            bool horizontalEdge = handle is EditorLayerResizeHandleV071.North or EditorLayerResizeHandleV071.South;
            bool verticalEdge = handle is EditorLayerResizeHandleV071.East or EditorLayerResizeHandleV071.West;
            var thumb = new Thumb
            {
                Width = horizontalEdge ? 20 : verticalEdge ? 7 : 9,
                Height = verticalEdge ? 20 : horizontalEdge ? 7 : 9,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = ResizeCursorV071(handle),
                Background = horizontalEdge || verticalEdge
                    ? Brushes.Transparent
                    : (Brush)FindResource("Accent"),
                BorderBrush = horizontalEdge || verticalEdge ? Brushes.Transparent : Brushes.White,
                BorderThickness = horizontalEdge || verticalEdge ? new Thickness(0) : new Thickness(0.75),
                Tag = handle,
                ToolTip = "Drag to resize. Corners preserve proportions; hold Shift for a free transform."
            };
            thumb.DragStarted += LayerResizeStartedV068;
            thumb.DragDelta += LayerResizeDeltaV068;
            thumb.DragCompleted += LayerResizeCompletedV068;
            Panel.SetZIndex(thumb, 22);
            host.Children.Add(thumb);
            _editorLayerResizeThumbsV071[handle] = thumb;
        }

        _editorLayerLockBadgeV068 = new Button
        {
            Content = "🔒",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 12,
            Width = 26,
            Height = 24,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            ToolTip = "This layer is locked. Click to unlock it."
        };
        _editorLayerLockBadgeV068.Click += (_, e) =>
        {
            e.Handled = true;
            EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
            if (layer is null || !layer.IsLocked) return;
            PushLayerEditHistoryV068(layer, "layer lock");
            layer.IsLocked = false;
            RefreshLayerListV067(layer);
            RefreshSelectedLayerAdornerV068();
            SetEditorStatus($"Unlocked image layer ‘{layer.Name}’.");
        };
        Panel.SetZIndex(_editorLayerLockBadgeV068, 23);
        host.Children.Add(_editorLayerLockBadgeV068);
        ApplyPasteboardOffsetToEditorOverlaysV078();
        RefreshSelectedLayerAdornerV068();
    }

    private void RefreshSelectedLayerAdornerV068()
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        bool show = layer is not null && layer.IsVisible && _editorPage?.Visibility == Visibility.Visible;

        if (_editorLayerSelectionOutlineV068 is not null)
        {
            _editorLayerSelectionOutlineV068.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                _editorLayerSelectionOutlineV068.Width = layer!.Width;
                _editorLayerSelectionOutlineV068.Height = layer.Height;
                _editorLayerSelectionOutlineV068.Margin = new Thickness(layer.X, layer.Y, 0, 0);
                _editorLayerSelectionOutlineV068.StrokeThickness =
                    0.85 / Math.Max(0.1, _editorZoomScale);
                _editorLayerSelectionOutlineV068.Stroke = layer.IsLocked
                    ? (Brush)FindResource("MutedText")
                    : (Brush)FindResource("Accent");
            }
        }

        foreach ((EditorLayerResizeHandleV071 handle, Thumb thumb) in _editorLayerResizeThumbsV071)
        {
            thumb.Visibility = show && layer is not null && !layer.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (show && layer is not null)
                PositionLayerResizeThumbV071(thumb, handle, layer);
        }

        if (_editorLayerLockBadgeV068 is not null)
        {
            _editorLayerLockBadgeV068.Visibility = show && layer is not null && layer.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (show && layer is not null)
                _editorLayerLockBadgeV068.Margin = new Thickness(
                    layer.X + layer.Width - 28,
                    layer.Y + 3,
                    0,
                    0);
        }
    }

    private void ConfigureLayerPointerInteractionV068()
    {
        FrameworkElement? host = _editorGuideHostCanary ?? _editorComposition;
        if (host is null)
            return;

        host.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(LayerPointerDownV068), true);
        host.AddHandler(Mouse.PreviewMouseMoveEvent,
            new MouseEventHandler(LayerPointerMoveV068), true);
        host.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(LayerPointerUpV068), true);
        host.AddHandler(UIElement.PreviewMouseRightButtonDownEvent,
            new MouseButtonEventHandler(LayerPointerRightDownV068), true);
        host.LostMouseCapture += (_, _) => EndLayerPointerInteractionV068();
    }

    private void LayerPointerDownV068(object sender, MouseButtonEventArgs e)
    {
        if (_editorComposition is null || _editorSelectionToolCanary != CanarySelectionTool.None)
            return;
        if (FindVisualParentCanary<Thumb>(e.OriginalSource as DependencyObject) is not null ||
            FindVisualParentCanary<Button>(e.OriginalSource as DependencyObject) is not null)
            return;

        Point point = e.GetPosition(_editorComposition);
        bool pixelEditing = string.Equals(_editorActiveToolKey, "layer-paint", StringComparison.OrdinalIgnoreCase);
        if (pixelEditing)
        {
            EditorImageLayerV067? selected = _editorSelectedImageLayerV067;
            if (selected is null || !selected.IsVisible || selected.IsLocked || !LayerBoundsV068(selected).Contains(point))
                return;

            PushLayerEditHistoryV068(selected, _editorLayerPixelToolV068 == EditorLayerPixelToolV068.Erase
                ? "layer erasing"
                : "layer painting");
            BeginLayerPixelStrokeV068(selected, point);
            (sender as UIElement)?.CaptureMouse();
            e.Handled = true;
            return;
        }

        EditorImageLayerV067? hit = HitTestImageLayerV068(point);
        if (hit is null)
            return;

        SelectImageLayerV068(hit);
        if (hit.IsLocked)
        {
            SetEditorStatus($"Image layer ‘{hit.Name}’ is locked. Click its lock badge or use right-click to unlock it.");
            e.Handled = true;
            return;
        }

        PushLayerEditHistoryV068(hit, "layer position");
        _editorLayerDraggingV068 = true;
        _editorLayerDragTargetV068 = hit;
        _editorLayerDragStartV068 = point;
        _editorLayerDragStartXV068 = hit.X;
        _editorLayerDragStartYV068 = hit.Y;
        if (sender is FrameworkElement element)
        {
            element.Cursor = Cursors.SizeAll;
            element.CaptureMouse();
        }
        e.Handled = true;
    }

    private void LayerPointerMoveV068(object sender, MouseEventArgs e)
    {
        if (_editorComposition is null)
            return;
        Point point = e.GetPosition(_editorComposition);

        if (_editorLayerStrokeActiveV068)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndLayerPointerInteractionV068();
                return;
            }
            ContinueLayerPixelStrokeV068(point);
            e.Handled = true;
            return;
        }

        if (!_editorLayerDraggingV068 || _editorLayerDragTargetV068 is null)
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndLayerPointerInteractionV068();
            return;
        }

        EditorImageLayerV067 layer = _editorLayerDragTargetV068;
        double x = _editorLayerDragStartXV068 + point.X - _editorLayerDragStartV068.X;
        double y = _editorLayerDragStartYV068 + point.Y - _editorLayerDragStartV068.Y;
        (x, y, double? guideX, double? guideY) = SnapImageLayerV068(layer, x, y);
        layer.X = x;
        layer.Y = y;
        UpdateImageLayerVisualV067(layer);
        EnsureLayerCanvasExtentV067();
        RefreshSelectedLayerAdornerV068();
        ShowSnapGuidesCanary(guideX, guideY);
        e.Handled = true;
    }

    private void LayerPointerUpV068(object sender, MouseButtonEventArgs e)
    {
        if (!_editorLayerDraggingV068 && !_editorLayerStrokeActiveV068)
            return;
        EndLayerPointerInteractionV068();
        e.Handled = true;
    }

    private void EndLayerPointerInteractionV068()
    {
        bool wasStroke = _editorLayerStrokeActiveV068;
        _editorLayerDraggingV068 = false;
        _editorLayerDragTargetV068 = null;
        _editorLayerStrokeActiveV068 = false;
        _editorLayerStrokeBitmapV068 = null;
        _editorLayerStrokePixelsV068 = null;
        _editorLayerStrokeStrideV068 = 0;

        FrameworkElement? host = _editorGuideHostCanary ?? _editorComposition;
        if (host?.IsMouseCaptured == true)
            host.ReleaseMouseCapture();
        if (host is not null)
            host.Cursor = Cursors.Arrow;

        if (wasStroke && _editorSelectedImageLayerV067 is not null)
            RefreshLayerListV067(_editorSelectedImageLayerV067);
        RefreshSelectedLayerAdornerV068();
        _editorSnapGuideTimerCanary.Stop();
        _editorSnapGuideTimerCanary.Start();
    }

    private void LayerPointerRightDownV068(object sender, MouseButtonEventArgs e)
    {
        if (_editorComposition is null || _editorSelectionToolCanary != CanarySelectionTool.None)
            return;
        Point point = e.GetPosition(_editorComposition);
        EditorImageLayerV067? hit = HitTestImageLayerV068(point);
        if (hit is null)
            return;

        SelectImageLayerV068(hit);
        ContextMenu menu = CreateImageLayerContextMenuV068(hit);
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu CreateImageLayerContextMenuV068(EditorImageLayerV067 layer)
    {
        ContextMenu menu = CreateAfterlineContextMenu();
        menu.Items.Add(CreateAfterlineContextMenuItem(layer.IsLocked ? "Unlock Layer" : "Lock Layer", (_, _) =>
        {
            PushLayerEditHistoryV068(layer, "layer lock");
            layer.IsLocked = !layer.IsLocked;
            RefreshLayerListV067(layer);
            RefreshSelectedLayerAdornerV068();
            SetEditorStatus($"{(layer.IsLocked ? "Locked" : "Unlocked")} image layer ‘{layer.Name}’.");
        }));
        menu.Items.Add(CreateAfterlineContextMenuSeparator());
        menu.Items.Add(CreateAfterlineContextMenuItem("Set Exact Size…", (_, _) => SetExactLayerSizeV068(layer)));
        menu.Items.Add(CreateAfterlineContextMenuItem("Reset to Original Size", (_, _) =>
        {
            if (layer.IsLocked) return;
            PushLayerEditHistoryV068(layer, "layer size");
            layer.Width = Math.Max(1, layer.Bitmap.PixelWidth);
            layer.Height = Math.Max(1, layer.Bitmap.PixelHeight);
            UpdateImageLayerVisualV067(layer);
            EnsureLayerCanvasExtentV067();
            RefreshSelectedLayerAdornerV068();
        }));
        return menu;
    }

    private void SetExactLayerSizeV068(EditorImageLayerV067 layer)
    {
        if (layer.IsLocked)
        {
            SetEditorStatus("Unlock this image layer before resizing it.");
            return;
        }
        var dialog = new LayerSizeWindowV068(this, layer.Width, layer.Height);
        if (dialog.ShowDialog() != true)
            return;

        PushLayerEditHistoryV068(layer, "layer size");
        layer.Width = dialog.LayerWidth;
        layer.Height = dialog.LayerHeight;
        UpdateImageLayerVisualV067(layer);
        EnsureLayerCanvasExtentV067();
        RefreshSelectedLayerAdornerV068();
        SetEditorStatus($"Resized ‘{layer.Name}’ to {layer.Width:0} × {layer.Height:0} px.");
    }

    private void LayerResizeStartedV068(object sender, DragStartedEventArgs e)
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        if (layer is null || layer.IsLocked)
            return;
        PushLayerEditHistoryV068(layer, "layer size");
        if (sender is Thumb { Tag: EditorLayerResizeHandleV071 handle })
            _editorLayerResizeHandleV071 = handle;
        _editorLayerResizeStartXV071 = layer.X;
        _editorLayerResizeStartYV071 = layer.Y;
        _editorLayerResizeStartWidthV068 = layer.Width;
        _editorLayerResizeStartHeightV068 = layer.Height;
        _editorLayerResizePointerStartV079 = _editorComposition is null
            ? new Point(layer.X + layer.Width, layer.Y + layer.Height)
            : Mouse.GetPosition(_editorComposition);
    }

    private void LayerResizeDeltaV068(object sender, DragDeltaEventArgs e)
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        if (layer is null || layer.IsLocked)
            return;

        if (_editorComposition is null)
            return;
        double zoom = Math.Max(0.1, _editorZoomScale);
        Point pointer = Mouse.GetPosition(_editorComposition);
        Vector pointerDelta = pointer - _editorLayerResizePointerStartV079;
        bool corner = _editorLayerResizeHandleV071 is
            EditorLayerResizeHandleV071.NorthWest or
            EditorLayerResizeHandleV071.NorthEast or
            EditorLayerResizeHandleV071.SouthEast or
            EditorLayerResizeHandleV071.SouthWest;
        (double baseWidth, double baseHeight) = GetBaseImageBoundsV068();
        (Rect bounds, double? guideX, double? guideY) = CalculateLayerResizeBoundsV071(
            new Rect(
                _editorLayerResizeStartXV071,
                _editorLayerResizeStartYV071,
                _editorLayerResizeStartWidthV068,
                _editorLayerResizeStartHeightV068),
            _editorLayerResizeHandleV071,
            pointerDelta.X,
            pointerDelta.Y,
            baseWidth,
            baseHeight,
            _editorSnapCheckCanary?.IsChecked == true,
            10 / zoom,
            preserveAspectRatio: corner && (Keyboard.Modifiers & ModifierKeys.Shift) == 0);

        layer.X = bounds.X;
        layer.Y = bounds.Y;
        layer.Width = bounds.Width;
        layer.Height = bounds.Height;
        UpdateImageLayerVisualV067(layer);
        EnsureLayerCanvasExtentV067();
        RefreshSelectedLayerAdornerV068();
        ShowSnapGuidesCanary(guideX, guideY);
    }

    private void LayerResizeCompletedV068(object sender, DragCompletedEventArgs e)
    {
        if (_editorSelectedImageLayerV067 is not null)
            SetEditorStatus($"Resized ‘{_editorSelectedImageLayerV067.Name}’ to {_editorSelectedImageLayerV067.Width:0} × {_editorSelectedImageLayerV067.Height:0} px.");
        _editorSnapGuideTimerCanary.Stop();
        _editorSnapGuideTimerCanary.Start();
    }

    private static Cursor ResizeCursorV071(EditorLayerResizeHandleV071 handle)
        => handle switch
        {
            EditorLayerResizeHandleV071.North or EditorLayerResizeHandleV071.South => Cursors.SizeNS,
            EditorLayerResizeHandleV071.East or EditorLayerResizeHandleV071.West => Cursors.SizeWE,
            EditorLayerResizeHandleV071.NorthEast or EditorLayerResizeHandleV071.SouthWest => Cursors.SizeNESW,
            _ => Cursors.SizeNWSE
        };

    private void PositionLayerResizeThumbV071(
        Thumb thumb,
        EditorLayerResizeHandleV071 handle,
        EditorImageLayerV067 layer)
    {
        bool horizontalEdge = handle is EditorLayerResizeHandleV071.North or EditorLayerResizeHandleV071.South;
        bool verticalEdge = handle is EditorLayerResizeHandleV071.East or EditorLayerResizeHandleV071.West;
        double zoom = Math.Max(0.1, _editorZoomScale);
        double cornerSize = 8 / zoom;
        double edgeThickness = 6 / zoom;
        if (horizontalEdge)
        {
            thumb.Width = Math.Max(16 / zoom, layer.Width - cornerSize * 2);
            thumb.Height = edgeThickness;
        }
        else if (verticalEdge)
        {
            thumb.Width = edgeThickness;
            thumb.Height = Math.Max(16 / zoom, layer.Height - cornerSize * 2);
        }
        else
        {
            thumb.Width = cornerSize;
            thumb.Height = cornerSize;
            thumb.BorderThickness = new Thickness(0.75 / zoom);
        }

        double left = handle switch
        {
            EditorLayerResizeHandleV071.NorthWest or EditorLayerResizeHandleV071.West or EditorLayerResizeHandleV071.SouthWest
                => layer.X - thumb.Width / 2,
            EditorLayerResizeHandleV071.North or EditorLayerResizeHandleV071.South
                => layer.X + layer.Width / 2 - thumb.Width / 2,
            _ => layer.X + layer.Width - thumb.Width / 2
        };
        double top = handle switch
        {
            EditorLayerResizeHandleV071.NorthWest or EditorLayerResizeHandleV071.North or EditorLayerResizeHandleV071.NorthEast
                => layer.Y - thumb.Height / 2,
            EditorLayerResizeHandleV071.West or EditorLayerResizeHandleV071.East
                => layer.Y + layer.Height / 2 - thumb.Height / 2,
            _ => layer.Y + layer.Height - thumb.Height / 2
        };
        thumb.Margin = new Thickness(left, top, 0, 0);
    }

    private static (Rect Bounds, double? GuideX, double? GuideY) CalculateLayerResizeBoundsV071(
        Rect start,
        EditorLayerResizeHandleV071 handle,
        double deltaX,
        double deltaY,
        double baseWidth,
        double baseHeight,
        bool snap,
        double snapThreshold,
        bool preserveAspectRatio = false)
    {
        const double minimum = 16;
        bool moveLeft = handle is EditorLayerResizeHandleV071.NorthWest or EditorLayerResizeHandleV071.West or EditorLayerResizeHandleV071.SouthWest;
        bool moveRight = handle is EditorLayerResizeHandleV071.NorthEast or EditorLayerResizeHandleV071.East or EditorLayerResizeHandleV071.SouthEast;
        bool moveTop = handle is EditorLayerResizeHandleV071.NorthWest or EditorLayerResizeHandleV071.North or EditorLayerResizeHandleV071.NorthEast;
        bool moveBottom = handle is EditorLayerResizeHandleV071.SouthWest or EditorLayerResizeHandleV071.South or EditorLayerResizeHandleV071.SouthEast;

        if (preserveAspectRatio &&
            (moveLeft || moveRight) &&
            (moveTop || moveBottom))
        {
            double widthDelta = moveLeft ? -deltaX : deltaX;
            double heightDelta = moveTop ? -deltaY : deltaY;
            double denominator = start.Width * start.Width + start.Height * start.Height;
            double projectedScale = denominator <= 0
                ? 1
                : 1 + (widthDelta * start.Width + heightDelta * start.Height) / denominator;
            double minimumScale = Math.Max(minimum / start.Width, minimum / start.Height);
            double scale = Math.Max(minimumScale, projectedScale);
            double proportionalWidth = start.Width * scale;
            double proportionalHeight = start.Height * scale;
            deltaX = moveLeft
                ? start.Width - proportionalWidth
                : proportionalWidth - start.Width;
            deltaY = moveTop
                ? start.Height - proportionalHeight
                : proportionalHeight - start.Height;
        }

        double left = start.Left;
        double right = start.Right;
        double top = start.Top;
        double bottom = start.Bottom;
        if (moveLeft) left = Math.Min(start.Left + deltaX, start.Right - minimum);
        if (moveRight) right = Math.Max(start.Left + minimum, start.Right + deltaX);
        if (moveTop) top = Math.Min(start.Top + deltaY, start.Bottom - minimum);
        if (moveBottom) bottom = Math.Max(start.Top + minimum, start.Bottom + deltaY);

        double? guideX = null;
        double? guideY = null;
        if (snap)
        {
            if (moveLeft && Math.Abs(left) <= snapThreshold)
            {
                left = 0;
                guideX = 0;
            }
            else if (moveRight && Math.Abs(right - baseWidth) <= snapThreshold)
            {
                right = Math.Max(left + minimum, baseWidth);
                guideX = baseWidth;
            }

            if (moveTop && Math.Abs(top) <= snapThreshold)
            {
                top = 0;
                guideY = 0;
            }
            else if (moveBottom && Math.Abs(bottom - baseHeight) <= snapThreshold)
            {
                bottom = Math.Max(top + minimum, baseHeight);
                guideY = baseHeight;
            }
        }

        return (new Rect(left, top, Math.Max(minimum, right - left), Math.Max(minimum, bottom - top)), guideX, guideY);
    }

    private (double X, double Y, double? GuideX, double? GuideY) SnapImageLayerV068(
        EditorImageLayerV067 layer,
        double x,
        double y)
    {
        if (_editorSnapCheckCanary?.IsChecked != true)
            return (x, y, null, null);

        (double baseWidth, double baseHeight) = GetBaseImageBoundsV068();
        double threshold = 10 / Math.Max(0.1, _editorZoomScale);
        double? guideX = null;
        double? guideY = null;

        double leftDistance = Math.Abs(x);
        double rightDistance = Math.Abs(x + layer.Width - baseWidth);
        if (leftDistance <= threshold || rightDistance <= threshold)
        {
            if (leftDistance <= rightDistance)
            {
                x = 0;
                guideX = 0;
            }
            else if (baseWidth >= layer.Width)
            {
                x = baseWidth - layer.Width;
                guideX = baseWidth;
            }
        }

        double topDistance = Math.Abs(y);
        double bottomDistance = Math.Abs(y + layer.Height - baseHeight);
        if (topDistance <= threshold || bottomDistance <= threshold)
        {
            if (topDistance <= bottomDistance)
            {
                y = 0;
                guideY = 0;
            }
            else if (baseHeight >= layer.Height)
            {
                y = baseHeight - layer.Height;
                guideY = baseHeight;
            }
        }
        return (x, y, guideX, guideY);
    }

    private (double Width, double Height) GetBaseImageBoundsV068()
    {
        if (_editorBaseOriginal is not null)
            return (_editorBaseOriginal.PixelWidth, _editorBaseOriginal.PixelHeight);
        if (_editorComposition is not null)
            return (Math.Max(1, _editorComposition.Width), Math.Max(1, _editorComposition.Height));
        return (1, 1);
    }

    private EditorImageLayerV067? HitTestImageLayerV068(Point point)
        => _editorImageLayersV067
            .AsEnumerable()
            .Reverse()
            .FirstOrDefault(layer => layer.IsVisible && LayerBoundsV068(layer).Contains(point));

    private static Rect LayerBoundsV068(EditorImageLayerV067 layer)
        => new(layer.X, layer.Y, Math.Max(1, layer.Width), Math.Max(1, layer.Height));

    private void SelectImageLayerV068(EditorImageLayerV067 layer)
    {
        EditorImageLayerV067? previous = _editorSelectedImageLayerV067;
        _editorSelectedImageLayerV067 = layer;
        if (!ReferenceEquals(previous, layer))
            EditorFilterTargetSelectionChangedV071();
        if (_editorLayerListV067 is not null)
        {
            foreach (ListBoxItem item in _editorLayerListV067.Items.OfType<ListBoxItem>())
            {
                if (!ReferenceEquals(item.Tag, layer)) continue;
                _editorLayerListV067.SelectedItem = item;
                item.BringIntoView();
                break;
            }
        }
        SyncLayerControlsV067();
        RefreshSelectedLayerAdornerV068();
    }

    private void BeginLayerPixelStrokeV068(EditorImageLayerV067 layer, Point point)
    {
        WriteableBitmap bitmap = EnsureEditableLayerBitmapV068(layer);
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        _editorLayerStrokeActiveV068 = true;
        _editorLayerStrokeBitmapV068 = bitmap;
        _editorLayerStrokePixelsV068 = pixels;
        _editorLayerStrokeStrideV068 = stride;
        _editorLayerStrokeLastPointV068 = point;
        ApplyLayerBrushStampV068(layer, point);
    }

    private void ContinueLayerPixelStrokeV068(Point point)
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        if (layer is null || _editorLayerStrokeBitmapV068 is null || _editorLayerStrokePixelsV068 is null)
            return;

        double brush = Math.Max(1, _editorLayerBrushSizeV068?.Value ?? _settings.Editor.BrushSize);
        double distance = (point - _editorLayerStrokeLastPointV068).Length;
        int steps = Math.Max(1, (int)Math.Ceiling(distance / Math.Max(1, brush * 0.22)));
        Point start = _editorLayerStrokeLastPointV068;
        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            ApplyLayerBrushStampV068(layer, new Point(
                start.X + (point.X - start.X) * t,
                start.Y + (point.Y - start.Y) * t));
        }
        _editorLayerStrokeLastPointV068 = point;
    }

    private WriteableBitmap EnsureEditableLayerBitmapV068(EditorImageLayerV067 layer)
    {
        if (layer.Bitmap is WriteableBitmap writable && writable.Format == PixelFormats.Pbgra32)
            return writable;

        BitmapSource source = layer.Bitmap;
        if (source.Format != PixelFormats.Pbgra32)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            converted.Freeze();
            source = converted;
        }

        var bitmap = new WriteableBitmap(source);
        layer.Bitmap = bitmap;
        layer.Image.Source = bitmap;
        return bitmap;
    }

    private void ApplyLayerBrushStampV068(EditorImageLayerV067 layer, Point canvasPoint)
    {
        WriteableBitmap? bitmap = _editorLayerStrokeBitmapV068;
        byte[]? pixels = _editorLayerStrokePixelsV068;
        if (bitmap is null || pixels is null || layer.Width <= 0 || layer.Height <= 0)
            return;

        double px = (canvasPoint.X - layer.X) * bitmap.PixelWidth / layer.Width;
        double py = (canvasPoint.Y - layer.Y) * bitmap.PixelHeight / layer.Height;
        double canvasBrush = Math.Max(1, _editorLayerBrushSizeV068?.Value ?? _settings.Editor.BrushSize);
        double radiusX = Math.Max(0.6, canvasBrush * 0.5 * bitmap.PixelWidth / layer.Width);
        double radiusY = Math.Max(0.6, canvasBrush * 0.5 * bitmap.PixelHeight / layer.Height);

        int minX = Math.Max(0, (int)Math.Floor(px - radiusX - 1));
        int maxX = Math.Min(bitmap.PixelWidth - 1, (int)Math.Ceiling(px + radiusX + 1));
        int minY = Math.Max(0, (int)Math.Floor(py - radiusY - 1));
        int maxY = Math.Min(bitmap.PixelHeight - 1, (int)Math.Ceiling(py + radiusY + 1));
        if (minX > maxX || minY > maxY)
            return;

        Color color = ResolveLayerPaintColorV068();
        bool erase = _editorLayerPixelToolV068 == EditorLayerPixelToolV068.Erase;
        for (int y = minY; y <= maxY; y++)
        {
            double dy = (y + 0.5 - py) / radiusY;
            for (int x = minX; x <= maxX; x++)
            {
                double dx = (x + 0.5 - px) / radiusX;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double coverage = Math.Clamp((1.08 - distance) * 7.0, 0, 1);
                if (coverage <= 0) continue;

                int index = y * _editorLayerStrokeStrideV068 + x * 4;
                double keep = 1 - coverage;
                if (erase)
                {
                    pixels[index] = (byte)Math.Round(pixels[index] * keep);
                    pixels[index + 1] = (byte)Math.Round(pixels[index + 1] * keep);
                    pixels[index + 2] = (byte)Math.Round(pixels[index + 2] * keep);
                    pixels[index + 3] = (byte)Math.Round(pixels[index + 3] * keep);
                }
                else
                {
                    pixels[index] = (byte)Math.Clamp(Math.Round(color.B * coverage + pixels[index] * keep), 0, 255);
                    pixels[index + 1] = (byte)Math.Clamp(Math.Round(color.G * coverage + pixels[index + 1] * keep), 0, 255);
                    pixels[index + 2] = (byte)Math.Clamp(Math.Round(color.R * coverage + pixels[index + 2] * keep), 0, 255);
                    pixels[index + 3] = (byte)Math.Clamp(Math.Round(255 * coverage + pixels[index + 3] * keep), 0, 255);
                }
            }
        }

        bitmap.WritePixels(
            new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
            pixels,
            _editorLayerStrokeStrideV068,
            minY * _editorLayerStrokeStrideV068 + minX * 4);
    }

    private Color ResolveLayerPaintColorV068()
        => (_editorLayerPaintColorV068?.SelectedItem?.ToString() ?? "White") switch
        {
            "Black" => Colors.Black,
            "Red" => Color.FromRgb(0xFF, 0x3B, 0x30),
            "Yellow" => Color.FromRgb(0xFF, 0xF3, 0x00),
            "Green" => Color.FromRgb(0x20, 0xE8, 0x5A),
            "Blue" => Color.FromRgb(0x16, 0x9B, 0xFF),
            "Purple" => Color.FromRgb(0xC2, 0xA2, 0xDA),
            "Orange" => Color.FromRgb(0xFF, 0xA5, 0x1F),
            _ => Colors.White
        };

    private void PushLayerEditHistoryV068(EditorImageLayerV067 layer, string description)
    {
        _editorLayerUndoV068.Push(CaptureLayerStateV068(layer, description));
        while (_editorLayerUndoV068.Count > EditorLayerHistoryLimitV068)
        {
            EditorLayerStateV068[] keep = _editorLayerUndoV068.Take(EditorLayerHistoryLimitV068).Reverse().ToArray();
            _editorLayerUndoV068.Clear();
            foreach (EditorLayerStateV068 entry in keep)
                _editorLayerUndoV068.Push(entry);
        }
        _editorLayerRedoV068.Clear();
    }

    private EditorLayerStateV068 CaptureLayerStateV068(EditorImageLayerV067 layer, string description)
        => new(
            layer.Id,
            CloneBitmapCanary(layer.Bitmap),
            layer.X,
            layer.Y,
            layer.Width,
            layer.Height,
            layer.Opacity,
            layer.IsVisible,
            layer.IsLocked,
            description);

    private void UndoLayerEditV068()
    {
        if (_editorLayerUndoV068.Count == 0)
        {
            SetEditorStatus("Nothing to undo for image layers.");
            return;
        }
        EditorLayerStateV068 previous = _editorLayerUndoV068.Pop();
        EditorImageLayerV067? layer = _editorImageLayersV067.FirstOrDefault(item => item.Id == previous.LayerId);
        if (layer is null)
            return;
        _editorLayerRedoV068.Push(CaptureLayerStateV068(layer, "redo layer edit"));
        RestoreLayerStateV068(layer, previous);
        SetEditorStatus($"Undid {previous.Description} on ‘{layer.Name}’.");
    }

    private void RedoLayerEditV068()
    {
        if (_editorLayerRedoV068.Count == 0)
        {
            SetEditorStatus("Nothing to redo for image layers.");
            return;
        }
        EditorLayerStateV068 next = _editorLayerRedoV068.Pop();
        EditorImageLayerV067? layer = _editorImageLayersV067.FirstOrDefault(item => item.Id == next.LayerId);
        if (layer is null)
            return;
        _editorLayerUndoV068.Push(CaptureLayerStateV068(layer, "undo layer edit"));
        RestoreLayerStateV068(layer, next);
        SetEditorStatus($"Redid the last edit on ‘{layer.Name}’.");
    }

    private void RestoreLayerStateV068(EditorImageLayerV067 layer, EditorLayerStateV068 state)
    {
        ResetLayerFilterTargetV071(restoreVisual: false);
        layer.Bitmap = CloneBitmapCanary(state.Bitmap);
        layer.X = state.X;
        layer.Y = state.Y;
        layer.Width = state.Width;
        layer.Height = state.Height;
        layer.Opacity = state.Opacity;
        layer.IsVisible = state.Visible;
        layer.IsLocked = state.Locked;
        UpdateImageLayerVisualV067(layer);
        EnsureLayerCanvasExtentV067();
        RefreshLayerListV067(layer);
        RefreshSelectedLayerAdornerV068();
    }

    private void ClearLayerEditHistoryV068()
    {
        _editorLayerUndoV068.Clear();
        _editorLayerRedoV068.Clear();
    }

    private void ConfigureEditorRulersV068()
    {
        if (_editorZoomHost is null ||
            _editorZoomHost.Child is not FrameworkElement content ||
            _editorPreviewScroll is null ||
            _editorPreviewScroll.Parent is not Grid previewRoot)
            return;

        _editorRulerContentV068 = content;
        int previewRow = Grid.GetRow(_editorPreviewScroll);
        int previewColumn = Grid.GetColumn(_editorPreviewScroll);
        previewRoot.Children.Remove(_editorPreviewScroll);
        _editorPreviewScroll.Content = null;
        _editorPreviewScroll.Padding = new Thickness(0);

        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true
        };
        Grid.SetRow(grid, previewRow);
        Grid.SetColumn(grid, previewColumn);
        _editorRulerGridV068 = grid;
        _editorHorizontalRulerRowV068 = new RowDefinition { Height = new GridLength(24) };
        _editorVerticalRulerColumnV068 = new ColumnDefinition { Width = new GridLength(38) };
        grid.RowDefinitions.Add(_editorHorizontalRulerRowV068);
        // The canvas viewport must consume the remaining preview area. Auto sizing
        // lets the image dictate the ScrollViewer's desired size, which leaves the
        // viewport unbounded and makes Fit calculate against the image itself.
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(_editorVerticalRulerColumnV068);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Brush rulerBackground = TryFindResource("Inset") as Brush
                                ?? TryFindResource("Raised") as Brush
                                ?? Brushes.Transparent;

        var corner = new Border
        {
            Background = rulerBackground,
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
        grid.Children.Add(corner);

        _editorHorizontalRulerV068 = new Canvas
        {
            Height = 24,
            Background = rulerBackground,
            ClipToBounds = true,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(_editorHorizontalRulerV068, 1);
        grid.Children.Add(_editorHorizontalRulerV068);

        _editorVerticalRulerV068 = new Canvas
        {
            Width = 38,
            Background = rulerBackground,
            ClipToBounds = true,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(_editorVerticalRulerV068, 1);
        grid.Children.Add(_editorVerticalRulerV068);

        Grid.SetRow(_editorPreviewScroll, 1);
        Grid.SetColumn(_editorPreviewScroll, 1);
        _editorPreviewScroll.Content = _editorZoomHost;
        _editorPreviewScroll.ScrollChanged += (_, _) => RefreshEditorRulersV068();
        _editorZoomHost.SizeChanged += (_, _) =>
            Dispatcher.BeginInvoke(new Action(RefreshEditorRulersV068));
        grid.Children.Add(_editorPreviewScroll);
        previewRoot.Children.Add(grid);
        RefreshEditorRulersV068();
    }

    private void ToggleEditorRulersV068()
    {
        _editorRulersVisibleV068 = !_editorRulersVisibleV068;
        if (_editorHorizontalRulerRowV068 is not null)
            _editorHorizontalRulerRowV068.Height = _editorRulersVisibleV068 ? new GridLength(24) : new GridLength(0);
        if (_editorVerticalRulerColumnV068 is not null)
            _editorVerticalRulerColumnV068.Width = _editorRulersVisibleV068 ? new GridLength(38) : new GridLength(0);
        if (_editorHorizontalRulerV068 is not null)
            _editorHorizontalRulerV068.Visibility = _editorRulersVisibleV068 ? Visibility.Visible : Visibility.Collapsed;
        if (_editorVerticalRulerV068 is not null)
            _editorVerticalRulerV068.Visibility = _editorRulersVisibleV068 ? Visibility.Visible : Visibility.Collapsed;
        SetEditorStatus($"Canvas rulers {(_editorRulersVisibleV068 ? "shown" : "hidden")} · {_settings.Editor.RulerKeybind} toggles them.");
        if (_editorFitZoom)
            Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private void RefreshEditorRulersV068()
    {
        if (_editorComposition is null || _editorHorizontalRulerV068 is null || _editorVerticalRulerV068 is null)
            return;

        double scale = Math.Max(0.01, _editorZoomScale);
        double visibleWidth = Math.Max(
            1,
            _editorHorizontalRulerV068.ActualWidth > 0
                ? _editorHorizontalRulerV068.ActualWidth
                : _editorPreviewScroll?.ViewportWidth ?? 1);
        double visibleHeight = Math.Max(
            1,
            _editorVerticalRulerV068.ActualHeight > 0
                ? _editorVerticalRulerV068.ActualHeight
                : _editorPreviewScroll?.ViewportHeight ?? 1);
        _editorHorizontalRulerV068.Children.Clear();
        _editorVerticalRulerV068.Children.Clear();

        Brush lineBrush = (Brush)FindResource("MutedText");
        Brush zeroBrush = TryFindResource("Accent") as Brush ?? lineBrush;
        double[] majorCandidates = { 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        double major = majorCandidates.FirstOrDefault(candidate => candidate * scale >= 68);
        if (major <= 0)
            major = 5000;
        double minor = major / 5;

        // Translate the actual composition rather than deriving an origin from the
        // scroll offsets. Fit mode centers the image in the viewport, while manual
        // zoom and scrolling move it; TranslatePoint accounts for all three. This
        // keeps (0, 0) attached to the base image's top-left corner at every zoom.
        Point horizontalOrigin;
        Point verticalOrigin;
        try
        {
            _editorComposition.UpdateLayout();
            horizontalOrigin = _editorComposition.TranslatePoint(new Point(0, 0), _editorHorizontalRulerV068);
            verticalOrigin = _editorComposition.TranslatePoint(new Point(0, 0), _editorVerticalRulerV068);
        }
        catch (InvalidOperationException)
        {
            horizontalOrigin = new Point(-(_editorPreviewScroll?.HorizontalOffset ?? 0), 0);
            verticalOrigin = new Point(0, -(_editorPreviewScroll?.VerticalOffset ?? 0));
        }

        const double rulerLimit = 5000;
        double firstX = Math.Max(-rulerLimit, Math.Floor((-horizontalOrigin.X) / scale / minor) * minor);
        double lastX = Math.Min(rulerLimit, Math.Ceiling((visibleWidth - horizontalOrigin.X) / scale / minor) * minor);
        for (double x = firstX; x <= lastX + 0.01; x += minor)
        {
            double screenX = horizontalOrigin.X + x * scale;
            if (screenX < -1 || screenX > visibleWidth + 1) continue;
            bool isMajor = IsEditorRulerMajorTickV076(x, major);
            bool isZero = Math.Abs(x) < 0.01;
            _editorHorizontalRulerV068.Children.Add(new Line
            {
                X1 = screenX,
                X2 = screenX,
                Y1 = isMajor ? 8 : 15,
                Y2 = 24,
                Stroke = isZero ? zeroBrush : lineBrush,
                StrokeThickness = isZero ? 1.5 : 1,
                Opacity = isMajor ? 0.9 : 0.55
            });
            if (!isMajor) continue;
            var label = new TextBlock
            {
                Text = Math.Abs(x) < 0.01 ? "0" : x.ToString("0"),
                FontSize = 8,
                Foreground = isZero ? zeroBrush : lineBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, screenX + 2);
            Canvas.SetTop(label, 0);
            _editorHorizontalRulerV068.Children.Add(label);
        }

        double firstY = Math.Max(-rulerLimit, Math.Floor((-verticalOrigin.Y) / scale / minor) * minor);
        double lastY = Math.Min(rulerLimit, Math.Ceiling((visibleHeight - verticalOrigin.Y) / scale / minor) * minor);
        for (double y = firstY; y <= lastY + 0.01; y += minor)
        {
            double screenY = verticalOrigin.Y + y * scale;
            if (screenY < -1 || screenY > visibleHeight + 1) continue;
            bool isMajor = IsEditorRulerMajorTickV076(y, major);
            bool isZero = Math.Abs(y) < 0.01;
            _editorVerticalRulerV068.Children.Add(new Line
            {
                X1 = isMajor ? 20 : 29,
                X2 = 38,
                Y1 = screenY,
                Y2 = screenY,
                Stroke = isZero ? zeroBrush : lineBrush,
                StrokeThickness = isZero ? 1.5 : 1,
                Opacity = isMajor ? 0.9 : 0.55
            });
            if (!isMajor) continue;
            var label = new TextBlock
            {
                Text = Math.Abs(y) < 0.01 ? "0" : y.ToString("0"),
                FontSize = 8,
                Foreground = isZero ? zeroBrush : lineBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, 1);
            Canvas.SetTop(label, screenY + 2);
            _editorVerticalRulerV068.Children.Add(label);
        }
    }

    private static bool IsEditorRulerMajorTickV076(double value, double major)
    {
        double remainder = Math.Abs(value % major);
        return remainder < 0.01 || Math.Abs(remainder - major) < 0.01;
    }

    private void ApplyAutomaticEditorSidebarV068(bool editorVisible)
    {
        if (_editorRootLayoutCanary is null || _editorRootLayoutCanary.ColumnDefinitions.Count == 0)
            return;

        ColumnDefinition sidebar = _editorRootLayoutCanary.ColumnDefinitions[0];
        if (editorVisible)
        {
            if (!_editorSidebarAutoCollapsedV068)
            {
                _editorSidebarWidthBeforeEditorV068 = sidebar.Width;
                _editorSidebarAutoCollapsedV068 = true;
            }
            sidebar.Width = new GridLength(0);
        }
        else if (_editorSidebarAutoCollapsedV068)
        {
            sidebar.Width = _editorSidebarWidthBeforeEditorV068;
            _editorSidebarAutoCollapsedV068 = false;
        }
    }
}
