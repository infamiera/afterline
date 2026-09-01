using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private Grid? _editorGuideHostCanary;
    private Grid? _editorCompositionFrameV078;
    private double _editorPasteboardOffsetXV078;
    private double _editorPasteboardOffsetYV078;

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
            VerticalAlignment = VerticalAlignment.Top,
            Background = System.Windows.Media.Brushes.Transparent,
            ClipToBounds = false
        };

        _editorCompositionFrameV078 = new Grid
        {
            Width = Math.Max(1, _editorComposition.Width),
            Height = Math.Max(1, _editorComposition.Height),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = false
        };
        _editorCompositionFrameV078.Children.Add(_editorComposition);
        host.Children.Add(_editorCompositionFrameV078);
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
        if (_editorGuideHostCanary is null || _editorComposition is null || _editorCompositionFrameV078 is null) return;

        double width = Math.Max(1, _editorComposition.Width);
        double height = Math.Max(1, _editorComposition.Height);
        // A bounded pasteboard gives transforms room on every side without
        // changing the Base Image/export dimensions. It is deliberately based
        // on the canvas rather than layer positions so its origin remains stable
        // while a pointer drag is in progress.
        _editorPasteboardOffsetXV078 = Math.Clamp(width * 0.70, 240, 1800);
        _editorPasteboardOffsetYV078 = Math.Clamp(height * 0.70, 240, 1400);
        _editorGuideHostCanary.Width = width + _editorPasteboardOffsetXV078 * 2;
        _editorGuideHostCanary.Height = height + _editorPasteboardOffsetYV078 * 2;
        _editorCompositionFrameV078.Width = width;
        _editorCompositionFrameV078.Height = height;
        _editorCompositionFrameV078.Margin = new Thickness(
            _editorPasteboardOffsetXV078,
            _editorPasteboardOffsetYV078,
            0,
            0);
        _editorComposition.ClipToBounds = false;
        _editorComposition.Margin = new Thickness(0);

        ApplyPasteboardOffsetToEditorOverlaysV078();

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

    private void ApplyPasteboardOffsetToEditorOverlaysV078()
    {
        if (_editorGuideHostCanary is null || _editorComposition is null)
            return;

        foreach (UIElement child in _editorGuideHostCanary.Children)
        {
            if (ReferenceEquals(child, _editorCompositionFrameV078))
                continue;
            child.RenderTransform = new System.Windows.Media.TranslateTransform(
                _editorPasteboardOffsetXV078,
                _editorPasteboardOffsetYV078);
        }
    }
}
