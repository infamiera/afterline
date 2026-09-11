using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed record EditorLineChoice(int SourceIndex, string Text, string AutoStyle)
    {
        public override string ToString()
        {
            string value = string.IsNullOrWhiteSpace(Text) ? "(blank line)" : Text.Replace('\t', ' ').Trim();
            if (value.Length > 72) value = value[..69] + "…";
            return $"{SourceIndex + 1}. {value}";
        }
    }

    private sealed record EditorPresetChoice(string? Key, string Name, Color? Color)
    {
        public override string ToString() => Name;
    }

    private bool _editorV041Initialized;
    private readonly Dictionary<string, Button> _editorToolButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameworkElement> _editorToolPanels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Color> _editorLineColorOverrides = new();
    private readonly List<EditorTextColorOverride> _editorTextColorOverridesV071 = new();
    private readonly List<EditorTextBlurOverride> _editorTextBlurOverridesV092 = new();
    private ColumnDefinition? _editorToolPanelColumn;
    private ColumnDefinition? _editorToolGapColumn;
    private Border? _editorToolPanelHost;
    private ContentControl? _editorToolPanelContent;
    private TextBlock? _editorToolPanelTitle;
    private string? _editorActiveToolKey;
    private ScrollViewer? _editorPreviewScroll;
    private Border? _editorZoomHost;
    private TextBlock? _editorZoomText;
    private double _editorZoomScale = 1.0;
    private bool _editorFitZoom;
    private ListBox? _editorLineColorList;
    private ComboBox? _editorLineColorPresetBox;
    private TextBlock? _editorLineColorHint;
    private Expander? _editorChatColorsExpanderV071;
    private bool _editorUpdatingLineColorUi;

    private CheckBox? _editorShadowEnabledCheck;
    private Slider? _editorShadowOpacitySlider;
    private Slider? _editorShadowBlurSlider;
    private Slider? _editorShadowOffsetXSlider;
    private Slider? _editorShadowOffsetYSlider;
    private ComboBox? _editorShadowColorBox;
    private Slider? _editorTextBlurRadiusSliderV092;
    private WrapPanel? _editorContextOptionsHostV092;
    private Expander? _editorFontLayoutExpanderV092;
    private CheckBox? _editorStrokeEnabledCheck;
    private Slider? _editorStrokeWidthSlider;
    private ComboBox? _editorStrokeColorBox;

    private void EnsureEditorV041()
    {
        if (_editorV041Initialized || _editorPage is null || _editorComposition is null) return;
        _editorV041Initialized = true;

        string existingInput = _editorInput?.Text ?? string.Empty;
        DetachEditorElement(_editorComposition);

        UIElement[] oldChildren = _editorPage.Children.Cast<UIElement>().ToArray();
        foreach (UIElement child in oldChildren)
            _editorPage.Children.Remove(child);

        _editorPage.Children.Add(BuildEditorV041Header());
        Grid newBody = BuildEditorV041Body(existingInput);
        Grid.SetRow(newBody, 2);
        _editorPage.Children.Add(newBody);
        MoveEditorTypographyControlsToContextBarV092();

        ConfigureEditorContextMenus();
        ConfigureEditorLineColorContextMenu();
        UpdateEditorDrawingAttributes();
        UpdateEditorHistoryButtons();
        SetEditorMarkupTool(_editorMarkupTool);

        if (_editorComposition is not null)
            _editorComposition.SizeChanged += (_, _) =>
            {
                if (_editorFitZoom) _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
            };

        RenderEditorChatOverlay();
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
        ShowEditorToolPanel("chat", forceOpen: true);
    }

    private Border BuildEditorV041Header()
    {
        // This is a compact document/options bar. Text actions live beside the
        // chat input and image loading stays in File, preserving preview room.
        var card = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var identity = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new TextBlock
        {
            Text = "EDITOR",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        });
        identity.Children.Add(new TextBlock
        {
            Text = "Chat composition",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10,
            Margin = new Thickness(8, 0, 0, 0)
        });
        grid.Children.Add(identity);

        var typography = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(typography, 1);
        grid.Children.Add(typography);

        _editorContextOptionsHostV092 = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        typography.Children.Add(_editorContextOptionsHostV092);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actions, 2);

        _editorUndoButton = CreateEditorHeaderButton("Undo", EditorUndo_Click);
        _editorRedoButton = CreateEditorHeaderButton("Redo", EditorRedo_Click);
        actions.Children.Add(_editorUndoButton);
        actions.Children.Add(_editorRedoButton);
        var export = CreateEditorHeaderButton("Export PNG", EditorExportPng_Click);
        export.Style = (Style)FindResource("PrimaryButton");
        actions.Children.Add(export);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    private Button CreateEditorCommandBarButton(string text, string toolTip, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            ToolTip = toolTip,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 4, 0),
            MinHeight = 28,
            FontSize = 10.5
        };
        button.Click += handler;
        return button;
    }

    private Border CreateEditorCommandBarDivider()
        => new()
        {
            Width = 1,
            Height = 22,
            Background = (Brush)FindResource("Border"),
            Margin = new Thickness(3, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

    private Grid BuildEditorV041Body(string existingInput)
    {
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
        _editorToolPanelColumn = new ColumnDefinition { Width = new GridLength(300) };
        body.ColumnDefinitions.Add(_editorToolPanelColumn);
        _editorToolGapColumn = new ColumnDefinition { Width = new GridLength(12) };
        body.ColumnDefinitions.Add(_editorToolGapColumn);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        body.Children.Add(BuildEditorV041IconRail());

        _editorToolPanelHost = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(10) };
        var toolHostGrid = new Grid();
        toolHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        toolHostGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        toolHostGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolHeader = new Grid();
        toolHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _editorToolPanelTitle = new TextBlock
        {
            Text = "Chat",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        toolHeader.Children.Add(_editorToolPanelTitle);
        var close = new Button
        {
            Content = "×",
            FontSize = 18,
            Width = 32,
            Height = 30,
            Padding = new Thickness(0),
            ToolTip = "Close tool panel"
        };
        _editorLeftSidebarToggleV072 = close;
        close.Click += (_, _) => ToggleEditorLeftSidebarV072();
        Grid.SetColumn(close, 1);
        toolHeader.Children.Add(close);
        toolHostGrid.Children.Add(toolHeader);

        _editorToolPanelContent = new ContentControl();
        Grid.SetRow(_editorToolPanelContent, 2);
        toolHostGrid.Children.Add(_editorToolPanelContent);
        _editorToolPanelHost.Child = toolHostGrid;
        Grid.SetColumn(_editorToolPanelHost, 2);
        body.Children.Add(_editorToolPanelHost);

        _editorToolPanels["chat"] = BuildEditorV041ChatPanel(existingInput);
        _editorToolPanels["effects"] = BuildEditorV041TextEffectsPanel();
        _editorToolPanels["image"] = BuildEditorV041ImagePanel();
        _editorToolPanels["markup"] = BuildEditorV041MarkupPanel();
        _editorToolPanels["export"] = BuildEditorV041ExportPanel();

        Border preview = BuildEditorV041PreviewPanel();
        Grid.SetColumn(preview, 4);
        body.Children.Add(preview);
        return body;
    }

    private Border BuildEditorV041IconRail()
    {
        var rail = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(5, 8, 5, 8) };
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(CreateEditorToolIconButton("\uE8C8", "Chat & font", "chat"));
        stack.Children.Add(CreateEditorToolIconButton("\uE71C", "Text effects", "effects"));
        stack.Children.Add(CreateEditorToolIconButton("\uEB9F", "Image & canvas", "image"));
        stack.Children.Add(CreateEditorToolIconButton("\uE76D", "Paint & markup", "markup"));
        stack.Children.Add(CreateEditorToolIconButton("\uE74E", "Export", "export"));
        _editorLeftSidebarReopenV073 = new Button
        {
            Content = "▶",
            FontSize = 13,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 4, 0, 0),
            ToolTip = "Open the left Editor panel",
            Visibility = Visibility.Collapsed
        };
        _editorLeftSidebarReopenV073.Click += (_, _) => ToggleEditorLeftSidebarV072();
        stack.Children.Add(_editorLeftSidebarReopenV073);
        rail.Child = stack;
        return rail;
    }

    private Button CreateEditorToolIconButton(string glyph, string toolTip, string key)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 17,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = toolTip
        };
        button.Click += (_, _) => ShowEditorToolPanel(key, forceOpen: false);
        _editorToolButtons[key] = button;
        return button;
    }

    private FrameworkElement BuildEditorV041ChatPanel(string existingInput)
    {
        var sections = new StackPanel();
        var chatContent = new StackPanel();
        chatContent.Children.Add(EditorHelpText("Paste or import chat. Afterline applies the closest RP colors automatically, and selected text can be recolored in the Line Colors section below."));

        _editorInput = new TextBox
        {
            Text = existingInput,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 220,
            MaxHeight = 330,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(9)
        };
        EditorTextProofingService.Attach(_editorInput);
        _editorInput.TextChanged += (_, _) =>
        {
            PruneEditorLineColorOverrides();
            UpdateTextBlurTargetHintV093();
            ScheduleEditorChatRender();
        };
        _editorInput.SelectionChanged += (_, _) =>
        {
            CaptureTextBlurTargetV093();
            if (_editorInput.SelectionLength > 0 && _editorChatColorsExpanderV071 is not null)
            {
                bool wasCollapsed = !_editorChatColorsExpanderV071.IsExpanded;
                _editorChatColorsExpanderV071.IsExpanded = true;
                if (wasCollapsed)
                    _ = Dispatcher.BeginInvoke(new Action(() => _editorChatColorsExpanderV071?.BringIntoView()));
            }
            UpdateEditorLineColorControls();
        };
        chatContent.Children.Add(_editorInput);

        var inputActions = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        inputActions.Children.Add(CreateEditorChatInputActionV093("Paste", "Paste chat from the clipboard", EditorPaste_Click));
        inputActions.Children.Add(CreateEditorChatInputActionV093("Import .txt", "Import chat from a text file", EditorImportText_Click));
        inputActions.Children.Add(CreateEditorChatInputActionV093("Clear", "Clear the editable chat text", EditorClearInput_Click));
        chatContent.Children.Add(inputActions);

        sections.Children.Add(CreateEditorSidebarExpanderV068("CHAT TEXT", chatContent, expanded: true));

        var fontContent = new StackPanel();

        _editorFontBox = new ComboBox { Height = 34 };
        PopulateEditorFontBoxV071(_editorFontBox);
        _editorFontBox.SelectedIndex = 0;
        _editorFontBox.SelectionChanged += (_, _) => ScheduleEditorChatRender();
        fontContent.Children.Add(CreateEditorField("Font", _editorFontBox));
        fontContent.Children.Add(EditorSubtleNote(
            "Font stacks use the first installed Windows font and fall back safely. Server webfonts such as Raleway or Mukta render exactly when that font is installed locally."));

        var fontSize = CreateEditorV041Slider("Font size", 12, 100, 18);
        _editorFontSizeSlider = fontSize.Slider;
        _editorFontSizeSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        fontContent.Children.Add(fontSize.Panel);

        var spacing = CreateEditorV041Slider("Line spacing", 0, 8, 1);
        _editorLineSpacingSlider = spacing.Slider;
        _editorLineSpacingSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        fontContent.Children.Add(spacing.Panel);

        var width = CreateEditorV041Slider("Chat width", 320, 1500, 900, 10);
        _editorChatWidthSlider = width.Slider;
        _editorChatWidthSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        fontContent.Children.Add(width.Panel);

        _editorShowTimestampsCheck = new CheckBox
        {
            Content = "Show timestamps",
            IsChecked = false,
            Margin = new Thickness(0, 5, 0, 0),
            ToolTip = "Leave this off for the clean RP screenshot style."
        };
        _editorShowTimestampsCheck.Checked += (_, _) => ScheduleEditorChatRender();
        _editorShowTimestampsCheck.Unchecked += (_, _) => ScheduleEditorChatRender();
        fontContent.Children.Add(_editorShowTimestampsCheck);

        Expander fontSection = CreateEditorSidebarExpanderV068("FONT & LAYOUT", fontContent, expanded: true);
        _editorFontLayoutExpanderV092 = fontSection;
        fontSection.Margin = new Thickness(0, 8, 0, 0);
        sections.Children.Add(fontSection);

        _editorChatColorsExpanderV071 = CreateEditorSidebarExpanderV068(
            "LINE COLORS",
            BuildEditorV041ColorsContent(),
            expanded: false);
        _editorChatColorsExpanderV071.Margin = new Thickness(0, 8, 0, 0);
        sections.Children.Add(_editorChatColorsExpanderV071);

        return WrapEditorToolPanel(sections);
    }

    private void MoveEditorTypographyControlsToContextBarV092()
    {
        if (_editorContextOptionsHostV092 is null ||
            _editorFontBox is null ||
            _editorFontSizeSlider is null ||
            _editorLineSpacingSlider is null ||
            _editorChatWidthSlider is null ||
            _editorShowTimestampsCheck is null)
            return;

        _editorContextOptionsHostV092.Children.Clear();
        _editorContextOptionsHostV092.Children.Add(CreateEditorContextLabelV092("FONT & LAYOUT"));

        DetachEditorElement(_editorFontBox);
        _editorFontBox.Width = 150;
        _editorFontBox.Height = 32;
        _editorFontBox.Margin = new Thickness(0, 0, 6, 0);
        _editorFontBox.ToolTip = "Choose the font used for the generated chat overlay.";
        _editorContextOptionsHostV092.Children.Add(_editorFontBox);

        _editorContextOptionsHostV092.Children.Add(CreateEditorContextSliderV092(
            "Size", _editorFontSizeSlider, 96, "Adjust generated chat font size."));
        _editorContextOptionsHostV092.Children.Add(CreateEditorContextSliderV092(
            "Leading", _editorLineSpacingSlider, 84, "Adjust the vertical space between chat lines."));
        _editorContextOptionsHostV092.Children.Add(CreateEditorContextSliderV092(
            "Width", _editorChatWidthSlider, 96, "Adjust the width of the generated chat block."));

        DetachEditorElement(_editorShowTimestampsCheck);
        _editorShowTimestampsCheck.Content = "Timestamps";
        _editorShowTimestampsCheck.Margin = new Thickness(4, 0, 0, 0);
        _editorShowTimestampsCheck.MinHeight = 32;
        _editorShowTimestampsCheck.VerticalAlignment = VerticalAlignment.Center;
        _editorContextOptionsHostV092.Children.Add(_editorShowTimestampsCheck);

        // The old Font & Layout panel contains the source holders for the controls
        // above. Hide it after reparenting so it cannot become a duplicate, empty
        // card in the Chat panel.
        if (_editorFontLayoutExpanderV092 is not null)
            _editorFontLayoutExpanderV092.Visibility = Visibility.Collapsed;
    }

    private TextBlock CreateEditorContextLabelV092(string text)
        => new()
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };

    private Button CreateEditorChatInputActionV093(string text, string toolTip, RoutedEventHandler handler)
    {
        var button = CreateSmallEditorButton(text, handler);
        button.MinHeight = 28;
        button.Height = 28;
        button.Padding = new Thickness(8, 3, 8, 3);
        button.ToolTip = toolTip;
        return button;
    }

    private FrameworkElement CreateEditorContextSliderV092(string label, Slider slider, double width, string toolTip)
    {
        DetachEditorElement(slider);
        slider.Width = width;
        slider.Margin = new Thickness(0);
        slider.ToolTip = toolTip;

        var panel = new StackPanel
        {
            Width = width,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 8.5,
            Foreground = (Brush)FindResource("MutedText"),
            ToolTip = toolTip
        });
        panel.Children.Add(slider);
        return panel;
    }

}
