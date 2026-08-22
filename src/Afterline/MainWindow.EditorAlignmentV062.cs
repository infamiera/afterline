using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private bool _editorAlignmentV062Initialized;

    private void EnsureEditorAlignmentV062()
    {
        if (_editorAlignmentV062Initialized ||
            !_editorToolPanels.TryGetValue("chat", out FrameworkElement? panel) ||
            panel is not ScrollViewer scroll ||
            scroll.Content is not StackPanel chatContent ||
            _editorChatXSlider?.Parent is not FrameworkElement xPanel)
            return;

        _editorAlignmentV062Initialized = true;

        int insertAt = chatContent.Children.IndexOf(xPanel);
        if (insertAt < 0) insertAt = chatContent.Children.Count;

        chatContent.Children.Insert(insertAt++, new TextBlock
        {
            Text = "Horizontal alignment",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 2, 0, 6)
        });

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        actions.Children.Add(CreateEditorAlignmentButtonV062("Left", "Align chat to the left edge.", () => AlignEditorChatV062(0)));
        actions.Children.Add(CreateEditorAlignmentButtonV062("Center", "Center chat horizontally in the screenshot.", () => AlignEditorChatV062(1)));
        actions.Children.Add(CreateEditorAlignmentButtonV062("Right", "Align chat to the right edge of the screenshot.", () => AlignEditorChatV062(2)));
        chatContent.Children.Insert(insertAt, actions);
    }

    private Button CreateEditorAlignmentButtonV062(string label, string tooltip, Action action)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 7, 0),
            MinWidth = 62,
            ToolTip = tooltip
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void AlignEditorChatV062(int alignment)
    {
        if (_editorChatBitmap is null || _editorChatXSlider is null)
        {
            SetEditorStatus("Add chat text before changing its alignment.");
            return;
        }

        double canvasWidth;
        if (_editorBaseOriginal is not null)
        {
            canvasWidth = Math.Max(1, _editorBaseOriginal.PixelWidth);
        }
        else
        {
            double compositionWidth = _editorComposition?.Width ?? 0;
            if (!double.IsFinite(compositionWidth) || compositionWidth <= 0)
                compositionWidth = _editorChatBitmap.PixelWidth;
            canvasWidth = Math.Max(_editorChatBitmap.PixelWidth, compositionWidth);
        }

        double available = Math.Max(0, canvasWidth - _editorChatBitmap.PixelWidth);
        double x = alignment switch
        {
            1 => available / 2.0,
            2 => available,
            _ => 0
        };

        _editorChatXSlider.Value = Math.Clamp(x, _editorChatXSlider.Minimum, _editorChatXSlider.Maximum);
        UpdateEditorCanvasSize();

        string name = alignment switch
        {
            1 => "centered",
            2 => "aligned right",
            _ => "aligned left"
        };
        SetEditorStatus($"Chat block {name}.");
    }
}
