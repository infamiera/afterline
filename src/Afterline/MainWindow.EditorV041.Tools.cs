using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private FrameworkElement BuildEditorV041ColorsPanel()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText("Automatic is recommended. Select a line only when you want to override its entire color manually."));

        _editorLineColorList = new ListBox
        {
            MinHeight = 220,
            MaxHeight = 360,
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Background = (Brush)FindResource("Bg"),
            Padding = new Thickness(4)
        };
        _editorLineColorList.SelectionChanged += EditorLineColorList_SelectionChanged;
        content.Children.Add(_editorLineColorList);

        _editorLineColorHint = new TextBlock
        {
            Text = "Select a line to see its detected style.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        };
        content.Children.Add(_editorLineColorHint);

        _editorLineColorPresetBox = new ComboBox { Height = 34 };
        _editorLineColorPresetBox.Items.Add(new EditorPresetChoice(null, "Auto (recommended)", null));
        foreach (EditorColorPreset preset in EditorChatFormatter.ColorPresets)
            _editorLineColorPresetBox.Items.Add(new EditorPresetChoice(preset.Key, preset.Name, preset.Color));
        _editorLineColorPresetBox.SelectedIndex = 0;
        _editorLineColorPresetBox.SelectionChanged += EditorLineColorPreset_SelectionChanged;
        content.Children.Add(CreateEditorField("Selected line color", _editorLineColorPresetBox));

        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        buttons.Children.Add(CreateSmallEditorButton("Use Auto", EditorUseAutoLineColor_Click));
        buttons.Children.Add(CreateSmallEditorButton("Reset All", EditorResetAllLineColors_Click));
        content.Children.Add(buttons);

        content.Children.Add(EditorSubtleNote("Mixed automatic lines can contain several colors at once (for example an info tag, money amount and command). A manual override intentionally makes the whole selected line one color."));
        return WrapEditorToolPanel(content);
    }

    private FrameworkElement BuildEditorV041TextEffectsPanel()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText("Optional readability effects for generated chat text. Both effects are applied before export and do not affect the loaded screenshot."));

        _editorStrokeEnabledCheck = new CheckBox
        {
            Content = "Text stroke",
            IsChecked = false,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 8)
        };
        _editorStrokeEnabledCheck.Checked += EditorTextEffectChanged;
        _editorStrokeEnabledCheck.Unchecked += EditorTextEffectChanged;
        content.Children.Add(_editorStrokeEnabledCheck);

        var stroke = CreateEditorV041Slider("Stroke width", 0, 5, 1, 0.5);
        _editorStrokeWidthSlider = stroke.Slider;
        _editorStrokeWidthSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(stroke.Panel);

        _editorStrokeColorBox = CreateEditorEffectColorBox("Black");
        _editorStrokeColorBox.SelectionChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(CreateEditorField("Stroke color", _editorStrokeColorBox));

        content.Children.Add(CreateEditorDivider());
        _editorShadowEnabledCheck = new CheckBox
        {
            Content = "Drop shadow",
            IsChecked = false,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 8)
        };
        _editorShadowEnabledCheck.Checked += EditorTextEffectChanged;
        _editorShadowEnabledCheck.Unchecked += EditorTextEffectChanged;
        content.Children.Add(_editorShadowEnabledCheck);

        var opacity = CreateEditorV041Slider("Shadow opacity", 0, 100, 75, 5);
        _editorShadowOpacitySlider = opacity.Slider;
        _editorShadowOpacitySlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(opacity.Panel);

        var blur = CreateEditorV041Slider("Shadow softness", 0, 20, 5);
        _editorShadowBlurSlider = blur.Slider;
        _editorShadowBlurSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(blur.Panel);

        var offsetX = CreateEditorV041Slider("Shadow X", -12, 12, 2);
        _editorShadowOffsetXSlider = offsetX.Slider;
        _editorShadowOffsetXSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(offsetX.Panel);

        var offsetY = CreateEditorV041Slider("Shadow Y", -12, 12, 2);
        _editorShadowOffsetYSlider = offsetY.Slider;
        _editorShadowOffsetYSlider.ValueChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(offsetY.Panel);

        _editorShadowColorBox = CreateEditorEffectColorBox("Black");
        _editorShadowColorBox.SelectionChanged += (_, _) => ScheduleEditorChatRender();
        content.Children.Add(CreateEditorField("Shadow color", _editorShadowColorBox));

        var reset = CreateSmallEditorButton("Reset Text Effects", EditorResetTextEffects_Click);
        reset.Margin = new Thickness(0, 9, 0, 0);
        content.Children.Add(reset);
        return WrapEditorToolPanel(content);
    }

    private FrameworkElement BuildEditorV041ImagePanel()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText("Load a screenshot only when you want to compose the chat directly over an image. Chat-only PNGs work without one."));

        var imageButtons = new WrapPanel();
        imageButtons.Children.Add(CreateSmallEditorButton("Load Image", EditorLoadImage_Click));
        _editorRemoveImageButton = CreateSmallEditorButton("Remove Image", EditorRemoveImage_Click);
        _editorRemoveImageButton.IsEnabled = _editorBaseOriginal is not null;
        imageButtons.Children.Add(_editorRemoveImageButton);
        content.Children.Add(imageButtons);

        _editorBackgroundBox = new ComboBox { Height = 34 };
        _editorBackgroundBox.Items.Add("Black");
        _editorBackgroundBox.Items.Add("Transparent");
        _editorBackgroundBox.SelectedIndex = 0;
        _editorBackgroundBox.SelectionChanged += (_, _) => UpdateEditorCanvasSize();
        content.Children.Add(CreateEditorField("Canvas background", _editorBackgroundBox));

        var x = CreateEditorV041Slider("Chat horizontal position", 0, 600, 0, 5);
        _editorChatXSlider = x.Slider;
        _editorChatXSlider.ValueChanged += (_, _) => UpdateEditorCanvasSize();
        content.Children.Add(x.Panel);

        var y = CreateEditorV041Slider("Chat vertical position", 0, 400, 0, 5);
        _editorChatYSlider = y.Slider;
        _editorChatYSlider.ValueChanged += (_, _) => UpdateEditorCanvasSize();
        content.Children.Add(y.Panel);

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock { Text = "IMAGE TONE", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("MutedText"), Margin = new Thickness(0, 0, 0, 8) });

        var brightness = CreateEditorV041Slider("Brightness", -100, 100, 0);
        _editorBrightnessSlider = brightness.Slider;
        _editorBrightnessSlider.ValueChanged += EditorBaseAdjustment_Changed;
        content.Children.Add(brightness.Panel);

        var contrast = CreateEditorV041Slider("Contrast", -100, 100, 0);
        _editorContrastSlider = contrast.Slider;
        _editorContrastSlider.ValueChanged += EditorBaseAdjustment_Changed;
        content.Children.Add(contrast.Panel);

        var saturation = CreateEditorV041Slider("Saturation", -100, 100, 0);
        _editorSaturationSlider = saturation.Slider;
        _editorSaturationSlider.ValueChanged += EditorBaseAdjustment_Changed;
        content.Children.Add(saturation.Panel);

        var warmth = CreateEditorV041Slider("Warmth", -100, 100, 0);
        _editorWarmthSlider = warmth.Slider;
        _editorWarmthSlider.ValueChanged += EditorBaseAdjustment_Changed;
        content.Children.Add(warmth.Panel);

        var tint = CreateEditorV041Slider("Tint", -100, 100, 0);
        _editorTintSlider = tint.Slider;
        _editorTintSlider.ValueChanged += EditorBaseAdjustment_Changed;
        content.Children.Add(tint.Panel);

        var blur = CreateEditorV041Slider("Image blur", 0, 20, 0);
        _editorBlurSlider = blur.Slider;
        _editorBlurSlider.ValueChanged += EditorBaseAdjustment_Changed;
        content.Children.Add(blur.Panel);

        content.Children.Add(CreateSmallEditorButton("Reset Image Tone", EditorResetImageTone_Click));
        return WrapEditorToolPanel(content);
    }

    private FrameworkElement BuildEditorV041MarkupPanel()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText("Simple markup only: paint, erase, or place a text label. Undo and redo are always available at the top."));

        var tools = new WrapPanel();
        _editorPaintButton = CreateSmallEditorButton("Paint", EditorPaintTool_Click);
        _editorEraseButton = CreateSmallEditorButton("Erase", EditorEraseTool_Click);
        _editorTextButton = CreateSmallEditorButton("Text", EditorTextTool_Click);
        tools.Children.Add(_editorPaintButton);
        tools.Children.Add(_editorEraseButton);
        tools.Children.Add(_editorTextButton);
        content.Children.Add(tools);

        _editorPaintColorBox = new ComboBox { Height = 34 };
        foreach (string color in new[] { "White", "Black", "Red", "Yellow", "Green", "Blue", "Purple", "Orange" })
            _editorPaintColorBox.Items.Add(color);
        _editorPaintColorBox.SelectedIndex = 0;
        _editorPaintColorBox.SelectionChanged += (_, _) => UpdateEditorDrawingAttributes();
        content.Children.Add(CreateEditorField("Paint / text color", _editorPaintColorBox));

        var brush = CreateEditorV041Slider("Brush size", 1, 50, 5);
        _editorBrushSizeSlider = brush.Slider;
        _editorBrushSizeSlider.ValueChanged += (_, _) => UpdateEditorDrawingAttributes();
        content.Children.Add(brush.Panel);

        content.Children.Add(CreateSmallEditorButton("Clear Markup", EditorClearMarkup_Click));
        return WrapEditorToolPanel(content);
    }

    private FrameworkElement BuildEditorV041ExportPanel()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText("Preview zoom is only for viewing. The exported PNG always uses the composition's original pixel dimensions."));
        var copy = CreateSmallEditorButton("Copy Image", EditorCopyImage_Click);
        copy.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Children.Add(copy);
        var fullscreen = CreateSmallEditorButton("Full Screen Preview", EditorFullscreenPreview_Click);
        fullscreen.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Children.Add(fullscreen);
        var export = CreateSmallEditorButton("Export PNG", EditorExportPng_Click);
        export.Style = (Style)FindResource("PrimaryButton");
        export.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Children.Add(export);
        content.Children.Add(EditorSubtleNote("Tip: use Fit in the preview when you want to check the complete image before exporting."));
        return WrapEditorToolPanel(content);
    }

}
