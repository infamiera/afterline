using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Afterline;

public partial class MainWindow
{
    private BitmapSource ApplyEditorChatTextEffects(BitmapSource source)
    {
        bool strokeEnabled = _editorStrokeEnabledCheck?.IsChecked == true;
        bool shadowEnabled = _editorShadowEnabledCheck?.IsChecked == true;
        double strokeWidth = Math.Clamp(_editorStrokeWidthSlider?.Value ?? 0, 0, 5);
        int shadowBlur = Math.Clamp((int)Math.Round(_editorShadowBlurSlider?.Value ?? 0), 0, 20);
        int shadowOffsetX = Math.Clamp((int)Math.Round(_editorShadowOffsetXSlider?.Value ?? 0), -12, 12);
        int shadowOffsetY = Math.Clamp((int)Math.Round(_editorShadowOffsetYSlider?.Value ?? 0), -12, 12);
        double shadowOpacity = Math.Clamp((_editorShadowOpacitySlider?.Value ?? 75) / 100.0, 0, 1);

        if ((!strokeEnabled || strokeWidth <= 0.01) && (!shadowEnabled || shadowOpacity <= 0.001))
            return source;

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int sourceWidth = converted.PixelWidth;
        int sourceHeight = converted.PixelHeight;
        int sourceStride = sourceWidth * 4;
        byte[] sourcePixels = new byte[sourceStride * sourceHeight];
        converted.CopyPixels(sourcePixels, sourceStride, 0);

        int strokeRadius = strokeEnabled ? (int)Math.Ceiling(strokeWidth) : 0;
        int shadowSpread = shadowEnabled ? shadowBlur * 2 + Math.Max(Math.Abs(shadowOffsetX), Math.Abs(shadowOffsetY)) : 0;
        int padding = Math.Max(2, strokeRadius + shadowSpread + 3);
        int width = sourceWidth + padding * 2;
        int height = sourceHeight + padding * 2;
        int stride = width * 4;
        byte[] output = new byte[stride * height];

        if (shadowEnabled && shadowOpacity > 0.001)
        {
            byte[] shadowMask = new byte[width * height];
            StampSourceAlpha(sourcePixels, sourceWidth, sourceHeight, sourceStride, shadowMask, width, height,
                padding + shadowOffsetX, padding + shadowOffsetY);
            if (shadowBlur > 0)
                shadowMask = BoxBlurAlpha(shadowMask, width, height, shadowBlur);

            Color shadowColor = ResolveEditorEffectColor(_editorShadowColorBox?.SelectedItem?.ToString(), Colors.Black);
            PaintMask(output, stride, shadowMask, width, height, shadowColor, shadowOpacity);
        }

        if (strokeEnabled && strokeRadius > 0)
        {
            byte[] strokeMask = CreateStrokeMask(sourcePixels, sourceWidth, sourceHeight, sourceStride, width, height, padding, strokeRadius);
            Color strokeColor = ResolveEditorEffectColor(_editorStrokeColorBox?.SelectedItem?.ToString(), Colors.Black);
            PaintMask(output, stride, strokeMask, width, height, strokeColor, 1.0);
        }

        CompositeSource(output, width, height, stride, sourcePixels, sourceWidth, sourceHeight, sourceStride, padding, padding);

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), output, stride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static void StampSourceAlpha(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int sourceStride,
        byte[] mask,
        int maskWidth,
        int maskHeight,
        int offsetX,
        int offsetY)
    {
        for (int y = 0; y < sourceHeight; y++)
        {
            int targetY = y + offsetY;
            if ((uint)targetY >= (uint)maskHeight) continue;
            int sourceRow = y * sourceStride;
            int maskRow = targetY * maskWidth;
            for (int x = 0; x < sourceWidth; x++)
            {
                int targetX = x + offsetX;
                if ((uint)targetX >= (uint)maskWidth) continue;
                byte alpha = source[sourceRow + x * 4 + 3];
                if (alpha > mask[maskRow + targetX]) mask[maskRow + targetX] = alpha;
            }
        }
    }

