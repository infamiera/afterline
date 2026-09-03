using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

internal sealed class ThemeTemplatesWindow : Window
{
    private sealed class ThemeTemplate
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required ThemePreferences Theme { get; init; }
        public override string ToString() => Name;
    }

    private static readonly IReadOnlyList<ThemeTemplate> Templates = new ThemeTemplate[]
    {
        new()
        {
            Name = "Afterline Slate",
            Description = "Afterline's balanced blue-gray dark appearance.",
            Theme = new ThemePreferences()
        },
        new()
        {
            Name = "Midnight Violet",
            Description = "A subdued purple-black theme that keeps the same dark, editorial feel.",
            Theme = new ThemePreferences
            {
                Background = "#121019", Sidebar = "#0D0B12", Panel = "#1B1723", Raised = "#241F2E", Inset = "#17131E",
                Border = "#3B3149", Accent = "#9A7BD1", AccentHover = "#B092E6", ControlHover = "#30283D",
                PrimaryText = "#F3EEF8", SecondaryText = "#BDB2C8", ScrollbarTrack = "#211B2A", ScrollbarThumb = "#76668A",
                NavigationOverview = "#B092E6", NavigationChat = "#67D7CC", NavigationLibrary = "#E6B96F", NavigationCreate = "#D889C2"
            }
        },
        new()
        {
            Name = "Deep Ocean",
            Description = "Cool charcoal surfaces with a muted cyan-blue accent.",
            Theme = new ThemePreferences
            {
                Background = "#0E1518", Sidebar = "#0A1012", Panel = "#142025", Raised = "#1B2A30", Inset = "#101A1E",
                Border = "#294149", Accent = "#3FA7B8", AccentHover = "#64C2D0", ControlHover = "#22373E",
                PrimaryText = "#EDF7F8", SecondaryText = "#AAC3C8", ScrollbarTrack = "#17262B", ScrollbarThumb = "#557A83",
                NavigationOverview = "#6EA8E8", NavigationChat = "#58D6C5", NavigationLibrary = "#DDBB72", NavigationCreate = "#B894DD"
            }
        },
        new()
        {
            Name = "Carbon Ember",
            Description = "Near-black charcoal with restrained warm amber accents.",
            Theme = new ThemePreferences
            {
                Background = "#151312", Sidebar = "#0F0E0D", Panel = "#201B18", Raised = "#2B2420", Inset = "#191512",
                Border = "#44372F", Accent = "#D8874F", AccentHover = "#E9A06D", ControlHover = "#392D26",
                PrimaryText = "#F7F0EB", SecondaryText = "#C8B5A8", ScrollbarTrack = "#261F1B", ScrollbarThumb = "#806858",
                NavigationOverview = "#E09A66", NavigationChat = "#6FC9B6", NavigationLibrary = "#E5B56C", NavigationCreate = "#CC8AA4"
            }
        },
        new()
        {
            Name = "Graphite Rose",
            Description = "Neutral graphite surfaces with a soft rose accent for a warmer dark theme.",
            Theme = new ThemePreferences
            {
                Background = "#151416", Sidebar = "#100F11", Panel = "#1E1C20", Raised = "#28252A", Inset = "#19171B",
                Border = "#3C373F", Accent = "#D56C88", AccentHover = "#E888A0", ControlHover = "#342F36",
                PrimaryText = "#F5F1F2", SecondaryText = "#BFB6BA", ScrollbarTrack = "#232024", ScrollbarThumb = "#786E7B",
                NavigationOverview = "#8DA5E8", NavigationChat = "#67CCBC", NavigationLibrary = "#DDB46E", NavigationCreate = "#E888A0"
            }
        },
        new()
        {
            Name = "Black Cherry",
            Description = "Deep black-red surfaces with restrained cherry and rose highlights.",
            Theme = new ThemePreferences
            {
                Background = "#110B0E", Sidebar = "#0A0709", Panel = "#1A1014", Raised = "#26171D", Inset = "#140C10",
                Border = "#462630", Accent = "#C94F72", AccentHover = "#E06C8C", ControlHover = "#342028",
                PrimaryText = "#F8F0F3", SecondaryText = "#C6AEB6", ScrollbarTrack = "#25151B", ScrollbarThumb = "#815262",
                NavigationOverview = "#E06C8C", NavigationChat = "#63CBB9", NavigationLibrary = "#DEB06A", NavigationCreate = "#D77DA8"
            }
        }
    };

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private ThemePreferences _savedTheme;
    private readonly ComboBox _templateBox;
    private readonly ComboBox _customThemeBox;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _customThemeCountText;
    private readonly TextBlock _statusText;
    private bool _changingSelection;

    public bool CustomizeRequested { get; private set; }

    public ThemeTemplatesWindow(Window owner, AppSettings settings, SettingsService settingsService)
    {
        Owner = owner;
        _settings = settings;
        _settingsService = settingsService;
        _savedTheme = ThemeService.Clone(settings.Theme);

        Title = "Afterline Themes";
        Width = 1040;
        Height = 680;
        MinWidth = 900;
        MinHeight = 590;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(22, 20, 22, 18) };
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
            Text = "Choose a dark preset, preview it live, or build your own without changing captured chat colours.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var workspace = new Grid();
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });

        var templatesCard = CreateThemeCard("BUILT-IN THEMES", "Pick a starting point. Selection previews instantly.");
        var templatesContent = (StackPanel)templatesCard.Child;
        templatesContent.Children.Add(new TextBlock
        {
            Text = "Preset",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6)
        });

        _templateBox = new ComboBox
        {
            ItemsSource = Templates,
            DisplayMemberPath = nameof(ThemeTemplate.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 34
        };
        BindSelectorToThemeResources();
        _templateBox.SelectionChanged += TemplateBox_SelectionChanged;
        templatesContent.Children.Add(_templateBox);

        _descriptionText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
            MinHeight = 54
        };
        templatesContent.Children.Add(_descriptionText);

        var previewHint = new TextBlock
        {
            Text = "Previewing never saves automatically. Use the actions below when the theme feels right.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        templatesContent.Children.Add(previewHint);
        workspace.Children.Add(templatesCard);

        var previewCard = CreateThemeCard("LIVE PREVIEW", "A compact sample of the shell, navigation and controls.");
        ((StackPanel)previewCard.Child).Children.Add(BuildThemePreview());
        Grid.SetColumn(previewCard, 2);
        workspace.Children.Add(previewCard);

        var customCard = CreateThemeCard("YOUR THEMES", "Save and reuse up to eight named combinations.");
        var customContent = (StackPanel)customCard.Child;

        var customHeading = new Grid { Margin = new Thickness(0, 12, 0, 6) };
        customHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        customHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        customHeading.Children.Add(new TextBlock
        {
            Text = "Saved themes",
            FontWeight = FontWeights.SemiBold
        });
        _customThemeCountText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_customThemeCountText, 1);
        customHeading.Children.Add(_customThemeCountText);
        customContent.Children.Add(customHeading);

        _customThemeBox = new ComboBox
        {
            DisplayMemberPath = nameof(SavedThemePreset.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 34,
            ToolTip = $"Select one of up to {ThemeService.MaximumCustomThemes} locally saved custom themes to preview it."
        };
        _customThemeBox.SelectionChanged += CustomThemeBox_SelectionChanged;
        customContent.Children.Add(_customThemeBox);

        var customActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var useCustom = new Button
        {
            Content = "Use saved theme",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 6)
        };
        useCustom.Click += (_, _) => UseSelectedCustomTheme();
        customActions.Children.Add(useCustom);
        var saveCustom = new Button
        {
            Content = "Save current as…",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 6)
        };
        saveCustom.Click += (_, _) => SaveCurrentCustomTheme();
        customActions.Children.Add(saveCustom);
        var deleteCustom = new Button
        {
            Content = "Delete saved",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 6)
        };
        deleteCustom.Click += (_, _) => DeleteSelectedCustomTheme();
        customActions.Children.Add(deleteCustom);
        customContent.Children.Add(customActions);

        _statusText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        customContent.Children.Add(_statusText);
        Grid.SetColumn(customCard, 4);
        workspace.Children.Add(customCard);

        Grid.SetRow(workspace, 2);
        root.Children.Add(workspace);

        var footer = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var revert = new Button
        {
            Content = "Revert preview",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        revert.Click += (_, _) => RevertPreview();
        footer.Children.Add(revert);

        var use = new Button
        {
            Content = "Use template",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        use.Click += (_, _) => SaveSelectedTemplate(false);
        footer.Children.Add(use);

        var customizeTemplate = new Button
        {
            Content = "Use & customize",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        customizeTemplate.Click += (_, _) => SaveSelectedTemplate(true);
        footer.Children.Add(customizeTemplate);

        var customizeCurrent = new Button
        {
            Content = "Customize current",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        customizeCurrent.Click += (_, _) => CustomizeCurrentTheme();
        footer.Children.Add(customizeCurrent);

        var close = new Button { Content = "Close", Padding = new Thickness(12, 7, 12, 7) };
        close.Click += (_, _) => Close();
        footer.Children.Add(close);

        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);
        Closing += ThemeTemplatesWindow_Closing;
        RefreshCustomThemes();
        _templateBox.SelectedIndex = 0;
    }

    private Border CreateThemeCard(string title, string subtitle)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText")
        });
        content.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        return new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private FrameworkElement BuildThemePreview()
    {
        var shell = new Border
        {
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(12),
            MinHeight = 300
        };
        shell.SetResourceReference(Border.BackgroundProperty, "AfterlineAppGradient");
        shell.SetResourceReference(Border.BorderBrushProperty, "Border");

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var navigation = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(9) };
        navigation.SetResourceReference(Border.BackgroundProperty, "AfterlineSidebarGradient");
        var navigationItems = new StackPanel();
        navigationItems.Children.Add(CreatePreviewNavigation("\uE80F", "Dashboard", "AfterlineNavOverview"));
        navigationItems.Children.Add(CreatePreviewNavigation("\uE8BD", "Live Chat", "AfterlineNavChat"));
        navigationItems.Children.Add(CreatePreviewNavigation("\uE7B8", "Archive", "AfterlineNavLibrary"));
        navigationItems.Children.Add(CreatePreviewNavigation("\uE70F", "Editor", "AfterlineNavCreate"));
        navigation.Child = navigationItems;
        content.Children.Add(navigation);

        var page = new StackPanel();
        page.Children.Add(new TextBlock { Text = "Dashboard", FontSize = 18, FontWeight = FontWeights.SemiBold });
        page.Children.Add(new TextBlock
        {
            Text = "Theme preview",
            FontSize = 10.5,
            Margin = new Thickness(0, 3, 0, 12),
            Foreground = (Brush)FindResource("MutedText")
        });
        var sampleCard = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(12) };
        var sampleContent = new StackPanel();
        sampleContent.Children.Add(new TextBlock { Text = "Recent session", FontWeight = FontWeights.SemiBold });
        sampleContent.Children.Add(new TextBlock
        {
            Text = "Cards, text and controls update live.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 10.5,
            Margin = new Thickness(0, 4, 0, 10),
            Foreground = (Brush)FindResource("MutedText")
        });
        sampleContent.Children.Add(new Button
        {
            Content = "Open chatlog",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        });
        sampleCard.Child = sampleContent;
        page.Children.Add(sampleCard);
        Grid.SetColumn(page, 2);
        content.Children.Add(page);

        shell.Child = content;
        return shell;
    }

    private TextBlock CreatePreviewNavigation(string glyph, string label, string colorResource)
    {
        var text = new TextBlock { FontSize = 10.5, Margin = new Thickness(0, 0, 0, 12) };
        var icon = new System.Windows.Documents.Run(glyph)
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets")
        };
        icon.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, colorResource);
        text.Inlines.Add(icon);
        var caption = new System.Windows.Documents.Run($"  {label}");
        caption.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "MutedText");
        text.Inlines.Add(caption);
        return text;
    }

    private void TemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingSelection) return;
        if (_templateBox.SelectedItem is not ThemeTemplate template) return;
        _changingSelection = true;
        _customThemeBox.SelectedIndex = -1;
        _changingSelection = false;
        _descriptionText.Text = template.Description;
        ApplyPreviewTheme(template.Theme);
        SetStatus($"Previewing {template.Name}.", "MutedText");
    }

    private void CustomThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingSelection || _customThemeBox.SelectedItem is not SavedThemePreset preset) return;
        _changingSelection = true;
        _templateBox.SelectedIndex = -1;
        _changingSelection = false;
        _descriptionText.Text = $"Saved custom theme · {preset.Name}";
        ApplyPreviewTheme(preset.Theme);
        SetStatus($"Previewing {preset.Name}.", "MutedText");
    }

    private void RefreshCustomThemes(string? selectName = null)
    {
        _customThemeBox.ItemsSource = null;
        _customThemeBox.ItemsSource = _settings.CustomThemes;
        _customThemeCountText.Text = $"{_settings.CustomThemes.Count}/{ThemeService.MaximumCustomThemes} saved";
        if (!string.IsNullOrWhiteSpace(selectName))
        {
            _customThemeBox.SelectedItem = _settings.CustomThemes.FirstOrDefault(
                preset => string.Equals(preset.Name, selectName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void SaveCurrentCustomTheme()
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
            if (!ThemeService.TrySaveCustomTheme(
                    _settings,
                    prompt.Value,
                    _savedTheme,
                    out _,
                    out string message))
            {
                SetStatus(message, "Warning");
                return;
            }

            _settingsService.Save(_settings);
            string name = ThemeService.NormalizeCustomThemeName(prompt.Value);
            RefreshCustomThemes(name);
            SetStatus(message, "Success");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save a named custom theme.", ex);
            SetStatus("Unable to save the custom theme.", "Warning");
        }
    }

    private void UseSelectedCustomTheme()
    {
        if (_customThemeBox.SelectedItem is not SavedThemePreset preset)
        {
            SetStatus("Select a saved custom theme first.", "Warning");
            return;
        }

        try
        {
            _settings.Theme = ThemeService.Clone(preset.Theme);
            _settingsService.Save(_settings);
            _savedTheme = ThemeService.Clone(_settings.Theme);
            ApplyPreviewTheme(_savedTheme);
            SetStatus($"{preset.Name} is now the active theme.", "Success");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to apply a saved custom theme.", ex);
            SetStatus("Unable to use the selected custom theme.", "Warning");
        }
    }

    private void DeleteSelectedCustomTheme()
    {
        if (_customThemeBox.SelectedItem is not SavedThemePreset preset)
        {
            SetStatus("Select a saved custom theme first.", "Warning");
            return;
        }

        MessageBoxResult choice = MessageBox.Show(
            this,
            $"Delete the saved custom theme ‘{preset.Name}’?",
            "Delete Custom Theme",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes) return;

        try
        {
            _settings.CustomThemes.Remove(preset);
            _settingsService.Save(_settings);
            RefreshCustomThemes();
            ApplyPreviewTheme(_savedTheme);
            SetStatus("Custom theme deleted.", "Success");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to delete a saved custom theme.", ex);
            SetStatus("Unable to delete the custom theme.", "Warning");
        }
    }

    private void RevertPreview()
    {
        ApplyPreviewTheme(_savedTheme);
        SetStatus("Returned to your currently saved theme.", "MutedText");
    }

    private void CustomizeCurrentTheme()
    {
        ApplyPreviewTheme(_savedTheme);
        CustomizeRequested = true;
        Close();
    }

    private void SaveSelectedTemplate(bool customize)
    {
        if (_templateBox.SelectedItem is not ThemeTemplate template) return;

        try
        {
            _settings.Theme = ThemeService.Clone(template.Theme);
            _settingsService.Save(_settings);
            _savedTheme = ThemeService.Clone(_settings.Theme);
            ApplyPreviewTheme(_savedTheme);
            SetStatus($"{template.Name} saved locally.", "Success");

            if (customize)
            {
                CustomizeRequested = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save a theme template.", ex);
            SetStatus("Unable to save the selected template.", "Warning");
        }
    }

    private void ApplyPreviewTheme(ThemePreferences preferences)
    {
        ThemePreferences theme = ThemeService.Normalize(preferences);
        ThemeService.Apply(theme);
        ThemeService.ApplyWindow(this);

        // Keep the selector bound to live theme resources instead of assigning
        // one-off brushes. WPF can freeze brushes from the shared ComboBox style;
        // a light-theme preview may then leave the selector using stale colors.
        // Dynamic resource references survive resource replacement and update as
        // soon as a different template is previewed.
        BindSelectorToThemeResources();
        _customThemeBox.SetResourceReference(Control.BackgroundProperty, "Raised");
        _customThemeBox.SetResourceReference(Control.ForegroundProperty, "Text");
        _customThemeBox.SetResourceReference(Control.BorderBrushProperty, "Border");
        _templateBox.ApplyTemplate();
        _templateBox.InvalidateMeasure();
        _templateBox.InvalidateArrange();
        _templateBox.InvalidateVisual();
    }

    private void BindSelectorToThemeResources()
    {
        _templateBox.SetResourceReference(Control.BackgroundProperty, "Raised");
        _templateBox.SetResourceReference(Control.ForegroundProperty, "Text");
        _templateBox.SetResourceReference(Control.BorderBrushProperty, "Border");
    }

    private void SetStatus(string text, string resourceKey)
    {
        _statusText.Text = text;
        _statusText.Foreground = (Brush)FindResource(resourceKey);
    }

    private void ThemeTemplatesWindow_Closing(object? sender, CancelEventArgs e)
    {
        ThemeService.Apply(_savedTheme);
    }
}
