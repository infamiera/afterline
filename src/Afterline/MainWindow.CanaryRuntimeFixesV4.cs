using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Afterline.Services;
using Forms = System.Windows.Forms;

namespace Afterline;

public partial class MainWindow
{
    private bool _canaryRuntimeFixesV4Initialized;
    private Popup? _editorOpenMenuPopupCanaryV4;

    private void EnsureCanaryRuntimeFixesV4()
    {
        if (_canaryRuntimeFixesV4Initialized) return;
        _canaryRuntimeFixesV4Initialized = true;

        RebuildEditorTaskbarCanaryV4();
        RebuildSelectionPanelCanaryV4();
        RebuildExportPanelCanaryV4();
        RebuildEditorSettingsPanelCanaryV4();
        RebuildApplicationKeybindSettingsCanaryV4();
        CenterSettingsNavigationCanaryV4();
        ConfigureUpdaterHandoffCanaryV4();
        ConfigureEditorPrewarmCanaryV4();
    }

    private void RebuildEditorTaskbarCanaryV4()
    {
        if (_editorPage is null) return;
        Border? header = _editorPage.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (header is null) return;

        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var menus = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("File",
            ("Open Image / GIF…", OpenEditorMediaWithPrewarmCanaryV4),
            ("Import Chat Text…", () => EditorImportText_Click(this, new RoutedEventArgs())),
            ("Copy Current Frame", () => EditorCopyImageV060_Click(this, new RoutedEventArgs())),
            ("Export PNG…", () => EditorExportPngV060_Click(this, new RoutedEventArgs())),
            ("Export GIF…", () => EditorExportGifV060_Click(this, new RoutedEventArgs())),
            ("Remove Media", () =>
            {
                EditorRemoveMediaV060_Click(this, new RoutedEventArgs());
                Dispatcher.BeginInvoke(new Action(ResetCanaryFilterSource), DispatcherPriority.Background);
            })));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("Image",
            ("Image & Canvas", () => ShowEditorToolPanel("image", true)),
            ("Adjust Crop…", () => EditorAdjustCrop_ClickV060(this, new RoutedEventArgs())),
            ("Reset Crop", () => EditorResetCrop_ClickV060(this, new RoutedEventArgs())),
            ("Rotate Left", () => RunEditorTransformWithHistoryCanaryV2("rotate-left")),
            ("Rotate Right", () => RunEditorTransformWithHistoryCanaryV2("rotate-right")),
            ("Flip Horizontal", () => RunEditorTransformWithHistoryCanaryV2("flip-h")),
            ("Flip Vertical", () => RunEditorTransformWithHistoryCanaryV2("flip-v"))));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("Filter",
            ("Filters & Adjustments", () =>
            {
                PrepareEditorFiltersCanaryV4();
                ShowEditorToolPanel("filters", true);
            }),
            ("Apply Changes", ApplyFilterWithHistoryCanaryV2),
            ("Revert Preview", RevertCanaryFilterPreview),
            ("Save Current Filter…", SaveCurrentFilterPresetCanaryV2)));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("View",
            ("Fit Canvas", FitEditorPreviewToWindow),
            ("Zoom 100%", () => { _editorFitZoom = false; SetEditorZoom(1.0); }),
            ("Chat & Font", () => ShowEditorToolPanel("chat", true)),
            ("Selection Tools", () => ShowEditorToolPanel("selection", true)),
            ("Full Screen Editor", ToggleEditorFullscreenCanary)));

        menus.Children.Add(CreateEditorMenuButtonCanaryV4("Help",
            ("Editor Shortcuts", () => new CanaryEditorShortcutsWindow(this).ShowDialog()),
            ("About Afterline", () => new AboutWindow(this).ShowDialog())));

        bar.Children.Add(menus);

        var label = new TextBlock
        {
            Text = "CANARY EDITOR",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0)
        };
        Grid.SetColumn(label, 1);
        bar.Children.Add(label);

        header.Background = (Brush)FindResource("Panel");
        header.BorderBrush = (Brush)FindResource("Border");
        header.BorderThickness = new Thickness(1);
        header.Padding = new Thickness(6, 4, 6, 4);
        header.Child = bar;
        if (_editorPage.RowDefinitions.Count > 1)
            _editorPage.RowDefinitions[1].Height = new GridLength(5);
    }

    private Button CreateEditorMenuButtonCanaryV4(string title, params (string Label, Action Action)[] items)
    {
        var button = new Button
        {
            Content = title,
            Height = 29,
            MinWidth = 46,
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("Text"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = $"{title} menu"
        };

        button.Click += (_, _) => OpenEditorPopupMenuCanaryV4(button, items);
        return button;
    }

    private void OpenEditorPopupMenuCanaryV4(Button owner, IEnumerable<(string Label, Action Action)> items)
    {
        if (_editorOpenMenuPopupCanaryV4 is not null)
        {
            _editorOpenMenuPopupCanaryV4.IsOpen = false;
            _editorOpenMenuPopupCanaryV4 = null;
        }

        var stack = new StackPanel { Margin = new Thickness(3) };
        var popup = new Popup
        {
            PlacementTarget = owner,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade
        };

        foreach ((string label, Action action) in items)
        {
            var text = new TextBlock
            {
                Text = label,
                Foreground = (Brush)FindResource("Text"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            var item = new Border
            {
                Child = text,
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 7, 18, 7),
                CornerRadius = new CornerRadius(5),
                Cursor = Cursors.Hand,
                MinWidth = 200
            };
            item.MouseEnter += (_, _) => item.Background = (Brush)FindResource("Panel");
            item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
            item.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                popup.IsOpen = false;
                _editorOpenMenuPopupCanaryV4 = null;
                action();
            };
            stack.Children.Add(item);
        }

        popup.Child = new Border
        {
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(1),
            Child = stack
        };
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_editorOpenMenuPopupCanaryV4, popup))
                _editorOpenMenuPopupCanaryV4 = null;
        };

        _editorOpenMenuPopupCanaryV4 = popup;
        popup.IsOpen = true;
    }

    private void OpenEditorMediaWithPrewarmCanaryV4()
    {
        EditorLoadMediaV060_Click(this, new RoutedEventArgs());
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ResetCanaryFilterSource();
            PrepareEditorFiltersCanaryV4();
        }), DispatcherPriority.Background);
    }

    private void RebuildSelectionPanelCanaryV4()
    {
        DeactivateSelectionInteractionCanary();
        if (_editorSelectionOverlayCanary is not null)
        {
            _editorSelectionOverlayCanary.RemoveHandler(
                Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(ObjectSelectionMouseDownRefinedCanaryV2));
            _editorSelectionOverlayCanary.RemoveHandler(
                Mouse.PreviewMouseMoveEvent,
                new MouseEventHandler(ObjectSelectionMouseMoveRefinedCanaryV2));
        }
        ClearObjectHoverPreviewCanaryV2();
        if (_editorObjectHoverImageCanaryV2?.Parent is Panel hoverParent)
            hoverParent.Children.Remove(_editorObjectHoverImageCanaryV2);
        _editorObjectHoverImageCanaryV2 = null;
        _editorObjectThresholdSliderCanary = null;

        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Selections limit Filters & Adjustments to part of a still screenshot. Selected edges are outlined on the canvas."));

        var tools = new WrapPanel();
        tools.Children.Add(CreateSelectionButtonCanary("Rectangular", CanarySelectionTool.Rectangular,
            "Drag a rectangular marquee around the area you want to edit."));
        tools.Children.Add(CreateSelectionButtonCanary("Lasso", CanarySelectionTool.Lasso,
            "Hold the left mouse button and draw a freehand selection."));
        tools.Children.Add(CreateSelectionButtonCanary("Polygonal", CanarySelectionTool.Polygonal,
            "Click points around the subject. Double-click or press Enter to close the polygon."));
        content.Children.Add(tools);

        var actions = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
        actions.Children.Add(CreateSmallEditorButton("Select All", (_, _) => SelectAllCanary()));
        actions.Children.Add(CreateSmallEditorButton("Invert", (_, _) => InvertSelectionCanary()));
        actions.Children.Add(CreateSmallEditorButton("Clear", (_, _) => ClearSelectionCanary()));
        content.Children.Add(actions);
        content.Children.Add(EditorSubtleNote(
            "Selection borders and snapping guides are editor-only overlays and are never included in exported screenshots."));

        _editorToolPanels["selection"] = WrapEditorToolPanel(content);
    }

    private void RebuildExportPanelCanaryV4()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Export uses the crop and exact output size selected in Image & Canvas. Full Screen Editor expands the entire workspace rather than opening a separate preview window."));

        var copy = CreateSmallEditorButton("Copy Current Frame", EditorCopyImageV060_Click);
        copy.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Children.Add(copy);

        var fullscreen = CreateSmallEditorButton("Full Screen Editor", (_, _) => ToggleEditorFullscreenCanary());
        fullscreen.HorizontalAlignment = HorizontalAlignment.Stretch;
        fullscreen.ToolTip = "Maximize the complete Editor workspace. Press Escape or use the X button to leave full screen.";
        content.Children.Add(fullscreen);

        var exportPng = CreateSmallEditorButton("Export PNG", EditorExportPngV060_Click);
        exportPng.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Children.Add(exportPng);

        _editorExportGifButton = CreateSmallEditorButton("Export GIF", EditorExportGifV060_Click);
        _editorExportGifButton.Style = (Style)FindResource("PrimaryButton");
        _editorExportGifButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _editorExportGifButton.IsEnabled = EditorHasAnimatedGifV060;
        content.Children.Add(_editorExportGifButton);
        content.Children.Add(EditorSubtleNote(
            "PNG captures the currently displayed frame. GIF preserves animation and applies the same edit settings to every frame."));

        _editorToolPanels["export"] = WrapEditorToolPanel(content);
    }

    private void RebuildEditorSettingsPanelCanaryV4()
    {
        var content = new StackPanel();
        content.Children.Add(EditorHelpText(
            "Editor settings are stored locally. Image-specific selections and edits are not saved as defaults."));

        content.Children.Add(new TextBlock
        {
            Text = "PROJECTS FOLDER",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        var projectFolderRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        projectFolderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        projectFolderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
        projectFolderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var projectFolder = new TextBox
        {
            Text = GetEditorProjectsFolderV070(createDirectory: false),
            IsReadOnly = true,
            MinHeight = 32,
            Padding = new Thickness(7, 5, 7, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "New and existing Afterline Editor projects open from this folder by default."
        };
        projectFolderRow.Children.Add(projectFolder);
        var browseProjects = CreateSmallEditorButton("Browse…", (_, _) =>
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Choose where Afterline stores Editor projects",
                SelectedPath = Directory.Exists(projectFolder.Text)
                    ? projectFolder.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                return;

            Directory.CreateDirectory(dialog.SelectedPath);
            _settings.Editor.ProjectsFolder = dialog.SelectedPath;
            projectFolder.Text = dialog.SelectedPath;
            _settingsService.Save(_settings);
            SetEditorStatus("Editor projects folder updated.");
        });
        browseProjects.MinWidth = 72;
        Grid.SetColumn(browseProjects, 2);
        projectFolderRow.Children.Add(browseProjects);
        content.Children.Add(projectFolderRow);
        content.Children.Add(EditorSubtleNote(
            "Defaults to Documents\\Afterline Projects. You can still choose a different location in the Save dialog."));

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(BuildEditorAutosaveSettingsV159());

        content.Children.Add(CreateEditorDivider());

        var save = new Button
        {
            Content = "Save Editor Settings",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            ToolTip = "Save reusable Editor controls and output preferences."
        };
        save.Click += EditorSavePreferences_Click;
        content.Children.Add(save);

        var reset = new Button
        {
            Content = "Reset Editor Controls",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 7, 0, 0),
            ToolTip = "Reset reusable Editor controls without unloading the current screenshot."
        };
        reset.Click += EditorResetPreferences_Click;
        content.Children.Add(reset);

        content.Children.Add(CreateEditorDivider());
        var fullscreen = new Button
        {
            Content = "Enter Full Screen Editor",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 7, 10, 7),
            ToolTip = "Maximize the Editor workspace. Press Escape or use the X button to leave full screen."
        };
        fullscreen.Click += (_, _) => ToggleEditorFullscreenCanary();
        content.Children.Add(fullscreen);

        content.Children.Add(CreateEditorDivider());
        content.Children.Add(new TextBlock
        {
            Text = "EDITOR KEYBINDS",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        content.Children.Add(EditorSubtleNote(
            "Click a shortcut field, then press the desired key combination. Delete or Backspace restores its default."));
        content.Children.Add(CreateKeybindEditorRowCanaryV4("Export", "Export the current result.",
            () => _settings.Editor.ExportKeybind, value => _settings.Editor.ExportKeybind = value, "Ctrl+S"));
        content.Children.Add(CreateKeybindEditorRowCanaryV4("Undo", "Undo the latest Editor change.",
            () => _settings.Editor.UndoKeybind, value => _settings.Editor.UndoKeybind = value, "Ctrl+Z"));
        content.Children.Add(CreateKeybindEditorRowCanaryV4("Redo", "Redo the latest reverted Editor change.",
            () => _settings.Editor.RedoKeybind, value => _settings.Editor.RedoKeybind = value, "Ctrl+Shift+Z"));
        content.Children.Add(CreateKeybindEditorRowCanaryV4("Full Screen Editor", "Enter or leave the full Editor workspace.",
            () => _settings.Editor.FullscreenKeybind, value => _settings.Editor.FullscreenKeybind = value, "F11"));
        content.Children.Add(CreateKeybindEditorRowCanaryV4("Canvas Rulers", "Show or hide the pixel rulers around the canvas.",
            () => _settings.Editor.RulerKeybind, value => _settings.Editor.RulerKeybind = value, "R"));

        _editorToolPanels["settings"] = WrapEditorToolPanel(content);
    }

    private string GetEditorProjectsFolderV070(bool createDirectory)
    {
        string folder = _settings.Editor?.ProjectsFolder ?? string.Empty;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Afterline Projects");
            _settings.Editor ??= new Afterline.Models.EditorPreferences();
            _settings.Editor.ProjectsFolder = folder;
        }

        if (createDirectory)
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("The configured Editor projects folder is unavailable; using Documents instead.", ex);
                folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Afterline Projects");
                Directory.CreateDirectory(folder);
                (_settings.Editor ??= new Afterline.Models.EditorPreferences()).ProjectsFolder = folder;
            }
        }
        return folder;
    }

    private FrameworkElement CreateKeybindEditorRowCanaryV4(
        string label,
        string description,
        Func<string> getter,
        Action<string> setter,
        string defaultValue)
        => CreateKeybindFieldCanaryV4(label, description, getter, setter, defaultValue, 0);

    private FrameworkElement CreateKeybindFieldCanaryV4(
        string label,
        string description,
        Func<string> getter,
        Action<string> setter,
        string defaultValue,
        double bottomMargin = 10)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, bottomMargin) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        copy.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        });
        grid.Children.Add(copy);

        var field = new TextBox
        {
            Text = getter(),
            IsReadOnly = true,
            MinHeight = 34,
            Padding = new Thickness(8, 6, 8, 6),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = "Click, then press the shortcut you want. Delete or Backspace restores the default."
        };
        Grid.SetColumn(field, 2);
        grid.Children.Add(field);

        bool capturing = false;
        string previous = getter();
        field.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            previous = getter();
            capturing = true;
            field.Text = "Press keys…";
            field.Focus();
            field.SelectAll();
        };
        field.PreviewKeyDown += (_, e) =>
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
                field.Text = previous;
                e.Handled = true;
                return;
            }
            if (pressed is Key.Delete or Key.Back)
            {
                setter(defaultValue);
                _settingsService.Save(_settings);
                capturing = false;
                field.Text = defaultValue;
                e.Handled = true;
                return;
            }

            string formatted = FormatShortcutCanaryV3(pressed, Keyboard.Modifiers);
            setter(formatted);
            _settingsService.Save(_settings);
            capturing = false;
            field.Text = formatted;
            e.Handled = true;
        };
        field.LostKeyboardFocus += (_, _) =>
        {
            if (!capturing) return;
            capturing = false;
            field.Text = getter();
        };
        return grid;
    }

    private void RebuildApplicationKeybindSettingsCanaryV4()
    {
        if (_settingsSectionContentCanaryV2 is null) return;

        var content = new StackPanel();
        content.Children.Add(new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Application Keybinds", FontSize = 17, FontWeight = FontWeights.SemiBold },
                    new TextBlock
                    {
                        Text = "Click a shortcut field, then press a new key combination. Changes are saved immediately.",
                        FontSize = 10.5,
                        Foreground = (Brush)FindResource("MutedText"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 12)
                    },
                    CreateKeybindFieldCanaryV4("Find / Search", "Focus the page search field or open global Search.",
                        () => _settings.FindKeybind, value => _settings.FindKeybind = value, "Ctrl+F"),
                    CreateKeybindFieldCanaryV4("Open Chatlog", "Open a chatlog directly in Log Reader.",
                        () => _settings.OpenLogKeybind, value => _settings.OpenLogKeybind = value, "Ctrl+O"),
                    CreateKeybindFieldCanaryV4("Copy Selected", "Copy selected Live Chat or Log Reader lines.",
                        () => _settings.CopyKeybind, value => _settings.CopyKeybind = value, "Ctrl+C"),
                    CreateKeybindFieldCanaryV4("Copy With Context", "Copy a selected line with surrounding context.",
                        () => _settings.CopyContextKeybind, value => _settings.CopyContextKeybind = value, "Ctrl+Shift+C", 0)
                }
            }
        });

        _settingsSectionsCanaryV2["keybinds"] = WrapSettingsSectionCanaryV2(content,
            "Keybinds",
            "Customize keyboard shortcuts used throughout Afterline.");

        if (!_settingsSectionButtonsCanaryV2.ContainsKey("keybinds"))
        {
            Button? general = _settingsSectionButtonsCanaryV2.GetValueOrDefault("general");
            if (general?.Parent is StackPanel nav)
            {
                var keybindButton = CreateSettingsNavButtonCanaryV2("keybinds", "⌨", "Keybinds");
                int index = nav.Children.IndexOf(general);
                nav.Children.Insert(Math.Min(nav.Children.Count, index + 1), keybindButton);
            }
        }
    }

    private void CenterSettingsNavigationCanaryV4()
    {
        foreach ((string key, Button button) in _settingsSectionButtonsCanaryV2)
        {
            (string icon, string label) = key.ToLowerInvariant() switch
            {
                "general" => ("⚙", "General"),
                "keybinds" => ("⌨", "Keybinds"),
                "recovery" => ("↺", "Recovery Center"),
                "failsafe" => ("⛨", "Raw Capture Failsafe"),
                "canary" => ("◈", "Canary Branch"),
                _ => ("•", button.ToolTip?.ToString() ?? key)
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 15,
                Width = 22,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            content.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            button.Content = content;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }

    private void ConfigureUpdaterHandoffCanaryV4()
    {
        if (_checkUpdatesButton is null) return;

        _checkUpdatesButton.Click -= InstallAvailableUpdateCanaryV3_Click;
        _checkUpdatesButton.Click -= CheckForUpdates_Click;
        _checkUpdatesButton.Click -= ChannelAwareCheckForUpdatesV062_Click;
        _checkUpdatesButton.Click += InstallAvailableUpdateCanaryV4_Click;

        _updateRefreshTimerCanaryV3.Interval = TimeSpan.FromSeconds(30);
        _ = RefreshUpdateStateCanaryV3Async();
    }

    private async void InstallAvailableUpdateCanaryV4_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdateCanaryV3 is null || _updateInstallInProgress) return;

        UpdateCheckResult release = _availableUpdateCanaryV3;
        string current = GetCurrentBuildVersion() + (IsCanaryChannelV062() ? " Canary" : string.Empty);
        var window = new UpdateAvailableWindow(this, current, release);
        if (window.ShowDialog() != true || !window.InstallRequested) return;

        await InstallAvailableReleaseWithResilientHandoffCanaryV4(release);
    }

    private async Task<bool> InstallAvailableReleaseWithResilientHandoffCanaryV4(UpdateCheckResult release)
    {
        if (!UpdateService.CanSelfUpdate(out string? reason))
        {
            System.Windows.MessageBox.Show(
                this,
                reason ?? "Afterline cannot update itself from the current folder.",
                "Unable to self-update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        _updateInstallInProgress = true;
        _updateRefreshTimerCanaryV3.Stop();
        SetUpdateActionStateCanaryV3("Updating…", false);
        string current = GetCurrentBuildVersion() + (IsCanaryChannelV062() ? " Canary" : string.Empty);
        SetUpdateBuildLines(current, "Downloading…");

        try
        {
            UpdateDownloadResult download = await _updateService.DownloadVerifiedAsync(release, CancellationToken.None);
            SetUpdateBuildLines(current, "Verified · restarting…");

            // All Canary installs now use the detached, retrying updater. This avoids
            // the legacy self-copy race that could leave the UI saying Updating while
            // no replacement/restart completed.
            CanaryUpdateInstaller.LaunchUpdater(download);

            try { await _capture.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLogger.Error("Capture shutdown during Canary update failed.", ex); }
            try { await _processor.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLogger.Error("Background processor shutdown during Canary update failed.", ex); }

            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayBrandIcon?.Dispose();
            _trayBrandIcon = null;

            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            _updateInstallInProgress = false;
            DiagnosticLogger.Error("Unable to install the Canary update.", ex);
            SetUpdateBuildLines(current, "Update failed");
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Unable to install update",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _updateRefreshTimerCanaryV3.Start();
            await RefreshUpdateStateCanaryV3Async();
            return false;
        }
    }

    private void ConfigureEditorPrewarmCanaryV4()
    {
        if (_editorPage is null) return;

        _editorPage.PreviewDrop += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetCanaryFilterSource();
                PrepareEditorFiltersCanaryV4();
            }), DispatcherPriority.Background);

        if (_editorToolButtons.TryGetValue("filters", out Button? filters))
            filters.PreviewMouseLeftButtonDown += (_, _) => PrepareEditorFiltersCanaryV4();

        PrepareEditorFiltersCanaryV4();
    }

    private void PrepareEditorFiltersCanaryV4()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_editorBaseOriginal is null || EditorHasAnimatedGifV060) return;
            // Initialize only the committed source here. The previous warm-up ran a
            // full-resolution filter pass on the dispatcher and blocked first slider
            // and scrollbar interactions for large screenshots.
            PrewarmEditorFiltersCanaryV3();
        }), DispatcherPriority.ContextIdle);
    }
}
