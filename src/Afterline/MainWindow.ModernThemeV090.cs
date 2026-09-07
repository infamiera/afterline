using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    internal void PrepareManualUpdateExit() => _isExiting = true;
    private bool _modernThemeShellV090Initialized;
    private readonly List<Button> _modernNavigationButtonsV090 = new();

    private void EnsureModernThemeShellV090()
    {
        if (_modernThemeShellV090Initialized) return;
        if (DashboardNav.Parent is not StackPanel navigationPanel ||
            navigationPanel.Parent is not Grid sidebarGrid ||
            sidebarGrid.Parent is not Border sidebarBorder ||
            sidebarBorder.Parent is not Grid rootGrid)
            return;

        _modernThemeShellV090Initialized = true;

        rootGrid.SetResourceReference(Panel.BackgroundProperty, "AfterlineAppGradient");
        sidebarBorder.SetResourceReference(Border.BackgroundProperty, "AfterlineSidebarGradient");
        sidebarGrid.Margin = new Thickness(14, 18, 14, 14);

        if (rootGrid.ColumnDefinitions.Count > 0)
        {
            rootGrid.ColumnDefinitions[0].Width = new GridLength(220);
            _sidebarExpandedWidth = rootGrid.ColumnDefinitions[0].Width;
            _sidebarExpandedMargin = sidebarGrid.Margin;
        }

        EnsureModernNavigationScrollV090(navigationPanel, sidebarGrid);

        AddModernSystemNavigationV090(navigationPanel);
        RestyleModernNavigationV090(navigationPanel);
        DockModernSystemNavigationV090(navigationPanel, sidebarGrid);
        RestyleModernUpdateAreaV090();
        UpdateModernNavigationSelectionV090(FindVisibleMainPageV090());
        RoundNestedPanelCornersV090(rootGrid);
    }

    private static void EnsureModernNavigationScrollV090(
        StackPanel navigationPanel,
        Grid sidebarGrid)
    {
        if (navigationPanel.Parent is ScrollViewer)
            return;

        int row = Grid.GetRow(navigationPanel);
        sidebarGrid.Children.Remove(navigationPanel);
        var scroll = new ScrollViewer
        {
            Content = navigationPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = true,
            Margin = new Thickness(0, 0, -7, 0),
            Padding = new Thickness(0, 0, 7, 0)
        };
        Grid.SetRow(scroll, row);
        sidebarGrid.Children.Add(scroll);
    }

    private void AddModernSystemNavigationV090(StackPanel navigationPanel)
    {
        var systemButtons = new List<Button>();
        AddSystemButtonV090(systemButtons, SettingsNav, "Settings", "\uE713", "Open application settings.");
        AddSystemButtonV090(systemButtons, _themeFooterButton, "Themes", "\uE771", "Customize Afterline's appearance.");
        AddSystemButtonV090(systemButtons, _diagnosticsFooterButtonV159, "Error Logs", "\uE7BA", "Open error logs and diagnostics.");
        AddSystemButtonV090(systemButtons, _infoFooterButton, "About", "\uE946", "About Afterline.");

        if (systemButtons.Count > 0)
            AddSidebarSection(navigationPanel, "SYSTEM", systemButtons);
    }

    private static void DockModernSystemNavigationV090(StackPanel navigationPanel, Grid sidebarGrid)
    {
        if (navigationPanel.Parent is not ScrollViewer navigationScroll ||
            navigationScroll.Parent is not Grid ||
            navigationScroll.Tag as string == "ModernNavigationTop")
            return;

        Grid? systemHeader = navigationPanel.Children
            .OfType<Grid>()
            .FirstOrDefault(header => header.Children.OfType<TextBlock>()
                .Any(label => string.Equals(label.Text, "SYSTEM", StringComparison.Ordinal)));
        if (systemHeader is null) return;

        int systemIndex = navigationPanel.Children.IndexOf(systemHeader);
        UIElement[] systemElements = navigationPanel.Children
            .Cast<UIElement>()
            .Skip(systemIndex)
            .ToArray();
        foreach (UIElement element in systemElements)
            navigationPanel.Children.Remove(element);

        var systemPanel = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        foreach (UIElement element in systemElements)
            systemPanel.Children.Add(element);

        int row = Grid.GetRow(navigationScroll);
        sidebarGrid.Children.Remove(navigationScroll);
        var navigationHost = new Grid();
        navigationHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        navigationHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        navigationScroll.Tag = "ModernNavigationTop";
        navigationScroll.Margin = new Thickness(0, 0, -7, 0);
        navigationScroll.Padding = new Thickness(0, 0, 7, 0);
        Grid.SetRow(navigationScroll, 0);
        navigationHost.Children.Add(navigationScroll);
        Grid.SetRow(systemPanel, 1);
        navigationHost.Children.Add(systemPanel);

        Grid.SetRow(navigationHost, row);
        sidebarGrid.Children.Add(navigationHost);
    }

    private static void AddSystemButtonV090(
        ICollection<Button> target,
        Button? button,
        string label,
        string glyph,
        string tooltip)
    {
        if (button is null) return;
        if (button.Parent is Panel parent)
            parent.Children.Remove(button);

        button.Content = label;
        button.Tag = glyph;
        button.ToolTip = tooltip;
        target.Add(button);
    }

    private void RestyleModernNavigationV090(StackPanel navigationPanel)
    {
        _modernNavigationButtonsV090.Clear();

        foreach (Grid header in navigationPanel.Children.OfType<Grid>())
        {
            header.Height = 20;
            header.Margin = new Thickness(5, navigationPanel.Children.IndexOf(header) == 0 ? 0 : 12, 5, 5);
            foreach (Border divider in header.Children.OfType<Border>())
                divider.Visibility = Visibility.Collapsed;
            foreach (TextBlock label in header.Children.OfType<TextBlock>())
            {
                label.FontSize = 9.5;
                label.FontWeight = FontWeights.SemiBold;
            }
        }

        foreach (Button button in navigationPanel.Children.OfType<Button>())
        {
            string label = button.Content?.ToString() ?? string.Empty;
            button.Tag = NavigationGlyphV090(label, button.Tag?.ToString());
            button.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            button.FontSize = 13;
            button.FontWeight = FontWeights.Normal;
            button.Width = double.NaN;
            button.Height = 36;
            button.Padding = new Thickness(10, 0, 8, 0);
            button.Margin = new Thickness(0, 0, 0, 2);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Center;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.SetResourceReference(FrameworkElement.StyleProperty, "AfterlineSidebarNavigationButton");
            button.SetResourceReference(Control.ForegroundProperty, NavigationColorV090(label));
            button.BorderBrush = Brushes.Transparent;
            _modernNavigationButtonsV090.Add(button);
        }
    }

    private static string NavigationGlyphV090(string label, string? existing)
        => label switch
        {
            "Dashboard" => "\uE80F",
            "Live Chat" => "\uE8BD",
            "Search" => "\uE721",
            "Archive" => "\uE7B8",
            "Log Reader" => "\uE8A5",
            "Notes & Bookmarks" => "\uE70B",
            "Editor" => "\uE70F",
            "Gallery" => "\uEB9F",
            "Settings" => "\uE713",
            "Themes" => "\uE771",
            "Error Logs" => "\uE7BA",
            "About" => "\uE946",
            _ => existing ?? "\uE10C"
        };

    private static string NavigationColorV090(string label)
        => label switch
        {
            "Dashboard" => "AfterlineNavOverview",
            "Live Chat" => "AfterlineNavChat",
            "Search" or "Archive" or "Log Reader" or "Notes & Bookmarks" => "AfterlineNavLibrary",
            "Editor" or "Gallery" => "AfterlineNavCreate",
            "Themes" => "Accent",
            _ => "MutedText"
        };

    private void RestyleModernUpdateAreaV090()
    {
        if (TrayStateText.Parent is not StackPanel updatePanel || updatePanel.Parent is not Border updateCard)
            return;

        updateCard.Background = Brushes.Transparent;
        updateCard.BorderThickness = new Thickness(0, 1, 0, 0);
        updateCard.CornerRadius = new CornerRadius(0);
        updateCard.Padding = new Thickness(6, 12, 6, 0);
        updateCard.SetResourceReference(Border.BorderBrushProperty, "Border");
        if (!updatePanel.Children.OfType<TextBlock>().Any(text => text.Tag as string == "ManualUpdate"))
        {
            var link = new Hyperlink(new Run("Failed to update? Download manually here."));
            link.SetResourceReference(Hyperlink.ForegroundProperty, "Accent");
            link.Click += (_, _) => new ManualUpdateWindow(this).ShowDialog();
            var help = new TextBlock { Tag = "ManualUpdate", FontSize = 10,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            help.Inlines.Add(link);
            updatePanel.Children.Add(help);
        }

        if (!updatePanel.Children.OfType<TextBlock>().Any(text => text.Text == "UPDATES"))
        {
            var heading = new TextBlock
            {
                Text = "UPDATES",
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 7)
            };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
            updatePanel.Children.Insert(0, heading);
        }
    }

    private UIElement? FindVisibleMainPageV090()
    {
        UIElement?[] pages =
        {
            DashboardPage, LivePage, SearchPage, ArchivePage, SettingsPage,
            _logReaderPage, _notesBookmarksPage, _editorPage, _fiveMScreenshotGalleryPageV074
        };
        return pages.FirstOrDefault(page => page?.Visibility == Visibility.Visible);
    }

    private void UpdateModernNavigationSelectionV090(UIElement? page)
    {
        if (!_modernThemeShellV090Initialized) return;

        foreach (Button button in _modernNavigationButtonsV090)
        {
            button.BorderBrush = Brushes.Transparent;
            button.FontWeight = FontWeights.Normal;
            button.CommandParameter = null;
        }

        string? label = page switch
        {
            _ when ReferenceEquals(page, DashboardPage) => "Dashboard",
            _ when ReferenceEquals(page, LivePage) => "Live Chat",
            _ when ReferenceEquals(page, SearchPage) => "Search",
            _ when ReferenceEquals(page, ArchivePage) => "Archive",
            _ when ReferenceEquals(page, SettingsPage) => "Settings",
            _ when ReferenceEquals(page, _logReaderPage) => "Log Reader",
            _ when ReferenceEquals(page, _notesBookmarksPage) => "Notes & Bookmarks",
            _ when ReferenceEquals(page, _editorPage) => "Editor",
            _ when ReferenceEquals(page, _fiveMScreenshotGalleryPageV074) => "Gallery",
            _ => null
        };

        Button? selected = _modernNavigationButtonsV090.FirstOrDefault(
            button => string.Equals(button.Content?.ToString(), label, StringComparison.Ordinal));
        if (selected is null) return;

        selected.CommandParameter = "Selected";
        selected.FontWeight = FontWeights.SemiBold;
    }

    private static void RoundNestedPanelCornersV090(DependencyObject root)
    {
        if (root is Border outer &&
            outer.Child is Grid grid &&
            outer.CornerRadius.TopLeft > 0)
        {
            int lastRow = Math.Max(0, grid.RowDefinitions.Count - 1);
            Border[] edgeRows = grid.Children
                .OfType<Border>()
                .Where(inner => Grid.GetRow(inner) is 0 || Grid.GetRow(inner) == lastRow)
                .ToArray();
            if (edgeRows.Length > 0)
                outer.ClipToBounds = true;

            foreach (Border inner in edgeRows)
            {
                int row = Grid.GetRow(inner);
                if (row == 0)
                {
                    inner.CornerRadius = new CornerRadius(
                        outer.CornerRadius.TopLeft,
                        outer.CornerRadius.TopRight,
                        0,
                        0);
                }
                if (row == lastRow)
                {
                    inner.CornerRadius = new CornerRadius(
                        inner.CornerRadius.TopLeft,
                        inner.CornerRadius.TopRight,
                        outer.CornerRadius.BottomRight,
                        outer.CornerRadius.BottomLeft);
                }
            }
        }

        try
        {
            int children = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < children; index++)
                RoundNestedPanelCornersV090(VisualTreeHelper.GetChild(root, index));
        }
        catch (InvalidOperationException)
        {
            // Some WPF content objects are DependencyObjects without visual children.
        }
    }

    private void VerifyModernThemeAndChatIsolationV090()
    {
        if (!_modernThemeShellV090Initialized ||
            DashboardNav.Style != FindResource("AfterlineSidebarNavigationButton") ||
            (Content as Grid)?.Background is not LinearGradientBrush)
        {
            throw new InvalidOperationException("The modern theme shell was not fully applied.");
        }

        ThemePreferences original = ThemeService.Current;
        const string sampleText = "[12:34:56] Server color test";
        var expected = new[]
        {
            new ChatColorRun(0, 11, 250, 210, 40),
            new ChatColorRun(11, sampleText.Length - 11, 75, 180, 245)
        };
        var sample = new RoleplayColorTextBlock
        {
            DisplayText = sampleText,
            ExactColorRuns = expected,
            UseAutomaticColors = true,
            IsSystemMessage = false
        };
        var cardProbe = new Border
        {
            Style = (Style)FindResource("CardStyle")
        };
        var controlProbe = new Button { Content = "Theme resource probe" };
        var mutedProbe = new TextBlock { Text = "Theme text probe" };
        mutedProbe.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        var host = new Grid { Visibility = Visibility.Collapsed };
        host.Children.Add(sample);
        host.Children.Add(cardProbe);
        host.Children.Add(controlProbe);
        host.Children.Add(mutedProbe);

        try
        {
            if (_editorPage is null)
                throw new InvalidOperationException("The Editor page was unavailable for theme isolation testing.");
            _editorPage.Children.Add(host);
            Color[] before = ReadRenderedChatColorsV090(sample);

            ThemePreferences alternate = ThemeService.CreateGradientTheme(
                "#8D1638", "#4B1025", "#090609", 45, 60);
            alternate.PrimaryText = "#E8A1C1";
            alternate.SecondaryText = "#8BD4CA";
            alternate.Accent = "#D24D78";
            ThemeService.Apply(alternate);

            Color expectedPanel = ThemeService.ParseColor(alternate.Panel, Colors.Transparent);
            Color expectedRaised = ThemeService.ParseColor(alternate.Raised, Colors.Transparent);
            Color expectedMuted = ThemeService.ParseColor(alternate.SecondaryText, Colors.Transparent);
            var lateControlProbe = new Button { Content = "Late theme resource probe" };
            host.Children.Add(lateControlProbe);

            if (ReadSolidColorV092(cardProbe.Background) != expectedPanel ||
                ReadSolidColorV092(controlProbe.Background) != expectedRaised ||
                ReadSolidColorV092(lateControlProbe.Background) != expectedRaised ||
                ReadSolidColorV092(mutedProbe.Foreground) != expectedMuted)
            {
                throw new InvalidOperationException(
                    "Theme resources did not update every existing and newly created interface surface.");
            }

            if ((Content as Grid)?.Background is not LinearGradientBrush firstDirection)
                throw new InvalidOperationException("The application gradient was unavailable for direction testing.");
            Point firstStart = firstDirection.StartPoint;
            Point firstEnd = firstDirection.EndPoint;

            ThemePreferences rotated = ThemeService.Clone(alternate);
            rotated.GradientAngle = alternate.GradientAngle + 90;
            ThemeService.Apply(rotated);
            if ((Content as Grid)?.Background is not LinearGradientBrush secondDirection ||
                (secondDirection.StartPoint == firstStart && secondDirection.EndPoint == firstEnd))
            {
                throw new InvalidOperationException(
                    "Changing gradient direction did not update the displayed application gradient.");
            }

            ThemePreferences cloned = ThemeService.Clone(alternate);
            if (cloned.GradientStart != alternate.GradientStart ||
                cloned.GradientMiddle != alternate.GradientMiddle ||
                cloned.GradientEnd != alternate.GradientEnd ||
                cloned.GradientAngle != alternate.GradientAngle ||
                cloned.GradientIntensity != alternate.GradientIntensity ||
                Application.Current.FindResource("AfterlineAppGradient") is not LinearGradientBrush gradient ||
                gradient.GradientStops.Count != 3 ||
                gradient.GradientStops[0].Color == gradient.GradientStops[2].Color)
            {
                throw new InvalidOperationException("Gradient theme state was not applied completely.");
            }

            Color[] after = ReadRenderedChatColorsV090(sample);
            if (before.Length != 2 || !before.SequenceEqual(after) ||
                before[0] != Color.FromRgb(250, 210, 40) ||
                before[1] != Color.FromRgb(75, 180, 245))
            {
                throw new InvalidOperationException(
                    "Applying an interface theme changed captured server chat colours.");
            }
        }
        finally
        {
            if (_editorPage is not null)
                _editorPage.Children.Remove(host);
            ThemeService.Apply(original);
        }
    }

    private static Color[] ReadRenderedChatColorsV090(RoleplayColorTextBlock block)
        => block.Inlines
            .OfType<Run>()
            .Select(run => (run.Foreground as SolidColorBrush)?.Color ?? Colors.Transparent)
            .ToArray();

    private static Color ReadSolidColorV092(Brush? brush)
        => (brush as SolidColorBrush)?.Color ?? Colors.Transparent;
}
