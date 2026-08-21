using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _settingsButtonRelocated;

    private void EnsureSettingsButtonPlacement()
    {
        if (_settingsButtonRelocated) return;
        if (SettingsNav.Parent is not Panel navigationPanel || navigationPanel.Parent is not Grid sidebarGrid) return;

        _settingsButtonRelocated = true;
        navigationPanel.Children.Remove(SettingsNav);

        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int settingsRow = sidebarGrid.RowDefinitions.Count - 1;

        SettingsNav.Content = "\uE713";
        SettingsNav.FontFamily = new FontFamily("Segoe MDL2 Assets");
        SettingsNav.FontSize = 18;
        SettingsNav.Width = 38;
        SettingsNav.Height = 38;
        SettingsNav.Padding = new Thickness(0);
        SettingsNav.Margin = new Thickness(0, 10, 0, 0);
        SettingsNav.HorizontalAlignment = HorizontalAlignment.Left;
        SettingsNav.HorizontalContentAlignment = HorizontalAlignment.Center;
        SettingsNav.VerticalContentAlignment = VerticalAlignment.Center;
        SettingsNav.ToolTip = "Settings";

        Grid.SetRow(SettingsNav, settingsRow);
        sidebarGrid.Children.Add(SettingsNav);
    }
}
