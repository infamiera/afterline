using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Afterline;

internal enum EditorBackgroundRemovalFill
{
    Transparent,
    Black,
    White
}

internal sealed class EditorBackgroundRemovalWindow : Window
{
    private readonly BitmapSource _source;
    private readonly BitmapSource _previewSource;
    private readonly Image _preview;
    private readonly Slider _tolerance;
    private readonly Slider _feather;
    private readonly ComboBox? _fill;
    private readonly TextBlock _status;
    private readonly Button _apply;
    private readonly DispatcherTimer _previewTimer;
    private CancellationTokenSource? _previewCancellation;

    public BitmapSource? Result { get; private set; }
    public EditorBackgroundRemovalFill SelectedFill => ResolveFill();

    public EditorBackgroundRemovalWindow(Window owner, BitmapSource source, bool allowSolidFill)
    {
        Owner = owner;
        Title = "Remove Background";
        Width = 920;
        Height = 720;
        MinWidth = 700;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _source = EditorBackgroundRemovalProcessor.CloneFrozen(source);
        _previewSource = CreatePreviewSource(_source, 960, 600);

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Background removal preview",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Afterline removes only background-colored pixels connected to an image edge. Adjust the controls, inspect the preview, then apply.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        root.Children.Add(heading);

        _preview = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        var previewHost = new Border
        {
            Background = CreateCheckerboardBrush(),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(8),
            Child = _preview
        };
        Grid.SetRow(previewHost, 2);
        root.Children.Add(previewHost);

        var controls = new Grid();
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _tolerance = CreateSlider(8, 180, 54, 1);
        controls.Children.Add(CreateControlGroup("Color tolerance", _tolerance,
            "Higher values include a wider range of edge-connected background colors."));

        _feather = CreateSlider(0, 32, 8, 1);
        FrameworkElement featherGroup = CreateControlGroup("Edge feather", _feather,
            "Softens the transition around the retained subject.");
        Grid.SetColumn(featherGroup, 2);
        controls.Children.Add(featherGroup);

        if (allowSolidFill)
        {
            _fill = new ComboBox { MinWidth = 125, Height = 32 };
            _fill.Items.Add("Transparent");
            _fill.Items.Add("Black");
            _fill.Items.Add("White");
            _fill.SelectedIndex = 0;
            _fill.SelectionChanged += (_, _) => SchedulePreview();
            FrameworkElement fillGroup = CreateControlGroup("Removed area", _fill,
                "Transparent preserves PNG alpha; black and white create an opaque Base Image.");
            Grid.SetColumn(fillGroup, 4);
            controls.Children.Add(fillGroup);
        }

        var footer = new StackPanel();
        footer.Children.Add(controls);

        var footerRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status = new TextBlock
        {
            Text = "Preparing preview…",
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        footerRow.Children.Add(_status);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(13, 7, 13, 7) };
        cancel.Click += (_, _) => Close();
        _apply = new Button
        {
            Content = "Apply Background Removal",
            Padding = new Thickness(13, 7, 13, 7),
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource("PrimaryButton")
        };
        _apply.Click += Apply_Click;
        actions.Children.Add(cancel);
        actions.Children.Add(_apply);
        Grid.SetColumn(actions, 1);
        footerRow.Children.Add(actions);
        footer.Children.Add(footerRow);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _previewTimer.Tick += async (_, _) =>
        {
            _previewTimer.Stop();
            await RefreshPreviewAsync();
        };
        _tolerance.ValueChanged += (_, _) => SchedulePreview();
        _feather.ValueChanged += (_, _) => SchedulePreview();
        Closed += (_, _) => _previewCancellation?.Cancel();
        Loaded += (_, _) => SchedulePreview(immediate: true);
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        _previewCancellation?.Cancel();
        _previewTimer.Stop();
        _apply.IsEnabled = false;
        _status.Text = "Applying to the full-resolution image…";
        try
        {
            int tolerance = (int)Math.Round(_tolerance.Value);
            int feather = (int)Math.Round(_feather.Value);
            EditorBackgroundRemovalFill fill = ResolveFill();
            Result = await Task.Run(() => EditorBackgroundRemovalProcessor.Remove(
                _source,
                tolerance,
                feather,
                fill,
                CancellationToken.None));
            DialogResult = true;
        }
        catch (Exception ex)
        {
            _apply.IsEnabled = true;
            _status.Text = "Background removal failed. The original image was not changed.";
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to remove background", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SchedulePreview(bool immediate = false)
    {
        _previewTimer.Stop();
        _previewTimer.Interval = immediate ? TimeSpan.FromMilliseconds(1) : TimeSpan.FromMilliseconds(180);
        _previewTimer.Start();
    }

    private async Task RefreshPreviewAsync()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = new CancellationTokenSource();
        CancellationToken token = _previewCancellation.Token;
        _status.Text = "Updating preview…";
        try
        {
            BitmapSource bitmap = await Task.Run(() => EditorBackgroundRemovalProcessor.Remove(
                _previewSource,
                (int)Math.Round(_tolerance.Value),
                (int)Math.Round(_feather.Value),
                ResolveFill(),
                token), token);
            if (token.IsCancellationRequested) return;
            _preview.Source = bitmap;
            _status.Text = $"Preview · {_source.PixelWidth:N0} × {_source.PixelHeight:N0}px source. Nothing changes until Apply is clicked.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _status.Text = "Preview unavailable. The original image remains untouched.";
            Afterline.Services.DiagnosticLogger.Error("Unable to preview Editor background removal.", ex);
        }
    }

    private EditorBackgroundRemovalFill ResolveFill()
        => (_fill?.SelectedItem?.ToString() ?? "Transparent") switch
        {
            "Black" => EditorBackgroundRemovalFill.Black,
            "White" => EditorBackgroundRemovalFill.White,
            _ => EditorBackgroundRemovalFill.Transparent
        };

    private static Slider CreateSlider(double minimum, double maximum, double value, double tick)
        => new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            TickFrequency = tick,
            IsSnapToTickEnabled = true,
            MinWidth = 190
        };

