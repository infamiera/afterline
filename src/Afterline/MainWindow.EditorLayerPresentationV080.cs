using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Afterline;

public partial class MainWindow
{
    private void ConfigureEditorLayerPresentationV080()
    {
        if (_editorGuideHostCanary is null || _editorBaseOutlineV080 is not null)
            return;

        var pink = new SolidColorBrush(Color.FromRgb(0xFF, 0x4F, 0xA3));
        pink.Freeze();
        _editorBaseOutlineV080 = new Rectangle
        {
            Fill = Brushes.Transparent,
            Stroke = pink,
            StrokeThickness = 0.65,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_editorBaseOutlineV080, 900);
        _editorGuideHostCanary.Children.Add(_editorBaseOutlineV080);
        RefreshBaseImageOutlineV080();
    }

    private void RefreshBaseImageOutlineV080()
    {
        if (_editorBaseOutlineV080 is null || _editorComposition is null)
            return;

        bool show = _editorBaseOutlineCheckV080?.IsChecked == true && EditorHasRenderedBaseImageV079();
        _editorBaseOutlineV080.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
            return;

        _editorBaseOutlineV080.Width = Math.Max(1, _editorComposition.Width);
        _editorBaseOutlineV080.Height = Math.Max(1, _editorComposition.Height);
        _editorBaseOutlineV080.Margin = new Thickness(0);
        _editorBaseOutlineV080.StrokeThickness = 0.65 / Math.Max(0.1, _editorZoomScale);
        _editorBaseOutlineV080.RenderTransform = new TranslateTransform(
            _editorPasteboardOffsetXV078,
            _editorPasteboardOffsetYV078);
    }

    private void UpdateImageLayerPasteboardPresentationV080(EditorImageLayerV067 layer)
    {
        double width = Math.Max(1, layer.Width);
        double height = Math.Max(1, layer.Height);
        double radius = Math.Clamp(layer.CornerRadius, 0, Math.Min(width, height) / 2);

        layer.Image.Clip = radius > 0
            ? CreateRoundedLayerGeometryV080(width, height, radius)
            : null;

        Image preview = layer.PasteboardImage;
        preview.Source = layer.Bitmap;
        preview.Width = width;
        preview.Height = height;
        preview.Margin = new Thickness(layer.X, layer.Y, 0, 0);
        preview.Opacity = layer.Opacity;
        preview.RenderTransform = new TranslateTransform(
            _editorPasteboardOffsetXV078,
            _editorPasteboardOffsetYV078);

        if (!layer.IsVisible || !EditorHasRenderedBaseImageV079() || _editorComposition is null)
        {
            preview.Visibility = Visibility.Collapsed;
            preview.Clip = null;
            return;
        }

        Geometry layerGeometry = CreateRoundedLayerGeometryV080(width, height, radius);
        var baseGeometry = new RectangleGeometry(new Rect(
            -layer.X,
            -layer.Y,
            Math.Max(1, _editorComposition.Width),
            Math.Max(1, _editorComposition.Height)));
        Geometry overflow = Geometry.Combine(
            layerGeometry,
            baseGeometry,
            GeometryCombineMode.Exclude,
            transform: null);
        if (overflow.CanFreeze)
            overflow.Freeze();

        preview.Clip = overflow;
        preview.Visibility = Visibility.Visible;
    }

    private static Geometry CreateRoundedLayerGeometryV080(double width, double height, double radius)
    {
        var geometry = new RectangleGeometry(
            new Rect(0, 0, Math.Max(1, width), Math.Max(1, height)),
            radius,
            radius);
        geometry.Freeze();
        return geometry;
    }
}
