using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _liveChatFeaturesInitialized;
    private CheckBox? _showOocChatCheck;
    private CheckBox? _showIcChatCheckV076;
    private CheckBox? _roleplayColorsCheck;
    private CheckBox? _showLiveTimestampsCheck;
    private CheckBox? _autoScrollLiveCheck;
    private TextBlock? _liveActionStatus;
    private TextBlock? _serverStatusText;
    private TextBlock? _serverClockStatusText;
    private Button? _serverTimeZoneButton;

    internal void EnsureLiveChatEnhancements()
    {
        if (_liveChatFeaturesInitialized || !IsLoaded) return;
        _liveChatFeaturesInitialized = true;

        ChatEntry.ColorizeRoleplayLines = _settings.ColorizeRoleplayLines;
        ChatEntry.ShowTimestamps = _settings.ShowLiveTimestamps;

        ConfigureLiveChatItemTemplate();
        ConfigureLiveChatFiltering();
        ConfigureLiveChatContextMenu();
        ConfigureLiveChatHeader();
        ConfigureServerStatus();
        EnsurePotentialDuplicateReviewUi();
        ConfigureSearchClearBehavior();
        EnsureNotificationAndUpdateUi();

        AddSettingHelp(
            ReconnectBox,
            "How long Afterline shows reconnect grace after leaving a server. The current server chatlog is saved and finalized immediately; reconnecting later reopens that server's log for the same day.");
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

    private void ConfigureLiveChatFiltering()
    {
        LiveChatList.Items.Filter = item =>
            item is not ChatEntry entry || ShouldShowLiveChatEntryV076(entry);

        LiveMessages.CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(UpdateVisibleLiveCount));

        UpdateVisibleLiveCount();
    }

    private void UpdateVisibleLiveCount()
    {
        if (!IsLoaded) return;
        int count = LiveChatList.Items.Count;
        LiveCountText.Text = $"{count:N0} message{(count == 1 ? string.Empty : "s")} shown";
    }

    private void ConfigureLiveChatContextMenu()
    {
        LiveChatList.PreviewMouseRightButtonDown += LiveChatList_PreviewMouseRightButtonDown;

        var menu = new ContextMenu
        {
            Background = (System.Windows.Media.Brush)FindResource("Raised"),
            Foreground = (System.Windows.Media.Brush)FindResource("Text"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2)
        };

        var copyHeader = new TextBlock
        {
            Text = "Copy line",
            Foreground = (System.Windows.Media.Brush)FindResource("Text")
        };

        var copyItem = new MenuItem
        {
            Header = copyHeader,
            Foreground = (System.Windows.Media.Brush)FindResource("Text"),
            Background = (System.Windows.Media.Brush)FindResource("Raised"),
            Padding = new Thickness(12, 7, 12, 7)
        };
        copyItem.Click += CopyLiveChatLine_Click;
        menu.Items.Add(copyItem);
        LiveChatList.ContextMenu = menu;
    }

    private void LiveChatList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(LiveChatList, source) is not ListBoxItem item) return;

        item.IsSelected = true;
        item.Focus();
    }

    private void CopyLiveChatLine_Click(object sender, RoutedEventArgs e)
    {
        if (LiveChatList.SelectedItem is not ChatEntry entry) return;

        try
        {
            Clipboard.SetText(entry.Display);
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Line copied to clipboard.";
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to copy Live Chat line to the clipboard.", ex);
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Unable to copy line.";
        }
    }

    private void ConfigureLiveChatHeader()
    {
        if (ShowLiveChatCheck.Parent is not Grid headerGrid) return;

        headerGrid.RowDefinitions.Clear();
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel? leftPanel = headerGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 0);

        headerGrid.Children.Remove(ShowLiveChatCheck);

        var optionsPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 190
        };
        Grid.SetColumn(optionsPanel, 1);

        optionsPanel.Children.Add(new TextBlock
        {
            Text = "DISPLAY",
            FontSize = 10,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 5)
        });

        _showOocChatCheck = new CheckBox
        {
            Content = "Show OOC chat",
            IsChecked = _settings.ShowOocChat,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5),
            ToolTip = "Show or hide OOC, private-message, INFO, MAPPING, SUCCESS, ERROR and related gameplay-status lines. Capture and archived logs are never affected."
        };
        _showOocChatCheck.Checked += ShowOocChatCheck_Changed;
        _showOocChatCheck.Unchecked += ShowOocChatCheck_Changed;

        _showIcChatCheckV076 = new CheckBox
        {
            Content = "Show IC chat",
            IsChecked = _settings.ShowIcChat,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5),
            ToolTip = "Show or hide in-character chat. Turn this off to focus on OOC and Server Staff messages. Capture and archived logs are never affected."
        };
        _showIcChatCheckV076.Checked += ShowIcChatCheckV076_Changed;
        _showIcChatCheckV076.Unchecked += ShowIcChatCheckV076_Changed;

        _roleplayColorsCheck = new CheckBox
        {
            Content = "RP line colors",
            IsChecked = _settings.ColorizeRoleplayLines,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        };
        _roleplayColorsCheck.Checked += RoleplayColorsCheck_Changed;
        _roleplayColorsCheck.Unchecked += RoleplayColorsCheck_Changed;

        _showLiveTimestampsCheck = new CheckBox
        {
            Content = "Show timestamps",
            IsChecked = _settings.ShowLiveTimestamps,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        };
        _showLiveTimestampsCheck.Checked += ShowLiveTimestampsCheck_Changed;
        _showLiveTimestampsCheck.Unchecked += ShowLiveTimestampsCheck_Changed;

        _autoScrollLiveCheck = new CheckBox
        {
            Content = "Auto-scroll",
            IsChecked = _settings.AutoScrollLiveChat,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5),
            ToolTip = "Keep Live Chat pinned to the newest message. Turn this off to scroll through earlier messages without being pulled back down."
        };
        _autoScrollLiveCheck.Checked += AutoScrollLiveCheck_Changed;
        _autoScrollLiveCheck.Unchecked += AutoScrollLiveCheck_Changed;

        ShowLiveChatCheck.VerticalAlignment = VerticalAlignment.Center;
        ShowLiveChatCheck.Margin = new Thickness(0, 0, 0, 5);

        optionsPanel.Children.Add(_showOocChatCheck);
        optionsPanel.Children.Add(_showIcChatCheckV076);
        optionsPanel.Children.Add(_roleplayColorsCheck);
        optionsPanel.Children.Add(_showLiveTimestampsCheck);
        optionsPanel.Children.Add(_autoScrollLiveCheck);
        optionsPanel.Children.Add(ShowLiveChatCheck);
        headerGrid.Children.Add(optionsPanel);

        if (leftPanel is null) return;

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var parseButton = new Button
        {
            Content = "Parse current chat",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 10, 6),
            ToolTip = "Imports messages currently retained by FiveM's chat UI. Messages no longer retained by the game cannot be recovered."
        };
        parseButton.Click += ParseCurrentChat_Click;

        var exportButton = new Button
        {
            Content = "Save copy to Downloads",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 10, 6),
            ToolTip = "Writes an independent copy of the current captured server session to your Downloads folder."
        };
        exportButton.Click += ExportCurrentLiveLog_Click;

        _liveActionStatus = new TextBlock
        {
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };

        actions.Children.Add(parseButton);
        actions.Children.Add(exportButton);
        actions.Children.Add(_liveActionStatus);
        Grid.SetRow(actions, 1);
        Grid.SetColumnSpan(actions, 2);
        headerGrid.Children.Add(actions);
    }

    private void ConfigureServerStatus()
    {
        _capture.ServerSessionChanged += Capture_ServerSessionChanged;
        _capture.ServerClockChanged += Capture_ServerClockChanged;

        if (FiveMStateText.Parent is StackPanel panel)
        {
            _serverStatusText = new TextBlock
            {
                Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            int index = panel.Children.IndexOf(FiveMStateText);
            panel.Children.Insert(Math.Min(index + 1, panel.Children.Count), _serverStatusText);

            _serverClockStatusText = new TextBlock
            {
                Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Insert(Math.Min(index + 2, panel.Children.Count), _serverClockStatusText);

            _serverTimeZoneButton = new Button
            {
                Content = "Set server time zone",
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Automatic detection is preferred. Use this only when the server does not expose a reliable timezone."
            };
            _serverTimeZoneButton.Click += SetServerTimeZone_Click;
            panel.Children.Insert(Math.Min(index + 3, panel.Children.Count), _serverTimeZoneButton);
        }

        UpdateServerStatus(_capture.CurrentServer);
    }

    private void Capture_ServerSessionChanged(object? sender, ServerSessionChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => UpdateServerStatus(e.Server)));
    }

    private void UpdateServerStatus(ServerSessionInfo? server)
    {
        if (_serverStatusText is null) return;

        if (server is null)
        {
            _serverStatusText.Text = "No active server connection";
            _serverStatusText.Foreground = (System.Windows.Media.Brush)FindResource("MutedText");
            UpdateServerClockStatus(null);
            return;
        }

        _serverStatusText.Text = server.HasFriendlyName
            ? $"Server: {server.DisplayName}"
            : "Connected to a server · name unavailable";
        _serverStatusText.Foreground = (System.Windows.Media.Brush)FindResource("Success");
        UpdateServerClockStatus(server);
    }

    private void Capture_ServerClockChanged(object? sender, EventArgs e)
        => _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => UpdateServerClockStatus(_capture.CurrentServer)));

    private void UpdateServerClockStatus(ServerSessionInfo? server)
    {
        if (_serverClockStatusText is null || _serverTimeZoneButton is null) return;

        _serverTimeZoneButton.Visibility = server is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (server is null)
        {
            _serverClockStatusText.Text = "Server time: UTC until a server is connected";
            return;
        }

        ServerClockResult clock = ServerTimeService.Resolve(
            _settings,
            server,
            DateTimeOffset.UtcNow);
        _serverClockStatusText.Text = $"Server time: {clock.ServerTime:HH:mm:ss} · {clock.Description}";
    }

    private void SetServerTimeZone_Click(object sender, RoutedEventArgs e)
    {
        ServerSessionInfo? server = _capture.CurrentServer;
        if (server is null) return;

        ServerClockResult current = ServerTimeService.Resolve(
            _settings,
            server,
            DateTimeOffset.UtcNow);
        var dialog = new ServerTimeZoneWindow(
            this,
            server.DisplayName,
            current.Description,
            ServerTimeService.GetManualTimeZoneId(_settings, server));
        if (dialog.ShowDialog() != true) return;

        ServerTimeService.SetManualTimeZone(
            _settings,
            server,
            dialog.SelectedTimeZoneId);
        try
        {
            _settingsService.Save(_settings);
            UpdateServerClockStatus(server);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save the server timezone override.", ex);
            System.Windows.MessageBox.Show(
                this,
                "Afterline could not save the server timezone setting.",
                "Server Time Zone",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void CheckFiveMConnection_Click(object sender, RoutedEventArgs e)
        => await RefreshFiveMConnectionAsync(sender as Button, reportToLiveChat: false);

    private async void RefreshLiveChat_Click(object sender, RoutedEventArgs e)
        => await RefreshFiveMConnectionAsync(sender as Button, reportToLiveChat: true);

    private async Task RefreshFiveMConnectionAsync(Button? actionButton, bool reportToLiveChat)
    {
        if (actionButton is not null) actionButton.IsEnabled = false;
        if (reportToLiveChat && _liveActionStatus is not null)
            _liveActionStatus.Text = "Refreshing the active FiveM chat…";
        else if (_serverStatusText is not null)
        {
            _serverStatusText.Text = "Checking the active FiveM connection…";
            _serverStatusText.Foreground = (System.Windows.Media.Brush)FindResource("MutedText");
        }

        try
        {
            int captured = await _capture.RefreshConnectionAsync();
            UpdateStatusUi();
            UpdateServerStatus(_capture.CurrentServer);
            LiveChatList.Items.Refresh();
            UpdateVisibleLiveCount();

            if (_liveActionStatus is not null)
            {
                _liveActionStatus.Text = captured == 0
                    ? "Connection refreshed · active chat is up to date."
                    : $"Connection refreshed · imported {captured:N0} new message{(captured == 1 ? string.Empty : "s")}.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusUi();
            string message = ex.InnerException?.Message ?? ex.Message;
            if (reportToLiveChat && _liveActionStatus is not null)
                _liveActionStatus.Text = "Unable to refresh active chat: " + message;
            if (_serverStatusText is not null)
            {
                _serverStatusText.Text = "Connection check failed: " + message;
                _serverStatusText.Foreground = (System.Windows.Media.Brush)FindResource("Warning");
            }
        }
        finally
        {
            if (actionButton is not null) actionButton.IsEnabled = true;
        }
    }

    private void ShowOocChatCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_showOocChatCheck is null) return;
        _settings.ShowOocChat = _showOocChatCheck.IsChecked == true;
        LiveChatList.Items.Refresh();
        UpdateVisibleLiveCount();
        SaveLivePresentationSettings();
    }

    private void ShowIcChatCheckV076_Changed(object sender, RoutedEventArgs e)
    {
        if (_showIcChatCheckV076 is null) return;
        _settings.ShowIcChat = _showIcChatCheckV076.IsChecked == true;
        LiveChatList.Items.Refresh();
        _liveChatView?.Refresh();
        UpdateVisibleLiveCount();
        SaveLivePresentationSettings();
    }

    private void RoleplayColorsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_roleplayColorsCheck is null) return;
        _settings.ColorizeRoleplayLines = _roleplayColorsCheck.IsChecked == true;
        ChatEntry.ColorizeRoleplayLines = _settings.ColorizeRoleplayLines;
        RefreshAutomaticChatColorPresentation();
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

    private void AutoScrollLiveCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_autoScrollLiveCheck is null) return;
        _settings.AutoScrollLiveChat = _autoScrollLiveCheck.IsChecked == true;
        if (AutoScrollCheck.IsChecked != _settings.AutoScrollLiveChat)
            AutoScrollCheck.IsChecked = _settings.AutoScrollLiveChat;
        if (_settings.AutoScrollLiveChat && LiveMessages.Count > 0)
            LiveChatList.ScrollIntoView(LiveMessages[^1]);
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

    private void ConfigureSearchClearBehavior()
    {
        SearchQueryBox.TextChanged += SearchQueryBox_TextChanged;
    }

    private void SearchQueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchQueryBox.Text)) return;

        SearchResults.Clear();
        SearchSummaryText.Text = "Enter a keyword to search your chatlogs.";
    }

    private async void ParseCurrentChat_Click(object sender, RoutedEventArgs e)
    {
        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Reading current chat…";
            int imported = await _capture.ParseCurrentChatAsync();
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = imported == 0
                    ? "Current chat is already up to date."
                    : $"Restored {imported:N0} cached/current message{(imported == 1 ? string.Empty : "s")}.";

            string? activePath = _journal.ActiveFile;
            if (activePath is not null)
            {
                IReadOnlyList<PotentialDuplicateCandidate> pending =
                    await _capture.ReadPotentialDuplicatesAsync(
                        activePath,
                        CancellationToken.None);
                if (pending.Count > 0)
                    await OfferPotentialDuplicateReviewAsync(activePath);
            }
        }
        catch (Exception ex)
        {
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Unable to parse current chat: " + ex.Message;
        }
        finally
        {
            if (actionButton is not null) actionButton.IsEnabled = true;
        }
    }

    private async void ExportCurrentLiveLog_Click(object sender, RoutedEventArgs e)
    {
        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            string downloads = GetDownloadsFolder();
            string path = await _journal.ExportCurrentLogAsync(
                _settings.ArchiveRoot,
                downloads,
                CancellationToken.None,
                ServerTimeService.Resolve(
                    _settings,
                    _capture.CurrentServer,
                    DateTimeOffset.UtcNow).ServerTime);
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = $"Saved {Path.GetFileName(path)} to Downloads.";
            ShowExportSuccessNotification(path);
        }
        catch (Exception ex)
        {
            if (_liveActionStatus is not null) _liveActionStatus.Text = "Unable to save log copy: " + ex.Message;
        }
        finally
        {
            if (actionButton is not null) actionButton.IsEnabled = true;
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
