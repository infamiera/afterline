using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public sealed class TextPromptWindow : Window
{
    private readonly TextBox _input;
    public string Value => _input.Text.Trim();

    public TextPromptWindow(string title, string prompt, string initialValue = "")
    {
        Title = title;
        Width = 500;
        Height = 260;
        MinWidth = 420;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = (Brush)System.Windows.Application.Current.FindResource("Bg");
        Foreground = (Brush)System.Windows.Application.Current.FindResource("Text");

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = prompt,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)System.Windows.Application.Current.FindResource("MutedText")
        };
        root.Children.Add(label);

        _input = new TextBox
        {
            Text = initialValue,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10)
        };
        Grid.SetRow(_input, 2);
        root.Children.Add(_input);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var save = new Button
        {
            Content = "Save",
            Padding = new Thickness(14, 7, 14, 7),
            Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton")
        };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_input.Text)) return;
            DialogResult = true;
            Close();
        };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = root;
        Loaded += (_, _) => { _input.Focus(); _input.CaretIndex = _input.Text.Length; };
    }
}
