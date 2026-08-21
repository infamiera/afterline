using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;
using Forms = System.Windows.Forms;

namespace Afterline;

internal sealed class ThemeEditorWindow : Window
{
    private sealed class ColorRowBinding
    {
        public required Func<string> Getter { get; init; }
        public required TextBlock ValueText { get; init; }
        public required Border Swatch { get; init; }
    }

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly List<ColorRowBinding> _rows = new();
    private ThemePreferences _saved;
    private ThemePreferences _working;
    private readonly TextBlock _statusText;

    public ThemeEditorWindow(Window owner, AppSettings settings, SettingsService settingsService)
    {
        Owner = owner;
        _settings = settings;
        _settingsService = settingsService;
        _saved = ThemeService.Clone(settings.Theme);
        _working = ThemeService.Clone(_saved);

        Title = "Afterline Theme Creator";
        Width = 660;
        Height = 760;
        MinWidth = 560;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Theme Creator",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Safely customize Afterline's surfaces and normal interface text. Roleplay chat colors are kept separate.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Changes preview immediately. Nothing is stored until you press Save theme.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });
        root.Children.Add(header);

        var contentStack = new StackPanel();
        AddSectionHeader(contentStack, "APP COLORS", "Backgrounds, cards, controls, borders and accent colors.");
        AddColorRow(contentStack, "App background", "Main application background.", () => _working.Background, value => _working.Background = value);
        AddColorRow(contentStack, "Sidebar", "Navigation sidebar background.", () => _working.Sidebar, value => _working.Sidebar = value);
        AddColorRow(contentStack, "Panels", "Cards and larger surfaces.", () => _working.Panel, value => _working.Panel = value);
        AddColorRow(contentStack, "Controls", "Buttons, inputs and raised controls.", () => _working.Raised, value => _working.Raised = value);
        AddColorRow(contentStack, "Inset surfaces", "Toolbars and secondary recessed areas.", () => _working.Inset, value => _working.Inset = value);
        AddColorRow(contentStack, "Borders", "Card and control outlines.", () => _working.Border, value => _working.Border = value);
        AddColorRow(contentStack, "Accent", "Primary buttons and focus highlights.", () => _working.Accent, value => _working.Accent = value);
        AddColorRow(contentStack, "Accent hover", "Secondary accent used for hover states.", () => _working.AccentHover, value => _working.AccentHover = value);
        AddColorRow(contentStack, "Control hover", "General control hover surface.", () => _working.ControlHover, value => _working.ControlHover = value);

        AddSectionHeader(contentStack, "TEXT COLORS", "Useful when creating a light theme. These only affect normal interface text.");
        AddColorRow(contentStack, "Primary text", "Main labels, headings and standard interface text.", () => _working.PrimaryText, value => _working.PrimaryText = value);
        AddColorRow(contentStack, "Secondary text", "Subtitles, hints and muted interface text.", () => _working.SecondaryText, value => _working.SecondaryText = value);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = contentStack
        };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _statusText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        footer.Children.Add(_statusText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var revert = new Button { Content = "Revert changes", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        revert.Click += (_, _) => RevertChanges();
        buttons.Children.Add(revert);

        var reset = new Button { Content = "Reset to default", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        reset.Click += (_, _) => ResetDefaults();
        buttons.Children.Add(reset);

        var save = new Button
        {
            Content = "Save theme",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        save.Click += (_, _) => SaveTheme();
        buttons.Children.Add(save);

        var close = new Button { Content = "Close", Padding = new Thickness(12, 7, 12, 7) };
        close.Click += (_, _) => Close();
        buttons.Children.Add(close);

        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        RefreshRows();
        Closing += ThemeEditorWindow_Closing;
    }

    private void AddSectionHeader(StackPanel parent, string title, string subtitle)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, parent.Children.Count == 0 ? 4 : 20, 0, 3)
        });
        parent.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddColorRow(StackPanel parent, string label, string description, Func<string> getter, Action<string> setter)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 10.5,
            Margin = new Thickness(0, 2, 0, 0)
        });
        grid.Children.Add(text);

        var valueText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("MutedText"),
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(12, 0, 12, 0)
        };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        var swatch = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var choose = new Button
        {
            Content = "Choose",
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        choose.Click += (_, _) => ChooseColor(getter, setter);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(swatch);
        actions.Children.Add(choose);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = grid
        };
        parent.Children.Add(card);

        _rows.Add(new ColorRowBinding { Getter = getter, ValueText = valueText, Swatch = swatch });
    }

    private void ChooseColor(Func<string> getter, Action<string> setter)
    {
        Color current = ThemeService.ParseColor(getter(), Colors.Black);
        using var dialog = new Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
            FullOpen = true,
            AnyColor = true,
            SolidColorOnly = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;

        string value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        setter(value);
        ThemeService.Apply(_working);
        RefreshRows();
        _statusText.Text = "Previewing unsaved theme changes.";
    }

    private void RefreshRows()
    {
        foreach (ColorRowBinding row in _rows)
        {
            string value = row.Getter();
            row.ValueText.Text = value;
            row.Swatch.Background = new SolidColorBrush(ThemeService.ParseColor(value, Colors.Transparent));
        }
    }

    private void RevertChanges()
    {
        _working = ThemeService.Clone(_saved);
        ThemeService.Apply(_working);
        RefreshRows();
        _statusText.Text = "Reverted to your last saved theme.";
    }

    private void ResetDefaults()
    {
        _working = ThemeService.CreateDefault();
        ThemeService.Apply(_working);
        RefreshRows();
        _statusText.Text = "Default theme previewed. Press Save theme to keep it.";
    }

    private void SaveTheme()
    {
        try
        {
            _settings.Theme = ThemeService.Clone(_working);
            _settingsService.Save(_settings);
            _saved = ThemeService.Clone(_working);
            ThemeService.Apply(_saved);
            _statusText.Text = "Theme saved locally.";
            _statusText.Foreground = (Brush)FindResource("Success");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save theme settings.", ex);
            _statusText.Text = "Unable to save theme settings.";
            _statusText.Foreground = (Brush)FindResource("Warning");
        }
    }

    private void ThemeEditorWindow_Closing(object? sender, CancelEventArgs e)
    {
        ThemeService.Apply(_saved);
    }
}
