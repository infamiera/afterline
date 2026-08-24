using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Afterline;

public partial class MainWindow
{
    private sealed class EditorImageLayerV067
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Image Layer";
        public required BitmapSource Bitmap { get; set; }
        public required Image Image { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Opacity { get; set; } = 1.0;
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; }
    }

    private bool _editorWorkspaceV067Initialized;
    private Border? _editorRightSidebarV067;
    private WrapPanel? _editorPresetGalleryV067;
    private ListBox? _editorLayerListV067;
    private TextBlock? _editorProjectLabelV067;
    private TextBlock? _editorSelectedLayerLabelV067;
    private Slider? _editorLayerOpacityV067;
    private Button? _editorLayerRemoveV067;
    private Button? _editorLayerUpV067;
    private Button? _editorLayerDownV067;
    private Image? _editorSelectionHighlightV067;
    private readonly List<EditorImageLayerV067> _editorImageLayersV067 = new();
    private readonly Dictionary<string, Border> _editorPresetCardsV067 = new(StringComparer.OrdinalIgnoreCase);
    private EditorImageLayerV067? _editorSelectedImageLayerV067;
    private bool _editorLayerUiUpdatingV067;
    private bool _editorLayerCanvasAdjustingV067;
    private string? _editorProjectPathV067;

    private void EnsureEditorWorkspaceV067()
    {
        if (_editorWorkspaceV067Initialized ||
            _editorPage is null ||
            _editorComposition is null ||
            _editorToolPanelHost?.Parent is not Grid editorBody)
            return;

        _editorWorkspaceV067Initialized = true;

        ConfigurePersistentSelectionHighlightV067();
        ConfigureRightSidebarV067(editorBody);
        ConfigureAdvancedImageLayersV068(editorBody);
        ConfigureFilterPresetGalleryV067();
        ConfigureSelectionPersistenceV067();
        RebuildEditorTaskbarV067();
        UpdateEditorLayerZOrderV067();

        _editorComposition.SizeChanged += (_, _) => EnsureLayerCanvasExtentV067();
        _editorPage.IsVisibleChanged += (_, _) =>
        {
            if (_editorPage.Visibility == Visibility.Visible)
            {
                ApplyEditorChromeCanary(true);
                RefreshFilterPresetGalleryV067();
                RefreshLayerListV067();
                RefreshSelectedLayerAdornerV068();
            }
        };

        RefreshFilterPresetGalleryV067();
        RefreshLayerListV067();
        UpdateProjectLabelV067();
    }

    private void ConfigurePersistentSelectionHighlightV067()
    {
        if (_editorGuideHostCanary is null || _editorSelectionBoundaryImageCanary is null)
            return;

        _editorSelectionHighlightV067 = new Image
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Width = Math.Max(1, _editorComposition?.Width ?? 1),
            Height = Math.Max(1, _editorComposition?.Height ?? 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        Panel.SetZIndex(_editorSelectionHighlightV067, 3);
        _editorGuideHostCanary.Children.Add(_editorSelectionHighlightV067);

        DependencyPropertyDescriptor? descriptor =
            DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));
        descriptor?.AddValueChanged(_editorSelectionBoundaryImageCanary, (_, _) => RefreshSelectionHighlightV067());

        if (_editorComposition is not null)
            _editorComposition.SizeChanged += (_, _) =>
            {
                if (_editorSelectionHighlightV067 is null) return;
                _editorSelectionHighlightV067.Width = Math.Max(1, _editorComposition.Width);
                _editorSelectionHighlightV067.Height = Math.Max(1, _editorComposition.Height);
            };

        RefreshSelectionHighlightV067();
    }

    private void RefreshSelectionHighlightV067()
    {
        if (_editorSelectionHighlightV067 is null)
            return;

        if (_editorSelectionMaskCanary is null ||
            _editorSelectionWidthCanary <= 0 ||
            _editorSelectionHeightCanary <= 0)
        {
            _editorSelectionHighlightV067.Source = null;
            return;
        }

        int width = _editorSelectionWidthCanary;
        int height = _editorSelectionHeightCanary;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        Color accent = (FindResource("Accent") as SolidColorBrush)?.Color ?? Color.FromRgb(91, 159, 239);

        bool Selected(int x, int y)
            => x >= 0 && y >= 0 && x < width && y < height && _editorSelectionMaskCanary[y * width + x];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!Selected(x, y))
                    continue;

                bool edge = false;
                for (int oy = -2; oy <= 2 && !edge; oy++)
                    for (int ox = -2; ox <= 2; ox++)
                        if (!Selected(x + ox, y + oy))
                        {
                            edge = true;
                            break;
                        }

                int i = y * stride + x * 4;
                pixels[i] = accent.B;
                pixels[i + 1] = accent.G;
                pixels[i + 2] = accent.R;
                pixels[i + 3] = edge ? (byte)220 : (byte)58;
            }
        }

        BitmapSource highlight = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        highlight.Freeze();
        _editorSelectionHighlightV067.Source = highlight;
        _editorSelectionHighlightV067.Width = width;
        _editorSelectionHighlightV067.Height = height;
    }

    private void ConfigureSelectionPersistenceV067()
    {
        if (_editorToolButtons.TryGetValue("filters", out Button? filters))
        {
            filters.AddHandler(
                Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler((_, _) => PreserveSelectionAcrossLegacyFilterPrepareV067()),
                true);
        }
    }

    private void PreserveSelectionAcrossLegacyFilterPrepareV067()
    {
        bool[]? mask = _editorSelectionMaskCanary?.ToArray();
        int width = _editorSelectionWidthCanary;
        int height = _editorSelectionHeightCanary;
        if (mask is null)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_editorBaseOriginal is null ||
                mask.Length != width * height ||
                _editorBaseOriginal.PixelWidth != width ||
                _editorBaseOriginal.PixelHeight != height)
                return;

            _editorSelectionMaskCanary = mask;
            _editorSelectionWidthCanary = width;
            _editorSelectionHeightCanary = height;
            RenderSelectionBoundaryCanary();
            RefreshSelectionHighlightV067();
        }), DispatcherPriority.ApplicationIdle);
    }

    private void PrepareEditorFiltersPreservingSelectionV067()
    {
        bool[]? mask = _editorSelectionMaskCanary?.ToArray();
        int width = _editorSelectionWidthCanary;
        int height = _editorSelectionHeightCanary;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_editorBaseOriginal is null || EditorHasAnimatedGifV060)
                return;

            EnsureCanaryFilterSource();

            if (mask is not null &&
                _editorBaseOriginal.PixelWidth == width &&
                _editorBaseOriginal.PixelHeight == height &&
                mask.Length == width * height)
            {
                _editorSelectionMaskCanary = mask;
                _editorSelectionWidthCanary = width;
                _editorSelectionHeightCanary = height;
                RenderSelectionBoundaryCanary();
                RefreshSelectionHighlightV067();
            }

            // Filter rendering is intentionally deferred until a control changes so
            // opening the panel never blocks scrolling or the first slider gesture.
        }), DispatcherPriority.Background);
    }

    private void ConfigureRightSidebarV067(Grid editorBody)
    {
        if (_editorRightSidebarV067 is not null)
            return;

        while (editorBody.ColumnDefinitions.Count < 7)
        {
            if (editorBody.ColumnDefinitions.Count == 5)
                editorBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            else
                editorBody.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(300),
                    MinWidth = 260,
                    MaxWidth = 390
                });
        }

        var sidebar = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(8),
            MinWidth = 260
        };
        _editorRightSidebarV067 = sidebar;
        Grid.SetColumn(sidebar, 6);

        var root = new StackPanel();

        var projectBar = new Grid();
        projectBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        projectBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _editorProjectLabelV067 = new TextBlock
        {
            Text = "Project · Untitled",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        projectBar.Children.Add(_editorProjectLabelV067);

        var addImage = new Button
        {
            Content = "+ Image",
            Padding = new Thickness(7, 3, 7, 3),
            MinHeight = 26,
            ToolTip = "Add another image layer to this project."
        };
        addImage.Click += (_, _) => AddImageLayerV067();
        Grid.SetColumn(addImage, 1);
        projectBar.Children.Add(addImage);
        projectBar.Margin = new Thickness(0, 0, 0, 7);
        root.Children.Add(projectBar);

        Expander presets = CreateEditorSidebarExpanderV068(
            "FILTER PRESETS",
            BuildFilterPresetPanelV067(),
            expanded: true);
        root.Children.Add(presets);

        if (_editorToolPanels.TryGetValue("filters", out FrameworkElement? adjustments))
        {
            DetachEditorElement(adjustments);
            adjustments.MaxHeight = 430;
            _editorFilterAdjustmentsExpanderV068 = CreateEditorSidebarExpanderV068(
                "FILTERS & ADJUSTMENTS",
                adjustments,
                expanded: false);
            _editorFilterAdjustmentsExpanderV068.Margin = new Thickness(0, 7, 0, 0);
            root.Children.Add(_editorFilterAdjustmentsExpanderV068);
            _editorToolPanels.Remove("filters");
            RemoveEditorRailButtonV068("filters");
        }

        Border layers = BuildLayersPanelV067();
        layers.Margin = new Thickness(0, 7, 0, 0);
        root.Children.Add(layers);

        sidebar.Child = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        editorBody.Children.Add(sidebar);
    }

    private Border BuildFilterPresetPanelV067()
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8)
        };

        var root = new StackPanel();

        _editorPresetGalleryV067 = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        var scroll = new ScrollViewer
        {
            Content = _editorPresetGalleryV067,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        root.Children.Add(scroll);

        var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        Button save = CreateSmallEditorButton("Save Current", (_, _) =>
        {
            SaveCurrentFilterPresetCanaryV2();
            RefreshFilterPresetGalleryV067();
        });
        save.ToolTip = "Save the current adjustment values as a reusable preset.";
        Button delete = CreateSmallEditorButton("Delete Saved", (_, _) =>
        {
            DeleteSelectedFilterPresetCanaryV2();
            RefreshFilterPresetGalleryV067();
        });
        delete.ToolTip = "Delete the selected saved preset.";
        actions.Children.Add(save);
        actions.Children.Add(delete);
        root.Children.Add(actions);

        border.Child = root;
        return border;
    }

    private Border BuildLayersPanelV067()
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8)
        };

        var root = new StackPanel();

        root.Children.Add(new TextBlock
        {
            Text = "LAYERS",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        _editorLayerListV067 = new ListBox
        {
            MinHeight = 270,
            MaxHeight = 520,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _editorLayerListV067.SelectionChanged += (_, _) =>
        {
            _editorSelectedImageLayerV067 =
                (_editorLayerListV067.SelectedItem as ListBoxItem)?.Tag as EditorImageLayerV067;
            if (_editorSelectedImageLayerV067 is not null)
                DeactivateSelectionInteractionCanary();
            SyncLayerControlsV067();
            RefreshSelectedLayerAdornerV068();
        };
        ConfigureLayerListDragReorderV069();
        root.Children.Add(_editorLayerListV067);

        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 4) };
        buttons.Children.Add(CreateSmallEditorButton("+ Image", (_, _) => AddImageLayerV067()));
        _editorLayerRemoveV067 = CreateSmallEditorButton("Remove", (_, _) => RemoveSelectedImageLayerV067());
        _editorLayerUpV067 = CreateSmallEditorButton("Up", (_, _) => MoveSelectedImageLayerV067(1));
        _editorLayerDownV067 = CreateSmallEditorButton("Down", (_, _) => MoveSelectedImageLayerV067(-1));
        buttons.Children.Add(_editorLayerRemoveV067);
        buttons.Children.Add(_editorLayerUpV067);
        buttons.Children.Add(_editorLayerDownV067);
        root.Children.Add(buttons);

        var controls = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        _editorSelectedLayerLabelV067 = new TextBlock
        {
            Text = "Select an image layer to edit it.",
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };
        controls.Children.Add(_editorSelectedLayerLabelV067);

        var opacity = CreateEditorV041Slider("Opacity", 0, 100, 100, 1);
        _editorLayerOpacityV067 = opacity.Slider;
        _editorLayerOpacityV067.ValueChanged += (_, _) => ApplyLayerControlValuesV067();
        controls.Children.Add(opacity.Panel);

        controls.Children.Add(EditorSubtleNote(
            "Drag the selected image on the canvas. Use its corner handle or right-click it to set an exact pixel size, lock it, or reset its dimensions."));
        root.Children.Add(controls);

        border.Child = root;
        return border;
    }

    private void ConfigureFilterPresetGalleryV067()
    {
        if (_editorFilterPresetCanary?.Parent is FrameworkElement builtInParent)
            builtInParent.Visibility = Visibility.Collapsed;
        else if (_editorFilterPresetCanary is not null)
            _editorFilterPresetCanary.Visibility = Visibility.Collapsed;

        if (_editorSavedFilterBoxCanaryV2?.Parent is FrameworkElement savedParent)
            savedParent.Visibility = Visibility.Collapsed;
        else if (_editorSavedFilterBoxCanaryV2 is not null)
            _editorSavedFilterBoxCanaryV2.Visibility = Visibility.Collapsed;

        if (_editorFilterPresetCanary is not null)
            _editorFilterPresetCanary.SelectionChanged += (_, _) => UpdatePresetCardSelectionV067();

        if (_editorSavedFilterBoxCanaryV2 is not null)
            _editorSavedFilterBoxCanaryV2.SelectionChanged += (_, _) => UpdatePresetCardSelectionV067();

        _editorPage?.AddHandler(
            UIElement.DropEvent,
            new DragEventHandler((_, _) => Dispatcher.BeginInvoke(
                new Action(RefreshFilterPresetGalleryV067),
                DispatcherPriority.Background)),
            true);
    }

    private void RefreshFilterPresetGalleryV067()
    {
        if (_editorPresetGalleryV067 is null)
            return;

        _editorPresetGalleryV067.Children.Clear();
        _editorPresetCardsV067.Clear();

        BitmapSource source = CreatePresetPreviewSourceV067();
        foreach (string preset in new[] { "None", "Warm", "Cool", "Black & White", "Faded", "Cinematic", "High Contrast" })
        {
            BitmapSource preview = ApplyPresetPreviewV067(source, preset, null);
            AddPresetCardV067(preset, preview, () =>
            {
                if (_editorFilterPresetCanary is not null)
                    _editorFilterPresetCanary.SelectedItem = preset;
                if (_editorSavedFilterBoxCanaryV2 is not null)
                    _editorSavedFilterBoxCanaryV2.SelectedIndex = 0;
                ScheduleCanaryFilterPreview();
                UpdatePresetCardSelectionV067();
            });
        }

        foreach (CanarySavedFilter saved in LoadSavedFiltersCanaryV2())
        {
            BitmapSource preview = ApplyPresetPreviewV067(source, "None", saved);
            string key = "Saved: " + saved.Name;
            AddPresetCardV067(saved.Name, preview, () =>
            {
                if (_editorSavedFilterBoxCanaryV2 is not null)
                {
                    string target = "Saved: " + saved.Name;
                    foreach (object item in _editorSavedFilterBoxCanaryV2.Items)
                    {
                        if (string.Equals(item?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                        {
                            _editorSavedFilterBoxCanaryV2.SelectedItem = item;
                            break;
                        }
                    }
                }
                UpdatePresetCardSelectionV067();
            }, key);
        }

        UpdatePresetCardSelectionV067();
    }

    private void AddPresetCardV067(string label, BitmapSource preview, Action action, string? key = null)
    {
        if (_editorPresetGalleryV067 is null) return;
        string cardKey = key ?? label;

        var image = new Image
        {
            Source = preview,
            Width = 78,
            Height = 48,
            Stretch = Stretch.UniformToFill
        };
        var stack = new StackPanel();
        stack.Children.Add(image);
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(2, 4, 2, 0)
        });

        var card = new Border
        {
            Width = 88,
            Height = 76,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 5, 5),
            CornerRadius = new CornerRadius(5),
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = stack,
            ToolTip = $"Apply {label} preset"
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            PrepareEditorFiltersPreservingSelectionV067();
            action();
        };
        _editorPresetCardsV067[cardKey] = card;
        _editorPresetGalleryV067.Children.Add(card);
    }

    private void UpdatePresetCardSelectionV067()
    {
        string builtIn = _editorFilterPresetCanary?.SelectedItem?.ToString() ?? "None";
        string saved = _editorSavedFilterBoxCanaryV2?.SelectedItem?.ToString() ?? string.Empty;
        string selected = saved.StartsWith("Saved: ", StringComparison.OrdinalIgnoreCase) ? saved : builtIn;

        foreach ((string key, Border card) in _editorPresetCardsV067)
        {
            bool active = string.Equals(key, selected, StringComparison.OrdinalIgnoreCase);
            card.BorderBrush = active ? (Brush)FindResource("Accent") : (Brush)FindResource("Border");
            card.BorderThickness = active ? new Thickness(2) : new Thickness(1);
        }
    }

    private BitmapSource CreatePresetPreviewSourceV067()
    {
        BitmapSource? source = _editorFilterCommittedCanary ?? _editorBaseOriginal;
        return source is null ? CreateStockPresetPreviewV067() : RenderPresetThumbnailV067(source);
    }

    private static BitmapSource RenderPresetThumbnailV067(BitmapSource source)
    {
        const int width = 156;
        const int height = 88;
        var image = new Image
        {
            Source = source,
            Stretch = Stretch.UniformToFill,
            Width = width,
            Height = height
        };
        var host = new Border
        {
            Width = width,
            Height = height,
            ClipToBounds = true,
            Child = image
        };
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource CreateStockPresetPreviewV067()
    {
        const int width = 156;
        const int height = 88;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double t = y / (double)(height - 1);
                byte r;
                byte g;
                byte b;

                if (y < 52)
                {
                    r = (byte)Math.Clamp(110 + t * 125 + (x > 95 ? 20 : 0), 0, 255);
                    g = (byte)Math.Clamp(155 + t * 75, 0, 255);
                    b = (byte)Math.Clamp(205 - t * 70, 0, 255);
                }
                else
                {
                    double wave = Math.Sin(x * 0.13) * 9;
                    r = (byte)Math.Clamp(55 + wave + (height - y) * 2.3, 0, 255);
                    g = (byte)Math.Clamp(72 + wave + (height - y) * 1.7, 0, 255);
                    b = (byte)Math.Clamp(77 + wave + (height - y) * 1.0, 0, 255);
                }

                double dx = x - 118;
                double dy = y - 31;
                if (dx * dx + dy * dy < 105)
                {
                    r = 244;
                    g = 179;
                    b = 91;
                }

                int i = y * stride + x * 4;
                pixels[i] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = 255;
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private BitmapSource ApplyPresetPreviewV067(BitmapSource source, string preset, CanarySavedFilter? saved)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        double strength = saved is null ? 1.0 : saved.Strength / 100.0;
        double brightness = saved?.Brightness ?? 0;
        double contrast = saved?.Contrast ?? 0;
        double saturation = saved?.Saturation ?? 0;
        double temperature = saved?.Temperature ?? 0;
        double fade = saved?.Fade ?? 0;
        double blur = saved?.Blur ?? 0;
        double pixelate = saved?.Pixelate ?? 0;

        switch (preset)
        {
            case "Warm":
                temperature += 48 * strength;
                saturation += 8 * strength;
                break;
            case "Cool":
                temperature -= 48 * strength;
                contrast += 5 * strength;
                break;
            case "Black & White":
                saturation -= 100 * strength;
                contrast += 8 * strength;
                break;
            case "Faded":
                saturation -= 38 * strength;
                contrast -= 20 * strength;
                brightness += 7 * strength;
                fade += 35 * strength;
                break;
            case "Cinematic":
                contrast += 24 * strength;
                saturation -= 10 * strength;
                temperature += 10 * strength;
                fade += 8 * strength;
                break;
            case "High Contrast":
                contrast += 38 * strength;
                saturation += 8 * strength;
                break;
        }

        double contrastScale = 1.0 + contrast / 100.0;
        double saturationScale = Math.Max(0, 1.0 + saturation / 100.0);
        double brightnessOffset = brightness * 2.0;
        double temperatureOffset = temperature * 0.75;
        double fadeAmount = Math.Clamp(fade / 100.0, 0, 1);

        for (int i = 0; i < pixels.Length; i += 4)
        {
            double b = pixels[i];
            double g = pixels[i + 1];
            double r = pixels[i + 2];

            r = (r - 128) * contrastScale + 128 + brightnessOffset;
            g = (g - 128) * contrastScale + 128 + brightnessOffset;
            b = (b - 128) * contrastScale + 128 + brightnessOffset;

            double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            r = luminance + (r - luminance) * saturationScale;
            g = luminance + (g - luminance) * saturationScale;
            b = luminance + (b - luminance) * saturationScale;
            r += temperatureOffset;
            b -= temperatureOffset;

            if (fadeAmount > 0)
            {
                double lifted = 118 + (luminance - 118) * 0.72;
                r = r * (1 - fadeAmount) + lifted * fadeAmount;
                g = g * (1 - fadeAmount) + lifted * fadeAmount;
                b = b * (1 - fadeAmount) + lifted * fadeAmount;
            }

            pixels[i] = ClampEditorByte(b);
            pixels[i + 1] = ClampEditorByte(g);
            pixels[i + 2] = ClampEditorByte(r);
        }

        if (blur >= 1)
            pixels = BoxBlurCanary(pixels, width, height, stride, Math.Clamp((int)Math.Round(blur), 1, 10));

        BitmapSource result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();

        if (pixelate > 1)
            result = PixelatePreviewBitmapV067(result, Math.Clamp((int)Math.Round(pixelate), 2, 48));

        return result;
    }

    private static BitmapSource PixelatePreviewBitmapV067(BitmapSource source, int block)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] input = new byte[stride * height];
        converted.CopyPixels(input, stride, 0);
        byte[] output = (byte[])input.Clone();

        for (int by = 0; by < height; by += block)
        {
            for (int bx = 0; bx < width; bx += block)
            {
                int maxX = Math.Min(width, bx + block);
                int maxY = Math.Min(height, by + block);
                long b = 0, g = 0, r = 0, a = 0;
                int count = 0;
                for (int y = by; y < maxY; y++)
                    for (int x = bx; x < maxX; x++)
                    {
                        int i = y * stride + x * 4;
                        b += input[i];
                        g += input[i + 1];
                        r += input[i + 2];
                        a += input[i + 3];
                        count++;
                    }

                byte bb = (byte)(b / count);
                byte gg = (byte)(g / count);
                byte rr = (byte)(r / count);
                byte aa = (byte)(a / count);
                for (int y = by; y < maxY; y++)
                    for (int x = bx; x < maxX; x++)
                    {
                        int i = y * stride + x * 4;
                        output[i] = bb;
                        output[i + 1] = gg;
                        output[i + 2] = rr;
                        output[i + 3] = aa;
                    }
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, output, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private void AddImageLayerV067()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add image layer",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        foreach (string path in dialog.FileNames)
        {
            try
            {
                BitmapSource bitmap = LoadBitmapFileV067(path);
                AddImageLayerFromBitmapV067(bitmap, Path.GetFileName(path), refresh: false);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Unable to add Editor image layer.", ex);
                System.Windows.MessageBox.Show(this, ex.Message, "Unable to add image layer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        UpdateEditorLayerZOrderV067();
        EnsureLayerCanvasExtentV067();
        RefreshLayerListV067();
        SetEditorStatus($"Added {dialog.FileNames.Length:N0} image layer(s).");
    }

    private static BitmapSource LoadBitmapFileV067(string path)
    {
        using FileStream stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private EditorImageLayerV067 AddImageLayerFromBitmapV067(
        BitmapSource bitmap,
        string name,
        double x = 0,
        double y = 0,
        double scale = 1,
        double opacity = 1,
        bool visible = true,
        bool locked = false,
        double? width = null,
        double? height = null,
        bool refresh = true)
    {
        if (_editorComposition is null)
            throw new InvalidOperationException("Editor canvas is not available.");

        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };

        var layer = new EditorImageLayerV067
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Image Layer" : name,
            Bitmap = bitmap,
            Image = image,
            X = Math.Max(0, x),
            Y = Math.Max(0, y),
            Width = Math.Max(1, width ?? bitmap.PixelWidth * Math.Clamp(scale, 0.1, 3.0)),
            Height = Math.Max(1, height ?? bitmap.PixelHeight * Math.Clamp(scale, 0.1, 3.0)),
            Opacity = Math.Clamp(opacity, 0, 1),
            IsVisible = visible,
            IsLocked = locked
        };
        _editorImageLayersV067.Add(layer);
        _editorComposition.Children.Add(image);
        UpdateImageLayerVisualV067(layer);
        UpdateEditorLayerZOrderV067();

        if (refresh)
        {
            EnsureLayerCanvasExtentV067();
            RefreshLayerListV067(layer);
        }

        return layer;
    }

    private void UpdateImageLayerVisualV067(EditorImageLayerV067 layer)
    {
        layer.Image.Source = layer.Bitmap;
        layer.Image.Width = Math.Max(1, layer.Width);
        layer.Image.Height = Math.Max(1, layer.Height);
        layer.Image.Margin = new Thickness(layer.X, layer.Y, 0, 0);
        layer.Image.Opacity = layer.Opacity;
        layer.Image.Visibility = layer.IsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEditorLayerZOrderV067()
    {
        for (int i = 0; i < _editorImageLayersV067.Count; i++)
            Panel.SetZIndex(_editorImageLayersV067[i].Image, 10 + i);

        if (_editorChatImage is not null)
            Panel.SetZIndex(_editorChatImage, 500);

        for (int i = 0; i < _editorExtraChatsCanary.Count; i++)
            Panel.SetZIndex(_editorExtraChatsCanary[i].Image, 510 + i);
    }

    private void RefreshLayerListV067(EditorImageLayerV067? select = null)
    {
        if (_editorLayerListV067 is null)
            return;

        EditorImageLayerV067? keep = select ?? _editorSelectedImageLayerV067;
        _editorLayerListV067.Items.Clear();

        for (int i = _editorImageLayersV067.Count - 1; i >= 0; i--)
        {
            EditorImageLayerV067 layer = _editorImageLayersV067[i];
            var item = new ListBoxItem
            {
                Tag = layer,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(3)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var visible = new CheckBox
            {
                IsChecked = layer.IsVisible,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Show or hide this layer."
            };
            visible.Checked += (_, _) =>
            {
                layer.IsVisible = true;
                UpdateImageLayerVisualV067(layer);
                RefreshSelectedLayerAdornerV068();
            };
            visible.Unchecked += (_, _) =>
            {
                layer.IsVisible = false;
                UpdateImageLayerVisualV067(layer);
                RefreshSelectedLayerAdornerV068();
            };
            row.Children.Add(visible);

            var thumb = new Image
            {
                Source = layer.Bitmap,
                Width = 46,
                Height = 30,
                Stretch = Stretch.UniformToFill
            };
            Grid.SetColumn(thumb, 2);
            row.Children.Add(thumb);

            var name = new TextBlock
            {
                Text = layer.Name,
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            var lockButton = new Button
            {
                Content = layer.IsLocked ? "🔒" : string.Empty,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 11,
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Visibility = layer.IsLocked ? Visibility.Visible : Visibility.Hidden,
                ToolTip = layer.IsLocked ? "Unlock this layer" : "Layer is unlocked"
            };
            lockButton.Click += (_, e) =>
            {
                e.Handled = true;
                if (!layer.IsLocked) return;
                PushLayerEditHistoryV068(layer, "layer lock");
                layer.IsLocked = false;
                RefreshLayerListV067(layer);
                RefreshSelectedLayerAdornerV068();
                SetEditorStatus($"Unlocked image layer ‘{layer.Name}’.");
            };
            Grid.SetColumn(lockButton, 4);
            row.Children.Add(lockButton);

            Grid.SetColumn(name, 6);
            row.Children.Add(name);

            item.Content = row;
            _editorLayerListV067.Items.Add(item);
            if (ReferenceEquals(layer, keep))
                _editorLayerListV067.SelectedItem = item;
        }

        BitmapSource? baseSource = _editorFilterCommittedCanary ?? _editorBaseOriginal;
        if (baseSource is not null)
        {
            var baseItem = new ListBoxItem
            {
                Tag = "base",
                IsEnabled = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(3)
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(new TextBlock
            {
                Text = "●",
                Foreground = (Brush)FindResource("MutedText"),
                FontSize = 9,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });

            var thumb = new Image
            {
                Source = baseSource,
                Width = 46,
                Height = 30,
                Stretch = Stretch.UniformToFill
            };
            Grid.SetColumn(thumb, 1);
            row.Children.Add(thumb);

            var label = new TextBlock
            {
                Text = "Base Image",
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 3);
            row.Children.Add(label);
            baseItem.Content = row;
            _editorLayerListV067.Items.Add(baseItem);
        }

        if (_editorLayerListV067.SelectedItem is null && _editorLayerListV067.Items.Count > 0)
            _editorLayerListV067.SelectedIndex = 0;

        SyncLayerControlsV067();
    }

    private void SyncLayerControlsV067()
    {
        _editorLayerUiUpdatingV067 = true;
        try
        {
            EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
            bool enabled = layer is not null;

            if (_editorSelectedLayerLabelV067 is not null)
                _editorSelectedLayerLabelV067.Text = enabled
                    ? $"Editing · {layer!.Name}"
                    : "Select an added image layer to edit it.";

            if (_editorLayerOpacityV067 is not null)
            {
                _editorLayerOpacityV067.IsEnabled = enabled;
                _editorLayerOpacityV067.Value = enabled ? layer!.Opacity * 100 : 100;
            }
            if (_editorLayerRemoveV067 is not null) _editorLayerRemoveV067.IsEnabled = enabled;
            if (_editorLayerUpV067 is not null) _editorLayerUpV067.IsEnabled = enabled;
            if (_editorLayerDownV067 is not null) _editorLayerDownV067.IsEnabled = enabled;
        }
        finally
        {
            _editorLayerUiUpdatingV067 = false;
        }
    }

    private void ApplyLayerControlValuesV067()
    {
        if (_editorLayerUiUpdatingV067 || _editorSelectedImageLayerV067 is null)
            return;

        EditorImageLayerV067 layer = _editorSelectedImageLayerV067;
        layer.Opacity = Math.Clamp((_editorLayerOpacityV067?.Value ?? 100) / 100.0, 0, 1);
        UpdateImageLayerVisualV067(layer);
        EnsureLayerCanvasExtentV067();
        RefreshSelectedLayerAdornerV068();
    }

    private void RemoveSelectedImageLayerV067()
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        if (layer is null || _editorComposition is null)
            return;

        _editorComposition.Children.Remove(layer.Image);
        _editorImageLayersV067.Remove(layer);
        _editorSelectedImageLayerV067 = null;
        RefreshSelectedLayerAdornerV068();
        UpdateEditorCanvasSize();
        EnsureLayerCanvasExtentV067();
        UpdateEditorLayerZOrderV067();
        RefreshLayerListV067();
        SetEditorStatus($"Removed image layer ‘{layer.Name}’.");
    }

    private void MoveSelectedImageLayerV067(int direction)
    {
        EditorImageLayerV067? layer = _editorSelectedImageLayerV067;
        if (layer is null)
            return;

        int index = _editorImageLayersV067.IndexOf(layer);
        int target = Math.Clamp(index + direction, 0, _editorImageLayersV067.Count - 1);
        if (target == index)
            return;

        _editorImageLayersV067.RemoveAt(index);
        _editorImageLayersV067.Insert(target, layer);
        UpdateEditorLayerZOrderV067();
        RefreshLayerListV067(layer);
    }

    private void ClearImageLayersV067()
    {
        if (_editorComposition is not null)
            foreach (EditorImageLayerV067 layer in _editorImageLayersV067)
                _editorComposition.Children.Remove(layer.Image);

        _editorImageLayersV067.Clear();
        _editorSelectedImageLayerV067 = null;
        ClearLayerEditHistoryV068();
        RefreshSelectedLayerAdornerV068();
        RefreshLayerListV067();
    }

    private void EnsureLayerCanvasExtentV067()
    {
        if (_editorLayerCanvasAdjustingV067 || _editorComposition is null || _editorImageLayersV067.Count == 0)
            return;

        double requiredWidth = _editorComposition.Width;
        double requiredHeight = _editorComposition.Height;
        foreach (EditorImageLayerV067 layer in _editorImageLayersV067.Where(l => l.IsVisible))
        {
            requiredWidth = Math.Max(requiredWidth, layer.X + layer.Width);
            requiredHeight = Math.Max(requiredHeight, layer.Y + layer.Height);
        }

        if (requiredWidth <= _editorComposition.Width + 0.5 &&
            requiredHeight <= _editorComposition.Height + 0.5)
            return;

        _editorLayerCanvasAdjustingV067 = true;
        try
        {
            _editorComposition.Width = Math.Ceiling(requiredWidth);
            _editorComposition.Height = Math.Ceiling(requiredHeight);
        }
        finally
        {
            _editorLayerCanvasAdjustingV067 = false;
        }
    }

    private void RebuildEditorTaskbarV067()
    {
        if (_editorPage is null)
            return;

        Border? header = _editorPage.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (header is null)
            return;

        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var menus = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("File",
            ("New Project…", NewEditorProjectV067),
            ("Save Current Project…", () => SaveCurrentEditorProjectV067()),
            ("Load Project…", LoadEditorProjectV067),
            ("Open Image / GIF…", () =>
            {
                OpenEditorMediaWithPrewarmCanaryV4();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshFilterPresetGalleryV067();
                    RefreshLayerListV067();
                }), DispatcherPriority.Background);
            }),
            ("Import Chat Text…", () => EditorImportText_Click(this, new RoutedEventArgs())),
            ("Copy Current Frame", () => EditorCopyImageV060_Click(this, new RoutedEventArgs())),
            ("Export PNG…", () => EditorExportPngV060_Click(this, new RoutedEventArgs())),
            ("Export GIF…", () => EditorExportGifV060_Click(this, new RoutedEventArgs())),
            ("Remove Media", () =>
            {
                EditorRemoveMediaV060_Click(this, new RoutedEventArgs());
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ResetCanaryFilterSource();
                    RefreshFilterPresetGalleryV067();
                    RefreshLayerListV067();
                }), DispatcherPriority.Background);
            })));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("Image",
            ("Add Image Layer…", AddImageLayerV067),
            ("Image & Canvas", () => ShowEditorToolPanel("image", true)),
            ("Adjust Crop…", () => EditorAdjustCrop_ClickV060(this, new RoutedEventArgs())),
            ("Reset Crop", () => EditorResetCrop_ClickV060(this, new RoutedEventArgs())),
            ("Rotate Left", () => RunEditorTransformWithHistoryCanaryV2("rotate-left")),
            ("Rotate Right", () => RunEditorTransformWithHistoryCanaryV2("rotate-right")),
            ("Flip Horizontal", () => RunEditorTransformWithHistoryCanaryV2("flip-h")),
            ("Flip Vertical", () => RunEditorTransformWithHistoryCanaryV2("flip-v"))));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("Filter",
            ("Filters & Adjustments", () =>
            {
                OpenFilterAdjustmentsV068();
            }),
            ("Apply Changes", ApplyFilterWithHistoryCanaryV2),
            ("Revert Preview", RevertCanaryFilterPreview),
            ("Save Current Filter…", () =>
            {
                SaveCurrentFilterPresetCanaryV2();
                RefreshFilterPresetGalleryV067();
            })));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("View",
            ("Fit Canvas", FitEditorPreviewToWindow),
            ("Zoom 100%", () => { _editorFitZoom = false; SetEditorZoom(1.0); }),
            ($"Toggle Rulers ({_settings.Editor.RulerKeybind})", ToggleEditorRulersV068),
            ("Chat & Font", () => ShowEditorToolPanel("chat", true)),
            ("Selection Tools", () => ShowEditorToolPanel("selection", true)),
            ("Full Screen Editor", ToggleEditorFullscreenCanary)));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("Help",
            ("Editor Shortcuts", () => new CanaryEditorShortcutsWindow(this, _settings.Editor.RulerKeybind).ShowDialog()),
            ("About Afterline", () => new AboutWindow(this).ShowDialog())));

        bar.Children.Add(menus);

        var label = new TextBlock
        {
            Text = "CANARY EDITOR · PROJECTS & LAYERS",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0)
        };
        Grid.SetColumn(label, 1);
        bar.Children.Add(label);

        header.Background = (Brush)FindResource("Panel");
        header.BorderBrush = (Brush)FindResource("Border");
        header.BorderThickness = new Thickness(1);
        header.Padding = new Thickness(6, 4, 6, 4);
        header.Child = bar;
    }

    private void UpdateProjectLabelV067()
    {
        if (_editorProjectLabelV067 is null)
            return;

        string name = string.IsNullOrWhiteSpace(_editorProjectPathV067)
            ? "Untitled"
            : Path.GetFileNameWithoutExtension(_editorProjectPathV067);
        _editorProjectLabelV067.Text = "Project · " + name;
        _editorProjectLabelV067.ToolTip = _editorProjectPathV067 ?? "This project has not been saved yet.";
    }
}
