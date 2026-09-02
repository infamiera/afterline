using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

internal sealed class EditorBaseSizeWindowV081 : Window
{
    private readonly int _currentWidth;
    private readonly int _currentHeight;
    private readonly RadioButton _current;
    private readonly RadioButton _custom;
    private readonly TextBox _width;
    private readonly TextBox _height;
    private readonly TextBlock _validation;

    public int ImageWidth { get; private set; }
    public int ImageHeight { get; private set; }

    public EditorBaseSizeWindowV081(Window owner, double currentWidth, double currentHeight)
    {
        Owner = owner;
        Title = "Set as Base Image";
        Width = 480;
        Height = 360;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _currentWidth = Math.Clamp((int)Math.Round(currentWidth), 1, 12000);
        _currentHeight = Math.Clamp((int)Math.Round(currentHeight), 1, 12000);

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = "Choose the Base Image size",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = "The selected layer becomes the new export boundary. The previous Base Image is preserved as an ordinary layer.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 16)
        });

        _current = new RadioButton
        {
            Content = $"Use current displayed size · {_currentWidth:N0} × {_currentHeight:N0}px",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _custom = new RadioButton { Content = "Use a specific pixel size" };
        root.Children.Add(_current);
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

        if (!int.TryParse(_width.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(_height.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) ||
            width is < 1 or > 12000 || height is < 1 or > 12000)
        {
            _validation.Text = "Enter whole pixel dimensions from 1 to 12,000.";
            return;
        }
        ImageWidth = width;
        ImageHeight = height;
        DialogResult = true;
    }

    private void UpdateFields()
    {
        bool enabled = _custom.IsChecked == true;
        _width.IsEnabled = enabled;
        _height.IsEnabled = enabled;
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
