using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

internal sealed class EditorBaseSizeWindowV081 : Window
{
    internal const int MinimumBaseWidth = 1920;
    internal const int MinimumBaseHeight = 1080;
    private const int MaximumBaseDimension = 12000;

    private sealed record BaseSizePreset(string Name, int Width, int Height)
    {
        public override string ToString() => $"{Name} · {Width:N0} × {Height:N0}";
    }

    private static readonly BaseSizePreset[] CommonMonitorSizes =
    {
        new("Full HD (1080p)", 1920, 1080),
        new("UltraWide Full HD", 2560, 1080),
        new("QHD (1440p)", 2560, 1440),
        new("QHD 16:10", 2560, 1600),
        new("UltraWide QHD", 3440, 1440),
        new("4K UHD", 3840, 2160),
        new("Super UltraWide QHD", 5120, 1440),
        new("5K", 5120, 2880),
        new("8K UHD", 7680, 4320)
    };

    private readonly int _currentWidth;
    private readonly int _currentHeight;
    private readonly RadioButton _current;
    private readonly RadioButton _preset;
    private readonly RadioButton _custom;
    private readonly ComboBox _presetBox;
    private readonly TextBox _width;
    private readonly TextBox _height;
    private readonly TextBlock _validation;

    public int ImageWidth { get; private set; }
    public int ImageHeight { get; private set; }

    public EditorBaseSizeWindowV081(Window owner, double currentWidth, double currentHeight)
    {
        Owner = owner;
        Title = "Set as Base Image";
        Width = 520;
        Height = 475;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        (_currentWidth, _currentHeight) = NormalizeCurrentSize(currentWidth, currentHeight);

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = "Choose the Base Image size",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = "The selected layer becomes the new export boundary. Base Images are at least 1,920 × 1,080px; smaller layers are scaled proportionally. The previous Base Image is preserved as an ordinary layer.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 16)
        });

        _current = new RadioButton
        {
            Content = $"Use current proportions · {_currentWidth:N0} × {_currentHeight:N0}px",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _preset = new RadioButton { Content = "Use a common monitor size", Margin = new Thickness(0, 0, 0, 7) };
        _presetBox = new ComboBox
        {
            ItemsSource = CommonMonitorSizes,
            SelectedIndex = 0,
            Height = 34,
            Margin = new Thickness(22, 0, 0, 12)
        };
        _custom = new RadioButton { Content = "Use a specific pixel size" };
        root.Children.Add(_current);
        root.Children.Add(_preset);
        root.Children.Add(_presetBox);
        root.Children.Add(_custom);

        var dimensions = new Grid { Margin = new Thickness(22, 9, 0, 0) };
        dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _width = CreateDimensionBox(_currentWidth);
        _height = CreateDimensionBox(_currentHeight);
        dimensions.Children.Add(CreateField("Width", _width));
        FrameworkElement heightField = CreateField("Height", _height);
        Grid.SetColumn(heightField, 2);
        dimensions.Children.Add(heightField);
        root.Children.Add(dimensions);

        _validation = new TextBlock
        {
            Foreground = (Brush)FindResource("Warning"),
            FontSize = 10.5,
            Margin = new Thickness(22, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(_validation);
        _presetBox.SelectionChanged += (_, _) => _validation.Text = string.Empty;

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(13, 7, 13, 7) };
        cancel.Click += (_, _) => Close();
        var apply = new Button
        {
            Content = "Set as Base Image",
            Padding = new Thickness(13, 7, 13, 7),
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource("PrimaryButton")
        };
        apply.Click += (_, _) => Confirm();
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        root.Children.Add(actions);

        _current.Checked += (_, _) => UpdateFields();
        _preset.Checked += (_, _) => UpdateFields();
        _custom.Checked += (_, _) => UpdateFields();
        _width.TextChanged += (_, _) => _validation.Text = string.Empty;
        _height.TextChanged += (_, _) => _validation.Text = string.Empty;
        Content = root;
        ThemeService.ApplyWindow(this);
        UpdateFields();
    }

    private void Confirm()
    {
        if (_current.IsChecked == true)
        {
            ImageWidth = _currentWidth;
            ImageHeight = _currentHeight;
            DialogResult = true;
            return;
        }

        if (_preset.IsChecked == true && _presetBox.SelectedItem is BaseSizePreset preset)
        {
            ImageWidth = preset.Width;
            ImageHeight = preset.Height;
            DialogResult = true;
            return;
        }

        if (!int.TryParse(_width.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(_height.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) ||
            width is < MinimumBaseWidth or > MaximumBaseDimension ||
            height is < MinimumBaseHeight or > MaximumBaseDimension)
        {
            _validation.Text = "Enter a width from 1,920 to 12,000 and a height from 1,080 to 12,000 pixels.";
            return;
        }
        ImageWidth = width;
        ImageHeight = height;
        DialogResult = true;
    }

    private void UpdateFields()
    {
        bool customEnabled = _custom.IsChecked == true;
        _presetBox.IsEnabled = _preset.IsChecked == true;
        _width.IsEnabled = customEnabled;
        _height.IsEnabled = customEnabled;
    }

    private static (int Width, int Height) NormalizeCurrentSize(double width, double height)
    {
        double sourceWidth = Math.Clamp(width, 1, MaximumBaseDimension);
        double sourceHeight = Math.Clamp(height, 1, MaximumBaseDimension);
        double scale = Math.Max(1, Math.Max(
            MinimumBaseWidth / sourceWidth,
            MinimumBaseHeight / sourceHeight));
        int normalizedWidth = Math.Clamp((int)Math.Round(sourceWidth * scale), MinimumBaseWidth, MaximumBaseDimension);
        int normalizedHeight = Math.Clamp((int)Math.Round(sourceHeight * scale), MinimumBaseHeight, MaximumBaseDimension);
        return (normalizedWidth, normalizedHeight);
    }

    private static TextBox CreateDimensionBox(int value)
        => new()
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            Height = 32,
            Padding = new Thickness(7, 5, 7, 5)
        };

    private FrameworkElement CreateField(string label, TextBox box)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(box);
        return stack;
    }
}
