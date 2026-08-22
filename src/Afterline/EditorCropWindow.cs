using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

internal sealed class EditorCropWindow : Window
{
    private readonly BitmapSource _source;
    private readonly Canvas _canvas;
    private readonly Image _image;
    private readonly Slider _zoomSlider;
    private readonly TextBlock _statusText;
    private readonly double _frameWidth;
    private readonly double _frameHeight;
    private readonly Rect _initialCrop;

    private double _baseScale;
    private double _imageLeft;
    private double _imageTop;
    private Point _dragStart;
    private double _dragStartLeft;
    private double _dragStartTop;
    private bool _dragging;
    private bool _initializing;

    public Rect CropNormalized { get; private set; }
    public bool Saved { get; private set; }

    public EditorCropWindow(
        Window owner,
        BitmapSource source,
        Rect currentCrop,
        int outputWidth,
        int outputHeight)
    {
        Owner = owner;
        Title = "Crop RP Screenshot";
        Width = 720;
        Height = 650;
        MinWidth = 620;
        MinHeight = 570;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _source = source;
        _initialCrop = NormalizeCrop(currentCrop);
        CropNormalized = _initialCrop;

        double outputRatio = Math.Max(1, outputWidth) / (double)Math.Max(1, outputHeight);
        const double maxWidth = 560;
        const double maxHeight = 380;
        if (outputRatio >= maxWidth / maxHeight)
        {
            _frameWidth = maxWidth;
            _frameHeight = Math.Max(110, maxWidth / outputRatio);
        }
        else
        {
            _frameHeight = maxHeight;
            _frameWidth = Math.Max(110, maxHeight * outputRatio);
        }

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Adjust crop",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Drag to reposition · zoom to tighten the frame · output {outputWidth:N0} × {outputHeight:N0}px",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var cropFrame = new Grid
        {
            Width = _frameWidth,
            Height = _frameHeight,
            ClipToBounds = true,
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _canvas = new Canvas
        {
            Width = _frameWidth,
            Height = _frameHeight,
            ClipToBounds = true,
            Cursor = Cursors.SizeAll
        };

        _image = new Image
        {
            Source = _source,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true
        };
        _canvas.Children.Add(_image);
        cropFrame.Children.Add(_canvas);
        cropFrame.Children.Add(new Border
        {
            Width = _frameWidth,
            Height = _frameHeight,
            BorderBrush = (Brush)FindResource("Accent"),
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false
        });

        _canvas.PreviewMouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        _canvas.PreviewMouseLeftButtonUp += Canvas_MouseLeftButtonUp;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeave += (_, _) => EndDrag();
        body.Children.Add(cropFrame);

        var controls = new Grid
        {
            Width = Math.Max(360, _frameWidth),
            Margin = new Thickness(0, 16, 0, 0)
        };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.Children.Add(new TextBlock
        {
            Text = "Zoom",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        _zoomSlider = new Slider
        {
            Minimum = 1,
            Maximum = 8,
            Value = 1,
            TickFrequency = 0.1,
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        _zoomSlider.ValueChanged += (_, _) => UpdateImageLayout(true);
        Grid.SetColumn(_zoomSlider, 2);
        controls.Children.Add(_zoomSlider);

        var reset = new Button
        {
            Content = "Center",
            Padding = new Thickness(10, 6, 10, 6)
        };
        reset.Click += (_, _) => ResetCrop();
        Grid.SetColumn(reset, 4);
        controls.Children.Add(reset);
        body.Children.Add(controls);

        _statusText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 11, 0, 0)
        };
        body.Children.Add(_statusText);

        Grid.SetRow(body, 2);
        root.Children.Add(body);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Click += (_, _) => Close();
        footer.Children.Add(cancel);

        var apply = new Button
        {
            Content = "Apply crop",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(12, 7, 12, 7)
        };
        apply.Click += (_, _) => SaveCrop();
        footer.Children.Add(apply);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);
        Loaded += (_, _) => ApplyInitialCrop();
    }

    private void ApplyInitialCrop()
    {
        _initializing = true;
        _baseScale = Math.Max(_frameWidth / _source.PixelWidth, _frameHeight / _source.PixelHeight);

        Rect crop = NormalizeCrop(_initialCrop);
        double requestedScale = Math.Max(
            _frameWidth / Math.Max(1, _source.PixelWidth * crop.Width),
            _frameHeight / Math.Max(1, _source.PixelHeight * crop.Height));
        double zoom = Math.Clamp(requestedScale / _baseScale, 1, _zoomSlider.Maximum);
        _zoomSlider.Value = zoom;

        double scale = _baseScale * zoom;
        _image.Width = _source.PixelWidth * scale;
        _image.Height = _source.PixelHeight * scale;
        _imageLeft = -(crop.X * _source.PixelWidth * scale);
        _imageTop = -(crop.Y * _source.PixelHeight * scale);
        ClampPosition();
        Canvas.SetLeft(_image, _imageLeft);
        Canvas.SetTop(_image, _imageTop);
        _initializing = false;
        UpdateStatus();
    }

    private void ResetCrop()
    {
        _zoomSlider.Value = 1;
        _baseScale = Math.Max(_frameWidth / _source.PixelWidth, _frameHeight / _source.PixelHeight);
        double scale = _baseScale;
        _image.Width = _source.PixelWidth * scale;
        _image.Height = _source.PixelHeight * scale;
        _imageLeft = (_frameWidth - _image.Width) / 2;
        _imageTop = (_frameHeight - _image.Height) / 2;
        ClampPosition();
        Canvas.SetLeft(_image, _imageLeft);
        Canvas.SetTop(_image, _imageTop);
        UpdateStatus();
    }

    private void UpdateImageLayout(bool preserveCenter)
    {
        if (_initializing) return;
        if (_baseScale <= 0)
            _baseScale = Math.Max(_frameWidth / _source.PixelWidth, _frameHeight / _source.PixelHeight);

        double oldWidth = _image.Width > 0 ? _image.Width : _source.PixelWidth * _baseScale;
        double oldHeight = _image.Height > 0 ? _image.Height : _source.PixelHeight * _baseScale;
        double centerX = _imageLeft + oldWidth / 2;
        double centerY = _imageTop + oldHeight / 2;

        double scale = _baseScale * _zoomSlider.Value;
        double width = _source.PixelWidth * scale;
        double height = _source.PixelHeight * scale;

        if (!preserveCenter || double.IsNaN(_image.Width))
        {
            _imageLeft = (_frameWidth - width) / 2;
            _imageTop = (_frameHeight - height) / 2;
        }
        else
        {
            _imageLeft = centerX - width / 2;
            _imageTop = centerY - height / 2;
        }

        _image.Width = width;
        _image.Height = height;
        ClampPosition();
        Canvas.SetLeft(_image, _imageLeft);
        Canvas.SetTop(_image, _imageTop);
        UpdateStatus();
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStart = e.GetPosition(_canvas);
        _dragStartLeft = _imageLeft;
        _dragStartTop = _imageTop;
        _canvas.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        Point current = e.GetPosition(_canvas);
        _imageLeft = _dragStartLeft + current.X - _dragStart.X;
        _imageTop = _dragStartTop + current.Y - _dragStart.Y;
        ClampPosition();
        Canvas.SetLeft(_image, _imageLeft);
        Canvas.SetTop(_image, _imageTop);
        UpdateStatus();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        if (_canvas.IsMouseCaptured) _canvas.ReleaseMouseCapture();
    }

    private void ClampPosition()
    {
        double width = _image.Width;
        double height = _image.Height;

        _imageLeft = width <= _frameWidth
            ? (_frameWidth - width) / 2
            : Math.Clamp(_imageLeft, _frameWidth - width, 0);
        _imageTop = height <= _frameHeight
            ? (_frameHeight - height) / 2
            : Math.Clamp(_imageTop, _frameHeight - height, 0);
    }

    private void UpdateStatus()
    {
        Rect crop = CalculateNormalizedCrop();
        int sourceWidth = Math.Max(1, (int)Math.Round(_source.PixelWidth * crop.Width));
        int sourceHeight = Math.Max(1, (int)Math.Round(_source.PixelHeight * crop.Height));
        _statusText.Text = $"Source crop ≈ {sourceWidth:N0} × {sourceHeight:N0}px · zoom {_zoomSlider.Value:0.00}×";
    }

    private void SaveCrop()
    {
        CropNormalized = CalculateNormalizedCrop();
        Saved = true;
        DialogResult = true;
        Close();
    }

    private Rect CalculateNormalizedCrop()
    {
        double scale = _image.Width / Math.Max(1, _source.PixelWidth);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
            return new Rect(0, 0, 1, 1);

        double x = (-_imageLeft / scale) / _source.PixelWidth;
        double y = (-_imageTop / scale) / _source.PixelHeight;
        double width = (_frameWidth / scale) / _source.PixelWidth;
        double height = (_frameHeight / scale) / _source.PixelHeight;
        return NormalizeCrop(new Rect(x, y, width, height));
    }

    private static Rect NormalizeCrop(Rect value)
    {
        double width = Math.Clamp(value.Width, 0.0001, 1);
        double height = Math.Clamp(value.Height, 0.0001, 1);
        double x = Math.Clamp(value.X, 0, Math.Max(0, 1 - width));
        double y = Math.Clamp(value.Y, 0, Math.Max(0, 1 - height));
        return new Rect(x, y, width, height);
    }
}
