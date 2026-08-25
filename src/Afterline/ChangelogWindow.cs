using System.Reflection;
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
            Text = IsCanaryBuild()
                ? "Canary build notes followed by the highlighted Stable release history."
                : "Stable release notes for the current build and previous Afterline updates.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(heading);

        var releaseStack = new StackPanel();
        IEnumerable<ChangelogEntry> entries = CurrentReleaseData.Entries.Concat(ChangelogData.Entries);
        if (IsCanaryBuild())
            entries = CanaryChangelogData.Entries.Concat(entries);
        foreach (ChangelogEntry entry in entries)
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
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = entry.Channel == ChangelogChannel.Canary
                ? $"Afterline Canary - #{entry.CanaryBuild.GetValueOrDefault():000}"
                : $"Afterline Stable - v{entry.Version}",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        var badge = new Border
        {
            Background = (Brush)FindResource(entry.Channel == ChangelogChannel.Canary ? "Raised" : "Bg"),
            BorderBrush = (Brush)FindResource(entry.Channel == ChangelogChannel.Canary ? "Border" : "Accent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 3, 9, 3),
            Child = new TextBlock
            {
                Text = entry.Channel == ChangelogChannel.Canary ? "CANARY" : "STABLE",
                Foreground = (Brush)FindResource(entry.Channel == ChangelogChannel.Canary ? "MutedText" : "Accent"),
                FontSize = 9,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        content.Children.Add(header);

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
            var line = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("MutedText"),
                Margin = new Thickness(2, 0, 6, 8),
                LineHeight = 19
            };
            line.Inlines.Add(new Run("• ")
            {
                Foreground = (Brush)FindResource("Accent"),
                FontWeight = FontWeights.Bold
            });
            int separator = change.IndexOf(" — ", StringComparison.Ordinal);
            if (separator > 0)
            {
                line.Inlines.Add(new Run(change[..separator])
                {
                    Foreground = CategoryBrush(change[..separator]),
                    FontWeight = FontWeights.SemiBold
                });
                line.Inlines.Add(new Run(change[separator..])
                {
                    Foreground = (Brush)FindResource("MutedText")
                });
            }
            else
            {
                line.Inlines.Add(new Run(change)
                {
                    Foreground = (Brush)FindResource("MutedText")
                });
            }
            content.Children.Add(line);
        }

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 10, 12),
            Padding = new Thickness(18),
            Child = content
        };
        if (entry.Channel == ChangelogChannel.Stable)
        {
            Brush border = ((Brush)FindResource("Accent")).Clone();
            border.Opacity = 0.48;
            card.BorderBrush = border;
            card.BorderThickness = new Thickness(1.25);
        }
        return card;
    }

    private Brush CategoryBrush(string category)
    {
        string resource = category.Contains("performance", StringComparison.OrdinalIgnoreCase) ||
                          category.Contains("usage", StringComparison.OrdinalIgnoreCase)
            ? "Success"
            : category.Contains("recovery", StringComparison.OrdinalIgnoreCase) ||
              category.Contains("identity", StringComparison.OrdinalIgnoreCase)
                ? "Warning"
                : "Accent";
        return (Brush)FindResource(resource);
    }

    private static bool IsCanaryBuild()
        => (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Contains("-canary.", StringComparison.OrdinalIgnoreCase) == true;
}
