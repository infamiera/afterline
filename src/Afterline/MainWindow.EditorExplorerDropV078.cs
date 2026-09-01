using System.Windows;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private void ImportDroppedEditorImagesV078(IReadOnlyList<string> paths, Point dropPoint)
    {
        if (paths.Count == 0 || _editorComposition is null)
            return;

        int next = 0;
        if (!EditorHasRenderedBaseImageV079())
        {
            // The first dropped image establishes the non-destructive export
            // boundary. Any additional files in the same Explorer drop become
            // ordinary image layers above it.
            LoadEditorMediaV060(paths[0]);
            if (!EditorHasRenderedBaseImageV079())
                return;
            next = 1;
        }

        EditorImageLayerV067? lastAdded = null;
        int added = 0;
        for (int index = next; index < paths.Count; index++)
        {
            string path = paths[index];
            try
            {
                BitmapSource bitmap = LoadBitmapFileV067(path);
                (double width, double height, double x, double y) =
                    CalculateDroppedLayerPlacementV078(bitmap, dropPoint, added);
                lastAdded = AddImageLayerFromBitmapV067(
                    bitmap,
                    Path.GetFileName(path),
                    x,
                    y,
                    width: width,
                    height: height,
                    refresh: false);
                added++;
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error($"Unable to add dropped Editor image layer '{Path.GetFileName(path)}'.", ex);
                SetEditorStatus($"Could not add {Path.GetFileName(path)} as an image layer.");
            }
        }

        if (lastAdded is null)
        {
            SetEditorStatus($"Loaded {Path.GetFileName(paths[0])} as the Base Image. Drop another image to add a layer.");
            return;
        }

        UpdateEditorLayerZOrderV067();
        EnsureLayerCanvasExtentV067();
        RefreshLayerListV067(lastAdded);
        SelectImageLayerV068(lastAdded);
        RefreshSelectedLayerAdornerV068();
        SetEditorStatus(added == 1
            ? $"Added {lastAdded.Name} as an image layer. Drag inside it to move; use the handles to resize."
            : $"Added {added:N0} image layers. The last layer is ready to move or resize.");

        if (_editorFitZoom || !IsLayerFullyVisibleV078(lastAdded))
            _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private bool EditorHasRenderedBaseImageV079()
        => _editorBaseOriginal is not null || _editorBaseImage?.Source is not null;

    private (double Width, double Height, double X, double Y) CalculateDroppedLayerPlacementV078(
        BitmapSource bitmap,
        Point dropPoint,
        int cascadeIndex)
    {
        (double baseWidth, double baseHeight) = GetBaseImageBoundsV068();
        double maximumWidth = Math.Max(64, baseWidth * 0.72);
        double maximumHeight = Math.Max(64, baseHeight * 0.72);
        double scale = Math.Min(1, Math.Min(maximumWidth / bitmap.PixelWidth, maximumHeight / bitmap.PixelHeight));
        double width = Math.Max(1, bitmap.PixelWidth * scale);
        double height = Math.Max(1, bitmap.PixelHeight * scale);

        bool pointIsUseful = double.IsFinite(dropPoint.X) &&
                             double.IsFinite(dropPoint.Y) &&
                             dropPoint.X >= 0 && dropPoint.X <= baseWidth &&
                             dropPoint.Y >= 0 && dropPoint.Y <= baseHeight;
        double centerX = pointIsUseful ? dropPoint.X : baseWidth / 2;
        double centerY = pointIsUseful ? dropPoint.Y : baseHeight / 2;
        double cascade = cascadeIndex * 18;
        return (width, height, centerX - width / 2 + cascade, centerY - height / 2 + cascade);
    }

    private bool IsLayerFullyVisibleV078(EditorImageLayerV067 layer)
    {
        if (_editorPreviewScroll is null || _editorComposition is null)
            return true;
        try
        {
            Point topLeft = _editorComposition.TranslatePoint(new Point(layer.X, layer.Y), _editorPreviewScroll);
            Point bottomRight = _editorComposition.TranslatePoint(
                new Point(layer.X + layer.Width, layer.Y + layer.Height),
                _editorPreviewScroll);
            return topLeft.X >= 0 && topLeft.Y >= 0 &&
                   bottomRight.X <= _editorPreviewScroll.ViewportWidth &&
                   bottomRight.Y <= _editorPreviewScroll.ViewportHeight;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
