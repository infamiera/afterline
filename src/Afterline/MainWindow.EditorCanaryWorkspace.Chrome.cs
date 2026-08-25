using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private void ConfigureEditorChromeCanary()
    {
        if (PageTitle.Parent is StackPanel titleStack &&
            titleStack.Parent is Grid headerGrid &&
            headerGrid.Parent is Grid mainGrid)
        {
            _editorGlobalHeaderCanary = headerGrid;
            _editorMainLayoutCanary = mainGrid;
            _editorMainLayoutMarginCanary = mainGrid.Margin;
            if (mainGrid.RowDefinitions.Count >= 5)
            {
                _editorHeaderGapHeightCanary = mainGrid.RowDefinitions[1].Height;
                _editorFooterGapHeightCanary = mainGrid.RowDefinitions[3].Height;
                _editorFooterHeightCanary = mainGrid.RowDefinitions[4].Height;
            }

            if (mainGrid.Parent is Grid root)
            {
                _editorRootLayoutCanary = root;
                if (root.ColumnDefinitions.Count > 0)
                    _editorSidebarWidthCanary = root.ColumnDefinitions[0].Width;
            }
        }

        if (TrayStateText.Parent is StackPanel stack && stack.Parent is Border card)
            _editorSidebarCaptureCardCanary = card;

        _editorFullscreenCloseCanary = new Button
        {
            Content = "×",
            Width = 36,
            Height = 34,
            FontSize = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 5, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            ToolTip = "Exit Full Screen Editor"
        };
        _editorFullscreenCloseCanary.Click += (_, _) => ExitEditorFullscreenCanary();
        Grid.SetRowSpan(_editorFullscreenCloseCanary, 3);
        Panel.SetZIndex(_editorFullscreenCloseCanary, 100);
        _editorPage!.Children.Add(_editorFullscreenCloseCanary);

        PreviewKeyDown += (_, e) =>
        {
            if (_editorFullscreenWorkspaceCanary && e.Key == Key.Escape)
            {
                ExitEditorFullscreenCanary();
                e.Handled = true;
            }
        };
    }

    private void ApplyEditorChromeCanary(bool editorVisible)
    {
        ApplyAutomaticEditorSidebarV068(editorVisible);

        if (_editorGlobalHeaderCanary is not null)
            _editorGlobalHeaderCanary.Visibility = editorVisible ? Visibility.Collapsed : Visibility.Visible;
        if (_editorSidebarCaptureCardCanary is not null)
            _editorSidebarCaptureCardCanary.Visibility = editorVisible ? Visibility.Collapsed : Visibility.Visible;
        if (_profileHeaderButton is not null)
            _profileHeaderButton.Visibility = editorVisible ? Visibility.Collapsed : Visibility.Visible;

        if (_editorMainLayoutCanary is not null && _editorMainLayoutCanary.RowDefinitions.Count >= 5)
        {
            _editorMainLayoutCanary.RowDefinitions[1].Height = editorVisible ? new GridLength(0) : _editorHeaderGapHeightCanary;
            _editorMainLayoutCanary.RowDefinitions[3].Height = editorVisible ? new GridLength(0) : _editorFooterGapHeightCanary;
            _editorMainLayoutCanary.RowDefinitions[4].Height = editorVisible ? new GridLength(0) : _editorFooterHeightCanary;
            if (!_editorFullscreenWorkspaceCanary)
                _editorMainLayoutCanary.Margin = editorVisible ? new Thickness(12, 10, 12, 10) : _editorMainLayoutMarginCanary;
        }
    }

    private void ToggleEditorFullscreenCanary()
    {
        if (_editorFullscreenWorkspaceCanary)
            ExitEditorFullscreenCanary();
        else
            EnterEditorFullscreenCanary();
    }

    private void EnterEditorFullscreenCanary()
    {
        if (_editorFullscreenWorkspaceCanary || _editorPage?.Visibility != Visibility.Visible)
            return;

        _editorFullscreenWorkspaceCanary = true;
        _editorSavedWindowStyleCanary = WindowStyle;
        _editorSavedWindowStateCanary = WindowState;
        _editorSavedResizeModeCanary = ResizeMode;

        // WPF can ignore a chrome/state transition when a maximized window changes
        // directly to borderless. Normalize first, then enter maximized borderless
        // mode so the toolbar button works from both normal and maximized layouts.
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;

        if (_editorRootLayoutCanary?.ColumnDefinitions.Count > 0)
            _editorRootLayoutCanary.ColumnDefinitions[0].Width = new GridLength(0);
        if (_editorMainLayoutCanary is not null)
            _editorMainLayoutCanary.Margin = new Thickness(6);
        if (_editorFullscreenCloseCanary is not null)
            _editorFullscreenCloseCanary.Visibility = Visibility.Visible;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        ApplyEditorChromeCanary(true);
        _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private void ExitEditorFullscreenCanary()
    {
        if (!_editorFullscreenWorkspaceCanary) return;
        _editorFullscreenWorkspaceCanary = false;

        if (_editorRootLayoutCanary?.ColumnDefinitions.Count > 0)
            _editorRootLayoutCanary.ColumnDefinitions[0].Width = _editorSidebarWidthCanary;
        if (_editorFullscreenCloseCanary is not null)
            _editorFullscreenCloseCanary.Visibility = Visibility.Collapsed;

        WindowStyle = _editorSavedWindowStyleCanary;
        ResizeMode = _editorSavedResizeModeCanary;
        WindowState = _editorSavedWindowStateCanary;
        if (_editorMainLayoutCanary is not null)
            _editorMainLayoutCanary.Margin = _editorPage?.Visibility == Visibility.Visible
                ? new Thickness(12, 10, 12, 10)
                : _editorMainLayoutMarginCanary;

        ApplyEditorChromeCanary(_editorPage?.Visibility == Visibility.Visible);
        _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private void ResizeCanaryOverlays()
    {
        if (_editorComposition is null) return;
        foreach (Canvas? canvas in new[] { _editorSelectionOverlayCanary, _editorSnapGuideCanvasCanary })
        {
            if (canvas is null) continue;
            canvas.Width = _editorComposition.Width;
            canvas.Height = _editorComposition.Height;
        }
        if (_editorSelectionBoundaryImageCanary is not null && _editorBaseOriginal is not null)
        {
            _editorSelectionBoundaryImageCanary.Width = _editorBaseOriginal.PixelWidth;
            _editorSelectionBoundaryImageCanary.Height = _editorBaseOriginal.PixelHeight;
        }
    }

    private void ConfigureEditorExportRefreshCanary()
    {
        if (_editorPage is null) return;
        _editorPage.PreviewMouseDown += (_, e) =>
        {
            if (FindVisualParentCanary<Button>(e.OriginalSource as DependencyObject) is not Button button)
                return;

            string text = button.Content?.ToString() ?? string.Empty;
            if (text.Contains("Export", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Copy", StringComparison.OrdinalIgnoreCase))
            {
                RenderExtraChatLayersCanary();
                if (_editorFilterTimerCanary?.IsEnabled == true ||
                    _editorFilterPreviewRenderCountV070 > 0)
                {
                    _editorFilterTimerCanary?.Stop();
                    _editorFilterPreviewVersionV070++;
                    ApplyCanaryFilterPreview();
                }
            }
        };
    }

    private static T? FindVisualParentCanary<T>(DependencyObject? child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (current is T match) return match;
            try
            {
                current = VisualTreeHelper.GetParent(current);
            }
            catch
            {
                current = LogicalTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildrenCanary<T>(DependencyObject root) where T : DependencyObject
    {
        int count;
        try { count = VisualTreeHelper.GetChildrenCount(root); }
        catch { yield break; }

        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (T nested in FindVisualChildrenCanary<T>(child))
                yield return nested;
        }
    }
}
