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

        var open = new FrameworkElementFactory(typeof(Button));
        open.SetValue(Button.ContentProperty, "Open in Editor");
        open.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        open.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding(nameof(FiveMScreenshotGalleryItemV074.FilePath)));
        open.AddHandler(Button.ClickEvent, new RoutedEventHandler(OpenFiveMScreenshotInEditorButtonV074));
        root.AppendChild(open);

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
        try
        {
            SetFiveMScreenshotStatusV074("Capturing the foreground FiveM game window…");
            FiveMScreenshotCaptureService.CaptureResult result = await Task.Run(
                () => FiveMScreenshotCaptureService.CaptureForegroundWindow(_settings.ScreenshotFolder));
            ScreenshotGalleryIndexService.Record(result.FilePath);
            CaptureFeedbackSoundService.Play(_settings.ScreenshotCaptureSound, _settings.ScreenshotCaptureSoundVolume);
            SetFiveMScreenshotStatusV074($"Saved {Path.GetFileName(result.FilePath)} · {result.PixelWidth:N0} × {result.PixelHeight:N0}px");
            await RefreshFiveMScreenshotGalleryV074Async();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("FiveM screenshot capture failed.", ex);
            SetFiveMScreenshotStatusV074(ex.Message);
        }
        finally { _fiveMScreenshotCaptureInProgressV074 = false; }
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

    private void ConfigureFiveMScreenshotHotkeyV074()
    {
        ReleaseFiveMScreenshotHotkeyV074();
        if (!_settings.EnableFiveMScreenshotCapture) return;

        if (!TryParseFiveMScreenshotHotkeyV074(_settings.ScreenshotHotkey, out uint modifiers, out uint virtualKey))
        {
            _settings.ScreenshotHotkey = "Ctrl+Shift+F12";
            modifiers = ModControlV074 | ModShiftV074;
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.F12);
            DiagnosticLogger.Info("Invalid FiveM screenshot hotkey was reset to Ctrl+Shift+F12.");
        }

        IntPtr handle = new WindowInteropHelper(this).Handle;
        _fiveMScreenshotHotkeySourceV074 = HwndSource.FromHwnd(handle);
        if (_fiveMScreenshotHotkeySourceV074 is null)
        {
            DiagnosticLogger.Error("Unable to register the FiveM screenshot hotkey because Afterline has no window handle.");
            return;
        }

        _fiveMScreenshotHotkeySourceV074.AddHook(FiveMScreenshotHotkeyWndProcV074);
        _fiveMScreenshotHotkeyRegisteredV074 = RegisterHotKey(handle, ScreenshotHotkeyIdV074, modifiers | ModNoRepeatV074, virtualKey);
        if (!_fiveMScreenshotHotkeyRegisteredV074)
        {
            _fiveMScreenshotHotkeySourceV074.RemoveHook(FiveMScreenshotHotkeyWndProcV074);
            _fiveMScreenshotHotkeySourceV074 = null;
            DiagnosticLogger.Error($"Unable to register FiveM screenshot hotkey '{_settings.ScreenshotHotkey}'. It may already be in use.");
        }
    }

    private void ReleaseFiveMScreenshotHotkeyV074()
    {
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
            else if (Enum.TryParse(segment, ignoreCase: true, out Key parsed) && parsed is not Key.None and not Key.LeftCtrl and not Key.RightCtrl and not Key.LeftShift and not Key.RightShift and not Key.LeftAlt and not Key.RightAlt and not Key.LWin and not Key.RWin)
            {
                if (key is not null) return false;
                key = parsed;
            }
            else
                return false;
        }
        if (key is not Key selected) return false;
        int valueCode = KeyInterop.VirtualKeyFromKey(selected);
        if (valueCode <= 0) return false;
        virtualKey = (uint)valueCode;
        return true;
    }

    private void ApplyFiveMScreenshotCaptureSettingsV074()
    {
        if (_settings.EnableFiveMScreenshotCapture)
        {
            bool alreadyInitialized = _fiveMScreenshotFeatureInitializedV074;
            EnsureFiveMScreenshotCaptureV074();
            if (_fiveMScreenshotNavButtonV074 is not null)
                _fiveMScreenshotNavButtonV074.Visibility = Visibility.Visible;
            if (alreadyInitialized)
                ConfigureFiveMScreenshotHotkeyV074();
        }
        else
        {
            ReleaseFiveMScreenshotHotkeyV074();
            FiveMScreenshotsV074.Clear();
            if (_fiveMScreenshotNavButtonV074 is not null)
                _fiveMScreenshotNavButtonV074.Visibility = Visibility.Collapsed;
            if (_fiveMScreenshotGalleryPageV074 is not null)
                _fiveMScreenshotGalleryPageV074.Visibility = Visibility.Collapsed;
        }
    }

    private void SetFiveMScreenshotStatusV074(string message)
    {
        if (_fiveMScreenshotGalleryStatusV074 is not null)
            _fiveMScreenshotGalleryStatusV074.Text = message;
    }

    private void BrowseScreenshotFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            SelectedPath = ScreenshotFolderBox.Text,
            Description = "Choose where Afterline stores FiveM screenshots"
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            ScreenshotFolderBox.Text = dialog.SelectedPath;
    }

    private void ResetScreenshotHotkey_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotHotkeyBox.Text = "Ctrl+Shift+F12";
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
