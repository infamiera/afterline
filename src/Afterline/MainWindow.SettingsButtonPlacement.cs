using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _settingsButtonRelocated;
    private ColumnDefinition? _sidebarColumn;
    private Grid? _sidebarGrid;
    private Button? _sidebarToggleButton;
    private GridLength _sidebarExpandedWidth;
    private Thickness _sidebarExpandedMargin;
    private bool _sidebarCollapsed;

    private void EnsureSettingsButtonPlacement()
    {
        if (_settingsButtonRelocated) return;
        if (SettingsNav.Parent is not StackPanel navigationPanel ||
            navigationPanel.Parent is not Grid sidebarGrid ||
            sidebarGrid.Parent is not Border sidebarBorder ||
            sidebarBorder.Parent is not Grid rootGrid ||
            rootGrid.ColumnDefinitions.Count < 2)
            return;

        _settingsButtonRelocated = true;
        _sidebarGrid = sidebarGrid;
        _sidebarColumn = rootGrid.ColumnDefinitions[0];
        _sidebarExpandedWidth = _sidebarColumn.Width;
        _sidebarExpandedMargin = sidebarGrid.Margin;

        Button? logReader = _logReaderNavButton ?? navigationPanel.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Log Reader", StringComparison.Ordinal));
        Button? notes = navigationPanel.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Notes & Bookmarks", StringComparison.Ordinal));
        Button? editor = _editorNavButton ?? navigationPanel.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Editor", StringComparison.Ordinal));
        Button? screenshots = _fiveMScreenshotNavButtonV074 ?? navigationPanel.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Gallery", StringComparison.Ordinal));

        if (notes is not null)
        {
            notes.Click += (_, _) =>
            {
                if (_logReaderPage is not null) _logReaderPage.Visibility = Visibility.Collapsed;
            };
        }

        navigationPanel.Children.Remove(SettingsNav);
        navigationPanel.Children.Clear();

        AddSidebarSection(navigationPanel, "OVERVIEW", new[] { DashboardNav });
        AddSidebarSection(navigationPanel, "CHAT", new[] { LiveNav });

        var libraryButtons = new List<Button> { SearchNav, ArchiveNav };
        if (logReader is not null) libraryButtons.Add(logReader);
        if (notes is not null) libraryButtons.Add(notes);
        AddSidebarSection(navigationPanel, "LIBRARY", libraryButtons);

        if (editor is not null || screenshots is not null)
            AddSidebarSection(navigationPanel, "IMAGE EDITOR", new[] { editor, screenshots }.OfType<Button>());

        PlaceSettingsInMainFooter();
        ApplyMainSidebarTooltips(navigationPanel);

        _sidebarToggleButton = new Button
        {
            Content = "\uE76B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12,
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Hide sidebar"
        };
        _sidebarToggleButton.Click += (_, _) => ToggleSidebar();
        Grid.SetRow(_sidebarToggleButton, 0);
        Panel.SetZIndex(_sidebarToggleButton, 20);
        sidebarGrid.Children.Add(_sidebarToggleButton);
    }

    private static void ApplyMainSidebarTooltips(StackPanel navigationPanel)
    {
        foreach (Button button in navigationPanel.Children.OfType<Button>())
        {
            string label = button.Content?.ToString() ?? string.Empty;
            button.ToolTip = label switch
            {
                "Dashboard" => "View recent sessions and FiveM status.",
                "Live Chat" => "View the active chat session.",
                "Search" => "Search stored chatlogs.",
                "Archive" => "Browse recent chatlogs.",
                "Log Reader" => "Read a saved chatlog.",
                "Notes & Bookmarks" => "Open saved notes and bookmarks.",
                "Editor" => "Create and edit screenshots.",
                "Gallery" => "View locally stored captures.",
                _ => button.ToolTip
            };
        }
    }

    private void PlaceSettingsInMainFooter()
    {
        if (SettingsNav.Parent is Panel currentParent)
            currentParent.Children.Remove(SettingsNav);

        if (BottomStatusText.Parent is not Grid footerGrid) return;

        if (footerGrid.ColumnDefinitions.Count < 3)
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        SettingsNav.Content = "\uE713";
        SettingsNav.FontFamily = new FontFamily("Segoe MDL2 Assets");
        SettingsNav.FontSize = 16;
        SettingsNav.Width = 30;
        SettingsNav.Height = 28;
        SettingsNav.Padding = new Thickness(0);
        SettingsNav.Margin = new Thickness(10, 0, 0, 0);
        SettingsNav.HorizontalAlignment = HorizontalAlignment.Right;
        SettingsNav.VerticalAlignment = VerticalAlignment.Center;
        SettingsNav.HorizontalContentAlignment = HorizontalAlignment.Center;
        SettingsNav.VerticalContentAlignment = VerticalAlignment.Center;
        SettingsNav.ToolTip = "Settings";

        Grid.SetColumn(SettingsNav, 2);
        footerGrid.Children.Add(SettingsNav);
    }

    private void AddSidebarSection(StackPanel navigationPanel, string title, IEnumerable<Button> buttons)
    {
        Button[] entries = buttons.Distinct().ToArray();
        if (entries.Length == 0) return;

        var header = new Grid
        {
            Margin = new Thickness(2, navigationPanel.Children.Count == 0 ? 0 : 8, 2, 7),
            Height = 18
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = title,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(label);

        var divider = new Border
        {
            Height = 1,
            Background = (Brush)FindResource("Border"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(divider, 2);
        header.Children.Add(divider);
        navigationPanel.Children.Add(header);

        foreach (Button button in entries)
        {
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0, 0, 0, 8);
            navigationPanel.Children.Add(button);
        }
    }

    private void ToggleSidebar()
    {
        if (_sidebarColumn is null || _sidebarGrid is null || _sidebarToggleButton is null) return;

        _sidebarCollapsed = !_sidebarCollapsed;
        if (_sidebarCollapsed)
        {
            _sidebarColumn.Width = new GridLength(52);
            _sidebarGrid.Margin = new Thickness(10, _sidebarExpandedMargin.Top, 10, _sidebarExpandedMargin.Bottom);

            foreach (UIElement child in _sidebarGrid.Children)
            {
                if (!ReferenceEquals(child, _sidebarToggleButton)) child.Visibility = Visibility.Collapsed;
            }

            _sidebarToggleButton.Visibility = Visibility.Visible;
            _sidebarToggleButton.Content = "\uE76C";
            _sidebarToggleButton.HorizontalAlignment = HorizontalAlignment.Center;
            _sidebarToggleButton.ToolTip = "Show sidebar";
        }
        else
        {
            _sidebarColumn.Width = _sidebarExpandedWidth;
            _sidebarGrid.Margin = _sidebarExpandedMargin;

            foreach (UIElement child in _sidebarGrid.Children)
                child.Visibility = Visibility.Visible;

            _sidebarToggleButton.Content = "\uE76B";
            _sidebarToggleButton.HorizontalAlignment = HorizontalAlignment.Right;
            _sidebarToggleButton.ToolTip = "Hide sidebar";
        }
    }
}
