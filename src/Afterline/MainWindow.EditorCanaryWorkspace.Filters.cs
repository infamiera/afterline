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
    private sealed record CanaryFilterRenderSettingsV070(
        string Preset,
        double Strength,
        double Brightness,
        double Contrast,
        double Saturation,
        double Temperature,
        double Fade,
        double Blur,
        bool[]? SelectionMask,
        int SelectionWidth,
        int SelectionHeight);

    private int _editorFilterPreviewVersionV070;
    private int _editorFilterPreviewRenderCountV070;
    private string? _editorLayerFilterTargetIdV071;
    private BitmapSource? _editorLayerFilterCommittedV071;
    private BitmapSource? _editorLayerFilterPreviewV071;
    private TextBlock? _editorFilterTargetLabelV071;

    private FrameworkElement BuildFilterToolPanelCanary()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Adjust the selected image layer non-destructively. With no added image layer selected, changes target the Base Image; Base Image selections still limit the affected area."));

        _editorFilterTargetLabelV071 = new TextBlock
        {
            Text = "Target · Base Image",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Accent"),
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(_editorFilterTargetLabelV071);

        _editorFilterPresetCanary = new ComboBox { Height = 34 };
        foreach (string preset in new[] { "None", "Warm", "Cool", "Black & White", "Faded", "Cinematic", "High Contrast" })
            _editorFilterPresetCanary.Items.Add(preset);
        _editorFilterPresetCanary.SelectedIndex = 0;
        _editorFilterPresetCanary.SelectionChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(CreateEditorField("Filter preset", _editorFilterPresetCanary));

        var strength = CreateEditorV041Slider("Filter strength", 0, 100, 100, 5);
        _editorFilterStrengthCanary = strength.Slider;
        _editorFilterStrengthCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(strength.Panel);

        var brightness = CreateEditorV041Slider("Brightness", -100, 100, 0);
        _editorFilterBrightnessCanary = brightness.Slider;
        _editorFilterBrightnessCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(brightness.Panel);

        var contrast = CreateEditorV041Slider("Contrast", -100, 100, 0);
        _editorFilterContrastCanary = contrast.Slider;
        _editorFilterContrastCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(contrast.Panel);

        var saturation = CreateEditorV041Slider("Saturation", -100, 100, 0);
        _editorFilterSaturationCanary = saturation.Slider;
        _editorFilterSaturationCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(saturation.Panel);

        var temperature = CreateEditorV041Slider("Temperature (cool ↔ warm)", -100, 100, 0);
        _editorFilterTemperatureCanary = temperature.Slider;
        _editorFilterTemperatureCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(temperature.Panel);

        var fade = CreateEditorV041Slider("Fade", 0, 100, 0);
        _editorFilterFadeCanary = fade.Slider;
        _editorFilterFadeCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(fade.Panel);

        var blur = CreateEditorV041Slider("Blur", 0, 16, 0);
        _editorFilterBlurCanary = blur.Slider;
        _editorFilterBlurCanary.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(blur.Panel);

        var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        var apply = CreateSmallEditorButton("Apply Changes", (_, _) => ApplyFilterWithHistoryCanaryV2());
        apply.ToolTip = "Commit the current preview to the named target. Base Image selections limit the affected area; added image layers are adjusted independently.";
        var revert = CreateSmallEditorButton("Revert Preview", (_, _) => RevertCanaryFilterPreview());
        revert.ToolTip = "Discard the current filter preview and return to the last applied image.";
        actions.Children.Add(apply);
        actions.Children.Add(revert);
        content.Children.Add(actions);

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock
        {
            Text = "IMAGE TRANSFORM",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 7)
        });
        var transforms = new WrapPanel();
        transforms.Children.Add(CreateSmallEditorButton("Rotate Left", (_, _) => TransformStillImageCanary("rotate-left")));
        transforms.Children.Add(CreateSmallEditorButton("Rotate Right", (_, _) => TransformStillImageCanary("rotate-right")));
        transforms.Children.Add(CreateSmallEditorButton("Flip H", (_, _) => TransformStillImageCanary("flip-h")));
        transforms.Children.Add(CreateSmallEditorButton("Flip V", (_, _) => TransformStillImageCanary("flip-v")));
        content.Children.Add(transforms);

        content.Children.Add(EditorSubtleNote(
            "Added image layers receive their own preview, commit and Layer Undo history. Selected-area filters remain specific to the Base Image; animated GIFs keep their existing crop, chat and export workflow."));
        return WrapEditorToolPanel(content);
    }

    private void ConfigureFilterToolCanary()
    {
        ResetLegacyImageToneCanary();
        RemoveLegacyImageToneControlsCanary();

        _editorFilterTimerCanary = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
        _editorFilterTimerCanary.Tick += async (_, _) =>
        {
            _editorFilterTimerCanary.Stop();
            await ApplyCanaryFilterPreviewBackgroundV070();
        };

        foreach (Button button in FindVisualChildrenCanary<Button>(_editorPage!))
        {
            string content = button.Content?.ToString() ?? string.Empty;
            if (content.StartsWith("Load Image", StringComparison.OrdinalIgnoreCase) ||
                content.StartsWith("Remove Media", StringComparison.OrdinalIgnoreCase))
            {
                button.Click += (_, _) => Dispatcher.BeginInvoke(new Action(ResetCanaryFilterSource));
            }
        }
    }

    private void RemoveLegacyImageToneControlsCanary()
    {
        if (!_editorToolPanels.TryGetValue("image", out FrameworkElement? panel) ||
            panel is not ScrollViewer scroll ||
            scroll.Content is not StackPanel content)
            return;

        var remove = new HashSet<UIElement>();
        foreach (Slider? slider in new[]
        {
            _editorBrightnessSlider,
            _editorContrastSlider,
            _editorSaturationSlider,
            _editorWarmthSlider,
            _editorTintSlider,
            _editorBlurSlider
        })
        {
            if (slider?.Parent is UIElement parent)
                remove.Add(parent);
        }

        foreach (UIElement child in content.Children.Cast<UIElement>().ToArray())
        {
            if (remove.Contains(child))
                content.Children.Remove(child);
            else if (child is TextBlock text && string.Equals(text.Text, "IMAGE TONE", StringComparison.OrdinalIgnoreCase))
                content.Children.Remove(child);
            else if (child is Button button && string.Equals(button.Content?.ToString(), "Reset Image Tone", StringComparison.OrdinalIgnoreCase))
                content.Children.Remove(child);
        }
    }

    private void ResetLegacyImageToneCanary()
    {
        foreach (Slider? slider in new[]
        {
            _editorBrightnessSlider,
            _editorContrastSlider,
            _editorSaturationSlider,
            _editorWarmthSlider,
            _editorTintSlider,
            _editorBlurSlider
        })
        {
            if (slider is not null) slider.Value = 0;
        }
    }

    private void ScheduleCanaryFilterPreview()
    {
        if (_editorFilterUiUpdatingCanary || _editorFilterTimerCanary is null) return;
        _editorFilterPreviewVersionV070++;
        _editorFilterTimerCanary.Stop();
        _editorFilterTimerCanary.Start();
    }

    private void ResetCanaryFilterSource()
    {
        _editorFilterPreviewVersionV070++;
        _editorFilterTimerCanary?.Stop();
        ResetLayerFilterTargetV071(restoreVisual: true);
        _editorFilterPreviewCanary = null;
        _editorFilterCommittedCanary = null;
        _editorFilterTrackedMediaCanary = _editorLoadedMediaPath;
        ClearSelectionCanarySilently();
        if (_editorBaseOriginal is not null && !EditorHasAnimatedGifV060)
            _editorFilterCommittedCanary = _editorBaseOriginal;
        ResetCanaryFilterControls();
    }

    private bool EnsureCanaryFilterSource()
    {
        EditorImageLayerV067? selectedLayer = _editorSelectedImageLayerV067;
        if (selectedLayer is not null)
        {
            if (selectedLayer.IsLocked)
            {
                SetEditorStatus("Unlock the selected image layer before applying Filters & Adjustments.");
                return false;
            }

            bool targetChanged = !string.Equals(
                _editorLayerFilterTargetIdV071,
                selectedLayer.Id,
                StringComparison.Ordinal);
            bool sourceChanged = _editorLayerFilterCommittedV071 is null ||
                (!ReferenceEquals(selectedLayer.Bitmap, _editorLayerFilterCommittedV071) &&
                 !ReferenceEquals(selectedLayer.Bitmap, _editorLayerFilterPreviewV071));
            if (targetChanged || sourceChanged)
            {
                ResetLayerFilterTargetV071(restoreVisual: true);
                _editorLayerFilterTargetIdV071 = selectedLayer.Id;
                _editorLayerFilterCommittedV071 = selectedLayer.Bitmap;
                _editorLayerFilterPreviewV071 = null;
            }

            UpdateFilterTargetLabelV071();
            return true;
        }

        if (_editorLayerFilterTargetIdV071 is not null)
            ResetLayerFilterTargetV071(restoreVisual: true);
        UpdateFilterTargetLabelV071();

        if (_editorBaseOriginal is null)
        {
            SetEditorStatus("Load a still screenshot before using Filters & Adjustments.");
            return false;
        }
        if (EditorHasAnimatedGifV060)
        {
            SetEditorStatus("Selected-area Filters & Adjustments currently target still screenshots. GIF editing remains available through crop, chat and export tools.");
            return false;
        }

        bool pathChanged = !string.Equals(_editorFilterTrackedMediaCanary, _editorLoadedMediaPath, StringComparison.Ordinal);
        bool currentIsPreview = ReferenceEquals(_editorBaseOriginal, _editorFilterPreviewCanary);
        bool currentIsCommitted = ReferenceEquals(_editorBaseOriginal, _editorFilterCommittedCanary);

        if (_editorFilterCommittedCanary is null || pathChanged || (!currentIsPreview && !currentIsCommitted))
        {
            _editorFilterTrackedMediaCanary = _editorLoadedMediaPath;
            // Loaded and generated BitmapSources are immutable in the Editor. Keep
            // the source reference until an edit is committed instead of copying
            // every pixel during the first interaction.
            _editorFilterCommittedCanary = _editorBaseOriginal;
            _editorFilterPreviewCanary = null;
            ClearSelectionCanarySilently();
        }
        return true;
    }

    private void ApplyCanaryFilterPreview()
    {
        if (!EnsureCanaryFilterSource())
            return;

        EditorImageLayerV067? layer = GetLayerFilterTargetV071();
        BitmapSource? committed = layer is null
            ? _editorFilterCommittedCanary
            : _editorLayerFilterCommittedV071;
        if (committed is null) return;

        try
        {
            BitmapSource preview = BuildFilteredBitmapCanary(
                committed,
                CaptureCanaryFilterRenderSettingsV070());
            ApplyFilterPreviewToTargetV071(layer, preview);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to preview Canary image filters.", ex);
            SetEditorStatus("Filter preview could not be rendered.");
        }
    }

    private async Task ApplyCanaryFilterPreviewBackgroundV070()
    {
        if (!EnsureCanaryFilterSource())
            return;

        EditorImageLayerV067? layer = GetLayerFilterTargetV071();
        BitmapSource? committed = layer is null
            ? _editorFilterCommittedCanary
            : _editorLayerFilterCommittedV071;
        if (committed is null) return;

        BitmapSource source = committed;
        string? targetId = layer?.Id;
        if (!source.IsFrozen)
        {
            ApplyCanaryFilterPreview();
            return;
        }

        CanaryFilterRenderSettingsV070 settings = CaptureCanaryFilterRenderSettingsV070();
        int version = _editorFilterPreviewVersionV070;
        _editorFilterPreviewRenderCountV070++;
        try
        {
            BitmapSource preview = await Task.Run(() =>
                BuildFilteredBitmapCanary(source, settings));
            if (version != _editorFilterPreviewVersionV070 ||
                !string.Equals(targetId, GetLayerFilterTargetV071()?.Id, StringComparison.Ordinal) ||
                !ReferenceEquals(source, targetId is null
                    ? _editorFilterCommittedCanary
                    : _editorLayerFilterCommittedV071))
                return;

            ApplyFilterPreviewToTargetV071(layer, preview);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to render the background Canary filter preview.", ex);
            SetEditorStatus("Filter preview could not be rendered.");
        }
        finally
        {
            _editorFilterPreviewRenderCountV070 = Math.Max(0, _editorFilterPreviewRenderCountV070 - 1);
        }
    }

    private CanaryFilterRenderSettingsV070 CaptureCanaryFilterRenderSettingsV070()
    {
        bool targetLayer = GetLayerFilterTargetV071() is not null;
        return new(
            _editorFilterPresetCanary?.SelectedItem?.ToString() ?? "None",
            (_editorFilterStrengthCanary?.Value ?? 100) / 100.0,
            _editorFilterBrightnessCanary?.Value ?? 0,
            _editorFilterContrastCanary?.Value ?? 0,
            _editorFilterSaturationCanary?.Value ?? 0,
            _editorFilterTemperatureCanary?.Value ?? 0,
            _editorFilterFadeCanary?.Value ?? 0,
            _editorFilterBlurCanary?.Value ?? 0,
            targetLayer ? null : _editorSelectionMaskCanary?.ToArray(),
            targetLayer ? 0 : _editorSelectionWidthCanary,
            targetLayer ? 0 : _editorSelectionHeightCanary);
    }

    private static BitmapSource BuildFilteredBitmapCanary(
        BitmapSource source,
        CanaryFilterRenderSettingsV070 settings)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] original = new byte[stride * height];
        converted.CopyPixels(original, stride, 0);
        byte[] adjusted = (byte[])original.Clone();

        double strength = settings.Strength;
        string preset = settings.Preset;
        double brightness = settings.Brightness;
        double contrast = settings.Contrast;
        double saturation = settings.Saturation;
        double temperature = settings.Temperature;
        double fade = settings.Fade;

        switch (preset)
        {
            case "Warm":
                temperature += 48 * strength;
                saturation += 8 * strength;
                break;
            case "Cool":
                temperature -= 48 * strength;
                contrast += 5 * strength;
                break;
            case "Black & White":
                saturation -= 100 * strength;
                contrast += 8 * strength;
                break;
            case "Faded":
                saturation -= 38 * strength;
                contrast -= 20 * strength;
                brightness += 7 * strength;
                fade += 35 * strength;
                break;
            case "Cinematic":
                contrast += 24 * strength;
                saturation -= 10 * strength;
                temperature += 10 * strength;
                fade += 8 * strength;
                break;
            case "High Contrast":
                contrast += 38 * strength;
                saturation += 8 * strength;
                break;
        }

        double contrastScale = 1.0 + contrast / 100.0;
        double saturationScale = Math.Max(0, 1.0 + saturation / 100.0);
        double brightnessOffset = brightness * 2.0;
        double temperatureOffset = temperature * 0.75;
        double fadeAmount = Math.Clamp(fade / 100.0, 0, 1);

        bool useSelection = settings.SelectionMask is not null &&
                            settings.SelectionWidth == width &&
                            settings.SelectionHeight == height;

        for (int pixel = 0, i = 0; pixel < width * height; pixel++, i += 4)
        {
            if (useSelection && !settings.SelectionMask![pixel]) continue;

            double b = original[i];
            double g = original[i + 1];
            double r = original[i + 2];

            r = (r - 128) * contrastScale + 128 + brightnessOffset;
            g = (g - 128) * contrastScale + 128 + brightnessOffset;
            b = (b - 128) * contrastScale + 128 + brightnessOffset;

            double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            r = luminance + (r - luminance) * saturationScale;
            g = luminance + (g - luminance) * saturationScale;
            b = luminance + (b - luminance) * saturationScale;

            r += temperatureOffset;
            b -= temperatureOffset;

            if (fadeAmount > 0)
            {
                double lifted = 118 + (luminance - 118) * 0.72;
                r = r * (1 - fadeAmount) + lifted * fadeAmount;
                g = g * (1 - fadeAmount) + lifted * fadeAmount;
                b = b * (1 - fadeAmount) + lifted * fadeAmount;
            }

            adjusted[i] = ClampEditorByte(b);
            adjusted[i + 1] = ClampEditorByte(g);
            adjusted[i + 2] = ClampEditorByte(r);
        }

        int blurRadius = (int)Math.Round(settings.Blur);
        if (blurRadius > 0)
        {
            byte[] blurred = BoxBlurCanary(adjusted, width, height, stride, Math.Clamp(blurRadius, 1, 16));
            for (int pixel = 0, i = 0; pixel < width * height; pixel++, i += 4)
            {
                if (useSelection && !settings.SelectionMask![pixel]) continue;
                adjusted[i] = blurred[i];
                adjusted[i + 1] = blurred[i + 1];
                adjusted[i + 2] = blurred[i + 2];
            }
        }

        var bitmap = BitmapSource.Create(
            width, height,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32, null, adjusted, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] BoxBlurCanary(byte[] source, int width, int height, int stride, int radius)
    {
        byte[] horizontal = new byte[source.Length];
        byte[] output = new byte[source.Length];
        int size = radius * 2 + 1;

        for (int y = 0; y < height; y++)
        {
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int x = Math.Clamp(k, 0, width - 1);
                    sum += source[y * stride + x * 4 + c];
                }
                for (int x = 0; x < width; x++)
                {
                    horizontal[y * stride + x * 4 + c] = (byte)(sum / size);
                    int removeX = Math.Clamp(x - radius, 0, width - 1);
                    int addX = Math.Clamp(x + radius + 1, 0, width - 1);
                    sum -= source[y * stride + removeX * 4 + c];
                    sum += source[y * stride + addX * 4 + c];
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int y = Math.Clamp(k, 0, height - 1);
                    sum += horizontal[y * stride + x * 4 + c];
                }
                for (int y = 0; y < height; y++)
                {
                    output[y * stride + x * 4 + c] = (byte)(sum / size);
                    int removeY = Math.Clamp(y - radius, 0, height - 1);
                    int addY = Math.Clamp(y + radius + 1, 0, height - 1);
                    sum -= horizontal[removeY * stride + x * 4 + c];
                    sum += horizontal[addY * stride + x * 4 + c];
                }
            }
        }

        return output;
    }

    private void CommitCanaryFilterPreview()
    {
        _editorFilterPreviewVersionV070++;
        EditorImageLayerV067? layer = GetLayerFilterTargetV071();
        BitmapSource? preview = layer is null
            ? _editorFilterPreviewCanary
            : _editorLayerFilterPreviewV071;
        if (preview is null || _editorFilterPreviewRenderCountV070 > 0)
        {
            ApplyCanaryFilterPreview();
            layer = GetLayerFilterTargetV071();
            preview = layer is null
                ? _editorFilterPreviewCanary
                : _editorLayerFilterPreviewV071;
            if (preview is null) return;
        }

        if (layer is not null)
        {
            BitmapSource committed = CloneBitmapCanary(preview);
            layer.Bitmap = committed;
            _editorLayerFilterCommittedV071 = committed;
            _editorLayerFilterPreviewV071 = null;
            UpdateImageLayerVisualV067(layer);
            RefreshLayerListV067(layer);
            RefreshFilterPresetGalleryV067();
        }
        else
        {
            _editorFilterCommittedCanary = CloneBitmapCanary(preview);
            _editorBaseOriginal = _editorFilterCommittedCanary;
            _editorFilterPreviewCanary = null;
            ApplyEditorImageAdjustments();
        }
        ResetCanaryFilterControls();
        SetEditorStatus(layer is not null
            ? $"Image adjustments applied to layer ‘{layer.Name}’."
            : _editorSelectionMaskCanary is null
                ? "Image adjustments applied to the Base Image."
                : "Image adjustments applied to the selected Base Image area.");
    }

    private void RevertCanaryFilterPreview()
    {
        _editorFilterPreviewVersionV070++;
        EditorImageLayerV067? layer = GetLayerFilterTargetV071();
        if (layer is not null && _editorLayerFilterCommittedV071 is not null)
        {
            layer.Image.Source = _editorLayerFilterCommittedV071;
            _editorLayerFilterPreviewV071 = null;
            ResetCanaryFilterControls();
            SetEditorStatus($"Filter preview reverted for layer ‘{layer.Name}’.");
            return;
        }
        if (_editorFilterCommittedCanary is null) return;
        _editorBaseOriginal = _editorFilterCommittedCanary;
        _editorFilterPreviewCanary = null;
        ResetCanaryFilterControls();
        ApplyEditorImageAdjustments();
        SetEditorStatus("Filter preview reverted.");
    }

    private EditorImageLayerV067? GetLayerFilterTargetV071()
        => string.IsNullOrWhiteSpace(_editorLayerFilterTargetIdV071)
            ? null
            : _editorImageLayersV067.FirstOrDefault(layer =>
                string.Equals(layer.Id, _editorLayerFilterTargetIdV071, StringComparison.Ordinal));

    private void ApplyFilterPreviewToTargetV071(EditorImageLayerV067? layer, BitmapSource preview)
    {
        if (layer is not null)
        {
            _editorLayerFilterPreviewV071 = preview;
            layer.Image.Source = preview;
            SetEditorStatus($"Previewing Filters & Adjustments on layer ‘{layer.Name}’.");
            return;
        }

        _editorFilterPreviewCanary = preview;
        _editorBaseOriginal = preview;
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
    }

    private void ResetLayerFilterTargetV071(bool restoreVisual)
    {
        EditorImageLayerV067? previous = GetLayerFilterTargetV071();
        if (restoreVisual && previous is not null)
            UpdateImageLayerVisualV067(previous);
        _editorLayerFilterTargetIdV071 = null;
        _editorLayerFilterCommittedV071 = null;
        _editorLayerFilterPreviewV071 = null;
        UpdateFilterTargetLabelV071();
    }

    private void EditorFilterTargetSelectionChangedV071()
    {
        _editorFilterPreviewVersionV070++;
        _editorFilterTimerCanary?.Stop();
        ResetLayerFilterTargetV071(restoreVisual: true);
        ResetCanaryFilterControls();
        UpdateFilterTargetLabelV071();
        RefreshFilterPresetGalleryV067();
    }

    private void UpdateFilterTargetLabelV071()
    {
        if (_editorFilterTargetLabelV071 is null) return;
        _editorFilterTargetLabelV071.Text = _editorSelectedImageLayerV067 is EditorImageLayerV067 layer
            ? $"Target · {layer.Name}"
            : "Target · Base Image";
    }

    private void ResetCanaryFilterControls()
    {
        _editorFilterUiUpdatingCanary = true;
        if (_editorFilterPresetCanary is not null) _editorFilterPresetCanary.SelectedIndex = 0;
        if (_editorFilterStrengthCanary is not null) _editorFilterStrengthCanary.Value = 100;
        if (_editorFilterBrightnessCanary is not null) _editorFilterBrightnessCanary.Value = 0;
        if (_editorFilterContrastCanary is not null) _editorFilterContrastCanary.Value = 0;
        if (_editorFilterSaturationCanary is not null) _editorFilterSaturationCanary.Value = 0;
        if (_editorFilterTemperatureCanary is not null) _editorFilterTemperatureCanary.Value = 0;
        if (_editorFilterFadeCanary is not null) _editorFilterFadeCanary.Value = 0;
        if (_editorFilterBlurCanary is not null) _editorFilterBlurCanary.Value = 0;
        _editorFilterUiUpdatingCanary = false;
    }

    private void TransformStillImageCanary(string transform)
    {
        if (_editorSelectedImageLayerV067 is not null)
        {
            SetEditorStatus("Rotate and Flip currently target the Base Image. Select Base Image in Layers first.");
            return;
        }
        if (!EnsureCanaryFilterSource() || _editorFilterCommittedCanary is null)
            return;

        BitmapSource source = _editorFilterPreviewCanary ?? _editorFilterCommittedCanary;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] src = new byte[stride * height];
        converted.CopyPixels(src, stride, 0);

        bool rotate = transform.StartsWith("rotate", StringComparison.Ordinal);
        int newWidth = rotate ? height : width;
        int newHeight = rotate ? width : height;
        int newStride = newWidth * 4;
        byte[] dst = new byte[newStride * newHeight];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int nx = x;
                int ny = y;
                switch (transform)
                {
                    case "rotate-left":
                        nx = y;
                        ny = width - 1 - x;
                        break;
                    case "rotate-right":
                        nx = height - 1 - y;
                        ny = x;
                        break;
                    case "flip-h":
                        nx = width - 1 - x;
                        break;
                    case "flip-v":
                        ny = height - 1 - y;
                        break;
                }

                int si = y * stride + x * 4;
                int di = ny * newStride + nx * 4;
                Buffer.BlockCopy(src, si, dst, di, 4);
            }
        }

        var bitmap = BitmapSource.Create(newWidth, newHeight, 96, 96, PixelFormats.Bgra32, null, dst, newStride);
        bitmap.Freeze();
        _editorFilterCommittedCanary = bitmap;
        _editorFilterPreviewCanary = null;
        _editorBaseOriginal = bitmap;
        ResetCanaryFilterControls();
        ClearSelectionCanarySilently();
        _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
        UpdateOriginalOutputSizeV060();
        EnsureCenteredCropForOutputV060();
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
        SetEditorStatus("Image transformed. Selection cleared.");
    }

    private static BitmapSource CloneBitmapCanary(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var bitmap = BitmapSource.Create(
            converted.PixelWidth, converted.PixelHeight,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private void ClearSelectionCanarySilently()
    {
        _editorSelectionMaskCanary = null;
        _editorSelectionWidthCanary = 0;
        _editorSelectionHeightCanary = 0;
        if (_editorSelectionBoundaryImageCanary is not null)
            _editorSelectionBoundaryImageCanary.Source = null;
        if (_editorSelectionPreviewPathCanary is not null)
            _editorSelectionPreviewPathCanary.Data = null;
    }
}
