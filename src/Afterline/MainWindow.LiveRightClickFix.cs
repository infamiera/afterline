using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Afterline.Models;

namespace Afterline;

public partial class MainWindow
{
    private bool _liveRightClickFixInitializedV053;
    private ChatEntry? _liveRightClickTargetV053;
    private ListBoxItem? _liveRightClickHighlightV053;
    private bool _liveRightClickHighlightAppliedV053;

    private void EnsureLiveRightClickFixV053()
    {
        if (_liveRightClickFixInitializedV053) return;
        _liveRightClickFixInitializedV053 = true;

        LiveChatList.PreviewMouseRightButtonDown += LiveChatList_PreviewMouseRightButtonDownV053;
        LiveChatList.PreviewMouseLeftButtonDown += (_, _) => ClearLiveRightClickStateV053();
        LiveChatList.ContextMenuOpening += LiveChatList_ContextMenuOpeningV053;

        if (LiveChatList.ContextMenu is not null)
            LiveChatList.ContextMenu.Closed += (_, _) => ClearLiveRightClickStateV053();
    }

    private void LiveChatList_PreviewMouseRightButtonDownV053(object sender, MouseButtonEventArgs e)
    {
        ClearLiveRightClickStateV053();

        if (e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(LiveChatList, source) is not ListBoxItem item) return;
        if (item.DataContext is not ChatEntry entry) return;

        _liveRightClickTargetV053 = entry;

        // Explicitly make the line under the pointer the current item. Extended
        // selection can otherwise leave a different line as SelectedItem, causing
        // context actions to operate on the wrong chat entry.
        LiveChatList.SelectedItem = entry;
        item.IsSelected = true;
        item.Focus();

        _liveRightClickHighlightV053 = item;
        if (item.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
        {
            item.SetResourceReference(Control.BackgroundProperty, "AfterlineControlHover");
            _liveRightClickHighlightAppliedV053 = true;
        }
    }

    private void LiveChatList_ContextMenuOpeningV053(object sender, ContextMenuEventArgs e)
    {
        // Do not let a right-click on empty Live Chat space reuse an older selection.
        if (_liveRightClickTargetV053 is null)
            e.Handled = true;
    }

    private void ClearLiveRightClickStateV053()
    {
        if (_liveRightClickHighlightAppliedV053 && _liveRightClickHighlightV053 is not null)
            _liveRightClickHighlightV053.ClearValue(Control.BackgroundProperty);

        _liveRightClickHighlightAppliedV053 = false;
        _liveRightClickHighlightV053 = null;
        _liveRightClickTargetV053 = null;
    }
}
