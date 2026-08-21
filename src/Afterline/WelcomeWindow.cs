using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

internal sealed class WelcomeWindow : Window
{
    private sealed class WelcomeTheme
    {
        public required string Name { get; init; }
        public required ThemePreferences Theme { get; init; }
        public override string ToString() => Name;
    }

    private static readonly IReadOnlyList<WelcomeTheme> Themes = new WelcomeTheme[]
    {
        new() { Name = "Afterline Default", Theme = new ThemePreferences() },
        new()
        {
            Name = "Midnight Violet",
            Theme = new ThemePreferences
            {
                Background = "#121019", Sidebar = "#0D0B12", Panel = "#1B1723", Raised = "#241F2E", Inset = "#17131E",
                Border = "#3B3149", Accent = "#9A7BD1", AccentHover = "#B092E6", ControlHover = "#30283D",
                PrimaryText = "#F3EEF8", SecondaryText = "#BDB2C8"
            }
        },
        new()
        {
            Name = "Deep Ocean",
            Theme = new ThemePreferences
            {
                Background = "#0E1518", Sidebar = "#0A1012", Panel = "#142025", Raised = "#1B2A30", Inset = "#101A1E",
                Border = "#294149", Accent = "#3FA7B8", AccentHover = "#64C2D0", ControlHover = "#22373E",
                PrimaryText = "#EDF7F8", SecondaryText = "#AAC3C8"
            }
        },
        new()
        {
            Name = "Carbon Ember",
            Theme = new ThemePreferences
            {
                Background = "#151312", Sidebar = "#0F0E0D", Panel = "#201B18", Raised = "#2B2420", Inset = "#191512",
                Border = "#44372F", Accent = "#D8874F", AccentHover = "#E9A06D", ControlHover = "#392D26",
                PrimaryText = "#F7F0EB", SecondaryText = "#C8B5A8"
            }
        },
        new()
        {
            Name = "Graphite Rose",
            Theme = new ThemePreferences
            {
                Background = "#151416", Sidebar = "#100F11", Panel = "#1E1C20", Raised = "#28252A", Inset = "#19171B",
                Border = "#3C373F", Accent = "#D56C88", AccentHover = "#E888A0", ControlHover = "#342F36",
                PrimaryText = "#F5F1F2", SecondaryText = "#BFB6BA"
            }
        },
        new()
        {
            Name = "Frost",
            Theme = new ThemePreferences
            {
                Background = "#F3F6F9", Sidebar = "#E7EDF3", Panel = "#FFFFFF", Raised = "#EDF2F7", Inset = "#F7F9FB",
                Border = "#C9D3DD", Accent = "#3D7FC4", AccentHover = "#5B99D6", ControlHover = "#DFE7EF",
                PrimaryText = "#18212B", SecondaryText = "#5F6D7A"
            }
        }
    };

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ThemePreferences _originalTheme;
    private readonly TextBox _archiveBox;
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _startMinimized;
    private readonly CheckBox _minimizeToTray;
    private readonly ComboBox _themeBox;
    private bool _completed;

