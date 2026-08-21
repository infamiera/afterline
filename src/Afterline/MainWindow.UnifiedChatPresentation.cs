using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline;

public partial class MainWindow
{
    private bool _unifiedChatPresentationInitialized;

    private void EnsureUnifiedChatPresentation()
    {
        if (_unifiedChatPresentationInitialized) return;
        _unifiedChatPresentationInitialized = true;

        ApplyUnifiedLiveChatTemplate();
        ApplyUnifiedLogReaderTemplate();
        MoveLogReaderIntoLibrarySection();
        ExtendEditorPositionRange();

        if (_roleplayColorsCheck is not null)
        {
            _roleplayColorsCheck.Content = "Automatic chat colors";
            _roleplayColorsCheck.ToolTip = "Use the same automatic RP color recognition as the screenshot Editor. Turn this off to use the simpler legacy line colors.";
            _roleplayColorsCheck.Checked += UnifiedChatColorToggle_Changed;
            _roleplayColorsCheck.Unchecked += UnifiedChatColorToggle_Changed;
        }

        if (_logReaderRpCheck is not null)
        {
            _logReaderRpCheck.Content = "Automatic chat colors";
            _logReaderRpCheck.ToolTip = "Use the same automatic RP color recognition as the screenshot Editor. Turn this off to use the simpler legacy line colors.";
            _logReaderRpCheck.Checked += UnifiedChatColorToggle_Changed;
            _logReaderRpCheck.Unchecked += UnifiedChatColorToggle_Changed;
        }
    }

    private void UnifiedChatColorToggle_Changed(object sender, RoutedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            ApplyUnifiedLiveChatTemplate();
            ApplyUnifiedLogReaderTemplate();
            LiveChatList.Items.Refresh();
            _logReaderView?.Refresh();
            _logReaderList?.Items.Refresh();
        }));
    }

    private void ApplyUnifiedLiveChatTemplate()
    {
        var text = new FrameworkElementFactory(typeof(RoleplayColorTextBlock));
        text.SetBinding(RoleplayColorTextBlock.DisplayTextProperty, new Binding(nameof(ChatEntry.Display)));
        text.SetBinding(RoleplayColorTextBlock.FallbackBrushProperty, new Binding(nameof(ChatEntry.Foreground)));
        text.SetBinding(RoleplayColorTextBlock.IsSystemMessageProperty, new Binding(nameof(ChatEntry.IsSystemMessage)));
        text.SetValue(RoleplayColorTextBlock.UseAutomaticColorsProperty, _settings.ColorizeRoleplayLines);
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        LiveChatList.ItemTemplate = new DataTemplate(typeof(ChatEntry)) { VisualTree = text };
    }

    private void ApplyUnifiedLogReaderTemplate()
    {
        if (_logReaderList is null) return;

        var row = new FrameworkElementFactory(typeof(DockPanel));
        row.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        row.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);

        var number = new FrameworkElementFactory(typeof(TextBlock));
        number.SetBinding(TextBlock.TextProperty, new Binding(nameof(LogReaderLineItem.LineNumber)));
        number.SetValue(FrameworkElement.WidthProperty, 54d);
        number.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
        number.SetValue(TextBlock.ForegroundProperty, FindResource("MutedText"));
        number.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
        number.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
        number.SetValue(DockPanel.DockProperty, Dock.Left);
        row.AppendChild(number);

        var text = new FrameworkElementFactory(typeof(RoleplayColorTextBlock));
        text.SetBinding(RoleplayColorTextBlock.DisplayTextProperty, new Binding(nameof(LogReaderLineItem.Display)));
        text.SetBinding(RoleplayColorTextBlock.FallbackBrushProperty, new Binding(nameof(LogReaderLineItem.Foreground)));
        text.SetBinding(RoleplayColorTextBlock.IsSystemMessageProperty, new Binding("Entry.IsSystemMessage"));
        text.SetValue(RoleplayColorTextBlock.UseAutomaticColorsProperty, _settings.ColorizeRoleplayLines);
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        row.AppendChild(text);

        _logReaderList.ItemTemplate = new DataTemplate(typeof(LogReaderLineItem)) { VisualTree = row };
    }

    private void MoveLogReaderIntoLibrarySection()
    {
        if (_logReaderNavButton is null ||
            _logReaderNavButton.Parent is not StackPanel navigationPanel ||
            !ReferenceEquals(ArchiveNav.Parent, navigationPanel))
            return;

        navigationPanel.Children.Remove(_logReaderNavButton);
        int archiveIndex = navigationPanel.Children.IndexOf(ArchiveNav);
        if (archiveIndex < 0)
        {
            navigationPanel.Children.Add(_logReaderNavButton);
            return;
        }

        navigationPanel.Children.Insert(Math.Min(archiveIndex + 1, navigationPanel.Children.Count), _logReaderNavButton);
    }

    private void ExtendEditorPositionRange()
    {
        if (_editorChatXSlider is not null)
        {
            _editorChatXSlider.Maximum = 1000;
            _editorChatXSlider.ToolTip = "Move the generated chat horizontally from 0 to 1000 pixels.";
        }

        if (_editorChatYSlider is not null)
        {
            _editorChatYSlider.Maximum = 1000;
            _editorChatYSlider.ToolTip = "Move the generated chat vertically from 0 to 1000 pixels.";
        }
    }
}
