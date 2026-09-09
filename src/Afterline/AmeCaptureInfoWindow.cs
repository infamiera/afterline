using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

internal sealed class AmeCaptureInfoWindow : Window
{
    public AmeCaptureInfoWindow(Window owner)
    {
        Owner = owner;
        Title = "About /ame capture";
        Width = 610;
        MinWidth = 500;
        Height = 390;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Why some /ame lines are missing",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });

        var explanation = new TextBlock
        {
            Text = "Afterline can only save text that FiveM exposes through its normal visible chat interface. On GTAW, /ame messages from other players can be rendered only as floating world text instead of a chat row, so FiveM does not currently provide Afterline a safe text source to save.\n\nYour own /ame lines that do reach chat are shown with the same purple action colour as /me.\n\nWe are investigating a read-only UI-based fix. We will not add code that hooks into, injects into, or reads the FiveM game process, nor intercepts server resources. Those approaches can raise anti-virus flags and create trust, platform, or server-rule concerns.",
            Foreground = (Brush)owner.FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19
        };
        Grid.SetRow(explanation, 2);
        root.Children.Add(explanation);

        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(14, 7, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 4);
        root.Children.Add(close);

        Content = root;
        ThemeService.ApplyWindow(this);
    }
}
