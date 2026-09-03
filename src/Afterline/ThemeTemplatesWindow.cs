using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;
using Forms = System.Windows.Forms;

namespace Afterline;

internal sealed class ThemeTemplatesWindow : Window
{
    private sealed record ThemePreset(string Name, string Description, ThemePreferences Theme);

    private sealed class ColorControl
    {
        public required Func<string> Getter { get; init; }
        public required Action<string> Setter { get; init; }
        public required Border Swatch { get; init; }
        public required TextBlock Value { get; init; }
    }

    private static readonly IReadOnlyList<ThemePreset> DefaultThemes = new ThemePreset[]
    {
        new("Afterline Slate", "Balanced blue-gray", new ThemePreferences()),
        new("Midnight Violet", "Subdued purple-black", ThemeService.CreateGradientTheme("#4B3475", "#241B3A", "#0D0B12", 145, 28)),
        new("Deep Ocean", "Cool charcoal and teal", ThemeService.CreateGradientTheme("#176274", "#173C4A", "#081216", 145, 28)),
        new("Carbon Ember", "Charcoal and warm amber", ThemeService.CreateGradientTheme("#8A4A25", "#49301F", "#0F0E0D", 145, 26))
    };

    private static readonly IReadOnlyList<ThemePreset> GradientThemes = new ThemePreset[]
    {
        new("Black Cherry", "Black, cherry and rose", ThemeService.CreateGradientTheme("#8D1638", "#4B1025", "#090609", 135, 58)),
        new("Aurora", "Violet into cool green", ThemeService.CreateGradientTheme("#7020A5", "#17212A", "#22B826", 45, 54)),
        new("Amethyst", "Deep violet and indigo", ThemeService.CreateGradientTheme("#6F3AD8", "#31205D", "#0A0815", 135, 52)),
        new("Lagoon", "Teal into midnight blue", ThemeService.CreateGradientTheme("#087E83", "#123A5B", "#080D19", 35, 50)),
        new("Ember", "Crimson into warm amber", ThemeService.CreateGradientTheme("#A51C3A", "#63301D", "#120807", 145, 55)),
        new("Twilight", "Rose, violet and blue", ThemeService.CreateGradientTheme("#A72C67", "#592D8A", "#142C68", 45, 48))
    };

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly List<ColorControl> _colorControls = new();
    private readonly ComboBox _savedThemeBox;
    private readonly TextBlock _savedThemeCount;
    private readonly TextBlock _angleValue;
    private readonly TextBlock _intensityValue;
    private readonly TextBlock _statusText;
    private readonly Slider _angleSlider;
    private readonly Slider _intensitySlider;
    private ThemePreferences _savedTheme;
    private ThemePreferences _workingTheme;
    private bool _updatingControls;

    public ThemeTemplatesWindow(Window owner, AppSettings settings, SettingsService settingsService)
    {
        Owner = owner;
        _settings = settings;
        _settingsService = settingsService;
        _savedTheme = ThemeService.Clone(settings.Theme);
        _workingTheme = ThemeService.Clone(_savedTheme);

        Title = "Afterline Themes";
        Width = 1180;
        Height = 760;
        MinWidth = 980;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(22, 20, 22, 18) };
        root.SetResourceReference(Panel.BackgroundProperty, "AfterlineAppGradient");
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Themes",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Choose a dark theme or build a gradient with a few simple controls.",
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        root.Children.Add(header);

        var workspace = new Grid();
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var settingsStack = new StackPanel();
        AddSectionHeading(settingsStack, "DEFAULT THEMES", "Afterline's ready-made dark appearances.");
        settingsStack.Children.Add(BuildPresetGrid(DefaultThemes));

        AddSectionHeading(settingsStack, "COLOR & GRADIENT THEMES", "A quick starting point for a more colorful shell.", 20);
        settingsStack.Children.Add(BuildPresetGrid(GradientThemes));

        AddSectionHeading(settingsStack, "TRY IT OUT", "Pick three colors, then adjust direction and intensity.", 20);
        settingsStack.Children.Add(BuildColorControl("Start color", () => _workingTheme.GradientStart, value => _workingTheme.GradientStart = value));
        settingsStack.Children.Add(BuildColorControl("Middle color", () => _workingTheme.GradientMiddle, value => _workingTheme.GradientMiddle = value));
        settingsStack.Children.Add(BuildColorControl("End color", () => _workingTheme.GradientEnd, value => _workingTheme.GradientEnd = value));

