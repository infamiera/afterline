using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed record CanaryEditorImageSnapshot(
        BitmapSource? Image,
        bool[]? Selection,
        int SelectionWidth,
        int SelectionHeight,
        double ChatX,
        double ChatY,
        Rect Crop,
        string OutputWidth,
        string OutputHeight,
        string Description);

    private sealed record CanarySavedFilter(
        string Name,
        double Strength,
        double Brightness,
        double Contrast,
        double Saturation,
        double Temperature,
        double Fade,
        double Blur,
        double Pixelate);

    private bool _canaryEditorRefinementV2Initialized;
    private Slider? _editorPixelateSliderCanaryV2;
    private ComboBox? _editorSavedFilterBoxCanaryV2;
    private readonly Stack<CanaryEditorImageSnapshot> _editorUndoCanaryV2 = new();
    private readonly Stack<CanaryEditorImageSnapshot> _editorRedoCanaryV2 = new();
    private bool _editorHistoryRestoringCanaryV2;
    private bool _editorHistoryDragCapturedCanaryV2;
    private const int CanaryEditorHistoryLimitV2 = 30;
    private static string CanaryFilterPresetsFileV2 => Path.Combine(AppPaths.LocalDataRoot, "Editor", "filter-presets.json");

    private void EnsureCanaryEditorRefinementV2()
    {
        if (_canaryEditorRefinementV2Initialized || _editorPage is null)
            return;

        _canaryEditorRefinementV2Initialized = true;
        RemovePaintAndMarkupCanaryV2();
        RebuildEditorToolRailCanaryV2();
        BuildEditorTaskBarCanaryV2();
        ConfigureFilterEnhancementsCanaryV2();
        ConfigureEditorHistoryCanaryV2();
        ConfigureFullscreenSpacingCanaryV2();
        ConfigureObjectSelectionRefinedCanaryV2();
    }

    private void RemovePaintAndMarkupCanaryV2()
    {
        if (_editorToolButtons.TryGetValue("markup", out Button? markupButton) && markupButton.Parent is Panel parent)
            parent.Children.Remove(markupButton);
        _editorToolButtons.Remove("markup");
        _editorToolPanels.Remove("markup");

        if (_editorInkCanvas is not null)
        {
            _editorInkCanvas.Strokes.Clear();
            _editorInkCanvas.Visibility = Visibility.Collapsed;
            _editorInkCanvas.IsHitTestVisible = false;
        }

        if (string.Equals(_editorActiveToolKey, "markup", StringComparison.OrdinalIgnoreCase))
            ShowEditorToolPanel("chat", forceOpen: true);
    }

    private void RebuildEditorToolRailCanaryV2()
    {
        if (_editorToolPanelHost?.Parent is not Grid editorBody)
            return;

        Border? rail = editorBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        if (rail is null) return;

        _editorToolButtons.Clear();

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var tools = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };

        tools.Children.Add(CreateCanaryRailButtonV2("T", "Chat & Font", "chat", "Segoe UI", 18));
        tools.Children.Add(CreateCanaryRailButtonV2("fx", "Text Effects", "effects", "Segoe UI", 13));
        tools.Children.Add(CreateCanaryRailButtonV2("▧", "Image & Canvas", "image", "Segoe UI Symbol", 19));
        tools.Children.Add(CreateCanaryRailButtonV2("⬚", "Selection Tools", "selection", "Segoe UI Symbol", 17));
        tools.Children.Add(CreateCanaryRailButtonV2("◐", "Filters & Adjustments", "filters", "Segoe UI Symbol", 18));
        layout.Children.Add(tools);

        var bottom = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        bottom.Children.Add(CreateEditorCloseRailButtonV069());
        bottom.Children.Add(CreateCanaryRailButtonV2("⚙", "Editor Settings", "settings", "Segoe UI Symbol", 16));
        Grid.SetRow(bottom, 1);
        layout.Children.Add(bottom);

        rail.Padding = new Thickness(6, 7, 6, 7);
        rail.Child = layout;
    }

    private Button CreateCanaryRailButtonV2(string symbol, string tooltip, string key, string font, double fontSize)
    {
        var button = new Button
        {
            Content = symbol,
            FontFamily = new FontFamily(font),
            FontSize = fontSize,
            FontWeight = symbol is "T" or "fx" ? FontWeights.SemiBold : FontWeights.Normal,
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = tooltip
        };
        button.Click += (_, _) =>
        {
            if (!string.Equals(key, "selection", StringComparison.OrdinalIgnoreCase))
                DeactivateSelectionInteractionCanary();
            ShowEditorToolPanel(key, forceOpen: false);
            RestoreResizableToolPanelWidthCanary();
            if (_editorToolPanelTitle is not null)
            {
                _editorToolPanelTitle.Text = key switch
                {
                    "chat" => "Chat & Font",
                    "colors" => "Line Colors",
                    "effects" => "Text Effects",
                    "image" => "Image & Canvas",
                    "layer-paint" => "Layer Paint & Erase",
                    "selection" => "Selection",
                    "filters" => "Filters & Adjustments",
                    "settings" => "Editor Settings",
                    _ => "Editor"
                };
            }
            if (string.Equals(key, "selection", StringComparison.OrdinalIgnoreCase) &&
                _editorToolPanelHost?.Visibility == Visibility.Visible)
            {
                ActivateSelectionToolCanary(_editorSelectionToolCanary == CanarySelectionTool.None
                    ? CanarySelectionTool.Rectangular
                    : _editorSelectionToolCanary);
            }
        };
        _editorToolButtons[key] = button;
        return button;
    }

    private void BuildEditorTaskBarCanaryV2()
    {
        if (_editorPage is null) return;
        Border? header = _editorPage.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (header is null) return;

        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var menus = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        menus.Children.Add(CreateEditorMenuButtonCanaryV2("File",
            ("Open Image / GIF…", () => EditorLoadMediaV060_Click(this, new RoutedEventArgs())),
            ("Import Chat Text…", () => EditorImportText_Click(this, new RoutedEventArgs())),
            ("Copy Current Frame", () => EditorCopyImageV060_Click(this, new RoutedEventArgs())),
            ("Export PNG…", () => EditorExportPngV060_Click(this, new RoutedEventArgs())),
            ("Export GIF…", () => EditorExportGifV060_Click(this, new RoutedEventArgs())),
            ("Remove Media", () => EditorRemoveMediaV060_Click(this, new RoutedEventArgs()))));

        menus.Children.Add(CreateEditorMenuButtonCanaryV2("Image",
            ("Image & Canvas", () => ShowEditorToolPanel("image", true)),
            ("Adjust Crop…", () => EditorAdjustCrop_ClickV060(this, new RoutedEventArgs())),
            ("Reset Crop", () => EditorResetCrop_ClickV060(this, new RoutedEventArgs())),
            ("Rotate Left", () => RunEditorTransformWithHistoryCanaryV2("rotate-left")),
            ("Rotate Right", () => RunEditorTransformWithHistoryCanaryV2("rotate-right")),
            ("Flip Horizontal", () => RunEditorTransformWithHistoryCanaryV2("flip-h")),
            ("Flip Vertical", () => RunEditorTransformWithHistoryCanaryV2("flip-v"))));

        menus.Children.Add(CreateEditorMenuButtonCanaryV2("Filter",
            ("Filters & Adjustments", () => ShowEditorToolPanel("filters", true)),
            ("Apply Changes", ApplyFilterWithHistoryCanaryV2),
            ("Revert Preview", RevertCanaryFilterPreview),
            ("Save Current Filter…", SaveCurrentFilterPresetCanaryV2)));

        menus.Children.Add(CreateEditorMenuButtonCanaryV2("View",
            ("Fit Canvas", FitEditorPreviewToWindow),
            ("Zoom 100%", () => { _editorFitZoom = false; SetEditorZoom(1.0); }),
            ("Chat & Font", () => ShowEditorToolPanel("chat", true)),
            ("Selection Tools", () => ShowEditorToolPanel("selection", true)),
            ("Full Screen Editor", ToggleEditorFullscreenCanary)));

        menus.Children.Add(CreateEditorMenuButtonCanaryV2("Help",
            ("Editor Shortcuts", () => new CanaryEditorShortcutsWindow(this).ShowDialog()),
            ("About Afterline", () => new AboutWindow(this).ShowDialog())));

        bar.Children.Add(menus);

        var label = new TextBlock
        {
            Text = "CANARY EDITOR",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0)
        };
        Grid.SetColumn(label, 1);
        bar.Children.Add(label);

        header.Padding = new Thickness(6, 4, 6, 4);
        header.Child = bar;
        if (_editorPage.RowDefinitions.Count > 1)
            _editorPage.RowDefinitions[1].Height = new GridLength(5);
    }

    private Button CreateEditorMenuButtonCanaryV2(string title, params (string Label, Action Action)[] items)
    {
        var button = new Button
        {
            Content = title,
            Height = 29,
            MinWidth = 46,
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("Text"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = $"{title} menu"
        };

        button.Click += (_, _) =>
        {
            var menu = CreateEditorContextMenuCanaryV2(items);
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        };
        return button;
    }

    private ContextMenu CreateEditorContextMenuCanaryV2(IEnumerable<(string Label, Action Action)> items)
    {
        var menu = new ContextMenu
        {
            Background = (Brush)FindResource("Raised"),
            Foreground = (Brush)FindResource("Text"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            MinWidth = 190
        };

        foreach ((string label, Action action) in items)
        {
            var item = new MenuItem
            {
                Header = label,
                Foreground = (Brush)FindResource("Text"),
                Background = (Brush)FindResource("Raised"),
                Padding = new Thickness(9, 6, 14, 6)
            };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        return menu;
    }

    private void ConfigureFullscreenSpacingCanaryV2()
    {
        if (_editorFullscreenCloseCanary is not null)
        {
            _editorFullscreenCloseCanary.Margin = new Thickness(0, 6, 48, 0);
            _editorFullscreenCloseCanary.ToolTip = "Exit Full Screen Editor (Esc)";
        }
    }

    private void ConfigureFilterEnhancementsCanaryV2()
    {
        if (!_editorToolPanels.TryGetValue("filters", out FrameworkElement? panel) ||
            panel is not ScrollViewer scroll ||
            scroll.Content is not StackPanel content)
            return;

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock
        {
            Text = "PIXELATED BLUR",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 7)
        });
        content.Children.Add(EditorHelpText("Block-based pixelation. When a selection is active, only pixels inside that selection are pixelated."));

        var pixelate = CreateEditorV041Slider("Pixel size", 0, 64, 0, 1);
        _editorPixelateSliderCanaryV2 = pixelate.Slider;
        _editorPixelateSliderCanaryV2.ToolTip = "0 disables pixelation. Larger values create larger mosaic blocks.";
        _editorPixelateSliderCanaryV2.ValueChanged += (_, _) => ScheduleCanaryFilterPreview();
        content.Children.Add(pixelate.Panel);

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock
        {
            Text = "SAVED FILTER SETTINGS",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 7)
        });

        _editorSavedFilterBoxCanaryV2 = new ComboBox { Height = 34 };
        _editorSavedFilterBoxCanaryV2.SelectionChanged += (_, _) => ApplySelectedSavedFilterCanaryV2();
        content.Children.Add(CreateEditorField("Saved filter", _editorSavedFilterBoxCanaryV2));

        var savedActions = new WrapPanel();
        savedActions.Children.Add(CreateSmallEditorButton("Save Current", (_, _) => SaveCurrentFilterPresetCanaryV2()));
        savedActions.Children.Add(CreateSmallEditorButton("Delete", (_, _) => DeleteSelectedFilterPresetCanaryV2()));
        content.Children.Add(savedActions);
        RefreshSavedFilterPresetsCanaryV2();

        if (_editorFilterTimerCanary is not null)
            _editorFilterTimerCanary.Tick += (_, _) => ApplyPixelationAfterFilterPreviewCanaryV2();

        foreach (Button button in FindVisualChildrenCanary<Button>(panel))
        {
            string text = button.Content?.ToString() ?? string.Empty;
            if (text is "Apply Changes" or "Revert Preview" or "Rotate Left" or "Rotate Right" or "Flip H" or "Flip V")
                button.Click += (_, _) => ResetPixelationAfterCommittedActionCanaryV2();
        }
    }

    private void ApplyPixelationAfterFilterPreviewCanaryV2()
    {
        int block = (int)Math.Round(_editorPixelateSliderCanaryV2?.Value ?? 0);
        if (block <= 1 || _editorFilterPreviewCanary is null || _editorBaseOriginal is null)
            return;

        try
        {
            BitmapSource pixelated = PixelateBitmapCanaryV2(_editorFilterPreviewCanary, block);
            _editorFilterPreviewCanary = pixelated;
            _editorBaseOriginal = pixelated;
            ApplyEditorImageAdjustments();
            UpdateEditorCanvasSize();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to preview Canary pixelation.", ex);
            SetEditorStatus("Pixelated blur preview could not be rendered.");
        }
    }

    private BitmapSource PixelateBitmapCanaryV2(BitmapSource source, int blockSize)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);
        byte[] output = (byte[])pixels.Clone();
        int block = Math.Clamp(blockSize, 2, 96);
        bool useSelection = _editorSelectionMaskCanary is not null &&
                            _editorSelectionWidthCanary == width &&
                            _editorSelectionHeightCanary == height;

        for (int by = 0; by < height; by += block)
        {
            for (int bx = 0; bx < width; bx += block)
            {
                long b = 0, g = 0, r = 0, a = 0;
                int count = 0;
                int maxY = Math.Min(height, by + block);
                int maxX = Math.Min(width, bx + block);

                for (int y = by; y < maxY; y++)
                {
                    for (int x = bx; x < maxX; x++)
                    {
                        int pixel = y * width + x;
                        if (useSelection && !_editorSelectionMaskCanary![pixel]) continue;
                        int i = y * stride + x * 4;
                        b += pixels[i];
                        g += pixels[i + 1];
                        r += pixels[i + 2];
                        a += pixels[i + 3];
                        count++;
                    }
                }

                if (count == 0) continue;
                byte bb = (byte)(b / count);
                byte gg = (byte)(g / count);
                byte rr = (byte)(r / count);
                byte aa = (byte)(a / count);

                for (int y = by; y < maxY; y++)
                {
                    for (int x = bx; x < maxX; x++)
                    {
                        int pixel = y * width + x;
                        if (useSelection && !_editorSelectionMaskCanary![pixel]) continue;
                        int i = y * stride + x * 4;
                        output[i] = bb;
                        output[i + 1] = gg;
                        output[i + 2] = rr;
                        output[i + 3] = aa;
                    }
                }
            }
        }

        var result = BitmapSource.Create(
            width, height,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32, null, output, stride);
        result.Freeze();
        return result;
    }

    private void ResetPixelationAfterCommittedActionCanaryV2()
    {
        if (_editorPixelateSliderCanaryV2 is not null)
            _editorPixelateSliderCanaryV2.Value = 0;
    }

    private void RefreshSavedFilterPresetsCanaryV2(string? select = null)
    {
        if (_editorSavedFilterBoxCanaryV2 is null) return;
        _editorSavedFilterBoxCanaryV2.Items.Clear();
        _editorSavedFilterBoxCanaryV2.Items.Add("Choose saved filter…");
        foreach (CanarySavedFilter preset in LoadSavedFiltersCanaryV2())
            _editorSavedFilterBoxCanaryV2.Items.Add("Saved: " + preset.Name);
        _editorSavedFilterBoxCanaryV2.SelectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(select))
        {
            string target = "Saved: " + select;
            foreach (object item in _editorSavedFilterBoxCanaryV2.Items)
            {
                if (string.Equals(item?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    _editorSavedFilterBoxCanaryV2.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void SaveCurrentFilterPresetCanaryV2()
    {
        var prompt = new TextPromptWindow("Save Filter Settings", "Name this filter preset:") { Owner = this };
        if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Value)) return;

        string name = prompt.Value.Trim();
        List<CanarySavedFilter> presets = LoadSavedFiltersCanaryV2();
        presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        presets.Add(new CanarySavedFilter(
            name,
            _editorFilterStrengthCanary?.Value ?? 100,
            _editorFilterBrightnessCanary?.Value ?? 0,
            _editorFilterContrastCanary?.Value ?? 0,
            _editorFilterSaturationCanary?.Value ?? 0,
            _editorFilterTemperatureCanary?.Value ?? 0,
            _editorFilterFadeCanary?.Value ?? 0,
            _editorFilterBlurCanary?.Value ?? 0,
            _editorPixelateSliderCanaryV2?.Value ?? 0));
        SaveSavedFiltersCanaryV2(presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList());
        RefreshSavedFilterPresetsCanaryV2(name);
        SetEditorStatus($"Saved filter settings as ‘{name}’. ");
    }

    private void DeleteSelectedFilterPresetCanaryV2()
    {
        string selected = _editorSavedFilterBoxCanaryV2?.SelectedItem?.ToString() ?? string.Empty;
        if (!selected.StartsWith("Saved: ", StringComparison.OrdinalIgnoreCase)) return;
        string name = selected[7..].Trim();
        List<CanarySavedFilter> presets = LoadSavedFiltersCanaryV2();
        presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        SaveSavedFiltersCanaryV2(presets);
        RefreshSavedFilterPresetsCanaryV2();
        SetEditorStatus($"Deleted saved filter ‘{name}’. ");
    }

    private void ApplySelectedSavedFilterCanaryV2()
    {
        string selected = _editorSavedFilterBoxCanaryV2?.SelectedItem?.ToString() ?? string.Empty;
        if (!selected.StartsWith("Saved: ", StringComparison.OrdinalIgnoreCase)) return;
        string name = selected[7..].Trim();
        CanarySavedFilter? preset = LoadSavedFiltersCanaryV2()
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null) return;

        _editorFilterUiUpdatingCanary = true;
        if (_editorFilterPresetCanary is not null) _editorFilterPresetCanary.SelectedIndex = 0;
        if (_editorFilterStrengthCanary is not null) _editorFilterStrengthCanary.Value = preset.Strength;
        if (_editorFilterBrightnessCanary is not null) _editorFilterBrightnessCanary.Value = preset.Brightness;
        if (_editorFilterContrastCanary is not null) _editorFilterContrastCanary.Value = preset.Contrast;
        if (_editorFilterSaturationCanary is not null) _editorFilterSaturationCanary.Value = preset.Saturation;
        if (_editorFilterTemperatureCanary is not null) _editorFilterTemperatureCanary.Value = preset.Temperature;
        if (_editorFilterFadeCanary is not null) _editorFilterFadeCanary.Value = preset.Fade;
        if (_editorFilterBlurCanary is not null) _editorFilterBlurCanary.Value = preset.Blur;
        if (_editorPixelateSliderCanaryV2 is not null) _editorPixelateSliderCanaryV2.Value = preset.Pixelate;
        _editorFilterUiUpdatingCanary = false;
        ScheduleCanaryFilterPreview();
        SetEditorStatus($"Loaded saved filter ‘{preset.Name}’. ");
    }

    private static List<CanarySavedFilter> LoadSavedFiltersCanaryV2()
    {
        try
        {
            if (!File.Exists(CanaryFilterPresetsFileV2)) return new List<CanarySavedFilter>();
            return JsonSerializer.Deserialize<List<CanarySavedFilter>>(File.ReadAllText(CanaryFilterPresetsFileV2))
                   ?? new List<CanarySavedFilter>();
        }
        catch
        {
            return new List<CanarySavedFilter>();
        }
    }

    private static void SaveSavedFiltersCanaryV2(IReadOnlyList<CanarySavedFilter> presets)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CanaryFilterPresetsFileV2)!);
            File.WriteAllText(CanaryFilterPresetsFileV2, JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save Editor filter presets.", ex);
        }
    }

    private void ConfigureEditorHistoryCanaryV2()
    {
        PreviewKeyDown += EditorHistoryKeyDownCanaryV2;

        if (_editorComposition is not null)
        {
            _editorComposition.AddHandler(Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(EditorHistoryCompositionMouseDownCanaryV2), true);
            _editorComposition.AddHandler(Mouse.PreviewMouseUpEvent,
                new MouseButtonEventHandler((_, _) => _editorHistoryDragCapturedCanaryV2 = false), true);
        }

        foreach (Slider? slider in new[] { _editorChatXSlider, _editorChatYSlider })
        {
            slider?.AddHandler(Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler((_, _) => PushEditorHistoryCanaryV2("Chat position")), true);
        }

        if (_editorToolPanels.TryGetValue("filters", out FrameworkElement? filters))
        {
            foreach (Button button in FindVisualChildrenCanary<Button>(filters))
            {
                string text = button.Content?.ToString() ?? string.Empty;
                if (text is "Apply Changes" or "Rotate Left" or "Rotate Right" or "Flip H" or "Flip V")
                {
                    button.AddHandler(Mouse.PreviewMouseDownEvent,
                        new MouseButtonEventHandler((_, _) => PushEditorHistoryCanaryV2(text)), true);
                }
            }
        }
    }

    private void EditorHistoryCompositionMouseDownCanaryV2(object sender, MouseButtonEventArgs e)
    {
        if (_editorHistoryDragCapturedCanaryV2 || _editorSelectionToolCanary != CanarySelectionTool.None || _editorComposition is null)
            return;
        Point p = e.GetPosition(_editorComposition);
        if (!IsEditorChatPointV061(p)) return;
        _editorHistoryDragCapturedCanaryV2 = true;
        PushEditorHistoryCanaryV2("Chat position");
    }

    private void EditorHistoryKeyDownCanaryV2(object sender, KeyEventArgs e)
    {
        if (_editorPage?.Visibility != Visibility.Visible ||
            Keyboard.FocusedElement is TextBox or RichTextBox)
            return;

        ModifierKeys modifiers = Keyboard.Modifiers;
        bool ctrl = (modifiers & ModifierKeys.Control) != 0;
        bool shift = (modifiers & ModifierKeys.Shift) != 0;
        if (!ctrl || e.Key != Key.Z) return;

        bool useLayerHistory = _editorSelectedImageLayerV067 is not null &&
            (shift ? _editorLayerRedoV068.Count > 0 : _editorLayerUndoV068.Count > 0);
        if (useLayerHistory)
        {
            if (shift) RedoLayerEditV068();
            else UndoLayerEditV068();
        }
        else if (shift)
            RedoEditorHistoryCanaryV2();
        else
            UndoEditorHistoryCanaryV2();
        e.Handled = true;
    }

    private void PushEditorHistoryCanaryV2(string description)
    {
        if (_editorHistoryRestoringCanaryV2) return;
        CanaryEditorImageSnapshot snapshot = CaptureEditorHistoryCanaryV2(description);
        _editorUndoCanaryV2.Push(snapshot);
        while (_editorUndoCanaryV2.Count > CanaryEditorHistoryLimitV2)
        {
            CanaryEditorImageSnapshot[] keep = _editorUndoCanaryV2.Take(CanaryEditorHistoryLimitV2).Reverse().ToArray();
            _editorUndoCanaryV2.Clear();
            foreach (CanaryEditorImageSnapshot entry in keep) _editorUndoCanaryV2.Push(entry);
        }
        _editorRedoCanaryV2.Clear();
    }

    private CanaryEditorImageSnapshot CaptureEditorHistoryCanaryV2(string description)
    {
        BitmapSource? source = _editorFilterCommittedCanary ?? _editorBaseOriginal;
        BitmapSource? image = source is null || EditorHasAnimatedGifV060 ? null : CloneBitmapCanary(source);
        return new CanaryEditorImageSnapshot(
            image,
            _editorSelectionMaskCanary?.ToArray(),
            _editorSelectionWidthCanary,
            _editorSelectionHeightCanary,
            _editorChatXSlider?.Value ?? 0,
            _editorChatYSlider?.Value ?? 0,
            _editorCropNormalizedV060,
            _editorOutputWidthBox?.Text ?? string.Empty,
            _editorOutputHeightBox?.Text ?? string.Empty,
            description);
    }

    private void UndoEditorHistoryCanaryV2()
    {
        if (_editorUndoCanaryV2.Count == 0)
        {
            SetEditorStatus("Nothing to undo.");
            return;
        }
        CanaryEditorImageSnapshot current = CaptureEditorHistoryCanaryV2("Redo state");
        CanaryEditorImageSnapshot previous = _editorUndoCanaryV2.Pop();
        _editorRedoCanaryV2.Push(current);
        RestoreEditorHistoryCanaryV2(previous);
        SetEditorStatus($"Undid {previous.Description}. Ctrl+Shift+Z redoes it.");
    }

    private void RedoEditorHistoryCanaryV2()
    {
        if (_editorRedoCanaryV2.Count == 0)
        {
            SetEditorStatus("Nothing to redo.");
            return;
        }
        CanaryEditorImageSnapshot current = CaptureEditorHistoryCanaryV2("Undo state");
        CanaryEditorImageSnapshot next = _editorRedoCanaryV2.Pop();
        _editorUndoCanaryV2.Push(current);
        RestoreEditorHistoryCanaryV2(next);
        SetEditorStatus("Redid the last Editor change.");
    }

    private void RestoreEditorHistoryCanaryV2(CanaryEditorImageSnapshot snapshot)
    {
        _editorHistoryRestoringCanaryV2 = true;
        try
        {
            if (snapshot.Image is not null && !EditorHasAnimatedGifV060)
            {
                _editorFilterCommittedCanary = CloneBitmapCanary(snapshot.Image);
                _editorFilterPreviewCanary = null;
                _editorBaseOriginal = _editorFilterCommittedCanary;
            }

            _editorSelectionMaskCanary = snapshot.Selection?.ToArray();
            _editorSelectionWidthCanary = snapshot.SelectionWidth;
            _editorSelectionHeightCanary = snapshot.SelectionHeight;
            if (_editorSelectionMaskCanary is null)
                ClearSelectionCanarySilently();
            else
                RenderSelectionBoundaryCanary();

            if (_editorChatXSlider is not null) _editorChatXSlider.Value = snapshot.ChatX;
            if (_editorChatYSlider is not null) _editorChatYSlider.Value = snapshot.ChatY;
            _editorCropNormalizedV060 = snapshot.Crop;
            if (_editorOutputWidthBox is not null && !string.IsNullOrWhiteSpace(snapshot.OutputWidth)) _editorOutputWidthBox.Text = snapshot.OutputWidth;
            if (_editorOutputHeightBox is not null && !string.IsNullOrWhiteSpace(snapshot.OutputHeight)) _editorOutputHeightBox.Text = snapshot.OutputHeight;

            ResetCanaryFilterControls();
            ResetPixelationAfterCommittedActionCanaryV2();
            ApplyEditorImageAdjustments();
            UpdateEditorCanvasSize();
            RenderExtraChatLayersCanary();
        }
        finally
        {
            _editorHistoryRestoringCanaryV2 = false;
        }
    }

    private void ApplyFilterWithHistoryCanaryV2()
    {
        if (!EnsureCanaryFilterSource()) return;
        if (_editorSelectedImageLayerV067 is EditorImageLayerV067 layer)
            PushLayerEditHistoryV068(layer, "layer filter changes");
        else
            PushEditorHistoryCanaryV2("filter changes");
        CommitCanaryFilterPreview();
        ResetPixelationAfterCommittedActionCanaryV2();
    }

    private void RunEditorTransformWithHistoryCanaryV2(string transform)
    {
        PushEditorHistoryCanaryV2("image transform");
        TransformStillImageCanary(transform);
        ResetPixelationAfterCommittedActionCanaryV2();
    }
}
