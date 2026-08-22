using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _editorPositioningV061Initialized;
    private bool _editorChatDragActiveV061;
    private Point _editorChatDragStartV061;
    private double _editorChatDragStartXV061;
    private double _editorChatDragStartYV061;

    private void EnsureEditorPositioningV061()
    {
        if (_editorPositioningV061Initialized ||
            _editorComposition is null ||
            _editorChatXSlider is null ||
            _editorChatYSlider is null)
            return;

        _editorPositioningV061Initialized = true;

        ConfigureEditorPositionSliderV061(_editorChatXSlider);
        ConfigureEditorPositionSliderV061(_editorChatYSlider);
        MoveEditorPositionControlsToChatPanelV061();

        _editorComposition.PreviewMouseLeftButtonDown += EditorChatDrag_PreviewMouseLeftButtonDownV061;
        _editorComposition.PreviewMouseMove += EditorChatDrag_PreviewMouseMoveV061;
        _editorComposition.PreviewMouseLeftButtonUp += EditorChatDrag_PreviewMouseLeftButtonUpV061;
        _editorComposition.LostMouseCapture += (_, _) => EndEditorChatDragV061();
    }

    private static void ConfigureEditorPositionSliderV061(Slider slider)
    {
        slider.Minimum = 0;
        slider.Maximum = 8192;
        slider.TickFrequency = 1;
        slider.SmallChange = 1;
        slider.LargeChange = 10;
        slider.IsSnapToTickEnabled = false;
    }

    private void MoveEditorPositionControlsToChatPanelV061()
    {
        if (!_editorToolPanels.TryGetValue("chat", out FrameworkElement? panel) ||
            panel is not ScrollViewer scroll ||
            scroll.Content is not StackPanel chatContent ||
            _editorChatXSlider?.Parent is not FrameworkElement xPanel ||
            _editorChatYSlider?.Parent is not FrameworkElement yPanel)
            return;

        DetachEditorPositionPanelV061(xPanel);
        DetachEditorPositionPanelV061(yPanel);

        int insertAt = _editorShowTimestampsCheck is null
            ? chatContent.Children.Count
            : chatContent.Children.IndexOf(_editorShowTimestampsCheck);
        if (insertAt < 0) insertAt = chatContent.Children.Count;

        var divider = new Border
        {
            Height = 1,
            Background = (Brush)FindResource("Border"),
            Margin = new Thickness(0, 8, 0, 12)
        };
        chatContent.Children.Insert(insertAt++, divider);

        chatContent.Children.Insert(insertAt++, new TextBlock
        {
            Text = "CHAT POSITION",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 7)
        });

        chatContent.Children.Insert(insertAt++, new TextBlock
        {
            Text = "Drag the rendered chat block directly in the preview, or use the sliders below for precise positioning.",
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        chatContent.Children.Insert(insertAt++, xPanel);
        chatContent.Children.Insert(insertAt, yPanel);
    }

    private static void DetachEditorPositionPanelV061(FrameworkElement element)
    {
        if (element.Parent is Panel parent)
            parent.Children.Remove(element);
        else if (element.Parent is ContentControl content && ReferenceEquals(content.Content, element))
            content.Content = null;
    }

    private bool CanDragEditorChatV061()
        => _editorChatBitmap is not null &&
           _editorChatXSlider is not null &&
           _editorChatYSlider is not null &&
           !string.Equals(_editorActiveToolKey, "markup", StringComparison.OrdinalIgnoreCase);

    private bool IsEditorChatPointV061(Point point)
    {
        if (_editorChatBitmap is null) return false;

        double x = Math.Max(0, _editorChatXSlider?.Value ?? 0);
        double y = Math.Max(0, _editorChatYSlider?.Value ?? 0);
        double width = Math.Max(1, _editorChatBitmap.PixelWidth);
        double height = Math.Max(1, _editorChatBitmap.PixelHeight);
        return new Rect(x, y, width, height).Contains(point);
    }

    private void EditorChatDrag_PreviewMouseLeftButtonDownV061(object sender, MouseButtonEventArgs e)
    {
        if (_editorComposition is null || !CanDragEditorChatV061()) return;

        Point point = e.GetPosition(_editorComposition);
        if (!IsEditorChatPointV061(point)) return;

        _editorChatDragActiveV061 = true;
        _editorChatDragStartV061 = point;
        _editorChatDragStartXV061 = _editorChatXSlider!.Value;
        _editorChatDragStartYV061 = _editorChatYSlider!.Value;

        _editorComposition.Cursor = Cursors.SizeAll;
        _editorComposition.CaptureMouse();
        e.Handled = true;
    }

    private void EditorChatDrag_PreviewMouseMoveV061(object sender, MouseEventArgs e)
    {
        if (_editorComposition is null) return;
        Point point = e.GetPosition(_editorComposition);

        if (!_editorChatDragActiveV061)
        {
            _editorComposition.Cursor = CanDragEditorChatV061() && IsEditorChatPointV061(point)
                ? Cursors.SizeAll
                : Cursors.Arrow;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndEditorChatDragV061();
            return;
        }

        if (_editorChatXSlider is null || _editorChatYSlider is null) return;

        double nextX = _editorChatDragStartXV061 + point.X - _editorChatDragStartV061.X;
        double nextY = _editorChatDragStartYV061 + point.Y - _editorChatDragStartV061.Y;

        _editorChatXSlider.Value = Math.Clamp(
            nextX,
            _editorChatXSlider.Minimum,
            _editorChatXSlider.Maximum);
        _editorChatYSlider.Value = Math.Clamp(
            nextY,
            _editorChatYSlider.Minimum,
            _editorChatYSlider.Maximum);
        e.Handled = true;
    }

    private void EditorChatDrag_PreviewMouseLeftButtonUpV061(object sender, MouseButtonEventArgs e)
    {
        if (!_editorChatDragActiveV061) return;
        EndEditorChatDragV061();
        e.Handled = true;
    }

    private void EndEditorChatDragV061()
    {
        if (!_editorChatDragActiveV061) return;
        _editorChatDragActiveV061 = false;

        if (_editorComposition is null) return;
        if (_editorComposition.IsMouseCaptured)
            _editorComposition.ReleaseMouseCapture();
        _editorComposition.Cursor = Cursors.Arrow;
    }
}
