using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private bool _updatePanelV061Initialized;

    private void EnsureUpdatePanelV061()
    {
        if (_updatePanelV061Initialized) return;
        _updatePanelV061Initialized = true;

        EnsureNotificationAndUpdateUi();

        TrayStateText.Visibility = Visibility.Collapsed;
        FooterCaptureText.Visibility = Visibility.Collapsed;

        if (TrayStateText.Parent is not StackPanel panel) return;
        Separator? divider = panel.Children.OfType<Separator>().FirstOrDefault();
        if (divider is not null)
            panel.Children.Remove(divider);
    }
}
