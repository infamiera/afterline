using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

internal sealed class UpdateAvailableWindow : Window
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>{}\[\]""]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly UpdateCheckResult _release;
    public bool InstallRequested { get; private set; }

    public UpdateAvailableWindow(Window owner, string currentVersion, UpdateCheckResult release)
    {
        Owner = owner;
        _release = release;
        Title = "Afterline Update";
        Width = 570;
        Height = 530;
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
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        content.Children.Add(new TextBlock
        {
            Text = "WHAT'S NEW",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText")
        });

        var notes = new RichTextBox
        {
            IsReadOnly = true,
            IsDocumentEnabled = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            MinHeight = 220,
            Document = BuildReleaseNotesDocument(
                string.IsNullOrWhiteSpace(release.ReleaseNotes)
                    ? "Release notes were not provided for this update."
                    : release.ReleaseNotes)
        };
        Grid.SetRow(notes, 2);
        content.Children.Add(notes);

        var links = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var releaseLink = new TextBlock { FontSize = 11 };
        var releaseHyperlink = new Hyperlink(new Run("View this release on GitHub"))
        {
            Foreground = (Brush)FindResource("Accent"),
            ToolTip = release.ReleasePageUrl
        };
        releaseHyperlink.Click += (_, _) => OpenUrl(release.ReleasePageUrl);
        releaseLink.Inlines.Add(releaseHyperlink);
        links.Children.Add(releaseLink);

        links.Children.Add(new TextBlock
        {
            Text = "Links included in release notes are clickable. Only open links you recognise and trust.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        links.Children.Add(new TextBlock
        {
            Text = "Afterline verifies the downloaded executable against the release SHA-256 checksum before replacing the current build.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0)
        });
        Grid.SetRow(links, 3);
        content.Children.Add(links);

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

    private FlowDocument BuildReleaseNotesDocument(string notes)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = FontFamily,
            FontSize = 12,
            Foreground = (Brush)FindResource("Text")
        };

        string normalized = notes.Replace("\r\n", "\n");
        foreach (string line in normalized.Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 5) };
            int cursor = 0;

            foreach (Match match in UrlRegex.Matches(line))
            {
                if (match.Index > cursor)
                    paragraph.Inlines.Add(new Run(line[cursor..match.Index]));

                string rawUrl = match.Value;
                string url = rawUrl.TrimEnd('.', ',', ';', ':');
                string suffix = rawUrl[url.Length..];

                var link = new Hyperlink(new Run(url))
                {
                    Foreground = (Brush)FindResource("Accent"),
                    ToolTip = url
                };
                link.Click += (_, _) => OpenUrl(url);
                paragraph.Inlines.Add(link);
                if (suffix.Length > 0)
                    paragraph.Inlines.Add(new Run(suffix));

                cursor = match.Index + match.Length;
            }

            if (cursor < line.Length)
                paragraph.Inlines.Add(new Run(line[cursor..]));
            if (line.Length == 0)
                paragraph.Inlines.Add(new Run(" "));

            document.Blocks.Add(paragraph);
        }

        return document;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open an update link.", ex);
        }
    }
}
