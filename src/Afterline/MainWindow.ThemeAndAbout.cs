using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _themeAndAboutInitialized;
    private Button? _infoFooterButton;

    private void EnsureThemeAndAbout()
    {
        if (_themeAndAboutInitialized) return;
        _themeAndAboutInitialized = true;

        ThemeService.Apply(_settings.Theme);
        AddThemeCreatorCard();
        AddInfoFooterButton();
        EnsureStyledServerLabel();
    }

    private void AddThemeCreatorCard()
    {
        StackPanel? settingsStack = FindSettingsStackPanel();
        if (settingsStack is null || settingsStack.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "AfterlineThemeCreatorCard")))
            return;

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = "Themes",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = "Choose a ready-made theme template or safely customize interface and text colors yourself. Layout and roleplay chat colors are kept separate.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 18, 0)
        });
        content.Children.Add(text);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var templates = new Button
        {
            Content = "Templates",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        templates.Click += (_, _) => OpenThemeTemplates();
        actions.Children.Add(templates);

        var open = new Button
        {
            Content = "Customize",
            Padding = new Thickness(12, 7, 12, 7)
        };
        open.Click += (_, _) => OpenThemeCreator();
        actions.Children.Add(open);

        Grid.SetColumn(actions, 1);
        content.Children.Add(actions);

        var card = new Border
        {
            Tag = "AfterlineThemeCreatorCard",
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 14),
            Child = content
        };

        int insertAt = Math.Max(0, settingsStack.Children.Count - 1);
        settingsStack.Children.Insert(insertAt, card);
    }

    private StackPanel? FindSettingsStackPanel()
    {
        if ((object)SettingsPage is ScrollViewer scroll && scroll.Content is StackPanel direct)
            return direct;

        if ((object)SettingsPage is ContentControl contentControl && contentControl.Content is StackPanel contentStack)
            return contentStack;

        return FindFirstStackPanel(SettingsPage);
    }

    private static StackPanel? FindFirstStackPanel(DependencyObject root)
    {
        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            return null;
        }

        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is StackPanel stack) return stack;
            StackPanel? nested = FindFirstStackPanel(child);
            if (nested is not null) return nested;
        }

        return null;
    }

    private void OpenThemeTemplates()
    {
        var window = new ThemeTemplatesWindow(this, _settings, _settingsService);
        window.ShowDialog();
        ThemeService.Apply(_settings.Theme);

        if (window.CustomizeRequested)
            OpenThemeCreator();
    }

    private void OpenThemeCreator()
    {
        var window = new ThemeEditorWindow(this, _settings, _settingsService);
        window.ShowDialog();
        ThemeService.Apply(_settings.Theme);
    }

    private void AddInfoFooterButton()
    {
        if (BottomStatusText.Parent is not Grid footerGrid || _infoFooterButton is not null) return;

        while (footerGrid.ColumnDefinitions.Count < 4)
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (SettingsNav.Parent == footerGrid)
            Grid.SetColumn(SettingsNav, 3);

        _infoFooterButton = new Button
        {
            Tag = "AfterlineInfoButton",
            Content = "\uE946",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            Width = 30,
            Height = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(10, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "About Afterline"
        };
        _infoFooterButton.Click += (_, _) => new AboutWindow(this).ShowDialog();

        Grid.SetColumn(_infoFooterButton, 2);
        footerGrid.Children.Add(_infoFooterButton);
    }

    private void EnsureStyledServerLabel()
    {
        _capture.ServerSessionChanged += Theme_ServerSessionChanged;
        ApplyStyledServerLabel(_capture.CurrentServer);
    }

    private void Theme_ServerSessionChanged(object? sender, ServerSessionChangedEventArgs e)
        => Dispatcher.Invoke(() => ApplyStyledServerLabel(e.Server));

    private void ApplyStyledServerLabel(ServerSessionInfo? server)
    {
        if (_serverStatusText is null || server is null) return;

        _serverStatusText.Inlines.Clear();
        _serverStatusText.Inlines.Add(new Run("Server:")
        {
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Text")
        });
        _serverStatusText.Inlines.Add(new Run(server.HasFriendlyName
            ? $" {server.DisplayName}"
            : " name unavailable")
        {
            Foreground = (Brush)FindResource(server.HasFriendlyName ? "Success" : "MutedText")
        });
    }
}
