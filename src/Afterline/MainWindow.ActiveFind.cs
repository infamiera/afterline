using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private TextBox? _liveFindBoxV050;
    private TextBox? _logReaderFindBoxV050;
    private TextBlock? _liveFindStatusV050;
    private TextBlock? _logReaderFindStatusV050;

    private void ConfigureActiveChatSearchV050()
    {
        LiveChatList.Items.Filter = LiveFilterV050;

        if (_liveActionStatus?.Parent is WrapPanel liveActions && liveActions.Parent is StackPanel leftPanel)
        {
            FrameworkElement find = BuildFindRowV050("Find in live chat", out _liveFindBoxV050, out _liveFindStatusV050,
                (_, _) => _liveFindBoxV050?.Clear(), (_, _) => CopySelectedLiveLinesV050());
            leftPanel.Children.Add(find);
            _liveFindBoxV050.TextChanged += (_, _) =>
            {
                LiveChatList.Items.Refresh();
                UpdateVisibleLiveCount();
                UpdateFindStatusV050(_liveFindStatusV050, _liveFindBoxV050, LiveChatList.Items.Count);
            };
        }

        if (_logReaderView is not null) _logReaderView.Filter = LogReaderFilterV050;
        if (_logReaderStatusText?.Parent is Grid header)
        {
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FrameworkElement find = BuildFindRowV050("Find in loaded log", out _logReaderFindBoxV050, out _logReaderFindStatusV050,
                (_, _) => _logReaderFindBoxV050?.Clear(), (_, _) => CopySelectedLogLinesV050());
            find.Margin = new Thickness(0, 9, 0, 0);
            Grid.SetRow(find, header.RowDefinitions.Count - 1);
            Grid.SetColumnSpan(find, 2);
            header.Children.Add(find);
            _logReaderFindBoxV050.TextChanged += (_, _) =>
            {
                _logReaderView?.Refresh();
                UpdateLogReaderStatus();
                UpdateFindStatusV050(_logReaderFindStatusV050, _logReaderFindBoxV050, _logReaderList?.Items.Count ?? 0);
            };
        }
    }

    private FrameworkElement BuildFindRowV050(
        string label,
        out TextBox box,
        out TextBlock status,
        RoutedEventHandler clearHandler,
        RoutedEventHandler copyHandler)
    {
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        box = new TextBox
        {
            Width = 230,
            Height = 32,
            Padding = new Thickness(9, 5, 9, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Filter the messages currently loaded on this page."
        };
        box.SetResourceReference(Control.BackgroundProperty, "Raised");
        box.SetResourceReference(Control.ForegroundProperty, "Text");
        box.SetResourceReference(Control.BorderBrushProperty, "Border");
        row.Children.Add(box);

        var clear = new Button { Content = "Clear", Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(7, 0, 0, 0), MinHeight = 32 };
        clear.Click += clearHandler;
        row.Children.Add(clear);

        var copy = new Button
        {
            Content = "Copy selected",
            Padding = new Thickness(9, 5, 9, 5),
            Margin = new Thickness(7, 0, 0, 0),
            MinHeight = 32,
            ToolTip = "Copy every selected line in display order. Ctrl/Shift-click can select multiple lines."
        };
        copy.Click += copyHandler;
        row.Children.Add(copy);

        status = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        row.Children.Add(status);
        return row;
    }

    private bool LiveFilterV050(object item)
    {
        if (item is not ChatEntry entry) return true;
        if (!_settings.ShowOocChat && entry.IsOocLine) return false;
        string query = _liveFindBoxV050?.Text.Trim() ?? string.Empty;
        return query.Length == 0 || entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.Display.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool LogReaderFilterV050(object item)
    {
        if (item is not LogReaderLineItem line) return true;
        if (!_settings.ShowOocChat && line.IsOocLine) return false;
        string query = _logReaderFindBoxV050?.Text.Trim() ?? string.Empty;
        return query.Length == 0 || line.RawLine.Contains(query, StringComparison.OrdinalIgnoreCase) || line.Display.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static void UpdateFindStatusV050(TextBlock? status, TextBox? box, int count)
    {
        if (status is null) return;
        status.Text = string.IsNullOrWhiteSpace(box?.Text) ? string.Empty : $"{count:N0} match{(count == 1 ? string.Empty : "es")}";
    }

    private void CopySelectedLiveLinesV050()
    {
        if (LiveChatList.SelectedItems.Count == 0) return;
        var selected = new HashSet<ChatEntry>(LiveChatList.SelectedItems.OfType<ChatEntry>());
        string text = string.Join(Environment.NewLine, LiveChatList.Items.OfType<ChatEntry>().Where(selected.Contains).Select(entry => entry.Display));
        CopyChatTextV050(text, "Selected Live Chat lines copied.");
    }

    private void CopySelectedLogLinesV050()
    {
        if (_logReaderList is null || _logReaderList.SelectedItems.Count == 0) return;
        string text = string.Join(Environment.NewLine,
            _logReaderList.SelectedItems.OfType<LogReaderLineItem>().OrderBy(item => item.LineNumber).Select(item => item.Display));
        CopyChatTextV050(text, "Selected Log Reader lines copied.");
    }

    private void CopyLiveContextV050(int radius)
    {
        if (LiveChatList.SelectedItems.Count > 1) { CopySelectedLiveLinesV050(); return; }
        if (LiveChatList.SelectedItem is not ChatEntry selected) return;
        List<ChatEntry> visible = LiveChatList.Items.OfType<ChatEntry>().ToList();
        int index = visible.IndexOf(selected);
        if (index < 0) return;
        int start = Math.Max(0, index - radius);
        int end = Math.Min(visible.Count - 1, index + radius);
        CopyChatTextV050(string.Join(Environment.NewLine, visible.Skip(start).Take(end - start + 1).Select(x => x.Display)), "Live Chat context copied.");
    }

    private void CopyChatTextV050(string text, string success)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
            if (_liveActionStatus is not null) _liveActionStatus.Text = success;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to copy selected chat text.", ex);
        }
    }
}
