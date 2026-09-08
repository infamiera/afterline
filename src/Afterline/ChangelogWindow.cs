using System.Reflection;
using System.Text.RegularExpressions;
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

        bool isCanaryBuild = IsCanaryBuild();
        var releaseStack = new StackPanel();
        IEnumerable<ChangelogEntry> stableEntries = CurrentReleaseData.Entries
            .Concat(ChangelogData.Entries)
            .Where(entry => entry.Channel == ChangelogChannel.Stable);
        IEnumerable<ChangelogEntry> entries = isCanaryBuild
            ? CanaryChangelogData.Entries
                .Where(entry => entry.Channel == ChangelogChannel.Canary)
                .Concat(stableEntries)
            : stableEntries;
        bool currentCanaryCard = isCanaryBuild;
        foreach (ChangelogEntry entry in entries)
        {
            int? runningBuild = currentCanaryCard && entry.Channel == ChangelogChannel.Canary
                ? GetRunningCanaryBuild()
                : null;
            releaseStack.Children.Add(BuildReleaseCard(entry, runningBuild));
            if (entry.Channel == ChangelogChannel.Canary)
                currentCanaryCard = false;
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = PanningMode.VerticalOnly,
            Content = releaseStack
        };
        // Keep the viewport, including its empty area beside the cards, on an
        // explicit dynamic surface. The stock ScrollViewer background otherwise
        // exposes a stale palette during a live theme change.
        scroll.SetResourceReference(Control.BackgroundProperty, "AfterlineInset");
        scroll.SetResourceReference(Control.BorderBrushProperty, "Border");
        scroll.BorderThickness = new Thickness(0);
        var scrollHost = new Grid();
        scrollHost.SetResourceReference(Panel.BackgroundProperty, "AfterlineInset");
        scrollHost.Children.Add(scroll);
        var scrollFrame = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Child = scrollHost
        };
        scrollFrame.SetResourceReference(Border.BackgroundProperty, "AfterlineInset");
        scrollFrame.SetResourceReference(Border.BorderBrushProperty, "Border");
        // The host grid allows the reusable rounded-clip helper to clip both the
        // scrollbar track and viewport corners rather than leaving square pixels.
        RoundedPanelClip.Attach(scrollFrame);
        Grid.SetRow(scrollFrame, 2);
        root.Children.Add(scrollFrame);

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

    private Border BuildReleaseCard(ChangelogEntry entry, int? runningCanaryBuild)
    {
        var content = new StackPanel();
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = entry.Channel == ChangelogChannel.Canary
                ? $"Afterline Canary - #{(runningCanaryBuild ?? entry.CanaryBuild.GetValueOrDefault()):000}"
                : $"Afterline Stable - v{entry.Version}",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var badge = CreateChannelBadge(entry.Channel);
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
            int separator = change.IndexOf(" — ", StringComparison.Ordinal);
            string title = separator > 0 ? change[..separator].Trim() : "Change";
            string description = separator > 0 ? change[(separator + 3)..].Trim() : change.Trim();
            var changeBlock = new StackPanel
            {
                Margin = new Thickness(2, 0, 6, 11)
            };
            changeBlock.Children.Add(new TextBlock
            {
                Text = title,
                TextWrapping = TextWrapping.Wrap,
                Foreground = CategoryBrush(title),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            });
            if (!string.IsNullOrWhiteSpace(description))
            {
                changeBlock.Children.Add(new TextBlock
                {
                    Text = description,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)FindResource("MutedText"),
                    FontSize = 11.5,
                    LineHeight = 18,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            content.Children.Add(changeBlock);
        }

        var card = new Border
        {
            Margin = new Thickness(0, 0, 10, 12),
            Padding = new Thickness(18),
            Child = content
        };
        card.SetResourceReference(FrameworkElement.StyleProperty, "CardStyle");
        card.SetResourceReference(Border.BackgroundProperty, "Panel");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        return card;
    }

    private static Border CreateChannelBadge(ChangelogChannel channel)
    {
        bool canary = channel == ChangelogChannel.Canary;
        Color accent = canary
            ? Color.FromRgb(0xE5, 0xB5, 0x68)
            : Color.FromRgb(0xC8, 0xD0, 0xDC);
        Color background = canary
            ? Color.FromRgb(0x3D, 0x32, 0x18)
            : Color.FromRgb(0x2C, 0x31, 0x3A);
        var accentBrush = new SolidColorBrush(accent);
        var backgroundBrush = new SolidColorBrush(background);
        accentBrush.Freeze();
        backgroundBrush.Freeze();

        return new Border
        {
            Background = backgroundBrush,
            BorderBrush = accentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 3, 9, 3),
            Margin = new Thickness(12, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = canary ? "CANARY" : "STABLE",
                Foreground = accentBrush,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
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

    private static int? GetRunningCanaryBuild()
    {
        string informational = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;
        Match match = Regex.Match(informational, @"-canary\.(?<build>\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["build"].Value, out int build)
            ? build
            : null;
    }
}
