using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class UpdateAvailableWindow : Window
{
    private readonly UpdateCheckResult _release;
    public bool InstallRequested { get; private set; }

    public UpdateAvailableWindow(Window owner, string currentVersion, UpdateCheckResult release)
    {
        Owner = owner;
        _release = release;
        Title = "Afterline Update";
        Width = 560;
        Height = 520;
        MinWidth = 500;
        MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "An Afterline update is available",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Current {currentVersion}  →  Latest {release.LatestVersion}",
            Foreground = (Brush)FindResource("Accent"),
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0)
        });
        root.Children.Add(header);

        var card = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(14) };
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "WHAT'S NEW",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var notes = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(release.ReleaseNotes)
                ? "Release notes were not provided for this update."
                : release.ReleaseNotes,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 220,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };
        content.Children.Add(notes);

        var releaseLink = new TextBlock { Margin = new Thickness(0, 10, 0, 0), FontSize = 11 };
        var hyperlink = new Hyperlink(new Run("View release on GitHub"))
        {
            Foreground = (Brush)FindResource("Accent")
        };
        hyperlink.Click += (_, _) => OpenReleasePage();
        releaseLink.Inlines.Add(hyperlink);
        content.Children.Add(releaseLink);

        content.Children.Add(new TextBlock
        {
            Text = "Afterline verifies the downloaded executable against the release SHA-256 checksum before replacing the current build.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });
        card.Child = content;
        Grid.SetRow(card, 2);
        root.Children.Add(card);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var later = new Button
        {
            Content = "Later",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        later.Click += (_, _) => Close();
        footer.Children.Add(later);

        var install = new Button
        {
            Content = "Update now",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(14, 8, 14, 8)
        };
        install.Click += (_, _) =>
        {
            InstallRequested = true;
            DialogResult = true;
            Close();
        };
        footer.Children.Add(install);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);
    }

    private void OpenReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _release.ReleasePageUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open the Afterline release page.", ex);
        }
    }
}
