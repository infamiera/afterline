using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

internal sealed class PotentialDuplicatePromptWindow : Window
{
    public PotentialDuplicatePromptWindow(Window owner, int lineCount)
    {
        Owner = owner;
        Title = "Potential duplicate chat lines";
        Width = 540;
        MinWidth = 460;
        Height = 300;
        MinHeight = 250;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Possible duplicate capture detected",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });

        var body = new TextBlock
        {
            Text = $"Afterline found {lineCount:N0} line{(lineCount == 1 ? string.Empty : "s")} that match a recent chat scene but were received with an unusual timestamp pattern. Nothing has been removed.\n\nIgnore keeps every line and dismisses this notice. Investigate opens a scrollable comparison first; you can keep the lines there or continue to Live Chat for the final review and removal step.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)owner.FindResource("MutedText"),
            LineHeight = 19
        };
        Grid.SetRow(body, 2);
        root.Children.Add(body);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var ignore = new Button { Content = "Ignore", Padding = new Thickness(14, 7, 14, 7) };
        ignore.Click += (_, _) => { DialogResult = false; Close(); };
        var investigate = new Button
        {
            Content = "Investigate",
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(8, 0, 0, 0)
        };
        investigate.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        investigate.Click += (_, _) => { DialogResult = true; Close(); };
        actions.Children.Add(ignore);
        actions.Children.Add(investigate);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
    }
}
