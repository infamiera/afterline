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
        UpdateEditorLineColorControls();
    }

    private void UpdateEditorLineColorControls()
    {
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
        if (_editorUpdatingLineColorUi || _editorLineColorList?.SelectedItem is not EditorLineChoice selected ||
            _editorLineColorPresetBox?.SelectedItem is not EditorPresetChoice preset)
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
        if (_editorLineColorList?.SelectedItem is not EditorLineChoice selected) return;
        _editorLineColorOverrides.Remove(selected.SourceIndex);
        ScheduleEditorChatRender();
        UpdateEditorLineColorControls();
    }

    private void EditorResetAllLineColors_Click(object sender, RoutedEventArgs e)
    {
        if (_editorLineColorOverrides.Count == 0) return;
        _editorLineColorOverrides.Clear();
        ScheduleEditorChatRender();
        UpdateEditorLineColorControls();
        SetEditorStatus("All manual line colors were returned to Automatic.");
    }

    private void PruneEditorLineColorOverrides()
    {
        if (_editorInput is null || _editorLineColorOverrides.Count == 0) return;
        int count = _editorInput.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Length;
        int[] stale = _editorLineColorOverrides.Keys.Where(index => index < 0 || index >= count).ToArray();
        foreach (int index in stale) _editorLineColorOverrides.Remove(index);
    }

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
