using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private bool _editorAlignmentV062Initialized;
    private TextAlignment _editorChatTextAlignmentV063 = TextAlignment.Left;
    private Button? _editorTextAlignLeftButtonV063;
    private Button? _editorTextAlignCenterButtonV063;
    private Button? _editorTextAlignRightButtonV063;

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
            Text = "Text alignment",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
            Margin = new Thickness(0, 2, 0, 6)
        });

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        _editorTextAlignLeftButtonV063 = CreateEditorAlignmentButtonV063(
            "Left",
            "Align text to the left inside the chat block.",
            TextAlignment.Left);
        _editorTextAlignCenterButtonV063 = CreateEditorAlignmentButtonV063(
            "Center",
            "Center text inside the chat block without moving the block itself.",
            TextAlignment.Center);
        _editorTextAlignRightButtonV063 = CreateEditorAlignmentButtonV063(
            "Right",
            "Align text to the right inside the chat block without moving the block itself.",
            TextAlignment.Right);

        actions.Children.Add(_editorTextAlignLeftButtonV063);
        actions.Children.Add(_editorTextAlignCenterButtonV063);
        actions.Children.Add(_editorTextAlignRightButtonV063);
        chatContent.Children.Insert(insertAt, actions);
        RefreshEditorTextAlignmentButtonsV063();
    }

    private Button CreateEditorAlignmentButtonV063(string label, string tooltip, TextAlignment alignment)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 7, 0),
            MinWidth = 62,
            ToolTip = tooltip
        };
        button.Click += (_, _) => SetEditorTextAlignmentV063(alignment);
        return button;
    }

    private void SetEditorTextAlignmentV063(TextAlignment alignment)
    {
        _editorChatTextAlignmentV063 = alignment;
        RefreshEditorTextAlignmentButtonsV063();
        ScheduleEditorChatRender();

        string name = alignment switch
        {
            TextAlignment.Center => "centered",
            TextAlignment.Right => "aligned right",
            _ => "aligned left"
        };
        SetEditorStatus($"Chat text {name} within its block.");
    }

    private void RefreshEditorTextAlignmentButtonsV063()
    {
        SetEditorTextAlignmentButtonStateV063(_editorTextAlignLeftButtonV063, TextAlignment.Left);
        SetEditorTextAlignmentButtonStateV063(_editorTextAlignCenterButtonV063, TextAlignment.Center);
        SetEditorTextAlignmentButtonStateV063(_editorTextAlignRightButtonV063, TextAlignment.Right);
    }

    private void SetEditorTextAlignmentButtonStateV063(Button? button, TextAlignment alignment)
    {
        if (button is null) return;
        bool selected = _editorChatTextAlignmentV063 == alignment;
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        button.Opacity = selected ? 1.0 : 0.78;
    }
}
