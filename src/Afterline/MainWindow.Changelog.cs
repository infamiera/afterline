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
            Content = "Notes",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(7, 5, 7, 5),
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "View patch notes for the current and previous Afterline releases."
        };
        _changelogButton.Click += (_, _) =>
        {
            var window = new ChangelogWindow(this);
            window.ShowDialog();
        };

        int updateIndex = updatePanel.Children.IndexOf(_checkUpdatesButton);
        updatePanel.Children.Remove(_checkUpdatesButton);
        _checkUpdatesButton.Margin = new Thickness(0);

        var actions = new Grid { Margin = new Thickness(0, 7, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.Children.Add(_checkUpdatesButton);
        Grid.SetColumn(_changelogButton, 1);
        actions.Children.Add(_changelogButton);
        updatePanel.Children.Insert(Math.Min(updateIndex, updatePanel.Children.Count), actions);
    }
}
