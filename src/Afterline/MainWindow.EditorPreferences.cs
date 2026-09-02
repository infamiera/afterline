using System.Windows;
using System.Windows.Controls;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _editorPreferencesInitialized;

    private void EnsureEditorPreferences()
    {
        if (_editorPreferencesInitialized || _editorPage is null) return;
        _editorPreferencesInitialized = true;

        Border? header = _editorPage.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);

        if (header?.Child is Grid headerGrid)
        {
            WrapPanel? actions = headerGrid.Children
                .OfType<WrapPanel>()
                .FirstOrDefault(panel => Grid.GetColumn(panel) == 1);

            if (actions is not null)
            {
                var reset = CreateEditorHeaderButton("Reset", EditorResetPreferences_Click);
                reset.ToolTip = "Reset reusable Editor controls to their defaults without clearing your image, chat text, markup, crop, or saved settings.";

                var save = CreateEditorHeaderButton("Save Settings", EditorSavePreferences_Click);
                save.ToolTip = "Save your preferred Editor controls locally so they are restored next time Afterline starts.";

                int insertAt = Math.Max(0, actions.Children.Count - 1);
                actions.Children.Insert(insertAt, reset);
                actions.Children.Insert(insertAt + 1, save);
            }
        }

        ApplyEditorPreferences(_settings.Editor ?? new EditorPreferences());
    }

    private void EditorSavePreferences_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Editor = CaptureEditorPreferences();
            _settingsService.Save(_settings);
            SetEditorStatus("Editor settings saved locally. They will be restored on the next session.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save Editor settings.", ex);
            SetEditorStatus("Editor settings could not be saved.");
        }
    }

    private void EditorResetPreferences_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorPreferences(new EditorPreferences());
        SetEditorStatus("Editor controls reset to defaults. Saved settings were not changed.");
    }

    private EditorPreferences CaptureEditorPreferences()
    {
        TryReadEditorOutputSizeV060(out int outputWidth, out int outputHeight);
        return new EditorPreferences
        {
            ProjectsFolder = GetEditorProjectsFolderV070(createDirectory: false),
            ProjectAutosaveMinutes = _settings.Editor.ProjectAutosaveMinutes,
            Font = _editorFontBox?.SelectedItem?.ToString() ?? "Arial Bold",
            FontSize = _editorFontSizeSlider?.Value ?? 18,
            LineSpacing = _editorLineSpacingSlider?.Value ?? 1,
            ChatWidth = _editorChatWidthSlider?.Value ?? 900,
            ChatTextAlignment = _editorChatTextAlignmentV063.ToString(),
            ShowTimestamps = _editorShowTimestampsCheck?.IsChecked == true,
            CanvasBackground = _editorBackgroundBox?.SelectedItem?.ToString() ?? "Transparent",
            ChatHorizontalPosition = _editorChatXSlider?.Value ?? 0,
            ChatVerticalPosition = _editorChatYSlider?.Value ?? 0,
            ExportKeybind = _settings.Editor.ExportKeybind,
            UndoKeybind = _settings.Editor.UndoKeybind,
            RedoKeybind = _settings.Editor.RedoKeybind,
            FullscreenKeybind = _settings.Editor.FullscreenKeybind,
            RulerKeybind = _settings.Editor.RulerKeybind,
            StrokeEnabled = _editorStrokeEnabledCheck?.IsChecked == true,
            StrokeWidth = _editorStrokeWidthSlider?.Value ?? 1,
            StrokeColor = _editorStrokeColorBox?.SelectedItem?.ToString() ?? "Black",
            ShadowEnabled = _editorShadowEnabledCheck?.IsChecked == true,
            ShadowOpacity = _editorShadowOpacitySlider?.Value ?? 75,
            ShadowSoftness = _editorShadowBlurSlider?.Value ?? 5,
            ShadowX = _editorShadowOffsetXSlider?.Value ?? 2,
            ShadowY = _editorShadowOffsetYSlider?.Value ?? 2,
            ShadowColor = _editorShadowColorBox?.SelectedItem?.ToString() ?? "Black",
            PaintColor = _editorLayerPaintColorV068?.SelectedItem?.ToString()
                         ?? _editorPaintColorBox?.SelectedItem?.ToString()
                         ?? "White",
            BrushSize = _editorLayerBrushSizeV068?.Value ?? _editorBrushSizeSlider?.Value ?? 5,
            ImageBrightness = _editorBrightnessSlider?.Value ?? 0,
            ImageContrast = _editorContrastSlider?.Value ?? 0,
            ImageSaturation = _editorSaturationSlider?.Value ?? 0,
            ImageWarmth = _editorWarmthSlider?.Value ?? 0,
            ImageTint = _editorTintSlider?.Value ?? 0,
            ImageBlur = _editorBlurSlider?.Value ?? 0,
            OutputPreset = _editorOutputPresetBox?.SelectedItem?.ToString() ?? "Original",
            OutputWidth = outputWidth,
            OutputHeight = outputHeight,
            OutputLockAspect = _editorOutputLockAspectCheck?.IsChecked != false
        };
    }

    private void ApplyEditorPreferences(EditorPreferences preferences)
    {
        SetEditorComboSelection(_editorFontBox, preferences.Font, "Arial Bold");
        SetEditorSlider(_editorFontSizeSlider, preferences.FontSize);
        SetEditorSlider(_editorLineSpacingSlider, preferences.LineSpacing);
        SetEditorSlider(_editorChatWidthSlider, preferences.ChatWidth);
        _editorChatTextAlignmentV063 = ParseEditorTextAlignmentV063(preferences.ChatTextAlignment);
        RefreshEditorTextAlignmentButtonsV063();
        if (_editorShowTimestampsCheck is not null) _editorShowTimestampsCheck.IsChecked = preferences.ShowTimestamps;

        SetEditorComboSelection(_editorBackgroundBox, preferences.CanvasBackground, "Transparent");
        SetEditorSlider(_editorChatXSlider, preferences.ChatHorizontalPosition);
        SetEditorSlider(_editorChatYSlider, preferences.ChatVerticalPosition);

        if (_editorStrokeEnabledCheck is not null) _editorStrokeEnabledCheck.IsChecked = preferences.StrokeEnabled;
        SetEditorSlider(_editorStrokeWidthSlider, preferences.StrokeWidth);
        SetEditorComboSelection(_editorStrokeColorBox, preferences.StrokeColor, "Black");

        if (_editorShadowEnabledCheck is not null) _editorShadowEnabledCheck.IsChecked = preferences.ShadowEnabled;
        SetEditorSlider(_editorShadowOpacitySlider, preferences.ShadowOpacity);
        SetEditorSlider(_editorShadowBlurSlider, preferences.ShadowSoftness);
        SetEditorSlider(_editorShadowOffsetXSlider, preferences.ShadowX);
        SetEditorSlider(_editorShadowOffsetYSlider, preferences.ShadowY);
        SetEditorComboSelection(_editorShadowColorBox, preferences.ShadowColor, "Black");

        SetEditorComboSelection(_editorPaintColorBox, preferences.PaintColor, "White");
        SetEditorSlider(_editorBrushSizeSlider, preferences.BrushSize);
        SetEditorComboSelection(_editorLayerPaintColorV068, preferences.PaintColor, "White");
        SetEditorSlider(_editorLayerBrushSizeV068, preferences.BrushSize);

        SetEditorSlider(_editorBrightnessSlider, preferences.ImageBrightness);
        SetEditorSlider(_editorContrastSlider, preferences.ImageContrast);
        SetEditorSlider(_editorSaturationSlider, preferences.ImageSaturation);
        SetEditorSlider(_editorWarmthSlider, preferences.ImageWarmth);
        SetEditorSlider(_editorTintSlider, preferences.ImageTint);
        SetEditorSlider(_editorBlurSlider, preferences.ImageBlur);
        ApplyEditorOutputPreferencesV060(preferences);

        UpdateEditorDrawingAttributes();
        ScheduleEditorChatRender();
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
        UpdateEditorMediaControlsV060();
    }

    private static TextAlignment ParseEditorTextAlignmentV063(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            _ => TextAlignment.Left
        };

    private static void SetEditorSlider(Slider? slider, double value)
    {
        if (slider is null) return;
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
    }

    private static void SetEditorComboSelection(ComboBox? box, string? value, string fallback)
    {
        if (box is null) return;
        string requested = string.IsNullOrWhiteSpace(value) ? fallback : value;
        object? match = box.Items.Cast<object>()
            .FirstOrDefault(item => string.Equals(item?.ToString(), requested, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            match = box.Items.Cast<object>()
                .FirstOrDefault(item => string.Equals(item?.ToString(), fallback, StringComparison.OrdinalIgnoreCase));
        }
        if (match is not null) box.SelectedItem = match;
    }
}