        _angleValue = CreateValueText();
        _angleSlider = BuildSlider(0, 360, 15);
        _angleSlider.ValueChanged += (_, _) => CustomControlsChanged();
        settingsStack.Children.Add(BuildSliderRow("Gradient direction", _angleSlider, _angleValue));

        _intensityValue = CreateValueText();
        _intensitySlider = BuildSlider(0, 100, 5);
        _intensitySlider.ValueChanged += (_, _) => CustomControlsChanged();
        settingsStack.Children.Add(BuildSliderRow("Color intensity", _intensitySlider, _intensityValue));

        var reset = new Button
        {
            Content = "Reset custom controls",
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        reset.Click += (_, _) => PreviewTheme(ThemeService.CreateDefault(), "Default controls restored.");
        settingsStack.Children.Add(reset);

        AddSectionHeading(settingsStack, "YOUR THEMES", "Save and reuse up to eight named themes.", 20);
        var savedHeading = new Grid();
        savedHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        savedHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _savedThemeCount = CreateValueText();
        Grid.SetColumn(_savedThemeCount, 1);
        savedHeading.Children.Add(_savedThemeCount);
        settingsStack.Children.Add(savedHeading);

        _savedThemeBox = new ComboBox
        {
            DisplayMemberPath = nameof(SavedThemePreset.Name),
            MinHeight = 34,
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _savedThemeBox.SelectionChanged += SavedThemeBox_SelectionChanged;
        settingsStack.Children.Add(_savedThemeBox);

        var savedButtons = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        savedButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        savedButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        savedButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var saveNamed = new Button { Content = "Save current as…", Padding = new Thickness(10, 7, 10, 7) };
        saveNamed.Click += (_, _) => SaveNamedTheme();
        savedButtons.Children.Add(saveNamed);
        var deleteSaved = new Button { Content = "Delete saved", Padding = new Thickness(10, 7, 10, 7) };
        deleteSaved.Click += (_, _) => DeleteSavedTheme();
        Grid.SetColumn(deleteSaved, 2);
        savedButtons.Children.Add(deleteSaved);
        settingsStack.Children.Add(savedButtons);

        var settingsScroll = new ScrollViewer
        {
            Content = settingsStack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 8, 0)
        };
        workspace.Children.Add(settingsScroll);

        var previewCard = CreateCard(18);
        var previewStack = new StackPanel();
        previewStack.Children.Add(new TextBlock
        {
            Text = "APPLICATION PREVIEW",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        previewStack.Children.Add(new TextBlock
        {
            Text = "A faithful dashboard sample using the same theme resources as Afterline.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 12)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        previewStack.Children.Add(BuildApplicationPreview());
        previewCard.Child = previewStack;
        Grid.SetColumn(previewCard, 2);
        workspace.Children.Add(previewCard);

        Grid.SetRow(workspace, 2);
        root.Children.Add(workspace);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _statusText = new TextBlock
        {
            Text = "Showing your active theme.",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 16, 0)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText");
        footer.Children.Add(_statusText);

        var footerButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var revert = new Button { Content = "Revert preview", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        revert.Click += (_, _) => PreviewTheme(_savedTheme, "Returned to your active theme.");
        footerButtons.Children.Add(revert);
        var apply = new Button
        {
            Content = "Apply",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(18, 7, 18, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        apply.Click += (_, _) => ApplyTheme();
        footerButtons.Children.Add(apply);
        var close = new Button { Content = "Close", Padding = new Thickness(12, 7, 12, 7) };
        close.Click += (_, _) => Close();
        footerButtons.Children.Add(close);
        Grid.SetColumn(footerButtons, 1);
        footer.Children.Add(footerButtons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        Closing += ThemeTemplatesWindow_Closing;
        RefreshSavedThemes();
        ThemeService.Apply(_workingTheme);
        ThemeService.ApplyWindow(this);
        RefreshCustomControls();
    }

    private static void AddSectionHeading(StackPanel parent, string title, string subtitle, double top = 0)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, top, 0, 3)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        parent.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
    }

    private UniformGrid BuildPresetGrid(IReadOnlyList<ThemePreset> presets)
    {
        var grid = new UniformGrid { Columns = 2 };
        foreach (ThemePreset preset in presets)
        {
            var button = new Button
            {
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 8, 8),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                ToolTip = preset.Description
            };
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var swatch = new Border
            {
                Width = 34,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Background = CreatePreviewGradient(preset.Theme)
            }.WithResource(Border.BorderBrushProperty, "Border");
            content.Children.Add(swatch);
            var label = new TextBlock
            {
                Text = preset.Name,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(label, 2);
            content.Children.Add(label);
            button.Content = content;
            button.Click += (_, _) => PreviewTheme(preset.Theme, $"Previewing {preset.Name}.");
            grid.Children.Add(button);
        }
        return grid;
    }

    private Border BuildColorControl(string label, Func<string> getter, Action<string> setter)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var value = CreateValueText();
        value.Margin = new Thickness(8, 0, 10, 0);
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        var swatch = new Border
        {
            Width = 32,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        }.WithResource(Border.BorderBrushProperty, "Border");
        swatch.MouseLeftButtonUp += (_, _) => ChooseColor(getter, setter);
        Grid.SetColumn(swatch, 2);
        row.Children.Add(swatch);
        _colorControls.Add(new ColorControl { Getter = getter, Setter = setter, Swatch = swatch, Value = value });
        var card = CreateCard(10);
        card.Margin = new Thickness(0, 0, 0, 7);
        card.Child = row;
        return card;
    }

    private static Slider BuildSlider(double minimum, double maximum, double tick)
        => new()
        {
            Minimum = minimum,
            Maximum = maximum,
            TickFrequency = tick,
            IsSnapToTickEnabled = false,
            Margin = new Thickness(0, 6, 0, 0)
        };

    private static Border BuildSliderRow(string label, Slider slider, TextBlock value)
    {
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new TextBlock { Text = label });
        Grid.SetColumn(value, 1);
        content.Children.Add(value);
        Grid.SetRow(slider, 1);
        Grid.SetColumnSpan(slider, 2);
        content.Children.Add(slider);
        var card = CreateCard(10);
        card.Margin = new Thickness(0, 0, 0, 7);
        card.Child = content;
        return card;
    }

    private FrameworkElement BuildApplicationPreview()
    {
        var shell = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), MinHeight = 490 };
        shell.SetResourceReference(Border.BackgroundProperty, "AfterlineAppGradient");
        shell.SetResourceReference(Border.BorderBrushProperty, "Border");

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(9, 0, 0, 9) };
        sidebar.SetResourceReference(Border.BackgroundProperty, "AfterlineSidebarGradient");
        var sidebarLayout = new Grid();
        sidebarLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebarLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var navigation = new StackPanel();
        navigation.Children.Add(new TextBlock { Text = "⌁", FontSize = 32, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) }.WithResource(TextBlock.ForegroundProperty, "Accent"));
        AddPreviewSection(navigation, "OVERVIEW", ("\uE80F", "Dashboard", "AfterlineNavOverview"));
        AddPreviewSection(navigation, "CHAT", ("\uE8BD", "Live Chat", "AfterlineNavChat"));
        AddPreviewSection(navigation, "LIBRARY",
            ("\uE721", "Search", "AfterlineNavLibrary"),
            ("\uE7B8", "Archive", "AfterlineNavLibrary"),
            ("\uE8A5", "Log Reader", "AfterlineNavLibrary"));
        AddPreviewSection(navigation, "IMAGE EDITOR",
            ("\uE70F", "Editor", "AfterlineNavCreate"),
            ("\uEB9F", "Gallery", "AfterlineNavCreate"));
        AddPreviewSection(navigation, "SYSTEM", ("\uE713", "Settings", "MutedText"), ("\uE771", "Themes", "Accent"));
        sidebarLayout.Children.Add(navigation);
        var updates = new Border { Padding = new Thickness(0, 10, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0) };
        updates.SetResourceReference(Border.BorderBrushProperty, "Border");
        var updateText = new StackPanel();
        updateText.Children.Add(new TextBlock { Text = "UPDATES", FontSize = 7.5 }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        updateText.Children.Add(new TextBlock { Text = "CANARY · Current", FontSize = 8.5, Margin = new Thickness(0, 5, 0, 0) }.WithResource(TextBlock.ForegroundProperty, "Accent"));
        updates.Child = updateText;
        Grid.SetRow(updates, 1);
        sidebarLayout.Children.Add(updates);
        sidebar.Child = sidebarLayout;
        root.Children.Add(sidebar);

        var page = new Grid { Margin = new Thickness(18) };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "Dashboard", FontSize = 22, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "FiveM capture and session overview", FontSize = 10.5, Margin = new Thickness(0, 3, 0, 0) }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        page.Children.Add(title);

        var summary = new UniformGrid { Columns = 3 };
        summary.Children.Add(BuildPreviewCard("FIVEM", "Not detected", "No active server connection"));
        summary.Children.Add(BuildPreviewCard("CURRENT SESSION", "0 messages", "No active session"));
        summary.Children.Add(BuildPreviewCard("AUTOSAVE", "Protected", "Waiting for first chat message", true));
        Grid.SetRow(summary, 2);
        page.Children.Add(summary);

        var lower = new Grid();
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var sessions = BuildPreviewCard("RECENT SESSIONS", "Recent sessions", "Completed chatlogs from the last 7 days.");
        lower.Children.Add(sessions);
        var projects = BuildPreviewCard("EDITOR", "Recent Editor projects", "Double-click a project to continue editing.");
        Grid.SetColumn(projects, 2);
        lower.Children.Add(projects);
        Grid.SetRow(lower, 4);
        page.Children.Add(lower);
        Grid.SetColumn(page, 1);
        root.Children.Add(page);
        shell.Child = root;
        return shell;
    }

