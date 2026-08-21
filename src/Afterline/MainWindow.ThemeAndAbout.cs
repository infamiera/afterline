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
    private Button? _themeFooterButton;

    private void EnsureThemeAndAbout()
    {
        if (_themeAndAboutInitialized) return;
        _themeAndAboutInitialized = true;

        ThemeService.Apply(_settings.Theme);
        AddFooterUtilityButtons();
        EnsureProfileUi();
        EnsureStyledServerLabel();
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

    private void AddFooterUtilityButtons()
    {
        if (BottomStatusText.Parent is not Grid footerGrid) return;

        while (footerGrid.ColumnDefinitions.Count < 5)
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (SettingsNav.Parent == footerGrid)
        {
            Grid.SetColumn(SettingsNav, 4);
            SettingsNav.Margin = new Thickness(6, 0, 0, 0);
        }

        if (_infoFooterButton is null)
        {
            _infoFooterButton = CreateFooterIconButton("\uE946", "About Afterline");
            _infoFooterButton.Margin = new Thickness(10, 0, 0, 0);
            _infoFooterButton.Click += (_, _) => new AboutWindow(this).ShowDialog();
            Grid.SetColumn(_infoFooterButton, 2);
            footerGrid.Children.Add(_infoFooterButton);
        }

        if (_themeFooterButton is null)
        {
            _themeFooterButton = CreateFooterIconButton("\uE771", "Themes");
            _themeFooterButton.Margin = new Thickness(6, 0, 0, 0);
            _themeFooterButton.Click += (_, _) => OpenThemeTemplates();
            Grid.SetColumn(_themeFooterButton, 3);
            footerGrid.Children.Add(_themeFooterButton);
        }
    }

    private Button CreateFooterIconButton(string glyph, string tooltip)
    {
        return new Button
        {
            Content = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            Width = 30,
            Height = 28,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = tooltip
        };
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
