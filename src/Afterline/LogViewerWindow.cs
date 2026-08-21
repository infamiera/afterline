using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Afterline;

public sealed class LogViewerWindow : Window
{
    private readonly ListBox _lines = new();

    public LogViewerWindow(string filePath, int? lineNumber = null)
    {
        Title = $"{Path.GetFileName(filePath)} — Afterline";
        Width = 1080;
        Height = 720;
        MinWidth = 760;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)System.Windows.Application.Current.FindResource("Bg");
        Foreground = (Brush)System.Windows.Application.Current.FindResource("Text");

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Border
        {
            Style = (Style)System.Windows.Application.Current.FindResource("CardStyle"),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = Path.GetFileName(filePath), FontSize = 18, FontWeight = FontWeights.SemiBold },
                    new TextBlock
                    {
                        Text = filePath,
                        Foreground = (Brush)System.Windows.Application.Current.FindResource("MutedText"),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
        root.Children.Add(header);

        string[] content;
        try { content = File.ReadAllLines(filePath); }
        catch (Exception ex)
        {
            content = new[] { "Unable to read chatlog: " + ex.Message };
        }

        _lines.ItemsSource = content.Select((line, index) => $"{index + 1,6}  {line}").ToArray();
        _lines.FontFamily = new FontFamily("Consolas");
        _lines.FontSize = 12.5;
        _lines.BorderThickness = new Thickness(0);
        _lines.Padding = new Thickness(8);
        _lines.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _lines.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        _lines.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

        var menu = new ContextMenu
        {
            Background = (Brush)System.Windows.Application.Current.FindResource("Raised"),
            Foreground = (Brush)System.Windows.Application.Current.FindResource("Text")
        };
        var copy = new MenuItem { Header = "Copy line" };
        copy.Click += (_, _) =>
        {
            if (_lines.SelectedItem is string selected)
            {
                int split = selected.IndexOf("  ", StringComparison.Ordinal);
                Clipboard.SetText(split >= 0 ? selected[(split + 2)..] : selected);
            }
        };
        menu.Items.Add(copy);
        _lines.ContextMenu = menu;

        var body = new Border
        {
            Style = (Style)System.Windows.Application.Current.FindResource("CardStyle"),
            Padding = new Thickness(0),
            Child = _lines
        };
        Grid.SetRow(body, 2);
        root.Children.Add(body);
        Content = root;

        if (lineNumber is int requested && requested > 0 && requested <= content.Length)
        {
            Loaded += (_, _) =>
            {
                _lines.SelectedIndex = requested - 1;
                _lines.ScrollIntoView(_lines.SelectedItem);
                if (_lines.ItemContainerGenerator.ContainerFromIndex(requested - 1) is ListBoxItem item)
                    item.Focus();
            };
        }
    }
}
