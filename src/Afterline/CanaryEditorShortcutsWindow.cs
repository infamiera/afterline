using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class CanaryEditorShortcutsWindow : Window
{
    public CanaryEditorShortcutsWindow(Window owner)
    {
        Owner = owner;
        Title = "Editor Shortcuts";
        Width = 470;
        Height = 390;
        MinWidth = 420;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Editor shortcuts",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(14)
        };
        var content = new StackPanel();
        AddShortcut(content, "Ctrl + Z", "Undo the last committed Editor change.");
        AddShortcut(content, "Ctrl + Shift + Z", "Redo the last reverted Editor change.");
        AddShortcut(content, "Ctrl + S", "Export using the current output format.");
        AddShortcut(content, "Ctrl + mouse wheel", "Zoom the canvas in or out.");
        AddShortcut(content, "Esc", "Exit Full Screen Editor or cancel the active selection tool.");
        AddShortcut(content, "Enter", "Finish a Polygonal Lasso selection.");
        card.Child = content;
        Grid.SetRow(card, 2);
        root.Children.Add(card);

        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(14, 6, 14, 6)
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 4);
        root.Children.Add(close);

        Content = root;
        ThemeService.ApplyWindow(this);
    }

    private void AddShortcut(StackPanel parent, string keys, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(new TextBlock
        {
            Text = keys,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Accent")
        });

        var detail = new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedText")
        };
        Grid.SetColumn(detail, 2);
        row.Children.Add(detail);
        parent.Children.Add(row);
    }
}
