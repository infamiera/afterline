using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private enum CanarySelectionTool
    {
        None,
        Rectangular,
        Lasso,
        Polygonal,
        Object
    }

    private sealed class CanaryChatLayer
    {
        public int Number { get; init; }
        public string Text { get; set; } = string.Empty;
        public double X { get; set; } = 48;
        public double Y { get; set; } = 48;
        public BitmapSource? Bitmap { get; set; }
        public required Image Image { get; init; }
        public override string ToString() => $"Chat {Number}";
    }

    private bool _editorCanaryWorkspaceInitialized;
    private CanarySelectionTool _editorSelectionToolCanary;
    private Canvas? _editorSelectionOverlayCanary;
    private System.Windows.Shapes.Path? _editorSelectionPreviewPathCanary;
    private Image? _editorSelectionBoundaryImageCanary;
    private Canvas? _editorSnapGuideCanvasCanary;
    private bool[]? _editorSelectionMaskCanary;
    private int _editorSelectionWidthCanary;
    private int _editorSelectionHeightCanary;
    private Point _editorSelectionStartCanary;
    private bool _editorSelectionDraggingCanary;
    private readonly List<Point> _editorSelectionPointsCanary = new();
    private Point _editorSelectionHoverCanary;
    private Slider? _editorObjectThresholdSliderCanary;

    private CheckBox? _editorMultipleChatsCheckCanary;
    private CheckBox? _editorSnapCheckCanary;
    private FrameworkElement? _editorMultipleChatsPanelCanary;
    private ComboBox? _editorExtraChatSelectorCanary;
    private TextBox? _editorExtraChatInputCanary;
    private TextBlock? _editorExtraChatPositionCanary;
    private readonly List<CanaryChatLayer> _editorExtraChatsCanary = new();
    private int _editorNextChatNumberCanary = 2;
    private CanaryChatLayer? _editorSelectedExtraChatCanary;
    private bool _editorExtraChatUiUpdatingCanary;
    private bool _editorExtraChatDragCanary;
    private CanaryChatLayer? _editorDraggingExtraChatCanary;
    private Point _editorExtraChatDragStartCanary;
    private double _editorExtraChatStartXCanary;
    private double _editorExtraChatStartYCanary;
    private bool _editorSnapUpdatingCanary;
    private readonly DispatcherTimer _editorSnapGuideTimerCanary = new() { Interval = TimeSpan.FromMilliseconds(650) };

    private ComboBox? _editorFilterPresetCanary;
    private Slider? _editorFilterStrengthCanary;
    private Slider? _editorFilterBrightnessCanary;
    private Slider? _editorFilterContrastCanary;
    private Slider? _editorFilterSaturationCanary;
    private Slider? _editorFilterTemperatureCanary;
    private Slider? _editorFilterFadeCanary;
    private Slider? _editorFilterBlurCanary;
    private DispatcherTimer? _editorFilterTimerCanary;
    private BitmapSource? _editorFilterCommittedCanary;
    private BitmapSource? _editorFilterPreviewCanary;
    private string? _editorFilterTrackedMediaCanary;
    private bool _editorFilterUiUpdatingCanary;

    private double _editorToolPanelWidthCanary = 300;
    private bool _editorFullscreenWorkspaceCanary;
    private Grid? _editorMainLayoutCanary;
    private Grid? _editorRootLayoutCanary;
    private Grid? _editorGlobalHeaderCanary;
    private Border? _editorSidebarCaptureCardCanary;
    private Thickness _editorMainLayoutMarginCanary;
    private GridLength _editorSidebarWidthCanary;
    private GridLength _editorHeaderGapHeightCanary;
    private GridLength _editorFooterGapHeightCanary;
    private GridLength _editorFooterHeightCanary;
    private WindowStyle _editorSavedWindowStyleCanary;
    private WindowState _editorSavedWindowStateCanary;
    private ResizeMode _editorSavedResizeModeCanary;
    private Button? _editorFullscreenCloseCanary;

    private void EnsureEditorCanaryWorkspace()
    {
        if (_editorCanaryWorkspaceInitialized ||
            _editorPage is null ||
            _editorComposition is null ||
            _editorToolPanelHost is null ||
            _editorToolPanelHost.Parent is not Grid editorBody)
            return;

        _editorCanaryWorkspaceInitialized = true;

        CompactEditorInternalHeaderCanary();
        ConfigureResizableEditorPanelsCanary(editorBody);
        ConfigureEditorRailCanary(editorBody);
        if (_editorToolPanelHost is not null)
        {
            foreach (Button closeButton in FindVisualChildrenCanary<Button>(_editorToolPanelHost)
                         .Where(button => string.Equals(button.Content?.ToString(), "×", StringComparison.Ordinal)))
                closeButton.Click += (_, _) => DeactivateSelectionInteractionCanary();
        }
        ConfigureMultipleChatsCanary();
        ConfigureSelectionOverlayCanary();
        ConfigureSnappingCanary();
        ConfigureFilterToolCanary();
        ConfigureEditorChromeCanary();
        ConfigureEditorExportRefreshCanary();

        _editorChatRenderTimer!.Tick += (_, _) => RenderExtraChatLayersCanary();
        _editorSnapGuideTimerCanary.Tick += (_, _) =>
        {
            _editorSnapGuideTimerCanary.Stop();
            ClearSnapGuidesCanary();
        };

        _editorPage.IsVisibleChanged += (_, _) =>
        {
            bool visible = _editorPage.Visibility == Visibility.Visible;
            if (!visible && _editorFullscreenWorkspaceCanary)
                ExitEditorFullscreenCanary();
            ApplyEditorChromeCanary(visible);
        };

        foreach ((string key, Button button) in _editorToolButtons.ToArray())
        {
            if (string.Equals(key, "selection", StringComparison.OrdinalIgnoreCase))
                continue;

            button.Click += (_, _) =>
            {
                DeactivateSelectionInteractionCanary();
                RestoreResizableToolPanelWidthCanary();
            };
        }

        ResetCanaryFilterSource();
        ApplyEditorChromeCanary(_editorPage.Visibility == Visibility.Visible);
    }

    private void CompactEditorInternalHeaderCanary()
    {
        if (_editorPage is null) return;
        Border? header = _editorPage.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (header?.Child is not Grid grid) return;

        StackPanel? title = grid.Children.OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 0);
        if (title is not null)
            title.Visibility = Visibility.Collapsed;

        header.Padding = new Thickness(7, 6, 7, 6);
        header.Margin = new Thickness(0);
        if (_editorPage.RowDefinitions.Count > 1)
            _editorPage.RowDefinitions[1].Height = new GridLength(6);

        foreach (Button button in FindVisualChildrenCanary<Button>(header))
        {
            if (button.Content?.ToString() is "Reset" or "Save Settings")
                button.Visibility = Visibility.Collapsed;
            else
            {
                button.MinHeight = 30;
                button.Height = 30;
                button.Padding = new Thickness(9, 4, 9, 4);
            }
        }
    }

    private void ConfigureResizableEditorPanelsCanary(Grid editorBody)
    {
        if (_editorToolPanelColumn is not null)
        {
            _editorToolPanelColumn.MinWidth = 220;
            _editorToolPanelColumn.MaxWidth = 620;
        }

        if (_editorToolGapColumn is not null)
            _editorToolGapColumn.Width = new GridLength(6);

        if (!editorBody.Children.OfType<GridSplitter>().Any())
        {
            var splitter = new GridSplitter
            {
                Width = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = Brushes.Transparent,
                ToolTip = "Drag to resize the active Editor tool panel."
            };
            splitter.DragCompleted += (_, _) =>
            {
                if (_editorToolPanelHost is not null && _editorToolPanelHost.ActualWidth >= 220)
                    _editorToolPanelWidthCanary = Math.Clamp(_editorToolPanelHost.ActualWidth, 220, 620);
            };
            Grid.SetColumn(splitter, 3);
            editorBody.Children.Add(splitter);
            Panel.SetZIndex(splitter, 20);
        }

        if (_editorInput is not null)
            AddVerticalResizeGripCanary(_editorInput, 120, 650, "Drag to resize the Chat & Font text area.");

        if (_editorLineColorList is not null)
            AddVerticalResizeGripCanary(_editorLineColorList, 140, 700, "Drag to resize the Line Colors list.");
    }

    private void AddVerticalResizeGripCanary(FrameworkElement control, double minimum, double maximum, string tooltip)
    {
        if (control.Parent is not StackPanel parent) return;
        int index = parent.Children.IndexOf(control);
        if (index < 0) return;

        control.MinHeight = minimum;
        control.MaxHeight = double.PositiveInfinity;

        var thumb = new Thumb
        {
            Height = 7,
            Cursor = Cursors.SizeNS,
            Margin = new Thickness(0, 3, 0, 5),
            Background = (Brush)FindResource("Border"),
            ToolTip = tooltip
        };
        thumb.DragDelta += (_, e) =>
        {
            double current = double.IsNaN(control.Height) ? Math.Max(minimum, control.ActualHeight) : control.Height;
            control.Height = Math.Clamp(current + e.VerticalChange, minimum, maximum);
        };
        parent.Children.Insert(index + 1, thumb);
    }

    private void RestoreResizableToolPanelWidthCanary()
    {
        if (_editorToolPanelHost?.Visibility == Visibility.Visible && _editorToolPanelColumn is not null)
            _editorToolPanelColumn.Width = new GridLength(_editorToolPanelWidthCanary);
    }

    private void ConfigureEditorRailCanary(Grid editorBody)
    {
        Border? rail = editorBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        if (rail?.Child is not StackPanel originalStack)
            return;

        _editorToolPanels["selection"] = BuildSelectionToolPanelCanary();
        _editorToolPanels["filters"] = BuildFilterToolPanelCanary();
        _editorToolPanels["settings"] = BuildEditorSettingsPanelCanary();

        var selectionButton = CreateEditorToolIconButton("\uE7C1", "Selection tools — marquee, lasso, polygonal lasso and experimental object selection.", "selection");
        selectionButton.Click += (_, _) =>
        {
            RestoreResizableToolPanelWidthCanary();
            if (_editorToolPanelTitle is not null) _editorToolPanelTitle.Text = "Selection";
            if (_editorToolPanelHost?.Visibility == Visibility.Visible)
                ActivateSelectionToolCanary(_editorSelectionToolCanary == CanarySelectionTool.None
                    ? CanarySelectionTool.Rectangular
                    : _editorSelectionToolCanary);
        };

        var filterButton = CreateEditorToolIconButton("\uE790", "Filters & adjustments — apply editable tone and filter changes globally or only to the current selection.", "filters");
        filterButton.Click += (_, _) =>
        {
            RestoreResizableToolPanelWidthCanary();
            if (_editorToolPanelTitle is not null) _editorToolPanelTitle.Text = "Filters & Adjustments";
        };

        int exportIndex = originalStack.Children
            .OfType<Button>()
            .Select((button, index) => (button, index))
            .FirstOrDefault(pair => string.Equals(pair.button.ToolTip?.ToString(), "Export", StringComparison.OrdinalIgnoreCase))
            .index;

        if (exportIndex <= 0 || exportIndex >= originalStack.Children.Count)
            exportIndex = Math.Max(0, originalStack.Children.Count - 1);

        originalStack.Children.Insert(exportIndex, selectionButton);
        originalStack.Children.Insert(exportIndex + 1, filterButton);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rail.Child = null;
        grid.Children.Add(originalStack);

        var bottom = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var fullscreen = new Button
        {
            Content = "\uE740",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            Width = 38,
            Height = 38,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 7),
            ToolTip = "Full Screen Editor — hide the rest of Afterline and maximize the editing workspace."
        };
        fullscreen.Click += (_, _) => ToggleEditorFullscreenCanary();
        bottom.Children.Add(fullscreen);

        var settings = CreateEditorToolIconButton("\uE713", "Editor settings — save or reset reusable Editor preferences.", "settings");
        settings.Click += (_, _) =>
        {
            DeactivateSelectionInteractionCanary();
            RestoreResizableToolPanelWidthCanary();
            if (_editorToolPanelTitle is not null) _editorToolPanelTitle.Text = "Editor Settings";
        };
        bottom.Children.Add(settings);
        Grid.SetRow(bottom, 1);
        grid.Children.Add(bottom);
        rail.Child = grid;

        foreach (Button button in originalStack.Children.OfType<Button>())
        {
            if (button.ToolTip is null)
                button.ToolTip = "Editor tool";
        }
    }

    private FrameworkElement BuildEditorSettingsPanelCanary()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Editor settings are stored locally. Image-specific selections, filters and edits are intentionally not saved as defaults."));

        var save = new Button
        {
            Content = "Save Editor Settings",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            ToolTip = "Save font, text effects, chat position, output preferences and other reusable Editor controls."
        };
        save.Click += EditorSavePreferences_Click;
        content.Children.Add(save);

        var reset = new Button
        {
            Content = "Reset Editor Controls",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 7, 0, 0),
            ToolTip = "Reset reusable controls to their defaults without deleting the loaded image, chat or markup."
        };
        reset.Click += EditorResetPreferences_Click;
        content.Children.Add(reset);

        content.Children.Add(CreateEditorDivider());

        var fullscreen = new Button
        {
            Content = "Enter Full Screen Editor",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            ToolTip = "Maximize the Editor workspace. Press Escape or use the X button to leave full screen."
        };
        fullscreen.Click += (_, _) => ToggleEditorFullscreenCanary();
        content.Children.Add(fullscreen);

        content.Children.Add(EditorSubtleNote(
            "Tip: Ctrl + mouse wheel zooms the canvas. Drag the divider beside this panel to resize it."));
        return WrapEditorToolPanel(content);
    }

    private void ConfigureMultipleChatsCanary()
    {
        if (!_editorToolPanels.TryGetValue("chat", out FrameworkElement? panel) ||
            panel is not ScrollViewer scroll ||
            scroll.Content is not StackPanel content)
            return;

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock
        {
            Text = "MULTIPLE CHAT BLOCKS",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 7)
        });

        _editorMultipleChatsCheckCanary = new CheckBox
        {
            Content = "Enable multiple chat text blocks",
            ToolTip = "Add separate chat blocks to the same screenshot and position each one independently."
        };
        _editorMultipleChatsCheckCanary.Checked += (_, _) =>
        {
            if (_editorExtraChatsCanary.Count == 0)
                AddExtraChatLayerCanary();
            RefreshMultipleChatUiCanary();
            RenderExtraChatLayersCanary();
        };
        _editorMultipleChatsCheckCanary.Unchecked += (_, _) =>
        {
            RefreshMultipleChatUiCanary();
            RenderExtraChatLayersCanary();
        };
        content.Children.Add(_editorMultipleChatsCheckCanary);

        _editorSnapCheckCanary = new CheckBox
        {
            Content = "Snap chat blocks and show alignment guides",
            IsChecked = true,
            ToolTip = "Snap chat blocks to canvas edges, centers, and nearby chat blocks. Thin guides show the active alignment."
        };
        _editorSnapCheckCanary.Unchecked += (_, _) => ClearSnapGuidesCanary();
        content.Children.Add(_editorSnapCheckCanary);

        var multi = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        _editorMultipleChatsPanelCanary = multi;

        _editorExtraChatSelectorCanary = new ComboBox { Height = 34 };
        _editorExtraChatSelectorCanary.SelectionChanged += (_, _) => SelectExtraChatLayerCanary(
            _editorExtraChatSelectorCanary.SelectedItem as CanaryChatLayer);
        multi.Children.Add(CreateEditorField("Editing block", _editorExtraChatSelectorCanary));

        var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var add = CreateSmallEditorButton("Add Chat", (_, _) => AddExtraChatLayerCanary());
        add.ToolTip = "Add another independently positioned chat block.";
        var remove = CreateSmallEditorButton("Remove", (_, _) => RemoveSelectedExtraChatCanary());
        remove.ToolTip = "Remove the selected extra chat block.";
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        multi.Children.Add(buttons);

        _editorExtraChatInputCanary = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 120,
            Height = 150,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(9),
            ToolTip = "Text for the selected additional chat block."
        };
        _editorExtraChatInputCanary.TextChanged += (_, _) =>
        {
            if (_editorExtraChatUiUpdatingCanary || _editorSelectedExtraChatCanary is null) return;
            _editorSelectedExtraChatCanary.Text = _editorExtraChatInputCanary.Text;
            ScheduleEditorChatRender();
        };
        multi.Children.Add(_editorExtraChatInputCanary);
        AddVerticalResizeGripCanary(_editorExtraChatInputCanary, 90, 500, "Drag to resize this additional chat text area.");

        _editorExtraChatPositionCanary = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10,
            Margin = new Thickness(0, 4, 0, 0)
        };
        multi.Children.Add(_editorExtraChatPositionCanary);

        content.Children.Add(multi);
        RefreshMultipleChatUiCanary();
    }

    private void AddExtraChatLayerCanary()
    {
        if (_editorComposition is null) return;

        var image = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        Panel.SetZIndex(image, 1);
        _editorComposition.Children.Add(image);

        var layer = new CanaryChatLayer
        {
            Number = _editorNextChatNumberCanary++,
            Image = image,
            X = 54 + (_editorExtraChatsCanary.Count * 28),
            Y = 54 + (_editorExtraChatsCanary.Count * 28)
        };
        _editorExtraChatsCanary.Add(layer);

        if (_editorExtraChatSelectorCanary is not null)
        {
            _editorExtraChatSelectorCanary.Items.Add(layer);
            _editorExtraChatSelectorCanary.SelectedItem = layer;
        }

        RefreshMultipleChatUiCanary();
        RenderExtraChatLayersCanary();
    }

    private void RemoveSelectedExtraChatCanary()
    {
        CanaryChatLayer? layer = _editorSelectedExtraChatCanary;
        if (layer is null || _editorComposition is null) return;

        _editorComposition.Children.Remove(layer.Image);
        _editorExtraChatsCanary.Remove(layer);
        _editorExtraChatSelectorCanary?.Items.Remove(layer);

        CanaryChatLayer? next = _editorExtraChatsCanary.LastOrDefault();
        if (_editorExtraChatSelectorCanary is not null)
            _editorExtraChatSelectorCanary.SelectedItem = next;
        SelectExtraChatLayerCanary(next);
        RenderExtraChatLayersCanary();
    }

    private void SelectExtraChatLayerCanary(CanaryChatLayer? layer)
    {
        _editorSelectedExtraChatCanary = layer;
        _editorExtraChatUiUpdatingCanary = true;
        if (_editorExtraChatInputCanary is not null)
            _editorExtraChatInputCanary.Text = layer?.Text ?? string.Empty;
        _editorExtraChatUiUpdatingCanary = false;
        RefreshExtraChatPositionCanary();
    }

    private void RefreshMultipleChatUiCanary()
    {
        bool enabled = _editorMultipleChatsCheckCanary?.IsChecked == true;
        if (_editorMultipleChatsPanelCanary is not null)
            _editorMultipleChatsPanelCanary.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        foreach (CanaryChatLayer layer in _editorExtraChatsCanary)
            layer.Image.Visibility = enabled && !string.IsNullOrWhiteSpace(layer.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void RenderExtraChatLayersCanary()
    {
        if (_editorMultipleChatsCheckCanary?.IsChecked != true)
        {
            RefreshMultipleChatUiCanary();
            return;
        }

        foreach (CanaryChatLayer layer in _editorExtraChatsCanary)
        {
            layer.Bitmap = RenderCanaryChatBitmap(layer.Text);
            layer.Image.Source = layer.Bitmap;
            layer.Image.Margin = new Thickness(layer.X, layer.Y, 0, 0);
            layer.Image.Visibility = layer.Bitmap is null ? Visibility.Collapsed : Visibility.Visible;
        }

        ExpandCanvasForExtraChatsCanary();
        RefreshExtraChatPositionCanary();
    }

    private BitmapSource? RenderCanaryChatBitmap(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        bool showTimestamps = _editorShowTimestampsCheck?.IsChecked == true;
        double fontSize = _editorFontSizeSlider?.Value ?? 18;
        double lineSpacing = _editorLineSpacingSlider?.Value ?? 1;
        double chatWidth = Math.Max(320, _editorChatWidthSlider?.Value ?? 900);
        (FontFamily fontFamily, FontWeight fontWeight) = ResolveEditorFont();
        IReadOnlyList<EditorChatLine> lines = UnifiedChatFormatter.FormatLines(input, showTimestamps, new Dictionary<int, Color>());

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Width = Math.Max(1, chatWidth - 16)
        };

        foreach (EditorChatLine line in lines)
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

            if (line.Segments.Count == 0)
            {
                text.Inlines.Add(new Run(" ") { Foreground = Brushes.Transparent });
            }
            else
            {
                foreach (EditorChatSegment segment in line.Segments)
                {
                    var brush = new SolidColorBrush(segment.Color);
                    if (brush.CanFreeze) brush.Freeze();
                    text.Inlines.Add(new Run(segment.Text) { Foreground = brush });
                }
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
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();

        BitmapSource final = ApplyEditorChatTextEffects(bitmap);
        if (final.CanFreeze && !final.IsFrozen) final.Freeze();
        return final;
    }

    private void ExpandCanvasForExtraChatsCanary()
    {
        if (_editorComposition is null || _editorInkCanvas is null || _editorBaseOriginal is not null)
            return;

        double width = _editorComposition.Width;
        double height = _editorComposition.Height;
        foreach (CanaryChatLayer layer in _editorExtraChatsCanary)
        {
            if (layer.Bitmap is null || layer.Image.Visibility != Visibility.Visible) continue;
            width = Math.Max(width, layer.X + layer.Bitmap.PixelWidth + 12);
            height = Math.Max(height, layer.Y + layer.Bitmap.PixelHeight + 12);
        }

        _editorComposition.Width = Math.Max(1, width);
        _editorComposition.Height = Math.Max(1, height);
        _editorInkCanvas.Width = _editorComposition.Width;
        _editorInkCanvas.Height = _editorComposition.Height;
        ResizeCanaryOverlays();
    }

    private void ConfigureSnappingCanary()
    {
        if (_editorComposition is null) return;

        _editorSnapGuideCanvasCanary = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_editorSnapGuideCanvasCanary, 6);
        _editorComposition.Children.Add(_editorSnapGuideCanvasCanary);

        _editorComposition.PreviewMouseLeftButtonDown += ExtraChatDragMouseDownCanary;
        _editorComposition.PreviewMouseMove += ExtraChatDragMouseMoveCanary;
        _editorComposition.PreviewMouseLeftButtonUp += ExtraChatDragMouseUpCanary;
        _editorComposition.LostMouseCapture += (_, _) => EndExtraChatDragCanary();

        _editorComposition.SizeChanged += (_, _) => ResizeCanaryOverlays();

        if (_editorChatXSlider is not null)
            _editorChatXSlider.ValueChanged += (_, _) => SnapPrimaryChatCanary(horizontalChanged: true);
        if (_editorChatYSlider is not null)
            _editorChatYSlider.ValueChanged += (_, _) => SnapPrimaryChatCanary(horizontalChanged: false);
    }

    private void SnapPrimaryChatCanary(bool horizontalChanged)
    {
        if (_editorSnapUpdatingCanary ||
            _editorSnapCheckCanary?.IsChecked != true ||
            _editorChatBitmap is null ||
            _editorChatXSlider is null ||
            _editorChatYSlider is null ||
            _editorSelectionToolCanary != CanarySelectionTool.None)
            return;

        double x = _editorChatXSlider.Value;
        double y = _editorChatYSlider.Value;
        (double sx, double sy, double? gx, double? gy) = SnapChatPositionCanary(
            x, y, _editorChatBitmap.PixelWidth, _editorChatBitmap.PixelHeight, null);

        _editorSnapUpdatingCanary = true;
        if (horizontalChanged && Math.Abs(sx - x) > 0.01) _editorChatXSlider.Value = sx;
        if (!horizontalChanged && Math.Abs(sy - y) > 0.01) _editorChatYSlider.Value = sy;
        _editorSnapUpdatingCanary = false;
        ShowSnapGuidesCanary(gx, gy);
    }

    private void ExtraChatDragMouseDownCanary(object sender, MouseButtonEventArgs e)
    {
        if (_editorComposition is null ||
            _editorSelectionToolCanary != CanarySelectionTool.None ||
            _editorMultipleChatsCheckCanary?.IsChecked != true)
            return;

        Point p = e.GetPosition(_editorComposition);
        CanaryChatLayer? hit = _editorExtraChatsCanary
            .AsEnumerable()
            .Reverse()
            .FirstOrDefault(layer => layer.Bitmap is not null &&
                new Rect(layer.X, layer.Y, layer.Bitmap.PixelWidth, layer.Bitmap.PixelHeight).Contains(p));

        if (hit is null) return;

        _editorExtraChatDragCanary = true;
        _editorDraggingExtraChatCanary = hit;
        _editorExtraChatDragStartCanary = p;
        _editorExtraChatStartXCanary = hit.X;
        _editorExtraChatStartYCanary = hit.Y;
        if (_editorExtraChatSelectorCanary is not null)
            _editorExtraChatSelectorCanary.SelectedItem = hit;

        _editorComposition.Cursor = Cursors.SizeAll;
        _editorComposition.CaptureMouse();
        e.Handled = true;
    }

    private void ExtraChatDragMouseMoveCanary(object sender, MouseEventArgs e)
    {
        if (!_editorExtraChatDragCanary || _editorComposition is null || _editorDraggingExtraChatCanary?.Bitmap is null)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndExtraChatDragCanary();
            return;
        }

        Point p = e.GetPosition(_editorComposition);
        double x = Math.Max(0, _editorExtraChatStartXCanary + p.X - _editorExtraChatDragStartCanary.X);
        double y = Math.Max(0, _editorExtraChatStartYCanary + p.Y - _editorExtraChatDragStartCanary.Y);

        (double sx, double sy, double? gx, double? gy) = SnapChatPositionCanary(
            x, y,
            _editorDraggingExtraChatCanary.Bitmap.PixelWidth,
            _editorDraggingExtraChatCanary.Bitmap.PixelHeight,
            _editorDraggingExtraChatCanary);

        _editorDraggingExtraChatCanary.X = sx;
        _editorDraggingExtraChatCanary.Y = sy;
        _editorDraggingExtraChatCanary.Image.Margin = new Thickness(sx, sy, 0, 0);
        ShowSnapGuidesCanary(gx, gy);
        RefreshExtraChatPositionCanary();
        ExpandCanvasForExtraChatsCanary();
        e.Handled = true;
    }

    private void ExtraChatDragMouseUpCanary(object sender, MouseButtonEventArgs e)
    {
        if (!_editorExtraChatDragCanary) return;
        EndExtraChatDragCanary();
        e.Handled = true;
    }

    private void EndExtraChatDragCanary()
    {
        if (!_editorExtraChatDragCanary) return;
        _editorExtraChatDragCanary = false;
        _editorDraggingExtraChatCanary = null;
        if (_editorComposition?.IsMouseCaptured == true)
            _editorComposition.ReleaseMouseCapture();
        if (_editorComposition is not null)
            _editorComposition.Cursor = Cursors.Arrow;
        _editorSnapGuideTimerCanary.Stop();
        _editorSnapGuideTimerCanary.Start();
    }

    private (double X, double Y, double? GuideX, double? GuideY) SnapChatPositionCanary(
        double x, double y, double width, double height, CanaryChatLayer? moving)
    {
        if (_editorSnapCheckCanary?.IsChecked != true || _editorComposition is null)
            return (x, y, null, null);

        const double threshold = 8;
        double? guideX = null;
        double? guideY = null;

        var verticalTargets = new List<double>
        {
            0,
            _editorComposition.Width / 2,
            _editorComposition.Width
        };
        var horizontalTargets = new List<double>
        {
            0,
            _editorComposition.Height / 2,
            _editorComposition.Height
        };

        if (_editorChatBitmap is not null && moving is not null)
        {
            double px = _editorChatXSlider?.Value ?? 0;
            double py = _editorChatYSlider?.Value ?? 0;
            verticalTargets.AddRange(new[] { px, px + _editorChatBitmap.PixelWidth / 2.0, px + _editorChatBitmap.PixelWidth });
            horizontalTargets.AddRange(new[] { py, py + _editorChatBitmap.PixelHeight / 2.0, py + _editorChatBitmap.PixelHeight });
        }

        foreach (CanaryChatLayer layer in _editorExtraChatsCanary)
        {
            if (ReferenceEquals(layer, moving) || layer.Bitmap is null) continue;
            verticalTargets.AddRange(new[] { layer.X, layer.X + layer.Bitmap.PixelWidth / 2.0, layer.X + layer.Bitmap.PixelWidth });
            horizontalTargets.AddRange(new[] { layer.Y, layer.Y + layer.Bitmap.PixelHeight / 2.0, layer.Y + layer.Bitmap.PixelHeight });
        }

        double[] xAnchors = { x, x + width / 2.0, x + width };
        double[] yAnchors = { y, y + height / 2.0, y + height };

        double bestX = threshold + 1;
        foreach (double target in verticalTargets)
        {
            for (int anchor = 0; anchor < xAnchors.Length; anchor++)
            {
                double delta = target - xAnchors[anchor];
                if (Math.Abs(delta) < Math.Abs(bestX))
                {
                    bestX = delta;
                    guideX = target;
                }
            }
        }
        if (Math.Abs(bestX) <= threshold) x += bestX;
        else guideX = null;

        double bestY = threshold + 1;
        foreach (double target in horizontalTargets)
        {
            for (int anchor = 0; anchor < yAnchors.Length; anchor++)
            {
                double delta = target - yAnchors[anchor];
                if (Math.Abs(delta) < Math.Abs(bestY))
                {
                    bestY = delta;
                    guideY = target;
                }
            }
        }
        if (Math.Abs(bestY) <= threshold) y += bestY;
        else guideY = null;

        return (Math.Max(0, x), Math.Max(0, y), guideX, guideY);
    }

    private void ShowSnapGuidesCanary(double? x, double? y)
    {
        if (_editorSnapGuideCanvasCanary is null || _editorComposition is null ||
            _editorSnapCheckCanary?.IsChecked != true)
            return;

        _editorSnapGuideCanvasCanary.Children.Clear();
        Brush accent = (Brush)FindResource("Accent");

        if (x is double gx)
        {
            _editorSnapGuideCanvasCanary.Children.Add(new Line
            {
                X1 = gx, X2 = gx, Y1 = 0, Y2 = _editorComposition.Height,
                Stroke = accent, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 },
                Opacity = 0.85
            });
        }
        if (y is double gy)
        {
            _editorSnapGuideCanvasCanary.Children.Add(new Line
            {
                X1 = 0, X2 = _editorComposition.Width, Y1 = gy, Y2 = gy,
                Stroke = accent, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 },
                Opacity = 0.85
            });
        }

        if (x is not null || y is not null)
        {
            _editorSnapGuideTimerCanary.Stop();
            _editorSnapGuideTimerCanary.Start();
        }
    }

    private void ClearSnapGuidesCanary() => _editorSnapGuideCanvasCanary?.Children.Clear();

    private void RefreshExtraChatPositionCanary()
    {
        if (_editorExtraChatPositionCanary is null) return;
        _editorExtraChatPositionCanary.Text = _editorSelectedExtraChatCanary is null
            ? "No additional chat block selected."
            : $"Position: {Math.Round(_editorSelectedExtraChatCanary.X):0}, {Math.Round(_editorSelectedExtraChatCanary.Y):0} · drag the block directly on the canvas.";
    }
}
