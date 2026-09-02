using System.Windows;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private Rect? _editorContentBoundaryV083;

    private void TrimContentToSelectedLayerV083()
    {
        if (_editorSelectedImageLayerV067 is not EditorImageLayerV067 layer || _editorComposition is null)
        {
            SetEditorStatus("Select an image layer before trimming the document to its outline.");
            return;
        }

        Rect canvas = new(0, 0, Math.Max(1, _editorComposition.Width), Math.Max(1, _editorComposition.Height));
        Rect boundary = Rect.Intersect(canvas, new Rect(layer.X, layer.Y, layer.Width, layer.Height));
        if (boundary.IsEmpty || boundary.Width < 1 || boundary.Height < 1)
        {
            SetEditorStatus("The selected layer does not overlap the Base Image, so it cannot define an export boundary.");
            return;
        }

        PushEditorDocumentHistoryV081("content boundary trim");
        _editorContentBoundaryV083 = boundary;
        ApplyEditorContentBoundaryV083();
        SetEditorStatus(
            $"Content trimmed to the selected {boundary.Width:N0} × {boundary.Height:N0}px outline. The {_editorComposition.Width:N0} × {_editorComposition.Height:N0}px canvas is unchanged; Undo restores the previous boundary.");
    }

    private void ApplyEditorContentBoundaryV083()
    {
        if (_editorComposition is null)
            return;

        if (_editorContentBoundaryV083 is not Rect boundary || boundary.IsEmpty ||
            boundary.Width < 1 || boundary.Height < 1)
        {
            _editorComposition.Clip = null;
            _editorContentBoundaryV083 = null;
        }
        else
        {
            Rect canvas = new(0, 0, Math.Max(1, _editorComposition.Width), Math.Max(1, _editorComposition.Height));
            Rect clipped = Rect.Intersect(canvas, boundary);
            if (clipped.IsEmpty || clipped.Width < 1 || clipped.Height < 1)
            {
                _editorComposition.Clip = null;
                _editorContentBoundaryV083 = null;
            }
            else
            {
                _editorContentBoundaryV083 = clipped;
                var geometry = new RectangleGeometry(clipped);
                geometry.Freeze();
                _editorComposition.Clip = geometry;
            }
        }

        foreach (EditorImageLayerV067 imageLayer in _editorImageLayersV067)
            UpdateImageLayerPasteboardPresentationV080(imageLayer);
        if (_editorClearContentBoundaryButtonV083 is not null)
            _editorClearContentBoundaryButtonV083.IsEnabled = _editorContentBoundaryV083 is not null;
        RefreshSelectedLayerAdornerV068();
    }

    private void RemoveEditorContentBoundaryV083()
    {
        if (_editorContentBoundaryV083 is null)
            return;

        PushEditorDocumentHistoryV081("clear content boundary");
        ClearEditorContentBoundaryV083();
        ApplyEditorContentBoundaryV083();
        SetEditorStatus("Content trim cleared. Undo restores the trimmed boundary.");
    }

    private void ClearEditorContentBoundaryV083()
    {
        _editorContentBoundaryV083 = null;
        if (_editorComposition is not null)
            _editorComposition.Clip = null;
        if (_editorClearContentBoundaryButtonV083 is not null)
            _editorClearContentBoundaryButtonV083.IsEnabled = false;
    }

    private static void VerifyEditorContentBoundaryGeometryV083()
    {
        Rect canvas = new(0, 0, 1920, 1080);
        Rect boundary = Rect.Intersect(canvas, new Rect(-120, 100, 900, 700));
        if (boundary.X != 0 || boundary.Y != 100 || boundary.Width != 780 || boundary.Height != 700 ||
            canvas.Width != 1920 || canvas.Height != 1080)
        {
            throw new InvalidOperationException(
                "The selected-layer content boundary did not clip to the fixed Base Image canvas.");
        }
    }
}