    private FrameworkElement CreateControlGroup(string title, Control control, string help)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        control.Margin = new Thickness(0, 5, 0, 0);
        stack.Children.Add(control);
        stack.Children.Add(new TextBlock
        {
            Text = help,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
            MaxWidth = 260
        });
        return stack;
    }

    private static BitmapSource CreatePreviewSource(BitmapSource source, int maxWidth, int maxHeight)
    {
        double scale = Math.Min(1, Math.Min(maxWidth / (double)source.PixelWidth, maxHeight / (double)source.PixelHeight));
        if (scale >= 0.999) return source;
        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }

    private static Brush CreateCheckerboardBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0xD9, 0xDD, 0xE2)),
            null,
            new RectangleGeometry(new Rect(0, 0, 20, 20))));
        var dark = new SolidColorBrush(Color.FromRgb(0xB8, 0xBE, 0xC6));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 10, 10))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(10, 10, 10, 10))));
        group.Freeze();
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 20, 20),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }
}

internal static class EditorBackgroundRemovalProcessor
{
    public static BitmapSource Remove(
        BitmapSource source,
        int tolerance,
        int feather,
        EditorBackgroundRemovalFill fill,
        CancellationToken cancellationToken)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);

        (byte targetB, byte targetG, byte targetR) = EstimateEdgeColor(pixels, width, height, stride);
        int hard = Math.Clamp(tolerance, 1, 255);
        int soft = Math.Clamp(feather, 0, 64);
        int connectedLimit = Math.Min(441, hard + soft + 18);
        int connectedLimitSquared = connectedLimit * connectedLimit;
        byte[] connected = new byte[checked(width * height)];
        int[] queue = new int[connected.Length];
        int head = 0;
        int tail = 0;

        void Seed(int x, int y)
        {
            int pixel = y * width + x;
            if (connected[pixel] != 0) return;
            int offset = y * stride + x * 4;
            if (ColorDistanceSquared(pixels[offset], pixels[offset + 1], pixels[offset + 2], targetB, targetG, targetR) > connectedLimitSquared)
                return;
            connected[pixel] = 1;
            queue[tail++] = pixel;
        }

        for (int x = 0; x < width; x++)
        {
            Seed(x, 0);
            if (height > 1) Seed(x, height - 1);
        }
        for (int y = 1; y < height - 1; y++)
        {
            Seed(0, y);
            if (width > 1) Seed(width - 1, y);
        }

        int checks = 0;
        while (head < tail)
        {
            if ((checks++ & 0x3FFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            int pixel = queue[head++];
            int x = pixel % width;
            int y = pixel / width;
            TryVisit(x - 1, y);
            TryVisit(x + 1, y);
            TryVisit(x, y - 1);
            TryVisit(x, y + 1);
        }

        void TryVisit(int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
            int pixel = y * width + x;
            if (connected[pixel] != 0) return;
            int offset = y * stride + x * 4;
            if (ColorDistanceSquared(pixels[offset], pixels[offset + 1], pixels[offset + 2], targetB, targetG, targetR) > connectedLimitSquared)
                return;
            connected[pixel] = 1;
            queue[tail++] = pixel;
        }

        int hardSquared = hard * hard;
        int softLimit = hard + soft;
        int softSquared = softLimit * softLimit;
        for (int pixel = 0; pixel < connected.Length; pixel++)
        {
            if (connected[pixel] == 0) continue;
            if ((pixel & 0x7FFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            int offset = pixel * 4;
            int distanceSquared = ColorDistanceSquared(
                pixels[offset], pixels[offset + 1], pixels[offset + 2], targetB, targetG, targetR);
            double retain = distanceSquared <= hardSquared
                ? 0
                : soft <= 0 || distanceSquared >= softSquared
                    ? 1
                    : (Math.Sqrt(distanceSquared) - hard) / soft;
            byte originalAlpha = pixels[offset + 3];
            byte alpha = (byte)Math.Clamp(Math.Round(originalAlpha * retain), 0, 255);

            if (fill == EditorBackgroundRemovalFill.Transparent)
            {
                pixels[offset + 3] = alpha;
                continue;
            }

            byte fillValue = fill == EditorBackgroundRemovalFill.White ? (byte)255 : (byte)0;
            double foreground = alpha / 255.0;
            pixels[offset] = (byte)Math.Round(pixels[offset] * foreground + fillValue * (1 - foreground));
            pixels[offset + 1] = (byte)Math.Round(pixels[offset + 1] * foreground + fillValue * (1 - foreground));
            pixels[offset + 2] = (byte)Math.Round(pixels[offset + 2] * foreground + fillValue * (1 - foreground));
            pixels[offset + 3] = 255;
        }

        if (fill != EditorBackgroundRemovalFill.Transparent)
        {
            byte fillValue = fill == EditorBackgroundRemovalFill.White ? (byte)255 : (byte)0;
            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                double foreground = pixels[offset + 3] / 255.0;
                pixels[offset] = (byte)Math.Round(pixels[offset] * foreground + fillValue * (1 - foreground));
                pixels[offset + 1] = (byte)Math.Round(pixels[offset + 1] * foreground + fillValue * (1 - foreground));
                pixels[offset + 2] = (byte)Math.Round(pixels[offset + 2] * foreground + fillValue * (1 - foreground));
                pixels[offset + 3] = 255;
            }
        }

        BitmapSource result = BitmapSource.Create(
            width,
            height,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    public static BitmapSource CloneFrozen(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = checked(converted.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        BitmapSource copy = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        copy.Freeze();
        return copy;
    }

    private static (byte B, byte G, byte R) EstimateEdgeColor(byte[] pixels, int width, int height, int stride)
    {
        int sampleBudget = 2048;
        int perimeter = Math.Max(1, width * 2 + Math.Max(0, height - 2) * 2);
        int step = Math.Max(1, perimeter / sampleBudget);
        var blues = new List<byte>();
        var greens = new List<byte>();
        var reds = new List<byte>();
        int cursor = 0;

        void Add(int x, int y)
        {
            if ((cursor++ % step) != 0) return;
            int offset = y * stride + x * 4;
            if (pixels[offset + 3] < 8) return;
            blues.Add(pixels[offset]);
            greens.Add(pixels[offset + 1]);
            reds.Add(pixels[offset + 2]);
        }

        for (int x = 0; x < width; x++) Add(x, 0);
        for (int y = 1; y < height; y++) Add(width - 1, y);
        if (height > 1) for (int x = width - 2; x >= 0; x--) Add(x, height - 1);
        if (width > 1) for (int y = height - 2; y > 0; y--) Add(0, y);

        if (blues.Count == 0) return (0, 0, 0);
        blues.Sort();
        greens.Sort();
        reds.Sort();
        int middle = blues.Count / 2;
        return (blues[middle], greens[middle], reds[middle]);
    }

    private static int ColorDistanceSquared(byte b, byte g, byte r, byte targetB, byte targetG, byte targetR)
    {
        int db = b - targetB;
        int dg = g - targetG;
        int dr = r - targetR;
        return db * db + dg * dg + dr * dr;
    }
}
