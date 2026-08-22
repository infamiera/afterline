using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private bool _liveFindLayoutV062Initialized;

    private void EnsureLiveFindLayoutV062()
    {
        if (_liveFindLayoutV062Initialized || _liveFindBoxV050?.Parent is not WrapPanel row)
            return;

        _liveFindLayoutV062Initialized = true;
        _liveFindBoxV050.Width = 150;
        _liveFindBoxV050.MinWidth = 120;

        Button? clear = row.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Clear", StringComparison.Ordinal));
        Button? copy = row.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Copy selected", StringComparison.Ordinal));

        if (clear is null || copy is null) return;

        int insertAt = Math.Min(row.Children.IndexOf(clear), row.Children.IndexOf(copy));
        row.Children.Remove(clear);
        row.Children.Remove(copy);

        clear.Margin = new Thickness(7, 0, 0, 0);
        copy.Margin = new Thickness(7, 0, 0, 0);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(clear);
        actions.Children.Add(copy);
        row.Children.Insert(Math.Max(0, insertAt), actions);
    }
}