    private static void AddPreviewSection(StackPanel parent, string heading, params (string Glyph, string Label, string Color)[] items)
    {
        parent.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 7.5,
            Margin = new Thickness(0, parent.Children.Count == 1 ? 0 : 10, 0, 5)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        foreach ((string glyph, string label, string color) in items)
        {
            var text = new TextBlock { FontSize = 10, Margin = new Thickness(2, 3, 0, 3) };
            var icon = new System.Windows.Documents.Run(glyph) { FontFamily = new FontFamily("Segoe MDL2 Assets") };
            icon.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, color);
            text.Inlines.Add(icon);
            text.Inlines.Add(new System.Windows.Documents.Run($"  {label}"));
            parent.Children.Add(text);
        }
    }

    private static Border BuildPreviewCard(string eyebrow, string title, string subtitle, bool success = false)
    {
        var card = CreateCard(13);
        card.Margin = new Thickness(0, 0, 8, 0);
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = eyebrow, FontSize = 8 }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 7, 0, 0)
        }.WithResource(TextBlock.ForegroundProperty, success ? "Success" : "Text"));
        content.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 9,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        }.WithResource(TextBlock.ForegroundProperty, "MutedText"));
        card.Child = content;
        return card;
    }

    private static Border CreateCard(double padding)
    {
        var card = new Border { Padding = new Thickness(padding), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Panel");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        return card;
    }

    private static TextBlock CreateValueText()
        => new() { FontFamily = new FontFamily("Consolas"), FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };

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
        setter($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        RebuildFromCustomControls("Previewing custom gradient.");
    }

    private void CustomControlsChanged()
    {
        if (_updatingControls) return;
        _workingTheme.GradientAngle = _angleSlider.Value;
        _workingTheme.GradientIntensity = _intensitySlider.Value;
        RebuildFromCustomControls("Previewing custom gradient.");
    }

    private void RebuildFromCustomControls(string status)
    {
        _workingTheme = ThemeService.CreateGradientTheme(
            _workingTheme.GradientStart,
            _workingTheme.GradientMiddle,
            _workingTheme.GradientEnd,
            _workingTheme.GradientAngle,
            _workingTheme.GradientIntensity);
        ApplyWorkingPreview(status);
    }

    private void PreviewTheme(ThemePreferences theme, string status)
    {
        _workingTheme = ThemeService.Clone(theme);
        ApplyWorkingPreview(status);
    }

    private void ApplyWorkingPreview(string status)
    {
        ThemeService.Apply(_workingTheme);
        ThemeService.ApplyWindow(this);
        RefreshCustomControls();
        SetStatus(status, "MutedText");
    }

    private void RefreshCustomControls()
    {
        _updatingControls = true;
        foreach (ColorControl control in _colorControls)
        {
            string value = control.Getter();
            control.Value.Text = value;
            control.Value.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
            control.Swatch.Background = new SolidColorBrush(ThemeService.ParseColor(value, Colors.Transparent));
        }
        _angleSlider.Value = _workingTheme.GradientAngle;
        _intensitySlider.Value = _workingTheme.GradientIntensity;
        _angleValue.Text = $"{Math.Round(_workingTheme.GradientAngle):0}°";
        _intensityValue.Text = $"{Math.Round(_workingTheme.GradientIntensity):0}%";
        _angleValue.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        _intensityValue.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        _updatingControls = false;
    }

    private void SavedThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || _savedThemeBox.SelectedItem is not SavedThemePreset preset) return;
        PreviewTheme(preset.Theme, $"Previewing {preset.Name}.");
    }

    private void RefreshSavedThemes(string? selectName = null)
    {
        _updatingControls = true;
        _savedThemeBox.ItemsSource = null;
        _savedThemeBox.ItemsSource = _settings.CustomThemes;
        _savedThemeCount.Text = $"{_settings.CustomThemes.Count}/{ThemeService.MaximumCustomThemes} saved";
        _savedThemeCount.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        if (!string.IsNullOrWhiteSpace(selectName))
        {
            _savedThemeBox.SelectedItem = _settings.CustomThemes.FirstOrDefault(
                preset => string.Equals(preset.Name, selectName, StringComparison.OrdinalIgnoreCase));
        }
        _updatingControls = false;
    }

    private void SaveNamedTheme()
    {
        var prompt = new TextPromptWindow(
            "Save Custom Theme",
            $"Name this custom theme. You can keep up to {ThemeService.MaximumCustomThemes} named themes:")
        {
            Owner = this
        };
        if (prompt.ShowDialog() != true) return;

        try
        {
            if (!ThemeService.TrySaveCustomTheme(_settings, prompt.Value, _workingTheme, out _, out string message))
            {
                SetStatus(message, "Warning");
                return;
            }
            _settingsService.Save(_settings);
            RefreshSavedThemes(ThemeService.NormalizeCustomThemeName(prompt.Value));
            SetStatus(message, "Success");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save a named custom theme.", ex);
            SetStatus("Unable to save the custom theme.", "Warning");
        }
    }

    private void DeleteSavedTheme()
    {
        if (_savedThemeBox.SelectedItem is not SavedThemePreset preset)
        {
            SetStatus("Select a saved theme first.", "Warning");
            return;
        }
        MessageBoxResult choice = MessageBox.Show(this, $"Delete the saved theme ‘{preset.Name}’?", "Delete Theme", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes) return;
        _settings.CustomThemes.Remove(preset);
        _settingsService.Save(_settings);
        RefreshSavedThemes();
        SetStatus("Saved theme deleted.", "Success");
    }

    private void ApplyTheme()
    {
        try
        {
            _settings.Theme = ThemeService.Clone(_workingTheme);
            _settingsService.Save(_settings);
            _savedTheme = ThemeService.Clone(_settings.Theme);
            ThemeService.Apply(_savedTheme);
            ThemeService.ApplyWindow(this);
            SetStatus("Theme applied and saved locally.", "Success");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to apply theme settings.", ex);
            SetStatus("Unable to save the theme.", "Warning");
        }
    }

    private void SetStatus(string text, string resource)
    {
        _statusText.Text = text;
        _statusText.SetResourceReference(TextBlock.ForegroundProperty, resource);
    }

    private void ThemeTemplatesWindow_Closing(object? sender, CancelEventArgs e)
        => ThemeService.Apply(_savedTheme);

    private static LinearGradientBrush CreatePreviewGradient(ThemePreferences theme)
    {
        ThemePreferences normalized = ThemeService.Normalize(theme);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(ThemeService.ParseColor(normalized.GradientStart, Colors.Black), 0));
        brush.GradientStops.Add(new GradientStop(ThemeService.ParseColor(normalized.GradientMiddle, Colors.Black), 0.5));
        brush.GradientStops.Add(new GradientStop(ThemeService.ParseColor(normalized.GradientEnd, Colors.Black), 1));
        return brush;
    }
}

internal static class ThemeElementExtensions
{
    public static T WithResource<T>(this T element, DependencyProperty property, object resourceKey)
        where T : FrameworkElement
    {
        element.SetResourceReference(property, resourceKey);
        return element;
    }
}
