using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Afterline;

public partial class MainWindow
{
    private const string EditorLayerDragFormatV069 = "Afterline.Editor.ImageLayer.V069";
    private Point _editorLayerListDragStartV069;
    private EditorImageLayerV067? _editorLayerListDragSourceV069;

    private Button CreateEditorCloseRailButtonV069()
    {
        var button = new Button
        {
            Content = "←",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 18,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Close Image Editor and return to Afterline"
        };
        button.Click += (_, _) => CloseEditorWorkspaceV069();
        return button;
    }

    private void CloseEditorWorkspaceV069()
    {
        DeactivateSelectionInteractionCanary();
        EndLayerPointerInteractionV068();
        if (_editorFullscreenWorkspaceCanary)
            ExitEditorFullscreenCanary();

        if (_editorPage is not null)
            _editorPage.Visibility = Visibility.Collapsed;
        ShowPage(DashboardPage, "Dashboard", "FiveM capture and session overview");
        ApplyAutomaticEditorSidebarV068(false);
        ApplyEditorChromeCanary(false);
    }

    private void FinalizeLoadedBaseImageV069()
    {
        if (_editorBaseOriginal is null || _editorBaseImage is null || _editorComposition is null)
            return;

        DeactivateSelectionInteractionCanary();
        ClearSelectionCanarySilently();

        if (!ReferenceEquals(_editorBaseImage.Parent, _editorComposition))
        {
            DetachEditorElement(_editorBaseImage);
            _editorComposition.Children.Insert(0, _editorBaseImage);
        }

        _editorBaseImage.Visibility = Visibility.Visible;
        _editorBaseImage.Opacity = 1;
        _editorBaseImage.IsHitTestVisible = false;
        if (_editorBaseImage.Source is null)
            _editorBaseImage.Source = _editorBaseOriginal;
        Panel.SetZIndex(_editorBaseImage, 0);

        foreach (EditorImageLayerV067 layer in _editorImageLayersV067)
            UpdateImageLayerVisualV067(layer);

        UpdateEditorCanvasSize();
        EnsureLayerCanvasExtentV067();
        UpdateEditorLayerZOrderV067();
        SyncCanaryGuideHostSize();
        RefreshEditorRulersV068();
        RefreshLayerListV067(_editorSelectedImageLayerV067);
        _editorComposition.UpdateLayout();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_editorBaseOriginal is null || _editorBaseImage is null)
                return;
            if (_editorBaseImage.Source is null)
                _editorBaseImage.Source = _editorBaseOriginal;
            UpdateEditorCanvasSize();
            EnsureLayerCanvasExtentV067();
            UpdateEditorLayerZOrderV067();
            SyncCanaryGuideHostSize();
            RefreshEditorRulersV068();
            RefreshLayerListV067(_editorSelectedImageLayerV067);
            if (_editorFitZoom)
                FitEditorPreviewToWindow();
        }), DispatcherPriority.Loaded);
    }

    private void ConfigureLayerListDragReorderV069()
    {
        if (_editorLayerListV067 is null)
            return;

        _editorLayerListV067.AllowDrop = true;
        _editorLayerListV067.ToolTip =
            "Select an image to edit it. Drag image rows to reorder their stacking position.";
        _editorLayerListV067.PreviewMouseLeftButtonDown += LayerListPointerDownV069;
        _editorLayerListV067.PreviewMouseMove += LayerListPointerMoveV069;
        _editorLayerListV067.PreviewDragOver += LayerListDragOverV069;
        _editorLayerListV067.Drop += LayerListDropV069;
    }

    private void RunEditorImageSmokeTestIfRequestedV069()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.FindIndex(args, value => string.Equals(
            value,
            "--afterline-smoke-image",
            StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return;

        string? path = args.Length > index + 1 ? args[index + 1] : null;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new FileNotFoundException("The Editor smoke-test image is unavailable.", path);

                if (_editorPage is null || _editorComposition is null || _editorBaseImage is null)
                    throw new InvalidOperationException("The Editor visual tree was not initialized.");

                ShowPage(_editorPage, "Editor", "Canary image-load smoke test");
                LoadEditorMediaV060(path);
                UpdateLayout();
                _editorComposition.UpdateLayout();

                if (_editorBaseOriginal is null || _editorBaseImage.Source is null ||
                    !ReferenceEquals(_editorBaseImage.Parent, _editorComposition))
                    throw new InvalidOperationException("The Base Image did not attach to the Editor composition.");

                int width = Math.Max(1, (int)Math.Ceiling(_editorComposition.ActualWidth));
                int height = Math.Max(1, (int)Math.Ceiling(_editorComposition.ActualHeight));
                var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(_editorComposition);
                byte[] pixel = new byte[4];
                rendered.CopyPixels(
                    new Int32Rect(Math.Max(0, width / 2), Math.Max(0, height / 2), 1, 1),
                    pixel,
                    4,
                    0);
                if (pixel[0] + pixel[1] + pixel[2] < 80 || pixel[3] == 0)
                    throw new InvalidOperationException("The loaded Base Image rendered as an empty or black canvas.");

                Afterline.Services.DiagnosticLogger.Info("Canary Editor image-load smoke test passed.");
                System.Windows.Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                Afterline.Services.DiagnosticLogger.Error("Canary Editor image-load smoke test failed.", ex);
                System.Windows.Application.Current.Shutdown(1);
            }
        }), DispatcherPriority.ApplicationIdle);
    }

    private void LayerListPointerDownV069(object sender, MouseButtonEventArgs e)
    {
        _editorLayerListDragStartV069 = e.GetPosition(_editorLayerListV067);
        ListBoxItem? item = ItemsControl.ContainerFromElement(
            _editorLayerListV067,
            e.OriginalSource as DependencyObject) as ListBoxItem;
        _editorLayerListDragSourceV069 = item?.Tag as EditorImageLayerV067;
    }

    private void LayerListPointerMoveV069(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _editorLayerListV067 is null ||
            _editorLayerListDragSourceV069 is null)
            return;

        Point current = e.GetPosition(_editorLayerListV067);
        if (Math.Abs(current.X - _editorLayerListDragStartV069.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _editorLayerListDragStartV069.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        EditorImageLayerV067 layer = _editorLayerListDragSourceV069;
        _editorLayerListDragSourceV069 = null;
        var data = new DataObject(EditorLayerDragFormatV069, layer.Id);
        DragDrop.DoDragDrop(_editorLayerListV067, data, DragDropEffects.Move);
    }

    private void LayerListDragOverV069(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(EditorLayerDragFormatV069)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void LayerListDropV069(object sender, DragEventArgs e)
    {
        if (_editorLayerListV067 is null ||
            e.Data.GetData(EditorLayerDragFormatV069) is not string layerId)
            return;

        EditorImageLayerV067? dragged = _editorImageLayersV067.FirstOrDefault(layer => layer.Id == layerId);
        if (dragged is null)
            return;

        ListBoxItem? targetItem = ItemsControl.ContainerFromElement(
            _editorLayerListV067,
            e.OriginalSource as DependencyObject) as ListBoxItem;
        EditorImageLayerV067? target = targetItem?.Tag as EditorImageLayerV067;

        List<EditorImageLayerV067> displayOrder = _editorImageLayersV067.AsEnumerable().Reverse().ToList();
        displayOrder.Remove(dragged);
        int targetIndex = target is null ? displayOrder.Count : displayOrder.IndexOf(target);
        if (targetIndex < 0)
            targetIndex = displayOrder.Count;
        else if (targetItem is not null && e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2)
            targetIndex++;

        displayOrder.Insert(Math.Clamp(targetIndex, 0, displayOrder.Count), dragged);
        _editorImageLayersV067.Clear();
        for (int i = displayOrder.Count - 1; i >= 0; i--)
            _editorImageLayersV067.Add(displayOrder[i]);

        _editorSelectedImageLayerV067 = dragged;
        UpdateEditorLayerZOrderV067();
        RefreshLayerListV067(dragged);
        RefreshSelectedLayerAdornerV068();
        SetEditorStatus($"Moved image layer ‘{dragged.Name}’ in the layer stack.");
        e.Handled = true;
    }
}
