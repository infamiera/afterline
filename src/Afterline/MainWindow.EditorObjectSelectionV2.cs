using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Afterline;

public partial class MainWindow
{
    private Image? _editorObjectHoverImageCanaryV2;
    private BitmapSource? _editorObjectCacheSourceCanaryV2;
    private byte[]? _editorObjectLowPixelsCanaryV2;
    private int _editorObjectLowWidthCanaryV2;
    private int _editorObjectLowHeightCanaryV2;
    private bool[]? _editorObjectHoverMaskLowCanaryV2;
    private DateTime _editorObjectLastPreviewUtcCanaryV2 = DateTime.MinValue;
    private Point _editorObjectLastPointCanaryV2 = new(-1000, -1000);

    private void ConfigureObjectSelectionRefinedCanaryV2()
    {
        if (_editorSelectionOverlayCanary is null) return;

        _editorObjectHoverImageCanaryV2 = new Image
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Opacity = 1
        };
        Panel.SetZIndex(_editorObjectHoverImageCanaryV2, 1);
        _editorSelectionOverlayCanary.Children.Insert(0, _editorObjectHoverImageCanaryV2);

        _editorSelectionOverlayCanary.AddHandler(Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler(ObjectSelectionMouseDownRefinedCanaryV2), true);
        _editorSelectionOverlayCanary.AddHandler(Mouse.PreviewMouseMoveEvent,
            new MouseEventHandler(ObjectSelectionMouseMoveRefinedCanaryV2), true);
        _editorSelectionOverlayCanary.MouseLeave += (_, _) => ClearObjectHoverPreviewCanaryV2();

