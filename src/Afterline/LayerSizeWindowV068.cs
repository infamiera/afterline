using System.Windows;
using System.Windows.Controls;
using Afterline.Services;

namespace Afterline;

internal sealed class LayerSizeWindowV068 : Window
{
    private readonly TextBox _widthBox;
    private readonly TextBox _heightBox;

    public double LayerWidth { get; private set; }
    public double LayerHeight { get; private set; }

    public LayerSizeWindowV068(Window owner, double width, double height)
    {
        Owner = owner;
        Title = "Set Image Layer Size";
        Width = 390;
        Height = 245;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Enter the exact displayed size for this image layer.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText")
        });

        var fields = new Grid();
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var widthStack = new StackPanel();
        widthStack.Children.Add(new TextBlock { Text = "Width (px)", Margin = new Thickness(0, 0, 0, 5) });
        _widthBox = new TextBox { Text = Math.Round(width).ToString("0"), Padding = new Thickness(8, 6, 8, 6) };
        widthStack.Children.Add(_widthBox);
        fields.Children.Add(widthStack);

        var heightStack = new StackPanel();
        heightStack.Children.Add(new TextBlock { Text = "Height (px)", Margin = new Thickness(0, 0, 0, 5) });
        _heightBox = new TextBox { Text = Math.Round(height).ToString("0"), Padding = new Thickness(8, 6, 8, 6) };
        heightStack.Children.Add(_heightBox);
        Grid.SetColumn(heightStack, 2);
        fields.Children.Add(heightStack);
        Grid.SetRow(fields, 2);
        root.Children.Add(fields);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(13, 6, 13, 6), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        var apply = new Button { Content = "Apply Size", Padding = new Thickness(13, 6, 13, 6) };
        apply.Click += (_, _) => Apply();
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
        Loaded += (_, _) =>
        {
            _widthBox.Focus();
            _widthBox.SelectAll();
        };
    }

    private void Apply()
    {
        if (!double.TryParse(_widthBox.Text, out double width) ||
            !double.TryParse(_heightBox.Text, out double height) ||
            width < 1 || height < 1 || width > 32768 || height > 32768)
        {
            System.Windows.MessageBox.Show(
                this,
                "Width and height must both be between 1 and 32,768 pixels.",
                "Invalid layer size",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        LayerWidth = Math.Round(width);
        LayerHeight = Math.Round(height);
        DialogResult = true;
    }
}
