using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _canaryUiFixesV3Initialized;
    private readonly DispatcherTimer _updateRefreshTimerCanaryV3 = new()
    {
        Interval = TimeSpan.FromMinutes(2)
    };
    private bool _updateRefreshBusyCanaryV3;
    private UpdateCheckResult? _availableUpdateCanaryV3;
    private string? _availableCanaryBuildCanaryV3;
    private string? _editorPrewarmedMediaCanaryV3;

    private void EnsureCanaryUiFixesV3()
    {
        if (_canaryUiFixesV3Initialized) return;
        _canaryUiFixesV3Initialized = true;

        ConfigureThemeCompliantMenusCanaryV3();
        RemoveObjectSelectionCanaryV3();
        SwapFullscreenPreviewCanaryV3();
        ConfigureCustomKeybindsCanaryV3();
        ConfigureSettingsKeybindPageCanaryV3();
        ConfigureEditorKeybindSettingsCanaryV3();
        CenterSettingsNavigationCanaryV3();
        ConfigureAutomaticUpdateStateCanaryV3();
        ConfigureEditorSliderPrewarmCanaryV3();
    }

    private void ConfigureThemeCompliantMenusCanaryV3()
    {
        var contextMenuStyle = new Style(typeof(ContextMenu));
        contextMenuStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Raised")));
        contextMenuStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Text")));
        contextMenuStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("Border")));
        contextMenuStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        contextMenuStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
        Application.Current.Resources[typeof(ContextMenu)] = contextMenuStyle;

        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Raised")));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Text")));
        itemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 14, 7)));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

        var template = new ControlTemplate(typeof(MenuItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Header")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;

        var highlighted = new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true
        };
        highlighted.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("ControlHover")));
        template.Triggers.Add(highlighted);

        var disabled = new Trigger
        {
            Property = UIElement.IsEnabledProperty,
            Value = false
        };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(disabled);

        itemStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
        Application.Current.Resources[typeof(MenuItem)] = itemStyle;
    }

    private void RemoveObjectSelectionCanaryV3()
    {
        if (_editorSelectionToolCanary == CanarySelectionTool.Object)
            DeactivateSelectionInteractionCanary();

        if (_editorToolPanels.TryGetValue("selection", out FrameworkElement? panel))
        {
            foreach (Button button in FindVisualChildrenCanary<Button>(panel).ToArray())
            {
                if (!string.Equals(button.Content?.ToString(), "Object Select", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (button.Parent is Panel parent)
                    parent.Children.Remove(button);
            }

            if (_editorObjectThresholdSliderCanary is not null)
            {
                DependencyObject? current = _editorObjectThresholdSliderCanary;
                while (current is FrameworkElement element && element.Parent is FrameworkElement parentElement)
                {
                    if (parentElement is StackPanel stack && stack == (panel as ScrollViewer)?.Content)
                    {
                        stack.Children.Remove(element);
                        break;
                    }
                    current = parentElement;
                }
            }

            foreach (TextBlock text in FindVisualChildrenCanary<TextBlock>(panel))
            {
                if (text.Text?.Contains("Object Select", StringComparison.OrdinalIgnoreCase) == true)
                {
                    text.Text = "Selections limit Filters & Adjustments to part of a still screenshot. Selected edges are outlined on the canvas. Use Rectangular Marquee, Lasso or Polygonal Lasso to define the area you want to edit.";
                }
            }
        }

        _editorObjectThresholdSliderCanary = null;
    }

    private void SwapFullscreenPreviewCanaryV3()
    {
        if (!_editorToolPanels.TryGetValue("export", out FrameworkElement? exportPanel))
            return;

        Button? preview = FindVisualChildrenCanary<Button>(exportPanel)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Full Screen Preview", StringComparison.OrdinalIgnoreCase));
        if (preview is null) return;

        preview.Click -= EditorFullscreenPreview_Click;
        preview.Content = "Full Screen Editor";
        preview.ToolTip = "Maximize the complete Editor workspace. Press Escape or use the X button to return.";
        preview.Click += (_, _) => ToggleEditorFullscreenCanary();
    }

    private void ConfigureCustomKeybindsCanaryV3()
    {
        PreviewKeyDown -= QolV050_PreviewKeyDown;
        PreviewKeyDown -= EditorHistoryKeyDownCanaryV2;
        PreviewKeyDown += CanaryKeybindPreviewKeyDownV3;
    }

    private async void CanaryKeybindPreviewKeyDownV3(object sender, KeyEventArgs e)
    {
        if (e.Handled || IsTextEditorFocusedV050()) return;

        bool editorVisible = _editorPage?.Visibility == Visibility.Visible;
        if (editorVisible && ShortcutMatchesCanaryV3(_settings.Editor.UndoKeybind, e))
        {
            UndoEditorHistoryCanaryV2();
            e.Handled = true;
            return;
        }
        if (editorVisible && ShortcutMatchesCanaryV3(_settings.Editor.RedoKeybind, e))
        {
            RedoEditorHistoryCanaryV2();
            e.Handled = true;
            return;
        }
        if (editorVisible && ShortcutMatchesCanaryV3(_settings.Editor.ExportKeybind, e))
        {
            EditorExportDefaultV060_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (editorVisible && ShortcutMatchesCanaryV3(_settings.Editor.FullscreenKeybind, e))
        {
            ToggleEditorFullscreenCanary();
            e.Handled = true;
            return;
        }
        if (editorVisible && ShortcutMatchesCanaryV3(_settings.Editor.RulerKeybind, e))
        {
            ToggleEditorRulersV068();
            e.Handled = true;
            return;
        }

        if (ShortcutMatchesCanaryV3(_settings.FindKeybind, e))
        {
            if (_logReaderPage?.Visibility == Visibility.Visible && _logReaderFindBoxV050 is not null)
                FocusFindV050(_logReaderFindBoxV050);
            else if (LivePage.Visibility == Visibility.Visible && _liveFindBoxV050 is not null)
                FocusFindV050(_liveFindBoxV050);
            else
            {
                ShowPage(SearchPage, "Search", "Search one or multiple terms across your chatlog folders");
                SearchQueryBox.Focus();
                SearchQueryBox.SelectAll();
            }
            e.Handled = true;
            return;
        }

        if (ShortcutMatchesCanaryV3(_settings.OpenLogKeybind, e))
        {
            e.Handled = true;
            var dialog = new OpenFileDialog
            {
                Title = "Open chatlog",
                Filter = "Text chatlogs (*.txt)|*.txt|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = Directory.Exists(_settings.ArchiveRoot) ? _settings.ArchiveRoot : string.Empty
            };
            if (dialog.ShowDialog(this) == true)
                await OpenLogInReaderAsync(dialog.FileName, null);
            return;
        }

        if (ShortcutMatchesCanaryV3(_settings.CopyContextKeybind, e))
        {
            if (_logReaderPage?.Visibility == Visibility.Visible && _logReaderList?.SelectedItems.Count > 0)
            {
                CopyLogReaderContext(5);
                e.Handled = true;
            }
            else if (LivePage.Visibility == Visibility.Visible && LiveChatList.SelectedItems.Count > 0)
            {
                CopyLiveContextV050(5);
                e.Handled = true;
            }
            return;
        }

        if (ShortcutMatchesCanaryV3(_settings.CopyKeybind, e))
        {
            if (_logReaderPage?.Visibility == Visibility.Visible && _logReaderList?.SelectedItems.Count > 0)
            {
                CopySelectedLogLinesV050();
                e.Handled = true;
            }
            else if (LivePage.Visibility == Visibility.Visible && LiveChatList.SelectedItems.Count > 0)
            {
                CopySelectedLiveLinesV050();
                e.Handled = true;
            }
        }
    }

    private static bool ShortcutMatchesCanaryV3(string? shortcut, KeyEventArgs e)
    {
        if (!TryParseShortcutCanaryV3(shortcut, out Key key, out ModifierKeys modifiers))
            return false;
        Key eventKey = e.Key == Key.System ? e.SystemKey : e.Key;
        return eventKey == key && Keyboard.Modifiers == modifiers;
    }

    private static bool TryParseShortcutCanaryV3(string? shortcut, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(shortcut)) return false;

        string[] parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Shift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Alt;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Windows;
            else
            {
                string keyName = part.Equals("Enter", StringComparison.OrdinalIgnoreCase) ? nameof(Key.Return)
                    : part.Equals("Esc", StringComparison.OrdinalIgnoreCase) ? nameof(Key.Escape)
                    : part;
                if (!Enum.TryParse(keyName, true, out key))
                    return false;
            }
        }
        return key != Key.None;
    }

    private static bool IsModifierKeyCanaryV3(Key key)
        => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private static string FormatShortcutCanaryV3(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(key switch
        {
            Key.Return => "Enter",
            Key.Escape => "Esc",
            _ => key.ToString()
        });
        return string.Join('+', parts);
    }

    private FrameworkElement CreateKeybindRowCanaryV3(
        string label,
        string description,
        Func<string> getter,
        Action<string> setter,
        string defaultValue)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        copy.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        });
        row.Children.Add(copy);

        var button = new Button
        {
            Content = getter(),
            MinHeight = 34,
            Padding = new Thickness(8, 5, 8, 5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Click, then press the shortcut you want. Delete or Backspace restores the default."
        };
        Grid.SetColumn(button, 2);
        row.Children.Add(button);

        bool capturing = false;
        button.Click += (_, _) =>
        {
            capturing = true;
            button.Content = "Press keys…";
            button.Focus();
        };
        button.PreviewKeyDown += (_, e) =>
        {
            if (!capturing) return;
            Key pressed = e.Key == Key.System ? e.SystemKey : e.Key;
            if (IsModifierKeyCanaryV3(pressed))
            {
                e.Handled = true;
                return;
            }
            if (pressed == Key.Escape)
            {
                capturing = false;
                button.Content = getter();
                e.Handled = true;
                return;
            }
            if (pressed is Key.Delete or Key.Back)
            {
                setter(defaultValue);
                _settingsService.Save(_settings);
                capturing = false;
                button.Content = defaultValue;
                e.Handled = true;
                return;
            }

            string value = FormatShortcutCanaryV3(pressed, Keyboard.Modifiers);
            setter(value);
            _settingsService.Save(_settings);
            capturing = false;
            button.Content = value;
            e.Handled = true;
        };
        button.LostKeyboardFocus += (_, _) =>
        {
            if (!capturing) return;
            capturing = false;
            button.Content = getter();
        };
        return row;
    }

    private void ConfigureSettingsKeybindPageCanaryV3()
    {
        if (_settingsSectionContentCanaryV2 is null || _settingsSectionButtonsCanaryV2.Count == 0)
            return;
        if (_settingsSectionsCanaryV2.ContainsKey("keybinds")) return;

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Click a shortcut button, then press the key combination you want. Shortcuts are saved immediately and work without restarting Afterline.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
        content.Children.Add(CreateKeybindRowCanaryV3("Find / Search", "Focus Find in Live Chat or Log Reader, otherwise open Search.", () => _settings.FindKeybind, v => _settings.FindKeybind = v, "Ctrl+F"));
        content.Children.Add(CreateKeybindRowCanaryV3("Open chatlog", "Open a chatlog file directly in Log Reader.", () => _settings.OpenLogKeybind, v => _settings.OpenLogKeybind = v, "Ctrl+O"));
        content.Children.Add(CreateKeybindRowCanaryV3("Copy selected", "Copy selected Live Chat or Log Reader lines.", () => _settings.CopyKeybind, v => _settings.CopyKeybind = v, "Ctrl+C"));
        content.Children.Add(CreateKeybindRowCanaryV3("Copy with context", "Copy the selected line with surrounding context.", () => _settings.CopyContextKeybind, v => _settings.CopyContextKeybind = v, "Ctrl+Shift+C"));

        var reset = new Button
        {
            Content = "Reset Application Keybinds",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 4, 0, 0)
        };
        reset.Click += (_, _) =>
        {
            _settings.FindKeybind = "Ctrl+F";
            _settings.OpenLogKeybind = "Ctrl+O";
            _settings.CopyKeybind = "Ctrl+C";
            _settings.CopyContextKeybind = "Ctrl+Shift+C";
            _settingsService.Save(_settings);
            RebuildSettingsKeybindPageCanaryV3();
        };
        content.Children.Add(reset);

        _settingsSectionsCanaryV2["keybinds"] = WrapSettingsSectionCanaryV2(content,
            "Keybinds",
            "Customize keyboard shortcuts used across Afterline.");

        Button? general = _settingsSectionButtonsCanaryV2.GetValueOrDefault("general");
        if (general?.Parent is StackPanel nav)
        {
            var keybindButton = CreateSettingsNavButtonCanaryV2("keybinds", "⌨", "Keybinds");
            int index = nav.Children.IndexOf(general);
            nav.Children.Insert(Math.Min(nav.Children.Count, index + 1), keybindButton);
            keybindButton.Click += (_, _) =>
                PageSubtitle.Text = "Customize application keyboard shortcuts";
        }
    }

    private void RebuildSettingsKeybindPageCanaryV3()
    {
        _settingsSectionsCanaryV2.Remove("keybinds");
        if (_settingsSectionButtonsCanaryV2.TryGetValue("keybinds", out Button? existing))
        {
            if (existing.Parent is Panel parent) parent.Children.Remove(existing);
            _settingsSectionButtonsCanaryV2.Remove("keybinds");
        }
        ConfigureSettingsKeybindPageCanaryV3();
        CenterSettingsNavigationCanaryV3();
        ShowSettingsSectionCanaryV2("keybinds");
        PageSubtitle.Text = "Customize application keyboard shortcuts";
    }

    private void ConfigureEditorKeybindSettingsCanaryV3()
    {
        if (!_editorToolPanels.TryGetValue("settings", out FrameworkElement? settingsPanel) ||
            settingsPanel is not ScrollViewer scroll || scroll.Content is not StackPanel content)
            return;

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock
        {
            Text = "EDITOR KEYBINDS",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        content.Children.Add(EditorSubtleNote("These shortcuts only apply while the Editor is open. Click a shortcut and press the replacement keys."));
        content.Children.Add(CreateKeybindRowCanaryV3("Export", "Export using the current output format.", () => _settings.Editor.ExportKeybind, v => _settings.Editor.ExportKeybind = v, "Ctrl+S"));
        content.Children.Add(CreateKeybindRowCanaryV3("Undo", "Undo the most recent committed Editor change.", () => _settings.Editor.UndoKeybind, v => _settings.Editor.UndoKeybind = v, "Ctrl+Z"));
        content.Children.Add(CreateKeybindRowCanaryV3("Redo", "Redo the most recently undone Editor change.", () => _settings.Editor.RedoKeybind, v => _settings.Editor.RedoKeybind = v, "Ctrl+Shift+Z"));
        content.Children.Add(CreateKeybindRowCanaryV3("Full Screen Editor", "Enter or leave the distraction-free Editor workspace.", () => _settings.Editor.FullscreenKeybind, v => _settings.Editor.FullscreenKeybind = v, "F11"));
    }

    private void CenterSettingsNavigationCanaryV3()
    {
        foreach (Button button in _settingsSectionButtonsCanaryV2.Values)
        {
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;

            if (button.Content is not Grid grid) continue;
            TextBlock[] blocks = grid.Children.OfType<TextBlock>().ToArray();
            if (blocks.Length < 2) continue;

            TextBlock icon = blocks.OrderBy(Grid.GetColumn).First();
            TextBlock label = blocks.OrderBy(Grid.GetColumn).Last();
            grid.Children.Remove(icon);
            grid.Children.Remove(label);

            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 8, 0);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.TextAlignment = TextAlignment.Center;

            var centered = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            centered.Children.Add(icon);
            centered.Children.Add(label);
            button.Content = centered;
        }
    }

    private void ConfigureAutomaticUpdateStateCanaryV3()
    {
        if (_checkUpdatesButton is null) return;

        _checkUpdatesButton.Click -= CheckForUpdates_Click;
        _checkUpdatesButton.Click -= ChannelAwareCheckForUpdatesV062_Click;
        _checkUpdatesButton.Click += InstallAvailableUpdateCanaryV3_Click;
        SetUpdateActionStateCanaryV3("Checking…", false);

        _updateRefreshTimerCanaryV3.Tick += async (_, _) => await RefreshUpdateStateCanaryV3Async();
        _updateRefreshTimerCanaryV3.Start();
        Activated += async (_, _) =>
        {
            if (!_updateRefreshBusyCanaryV3)
                await RefreshUpdateStateCanaryV3Async();
        };
        _ = RefreshUpdateStateCanaryV3Async();
    }

    private async Task RefreshUpdateStateCanaryV3Async()
    {
        if (_updateRefreshBusyCanaryV3 || _updateInstallInProgress) return;
        _updateRefreshBusyCanaryV3 = true;
        SetUpdateActionStateCanaryV3("Checking…", false);

        try
        {
            if (IsCanaryChannelV062())
            {
                CanaryUpdateCheckResult canary = await _canaryUpdateServiceV062.CheckAsync(CancellationToken.None);
                UpdateCheckResult result = canary.Release;
                string current = GetCurrentBuildVersion() + " Canary";
                string latest = string.IsNullOrWhiteSpace(result.LatestVersion)
                    ? "Unavailable"
                    : result.LatestVersion + " Canary";
                SetUpdateBuildLines(current, latest);

                if (!string.IsNullOrWhiteSpace(result.Error) || string.IsNullOrWhiteSpace(result.LatestVersion))
                {
                    _availableUpdateCanaryV3 = null;
                    _availableCanaryBuildCanaryV3 = null;
                    SetUpdateActionStateCanaryV3("Unavailable", false);
                    return;
                }

                string? installedBuild = GetCurrentCanaryBuildIdV062();
                bool available = string.IsNullOrWhiteSpace(installedBuild) ||
                                 string.IsNullOrWhiteSpace(canary.BuildId) ||
                                 !string.Equals(installedBuild, canary.BuildId, StringComparison.OrdinalIgnoreCase);
                if (available)
                {
                    _availableUpdateCanaryV3 = result;
                    _availableCanaryBuildCanaryV3 = canary.BuildId;
                    SetUpdateBuildLines(current, latest + " available");
                    SetUpdateActionStateCanaryV3("Update", true);
                }
                else
                {
                    _availableUpdateCanaryV3 = null;
                    _availableCanaryBuildCanaryV3 = null;
                    SetUpdateActionStateCanaryV3("Up to date", false);
                }
                return;
            }

            UpdateCheckResult stable = await _updateService.CheckAsync(CancellationToken.None);
            string currentStable = GetCurrentBuildVersion();
            string latestStable = string.IsNullOrWhiteSpace(stable.LatestVersion) ? "Unavailable" : stable.LatestVersion;
            SetUpdateBuildLines(currentStable, latestStable);
            bool stableAvailable = string.IsNullOrWhiteSpace(stable.Error) &&
                                   !string.IsNullOrWhiteSpace(stable.LatestVersion) &&
                                   UpdateService.IsNewer(stable.LatestVersion, currentStable);
            _availableUpdateCanaryV3 = stableAvailable ? stable : null;
            _availableCanaryBuildCanaryV3 = null;
            SetUpdateActionStateCanaryV3(stableAvailable ? "Update" : "Up to date", stableAvailable);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Automatic Canary update refresh failed.", ex);
            _availableUpdateCanaryV3 = null;
            _availableCanaryBuildCanaryV3 = null;
            SetUpdateBuildLines(IsCanaryChannelV062() ? GetCurrentBuildVersion() + " Canary" : GetCurrentBuildVersion(), "Unavailable");
            SetUpdateActionStateCanaryV3("Unavailable", false);
        }
        finally
        {
            _updateRefreshBusyCanaryV3 = false;
        }
    }

    private void SetUpdateActionStateCanaryV3(string text, bool enabled)
    {
        if (_checkUpdatesButton is null) return;
        _checkUpdatesButton.Content = text;
        _checkUpdatesButton.IsEnabled = enabled && !_updateInstallInProgress;
        _checkUpdatesButton.Opacity = _checkUpdatesButton.IsEnabled ? 1.0 : 0.55;
    }

    private async void InstallAvailableUpdateCanaryV3_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdateCanaryV3 is null || _updateInstallInProgress) return;
        UpdateCheckResult release = _availableUpdateCanaryV3;
        string current = GetCurrentBuildVersion() + (IsCanaryChannelV062() ? " Canary" : string.Empty);
        var window = new UpdateAvailableWindow(this, current, release);
        if (window.ShowDialog() != true || !window.InstallRequested) return;

        if (IsCanaryChannelV062())
        {
            string? oldBuild = _settings.InstalledCanaryBuild;
            _settings.UpdateChannel = "Canary";
            _settings.InstalledCanaryBuild = _availableCanaryBuildCanaryV3;
            _settingsService.Save(_settings);
            await InstallUpdateAsyncV060(release);
            _settings.InstalledCanaryBuild = oldBuild;
            _settingsService.Save(_settings);
        }
        else
        {
            await InstallUpdateAsyncV060(release);
        }
    }

    private void ConfigureEditorSliderPrewarmCanaryV3()
    {
        if (_editorPage is null) return;
        _editorPage.IsVisibleChanged += (_, _) =>
        {
            if (_editorPage.Visibility == Visibility.Visible)
                ScheduleEditorFilterPrewarmCanaryV3();
        };

        foreach (Button button in FindVisualChildrenCanary<Button>(_editorPage))
        {
            string text = button.Content?.ToString() ?? string.Empty;
            if (text.StartsWith("Load Image", StringComparison.OrdinalIgnoreCase))
                button.Click += (_, _) => ScheduleEditorFilterPrewarmCanaryV3();
        }
        ScheduleEditorFilterPrewarmCanaryV3();
    }

    private void ScheduleEditorFilterPrewarmCanaryV3()
    {
        Dispatcher.BeginInvoke(new Action(PrewarmEditorFiltersCanaryV3), DispatcherPriority.ApplicationIdle);
    }

    private void PrewarmEditorFiltersCanaryV3()
    {
        if (_editorBaseOriginal is null || EditorHasAnimatedGifV060) return;
        string identity = _editorLoadedMediaPath ?? $"{_editorBaseOriginal.PixelWidth}x{_editorBaseOriginal.PixelHeight}";
        if (string.Equals(identity, _editorPrewarmedMediaCanaryV3, StringComparison.Ordinal)) return;

        try
        {
            if (_editorSelectedImageLayerV067 is not null ||
                !EnsureCanaryFilterSource() ||
                _editorFilterCommittedCanary is null) return;
            // Do not perform an eager full-resolution pixel pass on the UI thread.
            // The source clone above is sufficient preparation; actual filtering is
            // debounced until the user stops moving a control.
            _editorPrewarmedMediaCanaryV3 = identity;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to prewarm Canary Editor filters.", ex);
        }
    }
}
