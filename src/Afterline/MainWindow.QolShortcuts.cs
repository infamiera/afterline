using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _qolV050Initialized;
    private Button? _dashboardOpenCurrentLogButton;
    private Button? _liveOpenCurrentLogButton;

    private void EnsureQolV050()
    {
        if (_qolV050Initialized) return;
        _qolV050Initialized = true;

        ConfigureOpenCurrentLogShortcuts();
        ConfigureActiveChatSearchV050();
        ConfigureExtendedLineSelectionV050();
        ConfigureDragAndDropV050();
        ConfigureKeyboardShortcutsV050();
        ConfigureArchiveQuickAccessV050();

        _uiTimer.Tick += (_, _) => UpdateCurrentLogShortcutStateV050();
        UpdateCurrentLogShortcutStateV050();
    }

    private void ConfigureOpenCurrentLogShortcuts()
    {
        Button? finish = FindButtonByContent(DashboardPage, "Finish Session");
        if (finish?.Parent is StackPanel dashboardActions)
        {
            _dashboardOpenCurrentLogButton = new Button
            {
                Content = "Open current log",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Open the currently active chatlog directly in Log Reader."
            };
            _dashboardOpenCurrentLogButton.Click += async (_, _) => await OpenCurrentLogInReaderV050Async();
            dashboardActions.Children.Insert(0, _dashboardOpenCurrentLogButton);
        }

        if (_liveActionStatus?.Parent is WrapPanel liveActions)
        {
            _liveOpenCurrentLogButton = new Button
            {
                Content = "Open current log",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Open the currently active chatlog directly in Log Reader."
            };
            _liveOpenCurrentLogButton.Click += async (_, _) => await OpenCurrentLogInReaderV050Async();
            int index = liveActions.Children.IndexOf(_liveActionStatus);
            liveActions.Children.Insert(Math.Max(0, index), _liveOpenCurrentLogButton);
        }
    }

    private async Task OpenCurrentLogInReaderV050Async()
    {
        string? path = _journal.ActiveFile;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            System.Windows.MessageBox.Show(this, "There is no active chatlog to open yet.", "Afterline", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await OpenLogInReaderAsync(path, null);
    }

    private void UpdateCurrentLogShortcutStateV050()
    {
        bool available = _journal.HasActiveSession && !string.IsNullOrWhiteSpace(_journal.ActiveFile);
        if (_dashboardOpenCurrentLogButton is not null) _dashboardOpenCurrentLogButton.IsEnabled = available;
        if (_liveOpenCurrentLogButton is not null) _liveOpenCurrentLogButton.IsEnabled = available;
    }

    private void ConfigureExtendedLineSelectionV050()
    {
        LiveChatList.SelectionMode = SelectionMode.Extended;
        LiveChatList.ToolTip = "Ctrl-click individual lines · Shift-click a range · Ctrl+C copies the selection.";
        if (_logReaderList is not null)
        {
            _logReaderList.SelectionMode = SelectionMode.Extended;
            _logReaderList.ToolTip = "Ctrl-click individual lines · Shift-click a range · Ctrl+C copies the selection.";
        }
    }

    private void ConfigureDragAndDropV050()
    {
        if (_logReaderPage is not null)
        {
            _logReaderPage.AllowDrop = true;
            _logReaderPage.PreviewDragOver += (_, e) =>
            {
                e.Effects = DroppedFileV050(e, ".txt") is null ? DragDropEffects.None : DragDropEffects.Copy;
                e.Handled = true;
            };
            _logReaderPage.PreviewDrop += async (_, e) =>
            {
                string? path = DroppedFileV050(e, ".txt");
                e.Handled = true;
                if (path is not null) await OpenLogInReaderAsync(path, null);
            };
        }

        if (_editorPage is not null)
        {
            _editorPage.AllowDrop = true;
            _editorPage.PreviewDragOver += (_, e) =>
            {
                e.Effects = DroppedFileV050(e, ".png", ".jpg", ".jpeg", ".bmp") is null ? DragDropEffects.None : DragDropEffects.Copy;
                e.Handled = true;
            };
            _editorPage.PreviewDrop += (_, e) =>
            {
                string? path = DroppedFileV050(e, ".png", ".jpg", ".jpeg", ".bmp");
                e.Handled = true;
                if (path is not null) LoadEditorImageV050(path);
            };
        }
    }

    private static string? DroppedFileV050(DragEventArgs e, params string[] extensions)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetData(DataFormats.FileDrop) is not string[] files) return null;
        return files.FirstOrDefault(path => File.Exists(path) && extensions.Any(ext => string.Equals(Path.GetExtension(path), ext, StringComparison.OrdinalIgnoreCase)));
    }

    private void LoadEditorImageV050(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            if (bitmap.CanFreeze) bitmap.Freeze();

            _editorBaseOriginal = bitmap;
            if (_editorRemoveImageButton is not null) _editorRemoveImageButton.IsEnabled = true;
            ResetEditorAdjustmentSliders();
            ClearEditorMarkup(resetHistory: true);
            ApplyEditorImageAdjustments();
            SetEditorStatus($"Loaded {Path.GetFileName(path)} · {bitmap.PixelWidth:N0} × {bitmap.PixelHeight:N0}px.");
            if (_editorFitZoom) _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load dropped image into Editor.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to load image", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConfigureKeyboardShortcutsV050() => PreviewKeyDown += QolV050_PreviewKeyDown;

    private async void QolV050_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        bool ctrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (!ctrl) return;

        if (e.Key == Key.F)
        {
            if (_logReaderPage?.Visibility == Visibility.Visible && _logReaderFindBoxV050 is not null) FocusFindV050(_logReaderFindBoxV050);
            else if (LivePage.Visibility == Visibility.Visible && _liveFindBoxV050 is not null) FocusFindV050(_liveFindBoxV050);
            else
            {
                ShowPage(SearchPage, "Search", "Search one or multiple terms across your chatlog folders");
                SearchQueryBox.Focus();
                SearchQueryBox.SelectAll();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.O)
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
            if (dialog.ShowDialog(this) == true) await OpenLogInReaderAsync(dialog.FileName, null);
            return;
        }

        if (e.Key == Key.S && _editorPage?.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            EditorExportPng_Click(this, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.C && !IsTextEditorFocusedV050())
        {
            if (_logReaderPage?.Visibility == Visibility.Visible && _logReaderList?.SelectedItems.Count > 0)
            {
                e.Handled = true;
                if (shift && _logReaderList.SelectedItems.Count == 1) CopyLogReaderContext(5);
                else CopySelectedLogLinesV050();
            }
            else if (LivePage.Visibility == Visibility.Visible && LiveChatList.SelectedItems.Count > 0)
            {
                e.Handled = true;
                if (shift && LiveChatList.SelectedItems.Count == 1) CopyLiveContextV050(5);
                else CopySelectedLiveLinesV050();
            }
        }
    }

    private static bool IsTextEditorFocusedV050() => Keyboard.FocusedElement is TextBox or PasswordBox or RichTextBox;
    private static void FocusFindV050(TextBox box) { box.Focus(); box.SelectAll(); }
}
