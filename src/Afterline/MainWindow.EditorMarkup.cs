using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private enum EditorMarkupTool
    {
        Paint,
        Erase,
        Text
    }

    private sealed record EditorTextOverlaySnapshot(
        string Text,
        double Left,
        double Top,
        string FontFamily,
        double FontSize,
        FontWeight FontWeight,
        Color Color);

    private sealed record EditorMarkupSnapshot(
        StrokeCollection Strokes,
        IReadOnlyList<EditorTextOverlaySnapshot> TextOverlays);

    private readonly List<EditorMarkupSnapshot> _editorMarkupHistory = new();
    private int _editorMarkupHistoryIndex = -1;
    private bool _editorRestoringMarkup;
    private string? _editorPendingText;
    private EditorMarkupTool _editorMarkupTool = EditorMarkupTool.Paint;

    private void ConfigureEditorContextMenus()
    {
        if (_editorInput is not null)
        {
            var inputMenu = CreateAfterlineContextMenu();
            inputMenu.Items.Add(CreateAfterlineContextMenuItem("Cut", (_, _) => _editorInput.Cut()));
            inputMenu.Items.Add(CreateAfterlineContextMenuItem("Copy", (_, _) => _editorInput.Copy()));
            inputMenu.Items.Add(CreateAfterlineContextMenuItem("Paste", (_, _) => _editorInput.Paste()));
            inputMenu.Items.Add(CreateAfterlineContextMenuSeparator());
            inputMenu.Items.Add(CreateAfterlineContextMenuItem("Select all", (_, _) => _editorInput.SelectAll()));
            _editorInput.ContextMenu = inputMenu;
        }

        if (_editorInkCanvas is not null)
        {
            var canvasMenu = CreateAfterlineContextMenu();
            canvasMenu.Items.Add(CreateAfterlineContextMenuItem("Undo", EditorUndo_Click));
            canvasMenu.Items.Add(CreateAfterlineContextMenuItem("Redo", EditorRedo_Click));
            canvasMenu.Items.Add(CreateAfterlineContextMenuSeparator());
            canvasMenu.Items.Add(CreateAfterlineContextMenuItem("Paint", EditorPaintTool_Click));
            canvasMenu.Items.Add(CreateAfterlineContextMenuItem("Erase", EditorEraseTool_Click));
            canvasMenu.Items.Add(CreateAfterlineContextMenuItem("Add text…", EditorTextTool_Click));
            canvasMenu.Items.Add(CreateAfterlineContextMenuSeparator());
            canvasMenu.Items.Add(CreateAfterlineContextMenuItem("Clear markup", EditorClearMarkup_Click));
            _editorInkCanvas.ContextMenu = canvasMenu;
        }
    }

    private void EditorPaintTool_Click(object sender, RoutedEventArgs e)
        => SetEditorMarkupTool(EditorMarkupTool.Paint);

    private void EditorEraseTool_Click(object sender, RoutedEventArgs e)
        => SetEditorMarkupTool(EditorMarkupTool.Erase);

    private void EditorTextTool_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new TextPromptWindow("Add text", "Enter the text to place on the Editor canvas.") { Owner = this };
        if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Value)) return;

        _editorPendingText = prompt.Value;
        SetEditorMarkupTool(EditorMarkupTool.Text);
        SetEditorStatus("Text tool ready — click the canvas where the label should be placed.");
    }

    private void SetEditorMarkupTool(EditorMarkupTool tool)
    {
        _editorMarkupTool = tool;
        if (_editorInkCanvas is not null)
        {
            _editorInkCanvas.EditingMode = tool switch
            {
                EditorMarkupTool.Erase => InkCanvasEditingMode.EraseByPoint,
                EditorMarkupTool.Text => InkCanvasEditingMode.None,
                _ => InkCanvasEditingMode.Ink
            };
        }

        Brush raised = (Brush)FindResource("Raised");
        Brush accent = (Brush)FindResource("Accent");
        if (_editorPaintButton is not null) _editorPaintButton.Background = tool == EditorMarkupTool.Paint ? accent : raised;
        if (_editorEraseButton is not null) _editorEraseButton.Background = tool == EditorMarkupTool.Erase ? accent : raised;
        if (_editorTextButton is not null) _editorTextButton.Background = tool == EditorMarkupTool.Text ? accent : raised;

        if (tool == EditorMarkupTool.Paint) SetEditorStatus("Paint tool active. Drag over the canvas to draw.");
        if (tool == EditorMarkupTool.Erase) SetEditorStatus("Eraser active. Drag over painted strokes to remove them.");
    }

    private void UpdateEditorDrawingAttributes()
    {
        if (_editorInkCanvas is null) return;
        double size = Math.Max(1, _editorBrushSizeSlider?.Value ?? 5);
        Color color = ResolveEditorPaintColor();

        DrawingAttributes attributes = _editorInkCanvas.DefaultDrawingAttributes.Clone();
        attributes.Color = color;
        attributes.Width = size;
        attributes.Height = size;
        attributes.FitToCurve = true;
        _editorInkCanvas.DefaultDrawingAttributes = attributes;
        _editorInkCanvas.EraserShape = new EllipseStylusShape(Math.Max(2, size), Math.Max(2, size));
    }

    private Color ResolveEditorPaintColor()
    {
        string selected = _editorPaintColorBox?.SelectedItem?.ToString() ?? "White";
        return selected switch
        {
            "Black" => Colors.Black,
            "Red" => Color.FromRgb(0xFF, 0x3B, 0x30),
            "Yellow" => Color.FromRgb(0xFF, 0xF3, 0x00),
            "Green" => Color.FromRgb(0x20, 0xE8, 0x5A),
            "Blue" => Color.FromRgb(0x16, 0x9B, 0xFF),
            "Purple" => Color.FromRgb(0xC2, 0xA2, 0xDA),
            "Orange" => Color.FromRgb(0xFF, 0xA5, 0x1F),
            _ => Colors.White
        };
    }

    private void EditorInkCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editorMarkupTool != EditorMarkupTool.Text ||
            string.IsNullOrWhiteSpace(_editorPendingText) ||
            _editorInkCanvas is null)
            return;

        Point position = e.GetPosition(_editorInkCanvas);
        (FontFamily family, FontWeight weight) = ResolveEditorFont();
        var overlay = new TextBlock
        {
            Text = _editorPendingText,
            FontFamily = family,
            FontWeight = weight,
            FontSize = Math.Max(12, _editorFontSizeSlider?.Value ?? 18),
            Foreground = new SolidColorBrush(ResolveEditorPaintColor()),
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };
        InkCanvas.SetLeft(overlay, position.X);
        InkCanvas.SetTop(overlay, position.Y);
        _editorInkCanvas.Children.Add(overlay);

        _editorPendingText = null;
        CaptureEditorMarkupSnapshot();
        SetEditorMarkupTool(EditorMarkupTool.Paint);
        SetEditorStatus("Text label added. Undo can remove it if needed.");
        e.Handled = true;
    }

    private void InitializeEditorMarkupHistory()
    {
        _editorMarkupHistory.Clear();
        _editorMarkupHistory.Add(CreateEditorMarkupSnapshot());
        _editorMarkupHistoryIndex = 0;
        UpdateEditorHistoryButtons();
    }

    private void CaptureEditorMarkupSnapshot()
    {
        if (_editorRestoringMarkup || _editorInkCanvas is null) return;

        if (_editorMarkupHistoryIndex < _editorMarkupHistory.Count - 1)
            _editorMarkupHistory.RemoveRange(_editorMarkupHistoryIndex + 1, _editorMarkupHistory.Count - _editorMarkupHistoryIndex - 1);

        _editorMarkupHistory.Add(CreateEditorMarkupSnapshot());
        if (_editorMarkupHistory.Count > 40)
            _editorMarkupHistory.RemoveAt(0);
        _editorMarkupHistoryIndex = _editorMarkupHistory.Count - 1;
        UpdateEditorHistoryButtons();
    }

    private EditorMarkupSnapshot CreateEditorMarkupSnapshot()
    {
        if (_editorInkCanvas is null)
            return new EditorMarkupSnapshot(new StrokeCollection(), Array.Empty<EditorTextOverlaySnapshot>());

        StrokeCollection strokes = CloneEditorStrokes(_editorInkCanvas.Strokes);
        var text = new List<EditorTextOverlaySnapshot>();
        foreach (TextBlock overlay in _editorInkCanvas.Children.OfType<TextBlock>())
        {
            double left = InkCanvas.GetLeft(overlay);
            double top = InkCanvas.GetTop(overlay);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            Color color = overlay.Foreground is SolidColorBrush brush ? brush.Color : Colors.White;
            text.Add(new EditorTextOverlaySnapshot(
                overlay.Text,
                left,
                top,
                overlay.FontFamily.Source,
                overlay.FontSize,
                overlay.FontWeight,
                color));
        }
        return new EditorMarkupSnapshot(strokes, text);
    }

    private static StrokeCollection CloneEditorStrokes(StrokeCollection source)
    {
        var clone = new StrokeCollection();
        foreach (Stroke stroke in source)
            clone.Add(stroke.Clone());
        return clone;
    }

    private void EditorUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_editorMarkupHistoryIndex <= 0) return;
        _editorMarkupHistoryIndex--;
        RestoreEditorMarkupSnapshot(_editorMarkupHistory[_editorMarkupHistoryIndex]);
        SetEditorStatus("Markup change undone.");
    }

    private void EditorRedo_Click(object sender, RoutedEventArgs e)
    {
        if (_editorMarkupHistoryIndex < 0 || _editorMarkupHistoryIndex >= _editorMarkupHistory.Count - 1) return;
        _editorMarkupHistoryIndex++;
        RestoreEditorMarkupSnapshot(_editorMarkupHistory[_editorMarkupHistoryIndex]);
        SetEditorStatus("Markup change restored.");
    }

    private void RestoreEditorMarkupSnapshot(EditorMarkupSnapshot snapshot)
    {
        if (_editorInkCanvas is null) return;
        _editorRestoringMarkup = true;
        try
        {
            _editorInkCanvas.Strokes.Clear();
            foreach (Stroke stroke in snapshot.Strokes)
                _editorInkCanvas.Strokes.Add(stroke.Clone());

            _editorInkCanvas.Children.Clear();
            foreach (EditorTextOverlaySnapshot saved in snapshot.TextOverlays)
            {
                var overlay = new TextBlock
                {
                    Text = saved.Text,
                    FontFamily = new FontFamily(saved.FontFamily),
                    FontWeight = saved.FontWeight,
                    FontSize = saved.FontSize,
                    Foreground = new SolidColorBrush(saved.Color),
                    Background = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                InkCanvas.SetLeft(overlay, saved.Left);
                InkCanvas.SetTop(overlay, saved.Top);
                _editorInkCanvas.Children.Add(overlay);
            }
        }
        finally
        {
            _editorRestoringMarkup = false;
            UpdateEditorHistoryButtons();
        }
    }

    private void EditorClearMarkup_Click(object sender, RoutedEventArgs e)
    {
        ClearEditorMarkup(resetHistory: false);
        SetEditorStatus("Markup cleared.");
    }

    private void ClearEditorMarkup(bool resetHistory)
    {
        if (_editorInkCanvas is null) return;
        _editorRestoringMarkup = true;
        try
        {
            _editorPendingText = null;
            _editorInkCanvas.Strokes.Clear();
            _editorInkCanvas.Children.Clear();
        }
        finally
        {
            _editorRestoringMarkup = false;
        }

        if (resetHistory)
            InitializeEditorMarkupHistory();
        else
            CaptureEditorMarkupSnapshot();

        SetEditorMarkupTool(EditorMarkupTool.Paint);
    }

    private void UpdateEditorHistoryButtons()
    {
        if (_editorUndoButton is not null) _editorUndoButton.IsEnabled = _editorMarkupHistoryIndex > 0;
        if (_editorRedoButton is not null) _editorRedoButton.IsEnabled =
            _editorMarkupHistoryIndex >= 0 && _editorMarkupHistoryIndex < _editorMarkupHistory.Count - 1;
    }
}
