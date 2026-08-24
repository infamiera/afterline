using Microsoft.Win32;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Afterline;

public partial class MainWindow
{
    private sealed record EditorProjectFilterV067(
        string Preset,
        double Strength,
        double Brightness,
        double Contrast,
        double Saturation,
        double Temperature,
        double Fade,
        double Blur,
        double Pixelate);

    private sealed record EditorProjectImageLayerDataV067(
        string Name,
        string Entry,
        double X,
        double Y,
        double Scale,
        double Opacity,
        bool Visible,
        bool Locked = false,
        double? Width = null,
        double? Height = null);

    private sealed record EditorProjectChatLayerDataV067(
        string Text,
        double X,
        double Y);

    private sealed record EditorProjectManifestV067(
        int FormatVersion,
        string? ChatText,
        double ChatX,
        double ChatY,
        double CropX,
        double CropY,
        double CropWidth,
        double CropHeight,
        string OutputWidth,
        string OutputHeight,
        string? BaseImageEntry,
        EditorProjectFilterV067 Filter,
        string? SelectionEntry,
        int SelectionWidth,
        int SelectionHeight,
        IReadOnlyList<EditorProjectImageLayerDataV067> ImageLayers,
        IReadOnlyList<EditorProjectChatLayerDataV067> ExtraChats);

    private enum NewProjectChoiceV067
    {
        Save,
        Discard,
        Cancel
    }

    private bool HasEditorProjectContentV067()
        => _editorBaseOriginal is not null ||
           !string.IsNullOrWhiteSpace(_editorInput?.Text) ||
           _editorImageLayersV067.Count > 0 ||
           _editorExtraChatsCanary.Count > 0 ||
           _editorSelectionMaskCanary is not null;

    private void NewEditorProjectV067()
    {
        if (HasEditorProjectContentV067())
        {
            var warning = new NewProjectWarningWindowV067(this);
            if (warning.ShowDialog() != true || warning.Choice == NewProjectChoiceV067.Cancel)
                return;

            if (warning.Choice == NewProjectChoiceV067.Save && !SaveCurrentEditorProjectV067())
                return;
        }

        ResetEditorProjectV067();
        SetEditorStatus("New Editor project created.");
    }

