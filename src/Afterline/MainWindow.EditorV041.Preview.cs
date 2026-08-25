using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private Border BuildEditorV041PreviewPanel()
    {
        var card = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(10) };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var previewBar = new Grid();
        previewBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        previewBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        previewBar.Children.Add(new TextBlock
        {
            Text = "Preview",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var zoom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        zoom.Children.Add(CreateEditorPreviewButton("−", "Zoom out", (_, _) => ChangeEditorZoom(-0.1)));
        _editorZoomText = new TextBlock
        {
            Text = "100%",
            Width = 52,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Cursor = Cursors.Hand,
            ToolTip = "Reset zoom to 100%"
        };
        _editorZoomText.MouseLeftButtonDown += (_, args) =>
        {
            _editorFitZoom = false;
            SetEditorZoom(1.0);
            args.Handled = true;
        };
        zoom.Children.Add(_editorZoomText);
        zoom.Children.Add(CreateEditorPreviewButton("+", "Zoom in", (_, _) => ChangeEditorZoom(0.1)));
        zoom.Children.Add(CreateEditorPreviewButton("Fit", "Fit the complete image in the preview", (_, _) => FitEditorPreviewToWindow()));
        zoom.Children.Add(CreateEditorPreviewButton("\uE740", "Full screen preview", EditorFullscreenPreview_Click, useIconFont: true));
        Grid.SetColumn(zoom, 1);
        previewBar.Children.Add(zoom);
        root.Children.Add(previewBar);
        UpdateEditorSidebarToggleStateV072();

        _editorPreviewScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0E, 0x13)),
            Padding = new Thickness(0),
            CanContentScroll = false
        };
        _editorPreviewScroll.PreviewMouseWheel += EditorPreviewScroll_PreviewMouseWheel;
        _editorPreviewScroll.SizeChanged += (_, _) =>
        {
            if (_editorFitZoom) FitEditorPreviewToWindow();
        };
        Grid.SetRow(_editorPreviewScroll, 2);

        _editorZoomHost = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _editorComposition,
            LayoutTransform = new ScaleTransform(1, 1)
        };
        _editorPreviewScroll.Content = _editorZoomHost;
        root.Children.Add(_editorPreviewScroll);

        _editorStatusText = new TextBlock
        {
            Text = "Paste chat lines to begin.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 8, 2, 0)
        };
        Grid.SetRow(_editorStatusText, 3);
        root.Children.Add(_editorStatusText);

        _editorRightSidebarReopenV073 = new Button
        {
            Content = "◀",
            Width = 30,
            Height = 48,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Open the right Editor panel",
            Visibility = Visibility.Collapsed
        };
        _editorRightSidebarReopenV073.Click += (_, _) => ToggleEditorRightSidebarV072();
        Grid.SetRow(_editorRightSidebarReopenV073, 2);
        Panel.SetZIndex(_editorRightSidebarReopenV073, 40);
        root.Children.Add(_editorRightSidebarReopenV073);

        card.Child = root;
        return card;
    }

    private Button CreateEditorHeaderButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(6, 0, 0, 0),
            MinHeight = 34
        };
        button.Click += handler;
        return button;
    }

    private Button CreateEditorPreviewButton(string text, string toolTip, RoutedEventHandler handler, bool useIconFont = false)
    {
        var button = new Button
        {
            Content = text,
            Width = text == "Fit" ? 42 : 32,
            Height = 30,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = toolTip,
            FontSize = useIconFont ? 14 : 13
        };
        if (useIconFont) button.FontFamily = new FontFamily("Segoe MDL2 Assets");
        button.Click += handler;
        return button;
    }

    private (StackPanel Panel, Slider Slider) CreateEditorV041Slider(string label, double minimum, double maximum, double value, double tick = 1)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = label, Foreground = (Brush)FindResource("MutedText"), FontSize = 11 });
        var valueText = new TextBlock
        {
            Text = value.ToString(tick < 1 ? "0.0" : "0"),
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10
        };
        Grid.SetColumn(valueText, 1);
        header.Children.Add(valueText);
        panel.Children.Add(header);

        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            TickFrequency = tick,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 3, 0, 0)
        };
        slider.ValueChanged += (_, _) => valueText.Text = slider.Value.ToString(tick < 1 ? "0.0" : "0");
        panel.Children.Add(slider);
        return (panel, slider);
    }

    private ComboBox CreateEditorEffectColorBox(string selected)
    {
        var box = new ComboBox { Height = 34 };
        foreach (string color in new[] { "Black", "White", "Blue", "Yellow", "Green", "Purple", "Orange", "Red" })
            box.Items.Add(color);
        box.SelectedItem = selected;
        return box;
    }

    private FrameworkElement WrapEditorToolPanel(UIElement child)
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        scroll.Content = child;
        return scroll;
    }

    private TextBlock EditorHelpText(string text)
        => new()
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

    private TextBlock EditorSubtleNote(string text)
        => new()
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };

    private void ShowEditorToolPanel(string key, bool forceOpen)
    {
        if (_editorToolPanelHost is null || _editorToolPanelColumn is null || _editorToolGapColumn is null ||
            _editorToolPanelContent is null || _editorToolPanelTitle is null || !_editorToolPanels.TryGetValue(key, out FrameworkElement? panel))
            return;

        if (!forceOpen && string.Equals(_editorActiveToolKey, key, StringComparison.OrdinalIgnoreCase) && _editorToolPanelHost.Visibility == Visibility.Visible)
        {
            CloseEditorToolPanel();
            return;
        }

        _editorActiveToolKey = key;
        _editorLastToolKeyV072 = key;
        _editorToolPanelColumn.MinWidth = 220;
        _editorToolPanelColumn.Width = new GridLength(300);
        _editorToolGapColumn.Width = new GridLength(12);
        _editorToolPanelHost.Visibility = Visibility.Visible;
        _editorToolPanelContent.Content = panel;
        _editorToolPanelTitle.Text = key switch
        {
            "chat" => "Chat & Font",
            "colors" => "Line Colors",
            "effects" => "Text Effects",
            "image" => "Image & Canvas",
            "markup" => "Paint & Markup",
            "layer-paint" => "Layer Paint & Erase",
            "export" => "Export",
            _ => "Editor"
        };

        Brush accent = (Brush)FindResource("Accent");
        Brush raised = (Brush)FindResource("Raised");
        foreach ((string buttonKey, Button button) in _editorToolButtons)
            button.Background = string.Equals(buttonKey, key, StringComparison.OrdinalIgnoreCase) ? accent : raised;
        UpdateEditorSidebarToggleStateV072();
    }

    private void CloseEditorToolPanel()
    {
        if (_editorToolPanelHost is null || _editorToolPanelColumn is null || _editorToolGapColumn is null) return;
        _editorToolPanelHost.Visibility = Visibility.Collapsed;
        _editorToolPanelColumn.MinWidth = 0;
        _editorToolPanelColumn.Width = new GridLength(0);
        _editorToolGapColumn.Width = new GridLength(0);
        _editorActiveToolKey = null;
        Brush raised = (Brush)FindResource("Raised");
        foreach (Button button in _editorToolButtons.Values) button.Background = raised;
        UpdateEditorSidebarToggleStateV072();
    }

    private void ChangeEditorZoom(double delta)
    {
        _editorFitZoom = false;
        SetEditorZoom(_editorZoomScale + delta);
    }

    private void SetEditorZoom(double scale)
    {
        _editorZoomScale = Math.Clamp(scale, 0.10, 4.0);
        if (_editorZoomHost is not null)
            _editorZoomHost.LayoutTransform = new ScaleTransform(_editorZoomScale, _editorZoomScale);
        if (_editorZoomText is not null)
            _editorZoomText.Text = $"{Math.Round(_editorZoomScale * 100):0}%";
        RefreshEditorRulersV068();
    }

    private void FitEditorPreviewToWindow()
    {
        if (_editorPreviewScroll is null || _editorComposition is null) return;
        _editorPreviewScroll.UpdateLayout();
        double availableWidth = _editorPreviewScroll.ViewportWidth;
        double availableHeight = _editorPreviewScroll.ViewportHeight;
        if (!double.IsFinite(availableWidth) || availableWidth <= 1)
            availableWidth = _editorPreviewScroll.ActualWidth;
        if (!double.IsFinite(availableHeight) || availableHeight <= 1)
            availableHeight = _editorPreviewScroll.ActualHeight;

        double compositionWidth = double.IsFinite(_editorComposition.Width) && _editorComposition.Width > 0
            ? _editorComposition.Width
            : _editorComposition.ActualWidth;
        double compositionHeight = double.IsFinite(_editorComposition.Height) && _editorComposition.Height > 0
            ? _editorComposition.Height
            : _editorComposition.ActualHeight;
        if (availableWidth <= 1 || availableHeight <= 1 || compositionWidth <= 0 || compositionHeight <= 0)
            return;

        // Leave a sliver of breathing room so a rounding error cannot create
        // scrollbars and make Fit appear to have failed.
        double fit = Math.Min(
            Math.Max(1, availableWidth - 4) / compositionWidth,
            Math.Max(1, availableHeight - 4) / compositionHeight);
        _editorFitZoom = true;
        SetEditorZoom(Math.Clamp(fit, 0.10, 4.0));
        if (_editorZoomHost is not null)
        {
            _editorZoomHost.HorizontalAlignment = HorizontalAlignment.Center;
            _editorZoomHost.VerticalAlignment = VerticalAlignment.Center;
        }
        _editorPreviewScroll.ScrollToHorizontalOffset(0);
        _editorPreviewScroll.ScrollToVerticalOffset(0);
        _ = Dispatcher.BeginInvoke(new Action(RefreshEditorRulersV068));
    }

    private bool _editorFitScheduledV073;

    private void ScheduleEditorFitV073()
    {
        if (_editorFitScheduledV073) return;
        _editorFitScheduledV073 = true;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            _editorFitScheduledV073 = false;
            if (!_editorFitZoom || _editorPreviewScroll is null) return;
            _editorPreviewScroll.UpdateLayout();
            FitEditorPreviewToWindow();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void EditorPreviewScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        ChangeEditorZoom(e.Delta > 0 ? 0.1 : -0.1);
        e.Handled = true;
    }

    private void EditorFullscreenPreview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bitmap = CaptureEditorCompositeBitmap();
            if (bitmap is null) return;
            var fullscreen = new EditorFullscreenWindow(bitmap) { Owner = this };
            fullscreen.ShowDialog();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open Editor full screen preview.", ex);
            SetEditorStatus("Full screen preview could not be opened.");
        }
    }

}
