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
    private bool _editorUpdatingLineColorUi;

    private CheckBox? _editorShadowEnabledCheck;
    private Slider? _editorShadowOpacitySlider;
    private Slider? _editorShadowBlurSlider;
    private Slider? _editorShadowOffsetXSlider;
    private Slider? _editorShadowOffsetYSlider;
    private ComboBox? _editorShadowColorBox;
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
        var card = new Border { Style = (Style)FindResource("CardStyle") };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = "RP Screenshot Editor",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        title.Children.Add(new TextBlock
        {
            Text = "Automatic RP colors by default. Open only the tool you need from the icon bar.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        });
        grid.Children.Add(title);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actions, 1);

        actions.Children.Add(CreateEditorHeaderButton("Load Image", EditorLoadImage_Click));
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

    private Grid BuildEditorV041Body(string existingInput)
    {
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
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
        close.Click += (_, _) => CloseEditorToolPanel();
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
        _editorToolPanels["colors"] = BuildEditorV041ColorsPanel();
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
        stack.Children.Add(CreateEditorToolIconButton("\uE790", "Line colors", "colors"));
        stack.Children.Add(CreateEditorToolIconButton("\uE71C", "Text effects", "effects"));
        stack.Children.Add(CreateEditorToolIconButton("\uEB9F", "Image & canvas", "image"));
        stack.Children.Add(CreateEditorToolIconButton("\uE76D", "Paint & markup", "markup"));
        stack.Children.Add(CreateEditorToolIconButton("\uE74E", "Export", "export"));
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
        var content = new StackPanel();
        content.Children.Add(EditorHelpText("Paste or import chat. Afterline applies the closest RP colors automatically, so most users can leave the color tool alone."));

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
        _editorInput.TextChanged += (_, _) =>
        {
            PruneEditorLineColorOverrides();
            ScheduleEditorChatRender();
        };
        content.Children.Add(_editorInput);

        var inputButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 2) };
        inputButtons.Children.Add(CreateSmallEditorButton("Paste", EditorPaste_Click));
        inputButtons.Children.Add(CreateSmallEditorButton("Import .txt", EditorImportText_Click));
        inputButtons.Children.Add(CreateSmallEditorButton("Clear", EditorClearInput_Click));
        content.Children.Add(inputButtons);
        content.Children.Add(CreateEditorDivider());

        _editorFontBox = new ComboBox { Height = 34 };
        foreach (string font in new[] { "Arial Bold", "Arial", "Segoe UI Semibold", "Tahoma", "Verdana" })
            _editorFontBox.Items.Add(font);
        _editorFontBox.SelectedIndex = 0;
        _editorFontBox.SelectionChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(CreateEditorField("Font", _editorFontBox));

        var fontSize = CreateEditorV041Slider("Font size", 12, 32, 18);
        _editorFontSizeSlider = fontSize.Slider;
        _editorFontSizeSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(fontSize.Panel);

        var spacing = CreateEditorV041Slider("Line spacing", 0, 8, 1);
        _editorLineSpacingSlider = spacing.Slider;
        _editorLineSpacingSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(spacing.Panel);

        var width = CreateEditorV041Slider("Chat width", 320, 1200, 900, 10);
        _editorChatWidthSlider = width.Slider;
        _editorChatWidthSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(width.Panel);

        _editorShowTimestampsCheck = new CheckBox
        {
            Content = "Show timestamps",
            IsChecked = false,
            Margin = new Thickness(0, 5, 0, 0),
            ToolTip = "Leave this off for the clean RP screenshot style."
        };
        _editorShowTimestampsCheck.Checked += (_, _) => ScheduleEditorChatRender();
        _editorShowTimestampsCheck.Unchecked += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(_editorShowTimestampsCheck);

        return WrapEditorToolPanel(content);
    }

}
