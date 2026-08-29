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
    private readonly TextBox _errorText;
    private readonly TextBlock _summary;
    private readonly TextBlock _exportStatus;
    private readonly Button _previousSessionButton;
    private bool _showingPreviousSession;

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
        var headingActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _previousSessionButton = new Button
        {
            Content = "Previous session",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _previousSessionButton.Click += (_, _) =>
        {
            _showingPreviousSession = !_showingPreviousSession;
            RefreshErrors();
        };
        headingActions.Children.Add(_previousSessionButton);
        var refresh = new Button
        {
            Content = "Refresh",
            Padding = new Thickness(12, 7, 12, 7),
            VerticalAlignment = VerticalAlignment.Center
        };
        refresh.Click += (_, _) => RefreshErrors();
        headingActions.Children.Add(refresh);
        Grid.SetColumn(headingActions, 1);
        heading.Children.Add(headingActions);
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
        supportCopy.Children.Add(new TextBlock
        {
            Text = "Send error reports in the #afterline forum channel on Discord.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0)
        });
        supportCopy.Children.Add(new TextBlock
        {
            Text = "Exports include bounded current and previous-session diagnostic timelines so startup delays and recovered freezes can be diagnosed. Nothing is uploaded automatically.",
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
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _exportStatus = new TextBlock
        {
            Foreground = (Brush)FindResource("Success"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 12, 0)
        };
        actions.Children.Add(_exportStatus);
        var clear = new Button
        {
            Content = "Clear error logs",
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 120
        };
        clear.Click += Clear_Click;
        Grid.SetColumn(clear, 1);
        actions.Children.Add(clear);
        var export = new Button
        {
            Content = "Export .txt to Downloads",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource("PrimaryButton")
        };
        export.Click += Export_Click;
        Grid.SetColumn(export, 2);
        actions.Children.Add(export);
        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 80
        };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 3);
        actions.Children.Add(close);
        Grid.SetRow(actions, 6);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
        RefreshErrors();
    }

    private void RefreshErrors()
    {
        IReadOnlyList<string> errors = _showingPreviousSession
            ? DiagnosticLogger.ReadPreviousSessionErrors(100)
            : DiagnosticLogger.ReadRecentErrors(100);
        _previousSessionButton.Content = _showingPreviousSession
            ? "Current logs"
            : "Previous session";
        _previousSessionButton.IsEnabled = _showingPreviousSession ||
                                           DiagnosticLogger.HasPreviousSessionErrors;
        _summary.Text = errors.Count == 0
            ? _showingPreviousSession
                ? "No errors were captured in the previous-session snapshot."
                : "No errors recorded in the current diagnostic log."
            : $"Showing the {errors.Count} most recent error{(errors.Count == 1 ? string.Empty : "s")} from " +
              (_showingPreviousSession ? "the previous-session snapshot." : "the current diagnostic log.");
        _summary.Foreground = (Brush)FindResource(errors.Count == 0 ? "Success" : "Warning");
        _errorText.Text = errors.Count == 0
            ? _showingPreviousSession
                ? "No previous-session errors are available. Current and previous-session diagnostics are both included when an error report is exported."
                : "Afterline has not recorded any current errors. You can still export a report if support asks for one."
            : string.Join(Environment.NewLine + new string('-', 78) + Environment.NewLine, errors);
        _errorText.Select(0, 0);
        _errorText.ScrollToLine(0);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult choice = MessageBox.Show(
            this,
            "Clear the current diagnostic log and the retained previous-session snapshot? This cannot be undone.",
            "Clear Error Logs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes) return;

        if (DiagnosticLogger.ClearErrors())
        {
            _showingPreviousSession = false;
            _exportStatus.Foreground = (Brush)FindResource("Success");
            _exportStatus.Text = "Error logs cleared.";
            RefreshErrors();
        }
        else
        {
            _exportStatus.Foreground = (Brush)FindResource("Warning");
            _exportStatus.Text = "The error logs could not be cleared. Close other Afterline windows and try again.";
        }
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