    public WelcomeWindow(Window owner, AppSettings settings, SettingsService settingsService)
    {
        Owner = owner;
        _settings = settings;
        _settingsService = settingsService;
        _originalTheme = ThemeService.Clone(settings.Theme);

        Title = "Welcome to Afterline";
        Width = 680;
        Height = 610;
        MinWidth = 620;
        MinHeight = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Welcome to Afterline",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "A quick one-time setup. You can change all of this later in Settings or Themes.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var content = new StackPanel();

        var archiveCard = CreateCard("Chatlog folder", "Choose where completed chatlogs should be stored.");
        var archiveGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        archiveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        archiveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        archiveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _archiveBox = new TextBox { Text = settings.ArchiveRoot, IsReadOnly = true };
        archiveGrid.Children.Add(_archiveBox);
        var browse = new Button { Content = "Browse", Padding = new Thickness(12, 7, 12, 7) };
        browse.Click += Browse_Click;
        Grid.SetColumn(browse, 2);
        archiveGrid.Children.Add(browse);
        ((StackPanel)archiveCard.Child).Children.Add(archiveGrid);
        content.Children.Add(archiveCard);

        var startupCard = CreateCard("Startup", "Pick the startup behaviour you prefer.");
        var startup = (StackPanel)startupCard.Child;
        _startWithWindows = new CheckBox { Content = "Start Afterline with Windows", IsChecked = settings.StartWithWindows, Margin = new Thickness(0, 12, 0, 0) };
        _startMinimized = new CheckBox { Content = "Start minimized to the system tray", IsChecked = settings.StartMinimized };
        _minimizeToTray = new CheckBox { Content = "Closing the window minimizes to tray", IsChecked = settings.MinimizeToTray };
        startup.Children.Add(_startWithWindows);
        startup.Children.Add(_startMinimized);
        startup.Children.Add(_minimizeToTray);
        content.Children.Add(startupCard);

        var themeCard = CreateCard("Theme", "Pick a starting appearance. This only changes Afterline's interface colors.");
        var themeStack = (StackPanel)themeCard.Child;
        _themeBox = new ComboBox
        {
            ItemsSource = Themes,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 0),
            MinHeight = 34
        };
        BindThemeBoxResources();
        _themeBox.SelectionChanged += ThemeBox_SelectionChanged;
        themeStack.Children.Add(_themeBox);
        content.Children.Add(themeCard);

        scroll.Content = content;
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = "This setup is only shown on a new installation.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });
        var done = new Button
        {
            Content = "Done",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(18, 8, 18, 8),
            MinWidth = 96
        };
        done.Click += Done_Click;
        Grid.SetColumn(done, 1);
        footer.Children.Add(done);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);
        Closing += WelcomeWindow_Closing;
        _themeBox.SelectedIndex = FindClosestThemeIndex(settings.Theme);
    }

    private Border CreateCard(string title, string description)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        return new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack
        };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(_archiveBox.Text) ? _archiveBox.Text : string.Empty,
            Description = "Choose where Afterline stores completed chatlogs"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _archiveBox.Text = dialog.SelectedPath;
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_themeBox.SelectedItem is not WelcomeTheme choice) return;
        ThemeService.Apply(choice.Theme);
        ThemeService.ApplyWindow(this);
        BindThemeBoxResources();
        _themeBox.ApplyTemplate();
        _themeBox.InvalidateVisual();
    }

    private void BindThemeBoxResources()
    {
        _themeBox.SetResourceReference(Control.BackgroundProperty, "Raised");
        _themeBox.SetResourceReference(Control.ForegroundProperty, "Text");
        _themeBox.SetResourceReference(Control.BorderBrushProperty, "Border");
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string archive = string.IsNullOrWhiteSpace(_archiveBox.Text) ? _settings.ArchiveRoot : _archiveBox.Text.Trim();
            Directory.CreateDirectory(archive);

            _settings.ArchiveRoot = archive;
            _settings.StartWithWindows = _startWithWindows.IsChecked == true;
            _settings.StartMinimized = _startMinimized.IsChecked == true;
            _settings.MinimizeToTray = _minimizeToTray.IsChecked == true;
            if (_themeBox.SelectedItem is WelcomeTheme choice)
                _settings.Theme = ThemeService.Clone(choice.Theme);
            _settings.FirstRunCompleted = true;

            StartupService.SetEnabled(_settings.StartWithWindows);
            _settingsService.Save(_settings);
            ThemeService.Apply(_settings.Theme);
            _completed = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to complete first-run setup.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to save setup", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WelcomeWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_completed)
            ThemeService.Apply(_originalTheme);
    }

    private static int FindClosestThemeIndex(ThemePreferences theme)
    {
        string background = ThemeService.Normalize(theme).Background;
        for (int i = 0; i < Themes.Count; i++)
        {
            if (string.Equals(ThemeService.Normalize(Themes[i].Theme).Background, background, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }
}
