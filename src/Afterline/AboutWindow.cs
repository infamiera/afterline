using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class AboutWindow : Window
{
    public AboutWindow(Window owner)
    {
        Owner = owner;
        Title = "About Afterline";
        Width = 600;
        Height = 650;
        MinWidth = 540;
        MinHeight = 610;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Afterline",
            FontSize = 27,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Version {GetVersion()}",
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 4, 0, 0)
        });
        root.Children.Add(header);

        var body = new StackPanel();
        body.Children.Add(CreateCard(
            "About",
            "Afterline is a lightweight Windows utility for capturing, browsing, searching, and composing text roleplay chatlogs. It is designed as a private, personal-use tool with local storage and a simple interface."));

        var contactCard = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var contactStack = new StackPanel();
        contactStack.Children.Add(new TextBlock
        {
            Text = "Contact",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        contactStack.Children.Add(new TextBlock
        {
            Text = "A support contact can be linked here later.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 10)
        });
        contactStack.Children.Add(new Button
        {
            Content = "Contact / Support",
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 7, 12, 7),
            ToolTip = "Support link not configured yet"
        });
        contactCard.Child = contactStack;
        body.Children.Add(contactCard);

        var disclaimer = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(14)
        };
        var disclaimerStack = new StackPanel();
        disclaimerStack.Children.Add(new TextBlock
        {
            Text = "Disclaimer",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        });
        disclaimerStack.Children.Add(new TextBlock
        {
            Text = "Afterline is an independent third-party utility and is not affiliated with, endorsed by, sponsored by, or approved by Rockstar Games, Cfx.re, FiveM, or any roleplay server or community. FiveM is referenced solely to describe compatibility. Users are responsible for following the rules of the servers they use. Redistribution, resale, or re-uploading without the owner's permission is prohibited.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        disclaimer.Child = disclaimerStack;
        body.Children.Add(disclaimer);

        Grid.SetRow(body, 2);
        root.Children.Add(body);

        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 4);
        root.Children.Add(close);

        Content = root;
        ThemeService.ApplyWindow(this);
    }

    private Border CreateCard(string title, string text)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });

        return new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack
        };
    }

    private static string GetVersion()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? "0.0.0"
            : $"{Math.Max(0, version.Major)}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";
    }
}