    private static byte[] CreateStrokeMask(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int sourceStride,
        int width,
        int height,
        int padding,
        int radius)
    {
        var mask = new byte[width * height];
        int radiusSquared = radius * radius;

        for (int y = 0; y < sourceHeight; y++)
        {
            int sourceRow = y * sourceStride;
            for (int x = 0; x < sourceWidth; x++)
            {
                byte alpha = source[sourceRow + x * 4 + 3];
                if (alpha == 0) continue;
                int centerX = x + padding;
                int centerY = y + padding;

                for (int dy = -radius; dy <= radius; dy++)
                {
                    int targetY = centerY + dy;
                    if ((uint)targetY >= (uint)height) continue;
                    int row = targetY * width;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radiusSquared) continue;
                        int targetX = centerX + dx;
                        if ((uint)targetX >= (uint)width) continue;
                        int index = row + targetX;
                        if (alpha > mask[index]) mask[index] = alpha;
                    }
                }
            }
        }

        return mask;
    }

    private static byte[] BoxBlurAlpha(byte[] input, int width, int height, int radius)
    {
        if (radius <= 0) return input;
        var horizontal = new byte[input.Length];
        var output = new byte[input.Length];
        int diameter = radius * 2 + 1;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            int sum = 0;
            for (int x = -radius; x <= radius; x++)
            {
                int clamped = Math.Clamp(x, 0, width - 1);
                sum += input[row + clamped];
            }

            for (int x = 0; x < width; x++)
            {
                horizontal[row + x] = (byte)(sum / diameter);
                int removeX = Math.Clamp(x - radius, 0, width - 1);
                int addX = Math.Clamp(x + radius + 1, 0, width - 1);
                sum += input[row + addX] - input[row + removeX];
            }
        }

        for (int x = 0; x < width; x++)
        {
            int sum = 0;
            for (int y = -radius; y <= radius; y++)
            {
                int clamped = Math.Clamp(y, 0, height - 1);
                sum += horizontal[clamped * width + x];
            }

            for (int y = 0; y < height; y++)
            {
                output[y * width + x] = (byte)(sum / diameter);
                int removeY = Math.Clamp(y - radius, 0, height - 1);
                int addY = Math.Clamp(y + radius + 1, 0, height - 1);
                sum += horizontal[addY * width + x] - horizontal[removeY * width + x];
            }
        }

        return output;
    }

    private static void PaintMask(
        byte[] output,
        int stride,
        byte[] mask,
        int width,
        int height,
        Color color,
        double opacity)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                byte maskAlpha = mask[row + x];
                if (maskAlpha == 0) continue;
                double alpha = (maskAlpha / 255.0) * opacity;
                BlendEditorPixel(output, y * stride + x * 4, color.B, color.G, color.R, alpha);
            }
        }
    }

    private static void CompositeSource(
        byte[] output,
        int outputWidth,
        int outputHeight,
        int outputStride,
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int sourceStride,
        int offsetX,
        int offsetY)
    {
        for (int y = 0; y < sourceHeight; y++)
        {
            int targetY = y + offsetY;
            if ((uint)targetY >= (uint)outputHeight) continue;
            int sourceRow = y * sourceStride;
            int targetRow = targetY * outputStride;
            for (int x = 0; x < sourceWidth; x++)
            {
                int targetX = x + offsetX;
                if ((uint)targetX >= (uint)outputWidth) continue;
                int sourceIndex = sourceRow + x * 4;
                byte alphaByte = source[sourceIndex + 3];
                if (alphaByte == 0) continue;
                BlendEditorPixel(output, targetRow + targetX * 4,
                    source[sourceIndex], source[sourceIndex + 1], source[sourceIndex + 2], alphaByte / 255.0);
            }
        }
    }

    private static void BlendEditorPixel(byte[] destination, int index, byte blue, byte green, byte red, double sourceAlpha)
    {
        double destinationAlpha = destination[index + 3] / 255.0;
        double outAlpha = sourceAlpha + destinationAlpha * (1 - sourceAlpha);
        if (outAlpha <= 0.0001) return;

        double destinationWeight = destinationAlpha * (1 - sourceAlpha);
        destination[index] = ClampEditorByte((blue * sourceAlpha + destination[index] * destinationWeight) / outAlpha);
        destination[index + 1] = ClampEditorByte((green * sourceAlpha + destination[index + 1] * destinationWeight) / outAlpha);
        destination[index + 2] = ClampEditorByte((red * sourceAlpha + destination[index + 2] * destinationWeight) / outAlpha);
        destination[index + 3] = ClampEditorByte(outAlpha * 255.0);
    }

    private static Color ResolveEditorEffectColor(string? selected, Color fallback)
        => selected switch
        {
            "Black" => Colors.Black,
            "White" => Colors.White,
            "Blue" => Afterline.Services.EditorChatFormatter.Blue,
            "Yellow" => Afterline.Services.EditorChatFormatter.Yellow,
            "Green" => Afterline.Services.EditorChatFormatter.Green,
            "Purple" => Afterline.Services.EditorChatFormatter.Purple,
            "Orange" => Afterline.Services.EditorChatFormatter.Orange,
            "Red" => Afterline.Services.EditorChatFormatter.Red,
            _ => fallback
        };
}
