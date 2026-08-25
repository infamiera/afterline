using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class DiagnosticsWindow : Window
{
    private const string DiscordInvite = "https://discord.gg/At2znTygfV";
    private const string AfterlineForum = "https://discord.com/channels/1388519828553203818/1541203371455942748";
    private readonly TextBox _errorText;
    private readonly TextBlock _summary;
    private readonly TextBlock _exportStatus;

    public DiagnosticsWindow(Window owner)
    {
        Owner = owner;
        Title = "Afterline Error Logs";
        Width = 850;
        Height = 720;
        MinWidth = 680;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingCopy = new StackPanel();
        headingCopy.Children.Add(new TextBlock
        {
            Text = "Error logs",
            FontSize = 27,
            FontWeight = FontWeights.SemiBold
        });
        _summary = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        headingCopy.Children.Add(_summary);
        heading.Children.Add(headingCopy);
        var refresh = new Button
        {
            Content = "Refresh",
            Padding = new Thickness(12, 7, 12, 7),
            VerticalAlignment = VerticalAlignment.Center
        };
        refresh.Click += (_, _) => RefreshErrors();
        Grid.SetColumn(refresh, 1);
        heading.Children.Add(refresh);
        root.Children.Add(heading);

        var support = new Border
        {
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14)
        };
        var supportCopy = new StackPanel();
        supportCopy.Children.Add(CreateLinkLine("Join the Afterline Discord: ", DiscordInvite, DiscordInvite));
        supportCopy.Children.Add(CreateLinkLine(
            "Only post exported error logs in the Afterline forum: ",
            "Open the Afterline forum",
            AfterlineForum,
            new Thickness(0, 7, 0, 0)));
        supportCopy.Children.Add(new TextBlock
        {
            Text = "Nothing is uploaded automatically. Export the report below, create a forum post, and attach the generated .txt file so the error can be diagnosed.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0)
        });
        support.Child = supportCopy;
        Grid.SetRow(support, 2);
        root.Children.Add(support);

        _errorText = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Padding = new Thickness(10)
        };
        Grid.SetRow(_errorText, 4);
        root.Children.Add(_errorText);

        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _exportStatus = new TextBlock
        {
            Foreground = (Brush)FindResource("Success"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 12, 0)
        };
        actions.Children.Add(_exportStatus);
        var export = new Button
        {
            Content = "Export .txt to Downloads",
            Padding = new Thickness(14, 8, 14, 8),
            Style = (Style)FindResource("PrimaryButton")
        };
        export.Click += Export_Click;
        Grid.SetColumn(export, 1);
        actions.Children.Add(export);
        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 80
        };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 2);
        actions.Children.Add(close);
        Grid.SetRow(actions, 6);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
        RefreshErrors();
    }

    private void RefreshErrors()
    {
        IReadOnlyList<string> errors = DiagnosticLogger.ReadRecentErrors(100);
        _summary.Text = errors.Count == 0
            ? "No recorded application errors."
            : $"Showing the {errors.Count} most recent recorded error{(errors.Count == 1 ? string.Empty : "s")}.";
        _summary.Foreground = (Brush)FindResource(errors.Count == 0 ? "Success" : "Warning");
        _errorText.Text = errors.Count == 0
            ? "Afterline has not recorded any errors. You can still export a report if support asks for one."
            : string.Join(Environment.NewLine + new string('-', 78) + Environment.NewLine, errors);
        _errorText.Select(0, 0);
        _errorText.ScrollToLine(0);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = DiagnosticLogger.ExportErrorReportToDownloads();
            _exportStatus.Foreground = (Brush)FindResource("Success");
            _exportStatus.Text = $"Saved to Downloads · {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to export the Afterline error report.", ex);
            _exportStatus.Foreground = (Brush)FindResource("Warning");
            _exportStatus.Text = "The error report could not be saved to Downloads.";
        }
    }

    private TextBlock CreateLinkLine(
        string prefix,
        string linkText,
        string address,
        Thickness? margin = null)
    {
        var line = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0)
        };
        line.Inlines.Add(new Run(prefix));
        var link = new Hyperlink(new Run(linkText))
        {
            Foreground = (Brush)FindResource("Accent"),
            ToolTip = address
        };
        link.Click += (_, _) => OpenLink(address);
        line.Inlines.Add(link);
        return line;
    }

    private void OpenLink(string address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open an Afterline support link.", ex);
            _exportStatus.Foreground = (Brush)FindResource("Warning");
            _exportStatus.Text = "The link could not be opened in the default browser.";
        }
    }
}
