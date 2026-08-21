using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _liveChatFeaturesInitialized;
    private CheckBox? _roleplayColorsCheck;
    private CheckBox? _showLiveTimestampsCheck;
    private TextBlock? _liveActionStatus;

    internal void EnsureLiveChatEnhancements()
    {
        if (_liveChatFeaturesInitialized || !IsLoaded) return;
        _liveChatFeaturesInitialized = true;

        ChatEntry.ColorizeRoleplayLines = _settings.ColorizeRoleplayLines;
        ChatEntry.ShowTimestamps = _settings.ShowLiveTimestamps;

        ConfigureLiveChatItemTemplate();
        ConfigureLiveChatHeader();
        AddSettingHelp(
            ReconnectBox,
            "How long Afterline keeps a disconnected session open for a quick reconnect. Logs are checkpointed immediately when FiveM closes regardless of this value.");
        AddSettingHelp(
            ProcessingBox,
            "How often Afterline refreshes the archive/search index in the background. Chat capture itself is written to disk immediately and is not delayed by this setting.");
    }

    private void ConfigureLiveChatItemTemplate()
    {
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatEntry.Display)));
        textFactory.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(ChatEntry.Foreground)));
        textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        textFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        LiveChatList.ItemTemplate = new DataTemplate(typeof(ChatEntry)) { VisualTree = textFactory };
    }

    private void ConfigureLiveChatHeader()
    {
        if (ShowLiveChatCheck.Parent is not Grid headerGrid) return;

        StackPanel? leftPanel = headerGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 0);

        headerGrid.Children.Remove(ShowLiveChatCheck);

        var optionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(optionsPanel, 1);

        _roleplayColorsCheck = new CheckBox
        {
            Content = "RP line colors",
            IsChecked = _settings.ColorizeRoleplayLines,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        _roleplayColorsCheck.Checked += RoleplayColorsCheck_Changed;
        _roleplayColorsCheck.Unchecked += RoleplayColorsCheck_Changed;

        _showLiveTimestampsCheck = new CheckBox
        {
            Content = "Show timestamps",
            IsChecked = _settings.ShowLiveTimestamps,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        _showLiveTimestampsCheck.Checked += ShowLiveTimestampsCheck_Changed;
        _showLiveTimestampsCheck.Unchecked += ShowLiveTimestampsCheck_Changed;

        ShowLiveChatCheck.VerticalAlignment = VerticalAlignment.Center;
        ShowLiveChatCheck.Margin = new Thickness(0);

        optionsPanel.Children.Add(_roleplayColorsCheck);
        optionsPanel.Children.Add(_showLiveTimestampsCheck);
        optionsPanel.Children.Add(ShowLiveChatCheck);
        headerGrid.Children.Add(optionsPanel);

        if (leftPanel is null) return;

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var parseButton = new Button
        {
            Content = "Parse current chat",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Imports messages currently retained by FiveM's chat UI. Messages no longer retained by the game cannot be recovered."
        };
        parseButton.Click += ParseCurrentChat_Click;

        var exportButton = new Button
        {
            Content = "Save copy to Downloads",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = "Writes an independent copy of the current captured login to your Downloads folder."
        };
        exportButton.Click += ExportCurrentLiveLog_Click;

        _liveActionStatus = new TextBlock
        {
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        actions.Children.Add(parseButton);
        actions.Children.Add(exportButton);
        actions.Children.Add(_liveActionStatus);
        leftPanel.Children.Add(actions);
    }

    private void RoleplayColorsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_roleplayColorsCheck is null) return;
        _settings.ColorizeRoleplayLines = _roleplayColorsCheck.IsChecked == true;
        ChatEntry.ColorizeRoleplayLines = _settings.ColorizeRoleplayLines;
        LiveChatList.Items.Refresh();
        SaveLivePresentationSettings();
    }

    private void ShowLiveTimestampsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_showLiveTimestampsCheck is null) return;
        _settings.ShowLiveTimestamps = _showLiveTimestampsCheck.IsChecked == true;
        ChatEntry.ShowTimestamps = _settings.ShowLiveTimestamps;
        LiveChatList.Items.Refresh();
        SaveLivePresentationSettings();
    }

    private void SaveLivePresentationSettings()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to persist Live Chat presentation settings.", ex);
        }
    }

    private async void ParseCurrentChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        try
        {
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Reading current chat…";
            int imported = await _capture.ParseCurrentChatAsync();
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = imported == 0
                    ? "Current chat is already up to date."
                    : $"Imported {imported:N0} available message{(imported == 1 ? string.Empty : "s")}.";
        }
        catch (Exception ex)
        {
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Unable to parse current chat: " + ex.Message;
        }
        finally
        {
            if (sender is Button button) button.IsEnabled = true;
        }
    }

    private async void ExportCurrentLiveLog_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        try
        {
            string downloads = GetDownloadsFolder();
            string path = await _journal.ExportCurrentLogAsync(_settings.ArchiveRoot, downloads, CancellationToken.None);
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = $"Saved {Path.GetFileName(path)} to Downloads.";
        }
        catch (Exception ex)
        {
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Unable to save log copy: " + ex.Message;
        }
        finally
        {
            if (sender is Button button) button.IsEnabled = true;
        }
    }

    private static string GetDownloadsFolder()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloads = Path.Combine(profile, "Downloads");
        Directory.CreateDirectory(downloads);
        return downloads;
    }

    private void AddSettingHelp(ComboBox comboBox, string text)
    {
        if (comboBox.Parent is not Grid grid ||
            grid.Children.OfType<TextBlock>().Any(x => Equals(x.Tag, "AfterlineSettingHelp")))
            return;

        if (grid.RowDefinitions.Count == 0)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var help = new TextBlock
        {
            Text = text,
            Tag = "AfterlineSettingHelp",
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 2)
        };
        Grid.SetRow(help, 1);
        Grid.SetColumn(help, 0);
        Grid.SetColumnSpan(help, 3);
        grid.Children.Add(help);
    }
}
