using Microsoft.Win32;
using System.Text;
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
    private bool _editorMediaV060Initialized;
    private List<BitmapFrame>? _editorGifFrames;
    private List<TimeSpan>? _editorGifDelays;
    private DispatcherTimer? _editorGifTimer;
    private int _editorGifFrameIndex;
    private int _editorGifLoopCount;
    private int _editorGifCompletedLoops;
    private bool _editorGifExporting;
    private string? _editorLoadedMediaPath;

    private ComboBox? _editorOutputPresetBox;
    private TextBox? _editorOutputWidthBox;
    private TextBox? _editorOutputHeightBox;
    private CheckBox? _editorOutputLockAspectCheck;
    private TextBlock? _editorCropSummaryText;
    private Button? _editorAdjustCropButton;
    private Button? _editorResetCropButton;
    private Button? _editorExportGifButton;
    private Rect _editorCropNormalizedV060 = new(0, 0, 1, 1);
    private bool _editorUpdatingOutputControlsV060;
    private double _editorLockedAspectRatioV060 = 16.0 / 9.0;

    private bool EditorHasAnimatedGifV060 => _editorGifFrames is { Count: > 1 };

    private void EnsureEditorMediaV060()
    {
        if (_editorMediaV060Initialized) return;
        _editorMediaV060Initialized = true;

        _editorGifTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _editorGifTimer.Tick += EditorGifTimer_TickV060;
        Closed += (_, _) => _editorGifTimer?.Stop();
        RewireEditorHeaderV060();
        UpdateEditorMediaControlsV060();
    }


    private void RewireEditorHeaderV060()
    {
        if (_editorPage is null) return;
        Border? header = _editorPage.Children.OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (header?.Child is not Grid headerGrid) return;
        WrapPanel? actions = headerGrid.Children.OfType<WrapPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 1);
        if (actions is null) return;

        foreach (Button button in actions.Children.OfType<Button>())
        {
            string text = button.Content?.ToString() ?? string.Empty;
            if (string.Equals(text, "Load Image", StringComparison.OrdinalIgnoreCase))
            {
                button.Click -= EditorLoadImage_Click;
                button.Click += EditorLoadMediaV060_Click;
                button.Content = "Load Image / GIF";
                button.ToolTip = "Load a PNG, JPEG, BMP, or animated GIF into the RP Editor.";
            }
            else if (string.Equals(text, "Export PNG", StringComparison.OrdinalIgnoreCase))
            {
                button.Click -= EditorExportPng_Click;
                button.Click += EditorExportDefaultV060_Click;
                button.Content = "Export";
                button.ToolTip = "Exports GIF when an animated GIF is loaded; otherwise exports PNG.";
            }
        }
    }

    private void EditorLoadMediaV060_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load RP screenshot or GIF",
            Filter = "Image and GIF files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Animated GIF (*.gif)|*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        LoadEditorMediaV060(dialog.FileName);
    }

    private void LoadEditorMediaV060(string path)
    {
        try
        {
            StopEditorGifPreviewV060();
            _editorGifFrames = null;
            _editorGifDelays = null;
            _editorGifLoopCount = 0;
            _editorGifCompletedLoops = 0;
            _editorGifFrameIndex = 0;
            _editorLoadedMediaPath = path;

            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var stream = new MemoryStream(bytes, writable: false);
                var decoder = new GifBitmapDecoder(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                _editorGifFrames = decoder.Frames
                    .Select(frame => FreezeEditorFrameV060(frame))
                    .ToList();
                _editorGifDelays = decoder.Frames
                    .Select(ReadGifDelayV060)
                    .ToList();
                _editorGifLoopCount = ReadGifLoopCountV060(decoder.Frames.FirstOrDefault());

                if (_editorGifFrames.Count == 0)
                    throw new InvalidDataException("The GIF did not contain any readable frames.");

                _editorBaseOriginal = _editorGifFrames[0];
            }
            else
            {
                using FileStream stream = File.OpenRead(path);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                if (bitmap.CanFreeze) bitmap.Freeze();
                _editorBaseOriginal = bitmap;
            }

            _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
            if (_editorRemoveImageButton is not null) _editorRemoveImageButton.IsEnabled = true;
            ClearEditorMarkup(resetHistory: true);
            ApplyEditorImageAdjustments();
            UpdateOriginalOutputSizeV060();
            EnsureCenteredCropForOutputV060();
            UpdateEditorMediaControlsV060();

            BitmapSource source = _editorBaseOriginal!;
            string frameInfo = EditorHasAnimatedGifV060
                ? $" · {_editorGifFrames!.Count:N0} frames"
                : string.Empty;
            SetEditorStatus($"Loaded {Path.GetFileName(path)} · {source.PixelWidth:N0} × {source.PixelHeight:N0}px{frameInfo}.");

            if (EditorHasAnimatedGifV060)
                StartEditorGifPreviewV060(resetPosition: true);
            if (_editorFitZoom)
                _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load media into Editor.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to load image or GIF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditorRemoveMediaV060_Click(object sender, RoutedEventArgs e)
    {
        StopEditorGifPreviewV060();
        _editorGifFrames = null;
        _editorGifDelays = null;
        _editorGifLoopCount = 0;
        _editorGifCompletedLoops = 0;
        _editorGifFrameIndex = 0;
        _editorLoadedMediaPath = null;
        _editorBaseOriginal = null;
        _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);

        if (_editorBaseImage is not null)
        {
            _editorBaseImage.Source = null;
            _editorBaseImage.Effect = null;
        }
        if (_editorRemoveImageButton is not null) _editorRemoveImageButton.IsEnabled = false;
        ClearEditorMarkup(resetHistory: true);
        UpdateEditorCanvasSize();
        UpdateEditorMediaControlsV060();
        SetEditorStatus("Screenshot removed. The Editor is back in chat-overlay-only mode.");
    }

    private static BitmapFrame FreezeEditorFrameV060(BitmapFrame source)
    {
        BitmapFrame frame = BitmapFrame.Create(source);
        if (frame.CanFreeze) frame.Freeze();
        return frame;
    }

    private void StartEditorGifPreviewV060(bool resetPosition)
    {
        if (!EditorHasAnimatedGifV060 || _editorGifTimer is null || _editorGifDelays is null) return;
        if (resetPosition)
        {
            _editorGifFrameIndex = 0;
            _editorGifCompletedLoops = 0;
            _editorBaseOriginal = _editorGifFrames![0];
            ApplyEditorGifPreviewFrameV060();
        }

        _editorGifTimer.Interval = PreviewGifDelayV060(_editorGifDelays[_editorGifFrameIndex]);
        _editorGifTimer.Start();
    }

    private void StopEditorGifPreviewV060() => _editorGifTimer?.Stop();

    private void EditorGifTimer_TickV060(object? sender, EventArgs e)
    {
        if (_editorGifExporting || !EditorHasAnimatedGifV060 || _editorGifDelays is null || _editorGifTimer is null) return;

        int next = _editorGifFrameIndex + 1;
        if (next >= _editorGifFrames!.Count)
        {
            next = 0;
            _editorGifCompletedLoops++;
            if (_editorGifLoopCount > 0 && _editorGifCompletedLoops >= _editorGifLoopCount)
            {
                _editorGifTimer.Stop();
                return;
            }
        }

        _editorGifFrameIndex = next;
        _editorBaseOriginal = _editorGifFrames[next];
        ApplyEditorGifPreviewFrameV060();
        _editorGifTimer.Interval = PreviewGifDelayV060(_editorGifDelays[next]);
    }


    private void ApplyEditorGifPreviewFrameV060()
    {
        if (_editorBaseImage is null || _editorBaseOriginal is null)
            return;

        bool neutralTone = Math.Abs(_editorBrightnessSlider?.Value ?? 0) < 0.001 &&
                           Math.Abs(_editorContrastSlider?.Value ?? 0) < 0.001 &&
                           Math.Abs(_editorSaturationSlider?.Value ?? 0) < 0.001 &&
                           Math.Abs(_editorWarmthSlider?.Value ?? 0) < 0.001 &&
                           Math.Abs(_editorTintSlider?.Value ?? 0) < 0.001;
        if (!neutralTone)
        {
            ApplyEditorImageAdjustments();
            return;
        }

        _editorBaseImage.Source = _editorBaseOriginal;
        double blurRadius = Math.Max(0, _editorBlurSlider?.Value ?? 0);
        _editorBaseImage.Effect = blurRadius > 0.1
            ? new System.Windows.Media.Effects.BlurEffect
            {
                Radius = blurRadius,
                KernelType = System.Windows.Media.Effects.KernelType.Gaussian,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality
            }
            : null;
        UpdateEditorCanvasSize();
    }

    private static TimeSpan PreviewGifDelayV060(TimeSpan value)
        => TimeSpan.FromMilliseconds(Math.Clamp(value.TotalMilliseconds, 20, 2000));

    private static TimeSpan ReadGifDelayV060(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata)
            {
                object? value = metadata.GetQuery("/grctlext/Delay");
                int hundredths = value switch
                {
                    ushort u => u,
                    short s => Math.Max(0, (int)s),
                    uint u => (int)Math.Min(int.MaxValue, u),
                    int i => Math.Max(0, i),
                    _ => 0
                };
                if (hundredths > 0)
                    return TimeSpan.FromMilliseconds(hundredths * 10.0);
            }
        }
        catch
        {
        }
        return TimeSpan.FromMilliseconds(100);
    }

    private static int ReadGifLoopCountV060(BitmapFrame? frame)
    {
        try
        {
            if (frame?.Metadata is not BitmapMetadata metadata) return 0;
            object? data = metadata.GetQuery("/appext/data");
            if (data is byte[] bytes && bytes.Length >= 4 && bytes[0] == 0x03 && bytes[1] == 0x01)
                return bytes[2] | (bytes[3] << 8);
        }
        catch
        {
        }
        return 0;
    }

    private void EditorOutputPreset_ChangedV060(object sender, SelectionChangedEventArgs e)
    {
        if (_editorUpdatingOutputControlsV060 || _editorOutputPresetBox is null) return;
        string preset = _editorOutputPresetBox.SelectedItem?.ToString() ?? "Original";

        (int Width, int Height)? size = preset switch
        {
            "1920 × 1080" => (1920, 1080),
            "1280 × 720" => (1280, 720),
            "1080 × 1080" => (1080, 1080),
            "1080 × 1350" => (1080, 1350),
            _ => null
        };

        if (string.Equals(preset, "Original", StringComparison.OrdinalIgnoreCase))
        {
            UpdateOriginalOutputSizeV060();
            _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
        }
        else if (size is not null)
        {
            SetEditorOutputBoxesV060(size.Value.Width, size.Value.Height);
            _editorLockedAspectRatioV060 = size.Value.Width / (double)size.Value.Height;
            EnsureCenteredCropForOutputV060();
        }
        UpdateCropSummaryV060();
    }

    private void EditorOutputAspectLock_ChangedV060(object sender, RoutedEventArgs e)
    {
        if (TryReadEditorOutputSizeV060(out int width, out int height) && height > 0)
            _editorLockedAspectRatioV060 = width / (double)height;
    }

    private void EditorOutputSize_LostFocusV060(object sender, RoutedEventArgs e)
    {
        if (_editorUpdatingOutputControlsV060) return;

        if (_editorOutputLockAspectCheck?.IsChecked == true && _editorLockedAspectRatioV060 > 0)
        {
            if (ReferenceEquals(sender, _editorOutputWidthBox) && int.TryParse(_editorOutputWidthBox?.Text, out int editedWidth) && editedWidth > 0)
            {
                int lockedHeight = Math.Max(1, (int)Math.Round(editedWidth / _editorLockedAspectRatioV060));
                SetEditorOutputBoxesV060(editedWidth, lockedHeight);
            }
            else if (ReferenceEquals(sender, _editorOutputHeightBox) && int.TryParse(_editorOutputHeightBox?.Text, out int editedHeight) && editedHeight > 0)
            {
                int lockedWidth = Math.Max(1, (int)Math.Round(editedHeight * _editorLockedAspectRatioV060));
                SetEditorOutputBoxesV060(lockedWidth, editedHeight);
            }
        }

        if (!TryReadEditorOutputSizeV060(out int width, out int height))
        {
            UpdateOriginalOutputSizeV060();
            return;
        }

        _editorLockedAspectRatioV060 = width / (double)height;
        EnsureCenteredCropForOutputV060();
        if (_editorOutputPresetBox is not null)
        {
            string matching = MatchOutputPresetV060(width, height);
            _editorUpdatingOutputControlsV060 = true;
            SetComboTextV060(_editorOutputPresetBox, matching);
            _editorUpdatingOutputControlsV060 = false;
        }
        UpdateCropSummaryV060();
    }

    private void EditorAdjustCrop_ClickV060(object sender, RoutedEventArgs e)
    {
        BitmapSource? source = _editorGifFrames?.FirstOrDefault() ?? _editorBaseOriginal;
        if (source is null)
        {
            SetEditorStatus("Load an image or GIF before adjusting its crop.");
            return;
        }

        if (!TryReadEditorOutputSizeV060(out int width, out int height))
        {
            width = source.PixelWidth;
            height = source.PixelHeight;
            SetEditorOutputBoxesV060(width, height);
        }

        var cropper = new EditorCropWindow(this, source, _editorCropNormalizedV060, width, height);
        if (cropper.ShowDialog() == true && cropper.Saved)
        {
            _editorCropNormalizedV060 = cropper.CropNormalized;
            UpdateCropSummaryV060();
            SetEditorStatus($"Crop updated · output {width:N0} × {height:N0}px.");
        }
    }

    private void EditorResetCrop_ClickV060(object sender, RoutedEventArgs e)
    {
        _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
        EnsureCenteredCropForOutputV060();
        UpdateCropSummaryV060();
        SetEditorStatus("Crop reset to centered framing for the selected output size.");
    }

    private void UpdateOriginalOutputSizeV060()
    {
        if (_editorOutputPresetBox is null || _editorOutputWidthBox is null || _editorOutputHeightBox is null) return;
        string preset = _editorOutputPresetBox.SelectedItem?.ToString() ?? "Original";
        if (!string.Equals(preset, "Original", StringComparison.OrdinalIgnoreCase)) return;

        int width = _editorBaseOriginal?.PixelWidth ?? _editorChatBitmap?.PixelWidth ?? 1920;
        int height = _editorBaseOriginal?.PixelHeight ?? _editorChatBitmap?.PixelHeight ?? 1080;
        SetEditorOutputBoxesV060(width, height);
    }

    private void SetEditorOutputBoxesV060(int width, int height)
    {
        if (_editorOutputWidthBox is null || _editorOutputHeightBox is null) return;
        _editorUpdatingOutputControlsV060 = true;
        int safeWidth = Math.Clamp(width, 1, 16384);
        int safeHeight = Math.Clamp(height, 1, 16384);
        _editorOutputWidthBox.Text = safeWidth.ToString();
        _editorOutputHeightBox.Text = safeHeight.ToString();
        _editorLockedAspectRatioV060 = safeWidth / (double)safeHeight;
        _editorUpdatingOutputControlsV060 = false;
    }

    private bool TryReadEditorOutputSizeV060(out int width, out int height)
    {
        bool validWidth = int.TryParse(_editorOutputWidthBox?.Text, out width) && width > 0 && width <= 16384;
        bool validHeight = int.TryParse(_editorOutputHeightBox?.Text, out height) && height > 0 && height <= 16384;
        return validWidth && validHeight;
    }

    private static string MatchOutputPresetV060(int width, int height)
        => (width, height) switch
        {
            (1920, 1080) => "1920 × 1080",
            (1280, 720) => "1280 × 720",
            (1080, 1080) => "1080 × 1080",
            (1080, 1350) => "1080 × 1350",
            _ => "Custom"
        };

    private static void SetComboTextV060(ComboBox box, string text)
    {
        object? match = box.Items.Cast<object>()
            .FirstOrDefault(item => string.Equals(item?.ToString(), text, StringComparison.OrdinalIgnoreCase));
        if (match is not null) box.SelectedItem = match;
    }

    private void EnsureCenteredCropForOutputV060()
    {
        BitmapSource? source = _editorGifFrames?.FirstOrDefault() ?? _editorBaseOriginal;
        if (source is null || !TryReadEditorOutputSizeV060(out int outputWidth, out int outputHeight))
            return;

        if (string.Equals(_editorOutputPresetBox?.SelectedItem?.ToString(), "Original", StringComparison.OrdinalIgnoreCase))
        {
            _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
            return;
        }

        double sourceRatio = source.PixelWidth / (double)Math.Max(1, source.PixelHeight);
        double outputRatio = outputWidth / (double)Math.Max(1, outputHeight);
        if (Math.Abs(sourceRatio - outputRatio) < 0.0001)
        {
            _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
        }
        else if (sourceRatio > outputRatio)
        {
            double width = Math.Clamp(outputRatio / sourceRatio, 0.0001, 1);
            _editorCropNormalizedV060 = new Rect((1 - width) / 2, 0, width, 1);
        }
        else
        {
            double height = Math.Clamp(sourceRatio / outputRatio, 0.0001, 1);
            _editorCropNormalizedV060 = new Rect(0, (1 - height) / 2, 1, height);
        }
    }

    private void UpdateCropSummaryV060()
    {
        if (_editorCropSummaryText is null) return;
        Rect crop = _editorCropNormalizedV060;
        string cropText = crop.X <= 0.0001 && crop.Y <= 0.0001 && crop.Width >= 0.9999 && crop.Height >= 0.9999
            ? "Full frame"
            : $"Crop {crop.Width:P0} × {crop.Height:P0} of source";
        string output = TryReadEditorOutputSizeV060(out int width, out int height)
            ? $" · {width:N0} × {height:N0}px"
            : string.Empty;
        _editorCropSummaryText.Text = cropText + output;
    }

    private void UpdateEditorMediaControlsV060()
    {
        bool hasMedia = _editorBaseOriginal is not null;
        if (_editorRemoveImageButton is not null) _editorRemoveImageButton.IsEnabled = hasMedia;
        if (_editorAdjustCropButton is not null) _editorAdjustCropButton.IsEnabled = hasMedia;
        if (_editorResetCropButton is not null) _editorResetCropButton.IsEnabled = hasMedia;
        if (_editorExportGifButton is not null)
        {
            _editorExportGifButton.IsEnabled = EditorHasAnimatedGifV060;
            _editorExportGifButton.ToolTip = EditorHasAnimatedGifV060
                ? "Export the full animated composition while preserving frame timing."
                : "Load an animated GIF to enable GIF export.";
        }
        UpdateCropSummaryV060();
    }

    private void ApplyEditorOutputPreferencesV060(EditorPreferences preferences)
    {
        if (_editorOutputPresetBox is null) return;
        string preset = string.IsNullOrWhiteSpace(preferences.OutputPreset) ? "Original" : preferences.OutputPreset;
        SetComboTextV060(_editorOutputPresetBox, preset);
        if (preferences.OutputWidth > 0 && preferences.OutputHeight > 0 && !string.Equals(preset, "Original", StringComparison.OrdinalIgnoreCase))
            SetEditorOutputBoxesV060(preferences.OutputWidth, preferences.OutputHeight);
        else
            UpdateOriginalOutputSizeV060();
        if (_editorOutputLockAspectCheck is not null) _editorOutputLockAspectCheck.IsChecked = preferences.OutputLockAspect;
        if (TryReadEditorOutputSizeV060(out int width, out int height)) _editorLockedAspectRatioV060 = width / (double)height;
        UpdateCropSummaryV060();
    }

    private void EditorExportDefaultV060_Click(object sender, RoutedEventArgs e)
    {
        if (EditorHasAnimatedGifV060)
            EditorExportGifV060_Click(sender, e);
        else
            EditorExportPngV060_Click(sender, e);
    }

    private void EditorCopyImageV060_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RenderTargetBitmap? full = CaptureEditorCompositeBitmap();
            if (full is null) return;
            BitmapSource output = ApplyEditorOutputTransformV060(full);
            Clipboard.SetImage(output);
            SetEditorStatus($"Copied {output.PixelWidth:N0} × {output.PixelHeight:N0}px image to the clipboard.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to copy Editor output.", ex);
            SetEditorStatus("The rendered image could not be copied to the clipboard.");
        }
    }

    private void EditorExportPngV060_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RenderTargetBitmap? full = CaptureEditorCompositeBitmap();
            if (full is null) return;
            BitmapSource output = ApplyEditorOutputTransformV060(full);

            var dialog = new SaveFileDialog
            {
                Title = "Export RP screenshot",
                Filter = "PNG image (*.png)|*.png",
                DefaultExt = ".png",
                AddExtension = true,
                FileName = $"RP Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png"
            };
            if (dialog.ShowDialog(this) != true) return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using FileStream stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            SetEditorStatus($"PNG exported to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to export Editor PNG.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to export PNG", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditorExportGifV060_Click(object sender, RoutedEventArgs e)
    {
        if (!EditorHasAnimatedGifV060 || _editorGifFrames is null || _editorGifDelays is null)
        {
            SetEditorStatus("Load an animated GIF before exporting an animated RP screen.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export animated RP screenshot",
            Filter = "Animated GIF (*.gif)|*.gif",
            DefaultExt = ".gif",
            AddExtension = true,
            FileName = $"RP Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}.gif"
        };
        if (dialog.ShowDialog(this) != true) return;

        int restoreIndex = Math.Clamp(_editorGifFrameIndex, 0, _editorGifFrames.Count - 1);
        try
        {
            _editorGifExporting = true;
            StopEditorGifPreviewV060();
            SetEditorStatus($"Rendering {_editorGifFrames.Count:N0} GIF frames…");

            var encoder = new GifBitmapEncoder();
            for (int i = 0; i < _editorGifFrames.Count; i++)
            {
                _editorBaseOriginal = _editorGifFrames[i];
                RenderTargetBitmap? full = CaptureEditorCompositeBitmap();
                if (full is null) throw new InvalidOperationException("The GIF frame could not be rendered.");
                BitmapSource output = ApplyEditorOutputTransformV060(full);
                BitmapMetadata metadata = CreateGifMetadataV060(_editorGifDelays[i], i == 0 ? _editorGifLoopCount : null);
                encoder.Frames.Add(BitmapFrame.Create(output, null, metadata, null));
            }

            using FileStream stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            SetEditorStatus($"Animated GIF exported to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to export Editor GIF.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to export GIF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _editorGifExporting = false;
            _editorGifFrameIndex = restoreIndex;
            _editorBaseOriginal = _editorGifFrames[restoreIndex];
            ApplyEditorGifPreviewFrameV060();
            StartEditorGifPreviewV060(resetPosition: false);
        }
    }

    private BitmapSource ApplyEditorOutputTransformV060(BitmapSource full)
    {
        int fullWidth = Math.Max(1, full.PixelWidth);
        int fullHeight = Math.Max(1, full.PixelHeight);
        Rect normalized = NormalizeEditorCropV060(_editorCropNormalizedV060);

        int x = Math.Clamp((int)Math.Floor(normalized.X * fullWidth), 0, fullWidth - 1);
        int y = Math.Clamp((int)Math.Floor(normalized.Y * fullHeight), 0, fullHeight - 1);
        int width = Math.Clamp((int)Math.Round(normalized.Width * fullWidth), 1, fullWidth - x);
        int height = Math.Clamp((int)Math.Round(normalized.Height * fullHeight), 1, fullHeight - y);

        BitmapSource cropped = x == 0 && y == 0 && width == fullWidth && height == fullHeight
            ? full
            : new CroppedBitmap(full, new Int32Rect(x, y, width, height));

        if (!TryReadEditorOutputSizeV060(out int outputWidth, out int outputHeight) ||
            string.Equals(_editorOutputPresetBox?.SelectedItem?.ToString(), "Original", StringComparison.OrdinalIgnoreCase))
        {
            outputWidth = width;
            outputHeight = height;
        }

        outputWidth = Math.Clamp(outputWidth, 1, 16384);
        outputHeight = Math.Clamp(outputHeight, 1, 16384);
        if (cropped.PixelWidth == outputWidth && cropped.PixelHeight == outputHeight)
        {
            if (cropped.CanFreeze && !cropped.IsFrozen) cropped.Freeze();
            return cropped;
        }

        var resized = new TransformedBitmap(
            cropped,
            new ScaleTransform(
                outputWidth / (double)Math.Max(1, cropped.PixelWidth),
                outputHeight / (double)Math.Max(1, cropped.PixelHeight)));
        if (resized.CanFreeze) resized.Freeze();
        return resized;
    }

    private static Rect NormalizeEditorCropV060(Rect value)
    {
        double width = Math.Clamp(value.Width, 0.0001, 1);
        double height = Math.Clamp(value.Height, 0.0001, 1);
        double x = Math.Clamp(value.X, 0, Math.Max(0, 1 - width));
        double y = Math.Clamp(value.Y, 0, Math.Max(0, 1 - height));
        return new Rect(x, y, width, height);
    }

    private static BitmapMetadata CreateGifMetadataV060(TimeSpan delay, int? loopCount)
    {
        var metadata = new BitmapMetadata("gif");
        try
        {
            ushort hundredths = (ushort)Math.Clamp((int)Math.Round(delay.TotalMilliseconds / 10.0), 1, ushort.MaxValue);
            metadata.SetQuery("/grctlext/Delay", hundredths);
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
        }
        catch
        {
        }

        if (loopCount is not null)
        {
            try
            {
                int loops = Math.Clamp(loopCount.Value, 0, ushort.MaxValue);
                metadata.SetQuery("/appext/application", Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                metadata.SetQuery("/appext/data", new byte[]
                {
                    0x03, 0x01,
                    (byte)(loops & 0xFF),
                    (byte)((loops >> 8) & 0xFF)
                });
            }
            catch
            {
            }
        }
        return metadata;
    }
}
