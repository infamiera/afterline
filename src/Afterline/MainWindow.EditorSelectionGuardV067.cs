using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Afterline;

public partial class MainWindow
{
    private bool _editorSelectionGuardV067Initialized;

    private void EnsureEditorSelectionGuardV067()
    {
        if (_editorSelectionGuardV067Initialized ||
            !_editorToolButtons.TryGetValue("filters", out Button? filters))
            return;

        _editorSelectionGuardV067Initialized = true;
        filters.AddHandler(
            Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) => RestoreSelectionAfterLegacyFilterResetV067()),
            true);
    }

    private void RestoreSelectionAfterLegacyFilterResetV067()
    {
        bool[]? mask = _editorSelectionMaskCanary?.ToArray();
        int width = _editorSelectionWidthCanary;
        int height = _editorSelectionHeightCanary;
        if (mask is null || width <= 0 || height <= 0)
            return;

        // The legacy prewarm queues its reset at ApplicationIdle. SystemIdle is
        // deliberately lower priority, guaranteeing this restoration happens after
        // that reset regardless of event-handler registration order.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_editorBaseOriginal is null ||
                _editorBaseOriginal.PixelWidth != width ||
                _editorBaseOriginal.PixelHeight != height ||
                mask.Length != width * height)
                return;

            _editorSelectionMaskCanary = mask;
            _editorSelectionWidthCanary = width;
            _editorSelectionHeightCanary = height;
            RenderSelectionBoundaryCanary();
            RefreshSelectionHighlightV067();
        }), DispatcherPriority.SystemIdle);
    }
}
