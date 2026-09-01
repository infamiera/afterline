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
    private Button? _editorCloseRailButtonV072;
    private CancellationTokenSource? _editorCloseHighlightCtsV072;

    private Button CreateEditorCloseRailButtonV069()
    {
        var button = new Button
        {
            Content = "\uE72B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 18,
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 5),
            BorderThickness = new Thickness(1.5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Close Image Editor and return to Afterline"
        };
        ApplyEditorCloseRestingStyleV161(button);
        _editorCloseRailButtonV072 = button;
        button.Click += (_, _) => CloseEditorWorkspaceV069();
        return button;
    }

    private async void HighlightEditorCloseButtonV072()
    {
        if (_editorCloseRailButtonV072 is not Button button)
            return;

        _editorCloseHighlightCtsV072?.Cancel();
        _editorCloseHighlightCtsV072?.Dispose();
        ApplyEditorCloseRestingStyleV161(button);
        _editorCloseHighlightCtsV072 = new CancellationTokenSource();
        CancellationToken token = _editorCloseHighlightCtsV072.Token;

        button.BorderBrush = (Brush)FindResource("Accent");
        button.Background = (Brush)FindResource("Accent");
        button.Foreground = (Brush)FindResource("Bg");
        button.BorderThickness = new Thickness(2.5);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested && ReferenceEquals(button, _editorCloseRailButtonV072))
            ApplyEditorCloseRestingStyleV161(button);
    }

    private static void ApplyEditorCloseRestingStyleV161(Button button)
    {
        button.SetResourceReference(Control.ForegroundProperty, "Accent");
        button.SetResourceReference(Control.BorderBrushProperty, "Accent");
        button.SetResourceReference(Control.BackgroundProperty, "Raised");
        button.BorderThickness = new Thickness(1.5);
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
        _editorFitZoom = true;

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
        int projectIndex = Array.FindIndex(args, value => string.Equals(
            value,
            "--afterline-smoke-project",
            StringComparison.OrdinalIgnoreCase));
        string? projectPath = projectIndex >= 0 && args.Length > projectIndex + 1
            ? args[projectIndex + 1]
            : null;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                VerifyCaptureEventDoesNotWaitForUiV077();

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
                if (_editorGuideHostCanary is null ||
                    _editorGuideHostCanary.Width <= _editorComposition.Width ||
                    _editorGuideHostCanary.Height <= _editorComposition.Height ||
                    _editorComposition.Margin.Left <= 0 ||
                    _editorComposition.Margin.Top <= 0)
                {
                    throw new InvalidOperationException(
                        "The Editor pasteboard did not provide working room around the Base Image.");
                }
                if (!_compactEditorWorkspaceV078Initialized ||
                    _editorToolPanelColumn is null || _editorToolPanelColumn.Width.Value > 260.5 ||
                    _editorRightSidebarColumnV072 is null || _editorRightSidebarColumnV072.Width.Value > 276.5 ||
                    _editorLayerListV067 is null || _editorLayerListV067.MinHeight > 180.5)
                {
                    throw new InvalidOperationException(
                        "The compact Editor workspace density was not applied.");
                }

                if (!_editorFitZoom)
                    throw new InvalidOperationException("The first Base Image load did not automatically fit the preview.");
                if (_editorRulerGridV068 is null ||
                    _editorHorizontalRulerV068 is null ||
                    _editorVerticalRulerV068 is null ||
                    _editorPreviewScroll is null ||
                    _editorZoomHost is null ||
                    !ReferenceEquals(_editorPreviewScroll.Parent, _editorRulerGridV068) ||
                    !ReferenceEquals(_editorPreviewScroll.Content, _editorZoomHost) ||
                    _editorZoomHost.Child is null ||
                    ReferenceEquals(_editorZoomHost.Child, _editorRulerGridV068) ||
                    _editorPreviewScroll.Padding != new Thickness(0))
                {
                    throw new InvalidOperationException(
                        "Editor rulers were not attached to the fixed preview border without a canvas gap.");
                }
                if (_editorRulerGridV068.Parent is not Grid rulerParent)
                    throw new InvalidOperationException("The Editor ruler host was detached from the preview layout.");
                int rulerRow = Grid.GetRow(_editorRulerGridV068);
                if (rulerRow <= 0 ||
                    rulerParent.RowDefinitions.Count <= rulerRow ||
                    rulerParent.RowDefinitions[rulerRow - 1].ActualHeight > 0.5)
                {
                    throw new InvalidOperationException(
                        "The Editor preview retained a fixed spacer above its ruler and canvas.");
                }

                RefreshEditorRulersV068();
                Point horizontalOrigin = _editorComposition.TranslatePoint(new Point(0, 0), _editorHorizontalRulerV068);
                Point verticalOrigin = _editorComposition.TranslatePoint(new Point(0, 0), _editorVerticalRulerV068);
                TextBlock? horizontalZero = _editorHorizontalRulerV068.Children
                    .OfType<TextBlock>()
                    .FirstOrDefault(label => label.Text == "0");
                TextBlock? verticalZero = _editorVerticalRulerV068.Children
                    .OfType<TextBlock>()
                    .FirstOrDefault(label => label.Text == "0");
                if (!double.IsFinite(horizontalOrigin.X) || !double.IsFinite(verticalOrigin.Y) ||
                    horizontalZero is null || verticalZero is null ||
                    Math.Abs((Canvas.GetLeft(horizontalZero) - 2) - horizontalOrigin.X) > 1.5 ||
                    Math.Abs((Canvas.GetTop(verticalZero) - 2) - verticalOrigin.Y) > 1.5)
                {
                    throw new InvalidOperationException(
                        "Editor ruler zero points were not anchored to the Base Image top-left corner.");
                }

                ToggleEditorFullscreenCanary();
                if (!_editorFullscreenWorkspaceCanary)
                    throw new InvalidOperationException("The Editor did not enter full screen mode.");
                ToggleEditorFullscreenCanary();
                if (_editorFullscreenWorkspaceCanary)
                    throw new InvalidOperationException("The Editor did not leave full screen mode.");

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

                if (!string.IsNullOrWhiteSpace(projectPath))
                {
                    if (_editorLeftSidebarToggleV072 is null ||
                        _editorRightSidebarToggleV072 is null ||
                        _editorCloseRailButtonV072 is null)
                    {
                        throw new InvalidOperationException("Editor workspace collapse or close guidance controls were not initialized.");
                    }

                    ToggleEditorLeftSidebarV072();
                    if (_editorToolPanelHost?.Visibility != Visibility.Collapsed ||
                        _editorToolPanelColumn?.Width.Value != 0)
                        throw new InvalidOperationException("The left Editor panel did not collapse completely.");
                    ToggleEditorLeftSidebarV072();
                    if (_editorToolPanelHost?.Visibility != Visibility.Visible ||
                        _editorToolPanelColumn?.Width.Value <= 0)
                        throw new InvalidOperationException("The left Editor panel did not reopen.");

                    ToggleEditorRightSidebarV072();
                    if (_editorRightSidebarV067?.Visibility != Visibility.Collapsed ||
                        _editorRightSidebarColumnV072?.Width.Value != 0)
                        throw new InvalidOperationException("The right Editor panel did not collapse completely.");
                    ToggleEditorRightSidebarV072();
                    if (_editorRightSidebarV067?.Visibility != Visibility.Visible ||
                        _editorRightSidebarColumnV072?.Width.Value <= 0)
                        throw new InvalidOperationException("The right Editor panel did not reopen.");

                    if (_editorToolPanels.ContainsKey("colors") ||
                        !_editorToolPanels.ContainsKey("chat") ||
                        _editorChatColorsExpanderV071 is null)
                    {
                        throw new InvalidOperationException(
                            "Line Colors was not merged into the Chat & Font panel.");
                    }
                    if (_editorInput is null)
                        throw new InvalidOperationException("The Chat & Font input was not initialized.");
                    _editorInput.Text = "Select this text";
                    _editorChatColorsExpanderV071.IsExpanded = false;
                    _editorInput.Select(0, 6);
                    if (!_editorChatColorsExpanderV071.IsExpanded || _editorLineColorPresetBox is null)
                    {
                        throw new InvalidOperationException(
                            "Selecting Editor text did not reveal its color controls.");
                    }
                    if (_archiveFilterModeV071 is null ||
                        !_archiveFilterModeV071.Items.OfType<ComboBoxItem>().Any(item =>
                            string.Equals(item.Content?.ToString(), "Last # days", StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException("The Archive last-days label was not updated.");
                    }
                    if (_logReaderJumpTopButton is null || _logReaderJumpBottomButton is null)
                        throw new InvalidOperationException("The Log Reader jump controls were not initialized.");

                    string[] requiredFonts =
                    {
                        "Arial, Helvetica, sans-serif",
                        "Georgia, serif",
                        "\"Palatino Linotype\", \"Book Antiqua\", Palatino, serif",
                        "\"Roboto\", sans-serif",
                        "\"Open Sans\", sans-serif",
                        "\"Inter\", sans-serif"
                    };
                    if (_editorFontBox is null || requiredFonts.Any(required =>
                            !_editorFontBox.Items.Cast<object>().Any(item =>
                                string.Equals(item?.ToString(), required, StringComparison.Ordinal))))
                    {
                        throw new InvalidOperationException("The expanded Editor font catalog was not initialized.");
                    }

                    var (resized, _, _) = CalculateLayerResizeBoundsV071(
                        new Rect(20, 20, 40, 40),
                        EditorLayerResizeHandleV071.NorthWest,
                        -20,
                        -20,
                        64,
                        64,
                        snap: true,
                        snapThreshold: 10);
                    if (resized.X != 0 || resized.Y != 0 || resized.Width != 60 || resized.Height != 60)
                        throw new InvalidOperationException("Eight-direction image-layer resize geometry failed its edge/corner check.");

                    var (offCanvasResize, _, _) = CalculateLayerResizeBoundsV071(
                        new Rect(20, 20, 40, 40),
                        EditorLayerResizeHandleV071.NorthWest,
                        -40,
                        -35,
                        64,
                        64,
                        snap: false,
                        snapThreshold: 10);
                    if (offCanvasResize.X != -20 || offCanvasResize.Y != -15 ||
                        offCanvasResize.Width != 80 || offCanvasResize.Height != 75)
                    {
                        throw new InvalidOperationException(
                            "Image-layer transform geometry did not preserve a north/west off-canvas resize.");
                    }

                    if (_editorBaseOriginal is null)
                        throw new InvalidOperationException("The Editor smoke-test Base Image was lost.");
                    int layerCountBeforeDrop = _editorImageLayersV067.Count;
                    ImportDroppedEditorImagesV078(
                        new[] { path },
                        new Point(_editorComposition.Width / 2, _editorComposition.Height / 2));
                    if (_editorImageLayersV067.Count != layerCountBeforeDrop + 1)
                        throw new InvalidOperationException(
                            "Dropping an image onto an existing Base Image did not create a new layer.");
                    EditorImageLayerV067 filteredLayer = _editorImageLayersV067[^1];
                    double baseCanvasWidth = _editorComposition.Width;
                    double baseCanvasHeight = _editorComposition.Height;
                    filteredLayer.X = -18;
                    filteredLayer.Y = -12;
                    UpdateImageLayerVisualV067(filteredLayer);
                    EnsureLayerCanvasExtentV067();
                    if (_editorComposition.Width != baseCanvasWidth ||
                        _editorComposition.Height != baseCanvasHeight ||
                        filteredLayer.Image.RenderTransform is not TranslateTransform layerTransform ||
                        layerTransform.X != -18 || layerTransform.Y != -12)
                    {
                        throw new InvalidOperationException(
                            "An off-canvas image layer changed the Base Image export boundary or lost its coordinates.");
                    }
                    if (_editorFilterBrightnessCanary is not null)
                        _editorFilterBrightnessCanary.Value = 25;
                    _editorFilterTimerCanary?.Stop();
                    ApplyCanaryFilterPreview();
                    CommitCanaryFilterPreview();
                    byte[] filteredPixel = new byte[4];
                    filteredLayer.Bitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), filteredPixel, 4, 0);
                    if (filteredPixel[1] == 0)
                        throw new InvalidOperationException("Filters & Adjustments did not commit to the selected image layer.");

                    const string selectedText = "[17:38:23] Bianca Yurei says [low]: /quietly amused/.";
                    int selectedStart = selectedText.IndexOf("Bianca Yurei", StringComparison.Ordinal);
                    if (_editorInput is not null) _editorInput.Text = selectedText;
                    _editorTextColorOverridesV071.Add(new EditorTextColorOverride(
                        0,
                        selectedStart,
                        "Bianca Yurei".Length,
                        "Bianca Yurei",
                        EditorChatFormatter.Red));
                    RenderEditorChatOverlay();
                    if (_editorLineColorList is null || _editorLineColorList.Items.Count == 0)
                        throw new InvalidOperationException("The merged Line Colors list was not populated.");
                    _editorLineColorList.SelectedIndex = -1;
                    _editorInput.Select(0, Math.Min(6, _editorInput.Text.Length));
                    _editorLineColorList.SelectedIndex = 0;
                    if (_editorInput.SelectionLength != 0)
                    {
                        throw new InvalidOperationException(
                            "Selecting a whole line retained a stale text-range color target.");
                    }

                    SaveEditorProjectToPathV067(projectPath);
                    if (!File.Exists(projectPath) || new FileInfo(projectPath).Length == 0)
                        throw new InvalidOperationException("The Editor project was not written.");

                    ResetEditorProjectV067();
                    LoadEditorProjectFromPathV067(projectPath);
                    UpdateLayout();
                    _editorComposition.UpdateLayout();
                    if (_editorBaseOriginal is null || _editorBaseImage.Source is null)
                        throw new InvalidOperationException("The saved Editor project did not restore its Base Image.");
                    if (_editorImageLayersV067.Count != 1 ||
                        _editorImageLayersV067[0].X != -18 ||
                        _editorImageLayersV067[0].Y != -12 ||
                        !_editorTextColorOverridesV071.Any(value =>
                            value.Text == "Bianca Yurei" && value.Color == EditorChatFormatter.Red))
                    {
                        throw new InvalidOperationException(
                            "The saved Editor project did not restore its filtered image layer and selected-text color.");
                    }
                }

                Afterline.Services.DiagnosticLogger.Info(
                    string.IsNullOrWhiteSpace(projectPath)
                        ? "Canary Editor image-load smoke test passed."
                        : "Canary Editor image-load and project round-trip smoke test passed.");
                System.Windows.Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                Afterline.Services.DiagnosticLogger.Error("Canary Editor image-load smoke test failed.", ex);
                System.Windows.Application.Current.Shutdown(1);
            }
        }), DispatcherPriority.ApplicationIdle);
    }

    private void VerifyCaptureEventDoesNotWaitForUiV077()
    {
        bool previousShowLiveChat = _settings.ShowLiveChat;
        _settings.ShowLiveChat = true;
        try
        {
            var smokeEntry = new Afterline.Models.ChatEntry(
                DateTime.Now,
                "[00:00:00] capture UI isolation smoke test");
            Task delivery = Task.Run(() => Capture_MessageCaptured(this, smokeEntry));
            if (!delivery.Wait(TimeSpan.FromSeconds(2)))
            {
                throw new InvalidOperationException(
                    "Capture event delivery waited for the WPF dispatcher and could stall later journal writes.");
            }

            delivery.GetAwaiter().GetResult();
        }
        finally
        {
            _settings.ShowLiveChat = previousShowLiveChat;
            while (_pendingLiveMessages.TryDequeue(out _))
            {
            }
        }
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
