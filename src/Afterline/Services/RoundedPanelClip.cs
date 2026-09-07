using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline.Services;

internal static class RoundedPanelClip
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Border, object> Attached = new();

    public static void Attach(Border border)
    {
        // Only uniform rounded layout panels; never override custom canvas/image clips.
        double radius = border.CornerRadius.TopLeft;
        if (radius <= 0 || border.Child is not Grid || border.Clip is not null ||
            border.CornerRadius.TopRight != radius || border.CornerRadius.BottomLeft != radius ||
            border.CornerRadius.BottomRight != radius || Attached.TryGetValue(border, out _)) return;
        Attached.Add(border, new object());
        border.SizeChanged += (_, _) => Refresh(border);
        border.Loaded += (_, _) => Refresh(border);
        Refresh(border);
    }

    private static void Refresh(Border border)
    {
        if (border.ActualWidth <= 0 || border.ActualHeight <= 0) return;
        double radius = border.CornerRadius.TopLeft;
        // ClipToBounds alone is rectangular: nested scrollbars otherwise paint over corners.
        border.Clip = new RectangleGeometry(new Rect(0, 0, border.ActualWidth, border.ActualHeight), radius, radius);
    }
}
