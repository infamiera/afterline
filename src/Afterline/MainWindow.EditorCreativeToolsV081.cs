using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private const int DefaultEditorCanvasWidthV081 = 1920;
    private const int DefaultEditorCanvasHeightV081 = 1080;
    private const int EditorDocumentHistoryLimitV081 = 12;

    private sealed record EditorDocumentLayerSnapshotV081(
        string Id,
        string Name,
        BitmapSource Bitmap,
        double X,
        double Y,
        double Width,
        double Height,
        double Opacity,
        double CornerRadius,
        bool Visible,
        bool Locked,
        bool IsCollageFrame,
        string? CollagePresetId,
        int CollageSlotIndex,
        BitmapSource? CollageSource,
        double CollageOffsetX,
        double CollageOffsetY);

    private sealed record EditorDocumentSnapshotV081(
        BitmapSource? BaseImage,
        bool SyntheticBase,
        string Background,
        IReadOnlyList<EditorDocumentLayerSnapshotV081> Layers,
        string? SelectedLayerId,
        string Description,
        long Sequence);

    private readonly Stack<EditorDocumentSnapshotV081> _editorDocumentUndoV081 = new();
    private readonly Stack<EditorDocumentSnapshotV081> _editorDocumentRedoV081 = new();
    private bool _editorDocumentHistoryRestoringV081;
    private bool _editorSyntheticBaseV081;
    private bool _editorBackgroundUiUpdatingV081;
    private string _editorActiveProjectBackgroundV081 = "Transparent";
    private long _editorHistorySequenceV081;
    private bool _editorLayerSliderHistoryCapturedV081;
    private ComboBox? _editorSettingsBackgroundBoxV081;

    private void ConfigureEditorCreativeToolsV081(Grid editorBody)
    {
        EnsureDefaultEditorBaseV081();
        ConfigureCollageMakerV081(editorBody);
    }

    private void EnsureDefaultEditorBaseV081()
    {
        if (_editorBaseOriginal is not null || !string.IsNullOrWhiteSpace(_editorProjectPathV067))
            return;
        CreateDefaultProjectBaseV081();
    }

    private void CreateDefaultProjectBaseV081()
    {
        string background = ResolveProjectBackgroundV081();
        _editorActiveProjectBackgroundV081 = background;
        BitmapSource bitmap = CreateProjectBackgroundBitmapV081(
            DefaultEditorCanvasWidthV081,
            DefaultEditorCanvasHeightV081,
            background);
        _editorSyntheticBaseV081 = true;
        _editorBaseOriginal = bitmap;
        _editorFilterCommittedCanary = CloneBitmapCanary(bitmap);
        _editorFilterPreviewCanary = null;
        _editorFilterTrackedMediaCanary = null;
        if (_editorBaseImage is not null)
        {
            _editorBaseImage.Source = bitmap;
            _editorBaseImage.Effect = null;
        }
        if (_editorRemoveImageButton is not null)
            _editorRemoveImageButton.IsEnabled = false;
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
        FinalizeLoadedBaseImageV069();
    }

    private void EditorProjectBackgroundChangedV081()
    {
        if (_editorBackgroundUiUpdatingV081)
            return;
        UpdateEditorCanvasSize();
        SyncEditorSettingsBackgroundV081();
        if (!_editorSyntheticBaseV081)
            return;

        PushEditorDocumentHistoryV081("project background change");
        _editorActiveProjectBackgroundV081 = ResolveProjectBackgroundV081();

        int width = _editorBaseOriginal?.PixelWidth ?? DefaultEditorCanvasWidthV081;
        int height = _editorBaseOriginal?.PixelHeight ?? DefaultEditorCanvasHeightV081;
        _editorBaseOriginal = CreateProjectBackgroundBitmapV081(
            width,
            height,
            _editorActiveProjectBackgroundV081);
        _editorFilterCommittedCanary = CloneBitmapCanary(_editorBaseOriginal);
        _editorFilterPreviewCanary = null;
        ApplyEditorImageAdjustments();
        RefreshLayerListV067();
        SetEditorStatus($"New-project background set to {_editorActiveProjectBackgroundV081.ToLowerInvariant()}. Undo restores the previous background.");
    }

    private string ResolveProjectBackgroundV081()
        => (_editorBackgroundBox?.SelectedItem?.ToString() ?? _settings.Editor.CanvasBackground ?? "Transparent") switch
        {
            "Black" => "Black",
            "White" => "White",
            _ => "Transparent"
        };

    private static BitmapSource CreateProjectBackgroundBitmapV081(int width, int height, string background)
    {
        width = Math.Clamp(width, 1, 12000);
        height = Math.Clamp(height, 1, 12000);
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        if (!string.Equals(background, "Transparent", StringComparison.OrdinalIgnoreCase))
        {
            byte value = string.Equals(background, "White", StringComparison.OrdinalIgnoreCase) ? (byte)255 : (byte)0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = value;
                pixels[i + 1] = value;
                pixels[i + 2] = value;
                pixels[i + 3] = 255;
            }
        }
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private void OpenBackgroundRemovalV081()
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        BitmapSource? source = layer?.CollageSource ?? layer?.Bitmap ?? _editorFilterCommittedCanary ?? _editorBaseOriginal;
        if (source is null)
        {
            SetEditorStatus("Select an image layer or load a Base Image before removing a background.");
            return;
        }
        if (EditorHasAnimatedGifV060 && layer is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Background removal currently supports still Base Images and added image layers. Convert or load a still frame first.",
                "Animated Base Image",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var window = new EditorBackgroundRemovalWindow(this, source, allowSolidFill: layer is null);
        if (window.ShowDialog() != true || window.Result is not BitmapSource result)
            return;

        if (layer is not null)
        {
            PushLayerEditHistoryV068(layer, "background removal");
            if (layer.IsCollageFrame)
            {
                layer.CollageSource = result;
                RefreshCollageFrameBitmapV081(layer);
            }
            else
            {
                layer.Bitmap = result;
            }
            UpdateImageLayerVisualV067(layer);
            RefreshLayerListV067(layer);
            RefreshSelectedLayerAdornerV068();
            SetEditorStatus($"Removed the edge-connected background from ‘{layer.Name}’. Undo restores the original pixels.");
            return;
        }

        PushEditorDocumentHistoryV081("Base Image background removal");
        _editorSyntheticBaseV081 = false;
        _editorBaseOriginal = result;
        _editorFilterCommittedCanary = CloneBitmapCanary(result);
        _editorFilterPreviewCanary = null;
        SetBackgroundSelectionV081(window.SelectedFill switch
        {
            EditorBackgroundRemovalFill.Black => "Black",
            EditorBackgroundRemovalFill.White => "White",
            _ => "Transparent"
        });
        ApplyEditorImageAdjustments();
        FinalizeLoadedBaseImageV069();
        SetEditorStatus("Background removal applied to the Base Image. Undo restores the original pixels.");
    }

    private void SetSelectedLayerAsBaseV081(EditorImageLayerV067 layer)
    {
        if (layer.IsLocked)
        {
            SetEditorStatus("Unlock this image layer before setting it as the Base Image.");
            return;
        }

        var window = new EditorBaseSizeWindowV081(this, layer.Width, layer.Height);
        if (window.ShowDialog() != true)
            return;

        PushEditorDocumentHistoryV081("set image layer as Base Image");
        BitmapSource oldBase = CloneBitmapCanary(_editorFilterCommittedCanary ?? _editorBaseOriginal
            ?? CreateProjectBackgroundBitmapV081(DefaultEditorCanvasWidthV081, DefaultEditorCanvasHeightV081, "Transparent"));
        bool preserveOldBase = !_editorSyntheticBaseV081;
        BitmapSource promoted = RenderLayerBitmapAtSizeV081(layer, window.ImageWidth, window.ImageHeight);

        _editorComposition?.Children.Remove(layer.Image);
        _editorGuideHostCanary?.Children.Remove(layer.PasteboardImage);
        RemoveCollagePlaceholderV081(layer);
        _editorImageLayersV067.Remove(layer);
        _editorSelectedImageLayerV067 = null;

        if (preserveOldBase)
        {
            AddImageLayerFromBitmapV067(
                oldBase,
                "Previous Base Image",
                0,
                0,
                width: oldBase.PixelWidth,
                height: oldBase.PixelHeight,
                refresh: false);
        }

        _editorSyntheticBaseV081 = false;
        _editorBaseOriginal = promoted;
        _editorFilterCommittedCanary = CloneBitmapCanary(promoted);
        _editorFilterPreviewCanary = null;
        _editorFilterTrackedMediaCanary = null;
        ClearSelectionCanarySilently();
        ResetCanaryFilterControls();
        ApplyEditorImageAdjustments();
        UpdateEditorCanvasSize();
        EnsureLayerCanvasExtentV067();
        UpdateEditorLayerZOrderV067();
        RefreshLayerListV067();
        RefreshSelectedLayerAdornerV068();
        FinalizeLoadedBaseImageV069();
        SetEditorStatus($"Set ‘{layer.Name}’ as the {window.ImageWidth:N0} × {window.ImageHeight:N0}px Base Image. Undo restores the previous document.");
    }

    private static BitmapSource RenderLayerBitmapAtSizeV081(EditorImageLayerV067 layer, int width, int height)
    {
        width = Math.Clamp(width, 1, 12000);
        height = Math.Clamp(height, 1, 12000);
        var visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
        {
            double scaleX = width / Math.Max(1, layer.Width);
            double scaleY = height / Math.Max(1, layer.Height);
            double radius = Math.Clamp(layer.CornerRadius * Math.Min(scaleX, scaleY), 0, Math.Min(width, height) / 2.0);
            context.PushOpacity(Math.Clamp(layer.Opacity, 0, 1));
            if (radius > 0)
                context.PushClip(new RectangleGeometry(new Rect(0, 0, width, height), radius, radius));
            context.DrawImage(layer.Bitmap, new Rect(0, 0, width, height));
            if (radius > 0)
                context.Pop();
            context.Pop();
        }
        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();
        return rendered;
    }

    private void SetBackgroundSelectionV081(string value)
    {
        if (_editorBackgroundBox is null) return;
        _editorBackgroundUiUpdatingV081 = true;
        try
        {
            _editorBackgroundBox.SelectedItem = _editorBackgroundBox.Items
                .Cast<object>()
                .FirstOrDefault(item => string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                ?? _editorBackgroundBox.Items[0];
        }
        finally
        {
            _editorBackgroundUiUpdatingV081 = false;
        }
        UpdateEditorCanvasSize();
        _editorActiveProjectBackgroundV081 = value;
        SyncEditorSettingsBackgroundV081();
    }

    private void PushEditorDocumentHistoryV081(string description)
    {
        if (_editorDocumentHistoryRestoringV081) return;
        _editorDocumentUndoV081.Push(CaptureEditorDocumentV081(description, NextEditorHistorySequenceV081()));
        TrimEditorDocumentHistoryV081(_editorDocumentUndoV081);
        ClearAllEditorRedoV081();
    }

    private EditorDocumentSnapshotV081 CaptureEditorDocumentV081(string description, long sequence)
    {
        BitmapSource? baseImage = _editorFilterCommittedCanary ?? _editorBaseOriginal;
        var layers = _editorImageLayersV067.Select(layer => new EditorDocumentLayerSnapshotV081(
            layer.Id,
            layer.Name,
            CloneBitmapCanary(layer.Bitmap),
            layer.X,
            layer.Y,
            layer.Width,
            layer.Height,
            layer.Opacity,
            layer.CornerRadius,
            layer.IsVisible,
            layer.IsLocked,
            layer.IsCollageFrame,
            layer.CollagePresetId,
            layer.CollageSlotIndex,
            layer.CollageSource is null ? null : CloneBitmapCanary(layer.CollageSource),
            layer.CollageOffsetX,
            layer.CollageOffsetY)).ToArray();
        return new EditorDocumentSnapshotV081(
            baseImage is null ? null : CloneBitmapCanary(baseImage),
            _editorSyntheticBaseV081,
            _editorActiveProjectBackgroundV081,
            layers,
            _editorSelectedImageLayerV067?.Id,
            description,
            sequence);
    }

    private void UndoEditorDocumentV081()
    {
        if (_editorDocumentUndoV081.Count == 0) return;
        EditorDocumentSnapshotV081 previous = _editorDocumentUndoV081.Pop();
        _editorDocumentRedoV081.Push(CaptureEditorDocumentV081("redo document change", previous.Sequence));
        RestoreEditorDocumentV081(previous);
        SetEditorStatus($"Undid {previous.Description}.");
    }

    private void RedoEditorDocumentV081()
    {
        if (_editorDocumentRedoV081.Count == 0) return;
        EditorDocumentSnapshotV081 next = _editorDocumentRedoV081.Pop();
        _editorDocumentUndoV081.Push(CaptureEditorDocumentV081("undo document change", next.Sequence));
        RestoreEditorDocumentV081(next);
        SetEditorStatus("Redid the last document change.");
    }

    private void RestoreEditorDocumentV081(EditorDocumentSnapshotV081 snapshot)
    {
        _editorDocumentHistoryRestoringV081 = true;
        try
        {
            ClearImageLayersV067(clearHistory: false);
            _editorSyntheticBaseV081 = snapshot.SyntheticBase;
            _editorBaseOriginal = snapshot.BaseImage is null ? null : CloneBitmapCanary(snapshot.BaseImage);
            _editorFilterCommittedCanary = _editorBaseOriginal is null ? null : CloneBitmapCanary(_editorBaseOriginal);
            _editorFilterPreviewCanary = null;
            _editorFilterTrackedMediaCanary = null;
            SetBackgroundSelectionV081(snapshot.Background);

            EditorImageLayerV067? selected = null;
            foreach (EditorDocumentLayerSnapshotV081 layer in snapshot.Layers)
            {
                EditorImageLayerV067 restored = AddImageLayerFromBitmapV067(
                    CloneBitmapCanary(layer.Bitmap),
                    layer.Name,
                    layer.X,
                    layer.Y,
                    opacity: layer.Opacity,
                    visible: layer.Visible,
                    locked: layer.Locked,
                    width: layer.Width,
                    height: layer.Height,
                    refresh: false,
                    cornerRadius: layer.CornerRadius,
                    id: layer.Id,
                    isCollageFrame: layer.IsCollageFrame,
                    collagePresetId: layer.CollagePresetId,
                    collageSlotIndex: layer.CollageSlotIndex,
                    collageSource: layer.CollageSource is null ? null : CloneBitmapCanary(layer.CollageSource),
                    collageOffsetX: layer.CollageOffsetX,
                    collageOffsetY: layer.CollageOffsetY);
                if (restored.IsCollageFrame)
                    RefreshCollageFrameBitmapV081(restored);
                if (string.Equals(restored.Id, snapshot.SelectedLayerId, StringComparison.Ordinal))
                    selected = restored;
            }

            ApplyEditorImageAdjustments();
            UpdateEditorCanvasSize();
            EnsureLayerCanvasExtentV067();
            UpdateEditorLayerZOrderV067();
            RefreshLayerListV067(selected);
            if (selected is not null) SelectImageLayerV068(selected);
            RefreshSelectedLayerAdornerV068();
            SyncCanaryGuideHostSize();
        }
        finally
        {
            _editorDocumentHistoryRestoringV081 = false;
        }
    }

    private static void TrimEditorDocumentHistoryV081(Stack<EditorDocumentSnapshotV081> stack)
    {
        if (stack.Count <= EditorDocumentHistoryLimitV081) return;
        EditorDocumentSnapshotV081[] keep = stack.Take(EditorDocumentHistoryLimitV081).Reverse().ToArray();
        stack.Clear();
        foreach (EditorDocumentSnapshotV081 snapshot in keep) stack.Push(snapshot);
    }

    private long NextEditorHistorySequenceV081() => ++_editorHistorySequenceV081;

    private void ClearAllEditorRedoV081()
    {
        _editorDocumentRedoV081.Clear();
        _editorLayerRedoV068.Clear();
        _editorRedoCanaryV2.Clear();
    }

    private void ClearEditorDocumentHistoryV081()
    {
        _editorDocumentUndoV081.Clear();
        _editorDocumentRedoV081.Clear();
    }

    private void ConfigureReversibleLayerSliderV081(Slider slider, string description)
    {
        slider.PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (_editorLayerSliderHistoryCapturedV081 || _editorLayerUiUpdatingV067 || _editorSelectedImageLayerV067 is not EditorImageLayerV067 layer)
                return;
            PushLayerEditHistoryV068(layer, description);
            _editorLayerSliderHistoryCapturedV081 = true;
        };
        slider.PreviewMouseLeftButtonUp += (_, _) => _editorLayerSliderHistoryCapturedV081 = false;
        slider.LostMouseCapture += (_, _) => _editorLayerSliderHistoryCapturedV081 = false;
        slider.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End) ||
                _editorLayerUiUpdatingV067 ||
                _editorSelectedImageLayerV067 is not EditorImageLayerV067 layer)
                return;
            PushLayerEditHistoryV068(layer, description);
        };
    }

    private FrameworkElement BuildEditorNewProjectBackgroundSettingsV081()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "NEW PROJECT BACKGROUND",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        _editorSettingsBackgroundBoxV081 = new ComboBox { Height = 34 };
        _editorSettingsBackgroundBoxV081.Items.Add("Transparent");
        _editorSettingsBackgroundBoxV081.Items.Add("Black");
        _editorSettingsBackgroundBoxV081.Items.Add("White");
        _editorSettingsBackgroundBoxV081.SelectedItem = ResolveProjectBackgroundV081();
        _editorSettingsBackgroundBoxV081.SelectionChanged += (_, _) =>
        {
            if (_editorBackgroundUiUpdatingV081) return;
            string selected = _editorSettingsBackgroundBoxV081.SelectedItem?.ToString() ?? "Transparent";
            if (_editorBackgroundBox is not null)
                _editorBackgroundBox.SelectedItem = selected;
            else
                _settings.Editor.CanvasBackground = selected;
        };
        stack.Children.Add(_editorSettingsBackgroundBoxV081);
        stack.Children.Add(EditorSubtleNote(
            "Transparent is the default and preserves PNG alpha. Black and white create an opaque Base Image for each new project."));
        return stack;
    }

    private void SyncEditorSettingsBackgroundV081()
    {
        if (_editorSettingsBackgroundBoxV081 is null) return;
        _editorBackgroundUiUpdatingV081 = true;
        try
        {
            _editorSettingsBackgroundBoxV081.SelectedItem = ResolveProjectBackgroundV081();
        }
        finally
        {
            _editorBackgroundUiUpdatingV081 = false;
        }
    }

    private void VerifyEditorCreativeToolPrimitivesV081()
    {
        if (_editorFontSizeSlider is null || _editorFontSizeSlider.Maximum < 100 ||
            _editorChatWidthSlider is null || _editorChatWidthSlider.Maximum < 1500)
        {
            throw new InvalidOperationException("The expanded Editor font-size or chat-width limits were not initialized.");
        }
        if (_editorComposition?.Background is not SolidColorBrush compositionBackground ||
            compositionBackground.Color.A != 0)
        {
            throw new InvalidOperationException("The Editor composition would flatten transparent PNG exports.");
        }

        const int size = 5;
        byte[] pixels = new byte[size * size * 4];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
            pixels[index + 1] = 255;
            pixels[index + 2] = 255;
            pixels[index + 3] = 255;
        }
        int center = (2 * size + 2) * 4;
        pixels[center] = 0;
        pixels[center + 1] = 0;
        pixels[center + 2] = 255;
        BitmapSource sample = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        sample.Freeze();
        BitmapSource transparent = EditorBackgroundRemovalProcessor.Remove(
            sample, 30, 0, EditorBackgroundRemovalFill.Transparent, CancellationToken.None);
        byte[] corner = new byte[4];
        byte[] subject = new byte[4];
        transparent.CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
        transparent.CopyPixels(new Int32Rect(2, 2, 1, 1), subject, 4, 0);
        if (corner[3] != 0 || subject[3] != 255 || subject[2] < 200)
            throw new InvalidOperationException("Edge-connected background removal did not preserve the interior subject.");
        BitmapSource solid = EditorBackgroundRemovalProcessor.Remove(
            sample, 30, 0, EditorBackgroundRemovalFill.Black, CancellationToken.None);
        solid.CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
        if (corner[3] != 255)
            throw new InvalidOperationException("Solid background removal output retained transparent pixels.");

        BitmapSource blank = CreateProjectBackgroundBitmapV081(3, 3, "Transparent");
        blank.CopyPixels(new Int32Rect(1, 1, 1, 1), corner, 4, 0);
        if (corner[3] != 0)
            throw new InvalidOperationException("The new-project transparent Base Image was not truly transparent.");
    }
}
