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
            Name = "Afterline Default",
            Description = "The original dark blue-gray Afterline appearance.",
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
                PrimaryText = "#F3EEF8", SecondaryText = "#BDB2C8"
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
                PrimaryText = "#EDF7F8", SecondaryText = "#AAC3C8"
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
                PrimaryText = "#F7F0EB", SecondaryText = "#C8B5A8"
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
                PrimaryText = "#F5F1F2", SecondaryText = "#BFB6BA"
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
        Width = 640;
        Height = 660;
        MinWidth = 580;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
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
            Text = "Pick a ready-made appearance, preview it instantly, or use it as the starting point for your own theme.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(18)
        };

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Template",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
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
        content.Children.Add(_templateBox);

        _descriptionText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        };
        content.Children.Add(_descriptionText);

        var previewHint = new TextBlock
        {
            Text = "Selecting a template only previews it. Nothing is saved until you choose Use template or Use & customize.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 0)
        };
        content.Children.Add(previewHint);

        var customHeading = new Grid { Margin = new Thickness(0, 22, 0, 6) };
        customHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        customHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        customHeading.Children.Add(new TextBlock
        {
            Text = "Saved custom themes",
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
        content.Children.Add(customHeading);

        _customThemeBox = new ComboBox
        {
            DisplayMemberPath = nameof(SavedThemePreset.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 34,
            ToolTip = "Select one of up to three locally saved custom themes to preview it."
        };
        _customThemeBox.SelectionChanged += CustomThemeBox_SelectionChanged;
        content.Children.Add(_customThemeBox);

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
        content.Children.Add(customActions);

        _statusText = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        content.Children.Add(_statusText);

        card.Child = content;
        Grid.SetRow(card, 2);
        root.Children.Add(card);

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
            "Name this custom theme. You can keep up to three named themes:")
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