    private bool SaveCurrentEditorProjectV067()
    {
        string? path = _editorProjectPathV067;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Afterline Editor Project",
                Filter = "Afterline Editor Project (*.afterlineproj)|*.afterlineproj",
                DefaultExt = ".afterlineproj",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = "Afterline Project.afterlineproj"
            };
            if (dialog.ShowDialog(this) != true)
                return false;
            path = dialog.FileName;
        }

        try
        {
            SaveEditorProjectToPathV067(path);
            _editorProjectPathV067 = path;
            UpdateProjectLabelV067();
            SetEditorStatus($"Saved project · {Path.GetFileName(path)}.");
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save Afterline Editor project.", ex);
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Unable to save project",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private void LoadEditorProjectV067()
    {
        if (HasEditorProjectContentV067())
        {
            var warning = new NewProjectWarningWindowV067(this, "Load another project?");
            if (warning.ShowDialog() != true || warning.Choice == NewProjectChoiceV067.Cancel)
                return;

            if (warning.Choice == NewProjectChoiceV067.Save && !SaveCurrentEditorProjectV067())
                return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Load Afterline Editor Project",
            Filter = "Afterline Editor Project (*.afterlineproj)|*.afterlineproj|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            LoadEditorProjectFromPathV067(dialog.FileName);
            _editorProjectPathV067 = dialog.FileName;
            UpdateProjectLabelV067();
            SetEditorStatus($"Loaded project · {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load Afterline Editor project.", ex);
            System.Windows.MessageBox.Show(
                this,
                "Afterline could not load this project.\n\n" + ex.Message,
                "Unable to load project",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SaveEditorProjectToPathV067(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporary = path + ".writing";
        if (File.Exists(temporary))
            File.Delete(temporary);

        BitmapSource? baseImage = _editorFilterCommittedCanary ?? _editorBaseOriginal;
        string? baseEntry = baseImage is null ? null : "media/base.png";
        string? selectionEntry = _editorSelectionMaskCanary is null ? null : "selection.bin";

        var manifest = new EditorProjectManifestV067(
            1,
            _editorInput?.Text,
            _editorChatXSlider?.Value ?? 0,
            _editorChatYSlider?.Value ?? 0,
            _editorCropNormalizedV060.X,
            _editorCropNormalizedV060.Y,
            _editorCropNormalizedV060.Width,
            _editorCropNormalizedV060.Height,
            _editorOutputWidthBox?.Text ?? string.Empty,
            _editorOutputHeightBox?.Text ?? string.Empty,
            baseEntry,
            new EditorProjectFilterV067(
                _editorFilterPresetCanary?.SelectedItem?.ToString() ?? "None",
                _editorFilterStrengthCanary?.Value ?? 100,
                _editorFilterBrightnessCanary?.Value ?? 0,
                _editorFilterContrastCanary?.Value ?? 0,
                _editorFilterSaturationCanary?.Value ?? 0,
                _editorFilterTemperatureCanary?.Value ?? 0,
                _editorFilterFadeCanary?.Value ?? 0,
                _editorFilterBlurCanary?.Value ?? 0,
                _editorPixelateSliderCanaryV2?.Value ?? 0),
            selectionEntry,
            _editorSelectionWidthCanary,
            _editorSelectionHeightCanary,
            _editorImageLayersV067.Select((layer, index) =>
                new EditorProjectImageLayerDataV067(
                    layer.Name,
                    $"layers/{index:D3}.png",
                    layer.X,
                    layer.Y,
                    layer.Bitmap.PixelWidth <= 0 ? 1 : layer.Width / layer.Bitmap.PixelWidth,
                    layer.Opacity,
                    layer.IsVisible,
                    layer.IsLocked,
                    layer.Width,
                    layer.Height)).ToArray(),
            _editorExtraChatsCanary.Select(layer =>
                new EditorProjectChatLayerDataV067(layer.Text, layer.X, layer.Y)).ToArray());

        try
        {
            using (FileStream stream = File.Create(temporary))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                ZipArchiveEntry manifestEntry = archive.CreateEntry("project.json", CompressionLevel.Optimal);
                using (Stream manifestStream = manifestEntry.Open())
                {
                    JsonSerializer.Serialize(
                        manifestStream,
                        manifest,
                        new JsonSerializerOptions { WriteIndented = true });
                }

                if (baseImage is not null && baseEntry is not null)
                    WriteBitmapToProjectV067(archive, baseEntry, baseImage);

                for (int i = 0; i < _editorImageLayersV067.Count; i++)
                    WriteBitmapToProjectV067(archive, $"layers/{i:D3}.png", _editorImageLayersV067[i].Bitmap);

                if (_editorSelectionMaskCanary is not null &&
                    selectionEntry is not null &&
                    _editorSelectionWidthCanary > 0 &&
                    _editorSelectionHeightCanary > 0)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(selectionEntry, CompressionLevel.Optimal);
                    using Stream selection = entry.Open();
                    byte[] packed = PackSelectionMaskV067(_editorSelectionMaskCanary);
                    selection.Write(packed, 0, packed.Length);
                }
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
            throw;
        }
    }

    private void LoadEditorProjectFromPathV067(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        ZipArchiveEntry manifestEntry = archive.GetEntry("project.json")
            ?? throw new InvalidDataException("This file does not contain an Afterline project manifest.");

        EditorProjectManifestV067 manifest;
        using (Stream manifestStream = manifestEntry.Open())
        {
            manifest = JsonSerializer.Deserialize<EditorProjectManifestV067>(manifestStream)
                ?? throw new InvalidDataException("The project manifest could not be read.");
        }

        if (manifest.FormatVersion != 1)
            throw new InvalidDataException($"Unsupported project format version {manifest.FormatVersion}.");

        ResetEditorProjectV067();

        BitmapSource? baseImage = null;
        if (!string.IsNullOrWhiteSpace(manifest.BaseImageEntry))
        {
            ZipArchiveEntry baseEntry = archive.GetEntry(manifest.BaseImageEntry)
                ?? throw new InvalidDataException("The project base image is missing.");
            baseImage = ReadBitmapFromProjectV067(baseEntry);
        }

        if (baseImage is not null)
        {
            _editorBaseOriginal = baseImage;
            _editorFilterCommittedCanary = CloneBitmapCanary(baseImage);
            _editorFilterPreviewCanary = null;
            _editorFilterTrackedMediaCanary = null;
        }

        foreach (EditorProjectImageLayerDataV067 layerData in manifest.ImageLayers ?? Array.Empty<EditorProjectImageLayerDataV067>())
        {
            ZipArchiveEntry layerEntry = archive.GetEntry(layerData.Entry)
                ?? throw new InvalidDataException($"Image layer ‘{layerData.Name}’ is missing.");
            BitmapSource bitmap = ReadBitmapFromProjectV067(layerEntry);
            AddImageLayerFromBitmapV067(
                bitmap,
                layerData.Name,
                layerData.X,
                layerData.Y,
                layerData.Scale,
                layerData.Opacity,
                layerData.Visible,
                layerData.Locked,
                layerData.Width,
                layerData.Height,
                refresh: false);
        }

        if (_editorInput is not null)
            _editorInput.Text = manifest.ChatText ?? string.Empty;
        if (_editorChatXSlider is not null)
            _editorChatXSlider.Value = Math.Max(_editorChatXSlider.Minimum, Math.Min(_editorChatXSlider.Maximum, manifest.ChatX));
        if (_editorChatYSlider is not null)
            _editorChatYSlider.Value = Math.Max(_editorChatYSlider.Minimum, Math.Min(_editorChatYSlider.Maximum, manifest.ChatY));

        ClearExtraChatLayersV067();
        foreach (EditorProjectChatLayerDataV067 chat in manifest.ExtraChats ?? Array.Empty<EditorProjectChatLayerDataV067>())
        {
            AddExtraChatLayerCanary();
            CanaryChatLayer layer = _editorExtraChatsCanary[^1];
            layer.Text = chat.Text ?? string.Empty;
            layer.X = Math.Max(0, chat.X);
            layer.Y = Math.Max(0, chat.Y);
        }
        if (_editorMultipleChatsCheckCanary is not null)
            _editorMultipleChatsCheckCanary.IsChecked = _editorExtraChatsCanary.Count > 0;

        _editorCropNormalizedV060 = new Rect(
            Math.Clamp(manifest.CropX, 0, 1),
            Math.Clamp(manifest.CropY, 0, 1),
            Math.Clamp(manifest.CropWidth, 0.001, 1),
            Math.Clamp(manifest.CropHeight, 0.001, 1));
        if (_editorOutputWidthBox is not null && !string.IsNullOrWhiteSpace(manifest.OutputWidth))
            _editorOutputWidthBox.Text = manifest.OutputWidth;
        if (_editorOutputHeightBox is not null && !string.IsNullOrWhiteSpace(manifest.OutputHeight))
            _editorOutputHeightBox.Text = manifest.OutputHeight;

        RestoreProjectFilterControlsV067(manifest.Filter);

        if (!string.IsNullOrWhiteSpace(manifest.SelectionEntry) &&
            manifest.SelectionWidth > 0 &&
            manifest.SelectionHeight > 0)
        {
            ZipArchiveEntry? selectionEntry = archive.GetEntry(manifest.SelectionEntry);
            if (selectionEntry is not null)
            {
                using Stream selectionStream = selectionEntry.Open();
                using var memory = new MemoryStream();
                selectionStream.CopyTo(memory);
                int count = checked(manifest.SelectionWidth * manifest.SelectionHeight);
                _editorSelectionMaskCanary = UnpackSelectionMaskV067(memory.ToArray(), count);
                _editorSelectionWidthCanary = manifest.SelectionWidth;
                _editorSelectionHeightCanary = manifest.SelectionHeight;
                RenderSelectionBoundaryCanary();
                RefreshSelectionHighlightV067();
            }
        }

        if (_editorBaseOriginal is not null)
        {
            ApplyCanaryFilterPreview();
            ApplyPixelationAfterFilterPreviewCanaryV2();
        }
        else
        {
            ApplyEditorImageAdjustments();
        }

        RenderEditorChatOverlay();
        RenderExtraChatLayersCanary();
        UpdateEditorCanvasSize();
        EnsureLayerCanvasExtentV067();
        UpdateEditorLayerZOrderV067();
        RefreshLayerListV067();
        RefreshFilterPresetGalleryV067();

        if (_editorFitZoom)
            Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private void RestoreProjectFilterControlsV067(EditorProjectFilterV067? filter)
    {
        filter ??= new EditorProjectFilterV067("None", 100, 0, 0, 0, 0, 0, 0, 0);

        _editorFilterUiUpdatingCanary = true;
        try
        {
            if (_editorFilterPresetCanary is not null)
            {
                string preset = string.IsNullOrWhiteSpace(filter.Preset) ? "None" : filter.Preset;
                _editorFilterPresetCanary.SelectedItem = _editorFilterPresetCanary.Items
                    .Cast<object>()
                    .FirstOrDefault(item => string.Equals(item?.ToString(), preset, StringComparison.OrdinalIgnoreCase))
                    ?? _editorFilterPresetCanary.Items[0];
            }
            if (_editorFilterStrengthCanary is not null) _editorFilterStrengthCanary.Value = filter.Strength;
            if (_editorFilterBrightnessCanary is not null) _editorFilterBrightnessCanary.Value = filter.Brightness;
            if (_editorFilterContrastCanary is not null) _editorFilterContrastCanary.Value = filter.Contrast;
            if (_editorFilterSaturationCanary is not null) _editorFilterSaturationCanary.Value = filter.Saturation;
            if (_editorFilterTemperatureCanary is not null) _editorFilterTemperatureCanary.Value = filter.Temperature;
            if (_editorFilterFadeCanary is not null) _editorFilterFadeCanary.Value = filter.Fade;
            if (_editorFilterBlurCanary is not null) _editorFilterBlurCanary.Value = filter.Blur;
            if (_editorPixelateSliderCanaryV2 is not null) _editorPixelateSliderCanaryV2.Value = filter.Pixelate;
            if (_editorSavedFilterBoxCanaryV2 is not null) _editorSavedFilterBoxCanaryV2.SelectedIndex = 0;
        }
        finally
        {
            _editorFilterUiUpdatingCanary = false;
        }
    }

    private void ResetEditorProjectV067()
    {
        StopEditorGifPreviewV060();
        _editorGifFrames = null;
        _editorGifDelays = null;
        _editorGifLoopCount = 0;
        _editorGifCompletedLoops = 0;
        _editorGifFrameIndex = 0;
        _editorLoadedMediaPath = null;

        _editorFilterTimerCanary?.Stop();
        _editorFilterCommittedCanary = null;
        _editorFilterPreviewCanary = null;
        _editorFilterTrackedMediaCanary = null;
        _editorBaseOriginal = null;
        if (_editorBaseImage is not null)
        {
            _editorBaseImage.Source = null;
            _editorBaseImage.Effect = null;
        }

        ClearImageLayersV067();
        ClearExtraChatLayersV067();
        ClearSelectionCanarySilently();
        RefreshSelectionHighlightV067();

        if (_editorInput is not null)
            _editorInput.Text = string.Empty;
        if (_editorChatXSlider is not null)
            _editorChatXSlider.Value = 0;
        if (_editorChatYSlider is not null)
            _editorChatYSlider.Value = 0;

        _editorCropNormalizedV060 = new Rect(0, 0, 1, 1);
        ResetCanaryFilterControls();
        if (_editorPixelateSliderCanaryV2 is not null)
            _editorPixelateSliderCanaryV2.Value = 0;

        _editorUndoCanaryV2.Clear();
        _editorRedoCanaryV2.Clear();
        _editorProjectPathV067 = null;
        UpdateProjectLabelV067();

        RenderEditorChatOverlay();
        RenderExtraChatLayersCanary();
        UpdateEditorCanvasSize();
        RefreshLayerListV067();
        RefreshFilterPresetGalleryV067();
    }

    private void ClearExtraChatLayersV067()
    {
        if (_editorComposition is not null)
            foreach (CanaryChatLayer layer in _editorExtraChatsCanary)
                _editorComposition.Children.Remove(layer.Image);

        _editorExtraChatsCanary.Clear();
        _editorSelectedExtraChatCanary = null;
        _editorNextChatNumberCanary = 2;
        if (_editorExtraChatSelectorCanary is not null)
            _editorExtraChatSelectorCanary.Items.Clear();
        if (_editorExtraChatInputCanary is not null)
            _editorExtraChatInputCanary.Text = string.Empty;
        if (_editorMultipleChatsCheckCanary is not null)
            _editorMultipleChatsCheckCanary.IsChecked = false;
        RefreshMultipleChatUiCanary();
    }

    private static void WriteBitmapToProjectV067(ZipArchive archive, string entryName, BitmapSource bitmap)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static BitmapSource ReadBitmapFromProjectV067(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        var decoder = new PngBitmapDecoder(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        if (frame.CanFreeze) frame.Freeze();
        return frame;
    }

    private static byte[] PackSelectionMaskV067(bool[] mask)
    {
        byte[] packed = new byte[(mask.Length + 7) / 8];
        for (int i = 0; i < mask.Length; i++)
            if (mask[i])
                packed[i >> 3] |= (byte)(1 << (i & 7));
        return packed;
    }

    private static bool[] UnpackSelectionMaskV067(byte[] packed, int count)
    {
        var mask = new bool[count];
        for (int i = 0; i < count; i++)
            mask[i] = (packed[i >> 3] & (1 << (i & 7))) != 0;
        return mask;
    }

    private sealed class NewProjectWarningWindowV067 : Window
    {
        public NewProjectChoiceV067 Choice { get; private set; } = NewProjectChoiceV067.Cancel;

        public NewProjectWarningWindowV067(MainWindow owner, string title = "Create a new project?")
        {
            Owner = owner;
            Title = title;
            Width = 470;
            Height = 225;
            MinWidth = 430;
            MinHeight = 210;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = (Brush)owner.FindResource("Bg");
            Foreground = (Brush)owner.FindResource("Text");

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold
            });

            var copy = new TextBlock
            {
                Text = "The current Editor project contains work. Starting or loading another project will remove it from the workspace. Save it first, discard it, or cancel and keep working.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)owner.FindResource("MutedText"),
                Margin = new Thickness(0, 10, 0, 14),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(copy, 1);
            root.Children.Add(copy);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button save = new()
            {
                Content = "Save Current Project",
                Padding = new Thickness(12, 7, 12, 7),
                MinWidth = 145,
                Margin = new Thickness(0, 0, 8, 0)
            };
            save.Click += (_, _) =>
            {
                Choice = NewProjectChoiceV067.Save;
                DialogResult = true;
            };

            Button discard = new()
            {
                Content = "Discard",
                Padding = new Thickness(12, 7, 12, 7),
                MinWidth = 90,
                Margin = new Thickness(0, 0, 8, 0)
            };
            discard.Click += (_, _) =>
            {
                Choice = NewProjectChoiceV067.Discard;
                DialogResult = true;
            };

            Button cancel = new()
            {
                Content = "Cancel",
                Padding = new Thickness(12, 7, 12, 7),
                MinWidth = 90
            };
            cancel.Click += (_, _) =>
            {
                Choice = NewProjectChoiceV067.Cancel;
                DialogResult = false;
            };

            buttons.Children.Add(save);
            buttons.Children.Add(discard);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
        }
    }
}
