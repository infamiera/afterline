using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

internal enum ChatExportFormat
{
    Text,
    Html
}

internal sealed class ChatExportFormatWindow : Window
{
    public ChatExportFormat? SelectedFormat { get; private set; }

    public ChatExportFormatWindow(string exportScope)
    {
        Title = "Export chat";
        Width = 440;
        Height = 220;
        MinWidth = 400;
        MinHeight = 200;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("Bg");
        Foreground = (Brush)Application.Current.FindResource("Text");

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = $"Export {exportScope} as:",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });

        var explanation = new TextBlock
        {
            Text = "TXT saves plain chat text. HTML creates a self-contained colored chatlog.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("MutedText")
        };
        Grid.SetRow(explanation, 2);
        root.Children.Add(explanation);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        cancel.Click += (_, _) => Close();
        var text = new Button
        {
            Content = "TXT",
            Padding = new Thickness(18, 7, 18, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        text.Click += (_, _) => Select(ChatExportFormat.Text);
        var html = new Button
        {
            Content = "HTML",
            Padding = new Thickness(18, 7, 18, 7),
            Style = (Style)Application.Current.FindResource("PrimaryButton")
        };
        html.Click += (_, _) => Select(ChatExportFormat.Html);

        actions.Children.Add(cancel);
        actions.Children.Add(text);
        actions.Children.Add(html);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);
        Content = root;
    }

    private void Select(ChatExportFormat format)
    {
        SelectedFormat = format;
        DialogResult = true;
        Close();
    }
}
