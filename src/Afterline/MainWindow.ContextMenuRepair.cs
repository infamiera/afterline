using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _liveContextMenuRepairInitialized;

    private void EnsureLiveChatContextMenuRepair()
    {
        if (_liveContextMenuRepairInitialized) return;
        _liveContextMenuRepairInitialized = true;

        var menu = CreateAfterlineContextMenu();
        menu.Items.Add(CreateAfterlineContextMenuItem("Copy line", CopyLiveChatLine_Click));
        menu.Items.Add(CreateAfterlineContextMenuSeparator());
        menu.Items.Add(CreateAfterlineContextMenuItem("Copy ±5 lines", (_, _) => CopyLiveContext(5)));
        menu.Items.Add(CreateAfterlineContextMenuItem("Copy ±10 lines", (_, _) => CopyLiveContext(10)));
        menu.Items.Add(CreateAfterlineContextMenuSeparator());
        menu.Items.Add(CreateAfterlineContextMenuItem("Bookmark line", BookmarkSelectedLiveLine_Click));
        menu.Items.Add(CreateAfterlineContextMenuItem("Add note to line…", AddNoteToSelectedLiveLine_Click));
        LiveChatList.ContextMenu = menu;
    }

    private ContextMenu CreateAfterlineContextMenu()
        => new()
        {
            Background = (Brush)FindResource("Raised"),
            Foreground = (Brush)FindResource("Text"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

    private MenuItem CreateAfterlineContextMenuItem(string text, RoutedEventHandler handler)
    {
        var item = new MenuItem
        {
            Header = text,
            Style = CreateAfterlineMenuItemStyle()
        };
        item.Click += handler;
        return item;
    }

    private Style CreateAfterlineMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("Text")));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 16, 7)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

        var root = new FrameworkElementFactory(typeof(Border));
        root.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        root.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        root.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(HeaderedItemsControl.Header))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter);

        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = root };
        style.Setters.Add(new Setter(Control.TemplateProperty, template));

        var highlighted = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        highlighted.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x29, 0x3B, 0x50))));
        style.Triggers.Add(highlighted);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        style.Triggers.Add(disabled);
        return style;
    }

    private Separator CreateAfterlineContextMenuSeparator()
    {
        var separator = new Separator();
        var style = new Style(typeof(Separator));

        var line = new FrameworkElementFactory(typeof(Border));
        line.SetValue(FrameworkElement.HeightProperty, 1d);
        line.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 4, 8, 4));
        line.SetValue(Border.BackgroundProperty, FindResource("Border"));

        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Separator)) { VisualTree = line }));
        separator.Style = style;
        return separator;
    }
}
