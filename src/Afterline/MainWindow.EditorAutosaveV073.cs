using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    public sealed class RecentEditorProjectItemV073
    {
        public required string FilePath { get; init; }
        public required string FileName { get; init; }
        public required string Detail { get; init; }
        public BitmapSource? Preview { get; init; }
        public bool IsAutosave { get; init; }
    }

    public ObservableCollection<RecentEditorProjectItemV073> RecentEditorProjectsV073 { get; } = new();

    private readonly DispatcherTimer _editorAutosaveTimerV073 = new();
    private bool _editorAutosaveUiInitializedV073;
    private bool _editorAutosaveHooksInitializedV073;
    private bool _editorProjectDirtyV073;
    private int _recentProjectRefreshVersionV073;
    private Border? _editorAutosaveToastV073;
    private TextBlock? _editorAutosaveToastTextV073;
    private CancellationTokenSource? _editorAutosaveToastCtsV073;
    private ComboBox? _editorAutosaveIntervalV073;

    private void EnsureEditorProjectAutosaveUiV073()
    {
        if (_editorAutosaveUiInitializedV073) return;

        _editorAutosaveUiInitializedV073 = true;
        _editorAutosaveTimerV073.Tick += (_, _) => TryAutosaveEditorProjectV073(showToast: true);
        ConfigureEditorAutosaveTimerV073();
        RefreshRecentEditorProjectsV073();
    }

    private FrameworkElement BuildEditorAutosaveSettingsV159()
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "PROJECT AUTOSAVE",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        content.Children.Add(EditorHelpText(
            "Periodically protects the current project against application errors, power loss, or a system shutdown."));

        _editorAutosaveIntervalV073 = new ComboBox
        {
            MinHeight = 34,
            ToolTip = "Choose how often the current Editor project is protected. Select Off to disable project autosave."
        };
        foreach ((string label, int minutes) in new[]
                 {
                     ("Off", 0), ("1 minute", 1), ("5 minutes", 5),
                     ("10 minutes", 10), ("15 minutes", 15), ("30 minutes", 30)
                 })
        {
            _editorAutosaveIntervalV073.Items.Add(new ComboBoxItem { Content = label, Tag = minutes });
        }
        _editorAutosaveIntervalV073.SelectedItem = _editorAutosaveIntervalV073.Items
            .OfType<ComboBoxItem>()
            .First(item => Equals(item.Tag, _settings.Editor.ProjectAutosaveMinutes));
        _editorAutosaveIntervalV073.SelectionChanged += EditorAutosaveIntervalV073_Changed;
        content.Children.Add(CreateEditorField("Save interval", _editorAutosaveIntervalV073));
        content.Children.Add(EditorSubtleNote("Default: every 5 minutes. Successful autosaves appear as a small Editor notification."));
        return content;
    }

    private void InitializeEditorProjectAutosaveHooksV073()
    {
        if (_editorAutosaveHooksInitializedV073 || _editorPage is null)
            return;

        _editorAutosaveHooksInitializedV073 = true;
        if (System.Windows.Application.Current is not null)
            System.Windows.Application.Current.SessionEnding += (_, _) =>
                TryAutosaveEditorProjectV073(showToast: false);
        _editorPage.AddHandler(UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) => _editorProjectDirtyV073 = true), true);
        _editorPage.AddHandler(UIElement.PreviewKeyDownEvent,
            new KeyEventHandler((_, _) => _editorProjectDirtyV073 = true), true);

        _editorAutosaveToastTextV073 = new TextBlock
        {
            Foreground = (Brush)FindResource("Text"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        _editorAutosaveToastV073 = new Border
        {
            Child = _editorAutosaveToastTextV073,
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Success"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 10, 12, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            MaxWidth = 360,
            Visibility = Visibility.Collapsed
        };
        Grid.SetRowSpan(_editorAutosaveToastV073, Math.Max(1, _editorPage.RowDefinitions.Count));
        Panel.SetZIndex(_editorAutosaveToastV073, 100);
        _editorPage.Children.Add(_editorAutosaveToastV073);
    }

    private void EditorAutosaveIntervalV073_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_editorAutosaveIntervalV073?.SelectedItem is not ComboBoxItem item || item.Tag is not int minutes)
            return;

        _settings.Editor.ProjectAutosaveMinutes = minutes;
        try { _settingsService.Save(_settings); }
        catch (Exception ex) { DiagnosticLogger.Error("Unable to save the Editor autosave interval.", ex); }
        ConfigureEditorAutosaveTimerV073();
    }

    private void ConfigureEditorAutosaveTimerV073()
    {
        _editorAutosaveTimerV073.Stop();
        int minutes = _settings.Editor.ProjectAutosaveMinutes;
        if (minutes <= 0) return;
        _editorAutosaveTimerV073.Interval = TimeSpan.FromMinutes(minutes);
        _editorAutosaveTimerV073.Start();
    }

    private void TryAutosaveEditorProjectV073(bool showToast)
    {
        if (_settings.Editor.ProjectAutosaveMinutes <= 0 ||
            !_editorProjectDirtyV073 ||
            !HasEditorProjectContentV067())
            return;

        try
        {
            string path;
            if (!string.IsNullOrWhiteSpace(_editorProjectPathV067))
            {
                path = _editorProjectPathV067;
            }
            else
            {
                string folder = Path.Combine(GetEditorProjectsFolderV070(createDirectory: true), "Autosaves");
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, "Untitled Recovery.afterlineproj");
            }

            SaveEditorProjectToPathV067(path);
            _editorProjectDirtyV073 = false;
            RefreshRecentEditorProjectsV073();
            if (showToast && _editorPage?.Visibility == Visibility.Visible)
                ShowEditorAutosaveToastV073(path);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to autosave the current Editor project.", ex);
        }
    }

    private async void ShowEditorAutosaveToastV073(string path)
    {
        if (_editorAutosaveToastV073 is null || _editorAutosaveToastTextV073 is null) return;
        _editorAutosaveToastCtsV073?.Cancel();
        _editorAutosaveToastCtsV073?.Dispose();
        _editorAutosaveToastCtsV073 = new CancellationTokenSource();
        CancellationToken token = _editorAutosaveToastCtsV073.Token;
        _editorAutosaveToastTextV073.Text = $"Project autosaved · {Path.GetFileName(path)} · {DateTime.Now:HH:mm:ss}";
        _editorAutosaveToastV073.Visibility = Visibility.Visible;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), token);
            if (!token.IsCancellationRequested)
                _editorAutosaveToastV073.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
    }

    private void MarkEditorProjectSavedV073()
    {
        _editorProjectDirtyV073 = false;
        RefreshRecentEditorProjectsV073();
    }

    private void MarkEditorProjectLoadedV073(bool recoveredAutosave)
    {
        _editorProjectDirtyV073 = recoveredAutosave;
        RefreshRecentEditorProjectsV073();
        _editorFitZoom = true;
        ScheduleEditorFitV073();
    }

    private async void RefreshRecentEditorProjectsV073()
    {
        int refreshVersion = ++_recentProjectRefreshVersionV073;
        string folder = GetEditorProjectsFolderV070(createDirectory: false);
        IReadOnlyList<RecentEditorProjectItemV073> projects = await Task.Run(() => ScanRecentEditorProjectsV073(folder));
        if (refreshVersion != _recentProjectRefreshVersionV073) return;

        RecentEditorProjectsV073.Clear();
        foreach (RecentEditorProjectItemV073 project in projects)
            RecentEditorProjectsV073.Add(project);
        if (RecentEditorProjectsEmptyText is not null)
            RecentEditorProjectsEmptyText.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static IReadOnlyList<RecentEditorProjectItemV073> ScanRecentEditorProjectsV073(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return Array.Empty<RecentEditorProjectItemV073>();
            IEnumerable<string> files = Directory.EnumerateFiles(folder, "*.afterlineproj", SearchOption.TopDirectoryOnly);
            string autosaves = Path.Combine(folder, "Autosaves");
            if (Directory.Exists(autosaves))
                files = files.Concat(Directory.EnumerateFiles(autosaves, "*.afterlineproj", SearchOption.TopDirectoryOnly));

            // Keep only the eight newest records while enumerating. This avoids
            // building an in-memory list if a projects folder has grown very large.
            var newest = new PriorityQueue<FileInfo, DateTime>();
            foreach (string path in files)
            {
                var file = new FileInfo(path);
                newest.Enqueue(file, file.LastWriteTimeUtc);
                if (newest.Count > 8)
                    newest.Dequeue();
            }

            return newest.UnorderedItems
                .Select(item => item.Element)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file =>
                {
                    bool autosave = string.Equals(file.Directory?.Name, "Autosaves", StringComparison.OrdinalIgnoreCase);
                    return new RecentEditorProjectItemV073
                    {
                        FilePath = file.FullName,
                        FileName = autosave ? "Autosave recovery" : Path.GetFileNameWithoutExtension(file.Name),
                        Detail = $"{file.LastWriteTime:dd MMM yyyy · HH:mm} · {FormatProjectSizeV073(file.Length)}",
                        Preview = ReadProjectThumbnailV073(file.FullName),
                        IsAutosave = autosave
                    };
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to build the recent Editor projects list.", ex);
            return Array.Empty<RecentEditorProjectItemV073>();
        }
    }

    private static BitmapSource? ReadProjectThumbnailV073(string path)
    {
        try
        {
            using FileStream file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
            ZipArchiveEntry? entry = archive.GetEntry("media/base.png") ?? archive.Entries
                .FirstOrDefault(item => item.FullName.StartsWith("layers/", StringComparison.OrdinalIgnoreCase) &&
                                        item.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (entry is null) return null;
            using Stream source = entry.Open();
            using var memory = new MemoryStream();
            source.CopyTo(memory);
            memory.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 120;
            bitmap.StreamSource = memory;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatProjectSizeV073(long bytes)
        => bytes >= 1024 * 1024
            ? $"{bytes / (1024d * 1024d):0.0} MB"
            : $"{Math.Max(1, bytes / 1024d):0} KB";

    private void RecentEditorProjectsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentEditorProjectsList.SelectedItem is not RecentEditorProjectItemV073 project ||
            !File.Exists(project.FilePath))
            return;

        if (HasEditorProjectContentV067() &&
            !string.Equals(_editorProjectPathV067, project.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            var warning = new NewProjectWarningWindowV067(this, "Open this recent project?");
            if (warning.ShowDialog() != true || warning.Choice == NewProjectChoiceV067.Cancel)
                return;
            if (warning.Choice == NewProjectChoiceV067.Save && !SaveCurrentEditorProjectV067())
                return;
        }

        try
        {
            if (_editorPage is null) return;
            ShowPage(_editorPage, "Editor", $"Editing {Path.GetFileName(project.FilePath)}");
            LoadEditorProjectFromPathV067(project.FilePath);
            _editorProjectPathV067 = project.IsAutosave ? null : project.FilePath;
            UpdateProjectLabelV067();
            SetEditorStatus(project.IsAutosave
                ? "Recovered the latest autosaved Editor project. Save it to choose a permanent name."
                : $"Loaded project · {Path.GetFileName(project.FilePath)}.");
            MarkEditorProjectLoadedV073(project.IsAutosave);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open a recent Editor project.", ex);
            System.Windows.MessageBox.Show(this, "Afterline could not open this project.\n\n" + ex.Message,
                "Unable to open project", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteRecentEditorProjectV076_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string path } || !File.Exists(path))
            return;

        string fileName = Path.GetFileName(path);
        if (System.Windows.MessageBox.Show(
                this,
                $"Move '{fileName}' to the Recycle Bin?\n\nThis removes it from Recent Editor Projects.",
                "Delete Editor project",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            if (string.Equals(_editorProjectPathV067, path, StringComparison.OrdinalIgnoreCase))
                _editorProjectPathV067 = null;
            RefreshRecentEditorProjectsV073();
            SetEditorStatus($"Moved {fileName} to the Recycle Bin.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to delete a recent Editor project.", ex);
            System.Windows.MessageBox.Show(
                this,
                "Afterline could not move this project to the Recycle Bin.\n\n" + ex.Message,
                "Unable to delete project",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