        if (_editorToolPanels.TryGetValue("selection", out FrameworkElement? panel))
        {
            foreach (Button button in FindVisualChildrenCanary<Button>(panel))
            {
                if (string.Equals(button.Content?.ToString(), "Object Select", StringComparison.OrdinalIgnoreCase))
                {
                    button.ToolTip = "Hover over a visible subject to preview an edge-aware object region, then left-click to select it.";
                    button.Click += (_, _) =>
                    {
                        SetEditorStatus("Object Select active · hover over a subject to preview it, then left-click to select.");
                        if (_editorObjectThresholdSliderCanary is not null)
                            _editorObjectThresholdSliderCanary.ToolTip = "Object tolerance. Increase it when a subject contains more varied colors; lower it if the selection leaks into the background.";
                    };
                }
            }
        }
    }

    private void ObjectSelectionMouseMoveRefinedCanaryV2(object sender, MouseEventArgs e)
    {
        if (_editorSelectionToolCanary != CanarySelectionTool.Object ||
            _editorSelectionOverlayCanary is null ||
            _editorBaseOriginal is null)
        {
            ClearObjectHoverPreviewCanaryV2();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed) return;

        Point point = ClampSelectionPointCanary(e.GetPosition(_editorSelectionOverlayCanary));
        if ((point - _editorObjectLastPointCanaryV2).Length < 5 &&
            DateTime.UtcNow - _editorObjectLastPreviewUtcCanaryV2 < TimeSpan.FromMilliseconds(110))
            return;
        if (DateTime.UtcNow - _editorObjectLastPreviewUtcCanaryV2 < TimeSpan.FromMilliseconds(75))
            return;

        _editorObjectLastPointCanaryV2 = point;
        _editorObjectLastPreviewUtcCanaryV2 = DateTime.UtcNow;
        BuildObjectHoverPreviewCanaryV2(point);
    }

    private void ObjectSelectionMouseDownRefinedCanaryV2(object sender, MouseButtonEventArgs e)
    {
        if (_editorSelectionToolCanary != CanarySelectionTool.Object || e.ChangedButton != MouseButton.Left)
            return;

        _editorSelectionDraggingCanary = false;
        if (_editorSelectionOverlayCanary?.IsMouseCaptured == true)
            _editorSelectionOverlayCanary.ReleaseMouseCapture();

        if (_editorObjectHoverMaskLowCanaryV2 is null ||
            _editorObjectLowWidthCanaryV2 <= 0 ||
            _editorObjectLowHeightCanaryV2 <= 0 ||
            !TrySelectionDimensionsCanary(out int width, out int height))
        {
            BuildObjectHoverPreviewCanaryV2(ClampSelectionPointCanary(e.GetPosition(_editorSelectionOverlayCanary!)));
        }

        if (_editorObjectHoverMaskLowCanaryV2 is null || !TrySelectionDimensionsCanary(out width, out height))
        {
            SetEditorStatus("Object Select could not identify a subject at that point. Try another point or use a lasso tool.");
            e.Handled = true;
            return;
        }

        bool[] fullMask = UpscaleObjectMaskCanaryV2(_editorObjectHoverMaskLowCanaryV2,
            _editorObjectLowWidthCanaryV2, _editorObjectLowHeightCanaryV2, width, height);
        int selected = fullMask.Count(value => value);
        if (selected < Math.Max(32, width * height / 5000))
        {
            SetEditorStatus("Object Select found too little connected detail. Increase Object tolerance or use Lasso.");
            e.Handled = true;
            return;
        }

        SetSelectionMaskCanary(fullMask, width, height,
            $"Object selected · {selected:N0} pixels. Hover somewhere else to preview another subject.");
        ClearObjectHoverPreviewCanaryV2();
        e.Handled = true;
    }

    private void BuildObjectHoverPreviewCanaryV2(Point point)
    {
        if (!EnsureObjectSelectionCacheCanaryV2() ||
            _editorObjectLowPixelsCanaryV2 is null ||
            _editorBaseOriginal is null)
            return;

        int sourceWidth = _editorBaseOriginal.PixelWidth;
        int sourceHeight = _editorBaseOriginal.PixelHeight;
        int lowWidth = _editorObjectLowWidthCanaryV2;
        int lowHeight = _editorObjectLowHeightCanaryV2;
        int seedX = Math.Clamp((int)Math.Round(point.X / Math.Max(1, sourceWidth - 1) * (lowWidth - 1)), 0, lowWidth - 1);
        int seedY = Math.Clamp((int)Math.Round(point.Y / Math.Max(1, sourceHeight - 1) * (lowHeight - 1)), 0, lowHeight - 1);

        bool[] mask = SegmentObjectAtPointCanaryV2(seedX, seedY, lowWidth, lowHeight, _editorObjectLowPixelsCanaryV2);
        int count = mask.Count(value => value);
        int total = lowWidth * lowHeight;
        if (count < 8 || count > total * 0.58)
        {
            _editorObjectHoverMaskLowCanaryV2 = null;
            ClearObjectHoverPreviewCanaryV2();
            return;
        }

        _editorObjectHoverMaskLowCanaryV2 = CloseObjectMaskCanaryV2(mask, lowWidth, lowHeight);
        RenderObjectHoverBoundaryCanaryV2(_editorObjectHoverMaskLowCanaryV2, lowWidth, lowHeight,
            sourceWidth, sourceHeight);

        if (_editorSelectionPreviewPathCanary is not null)
            _editorSelectionPreviewPathCanary.Data = null;
    }

    private bool EnsureObjectSelectionCacheCanaryV2()
    {
        BitmapSource? source = _editorBaseOriginal;
        if (source is null || EditorHasAnimatedGifV060) return false;
        if (ReferenceEquals(source, _editorObjectCacheSourceCanaryV2) && _editorObjectLowPixelsCanaryV2 is not null)
            return true;

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] full = new byte[stride * height];
        converted.CopyPixels(full, stride, 0);

        double scale = Math.Max(1.0, Math.Max(width / 360.0, height / 260.0));
        int lowWidth = Math.Max(1, (int)Math.Ceiling(width / scale));
        int lowHeight = Math.Max(1, (int)Math.Ceiling(height / scale));
        byte[] low = new byte[lowWidth * lowHeight * 3];

        for (int y = 0; y < lowHeight; y++)
        {
            int sourceY = Math.Clamp((int)Math.Round(y * (height - 1d) / Math.Max(1, lowHeight - 1)), 0, height - 1);
            for (int x = 0; x < lowWidth; x++)
            {
                int sourceX = Math.Clamp((int)Math.Round(x * (width - 1d) / Math.Max(1, lowWidth - 1)), 0, width - 1);
                int si = sourceY * stride + sourceX * 4;
                int di = (y * lowWidth + x) * 3;
                low[di] = full[si];
                low[di + 1] = full[si + 1];
                low[di + 2] = full[si + 2];
            }
        }

        _editorObjectCacheSourceCanaryV2 = source;
        _editorObjectLowPixelsCanaryV2 = low;
        _editorObjectLowWidthCanaryV2 = lowWidth;
        _editorObjectLowHeightCanaryV2 = lowHeight;
        return true;
    }

    private bool[] SegmentObjectAtPointCanaryV2(int seedX, int seedY, int width, int height, byte[] pixels)
    {
        int total = width * height;
        var mask = new bool[total];
        var queued = new bool[total];
        var queue = new Queue<int>();
        int seed = seedY * width + seedX;
        int seedOffset = seed * 3;
        double seedB = pixels[seedOffset];
        double seedG = pixels[seedOffset + 1];
        double seedR = pixels[seedOffset + 2];
        double tolerance = _editorObjectThresholdSliderCanary?.Value ?? 58;
        double seedLimit = Math.Clamp(72 + tolerance * 1.15, 80, 285);
        double localLimit = Math.Clamp(30 + tolerance * 0.55, 36, 135);
        int maxCells = (int)(total * 0.60);

        queue.Enqueue(seed);
        queued[seed] = true;
        int accepted = 0;

        while (queue.Count > 0 && accepted <= maxCells)
        {
            int current = queue.Dequeue();
            int x = current % width;
            int y = current / width;
            int ci = current * 3;
            double cb = pixels[ci];
            double cg = pixels[ci + 1];
            double cr = pixels[ci + 2];

            double seedDistance = ColorDistanceCanaryV2(cb, cg, cr, seedB, seedG, seedR);
            if (current != seed && seedDistance > seedLimit) continue;

            mask[current] = true;
            accepted++;

            TryQueue(x - 1, y, cb, cg, cr);
            TryQueue(x + 1, y, cb, cg, cr);
            TryQueue(x, y - 1, cb, cg, cr);
            TryQueue(x, y + 1, cb, cg, cr);
        }

        return mask;

        void TryQueue(int nx, int ny, double fromB, double fromG, double fromR)
        {
            if (nx < 0 || ny < 0 || nx >= width || ny >= height) return;
            int next = ny * width + nx;
            if (queued[next]) return;
            queued[next] = true;
            int ni = next * 3;
            double localDistance = ColorDistanceCanaryV2(
                pixels[ni], pixels[ni + 1], pixels[ni + 2], fromB, fromG, fromR);
            if (localDistance <= localLimit)
                queue.Enqueue(next);
        }
    }

    private static double ColorDistanceCanaryV2(double b1, double g1, double r1, double b2, double g2, double r2)
    {
        double db = b1 - b2;
        double dg = g1 - g2;
        double dr = r1 - r2;
        return Math.Sqrt(db * db * 0.7 + dg * dg * 1.15 + dr * dr);
    }

    private static bool[] CloseObjectMaskCanaryV2(bool[] source, int width, int height)
    {
        bool[] dilated = new bool[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool any = false;
                for (int oy = -1; oy <= 1 && !any; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        int ny = y + oy;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && source[ny * width + nx])
                        {
                            any = true;
                            break;
                        }
                    }
                dilated[y * width + x] = any;
            }
        }

        bool[] closed = new bool[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool all = true;
                for (int oy = -1; oy <= 1 && all; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        int ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || !dilated[ny * width + nx])
                        {
                            all = false;
                            break;
                        }
                    }
                closed[y * width + x] = all;
            }
        }
        return closed;
    }

    private void RenderObjectHoverBoundaryCanaryV2(bool[] mask, int width, int height, int displayWidth, int displayHeight)
    {
        if (_editorObjectHoverImageCanaryV2 is null) return;
        int stride = width * 4;
        byte[] overlay = new byte[stride * height];
        Color accent = (FindResource("Accent") as SolidColorBrush)?.Color ?? Color.FromRgb(91, 159, 239);

        bool Selected(int x, int y)
            => x >= 0 && y >= 0 && x < width && y < height && mask[y * width + x];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!Selected(x, y)) continue;
                bool edge = !Selected(x - 1, y) || !Selected(x + 1, y) || !Selected(x, y - 1) || !Selected(x, y + 1);
                int i = y * stride + x * 4;
                overlay[i] = accent.B;
                overlay[i + 1] = accent.G;
                overlay[i + 2] = accent.R;
                overlay[i + 3] = edge ? (byte)235 : (byte)24;
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, overlay, stride);
        bitmap.Freeze();
        _editorObjectHoverImageCanaryV2.Source = bitmap;
        _editorObjectHoverImageCanaryV2.Width = displayWidth;
        _editorObjectHoverImageCanaryV2.Height = displayHeight;
        Canvas.SetLeft(_editorObjectHoverImageCanaryV2, 0);
        Canvas.SetTop(_editorObjectHoverImageCanaryV2, 0);
    }

    private static bool[] UpscaleObjectMaskCanaryV2(bool[] low, int lowWidth, int lowHeight, int width, int height)
    {
        var full = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            int ly = Math.Clamp((int)((long)y * lowHeight / Math.Max(1, height)), 0, lowHeight - 1);
            for (int x = 0; x < width; x++)
            {
                int lx = Math.Clamp((int)((long)x * lowWidth / Math.Max(1, width)), 0, lowWidth - 1);
                full[y * width + x] = low[ly * lowWidth + lx];
            }
        }
        return full;
    }

    private void ClearObjectHoverPreviewCanaryV2()
    {
        _editorObjectHoverMaskLowCanaryV2 = null;
        if (_editorObjectHoverImageCanaryV2 is not null)
            _editorObjectHoverImageCanaryV2.Source = null;
    }
}
