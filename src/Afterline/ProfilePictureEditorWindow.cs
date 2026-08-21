using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

internal sealed class ProfilePictureEditorWindow : Window
{
    private const double CropSize = 320;

    private readonly BitmapSource _source;
    private readonly Canvas _canvas;
    private readonly Image _image;
    private readonly Slider _zoomSlider;
    private readonly TextBlock _statusText;

    private double _baseScale;
    private double _imageLeft;
    private double _imageTop;
    private Point _dragStart;
    private double _dragStartLeft;
    private double _dragStartTop;
    private bool _dragging;

    public bool Saved { get; private set; }

    public ProfilePictureEditorWindow(Window owner, string sourcePath)
    {
        Owner = owner;
        Title = "Adjust Profile Picture";
        Width = 520;
        Height = 620;
        MinWidth = 480;
        MinHeight = 570;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _source = LoadSource(sourcePath);

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Adjust profile picture",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Drag the image to reposition it and use the zoom slider to frame the crop.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        var cropFrame = new Grid
        {
            Width = CropSize,
            Height = CropSize,
            ClipToBounds = true,
            Background = (Brush)FindResource("Raised")
        };

        _canvas = new Canvas
        {
            Width = CropSize,
            Height = CropSize,
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
            Width = CropSize,
            Height = CropSize,
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(CropSize / 2),
            IsHitTestVisible = false
        });

        _canvas.PreviewMouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        _canvas.PreviewMouseLeftButtonUp += Canvas_MouseLeftButtonUp;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeave += (_, _) => EndDrag();

        body.Children.Add(cropFrame);

        var controls = new Grid
        {
            Width = CropSize,
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
            Maximum = 4,
            Value = 1,
            TickFrequency = 0.1,
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("Accent"),
            Background = (Brush)FindResource("Border")
        };
        _zoomSlider.ValueChanged += (_, _) => UpdateImageLayout(true);
        Grid.SetColumn(_zoomSlider, 2);
        controls.Children.Add(_zoomSlider);

        var reset = new Button
        {
            Content = "Reset",
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
            Margin = new Thickness(0, 12, 0, 0)
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

        var save = new Button
        {
            Content = "Save picture",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(12, 7, 12, 7)
        };
        save.Click += (_, _) => SaveCrop();
        footer.Children.Add(save);

        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);
        ResetCrop();
    }

    private static BitmapSource LoadSource(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ResetCrop()
    {
        _zoomSlider.Value = 1;
        _baseScale = Math.Max(CropSize / _source.PixelWidth, CropSize / _source.PixelHeight);
        _imageLeft = 0;
        _imageTop = 0;
        UpdateImageLayout(false);
        _statusText.Text = "Drag to reposition · zoom up to 4×";
        _statusText.Foreground = (Brush)FindResource("MutedText");
    }

    private void UpdateImageLayout(bool preserveCenter)
    {
        if (_baseScale <= 0)
            _baseScale = Math.Max(CropSize / _source.PixelWidth, CropSize / _source.PixelHeight);

        double oldWidth = _image.Width > 0 ? _image.Width : _source.PixelWidth * _baseScale;
        double oldHeight = _image.Height > 0 ? _image.Height : _source.PixelHeight * _baseScale;
        double centerX = _imageLeft + oldWidth / 2;
        double centerY = _imageTop + oldHeight / 2;

        double scale = _baseScale * _zoomSlider.Value;
        double width = _source.PixelWidth * scale;
        double height = _source.PixelHeight * scale;

        if (!preserveCenter || double.IsNaN(_image.Width))
        {
            _imageLeft = (CropSize - width) / 2;
            _imageTop = (CropSize - height) / 2;
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
        _imageLeft = _dragStartLeft + (current.X - _dragStart.X);
        _imageTop = _dragStartTop + (current.Y - _dragStart.Y);
        ClampPosition();
        Canvas.SetLeft(_image, _imageLeft);
        Canvas.SetTop(_image, _imageTop);
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

        if (width <= CropSize)
            _imageLeft = (CropSize - width) / 2;
        else
            _imageLeft = Math.Clamp(_imageLeft, CropSize - width, 0);

        if (height <= CropSize)
            _imageTop = (CropSize - height) / 2;
        else
            _imageTop = Math.Clamp(_imageTop, CropSize - height, 0);
    }

    private void SaveCrop()
    {
        try
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, CropSize, CropSize)));
                dc.DrawImage(_source, new Rect(_imageLeft, _imageTop, _image.Width, _image.Height));
                dc.Pop();
            }

            var bitmap = new RenderTargetBitmap((int)CropSize, (int)CropSize, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            ProfilePictureService.Save(bitmap);
            Saved = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save the cropped profile picture.", ex);
            _statusText.Text = "Unable to save the profile picture.";
            _statusText.Foreground = (Brush)FindResource("Warning");
        }
    }
}
