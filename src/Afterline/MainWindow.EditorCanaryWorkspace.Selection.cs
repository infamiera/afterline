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
    private FrameworkElement BuildSelectionToolPanelCanary()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Selections limit Filters & Adjustments to part of a still screenshot. Selected edges are outlined on the canvas. Object Select is experimental and works best when the subject contrasts with its background."));

        var tools = new WrapPanel();
        tools.Children.Add(CreateSelectionButtonCanary("Rectangular", CanarySelectionTool.Rectangular,
            "Drag a rectangular marquee around the area you want to edit."));
        tools.Children.Add(CreateSelectionButtonCanary("Lasso", CanarySelectionTool.Lasso,
            "Hold the left mouse button and draw a freehand selection."));
        tools.Children.Add(CreateSelectionButtonCanary("Polygonal", CanarySelectionTool.Polygonal,
            "Click points around the subject. Double-click or press Enter to close the polygon."));
        tools.Children.Add(CreateSelectionButtonCanary("Object Select", CanarySelectionTool.Object,
            "Experimental: drag around a subject. Afterline estimates foreground pixels from local color contrast."));
        content.Children.Add(tools);

        var threshold = CreateEditorV041Slider("Object sensitivity", 20, 180, 58, 2);
        _editorObjectThresholdSliderCanary = threshold.Slider;
        _editorObjectThresholdSliderCanary.ToolTip = "Higher values require stronger color contrast from the selected area's border.";
        content.Children.Add(threshold.Panel);

        var actions = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
        actions.Children.Add(CreateSmallEditorButton("Select All", (_, _) => SelectAllCanary()));
        actions.Children.Add(CreateSmallEditorButton("Invert", (_, _) => InvertSelectionCanary()));
        actions.Children.Add(CreateSmallEditorButton("Clear", (_, _) => ClearSelectionCanary()));
        content.Children.Add(actions);

        content.Children.Add(EditorSubtleNote(
            "Selection borders and alignment guides are editor-only overlays and are never included in exported screenshots."));
        return WrapEditorToolPanel(content);
    }

    private Button CreateSelectionButtonCanary(string text, CanarySelectionTool tool, string tooltip)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(9, 6, 9, 6),
            Margin = new Thickness(0, 0, 6, 6),
            ToolTip = tooltip
        };
        button.Click += (_, _) => ActivateSelectionToolCanary(tool);
        return button;
    }

    private void ConfigureSelectionOverlayCanary()
    {
        if (_editorComposition is null) return;

        _editorSelectionOverlayCanary = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };
        _editorSelectionOverlayCanary.PreviewMouseLeftButtonDown += SelectionMouseDownCanary;
        _editorSelectionOverlayCanary.PreviewMouseMove += SelectionMouseMoveCanary;
        _editorSelectionOverlayCanary.PreviewMouseLeftButtonUp += SelectionMouseUpCanary;
        _editorSelectionOverlayCanary.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (_editorSelectionToolCanary == CanarySelectionTool.Polygonal)
            {
                CommitPolygonSelectionCanary();
                e.Handled = true;
            }
        };

        _editorSelectionBoundaryImageCanary = new Image
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_editorSelectionBoundaryImageCanary, 4);
        _editorComposition.Children.Add(_editorSelectionBoundaryImageCanary);

        _editorSelectionPreviewPathCanary = new System.Windows.Shapes.Path
        {
            Stroke = (Brush)FindResource("Accent"),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(20, 91, 159, 239)),
            IsHitTestVisible = false
        };
        _editorSelectionOverlayCanary.Children.Add(_editorSelectionPreviewPathCanary);
        Panel.SetZIndex(_editorSelectionOverlayCanary, 7);
        _editorComposition.Children.Add(_editorSelectionOverlayCanary);

        ResizeCanaryOverlays();
        PreviewKeyDown += SelectionKeyDownCanary;
    }

    private void ActivateSelectionToolCanary(CanarySelectionTool tool)
    {
        if (_editorBaseOriginal is null)
        {
            SetEditorStatus("Load a screenshot before using selection tools.");
            _editorSelectionToolCanary = CanarySelectionTool.None;
            DeactivateSelectionInteractionCanary();
            return;
        }

        _editorSelectionToolCanary = tool;
        _editorSelectionDraggingCanary = false;
        _editorSelectionPointsCanary.Clear();

        // Existing chat dragging intentionally treats "markup" as a non-drag mode.
        // Reuse that guard while the selection overlay owns the pointer.
        _editorActiveToolKey = "markup";

        if (_editorSelectionOverlayCanary is not null)
        {
            _editorSelectionOverlayCanary.IsHitTestVisible = true;
            _editorSelectionOverlayCanary.Cursor = Cursors.Cross;
        }
        if (_editorInkCanvas is not null)
            _editorInkCanvas.IsHitTestVisible = false;

        SetEditorStatus(tool switch
        {
            CanarySelectionTool.Rectangular => "Rectangular Marquee active · drag to select.",
            CanarySelectionTool.Lasso => "Lasso active · draw around the area with the left mouse button.",
            CanarySelectionTool.Polygonal => "Polygonal Lasso active · click points, then double-click or press Enter to close.",
            CanarySelectionTool.Object => "Object Select active · drag around the subject; a preview box follows the pointer.",
            _ => "Selection tool ready."
        });
    }

    private void DeactivateSelectionInteractionCanary()
    {
        _editorSelectionToolCanary = CanarySelectionTool.None;
        _editorSelectionDraggingCanary = false;
        _editorSelectionPointsCanary.Clear();
        if (_editorSelectionOverlayCanary is not null)
        {
            _editorSelectionOverlayCanary.IsHitTestVisible = false;
            _editorSelectionOverlayCanary.Cursor = Cursors.Arrow;
        }
        if (_editorSelectionPreviewPathCanary is not null)
            _editorSelectionPreviewPathCanary.Data = null;
        if (_editorInkCanvas is not null)
            _editorInkCanvas.IsHitTestVisible = true;
    }

    private void SelectionMouseDownCanary(object sender, MouseButtonEventArgs e)
    {
        if (_editorSelectionOverlayCanary is null || _editorSelectionToolCanary == CanarySelectionTool.None)
            return;

        Point p = ClampSelectionPointCanary(e.GetPosition(_editorSelectionOverlayCanary));
        _editorSelectionStartCanary = p;

        if (_editorSelectionToolCanary == CanarySelectionTool.Polygonal)
        {
            _editorSelectionPointsCanary.Add(p);
            UpdatePolygonPreviewCanary(p);
            if (e.ClickCount >= 2)
                CommitPolygonSelectionCanary();
            e.Handled = true;
            return;
        }

        _editorSelectionDraggingCanary = true;
        _editorSelectionPointsCanary.Clear();
        _editorSelectionPointsCanary.Add(p);
        _editorSelectionOverlayCanary.CaptureMouse();
        e.Handled = true;
    }

    private void SelectionMouseMoveCanary(object sender, MouseEventArgs e)
    {
        if (_editorSelectionOverlayCanary is null || _editorSelectionToolCanary == CanarySelectionTool.None)
            return;

        Point p = ClampSelectionPointCanary(e.GetPosition(_editorSelectionOverlayCanary));
        _editorSelectionHoverCanary = p;

        if (_editorSelectionToolCanary == CanarySelectionTool.Polygonal)
        {
            if (_editorSelectionPointsCanary.Count > 0)
                UpdatePolygonPreviewCanary(p);
            return;
        }

        if (!_editorSelectionDraggingCanary || e.LeftButton != MouseButtonState.Pressed)
        {
            if (_editorSelectionToolCanary == CanarySelectionTool.Object)
                DrawObjectHoverPreviewCanary(p);
            return;
        }

        if (_editorSelectionToolCanary == CanarySelectionTool.Lasso)
        {
            if (_editorSelectionPointsCanary.Count == 0 ||
                (p - _editorSelectionPointsCanary[^1]).Length >= 2.0)
                _editorSelectionPointsCanary.Add(p);
            DrawPolygonPreviewCanary(_editorSelectionPointsCanary, close: false, hover: p);
        }
        else
        {
            DrawRectanglePreviewCanary(NormalizeRectCanary(_editorSelectionStartCanary, p));
        }

        e.Handled = true;
    }

    private void SelectionMouseUpCanary(object sender, MouseButtonEventArgs e)
    {
        if (!_editorSelectionDraggingCanary || _editorSelectionOverlayCanary is null)
            return;

        Point end = ClampSelectionPointCanary(e.GetPosition(_editorSelectionOverlayCanary));
        _editorSelectionDraggingCanary = false;
        if (_editorSelectionOverlayCanary.IsMouseCaptured)
            _editorSelectionOverlayCanary.ReleaseMouseCapture();

        if (_editorSelectionToolCanary == CanarySelectionTool.Lasso)
        {
            _editorSelectionPointsCanary.Add(end);
            CommitPolygonMaskCanary(_editorSelectionPointsCanary);
        }
        else
        {
            Rect rect = NormalizeRectCanary(_editorSelectionStartCanary, end);
            if (_editorSelectionToolCanary == CanarySelectionTool.Object && (rect.Width < 8 || rect.Height < 8))
                rect = ObjectHoverRectCanary(end);

            if (_editorSelectionToolCanary == CanarySelectionTool.Object)
                CommitObjectSelectionCanary(rect);
            else
                CommitRectangleSelectionCanary(rect);
        }

        e.Handled = true;
    }

    private void SelectionKeyDownCanary(object sender, KeyEventArgs e)
    {
        if (_editorSelectionToolCanary == CanarySelectionTool.None) return;

        if (e.Key == Key.Escape)
        {
            DeactivateSelectionInteractionCanary();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _editorSelectionToolCanary == CanarySelectionTool.Polygonal)
        {
            CommitPolygonSelectionCanary();
            e.Handled = true;
        }
    }

    private void CommitPolygonSelectionCanary()
    {
        if (_editorSelectionPointsCanary.Count >= 3)
            CommitPolygonMaskCanary(_editorSelectionPointsCanary);
        else
            SetEditorStatus("Polygonal Lasso needs at least three points.");

        _editorSelectionPointsCanary.Clear();
        if (_editorSelectionPreviewPathCanary is not null)
            _editorSelectionPreviewPathCanary.Data = null;
    }

    private void CommitRectangleSelectionCanary(Rect rect)
    {
        if (!TrySelectionDimensionsCanary(out int width, out int height) || rect.Width < 1 || rect.Height < 1)
            return;

        var mask = new bool[width * height];
        int left = Math.Clamp((int)Math.Floor(rect.Left), 0, width - 1);
        int top = Math.Clamp((int)Math.Floor(rect.Top), 0, height - 1);
        int right = Math.Clamp((int)Math.Ceiling(rect.Right), 0, width);
        int bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), 0, height);

        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                mask[y * width + x] = true;

        SetSelectionMaskCanary(mask, width, height, $"Selected {right - left:N0} × {bottom - top:N0}px area.");
    }

    private void CommitPolygonMaskCanary(IReadOnlyList<Point> points)
    {
        if (!TrySelectionDimensionsCanary(out int width, out int height) || points.Count < 3)
            return;

        var mask = new bool[width * height];
        double minX = Math.Max(0, points.Min(p => p.X));
        double maxX = Math.Min(width - 1, points.Max(p => p.X));
        double minY = Math.Max(0, points.Min(p => p.Y));
        double maxY = Math.Min(height - 1, points.Max(p => p.Y));

        int left = (int)Math.Floor(minX);
        int right = (int)Math.Ceiling(maxX);
        int top = (int)Math.Floor(minY);
        int bottom = (int)Math.Ceiling(maxY);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (PointInPolygonCanary(x + 0.5, y + 0.5, points))
                    mask[y * width + x] = true;
            }
        }

        SetSelectionMaskCanary(mask, width, height, "Lasso selection created.");
    }

    private void CommitObjectSelectionCanary(Rect rect)
    {
        if (_editorBaseOriginal is null || !TrySelectionDimensionsCanary(out int width, out int height))
            return;

        rect.Intersect(new Rect(0, 0, width, height));
        if (rect.Width < 2 || rect.Height < 2) return;

        FormatConvertedBitmap converted = new(_editorBaseOriginal, PixelFormats.Bgra32, null, 0);
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        int left = Math.Clamp((int)Math.Floor(rect.Left), 0, width - 1);
        int right = Math.Clamp((int)Math.Ceiling(rect.Right), left + 1, width);
        int top = Math.Clamp((int)Math.Floor(rect.Top), 0, height - 1);
        int bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), top + 1, height);

        double sumR = 0, sumG = 0, sumB = 0;
        int samples = 0;
        int step = Math.Max(1, Math.Min(right - left, bottom - top) / 80);

        void Sample(int x, int y)
        {
            int i = y * stride + x * 4;
            sumB += pixels[i];
            sumG += pixels[i + 1];
            sumR += pixels[i + 2];
            samples++;
        }

        for (int x = left; x < right; x += step)
        {
            Sample(x, top);
            Sample(x, bottom - 1);
        }
        for (int y = top; y < bottom; y += step)
        {
            Sample(left, y);
            Sample(right - 1, y);
        }

        if (samples == 0) return;
        double bgR = sumR / samples;
        double bgG = sumG / samples;
        double bgB = sumB / samples;
        double threshold = _editorObjectThresholdSliderCanary?.Value ?? 58;

        var mask = new bool[width * height];
        int selected = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                int i = y * stride + x * 4;
                double db = pixels[i] - bgB;
                double dg = pixels[i + 1] - bgG;
                double dr = pixels[i + 2] - bgR;
                double distance = Math.Sqrt(dr * dr + dg * dg + db * db);
                if (distance >= threshold)
                {
                    mask[y * width + x] = true;
                    selected++;
                }
            }
        }

        if (selected < Math.Max(16, (right - left) * (bottom - top) / 200))
        {
            SetEditorStatus("Object Select found too little foreground detail. Lower sensitivity or use Lasso to refine it.");
            DrawRectanglePreviewCanary(rect);
            return;
        }

        SetSelectionMaskCanary(mask, width, height,
            $"Experimental object selection created · {selected:N0} foreground pixels.");
    }

    private void SetSelectionMaskCanary(bool[] mask, int width, int height, string status)
    {
        _editorSelectionMaskCanary = mask;
        _editorSelectionWidthCanary = width;
        _editorSelectionHeightCanary = height;
        if (_editorSelectionPreviewPathCanary is not null)
            _editorSelectionPreviewPathCanary.Data = null;
        RenderSelectionBoundaryCanary();
        ScheduleCanaryFilterPreview();
        SetEditorStatus(status);
    }

    private void SelectAllCanary()
    {
        if (!TrySelectionDimensionsCanary(out int width, out int height)) return;
        var mask = Enumerable.Repeat(true, width * height).ToArray();
        SetSelectionMaskCanary(mask, width, height, "Entire screenshot selected.");
    }

    private void InvertSelectionCanary()
    {
        if (!TrySelectionDimensionsCanary(out int width, out int height)) return;
        if (_editorSelectionMaskCanary is null ||
            _editorSelectionWidthCanary != width ||
            _editorSelectionHeightCanary != height)
        {
            SelectAllCanary();
            return;
        }

        for (int i = 0; i < _editorSelectionMaskCanary.Length; i++)
            _editorSelectionMaskCanary[i] = !_editorSelectionMaskCanary[i];

        RenderSelectionBoundaryCanary();
        ScheduleCanaryFilterPreview();
        SetEditorStatus("Selection inverted.");
    }

    private void ClearSelectionCanary()
    {
        _editorSelectionMaskCanary = null;
        _editorSelectionWidthCanary = 0;
        _editorSelectionHeightCanary = 0;
        if (_editorSelectionBoundaryImageCanary is not null)
            _editorSelectionBoundaryImageCanary.Source = null;
        if (_editorSelectionPreviewPathCanary is not null)
            _editorSelectionPreviewPathCanary.Data = null;
        ScheduleCanaryFilterPreview();
        SetEditorStatus("Selection cleared. Filters now affect the entire image.");
    }

    private bool TrySelectionDimensionsCanary(out int width, out int height)
    {
        BitmapSource? source = _editorBaseOriginal;
        width = source?.PixelWidth ?? 0;
        height = source?.PixelHeight ?? 0;
        return width > 0 && height > 0;
    }

    private void RenderSelectionBoundaryCanary()
    {
        if (_editorSelectionBoundaryImageCanary is null ||
            _editorSelectionMaskCanary is null ||
            _editorSelectionWidthCanary <= 0 ||
            _editorSelectionHeightCanary <= 0)
            return;

        int width = _editorSelectionWidthCanary;
        int height = _editorSelectionHeightCanary;
        int stride = width * 4;
        byte[] outline = new byte[stride * height];
        Color accent = (FindResource("Accent") as SolidColorBrush)?.Color ?? Color.FromRgb(91, 159, 239);

        bool IsSelected(int x, int y)
            => x >= 0 && y >= 0 && x < width && y < height && _editorSelectionMaskCanary[y * width + x];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsSelected(x, y)) continue;
                bool edge = !IsSelected(x - 1, y) || !IsSelected(x + 1, y) ||
                            !IsSelected(x, y - 1) || !IsSelected(x, y + 1);
                if (!edge) continue;

                int i = y * stride + x * 4;
                outline[i] = accent.B;
                outline[i + 1] = accent.G;
                outline[i + 2] = accent.R;
                outline[i + 3] = 235;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, outline, stride);
        bitmap.Freeze();
        _editorSelectionBoundaryImageCanary.Source = bitmap;
        _editorSelectionBoundaryImageCanary.Width = width;
        _editorSelectionBoundaryImageCanary.Height = height;
        _editorSelectionBoundaryImageCanary.Margin = new Thickness(0);
    }

    private void DrawRectanglePreviewCanary(Rect rect)
    {
        if (_editorSelectionPreviewPathCanary is null) return;
        _editorSelectionPreviewPathCanary.Data = new RectangleGeometry(rect);
    }

    private void DrawObjectHoverPreviewCanary(Point p) => DrawRectanglePreviewCanary(ObjectHoverRectCanary(p));

    private Rect ObjectHoverRectCanary(Point p)
    {
        double width = Math.Min(180, Math.Max(40, (_editorComposition?.Width ?? 180) * 0.18));
        double height = Math.Min(240, Math.Max(60, (_editorComposition?.Height ?? 240) * 0.28));
        double maxX = Math.Max(0, (_editorComposition?.Width ?? width) - width);
        double maxY = Math.Max(0, (_editorComposition?.Height ?? height) - height);
        return new Rect(
            Math.Clamp(p.X - width / 2, 0, maxX),
            Math.Clamp(p.Y - height / 2, 0, maxY),
            width, height);
    }

    private void DrawPolygonPreviewCanary(IReadOnlyList<Point> points, bool close, Point? hover = null)
    {
        if (_editorSelectionPreviewPathCanary is null || points.Count == 0) return;
        var figure = new PathFigure { StartPoint = points[0], IsClosed = close, IsFilled = close };
        for (int i = 1; i < points.Count; i++)
            figure.Segments.Add(new LineSegment(points[i], true));
        if (hover is Point h)
            figure.Segments.Add(new LineSegment(h, true));
        _editorSelectionPreviewPathCanary.Data = new PathGeometry(new[] { figure });
    }

    private void UpdatePolygonPreviewCanary(Point hover)
        => DrawPolygonPreviewCanary(_editorSelectionPointsCanary, close: false, hover: hover);

    private Point ClampSelectionPointCanary(Point p)
    {
        double width = Math.Max(1, _editorComposition?.Width ?? 1);
        double height = Math.Max(1, _editorComposition?.Height ?? 1);
        return new Point(Math.Clamp(p.X, 0, width - 1), Math.Clamp(p.Y, 0, height - 1));
    }

    private static Rect NormalizeRectCanary(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static bool PointInPolygonCanary(double x, double y, IReadOnlyList<Point> points)
    {
        bool inside = false;
        int j = points.Count - 1;
        for (int i = 0; i < points.Count; j = i++)
        {
            Point pi = points[i];
            Point pj = points[j];
            bool intersects = ((pi.Y > y) != (pj.Y > y)) &&
                              (x < (pj.X - pi.X) * (y - pi.Y) / ((pj.Y - pi.Y) == 0 ? 0.000001 : (pj.Y - pi.Y)) + pi.X);
            if (intersects) inside = !inside;
        }
        return inside;
    }
}
