using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class ChangelogWindow : Window
{
    public ChangelogWindow(Window owner)
    {
        Owner = owner;
        Title = "Afterline Changelog";
        Width = 760;
        Height = 760;
        MinWidth = 600;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Changelog",
            FontSize = 27,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Release notes for the current build and previous Afterline updates.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(heading);

        var releaseStack = new StackPanel();
        foreach (ChangelogEntry entry in CurrentReleaseData.Entries.Concat(ChangelogData.Entries))
            releaseStack.Children.Add(BuildReleaseCard(entry));

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = PanningMode.VerticalOnly,
            Content = releaseStack
        };
        scroll.SetResourceReference(Control.BackgroundProperty, "Bg");
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 82
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 4);
        root.Children.Add(close);

        Content = root;
        ThemeService.ApplyWindow(this);
    }

    private Border BuildReleaseCard(ChangelogEntry entry)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = $"Changelog - v{entry.Version}",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });

        var date = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 5, 0, 12)
        };
        date.Inlines.Add(new Run("Date: ") { FontWeight = FontWeights.Bold });
        date.Inlines.Add(new Run(entry.Date));
        content.Children.Add(date);

        foreach (string change in entry.Changes)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"• {change}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 6, 7)
            });
        }

        return new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 10, 12),
            Padding = new Thickness(18),
            Child = content
        };
    }
}
