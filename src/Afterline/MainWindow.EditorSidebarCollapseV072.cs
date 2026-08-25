using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private Button? _editorLeftSidebarToggleV072;
    private Button? _editorRightSidebarToggleV072;
    private ColumnDefinition? _editorRightSidebarGapColumnV072;
    private ColumnDefinition? _editorRightSidebarColumnV072;
    private GridLength _editorRightSidebarWidthV072 = new(300);
    private double _editorRightSidebarMinWidthV072 = 260;
    private bool _editorRightSidebarCollapsedV072;
    private string _editorLastToolKeyV072 = "chat";

    private void ToggleEditorLeftSidebarV072()
    {
        bool isOpen = _editorToolPanelHost?.Visibility == Visibility.Visible &&
                      _editorToolPanelColumn?.Width.Value > 0;
        if (isOpen)
        {
            CloseEditorToolPanel();
        }
        else
        {
            string key = _editorToolPanels.ContainsKey(_editorLastToolKeyV072)
                ? _editorLastToolKeyV072
                : "chat";
            ShowEditorToolPanel(key, forceOpen: true);
            if (_editorToolPanelColumn is not null)
                _editorToolPanelColumn.Width = new GridLength(
                    Math.Clamp(_editorToolPanelWidthCanary, 220, 620));
        }

        UpdateEditorSidebarToggleStateV072();
        _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private void ToggleEditorRightSidebarV072()
    {
        if (_editorRightSidebarV067 is null ||
            _editorRightSidebarColumnV072 is null ||
            _editorRightSidebarGapColumnV072 is null)
            return;

        if (!_editorRightSidebarCollapsedV072)
        {
            if (_editorRightSidebarColumnV072.Width.Value > 0)
                _editorRightSidebarWidthV072 = _editorRightSidebarColumnV072.Width;
            _editorRightSidebarMinWidthV072 = _editorRightSidebarColumnV072.MinWidth;
            _editorRightSidebarV067.Visibility = Visibility.Collapsed;
            _editorRightSidebarColumnV072.MinWidth = 0;
            _editorRightSidebarColumnV072.Width = new GridLength(0);
            _editorRightSidebarGapColumnV072.Width = new GridLength(0);
            _editorRightSidebarCollapsedV072 = true;
        }
        else
        {
            _editorRightSidebarColumnV072.MinWidth = Math.Max(0, _editorRightSidebarMinWidthV072);
            _editorRightSidebarColumnV072.Width = _editorRightSidebarWidthV072.Value > 0
                ? _editorRightSidebarWidthV072
                : new GridLength(300);
            _editorRightSidebarGapColumnV072.Width = new GridLength(8);
            _editorRightSidebarV067.Visibility = Visibility.Visible;
            _editorRightSidebarCollapsedV072 = false;
        }

        UpdateEditorSidebarToggleStateV072();
        _ = Dispatcher.BeginInvoke(new Action(FitEditorPreviewToWindow));
    }

    private void UpdateEditorSidebarToggleStateV072()
    {
        bool leftOpen = _editorToolPanelHost?.Visibility == Visibility.Visible &&
                        _editorToolPanelColumn?.Width.Value > 0;
        if (_editorLeftSidebarToggleV072 is not null)
        {
            _editorLeftSidebarToggleV072.Content = leftOpen ? "◀" : "▶";
            _editorLeftSidebarToggleV072.ToolTip = leftOpen
                ? "Collapse the left Editor panel"
                : "Open the left Editor panel";
        }

        if (_editorRightSidebarToggleV072 is not null)
        {
            _editorRightSidebarToggleV072.Content = _editorRightSidebarCollapsedV072 ? "◀" : "▶";
            _editorRightSidebarToggleV072.ToolTip = _editorRightSidebarCollapsedV072
                ? "Open the right Editor panel"
                : "Collapse the right Editor panel";
        }
    }
}
