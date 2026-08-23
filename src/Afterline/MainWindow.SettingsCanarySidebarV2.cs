using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _settingsCanarySidebarV2Initialized;
    private ContentControl? _settingsSectionContentCanaryV2;
    private readonly Dictionary<string, Button> _settingsSectionButtonsCanaryV2 = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameworkElement> _settingsSectionsCanaryV2 = new(StringComparer.OrdinalIgnoreCase);
    private Grid? _settingsLayoutRootCanaryV2;

    private void EnsureSettingsCanarySidebarV2()
    {
        if (_settingsCanarySidebarV2Initialized || SettingsPage.Content is not StackPanel original)
            return;

        _settingsCanarySidebarV2Initialized = true;
        var general = new StackPanel();
        var recovery = new StackPanel();
        var failsafe = new StackPanel();
        var canary = new StackPanel();

        foreach (UIElement child in original.Children.Cast<UIElement>().ToArray())
        {
            original.Children.Remove(child);
            if (child is Border card)
            {
                string title = GetSettingsCardTitleCanaryV2(card);
                card.Margin = new Thickness(0, 0, 0, 10);
                if (title.Equals("Recovery Center", StringComparison.OrdinalIgnoreCase))
                    recovery.Children.Add(card);
                else if (title.Equals("Raw Capture Failsafe", StringComparison.OrdinalIgnoreCase))
                    failsafe.Children.Add(card);
                else if (title.Equals("Update channel", StringComparison.OrdinalIgnoreCase) ||
                         title.Equals("Canary Branch", StringComparison.OrdinalIgnoreCase))
                {
                    RenameSettingsCardCanaryV2(card, "Canary Branch");
                    canary.Children.Add(card);
                }
                else
                    general.Children.Add(card);
            }
            else
            {
                general.Children.Add(child);
            }
        }

        if (recovery.Children.Count == 0)
            recovery.Children.Add(CreateEmptySettingsNoticeCanaryV2("Recovery Center is not available in this build."));
        if (failsafe.Children.Count == 0)
            failsafe.Children.Add(CreateEmptySettingsNoticeCanaryV2("Raw Capture Failsafe is not available in this build."));
        if (canary.Children.Count == 0)
            canary.Children.Add(CreateEmptySettingsNoticeCanaryV2("Canary Branch controls are not available yet."));

        _settingsSectionsCanaryV2["general"] = WrapSettingsSectionCanaryV2(general,
            "General",
            "Startup & FiveM, Capture & Processing, and Chatlog Storage.");
        _settingsSectionsCanaryV2["recovery"] = WrapSettingsSectionCanaryV2(recovery,
            "Recovery Center",
            "Replay cached sessions and inspect recovery state.");
        _settingsSectionsCanaryV2["failsafe"] = WrapSettingsSectionCanaryV2(failsafe,
            "Raw Capture Failsafe",
            "Recover the latest pre-parse FiveM chat snapshot.");
        _settingsSectionsCanaryV2["canary"] = WrapSettingsSectionCanaryV2(canary,
            "Canary Branch",
            "Opt into experimental builds or return to Stable.");

        var root = new Grid { MinHeight = 500 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(182) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var navigationCard = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var nav = new StackPanel();
        nav.Children.Add(new TextBlock
        {
            Text = "SETTINGS",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(7, 4, 7, 9)
        });
        nav.Children.Add(CreateSettingsNavButtonCanaryV2("general", "⚙", "General"));
        nav.Children.Add(CreateSettingsNavButtonCanaryV2("recovery", "↺", "Recovery Center"));
        nav.Children.Add(CreateSettingsNavButtonCanaryV2("failsafe", "⛨", "Raw Capture Failsafe"));
        nav.Children.Add(CreateSettingsNavButtonCanaryV2("canary", "◈", "Canary Branch"));
        navigationCard.Child = nav;
        root.Children.Add(navigationCard);

        _settingsSectionContentCanaryV2 = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(_settingsSectionContentCanaryV2, 2);
        root.Children.Add(_settingsSectionContentCanaryV2);

        _settingsLayoutRootCanaryV2 = root;
        SettingsPage.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        SettingsPage.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        SettingsPage.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        SettingsPage.VerticalContentAlignment = VerticalAlignment.Stretch;
        SettingsPage.Content = root;
        SettingsPage.SizeChanged += (_, _) =>
        {
            if (_settingsLayoutRootCanaryV2 is not null)
                _settingsLayoutRootCanaryV2.Height = Math.Max(460, SettingsPage.ActualHeight - 2);
        };
        SettingsPage.IsVisibleChanged += (_, _) =>
        {
            if (SettingsPage.Visibility == Visibility.Visible && _settingsSectionContentCanaryV2?.Content is null)
                ShowSettingsSectionCanaryV2("general");
        };

        ShowSettingsSectionCanaryV2("general");
    }

    private Button CreateSettingsNavButtonCanaryV2(string key, string icon, string label)
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(new TextBlock
        {
            Text = icon,
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        var text = new TextBlock
        {
            Text = label,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        var button = new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(7, 7, 7, 7),
            Margin = new Thickness(0, 0, 0, 5),
            MinHeight = 38,
            ToolTip = label
        };
        button.Click += (_, _) => ShowSettingsSectionCanaryV2(key);
        _settingsSectionButtonsCanaryV2[key] = button;
        return button;
    }

    private FrameworkElement WrapSettingsSectionCanaryV2(StackPanel content, string title, string subtitle)
    {
        var host = new StackPanel();
        host.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(2, 0, 0, 2)
        });
        host.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 10.5,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 0, 10)
        });
        foreach (UIElement child in content.Children.Cast<UIElement>().ToArray())
        {
            content.Children.Remove(child);
            host.Children.Add(child);
        }

        return new ScrollViewer
        {
            Content = host,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 8, 0)
        };
    }

    private void ShowSettingsSectionCanaryV2(string key)
    {
        if (_settingsSectionContentCanaryV2 is null || !_settingsSectionsCanaryV2.TryGetValue(key, out FrameworkElement? section))
            return;

        _settingsSectionContentCanaryV2.Content = section;
        Brush accent = (Brush)FindResource("Accent");
        Brush raised = (Brush)FindResource("Raised");
        foreach ((string entryKey, Button button) in _settingsSectionButtonsCanaryV2)
            button.Background = string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase) ? accent : raised;

        if (SettingsPage.Visibility == Visibility.Visible)
        {
            PageSubtitle.Text = key switch
            {
                "general" => "Startup, capture, processing and chatlog storage preferences",
                "recovery" => "Recovery Center and cached session tools",
                "failsafe" => "Raw Capture Failsafe status and recovery tools",
                "canary" => "Experimental Canary Branch update controls",
                _ => "Afterline settings"
            };
        }
    }

    private static string GetSettingsCardTitleCanaryV2(Border card)
    {
        if (card.Child is not DependencyObject root) return string.Empty;
        return FindTextBlocksCanaryV2(root)
            .Select(text => text.Text?.Trim() ?? string.Empty)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? string.Empty;
    }

    private static void RenameSettingsCardCanaryV2(Border card, string title)
    {
        if (card.Child is not DependencyObject root) return;
        TextBlock? heading = FindTextBlocksCanaryV2(root).FirstOrDefault(text => text.FontSize >= 16);
        if (heading is not null) heading.Text = title;
    }

    private Border CreateEmptySettingsNoticeCanaryV2(string message)
    {
        return new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Child = new TextBlock
            {
                Text = message,
                Foreground = (Brush)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static IEnumerable<TextBlock> FindTextBlocksCanaryV2(DependencyObject root)
    {
        int count;
        try { count = VisualTreeHelper.GetChildrenCount(root); }
        catch { yield break; }
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text) yield return text;
            foreach (TextBlock nested in FindTextBlocksCanaryV2(child))
                yield return nested;
        }
    }
}
