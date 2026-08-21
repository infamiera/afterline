using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private bool _changelogUiInitialized;
    private Button? _changelogButton;

    private void EnsureChangelogUi()
    {
        if (_changelogUiInitialized) return;
        _changelogUiInitialized = true;

        EnsureNotificationAndUpdateUi();
        if (_checkUpdatesButton?.Parent is not StackPanel updatePanel) return;

        _changelogButton = new Button
        {
            Content = "Changelog",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(9, 6, 9, 6),
            Margin = new Thickness(0, 8, 0, 0),
            ToolTip = "View patch notes for the current and previous Afterline releases."
        };
        _changelogButton.Click += (_, _) =>
        {
            var window = new ChangelogWindow(this);
            window.ShowDialog();
        };

        int updateIndex = updatePanel.Children.IndexOf(_checkUpdatesButton);
        updatePanel.Children.Insert(Math.Min(updateIndex + 1, updatePanel.Children.Count), _changelogButton);
    }
}
