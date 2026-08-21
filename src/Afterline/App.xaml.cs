using System.Windows;
using System.Windows.Threading;

namespace Afterline;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(TryInitializeMainWindowEnhancements));
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        TryInitializeMainWindowEnhancements();
    }

    private void TryInitializeMainWindowEnhancements()
    {
        if (MainWindow is not Afterline.MainWindow window) return;

        if (window.IsLoaded)
        {
            window.EnsureLiveChatEnhancements();
            return;
        }

        window.Loaded -= MainWindow_LoadedForEnhancements;
        window.Loaded += MainWindow_LoadedForEnhancements;
    }

    private void MainWindow_LoadedForEnhancements(object sender, RoutedEventArgs e)
    {
        if (sender is not Afterline.MainWindow window) return;
        window.Loaded -= MainWindow_LoadedForEnhancements;
        window.EnsureLiveChatEnhancements();
    }
}
