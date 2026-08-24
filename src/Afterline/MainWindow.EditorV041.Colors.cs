using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private void RefreshEditorLineColorList(IReadOnlyList<EditorChatLine> lines)
    {
        if (_editorLineColorList is null) return;
        int? selectedSourceIndex = (_editorLineColorList.SelectedItem as EditorLineChoice)?.SourceIndex;
        _editorUpdatingLineColorUi = true;
        try
        {
            _editorLineColorList.Items.Clear();
            foreach (EditorChatLine line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.PlainText)) continue;
                _editorLineColorList.Items.Add(new EditorLineChoice(line.SourceIndex, line.PlainText, line.AutoStyle));
            }

            if (selectedSourceIndex.HasValue)
            {
                EditorLineChoice? selected = _editorLineColorList.Items.Cast<EditorLineChoice>()
                    .FirstOrDefault(item => item.SourceIndex == selectedSourceIndex.Value);
                if (selected is not null) _editorLineColorList.SelectedItem = selected;
            }
        }
        finally
        {
            _editorUpdatingLineColorUi = false;
        }
        UpdateEditorLineColorControls();
    }

    private void EditorLineColorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editorUpdatingLineColorUi) return;
        if (_editorLineColorList?.SelectedItem is EditorLineChoice &&
            _editorInput is { SelectionLength: > 0 })
        {
            // Choosing a row means the next preset/custom color targets that
            // entire row, not a text selection left behind in the input box.
            int caret = _editorInput.SelectionStart + _editorInput.SelectionLength;
            _editorInput.Select(Math.Min(caret, _editorInput.Text.Length), 0);
        }
        UpdateEditorLineColorControls();
    }

    private void UpdateEditorLineColorControls()
    {
        if (_editorInput is { SelectionLength: > 0 } && _editorLineColorPresetBox is not null)
        {
            _editorUpdatingLineColorUi = true;
            try
            {
                Color? selectedColor = GetUniformSelectedTextColorV071();
                EditorColorPreset? preset = selectedColor is Color color
                    ? EditorChatFormatter.FindPreset(color)
                    : null;
                EditorPresetChoice? choice = _editorLineColorPresetBox.Items.Cast<EditorPresetChoice>()
                    .FirstOrDefault(item => item.Key == preset?.Key);
                _editorLineColorPresetBox.SelectedItem = choice ?? _editorLineColorPresetBox.Items[0];
                if (_editorLineColorHint is not null)
                    _editorLineColorHint.Text = selectedColor is null
                        ? $"Selected text · {_editorInput.SelectionLength:N0} characters · automatic/mixed color"
                        : $"Selected text · manual {preset?.Name ?? "custom color"}";
            }
            finally
            {
                _editorUpdatingLineColorUi = false;
            }
            return;
        }

        if (_editorLineColorList?.SelectedItem is not EditorLineChoice selected || _editorLineColorPresetBox is null)
        {
            if (_editorLineColorHint is not null) _editorLineColorHint.Text = "Select a line to see its detected style.";
            return;
        }

        _editorUpdatingLineColorUi = true;
        try
        {
            if (_editorLineColorOverrides.TryGetValue(selected.SourceIndex, out Color color))
            {
                EditorColorPreset? preset = EditorChatFormatter.FindPreset(color);
                EditorPresetChoice? choice = _editorLineColorPresetBox.Items.Cast<EditorPresetChoice>()
                    .FirstOrDefault(item => item.Key == preset?.Key);
                _editorLineColorPresetBox.SelectedItem = choice ?? _editorLineColorPresetBox.Items[0];
                if (_editorLineColorHint is not null)
                    _editorLineColorHint.Text = $"Manual override · {preset?.Name ?? "Custom color"}";
            }
            else
            {
                _editorLineColorPresetBox.SelectedIndex = 0;
                if (_editorLineColorHint is not null)
                    _editorLineColorHint.Text = $"Detected automatically · {selected.AutoStyle}";
            }
        }
        finally
        {
            _editorUpdatingLineColorUi = false;
        }
    }

    private void EditorLineColorPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editorUpdatingLineColorUi ||
            _editorLineColorPresetBox?.SelectedItem is not EditorPresetChoice preset)
            return;

        if (_editorInput is { SelectionLength: > 0 })
        {
            ApplySelectedTextColorV071(preset.Color);
            return;
        }

        if (_editorLineColorList?.SelectedItem is not EditorLineChoice selected)
            return;

        if (preset.Color.HasValue)
            _editorLineColorOverrides[selected.SourceIndex] = preset.Color.Value;
        else
            _editorLineColorOverrides.Remove(selected.SourceIndex);

        ScheduleEditorChatRender();
        UpdateEditorLineColorControls();
    }

    private void EditorUseAutoLineColor_Click(object sender, RoutedEventArgs e)
    {
        if (_editorInput is { SelectionLength: > 0 })
        {
            ApplySelectedTextColorV071(null);
            return;
        }
        if (_editorLineColorList?.SelectedItem is not EditorLineChoice selected) return;
        _editorLineColorOverrides.Remove(selected.SourceIndex);
        ScheduleEditorChatRender();
        UpdateEditorLineColorControls();
    }

    private void EditorResetAllLineColors_Click(object sender, RoutedEventArgs e)
    {
        if (_editorLineColorOverrides.Count == 0 && _editorTextColorOverridesV071.Count == 0) return;
        _editorLineColorOverrides.Clear();
        _editorTextColorOverridesV071.Clear();
        ScheduleEditorChatRender();
        UpdateEditorLineColorControls();
        SetEditorStatus("All manual line and selected-text colors were returned to Automatic.");
    }

    private void PruneEditorLineColorOverrides()
    {
        if (_editorInput is null) return;
        int count = _editorInput.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Length;
        int[] stale = _editorLineColorOverrides.Keys.Where(index => index < 0 || index >= count).ToArray();
        foreach (int index in stale) _editorLineColorOverrides.Remove(index);

        string[] lines = NormalizeEditorTextV071(_editorInput.Text).Split('\n');
        _editorTextColorOverridesV071.RemoveAll(value =>
            value.SourceIndex < 0 ||
            value.SourceIndex >= lines.Length ||
            value.Start < 0 ||
            value.Length <= 0 ||
            value.End > lines[value.SourceIndex].Length ||
            !string.Equals(
                lines[value.SourceIndex].Substring(value.Start, value.Length),
                value.Text,
                StringComparison.Ordinal));
    }

    private void EditorChooseCustomTextColorV071(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.White
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        Color color = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
        if (_editorInput is { SelectionLength: > 0 })
        {
            ApplySelectedTextColorV071(color);
            return;
        }

        if (_editorLineColorList?.SelectedItem is not EditorLineChoice selected)
        {
            SetEditorStatus("Select text in Chat & Font or select a line before choosing a custom color.");
            return;
        }

        _editorLineColorOverrides[selected.SourceIndex] = color;
        ScheduleEditorChatRender();
        UpdateEditorLineColorControls();
        SetEditorStatus("Applied a custom color to the selected line.");
    }

    private void ApplySelectedTextColorV071(Color? color)
    {
        if (_editorInput is null || _editorInput.SelectionLength <= 0) return;
        int selectionStart = _editorInput.SelectionStart;
        int selectionLength = _editorInput.SelectionLength;
        IReadOnlyList<EditorTextColorOverride> selected = GetSelectedTextRangesV071(color ?? EditorChatFormatter.White);
        if (selected.Count == 0) return;

        foreach (EditorTextColorOverride range in selected)
        {
            RemoveOverlappingTextColorsV071(range.SourceIndex, range.Start, range.End);
            if (color is Color selectedColor)
                _editorTextColorOverridesV071.Add(range with { Color = selectedColor });
        }

        _editorTextColorOverridesV071.Sort((left, right) =>
        {
            int line = left.SourceIndex.CompareTo(right.SourceIndex);
            return line != 0 ? line : left.Start.CompareTo(right.Start);
        });
        ScheduleEditorChatRender();
        _editorInput.SelectionStart = selectionStart;
        _editorInput.SelectionLength = selectionLength;
        UpdateEditorLineColorControls();
        SetEditorStatus(color is null
            ? "Returned the selected text to automatic coloring."
            : "Applied a manual color to the selected text.");
    }

    private IReadOnlyList<EditorTextColorOverride> GetSelectedTextRangesV071(Color color)
    {
        if (_editorInput is null || _editorInput.SelectionLength <= 0)
            return Array.Empty<EditorTextColorOverride>();

        string original = _editorInput.Text;
        int originalStart = Math.Clamp(_editorInput.SelectionStart, 0, original.Length);
        int originalEnd = Math.Clamp(originalStart + _editorInput.SelectionLength, originalStart, original.Length);
        string normalized = NormalizeEditorTextV071(original);
        int normalizedStart = NormalizeEditorTextV071(original[..originalStart]).Length;
        int normalizedEnd = NormalizeEditorTextV071(original[..originalEnd]).Length;
        string[] lines = normalized.Split('\n');

        var result = new List<EditorTextColorOverride>();
        int lineStart = 0;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            int lineEnd = lineStart + line.Length;
            int start = Math.Max(normalizedStart, lineStart);
            int end = Math.Min(normalizedEnd, lineEnd);
            if (end > start)
            {
                int localStart = start - lineStart;
                int length = end - start;
                result.Add(new EditorTextColorOverride(
                    index,
                    localStart,
                    length,
                    line.Substring(localStart, length),
                    color));
            }
            lineStart = lineEnd + 1;
        }
        return result;
    }

    private void RemoveOverlappingTextColorsV071(int sourceIndex, int start, int end)
    {
        string[] lines = _editorInput is null
            ? Array.Empty<string>()
            : NormalizeEditorTextV071(_editorInput.Text).Split('\n');
        var replacements = new List<EditorTextColorOverride>();
        for (int index = _editorTextColorOverridesV071.Count - 1; index >= 0; index--)
        {
            EditorTextColorOverride existing = _editorTextColorOverridesV071[index];
            if (existing.SourceIndex != sourceIndex || existing.End <= start || existing.Start >= end)
                continue;

            _editorTextColorOverridesV071.RemoveAt(index);
            if (sourceIndex < 0 || sourceIndex >= lines.Length) continue;
            string line = lines[sourceIndex];
            if (existing.Start < start)
            {
                int length = start - existing.Start;
                replacements.Add(existing with
                {
                    Length = length,
                    Text = line.Substring(existing.Start, length)
                });
            }
            if (existing.End > end)
            {
                int length = existing.End - end;
                replacements.Add(existing with
                {
                    Start = end,
                    Length = length,
                    Text = line.Substring(end, length)
                });
            }
        }
        _editorTextColorOverridesV071.AddRange(replacements);
    }

    private Color? GetUniformSelectedTextColorV071()
    {
        IReadOnlyList<EditorTextColorOverride> ranges = GetSelectedTextRangesV071(EditorChatFormatter.White);
        if (ranges.Count == 0) return null;
        Color? result = null;
        foreach (EditorTextColorOverride selected in ranges)
        {
            EditorTextColorOverride? exact = _editorTextColorOverridesV071.LastOrDefault(value =>
                value.SourceIndex == selected.SourceIndex &&
                value.Start <= selected.Start &&
                value.End >= selected.End);
            if (exact is null) return null;
            if (result.HasValue && result.Value != exact.Color) return null;
            result ??= exact.Color;
        }
        return result;
    }

    private static string NormalizeEditorTextV071(string text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    private void ConfigureEditorLineColorContextMenu()
    {
        if (_editorLineColorList is null) return;
        var menu = CreateAfterlineContextMenu();
        menu.Items.Add(CreateAfterlineContextMenuItem("Use automatic color", EditorUseAutoLineColor_Click));
        menu.Items.Add(CreateAfterlineContextMenuSeparator());
        menu.Items.Add(CreateAfterlineContextMenuItem("Reset all manual colors", EditorResetAllLineColors_Click));
        _editorLineColorList.ContextMenu = menu;
    }

    private void EditorTextEffectChanged(object sender, RoutedEventArgs e)
        => ScheduleEditorChatRender();

    private void EditorResetTextEffects_Click(object sender, RoutedEventArgs e)
    {
        if (_editorStrokeEnabledCheck is not null) _editorStrokeEnabledCheck.IsChecked = false;
        if (_editorStrokeWidthSlider is not null) _editorStrokeWidthSlider.Value = 1;
        if (_editorStrokeColorBox is not null) _editorStrokeColorBox.SelectedItem = "Black";
        if (_editorShadowEnabledCheck is not null) _editorShadowEnabledCheck.IsChecked = false;
        if (_editorShadowOpacitySlider is not null) _editorShadowOpacitySlider.Value = 75;
        if (_editorShadowBlurSlider is not null) _editorShadowBlurSlider.Value = 5;
        if (_editorShadowOffsetXSlider is not null) _editorShadowOffsetXSlider.Value = 2;
        if (_editorShadowOffsetYSlider is not null) _editorShadowOffsetYSlider.Value = 2;
        if (_editorShadowColorBox is not null) _editorShadowColorBox.SelectedItem = "Black";
        ScheduleEditorChatRender();
        SetEditorStatus("Text effects were reset.");
    }

    private void EditorResetImageTone_Click(object sender, RoutedEventArgs e)
    {
        ResetEditorAdjustmentSliders();
        _editorBaseAdjustTimer?.Stop();
        ApplyEditorImageAdjustments();
        SetEditorStatus("Image tone adjustments were reset.");
    }

    private static void DetachEditorElement(FrameworkElement element)
    {
        DependencyObject? parent = LogicalTreeHelper.GetParent(element);
        if (parent is null)
        {
            try { parent = VisualTreeHelper.GetParent(element); }
            catch { parent = null; }
        }

        switch (parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                contentControl.Content = null;
                break;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, element):
                presenter.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
        }
    }
}
