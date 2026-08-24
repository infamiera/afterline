using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private void RenderEditorChatOverlay()
    {
        if (_editorInput is null || _editorChatImage is null) return;

        string input = _editorInput.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            _editorChatBitmap = null;
            _editorChatImage.Source = null;
            RefreshEditorLineColorList(Array.Empty<EditorChatLine>());
            UpdateEditorCanvasSize();
            if (_editorBaseOriginal is null)
                SetEditorStatus("Paste chat lines to begin. Load an image if you want to edit a full RP screenshot.");
            return;
        }

        try
        {
            bool showTimestamps = _editorShowTimestampsCheck?.IsChecked == true;
            double fontSize = _editorFontSizeSlider?.Value ?? 18;
            double lineSpacing = _editorLineSpacingSlider?.Value ?? 1;
            double chatWidth = Math.Max(320, _editorChatWidthSlider?.Value ?? 900);
            (FontFamily fontFamily, FontWeight fontWeight) = ResolveEditorFont();
            IReadOnlyList<EditorChatLine> lines = UnifiedChatFormatter.FormatLines(input, showTimestamps, _editorLineColorOverrides);
            RefreshEditorLineColorList(lines);

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
            TextOptions.SetTextFormattingMode(host, TextFormattingMode.Display);

            host.Measure(new Size(chatWidth, double.PositiveInfinity));
            double height = Math.Max(1, Math.Ceiling(host.DesiredSize.Height));
            host.Arrange(new Rect(0, 0, chatWidth, height));
            host.UpdateLayout();

            var baseBitmap = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(chatWidth)),
                Math.Max(1, (int)Math.Ceiling(height)),
                96,
                96,
                PixelFormats.Pbgra32);
            baseBitmap.Render(host);
            baseBitmap.Freeze();

            BitmapSource finalBitmap = ApplyEditorChatTextEffects(baseBitmap);
            if (finalBitmap.CanFreeze && !finalBitmap.IsFrozen) finalBitmap.Freeze();

            _editorChatBitmap = finalBitmap;
            _editorChatImage.Source = finalBitmap;
            UpdateEditorCanvasSize();
            SetEditorStatus($"Rendered {lines.Count:N0} chat lines · {finalBitmap.PixelWidth:N0} × {finalBitmap.PixelHeight:N0}px.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to render Editor chat overlay.", ex);
            SetEditorStatus("The chat preview could not be rendered.");
        }
    }

    private (FontFamily Family, FontWeight Weight) ResolveEditorFont()
    {
        string selected = _editorFontBox?.SelectedItem?.ToString() ?? "Arial Bold";
        return selected switch
        {
            "Arial" => (new FontFamily("Arial"), FontWeights.Normal),
            "Segoe UI Semibold" => (new FontFamily("Segoe UI"), FontWeights.SemiBold),
            "Tahoma" => (new FontFamily("Tahoma"), FontWeights.Normal),
            "Verdana" => (new FontFamily("Verdana"), FontWeights.Normal),
            _ => (new FontFamily("Arial"), FontWeights.Bold)
        };
    }

    private void EditorLoadImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load RP screenshot",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            using FileStream stream = File.OpenRead(dialog.FileName);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            if (bitmap.CanFreeze) bitmap.Freeze();

            _editorBaseOriginal = bitmap;
            if (_editorRemoveImageButton is not null) _editorRemoveImageButton.IsEnabled = true;
            ResetEditorAdjustmentSliders();
            ClearEditorMarkup(resetHistory: true);
            ApplyEditorImageAdjustments();
            SetEditorStatus($"Loaded {Path.GetFileName(dialog.FileName)} · {bitmap.PixelWidth:N0} × {bitmap.PixelHeight:N0}px.");
            if (_editorFitZoom) _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load image into Editor.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to load image", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditorRemoveImage_Click(object sender, RoutedEventArgs e)
    {
        _editorBaseOriginal = null;
        if (_editorBaseImage is not null)
        {
            _editorBaseImage.Source = null;
            _editorBaseImage.Effect = null;
        }
        if (_editorRemoveImageButton is not null) _editorRemoveImageButton.IsEnabled = false;
        ResetEditorAdjustmentSliders();
        ClearEditorMarkup(resetHistory: true);
        UpdateEditorCanvasSize();
        SetEditorStatus("Screenshot removed. The Editor is back in chat-overlay-only mode.");
    }

    private void ApplyEditorImageAdjustments()
    {
        if (_editorBaseImage is null) return;
        if (_editorBaseOriginal is null)
        {
            _editorBaseImage.Source = null;
            _editorBaseImage.Effect = null;
            UpdateEditorCanvasSize();
            return;
        }

        try
        {
            BitmapSource source = _editorBaseOriginal;
            double brightnessValue = _editorBrightnessSlider?.Value ?? 0;
            double contrastValue = _editorContrastSlider?.Value ?? 0;
            double saturationValue = _editorSaturationSlider?.Value ?? 0;
            double warmthValue = _editorWarmthSlider?.Value ?? 0;
            double tintValue = _editorTintSlider?.Value ?? 0;
            double blurRadius = Math.Max(0, _editorBlurSlider?.Value ?? 0);

            bool neutralTone = Math.Abs(brightnessValue) < 0.001 &&
                               Math.Abs(contrastValue) < 0.001 &&
                               Math.Abs(saturationValue) < 0.001 &&
                               Math.Abs(warmthValue) < 0.001 &&
                               Math.Abs(tintValue) < 0.001;
            if (neutralTone)
            {
                _editorBaseImage.Source = source;
                ApplyEditorBlurEffect(blurRadius);
                UpdateEditorCanvasSize();
                return;
            }

            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            double brightness = brightnessValue * 2.55;
            double contrast = 1.0 + contrastValue / 100.0;
            double saturation = 1.0 + saturationValue / 100.0;
            double warmth = warmthValue * 0.9;
            double tint = tintValue * 0.75;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                double b = pixels[i];
                double g = pixels[i + 1];
                double r = pixels[i + 2];

                r = (r - 128) * contrast + 128 + brightness;
                g = (g - 128) * contrast + 128 + brightness;
                b = (b - 128) * contrast + 128 + brightness;

                double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                r = luminance + (r - luminance) * saturation;
                g = luminance + (g - luminance) * saturation;
                b = luminance + (b - luminance) * saturation;

                r += warmth;
                b -= warmth;
                g += tint;
                r -= tint * 0.25;
                b -= tint * 0.25;

                pixels[i] = ClampEditorByte(b);
                pixels[i + 1] = ClampEditorByte(g);
                pixels[i + 2] = ClampEditorByte(r);
            }

            var adjusted = new WriteableBitmap(
                width,
                height,
                source.DpiX > 0 ? source.DpiX : 96,
                source.DpiY > 0 ? source.DpiY : 96,
                PixelFormats.Bgra32,
                null);
            adjusted.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            if (adjusted.CanFreeze) adjusted.Freeze();

            _editorBaseImage.Source = adjusted;
            ApplyEditorBlurEffect(blurRadius);
            UpdateEditorCanvasSize();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to apply Editor image adjustments.", ex);
            SetEditorStatus("Image adjustments could not be applied.");
        }
    }

    private void ApplyEditorBlurEffect(double blurRadius)
    {
        if (_editorBaseImage is null) return;

        blurRadius = Math.Max(0, blurRadius);
        if (blurRadius <= 0.1)
        {
            if (_editorBaseImage.Effect is not null)
                _editorBaseImage.Effect = null;
            return;
        }

        if (_editorBaseImage.Effect is BlurEffect existing &&
            Math.Abs(existing.Radius - blurRadius) < 0.001 &&
            existing.KernelType == KernelType.Gaussian &&
            existing.RenderingBias == RenderingBias.Quality)
            return;

        _editorBaseImage.Effect = new BlurEffect
        {
            Radius = blurRadius,
            KernelType = KernelType.Gaussian,
            RenderingBias = RenderingBias.Quality
        };
    }

    private static byte ClampEditorByte(double value)
        => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private void UpdateEditorCanvasSize()
    {
        if (_editorComposition is null || _editorInkCanvas is null || _editorChatImage is null || _editorBaseImage is null) return;

        double chatX = Math.Max(0, _editorChatXSlider?.Value ?? 0);
        double chatY = Math.Max(0, _editorChatYSlider?.Value ?? 0);
        double width;
        double height;

        if (_editorBaseOriginal is not null)
        {
            width = Math.Max(1, _editorBaseOriginal.PixelWidth);
            height = Math.Max(1, _editorBaseOriginal.PixelHeight);
            _editorBaseImage.Width = width;
            _editorBaseImage.Height = height;
            _editorBaseImage.Stretch = Stretch.Fill;
        }
        else if (_editorChatBitmap is not null)
        {
            width = Math.Max(1, _editorChatBitmap.PixelWidth + chatX + 12);
            height = Math.Max(1, _editorChatBitmap.PixelHeight + chatY + 12);
            _editorBaseImage.Width = double.NaN;
            _editorBaseImage.Height = double.NaN;
        }
        else
        {
            width = 720;
            height = 420;
            _editorBaseImage.Width = double.NaN;
            _editorBaseImage.Height = double.NaN;
        }

        _editorComposition.Width = width;
        _editorComposition.Height = height;
        _editorInkCanvas.Width = width;
        _editorInkCanvas.Height = height;
        _editorChatImage.Margin = new Thickness(chatX, chatY, 0, 0);
        _editorComposition.Background = string.Equals(_editorBackgroundBox?.SelectedItem?.ToString(), "Transparent", StringComparison.Ordinal)
            ? Brushes.Transparent
            : Brushes.Black;
    }

    private void EditorResetEdits_Click(object sender, RoutedEventArgs e)
    {
        ResetEditorAdjustmentSliders();
        ClearEditorMarkup(resetHistory: true);
        _editorBaseAdjustTimer?.Stop();
        ApplyEditorImageAdjustments();
        SetEditorStatus("Image adjustments and markup were reset. Chat text and the loaded screenshot were kept.");
    }

    private void ResetEditorAdjustmentSliders()
    {
        if (_editorBrightnessSlider is not null) _editorBrightnessSlider.Value = 0;
        if (_editorContrastSlider is not null) _editorContrastSlider.Value = 0;
        if (_editorSaturationSlider is not null) _editorSaturationSlider.Value = 0;
        if (_editorWarmthSlider is not null) _editorWarmthSlider.Value = 0;
        if (_editorTintSlider is not null) _editorTintSlider.Value = 0;
        if (_editorBlurSlider is not null) _editorBlurSlider.Value = 0;
    }

    private void EditorCopyImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RenderTargetBitmap? bitmap = CaptureEditorCompositeBitmap();
            if (bitmap is null) return;
            Clipboard.SetImage(bitmap);
            SetEditorStatus($"Copied {bitmap.PixelWidth:N0} × {bitmap.PixelHeight:N0}px image to the clipboard.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to copy Editor image.", ex);
            SetEditorStatus("The rendered image could not be copied to the clipboard.");
        }
    }

    private void EditorExportPng_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RenderTargetBitmap? bitmap = CaptureEditorCompositeBitmap();
            if (bitmap is null) return;

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
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
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

    private RenderTargetBitmap? CaptureEditorCompositeBitmap(bool refreshChatOverlay = true)
    {
        if (_editorComposition is null) return null;
        if (_editorBaseOriginal is null && _editorChatBitmap is null)
        {
            SetEditorStatus("Add chat text or load an image before exporting.");
            return null;
        }

        _editorChatRenderTimer?.Stop();
        _editorBaseAdjustTimer?.Stop();
        if (refreshChatOverlay)
            RenderEditorChatOverlay();
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();

        int width = Math.Max(1, (int)Math.Ceiling(_editorComposition.Width));
        int height = Math.Max(1, (int)Math.Ceiling(_editorComposition.Height));
        _editorComposition.Measure(new Size(width, height));
        _editorComposition.Arrange(new Rect(0, 0, width, height));
        _editorComposition.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_editorComposition);
        bitmap.Freeze();
        return bitmap;
    }
}
