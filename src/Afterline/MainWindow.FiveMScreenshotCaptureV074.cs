using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;
using Forms = System.Windows.Forms;

namespace Afterline;

public partial class MainWindow
{
    private const int ScreenshotGalleryLimitV074 = 20;
    private const int ScreenshotHotkeyIdV074 = 0xA174;
    private const int WmHotkeyV074 = 0x0312;
    private const uint ModAltV074 = 0x0001;
    private const uint ModControlV074 = 0x0002;
    private const uint ModShiftV074 = 0x0004;
    private const uint ModWinV074 = 0x0008;
    private const uint ModNoRepeatV074 = 0x4000;
    private const int WhMouseLlV076 = 14;
    private const int WmXButtonDownV076 = 0x020B;

    public sealed class FiveMScreenshotGalleryItemV074
    {
        public required string FilePath { get; init; }
        public required string FileName { get; init; }
        public required string Detail { get; init; }
        public BitmapSource? Thumbnail { get; init; }
    }

    public ObservableCollection<FiveMScreenshotGalleryItemV074> FiveMScreenshotsV074 { get; } = new();

    private bool _fiveMScreenshotFeatureInitializedV074;
    private Button? _fiveMScreenshotNavButtonV074;
    private Grid? _fiveMScreenshotGalleryPageV074;
    private TextBlock? _fiveMScreenshotGalleryStatusV074;
    private TextBlock? _fiveMScreenshotGalleryEmptyV074;
    private HwndSource? _fiveMScreenshotHotkeySourceV074;
    private bool _fiveMScreenshotHotkeyRegisteredV074;
    private bool _fiveMScreenshotCaptureInProgressV074;
    private int _fiveMScreenshotRefreshVersionV074;
    private bool _screenshotHotkeyConfirmedV076 = true;
    private IntPtr _screenshotMouseHookV076;
    private LowLevelMouseProcV076? _screenshotMouseHookProcV076;
    private int _screenshotMouseButtonV076;
    private uint _screenshotMouseModifiersV076;

    private void EnsureFiveMScreenshotCaptureV074()
    {
        if (_fiveMScreenshotFeatureInitializedV074 || !_settings.EnableFiveMScreenshotCapture)
            return;
        if (DashboardNav.Parent is not StackPanel navigationPanel || DashboardPage.Parent is not Grid pageHost)
            return;

        _fiveMScreenshotFeatureInitializedV074 = true;
        _fiveMScreenshotNavButtonV074 = new Button
        {
            Content = "Gallery",
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = "View locally stored captures."
        };
        _fiveMScreenshotNavButtonV074.Click += (_, _) => ShowFiveMScreenshotGalleryV074();
        int settingsIndex = navigationPanel.Children.IndexOf(SettingsNav);
        if (settingsIndex >= 0)
            navigationPanel.Children.Insert(settingsIndex, _fiveMScreenshotNavButtonV074);
        else
            navigationPanel.Children.Add(_fiveMScreenshotNavButtonV074);

        _fiveMScreenshotGalleryPageV074 = BuildFiveMScreenshotGalleryPageV074();
        Grid.SetRow(_fiveMScreenshotGalleryPageV074, 2);
        pageHost.Children.Add(_fiveMScreenshotGalleryPageV074);

        foreach (Button nav in navigationPanel.Children.OfType<Button>().ToArray())
        {
            if (ReferenceEquals(nav, _fiveMScreenshotNavButtonV074)) continue;
            nav.Click += (_, _) =>
            {
                if (_fiveMScreenshotGalleryPageV074 is not null)
                    _fiveMScreenshotGalleryPageV074.Visibility = Visibility.Collapsed;
            };
        }

        ConfigureFiveMScreenshotHotkeyV074();
    }

