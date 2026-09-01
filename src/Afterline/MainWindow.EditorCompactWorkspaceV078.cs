using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _compactEditorWorkspaceV078Initialized;
    private bool _editorWorkspacePanningV078;
    private Point _editorPanStartV078;
    private double _editorPanHorizontalStartV078;
    private double _editorPanVerticalStartV078;

    private void EnsureCompactEditorWorkspaceV078()
    {
        if (_compactEditorWorkspaceV078Initialized ||
            _editorToolPanelHost?.Parent is not Grid editorBody ||
            _editorPreviewScroll is null)
            return;
        _compactEditorWorkspaceV078Initialized = true;
        _editorToolPanelWidthCanary = 260;

        // Keep the center workspace dominant at common laptop resolutions. These
        // values alter density only; every existing panel and action remains.
        if (editorBody.ColumnDefinitions.Count >= 5)
        {
            editorBody.ColumnDefinitions[0].Width = new GridLength(46);
            editorBody.ColumnDefinitions[1].Width = new GridLength(5);
            editorBody.ColumnDefinitions[2].MinWidth = 210;
            editorBody.ColumnDefinitions[2].MaxWidth = 340;
            editorBody.ColumnDefinitions[2].Width = new GridLength(260);
            editorBody.ColumnDefinitions[3].Width = new GridLength(6);
        }
        if (editorBody.ColumnDefinitions.Count >= 7)
        {
            editorBody.ColumnDefinitions[5].Width = new GridLength(6);
            editorBody.ColumnDefinitions[6].MinWidth = 230;
            editorBody.ColumnDefinitions[6].MaxWidth = 340;
            editorBody.ColumnDefinitions[6].Width = new GridLength(276);
            _editorRightSidebarWidthV072 = editorBody.ColumnDefinitions[6].Width;
            _editorRightSidebarMinWidthV072 = editorBody.ColumnDefinitions[6].MinWidth;
        }

        _editorToolPanelHost.Padding = new Thickness(8, 7, 8, 8);
        if (_editorToolPanelHost.Child is Grid toolHost)
        {
            if (toolHost.RowDefinitions.Count > 1)
                toolHost.RowDefinitions[1].Height = new GridLength(6);
            if (_editorToolPanelTitle is not null)
                _editorToolPanelTitle.FontSize = 14;
        }

        Border? rail = editorBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        if (rail is not null)
            rail.Padding = new Thickness(4, 5, 4, 5);
        foreach (Button button in _editorToolButtons.Values)
        {
            button.Width = 33;
            button.Height = 33;
            button.Margin = new Thickness(0, 0, 0, 4);
        }

        Border? preview = editorBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 4);
        if (preview is not null)
            preview.Padding = new Thickness(6);
        if (_editorRightSidebarV067 is not null)
        {
            _editorRightSidebarV067.Padding = new Thickness(6);
            _editorRightSidebarV067.MinWidth = 230;
        }
        if (_editorLayerListV067 is not null)
        {
            _editorLayerListV067.MinHeight = 180;
            _editorLayerListV067.MaxHeight = 420;
            _editorLayerListV067.ToolTip =
                "Select a layer to edit it · drag rows to reorder · drag images from Explorer to add them.";
        }

        _editorPreviewScroll.ToolTip =
            "Drop images here · Space-drag or middle-drag to pan · Ctrl+wheel to zoom at the pointer.";
        _editorPreviewScroll.PreviewMouseDown += EditorWorkspacePanDownV078;
        _editorPreviewScroll.PreviewMouseMove += EditorWorkspacePanMoveV078;
        _editorPreviewScroll.PreviewMouseUp += EditorWorkspacePanUpV078;
        _editorPreviewScroll.LostMouseCapture += (_, _) => EndEditorWorkspacePanV078();
    }

    private void EditorWorkspacePanDownV078(object sender, MouseButtonEventArgs e)
    {
        if (_editorPreviewScroll is null)
            return;
        bool middlePan = e.ChangedButton == MouseButton.Middle;
        bool spacePan = e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space);
        if (!middlePan && !spacePan)
            return;

        _editorWorkspacePanningV078 = true;
        _editorPanStartV078 = e.GetPosition(_editorPreviewScroll);
        _editorPanHorizontalStartV078 = _editorPreviewScroll.HorizontalOffset;
        _editorPanVerticalStartV078 = _editorPreviewScroll.VerticalOffset;
        _editorPreviewScroll.Cursor = Cursors.Hand;
        _editorPreviewScroll.CaptureMouse();
        e.Handled = true;
    }

    private void EditorWorkspacePanMoveV078(object sender, MouseEventArgs e)
    {
        if (!_editorWorkspacePanningV078 || _editorPreviewScroll is null)
            return;
        if (e.MiddleButton != MouseButtonState.Pressed &&
            !(e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)))
        {
            EndEditorWorkspacePanV078();
            return;
        }

        Point current = e.GetPosition(_editorPreviewScroll);
        _editorPreviewScroll.ScrollToHorizontalOffset(
            Math.Max(0, _editorPanHorizontalStartV078 - (current.X - _editorPanStartV078.X)));
        _editorPreviewScroll.ScrollToVerticalOffset(
            Math.Max(0, _editorPanVerticalStartV078 - (current.Y - _editorPanStartV078.Y)));
        RefreshEditorRulersV068();
        e.Handled = true;
    }

    private void EditorWorkspacePanUpV078(object sender, MouseButtonEventArgs e)
    {
        if (!_editorWorkspacePanningV078)
            return;
        EndEditorWorkspacePanV078();
        e.Handled = true;
    }

    private void EndEditorWorkspacePanV078()
    {
        if (!_editorWorkspacePanningV078)
            return;
        _editorWorkspacePanningV078 = false;
        if (_editorPreviewScroll?.IsMouseCaptured == true)
            _editorPreviewScroll.ReleaseMouseCapture();
        if (_editorPreviewScroll is not null)
            _editorPreviewScroll.Cursor = Cursors.Arrow;
        RefreshEditorRulersV068();
    }
}
