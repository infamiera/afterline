using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _editorInitialized;
    private Button? _editorNavButton;
    private Grid? _editorPage;
    private TextBox? _editorInput;
    private Grid? _editorComposition;
    private Image? _editorBaseImage;
    private Image? _editorChatImage;
    private InkCanvas? _editorInkCanvas;
    private TextBlock? _editorStatusText;
    private Button? _editorRemoveImageButton;
    private Button? _editorUndoButton;
    private Button? _editorRedoButton;
    private Button? _editorPaintButton;
    private Button? _editorEraseButton;
    private Button? _editorTextButton;
    private ComboBox? _editorFontBox;
    private ComboBox? _editorBackgroundBox;
    private ComboBox? _editorPaintColorBox;
    private CheckBox? _editorShowTimestampsCheck;
    private Slider? _editorFontSizeSlider;
    private Slider? _editorLineSpacingSlider;
    private Slider? _editorChatWidthSlider;
    private Slider? _editorChatXSlider;
    private Slider? _editorChatYSlider;
    private Slider? _editorBrightnessSlider;
    private Slider? _editorContrastSlider;
    private Slider? _editorSaturationSlider;
    private Slider? _editorWarmthSlider;
    private Slider? _editorTintSlider;
    private Slider? _editorBlurSlider;
    private Slider? _editorBrushSizeSlider;
    private BitmapSource? _editorBaseOriginal;
    private BitmapSource? _editorChatBitmap;
    private DispatcherTimer? _editorChatRenderTimer;
    private DispatcherTimer? _editorBaseAdjustTimer;
    private IReadOnlyDictionary<int, ChatColorLineRecord> _editorExactChatColorsV068 =
        new Dictionary<int, ChatColorLineRecord>();

    private void EnsureEditor()
    {
        if (_editorInitialized) return;
        if (SettingsNav.Parent is not StackPanel navigationPanel || DashboardPage.Parent is not Grid pageHost) return;

        _editorInitialized = true;
        _editorNavButton = new Button
        {
            Content = "Editor",
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = "Create and lightly edit roleplay screenshot chat overlays."
        };
        _editorNavButton.Click += EditorNav_Click;

        int settingsIndex = navigationPanel.Children.IndexOf(SettingsNav);
        navigationPanel.Children.Insert(Math.Max(0, settingsIndex), _editorNavButton);

        _editorPage = BuildEditorPage();
        Grid.SetRow(_editorPage, 2);
        pageHost.Children.Add(_editorPage);

        foreach (Button nav in navigationPanel.Children.OfType<Button>().ToArray())
        {
            if (ReferenceEquals(nav, _editorNavButton)) continue;
            nav.Click += (_, _) =>
            {
                if (_editorPage is not null) _editorPage.Visibility = Visibility.Collapsed;
            };
        }

        _editorChatRenderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _editorChatRenderTimer.Tick += (_, _) =>
        {
            _editorChatRenderTimer.Stop();
            RenderEditorChatOverlay();
        };

        _editorBaseAdjustTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _editorBaseAdjustTimer.Tick += (_, _) =>
        {
            _editorBaseAdjustTimer.Stop();
            ApplyEditorImageAdjustments();
        };

        ConfigureEditorContextMenus();
        InitializeEditorMarkupHistory();
        UpdateEditorDrawingAttributes();
        RenderEditorChatOverlay();
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
    }

    private Grid BuildEditorPage()
    {
        var page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        page.Children.Add(BuildEditorToolbar());

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 2);
        page.Children.Add(body);

        UIElement left = BuildEditorInputPanel();
        body.Children.Add(left);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        right.Children.Add(BuildEditorPreviewPanel());
        UIElement tools = BuildEditorToolsPanel();
        Grid.SetRow(tools, 2);
        right.Children.Add(tools);

        return page;
    }

    private Border BuildEditorToolbar()
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
            Text = "Paste chat, match in-game colors, add lightweight edits, then export a PNG.",
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 4, 0, 0)
        });
        grid.Children.Add(title);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actions, 1);

        var loadImage = CreateEditorToolbarButton("Load Image", EditorLoadImage_Click);
        _editorRemoveImageButton = CreateEditorToolbarButton("Remove Image", EditorRemoveImage_Click);
        _editorRemoveImageButton.IsEnabled = false;
        _editorUndoButton = CreateEditorToolbarButton("Undo", EditorUndo_Click);
        _editorRedoButton = CreateEditorToolbarButton("Redo", EditorRedo_Click);
        var reset = CreateEditorToolbarButton("Reset Edits", EditorResetEdits_Click);
        var copy = CreateEditorToolbarButton("Copy Image", EditorCopyImage_Click);
        var export = CreateEditorToolbarButton("Export PNG", EditorExportPng_Click);
        export.Style = (Style)FindResource("PrimaryButton");

        actions.Children.Add(loadImage);
        actions.Children.Add(_editorRemoveImageButton);
        actions.Children.Add(_editorUndoButton);
        actions.Children.Add(_editorRedoButton);
        actions.Children.Add(reset);
        actions.Children.Add(copy);
        actions.Children.Add(export);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    private Border BuildEditorInputPanel()
    {
        var card = new Border { Style = (Style)FindResource("CardStyle") };
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var content = new StackPanel();

        content.Children.Add(CreateEditorSectionHeading("CHAT INPUT", "Paste raw chat lines or import a .txt log. Timestamps can be removed automatically."));

        _editorInput = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 230,
            MaxHeight = 330,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(10)
        };
        _editorInput.TextChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(_editorInput);

        var inputActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        inputActions.Children.Add(CreateSmallEditorButton("Paste", EditorPaste_Click));
        inputActions.Children.Add(CreateSmallEditorButton("Import .txt", EditorImportText_Click));
        inputActions.Children.Add(CreateSmallEditorButton("Clear", EditorClearInput_Click));
        content.Children.Add(inputActions);

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(CreateEditorSectionHeading("CHAT STYLE", "Arial and Arial Bold are built in as the primary output fonts. Other choices use Windows system fonts only."));

        _editorFontBox = new ComboBox { Height = 34, Margin = new Thickness(0, 0, 0, 8) };
        PopulateEditorFontBoxV071(_editorFontBox);
        _editorFontBox.SelectedIndex = 0;
        _editorFontBox.SelectionChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(CreateEditorField("Font", _editorFontBox));

        var fontSize = CreateEditorSlider("Font size", 12, 32, 18);
        _editorFontSizeSlider = fontSize.Slider;
        _editorFontSizeSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(fontSize.Panel);

        var spacing = CreateEditorSlider("Line spacing", 0, 8, 1);
        _editorLineSpacingSlider = spacing.Slider;
        _editorLineSpacingSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(spacing.Panel);

        var chatWidth = CreateEditorSlider("Chat width", 320, 1200, 900, 10);
        _editorChatWidthSlider = chatWidth.Slider;
        _editorChatWidthSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(chatWidth.Panel);

        _editorShowTimestampsCheck = new CheckBox
        {
            Content = "Show timestamps",
            IsChecked = false,
            Margin = new Thickness(0, 4, 0, 8),
            ToolTip = "When disabled, leading [HH:mm:ss] timestamps are removed from the rendered image."
        };
        _editorShowTimestampsCheck.Checked += (_, _) => ScheduleEditorChatRender();
        _editorShowTimestampsCheck.Unchecked += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(_editorShowTimestampsCheck);

        _editorBackgroundBox = new ComboBox { Height = 34, Margin = new Thickness(0, 0, 0, 8) };
        _editorBackgroundBox.Items.Add("Black");
        _editorBackgroundBox.Items.Add("Transparent");
        _editorBackgroundBox.SelectedIndex = 0;
        _editorBackgroundBox.SelectionChanged += (_, _) => UpdateEditorCanvasSize();
        content.Children.Add(CreateEditorField("Canvas background", _editorBackgroundBox));

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(CreateEditorSectionHeading("CHAT POSITION", "Useful when a screenshot is loaded underneath the chat overlay."));

        var x = CreateEditorSlider("Horizontal offset", 0, 600, 0, 5);
        _editorChatXSlider = x.Slider;
        _editorChatXSlider.ValueChanged += (_, _) => UpdateEditorCanvasSize();
        content.Children.Add(x.Panel);

        var y = CreateEditorSlider("Vertical offset", 0, 400, 0, 5);
        _editorChatYSlider = y.Slider;
        _editorChatYSlider.ValueChanged += (_, _) => UpdateEditorCanvasSize();
        content.Children.Add(y.Panel);

        scroll.Content = content;
        card.Child = scroll;
        return card;
    }

    private Border BuildEditorPreviewPanel()
    {
        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(10)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14)
        };
        scroll.SetResourceReference(Control.BackgroundProperty, "AfterlineInset");

        _editorComposition = new Grid
        {
            Width = 720,
            Height = 420,
            Background = Brushes.Black,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            UseLayoutRounding = true
        };

        _editorBaseImage = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            SnapsToDevicePixels = true
        };
        Panel.SetZIndex(_editorBaseImage, 0);
        _editorComposition.Children.Add(_editorBaseImage);

        _editorChatImage = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_editorChatImage, 1);
        _editorComposition.Children.Add(_editorChatImage);

        _editorInkCanvas = new InkCanvas
        {
            Background = Brushes.Transparent,
            EditingMode = InkCanvasEditingMode.Ink,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };
        _editorInkCanvas.PreviewMouseLeftButtonDown += EditorInkCanvas_PreviewMouseLeftButtonDown;
        _editorInkCanvas.StrokeCollected += (_, _) => CaptureEditorMarkupSnapshot();
        _editorInkCanvas.StrokeErased += (_, _) => CaptureEditorMarkupSnapshot();
        Panel.SetZIndex(_editorInkCanvas, 2);
        _editorComposition.Children.Add(_editorInkCanvas);

        scroll.Content = _editorComposition;
        root.Children.Add(scroll);

        _editorStatusText = new TextBlock
        {
            Text = "Paste chat lines to begin. Load an image if you want to edit a full RP screenshot.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 8, 2, 0)
        };
        Grid.SetRow(_editorStatusText, 1);
        root.Children.Add(_editorStatusText);

        card.Child = root;
        return card;
    }

    private Border BuildEditorToolsPanel()
    {
        var card = new Border { Style = (Style)FindResource("CardStyle") };
        var root = new StackPanel();

        root.Children.Add(CreateEditorSectionHeading("IMAGE ADJUSTMENTS", "Applied to an optional loaded screenshot while the generated chat remains crisp."));
        var adjustments = new WrapPanel();

        var brightness = CreateEditorSlider("Brightness", -100, 100, 0);
        _editorBrightnessSlider = brightness.Slider;
        _editorBrightnessSlider.ValueChanged += EditorBaseAdjustment_Changed;
        adjustments.Children.Add(brightness.Panel);

        var contrast = CreateEditorSlider("Contrast", -100, 100, 0);
        _editorContrastSlider = contrast.Slider;
        _editorContrastSlider.ValueChanged += EditorBaseAdjustment_Changed;
        adjustments.Children.Add(contrast.Panel);

        var saturation = CreateEditorSlider("Saturation", -100, 100, 0);
        _editorSaturationSlider = saturation.Slider;
        _editorSaturationSlider.ValueChanged += EditorBaseAdjustment_Changed;
        adjustments.Children.Add(saturation.Panel);

        var warmth = CreateEditorSlider("Warmth", -100, 100, 0);
        _editorWarmthSlider = warmth.Slider;
        _editorWarmthSlider.ValueChanged += EditorBaseAdjustment_Changed;
        adjustments.Children.Add(warmth.Panel);

        var tint = CreateEditorSlider("Tint", -100, 100, 0);
        _editorTintSlider = tint.Slider;
        _editorTintSlider.ValueChanged += EditorBaseAdjustment_Changed;
        adjustments.Children.Add(tint.Panel);

        var blur = CreateEditorSlider("Blur", 0, 20, 0);
        _editorBlurSlider = blur.Slider;
        _editorBlurSlider.ValueChanged += EditorBaseAdjustment_Changed;
        adjustments.Children.Add(blur.Panel);

        root.Children.Add(adjustments);
        root.Children.Add(CreateEditorDivider());
        root.Children.Add(CreateEditorSectionHeading("MARKUP", "Paint, erase, or place a text label over the image. Undo and redo track markup changes."));

        var markupRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        _editorPaintButton = CreateSmallEditorButton("Paint", EditorPaintTool_Click);
        _editorEraseButton = CreateSmallEditorButton("Erase", EditorEraseTool_Click);
        _editorTextButton = CreateSmallEditorButton("Text", EditorTextTool_Click);
        markupRow.Children.Add(_editorPaintButton);
        markupRow.Children.Add(_editorEraseButton);
        markupRow.Children.Add(_editorTextButton);

        _editorPaintColorBox = new ComboBox
        {
            Width = 115,
            Height = 32,
            Margin = new Thickness(8, 0, 8, 6)
        };
        foreach (string color in new[] { "White", "Black", "Red", "Yellow", "Green", "Blue", "Purple", "Orange" })
            _editorPaintColorBox.Items.Add(color);
        _editorPaintColorBox.SelectedIndex = 0;
        _editorPaintColorBox.SelectionChanged += (_, _) => UpdateEditorDrawingAttributes();
        markupRow.Children.Add(_editorPaintColorBox);

        var brushSize = CreateEditorSlider("Brush", 1, 50, 5);
        _editorBrushSizeSlider = brushSize.Slider;
        _editorBrushSizeSlider.Width = 115;
        _editorBrushSizeSlider.ValueChanged += (_, _) => UpdateEditorDrawingAttributes();
        brushSize.Panel.Width = 150;
        markupRow.Children.Add(brushSize.Panel);

        var clearMarkup = CreateSmallEditorButton("Clear markup", EditorClearMarkup_Click);
        markupRow.Children.Add(clearMarkup);
        root.Children.Add(markupRow);

        card.Child = root;
        return card;
    }

    private Button CreateEditorToolbarButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(6, 0, 0, 0),
            MinHeight = 34
        };
        button.Click += handler;
        return button;
    }

    private Button CreateSmallEditorButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 0, 5, 5),
            MinHeight = 27,
            FontSize = 11
        };
        button.Click += handler;
        return button;
    }

    private StackPanel CreateEditorSectionHeading(string title, string description)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText")
        });
        panel.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        return panel;
    }

    private Border CreateEditorDivider()
        => new()
        {
            Height = 1,
            Background = (Brush)FindResource("Border"),
            Margin = new Thickness(0, 14, 0, 14)
        };

    private StackPanel CreateEditorField(string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(control);
        return panel;
    }

    private (StackPanel Panel, Slider Slider) CreateEditorSlider(string label, double minimum, double maximum, double value, double tick = 1)
    {
        var panel = new StackPanel
        {
            Width = 160,
            Margin = new Thickness(0, 0, 12, 8)
        };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11
        });
        var valueText = new TextBlock
        {
            Text = Math.Round(value).ToString("0"),
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10
        };
        Grid.SetColumn(valueText, 1);
        header.Children.Add(valueText);
        panel.Children.Add(header);

        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            TickFrequency = tick,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 3, 0, 0)
        };
        slider.ValueChanged += (_, _) => valueText.Text = Math.Round(slider.Value).ToString("0");
        panel.Children.Add(slider);
        return (panel, slider);
    }

    private void EditorNav_Click(object sender, RoutedEventArgs e)
    {
        if (_editorPage is null) return;
        if (_logReaderPage is not null) _logReaderPage.Visibility = Visibility.Collapsed;
        if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Collapsed;
        ShowPage(_editorPage, "Editor", "Create RP screenshot chat overlays and apply lightweight image edits");
    }

    private void ScheduleEditorChatRender()
    {
        if (_editorChatRenderTimer is null)
        {
            RenderEditorChatOverlay();
            return;
        }
        _editorChatRenderTimer.Stop();
        _editorChatRenderTimer.Start();
    }

    private void EditorBaseAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_editorBaseAdjustTimer is null)
        {
            ApplyEditorImageAdjustments();
            return;
        }
        _editorBaseAdjustTimer.Stop();
        _editorBaseAdjustTimer.Start();
    }

    private void EditorPaste_Click(object sender, RoutedEventArgs e)
    {
        if (_editorInput is null) return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            _editorExactChatColorsV068 = new Dictionary<int, ChatColorLineRecord>();
            _editorInput.SelectedText = Clipboard.GetText();
            _editorInput.Focus();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to paste text into Editor.", ex);
            SetEditorStatus("Clipboard text could not be pasted.");
        }
    }

    private async void EditorImportText_Click(object sender, RoutedEventArgs e)
    {
        if (_editorInput is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "Import chat text",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            string text = await File.ReadAllTextAsync(dialog.FileName);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            _editorExactChatColorsV068 = await ChatColorSidecarService.MatchLinesAsync(
                dialog.FileName,
                lines,
                CancellationToken.None);
            _editorInput.Text = text;
            _editorInput.CaretIndex = 0;
            string colors = _editorExactChatColorsV068.Count > 0
                ? $" · {_editorExactChatColorsV068.Count:N0} line(s) with exact FiveM colors"
                : string.Empty;
            SetEditorStatus($"Imported {Path.GetFileName(dialog.FileName)}{colors}.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to import Editor chat text.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to import text", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditorClearInput_Click(object sender, RoutedEventArgs e)
    {
        _editorExactChatColorsV068 = new Dictionary<int, ChatColorLineRecord>();
        if (_editorInput is not null) _editorInput.Clear();
    }

    private void SetEditorStatus(string text)
    {
        if (_editorStatusText is not null) _editorStatusText.Text = text;
    }
}