    private Grid BuildFiveMScreenshotGalleryPageV074()
    {
        var page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topCard = new Border { Style = (Style)FindResource("CardStyle") };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "Gallery", FontSize = 18, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock
        {
            Text = "Files are stored locally. The latest 20 source-resolution captures are shown here; capture is permitted only while FiveM, GTA5, or GTAVLauncher owns the foreground window.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            MaxWidth = 620
        });
        _fiveMScreenshotGalleryStatusV074 = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0),
            Text = "Ready"
        };
        title.Children.Add(_fiveMScreenshotGalleryStatusV074);
        top.Children.Add(title);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var capture = new Button
        {
            Content = "Capture",
            Style = (Style)FindResource("PrimaryButton"),
            ToolTip = "Capture the active game window."
        };
        capture.Click += async (_, _) => await CaptureFiveMScreenshotV074Async();
        actions.Children.Add(capture);
        var refresh = new Button { Content = "Scan folder", Margin = new Thickness(8, 0, 0, 0), ToolTip = "Find local captures in the selected folder." };
        refresh.Click += async (_, _) => await RefreshFiveMScreenshotGalleryV074Async(scanFolder: true);
        actions.Children.Add(refresh);
        var open = new Button { Content = "Open folder", Margin = new Thickness(8, 0, 0, 0) };
        open.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(_settings.ScreenshotFolder);
                OpenPath(_settings.ScreenshotFolder);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Unable to open the screenshot folder.", ex);
            }
        };
        actions.Children.Add(open);
        Grid.SetColumn(actions, 1);
        top.Children.Add(actions);
        topCard.Child = top;
        page.Children.Add(topCard);

        var galleryCard = new Border { Style = (Style)FindResource("CardStyle") };
        var holder = new Grid();
        _fiveMScreenshotGalleryEmptyV074 = new TextBlock
        {
            Text = "No captures yet. Bring the game to the foreground and use Capture or the configured hotkey.",
            Foreground = (Brush)FindResource("MutedText"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 400
        };
        holder.Children.Add(_fiveMScreenshotGalleryEmptyV074);
        var list = new ListBox
        {
            ItemsSource = FiveMScreenshotsV074,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };
        ScrollViewer.SetCanContentScroll(list, true);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is FiveMScreenshotGalleryItemV074 selected)
                OpenFiveMScreenshotInEditorV074(selected.FilePath);
        };
        list.ItemTemplate = CreateFiveMScreenshotGalleryTemplateV074();
        holder.Children.Add(list);
        galleryCard.Child = holder;
        Grid.SetRow(galleryCard, 2);
        page.Children.Add(galleryCard);
        return page;
    }

    private DataTemplate CreateFiveMScreenshotGalleryTemplateV074()
    {
        var template = new DataTemplate(typeof(FiveMScreenshotGalleryItemV074));
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        root.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 5, 4, 7));

        var imageBorder = new FrameworkElementFactory(typeof(Border));
        imageBorder.SetValue(Border.WidthProperty, 132.0);
        imageBorder.SetValue(Border.HeightProperty, 76.0);
        imageBorder.SetValue(Border.BackgroundProperty, FindResource("Bg"));
        imageBorder.SetValue(Border.BorderBrushProperty, FindResource("Border"));
        imageBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        imageBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        imageBorder.SetValue(Border.ClipToBoundsProperty, true);
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding(nameof(FiveMScreenshotGalleryItemV074.Thumbnail)));
        image.SetValue(Image.StretchProperty, Stretch.UniformToFill);
        imageBorder.AppendChild(image);
        root.AppendChild(imageBorder);

        var info = new FrameworkElementFactory(typeof(StackPanel));
        info.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 12, 0));
        info.SetValue(FrameworkElement.WidthProperty, 340.0);
        info.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var fileName = new FrameworkElementFactory(typeof(TextBlock));
        fileName.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(FiveMScreenshotGalleryItemV074.FileName)));
        fileName.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        fileName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        info.AppendChild(fileName);
        var detail = new FrameworkElementFactory(typeof(TextBlock));
        detail.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(FiveMScreenshotGalleryItemV074.Detail)));
        detail.SetValue(TextBlock.ForegroundProperty, FindResource("MutedText"));
        detail.SetValue(TextBlock.FontSizeProperty, 11.0);
        detail.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 0));
        detail.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        info.AppendChild(detail);
        root.AppendChild(info);

        var itemActions = new FrameworkElementFactory(typeof(StackPanel));
        itemActions.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        itemActions.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var open = new FrameworkElementFactory(typeof(Button));
        open.SetValue(Button.ContentProperty, "Open in Editor");
        open.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        open.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding(nameof(FiveMScreenshotGalleryItemV074.FilePath)));
        open.AddHandler(Button.ClickEvent, new RoutedEventHandler(OpenFiveMScreenshotInEditorButtonV074));
        itemActions.AppendChild(open);

        var delete = new FrameworkElementFactory(typeof(Button));
        delete.SetValue(Button.ContentProperty, "Delete");
        delete.SetValue(Control.ForegroundProperty, FindResource("Warning"));
        delete.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        delete.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        delete.SetValue(Control.ToolTipProperty, "Move this local capture to the Recycle Bin");
        delete.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding(nameof(FiveMScreenshotGalleryItemV074.FilePath)));
        delete.AddHandler(Button.ClickEvent, new RoutedEventHandler(DeleteFiveMScreenshotButtonV074));
        itemActions.AppendChild(delete);
        root.AppendChild(itemActions);

        template.VisualTree = root;
        return template;
    }

    private void ShowFiveMScreenshotGalleryV074()
    {
        if (_fiveMScreenshotGalleryPageV074 is null) return;
        ShowPage(_fiveMScreenshotGalleryPageV074, "Gallery", "Locally stored source-resolution captures and quick Editor handoff");
        _ = RefreshFiveMScreenshotGalleryV074Async();
    }

    private async Task CaptureFiveMScreenshotV074Async()
    {
        if (!_settings.EnableFiveMScreenshotCapture || _fiveMScreenshotCaptureInProgressV074) return;
        _fiveMScreenshotCaptureInProgressV074 = true;
        bool restoreAfterline = FiveMScreenshotCaptureService.IsAfterlineForeground();
        WindowState previousWindowState = WindowState;
        try
        {
            if (restoreAfterline)
            {
                if (!FiveMScreenshotCaptureService.TryFindGameWindowForAfterlineCapture(out IntPtr gameWindow, out string reason))
                    throw new InvalidOperationException(reason);

                SetFiveMScreenshotStatusV074("Switching briefly to the game window…");
                Hide();
                if (!FiveMScreenshotCaptureService.ActivateGameWindow(gameWindow))
                    throw new InvalidOperationException("Afterline found the game but Windows would not activate its window for capture.");
                await Task.Delay(220);
            }

            SetFiveMScreenshotStatusV074("Capturing the foreground FiveM game window…");
            FiveMScreenshotCaptureService.CaptureResult result = await Task.Run(
                () => FiveMScreenshotCaptureService.CaptureForegroundWindow(_settings.ScreenshotFolder));
            ScreenshotGalleryIndexService.Record(result.FilePath);
            CaptureFeedbackSoundService.Play(_settings.ScreenshotCaptureSound, _settings.ScreenshotCaptureSoundVolume);
            SetFiveMScreenshotStatusV074($"Saved {Path.GetFileName(result.FilePath)} · {result.PixelWidth:N0} × {result.PixelHeight:N0}px");
            if (_settings.ScreenshotCaptureNotificationEnabled)
                ShowScreenshotSavedNotificationV076(result.FilePath);
            await RefreshFiveMScreenshotGalleryV074Async();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("FiveM screenshot capture failed.", ex);
            SetFiveMScreenshotStatusV074(ex.Message);
        }
        finally
        {
            if (restoreAfterline)
            {
                Show();
                WindowState = previousWindowState;
                Activate();
            }
            _fiveMScreenshotCaptureInProgressV074 = false;
        }
    }

    private async Task RefreshFiveMScreenshotGalleryV074Async(bool scanFolder = false)
    {
        if (!_settings.EnableFiveMScreenshotCapture) return;
        int version = ++_fiveMScreenshotRefreshVersionV074;
        SetFiveMScreenshotStatusV074(scanFolder ? "Scanning the configured folder in the background…" : "Loading the latest 20 captures…");
        IReadOnlyList<FiveMScreenshotGalleryItemV074> items = await Task.Run(
            () => LoadFiveMScreenshotGalleryV074(_settings.ScreenshotFolder, scanFolder));
        if (version != _fiveMScreenshotRefreshVersionV074 || !_settings.EnableFiveMScreenshotCapture)
            return;

        FiveMScreenshotsV074.Clear();
        foreach (FiveMScreenshotGalleryItemV074 item in items)
            FiveMScreenshotsV074.Add(item);
        if (_fiveMScreenshotGalleryEmptyV074 is not null)
            _fiveMScreenshotGalleryEmptyV074.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SetFiveMScreenshotStatusV074(items.Count == 0
            ? "No captures found."
            : $"Showing the latest {items.Count} capture{(items.Count == 1 ? string.Empty : "s")}.");
    }

    private static IReadOnlyList<FiveMScreenshotGalleryItemV074> LoadFiveMScreenshotGalleryV074(string folder, bool scanFolder)
    {
        try
        {
            if (scanFolder)
                ScreenshotGalleryIndexService.IndexExistingFiles(folder, ScreenshotGalleryLimitV074);

            return ScreenshotGalleryIndexService.LoadRecent(folder, ScreenshotGalleryLimitV074)
                .Select(file => new FiveMScreenshotGalleryItemV074
                {
                    FilePath = file.FullName,
                    FileName = file.Name,
                    Detail = $"{file.LastWriteTime:dd MMM yyyy · HH:mm:ss} · {FormatScreenshotDimensionsV074(file.FullName)} · {FormatScreenshotSizeV074(file.Length)}",
                    Thumbnail = ReadFiveMScreenshotThumbnailV074(file.FullName)
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to refresh the FiveM screenshot gallery.", ex);
            return Array.Empty<FiveMScreenshotGalleryItemV074>();
        }
    }

    private static BitmapSource? ReadFiveMScreenshotThumbnailV074(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 360;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private static string FormatScreenshotDimensionsV074(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            BitmapFrame frame = decoder.Frames[0];
            return $"{frame.PixelWidth:N0} × {frame.PixelHeight:N0}px";
        }
        catch { return "Dimensions unavailable"; }
    }

    private static string FormatScreenshotSizeV074(long bytes)
        => bytes < 1024 * 1024
            ? $"{Math.Max(1, bytes / 1024d):0.#} KB"
            : $"{bytes / 1024d / 1024d:0.#} MB";

    private void OpenFiveMScreenshotInEditorButtonV074(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
            OpenFiveMScreenshotInEditorV074(path);
    }

    private void DeleteFiveMScreenshotButtonV074(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string path }) return;
        string fileName = Path.GetFileName(path);
        if (System.Windows.MessageBox.Show(
                this,
                $"Move '{fileName}' to the Recycle Bin?",
                "Delete local capture",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            if (File.Exists(path))
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            ScreenshotGalleryIndexService.Remove(path);
            FiveMScreenshotGalleryItemV074? item = FiveMScreenshotsV074.FirstOrDefault(entry =>
                string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (item is not null) FiveMScreenshotsV074.Remove(item);
            if (_fiveMScreenshotGalleryEmptyV074 is not null)
                _fiveMScreenshotGalleryEmptyV074.Visibility = FiveMScreenshotsV074.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SetFiveMScreenshotStatusV074($"Moved {fileName} to the Recycle Bin.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to delete a Gallery capture.", ex);
            SetFiveMScreenshotStatusV074("That capture could not be moved to the Recycle Bin.");
        }
    }

    private void OpenFiveMScreenshotInEditorV074(string path)
    {
        if (!File.Exists(path))
        {
            SetFiveMScreenshotStatusV074("That screenshot is no longer available in its configured folder.");
            return;
        }

        EditorNav_Click(this, new RoutedEventArgs());
        LoadEditorMediaV060(path);
    }

    private bool ConfigureFiveMScreenshotHotkeyV074(bool showFailure = false)
    {
        ReleaseFiveMScreenshotHotkeyV074();
        if (!_settings.EnableFiveMScreenshotCapture) return false;

        if (!TryParseFiveMScreenshotHotkeyV074(_settings.ScreenshotHotkey, out uint modifiers, out uint virtualKey))
        {
            _settings.ScreenshotHotkey = "Ctrl+Shift+F12";
            modifiers = ModControlV074 | ModShiftV074;
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.F12);
            DiagnosticLogger.Info("Invalid FiveM screenshot hotkey was reset to Ctrl+Shift+F12.");
        }

        if (TryGetScreenshotMouseButtonV076(_settings.ScreenshotHotkey, out int mouseButton))
        {
            _screenshotMouseButtonV076 = mouseButton;
            _screenshotMouseModifiersV076 = modifiers;
            _screenshotMouseHookProcV076 = ScreenshotMouseHookV076;
            _screenshotMouseHookV076 = SetWindowsHookExV076(
                WhMouseLlV076,
                _screenshotMouseHookProcV076,
                GetModuleHandleV076(null),
                0);
            if (_screenshotMouseHookV076 == IntPtr.Zero)
            {
                DiagnosticLogger.Error($"Unable to register screenshot shortcut '{_settings.ScreenshotHotkey}'.");
                if (showFailure)
                {
                    System.Windows.MessageBox.Show(
                        this,
                        "Windows could not activate that mouse shortcut. Choose a different shortcut and try again.",
                        "Capture hotkey unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else
            {
                DiagnosticLogger.Info($"Screen capture shortcut active: {_settings.ScreenshotHotkey} (mouse hook).");
            }
            return _screenshotMouseHookV076 != IntPtr.Zero;
        }

        IntPtr handle = new WindowInteropHelper(this).Handle;
        _fiveMScreenshotHotkeySourceV074 = HwndSource.FromHwnd(handle);
        if (_fiveMScreenshotHotkeySourceV074 is null)
        {
            DiagnosticLogger.Error("Unable to register the FiveM screenshot hotkey because Afterline has no window handle.");
            return false;
        }

        _fiveMScreenshotHotkeySourceV074.AddHook(FiveMScreenshotHotkeyWndProcV074);
        _fiveMScreenshotHotkeyRegisteredV074 = RegisterHotKey(handle, ScreenshotHotkeyIdV074, modifiers | ModNoRepeatV074, virtualKey);
        if (!_fiveMScreenshotHotkeyRegisteredV074)
        {
            // MOD_NOREPEAT is not accepted consistently by every Windows/hotkey
            // combination. The in-progress guard still prevents duplicate captures.
            _fiveMScreenshotHotkeyRegisteredV074 = RegisterHotKey(handle, ScreenshotHotkeyIdV074, modifiers, virtualKey);
        }
        if (!_fiveMScreenshotHotkeyRegisteredV074)
        {
            _fiveMScreenshotHotkeySourceV074.RemoveHook(FiveMScreenshotHotkeyWndProcV074);
            _fiveMScreenshotHotkeySourceV074 = null;
            DiagnosticLogger.Error($"Unable to register FiveM screenshot hotkey '{_settings.ScreenshotHotkey}'. It may already be in use.");
            if (showFailure)
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"Windows could not register '{_settings.ScreenshotHotkey}'. Choose another shortcut that is not already used by another app.",
                    "Capture hotkey unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                }
            }
        }
        else
        {
            DiagnosticLogger.Info($"Screen capture shortcut active: {_settings.ScreenshotHotkey} (Windows hotkey).");
        }
        return _fiveMScreenshotHotkeyRegisteredV074;

    private void ReleaseFiveMScreenshotHotkeyV074()
    {
        if (_screenshotMouseHookV076 != IntPtr.Zero)
        {
            try { UnhookWindowsHookExV076(_screenshotMouseHookV076); }
            catch { }
            _screenshotMouseHookV076 = IntPtr.Zero;
            _screenshotMouseHookProcV076 = null;
            _screenshotMouseButtonV076 = 0;
            _screenshotMouseModifiersV076 = 0;
        }
        if (_fiveMScreenshotHotkeyRegisteredV074)
        {
            try { UnregisterHotKey(new WindowInteropHelper(this).Handle, ScreenshotHotkeyIdV074); }
            catch { }
            _fiveMScreenshotHotkeyRegisteredV074 = false;
        }
        if (_fiveMScreenshotHotkeySourceV074 is not null)
        {
            _fiveMScreenshotHotkeySourceV074.RemoveHook(FiveMScreenshotHotkeyWndProcV074);
            _fiveMScreenshotHotkeySourceV074 = null;
        }
    }

    private IntPtr FiveMScreenshotHotkeyWndProcV074(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkeyV074 && wParam.ToInt32() == ScreenshotHotkeyIdV074)
        {
            handled = true;
            DiagnosticLogger.Info($"Screen capture shortcut detected: {_settings.ScreenshotHotkey}.");
            _ = CaptureFiveMScreenshotV074Async();
        }
        return IntPtr.Zero;
    }

    private static bool TryParseFiveMScreenshotHotkeyV074(string? value, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string[] segments = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;

        Key? key = null;
        int mouseButton = 0;
        foreach (string segment in segments)
        {
            if (segment.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || segment.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModControlV074;
            else if (segment.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModAltV074;
            else if (segment.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModShiftV074;
            else if (segment.Equals("Win", StringComparison.OrdinalIgnoreCase) || segment.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModWinV074;
            else if (TryParseScreenshotMouseButtonSegmentV076(segment, out int parsedMouse))
            {
                if (key is not null || mouseButton != 0) return false;
                mouseButton = parsedMouse;
            }
            else if (TryParseScreenshotKeyV076(segment, out Key parsed) && parsed is not Key.None and not Key.LeftCtrl and not Key.RightCtrl and not Key.LeftShift and not Key.RightShift and not Key.LeftAlt and not Key.RightAlt and not Key.LWin and not Key.RWin and not Key.Escape and not Key.Return and not Key.Back)
            {
                if (key is not null || mouseButton != 0) return false;
                key = parsed;
            }
            else
                return false;
        }
        if (mouseButton != 0)
            return true;
        if (key is not Key selected) return false;
        int valueCode = KeyInterop.VirtualKeyFromKey(selected);
        if (valueCode <= 0) return false;
        virtualKey = (uint)valueCode;
        return true;
    }

    private void ScreenshotHotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ScreenshotHotkeyBox.SelectAll();
    }

    private void ScreenshotHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
        {
            e.Handled = true;
            return;
        }

        if (key is Key.Escape or Key.Return or Key.Back)
        {
            ShowScreenshotHotkeyRecognitionV076("Esc, Enter, and Backspace cannot be used as capture keys.", valid: false);
            e.Handled = true;
            return;
        }

        ScreenshotHotkeyBox.Text = BuildScreenshotShortcutTextV076(Keyboard.Modifiers, FriendlyScreenshotKeyNameV076(key));
        ScreenshotHotkeyBox.CaretIndex = ScreenshotHotkeyBox.Text.Length;
        ShowScreenshotHotkeyRecognitionV076($"Recognized {ScreenshotHotkeyBox.Text}. Confirm it or choose Re-do.", valid: true);
        e.Handled = true;
    }

    private void ScreenshotHotkeyBox_PreviewMouseDownV076(object sender, MouseButtonEventArgs e)
    {
        string? mouseName = e.ChangedButton switch
        {
            MouseButton.XButton1 => "Mouse 4",
            MouseButton.XButton2 => "Mouse 5",
            _ => null
        };
        if (mouseName is null) return;

        ScreenshotHotkeyBox.Text = BuildScreenshotShortcutTextV076(Keyboard.Modifiers, mouseName);
        ShowScreenshotHotkeyRecognitionV076($"Recognized {ScreenshotHotkeyBox.Text}. Confirm it or choose Re-do.", valid: true);
        e.Handled = true;
    }

    private static string BuildScreenshotShortcutTextV076(ModifierKeys modifiers, string keyName)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static string FriendlyScreenshotKeyNameV076(Key key)
    {
        int keyValue = (int)key;
        if (keyValue >= (int)Key.D0 && keyValue <= (int)Key.D9)
            return (keyValue - (int)Key.D0).ToString();
        if (keyValue >= (int)Key.NumPad0 && keyValue <= (int)Key.NumPad9)
            return "Numpad " + (keyValue - (int)Key.NumPad0);

        return key switch
        {
            Key.Space => "Space",
            Key.Capital => "Caps Lock",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            Key.Snapshot => "Print Screen",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemQuestion => "Slash",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quote",
            Key.OemOpenBrackets => "Left Bracket",
            Key.OemCloseBrackets => "Right Bracket",
            Key.OemPipe => "Backslash",
            Key.OemTilde => "Tilde",
            _ => key.ToString()
        };
    }

    private static bool TryParseScreenshotKeyV076(string value, out Key key)
    {
        string normalized = value.Trim();
        if (normalized.Length == 1 && char.IsDigit(normalized[0]))
        {
            key = (Key)((int)Key.D0 + (normalized[0] - '0'));
            return true;
        }
        if (normalized.StartsWith("Numpad ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized[7..], out int number) && number is >= 0 and <= 9)
        {
            key = (Key)((int)Key.NumPad0 + number);
            return true;
        }

        key = normalized.ToLowerInvariant() switch
        {
            "caps lock" => Key.Capital,
            "page up" => Key.Prior,
            "page down" => Key.Next,
            "print screen" => Key.Snapshot,
            "plus" => Key.OemPlus,
            "minus" => Key.OemMinus,
            "comma" => Key.OemComma,
            "period" => Key.OemPeriod,
            "slash" => Key.OemQuestion,
            "semicolon" => Key.OemSemicolon,
            "quote" => Key.OemQuotes,
            "left bracket" => Key.OemOpenBrackets,
            "right bracket" => Key.OemCloseBrackets,
            "backslash" => Key.OemPipe,
            "tilde" => Key.OemTilde,
            _ => Key.None
        };
        return key != Key.None || Enum.TryParse(normalized, ignoreCase: true, out key);
    }

    private static bool TryParseScreenshotMouseButtonSegmentV076(string value, out int mouseButton)
    {
        if (value.Equals("Mouse 4", StringComparison.OrdinalIgnoreCase) || value.Equals("Mouse4", StringComparison.OrdinalIgnoreCase))
        {
            mouseButton = 4;
            return true;
        }
        if (value.Equals("Mouse 5", StringComparison.OrdinalIgnoreCase) || value.Equals("Mouse5", StringComparison.OrdinalIgnoreCase))
        {
            mouseButton = 5;
            return true;
        }
        mouseButton = 0;
        return false;
    }

    private static bool TryGetScreenshotMouseButtonV076(string? shortcut, out int mouseButton)
    {
        mouseButton = 0;
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        foreach (string segment in shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseScreenshotMouseButtonSegmentV076(segment, out mouseButton))
                return true;
        }
        return false;
    }

    private void ShowScreenshotHotkeyRecognitionV076(string message, bool valid)
    {
        _screenshotHotkeyConfirmedV076 = false;
        ScreenshotHotkeyConfirmationTextV076.Text = message;
        ScreenshotHotkeyConfirmationTextV076.Foreground = (Brush)FindResource(valid ? "MutedText" : "Warning");
        ScreenshotHotkeyConfirmationPanelV076.Visibility = Visibility.Visible;
    }

    private void ConfirmScreenshotHotkeyV076_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseFiveMScreenshotHotkeyV074(ScreenshotHotkeyBox.Text, out _, out _))
        {
            ShowScreenshotHotkeyRecognitionV076("That shortcut is not valid. Choose Re-do and try another key or combination.", valid: false);
            return;
        }
        string shortcut = ScreenshotHotkeyBox.Text.Trim();
        string previous = _settings.ScreenshotHotkey;
        try
        {
            _settings.ScreenshotHotkey = shortcut;

            if (!_settings.EnableFiveMScreenshotCapture)
            {
                _settingsService.Save(_settings);
                _screenshotHotkeyConfirmedV076 = true;
                ScreenshotHotkeyConfirmationTextV076.Text = $"{shortcut} saved. It will activate when Screen capture is enabled.";
                ScreenshotHotkeyConfirmationTextV076.Foreground = (Brush)FindResource("Success");
                return;
            }

            if (ConfigureFiveMScreenshotHotkeyV074(showFailure: false))
            {
                _settingsService.Save(_settings);
                _screenshotHotkeyConfirmedV076 = true;
                ScreenshotHotkeyConfirmationTextV076.Text = $"{shortcut} is active now.";
                ScreenshotHotkeyConfirmationTextV076.Foreground = (Brush)FindResource("Success");
                return;
            }

            _settings.ScreenshotHotkey = previous;
            ScreenshotHotkeyBox.Text = previous;
            _ = ConfigureFiveMScreenshotHotkeyV074(showFailure: false);
            _screenshotHotkeyConfirmedV076 = true;
            ScreenshotHotkeyConfirmationTextV076.Text = $"Windows could not activate {shortcut}. The previous shortcut {previous} is still active.";
            ScreenshotHotkeyConfirmationTextV076.Foreground = (Brush)FindResource("Warning");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to activate the confirmed screen capture shortcut.", ex);
            _settings.ScreenshotHotkey = previous;
            ScreenshotHotkeyBox.Text = previous;
            _ = ConfigureFiveMScreenshotHotkeyV074(showFailure: false);
            _screenshotHotkeyConfirmedV076 = true;
            ScreenshotHotkeyConfirmationTextV076.Text = $"Afterline could not save {shortcut}. The previous shortcut {previous} is still active.";
            ScreenshotHotkeyConfirmationTextV076.Foreground = (Brush)FindResource("Warning");
        }
    }

    private void RedoScreenshotHotkeyV076_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotHotkeyBox.Text = string.Empty;
        _screenshotHotkeyConfirmedV076 = false;
        ScreenshotHotkeyConfirmationTextV076.Text = "Press a keyboard combination or Mouse 4/5 in the shortcut field.";
        ScreenshotHotkeyConfirmationTextV076.Foreground = (Brush)FindResource("MutedText");
        ScreenshotHotkeyBox.Focus();
    }

    private IntPtr ScreenshotMouseHookV076(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == WmXButtonDownV076)
        {
            MouseHookDataV076 data = Marshal.PtrToStructure<MouseHookDataV076>(lParam);
            int button = ((data.MouseData >> 16) & 0xFFFF) switch { 1 => 4, 2 => 5, _ => 0 };
            if (button == _screenshotMouseButtonV076 && ActiveScreenshotModifiersV076() == _screenshotMouseModifiersV076)
            {
                DiagnosticLogger.Info($"Screen capture shortcut detected: {_settings.ScreenshotHotkey}.");
                _ = Dispatcher.BeginInvoke(new Action(() => _ = CaptureFiveMScreenshotV074Async()));
            }
        }
        return CallNextHookExV076(_screenshotMouseHookV076, code, wParam, lParam);
    }

    private static uint ActiveScreenshotModifiersV076()
    {
        uint modifiers = 0;
        if ((GetAsyncKeyStateV076(0x11) & 0x8000) != 0) modifiers |= ModControlV074;
        if ((GetAsyncKeyStateV076(0x10) & 0x8000) != 0) modifiers |= ModShiftV074;
        if ((GetAsyncKeyStateV076(0x12) & 0x8000) != 0) modifiers |= ModAltV074;
        if ((GetAsyncKeyStateV076(0x5B) & 0x8000) != 0 || (GetAsyncKeyStateV076(0x5C) & 0x8000) != 0) modifiers |= ModWinV074;
        return modifiers;
    }

    private void ApplyFiveMScreenshotCaptureSettingsV074()
    {
        if (_settings.EnableFiveMScreenshotCapture)
        {
            bool alreadyInitialized = _fiveMScreenshotFeatureInitializedV074;
            EnsureFiveMScreenshotCaptureV074();
            UpdateFiveMScreenshotUiAvailabilityV074(true);
            if (alreadyInitialized)
                ConfigureFiveMScreenshotHotkeyV074(showFailure: true);
        }
        else
        {
            ReleaseFiveMScreenshotHotkeyV074();
            FiveMScreenshotsV074.Clear();
            UpdateFiveMScreenshotUiAvailabilityV074(false);
        }
    }

    private void ScreenshotCaptureEnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        // Reflect the pending setting immediately. Registration and persistence
        // still happen through Save settings.
        UpdateFiveMScreenshotUiAvailabilityV074(ScreenshotCaptureEnabledCheck.IsChecked == true);
    }

    private void UpdateFiveMScreenshotUiAvailabilityV074(bool enabled)
    {
        if (_fiveMScreenshotNavButtonV074 is not null)
            _fiveMScreenshotNavButtonV074.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!enabled && _fiveMScreenshotGalleryPageV074 is not null)
            _fiveMScreenshotGalleryPageV074.Visibility = Visibility.Collapsed;
    }

    private void SetFiveMScreenshotStatusV074(string message)
    {
        if (_fiveMScreenshotGalleryStatusV074 is not null)
            _fiveMScreenshotGalleryStatusV074.Text = message;
    }

    private void ShowScreenshotSavedNotificationV076(string path)
    {
        if (_trayIcon is null)
        {
            ShowInAppFileNotification("Screenshot saved", $"{Path.GetFileName(path)} was saved locally.", path);
            return;
        }

        _lastExportPath = Path.GetFullPath(path);
        _trayIcon.BalloonTipTitle = "Screenshot saved";
        _trayIcon.BalloonTipText = $"{Path.GetFileName(path)} was saved locally. Click to open its location.";
        _trayIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(8_000);
    }

    private void BrowseScreenshotFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            SelectedPath = ScreenshotFolderBox.Text,
            Description = "Choose where Afterline stores screen captures"
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ScreenshotFolderBox.Text = dialog.SelectedPath;
            ApplyStreamerModePresentationV075();
        }
    }

    private void ResetScreenshotHotkey_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotHotkeyBox.Text = "Ctrl+Shift+F12";
        ShowScreenshotHotkeyRecognitionV076("Default shortcut restored. Confirm it to keep the change.", valid: true);
    }

    private delegate IntPtr LowLevelMouseProcV076(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookDataV076
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExV076(int hookId, LowLevelMouseProcV076 callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookExV076(IntPtr hook);

    [DllImport("user32.dll", EntryPoint = "CallNextHookEx")]
    private static extern IntPtr CallNextHookExV076(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetAsyncKeyState")]
    private static extern short GetAsyncKeyStateV076(int virtualKey);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleV076(string? moduleName);
}
