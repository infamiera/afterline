using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private Border? _editorProjectFolderPrivacyOverlayV075;

    private void StreamerModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        ApplyStreamerModePresentationV075();
    }

    private void ApplyStreamerModePresentationV075()
    {
        bool enabled = StreamerModeCheck?.IsChecked == true;
        StreamerModePresentationService.Enabled = enabled;

        UpdateStreamerPathOverlayV075(SearchRootPrivacyOverlay, SearchRootPrivacyText, SearchRootBox.Text, enabled);
        UpdateStreamerPathOverlayV075(ScreenshotFolderPrivacyOverlay, ScreenshotFolderPrivacyText, ScreenshotFolderBox.Text, enabled);
        UpdateStreamerPathOverlayV075(ArchiveRootPrivacyOverlay, ArchiveRootPrivacyText, ArchiveRootBox.Text, enabled);

        if (_settings is not null)
            ArchiveRootText.Text = StreamerModePresentationService.PathForDisplay(_settings.ArchiveRoot);
        if (_logReaderPathText is not null)
            _logReaderPathText.Text = StreamerModePresentationService.PathForDisplay(_logReaderCurrentPath);

        if (_editorProjectFolderPrivacyOverlayV075?.Child is TextBlock editorPath)
        {
            editorPath.Text = StreamerModePresentationService.PathForDisplay(_settings.Editor.ProjectsFolder);
            _editorProjectFolderPrivacyOverlayV075.Visibility = enabled
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        RefreshQuickLogCollectionsV050();
    }

    private static void UpdateStreamerPathOverlayV075(
        Border overlay,
        TextBlock text,
        string actualPath,
        bool enabled)
    {
        text.Text = StreamerModePresentationService.PathForDisplay(actualPath);
        overlay.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateStreamerPathOverlayV075(string actualPath)
    {
        var text = new TextBlock
        {
            Text = StreamerModePresentationService.PathForDisplay(actualPath),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        var overlay = new Border
        {
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 5, 7, 5),
            IsHitTestVisible = false,
            Visibility = StreamerModePresentationService.Enabled
                ? Visibility.Visible
                : Visibility.Collapsed,
            Child = text
        };
        Grid.SetColumn(overlay, 0);
        return overlay;
    }
}
