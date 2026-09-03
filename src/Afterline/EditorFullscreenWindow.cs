using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Afterline;

internal sealed class EditorFullscreenWindow : Window
{
    private readonly BitmapSource _source;
    private readonly ScrollViewer _scroll;
    private readonly Border _zoomHost;
    private readonly TextBlock _zoomText;
    private double _zoom = 1.0;
    private bool _fitMode = true;

    public EditorFullscreenWindow(BitmapSource source)
    {
        _source = source;
        Title = "Editor Preview";
        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SetResourceReference(Control.BackgroundProperty, "AfterlineAppGradient");
        SetResourceReference(Control.ForegroundProperty, "Text");
        KeyDown += OnKeyDown;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 12, 10)
        };
        topBar.SetResourceReference(Border.BackgroundProperty, "AfterlineHeaderGradient");
        topBar.SetResourceReference(Border.BorderBrushProperty, "Border");
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = "Full Screen Preview", FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        label.Children.Add(new TextBlock
        {
            Text = "  ·  ESC to close  ·  Ctrl + mouse wheel to zoom",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        topGrid.Children.Add(label);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(CreateButton("−", "Zoom out", (_, _) => ChangeZoom(-0.1)));
        _zoomText = new TextBlock
        {
            Text = "100%",
            Width = 54,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Cursor = Cursors.Hand,
            ToolTip = "Reset zoom to 100%"
        };
        _zoomText.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        _zoomText.MouseLeftButtonDown += (_, args) =>
        {
            _fitMode = false;
            SetZoom(1.0);
            args.Handled = true;
        };
        actions.Children.Add(_zoomText);
        actions.Children.Add(CreateButton("+", "Zoom in", (_, _) => ChangeZoom(0.1)));
        actions.Children.Add(CreateButton("Fit", "Fit image", (_, _) => FitToWindow(), 46));
        var close = CreateButton("×", "Close full screen", (_, _) => Close(), 38);
        close.FontSize = 19;
        close.Margin = new Thickness(10, 0, 0, 0);
        actions.Children.Add(close);
        Grid.SetColumn(actions, 1);
        topGrid.Children.Add(actions);
        topBar.Child = topGrid;
        root.Children.Add(topBar);

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(24),
            CanContentScroll = false
        };
        _scroll.SetResourceReference(Control.BackgroundProperty, "AfterlineInset");
        _scroll.PreviewMouseWheel += OnPreviewMouseWheel;
        _scroll.SizeChanged += (_, _) =>
        {
            if (_fitMode) _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitToWindow));
        };
        Grid.SetRow(_scroll, 1);

        var image = new Image
        {
            Source = _source,
            Width = _source.PixelWidth,
            Height = _source.PixelHeight,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _zoomHost = new Border
        {
            Child = image,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            LayoutTransform = new ScaleTransform(1, 1)
        };
        _scroll.Content = _zoomHost;
        root.Children.Add(_scroll);

        Content = root;
        Loaded += (_, _) =>
        {
            Focus();
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitToWindow));
        };
    }

    private Button CreateButton(string content, string toolTip, RoutedEventHandler handler, double width = 32)
    {
        var button = new Button
        {
            Content = content,
            Width = width,
            Height = 30,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = toolTip
        };
        button.SetResourceReference(Control.BackgroundProperty, "Raised");
        button.SetResourceReference(Control.ForegroundProperty, "Text");
        button.SetResourceReference(Control.BorderBrushProperty, "Border");
        button.Click += handler;
        return button;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        ChangeZoom(e.Delta > 0 ? 0.1 : -0.1);
        e.Handled = true;
    }

    private void ChangeZoom(double delta)
    {
        _fitMode = false;
        SetZoom(_zoom + delta);
    }

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, 0.10, 5.0);
        _zoomHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        _zoomText.Text = $"{Math.Round(_zoom * 100):0}%";
    }

    private void FitToWindow()
    {
        double availableWidth = _scroll.ViewportWidth - 48;
        double availableHeight = _scroll.ViewportHeight - 48;
        if (availableWidth <= 0 || availableHeight <= 0) return;
        double fit = Math.Min(availableWidth / _source.PixelWidth, availableHeight / _source.PixelHeight);
        _fitMode = true;
        SetZoom(Math.Clamp(fit, 0.10, 5.0));
        _scroll.ScrollToHorizontalOffset(0);
        _scroll.ScrollToVerticalOffset(0);
    }
}
