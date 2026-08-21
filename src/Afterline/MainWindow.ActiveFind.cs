using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private TextBox? _liveFindBoxV050;
    private TextBox? _logReaderFindBoxV050;
    private TextBlock? _liveFindStatusV050;
    private TextBlock? _logReaderFindStatusV050;
    private int _liveFindMatchIndexV051 = -1;
    private int _logReaderFindMatchIndexV051 = -1;
    private ListBoxItem? _liveFindHighlightedContainerV051;
    private ListBoxItem? _logReaderFindHighlightedContainerV051;

    private void ConfigureActiveChatSearchV050()
    {
        if (_liveActionStatus?.Parent is WrapPanel liveActions && liveActions.Parent is StackPanel leftPanel)
        {
            FrameworkElement find = BuildFindRowV050(
                "Find in live chat",
                out _liveFindBoxV050,
                out _liveFindStatusV050,
                (_, _) => _liveFindBoxV050?.Clear(),
                (_, _) => CopySelectedLiveLinesV050());
            leftPanel.Children.Add(find);

            _liveFindBoxV050.TextChanged += (_, _) =>
            {
                _liveFindMatchIndexV051 = -1;
                ClearFindHighlightV051(live: true);
                UpdateLiveFindStatusV051();
            };
            _liveFindBoxV050.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                FindNextLiveMatchV051((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
            };
        }

        if (_logReaderStatusText?.Parent is Grid header)
        {
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FrameworkElement find = BuildFindRowV050(
                "Find in loaded log",
                out _logReaderFindBoxV050,
                out _logReaderFindStatusV050,
                (_, _) => _logReaderFindBoxV050?.Clear(),
                (_, _) => CopySelectedLogLinesV050());
            find.Margin = new Thickness(0, 9, 0, 0);
            Grid.SetRow(find, header.RowDefinitions.Count - 1);
            Grid.SetColumnSpan(find, 2);
            header.Children.Add(find);

            _logReaderFindBoxV050.TextChanged += (_, _) =>
            {
                _logReaderFindMatchIndexV051 = -1;
                ClearFindHighlightV051(live: false);
                UpdateLogReaderFindStatusV051();
            };
            _logReaderFindBoxV050.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                FindNextLogReaderMatchV051((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
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
            ToolTip = "Type a keyword and press Enter to jump to the next matching line. Shift+Enter searches backwards."
        };
        box.SetResourceReference(Control.BackgroundProperty, "Raised");
        box.SetResourceReference(Control.ForegroundProperty, "Text");
        box.SetResourceReference(Control.BorderBrushProperty, "Border");
        row.Children.Add(box);

        var clear = new Button
        {
            Content = "Clear",
            Padding = new Thickness(9, 5, 9, 5),
            Margin = new Thickness(7, 0, 0, 0),
            MinHeight = 32,
            ToolTip = "Clear the current find text."
        };
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

    private void UpdateLiveFindStatusV051()
    {
        if (_liveFindStatusV050 is null || _liveFindBoxV050 is null) return;
        string query = _liveFindBoxV050.Text.Trim();
        if (query.Length == 0)
        {
            _liveFindStatusV050.Text = string.Empty;
            return;
        }

        int count = LiveChatList.Items.OfType<ChatEntry>().Count(entry => LiveFindMatchesV051(entry, query));
        _liveFindStatusV050.Text = count == 0
            ? "No matches"
            : $"{count:N0} match{(count == 1 ? string.Empty : "es")} · Enter: next";
    }

    private void UpdateLogReaderFindStatusV051()
    {
        if (_logReaderFindStatusV050 is null || _logReaderFindBoxV050 is null || _logReaderList is null) return;
        string query = _logReaderFindBoxV050.Text.Trim();
        if (query.Length == 0)
        {
            _logReaderFindStatusV050.Text = string.Empty;
            return;
        }

        int count = _logReaderList.Items.OfType<LogReaderLineItem>().Count(line => LogReaderFindMatchesV051(line, query));
        _logReaderFindStatusV050.Text = count == 0
            ? "No matches"
            : $"{count:N0} match{(count == 1 ? string.Empty : "es")} · Enter: next";
    }

    private void FindNextLiveMatchV051(bool reverse)
    {
        if (_liveFindBoxV050 is null || _liveFindStatusV050 is null) return;
        string query = _liveFindBoxV050.Text.Trim();
        if (query.Length == 0) return;

        List<ChatEntry> matches = LiveChatList.Items
            .OfType<ChatEntry>()
            .Where(entry => LiveFindMatchesV051(entry, query))
            .ToList();

        if (matches.Count == 0)
        {
            _liveFindMatchIndexV051 = -1;
            _liveFindStatusV050.Text = "No matches";
            return;
        }

        _liveFindMatchIndexV051 = AdvanceFindIndexV051(_liveFindMatchIndexV051, matches.Count, reverse);
        ChatEntry target = matches[_liveFindMatchIndexV051];
        _liveFindStatusV050.Text = $"{_liveFindMatchIndexV051 + 1:N0} of {matches.Count:N0}";
        _ = FlashFindMatchV051(LiveChatList, target, live: true);
    }

    private void FindNextLogReaderMatchV051(bool reverse)
    {
        if (_logReaderFindBoxV050 is null || _logReaderFindStatusV050 is null || _logReaderList is null) return;
        string query = _logReaderFindBoxV050.Text.Trim();
        if (query.Length == 0) return;

        List<LogReaderLineItem> matches = _logReaderList.Items
            .OfType<LogReaderLineItem>()
            .Where(line => LogReaderFindMatchesV051(line, query))
            .ToList();

        if (matches.Count == 0)
        {
            _logReaderFindMatchIndexV051 = -1;
            _logReaderFindStatusV050.Text = "No matches";
            return;
        }

        _logReaderFindMatchIndexV051 = AdvanceFindIndexV051(_logReaderFindMatchIndexV051, matches.Count, reverse);
        LogReaderLineItem target = matches[_logReaderFindMatchIndexV051];
        _logReaderFindStatusV050.Text = $"{_logReaderFindMatchIndexV051 + 1:N0} of {matches.Count:N0}";
        _ = FlashFindMatchV051(_logReaderList, target, live: false);
    }

    private static int AdvanceFindIndexV051(int current, int count, bool reverse)
    {
        if (count <= 0) return -1;
        if (reverse)
            return current <= 0 ? count - 1 : current - 1;
        return current < 0 || current >= count - 1 ? 0 : current + 1;
    }

    private static bool LiveFindMatchesV051(ChatEntry entry, string query)
        => entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           entry.Display.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool LogReaderFindMatchesV051(LogReaderLineItem line, string query)
        => line.RawLine.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           line.Display.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task FlashFindMatchV051(ListBox list, object item, bool live)
    {
        ClearFindHighlightV051(live);
        list.ScrollIntoView(item);
        await Dispatcher.InvokeAsync(() => list.UpdateLayout(), DispatcherPriority.Loaded);

        if (list.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container)
            return;

        container.SetResourceReference(Control.BackgroundProperty, "Accent");
        if (live)
            _liveFindHighlightedContainerV051 = container;
        else
            _logReaderFindHighlightedContainerV051 = container;

        await Task.Delay(TimeSpan.FromSeconds(1));

        ListBoxItem? current = live ? _liveFindHighlightedContainerV051 : _logReaderFindHighlightedContainerV051;
        if (!ReferenceEquals(current, container)) return;

        container.ClearValue(Control.BackgroundProperty);
        if (live)
            _liveFindHighlightedContainerV051 = null;
        else
            _logReaderFindHighlightedContainerV051 = null;
    }

    private void ClearFindHighlightV051(bool live)
    {
        ListBoxItem? container = live ? _liveFindHighlightedContainerV051 : _logReaderFindHighlightedContainerV051;
        if (container is not null)
            container.ClearValue(Control.BackgroundProperty);

        if (live)
            _liveFindHighlightedContainerV051 = null;
        else
            _logReaderFindHighlightedContainerV051 = null;
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
