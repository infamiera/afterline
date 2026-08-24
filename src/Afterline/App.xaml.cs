using System.Windows;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class App : System.Windows.Application
{
    private string[] _startupArgs = Array.Empty<string>();
    private DispatcherTimer? _completedUpdateCleanupTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        _startupArgs = e.Args.ToArray();
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        if (CanaryUpdateInstaller.TryRunUpdaterMode(e.Args) || UpdateService.TryRunUpdaterMode(e.Args))
        {
            Environment.Exit(0);
            return;
        }

        try
        {
            RetiredThemeGuard.EnsureUiFilter();
            var settings = new SettingsService().Load();
            ThemeService.Apply(settings.Theme);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to apply the saved theme during startup.", ex);
        }

        base.OnStartup(e);
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(TryInitializeMainWindowEnhancements));
    }

    internal void ConfirmHealthyStartup()
    {
        if (_completedUpdateCleanupTimer is not null ||
            !_startupArgs.Any(value => string.Equals(
                value,
                "--afterline-update-complete",
                StringComparison.OrdinalIgnoreCase)))
            return;

        _completedUpdateCleanupTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromSeconds(45)
        };
        _completedUpdateCleanupTimer.Tick += (_, _) =>
        {
            _completedUpdateCleanupTimer.Stop();
            UpdateService.CleanupCompletedUpdate(_startupArgs);
            DiagnosticLogger.Info("Afterline remained healthy after updating; the previous executable backup was cleared.");
        };
        _completedUpdateCleanupTimer.Start();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        DiagnosticLogger.Error(
            "Unhandled Afterline process exception. The previous update backup has been preserved when available.",
            e.ExceptionObject as Exception);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticLogger.Error(
            "Unhandled Afterline UI exception. The previous update backup has been preserved when available.",
            e.Exception);
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
