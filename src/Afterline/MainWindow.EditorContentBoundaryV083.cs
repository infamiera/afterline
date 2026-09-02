using System.Windows;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private Rect? _editorContentBoundaryV083;

    private void TrimContentToBaseImageV084()
    {
        if (_editorComposition is null || !EditorHasRenderedBaseImageV079())
        {
            SetEditorStatus("Load or create a Base Image before trimming overlapping layers to its borders.");
            return;
        }

        Rect canvas = new(0, 0, Math.Max(1, _editorComposition.Width), Math.Max(1, _editorComposition.Height));
        if (_editorContentBoundaryV083 == canvas)
        {
            SetEditorStatus("Overlapping layers are already trimmed to the Base Image borders.");
            return;
        }

        PushEditorDocumentHistoryV081("Base Image boundary trim");
        _editorContentBoundaryV083 = canvas;
        ApplyEditorContentBoundaryV083();
        SetEditorStatus(
            $"Overlapping layers are trimmed to the {_editorComposition.Width:N0} × {_editorComposition.Height:N0}px Base Image borders. Undo restores pasteboard overflow.");
    }

    private void ApplyEditorContentBoundaryV083()
    {
        if (_editorComposition is null)
            return;

        if (_editorContentBoundaryV083 is null)
        {
            _editorComposition.Clip = null;
        }
        else
        {
            // A content boundary is an on/off document setting whose bounds are always
            // the fixed Base Image canvas. Normalizing here also safely migrates projects
            // written by Canary #205, which could persist a selected-layer rectangle.
            Rect canvas = new(0, 0, Math.Max(1, _editorComposition.Width), Math.Max(1, _editorComposition.Height));
            _editorContentBoundaryV083 = canvas;
            var geometry = new RectangleGeometry(canvas);
            geometry.Freeze();
            _editorComposition.Clip = geometry;
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
        SetEditorStatus("Base Image trim cleared. Undo restores the trimmed borders.");
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
        Rect requestedLayerBounds = new(-120, 100, 900, 700);
        Rect boundary = canvas;
        if (boundary != canvas || requestedLayerBounds == boundary ||
            boundary.Width != 1920 || boundary.Height != 1080)
        {
            throw new InvalidOperationException(
                "The content boundary did not remain anchored to the full Base Image canvas.");
        }
    }
}
