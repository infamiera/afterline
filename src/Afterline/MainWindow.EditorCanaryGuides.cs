using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private Grid? _editorGuideHostCanary;

    private void MoveCanaryEditorGuidesOutsideComposition()
    {
        if (_editorGuideHostCanary is not null ||
            _editorComposition is null ||
            _editorZoomHost is null ||
            !ReferenceEquals(_editorZoomHost.Child, _editorComposition))
            return;

        _editorZoomHost.Child = null;

        var host = new Grid
        {
            Width = Math.Max(1, _editorComposition.Width),
            Height = Math.Max(1, _editorComposition.Height),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        host.Children.Add(_editorComposition);
        ReparentEditorGuideCanary(_editorSelectionBoundaryImageCanary, host, 4);
        ReparentEditorGuideCanary(_editorSnapGuideCanvasCanary, host, 6);
        ReparentEditorGuideCanary(_editorSelectionOverlayCanary, host, 7);

        _editorGuideHostCanary = host;
        _editorZoomHost.Child = host;

        _editorComposition.SizeChanged += (_, _) => SyncCanaryGuideHostSize();
        SyncCanaryGuideHostSize();
    }

    private static void ReparentEditorGuideCanary(UIElement? element, Grid target, int zIndex)
    {
        if (element is null) return;

        if (element is FrameworkElement framework && framework.Parent is Panel parent)
            parent.Children.Remove(element);

        Panel.SetZIndex(element, zIndex);
        target.Children.Add(element);
    }

    private void SyncCanaryGuideHostSize()
    {
        if (_editorGuideHostCanary is null || _editorComposition is null) return;

        double width = Math.Max(1, _editorComposition.Width);
        double height = Math.Max(1, _editorComposition.Height);
        _editorGuideHostCanary.Width = width;
        _editorGuideHostCanary.Height = height;

        if (_editorSelectionOverlayCanary is not null)
        {
            _editorSelectionOverlayCanary.Width = width;
            _editorSelectionOverlayCanary.Height = height;
        }

        if (_editorSnapGuideCanvasCanary is not null)
        {
            _editorSnapGuideCanvasCanary.Width = width;
            _editorSnapGuideCanvasCanary.Height = height;
        }
    }
}
